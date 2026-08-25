using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;
using Xunit.Sdk;

namespace PrimeAxiom.Tests;

public sealed class HardwareRepresentationAdapterTests
{
    private static readonly int[] Widths = [4, 6, 8];

    [Fact]
    public void DeclaredAdaptersExposeStableAcyclicNandGeometryAndPorts()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var firstEncoder = RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(width);
            var secondEncoder = RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(width);
            AssertDeclaredGraph(firstEncoder.Netlist, firstEncoder.EvidenceClass);
            Assert.Equal(firstEncoder.Netlist.Nodes, secondEncoder.Netlist.Nodes);
            Assert.Equal(firstEncoder.Netlist.NamedOutputs, secondEncoder.Netlist.NamedOutputs);
            Assert.Equal(width, firstEncoder.Ports.MagnitudeInputs.Count);
            Assert.Equal(domain.Caps, firstEncoder.Ports.Lanes.Select(lane => lane.Cap));
            Assert.Equal(
                ExpectedBinaryWidths(width),
                firstEncoder.Ports.Lanes.Select(lane => lane.ExponentOutputs.Count));
            AssertPortsExist(
                firstEncoder.Netlist,
                firstEncoder.Ports.MagnitudeInputs,
                firstEncoder.Ports.Lanes.SelectMany(lane => lane.ExponentOutputs)
                    .Append(firstEncoder.Ports.ZeroOutput)
                    .Append(firstEncoder.Ports.SupportedOutput)
                    .Append(firstEncoder.Ports.SmoothOutput));

            var firstDecoder = RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(width);
            var secondDecoder = RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(width);
            AssertDeclaredGraph(firstDecoder.Netlist, firstDecoder.EvidenceClass);
            Assert.Equal(firstDecoder.Netlist.Nodes, secondDecoder.Netlist.Nodes);
            Assert.Equal(firstDecoder.Netlist.NamedOutputs, secondDecoder.Netlist.NamedOutputs);
            Assert.Equal(width, firstDecoder.Ports.MagnitudeOutputs.Count);
            Assert.Equal(domain.Caps, firstDecoder.Ports.Lanes.Select(lane => lane.Cap));
            Assert.Equal(
                ExpectedBinaryWidths(width),
                firstDecoder.Ports.Lanes.Select(lane => lane.ExponentInputs.Count));
            AssertPortsExist(
                firstDecoder.Netlist,
                firstDecoder.Ports.Lanes.SelectMany(lane => lane.ExponentInputs)
                    .Concat(firstDecoder.Ports.Lanes.Select(lane => lane.SaturationInput))
                    .Append(firstDecoder.Ports.ZeroInput),
                firstDecoder.Ports.MagnitudeOutputs
                    .Append(firstDecoder.Ports.AcceptedOutput)
                    .Append(firstDecoder.Ports.OverflowOutput)
                    .Append(firstDecoder.Ports.MalformedOutput));

            foreach (var presence in new[]
                     {
                         RepresentationAdapterHardware.BuildBinaryExponentPresence(width),
                         RepresentationAdapterHardware.BuildThermometerPresence(width),
                     })
            {
                AssertDeclaredGraph(presence.Netlist, presence.EvidenceClass);
                Assert.Equal(domain.Caps, presence.Ports.Lanes.Select(lane => lane.Cap));
                AssertPortsExist(
                    presence.Netlist,
                    presence.Ports.Lanes.SelectMany(lane => lane.PayloadInputs)
                        .Concat(presence.Ports.Lanes.Select(lane => lane.SaturationInput))
                        .Append(presence.Ports.ZeroInput),
                    presence.Ports.Lanes.Select(lane => lane.PresenceOutput)
                        .Append(presence.Ports.AcceptedOutput)
                        .Append(presence.Ports.MalformedOutput));
            }
        }

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(16));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RepresentationAdapterHardware.BuildBinaryExponentPresence(2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RepresentationAdapterHardware.BuildThermometerPresence(3));
    }

    [Fact]
    public void ColdEncoderExhaustsEveryMagnitudeAndRetainsExactS4Valuations()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var declared = RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(width);
            var evaluator = new FastNandEvaluator(declared.Netlist);
            var unsupportedSeen = 0;
            var smoothSeen = 0;
            for (var magnitude = 0; magnitude <= domain.MaximumMagnitude; magnitude++)
            {
                evaluator.Reset();
                SetUnsigned(evaluator, declared.Ports.MagnitudeInputs, magnitude);
                evaluator.Run();
                var expected = Factor(domain, magnitude);
                var context = $"encoder W={width} magnitude={magnitude}";
                Require(evaluator.Read(declared.Ports.ZeroOutput) == (magnitude == 0), context + " zero");
                Require(evaluator.Read(declared.Ports.SmoothOutput) == expected.Smooth, context + " smooth");
                Require(
                    evaluator.Read(declared.Ports.SupportedOutput) == (magnitude == 0 || expected.Smooth),
                    context + " supported");
                for (var lane = 0; lane < domain.LaneCount; lane++)
                {
                    Require(
                        ReadUnsigned(evaluator, declared.Ports.Lanes[lane].ExponentOutputs) ==
                        expected.Exponents[lane],
                        context + $" lane={lane}");
                }

                if (expected.Smooth)
                {
                    smoothSeen++;
                }
                else if (magnitude != 0)
                {
                    unsupportedSeen++;
                }
            }

            Assert.True(smoothSeen > 0);
            Assert.True(unsupportedSeen > 0);
        }
    }

    [Fact]
    public void DecoderExhaustsRawPayloadsAndSeparatesMalformedOverflowAndAccepted()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var declared = RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(width);
            var evaluator = new FastNandEvaluator(declared.Netlist);
            var payloadWidth = declared.Ports.Lanes.Sum(lane => lane.ExponentInputs.Count);
            var legalVectors = 0;
            var overCapVectors = 0;
            var overflowVectors = 0;
            var noncanonicalZeroVectors = 0;
            for (var packed = 0; packed < 1 << payloadWidth; packed++)
            {
                var exponents = UnpackExponents(declared.Ports.Lanes, packed);
                var legal = Enumerable.Range(0, domain.LaneCount)
                    .All(lane => exponents[lane] <= domain.CapAt(lane));
                var product = legal ? Product(domain, exponents) : 0;
                for (var zeroTag = 0; zeroTag <= 1; zeroTag++)
                {
                    evaluator.Reset();
                    SetDecoderPayload(evaluator, declared.Ports, packed, zeroTag != 0);
                    evaluator.Run();
                    var payloadNonzero = exponents.Any(exponent => exponent != 0);
                    var malformed = !legal || (zeroTag != 0 && payloadNonzero);
                    var overflow = !malformed && zeroTag == 0 && product > domain.MaximumMagnitude;
                    var accepted = !malformed && !overflow;
                    var expectedMagnitude = accepted && zeroTag == 0 ? (int)product : 0;
                    var context = $"decoder W={width} packed={packed} zero={zeroTag}";
                    Require(evaluator.Read(declared.Ports.MalformedOutput) == malformed, context + " malformed");
                    Require(evaluator.Read(declared.Ports.OverflowOutput) == overflow, context + " overflow");
                    Require(evaluator.Read(declared.Ports.AcceptedOutput) == accepted, context + " accepted");
                    Require(
                        ReadUnsigned(evaluator, declared.Ports.MagnitudeOutputs) == expectedMagnitude,
                        context + " magnitude");
                    Require(
                        (malformed ? 1 : 0) + (overflow ? 1 : 0) + (accepted ? 1 : 0) == 1,
                        context + " status partition");

                    if (zeroTag == 0)
                    {
                        if (legal)
                        {
                            legalVectors++;
                        }
                        else
                        {
                            overCapVectors++;
                        }

                        if (overflow)
                        {
                            overflowVectors++;
                        }
                    }
                    else if (legal && payloadNonzero)
                    {
                        noncanonicalZeroVectors++;
                    }
                }
            }

            Assert.Equal(domain.Caps.Aggregate(1, (count, cap) => count * (cap + 1)), legalVectors);
            Assert.True(overCapVectors > 0);
            Assert.True(overflowVectors > 0);
            Assert.Equal(legalVectors - 1, noncanonicalZeroVectors);

            for (var lane = 0; lane < domain.LaneCount; lane++)
            {
                foreach (var zeroTag in new[] { false, true })
                {
                    evaluator.Reset();
                    SetDecoderPayload(evaluator, declared.Ports, packed: 0, zeroTag);
                    evaluator.Set(declared.Ports.Lanes[lane].SaturationInput, true);
                    evaluator.Run();
                    var context = $"decoder W={width} saturated lane={lane} zero={zeroTag}";
                    Require(evaluator.Read(declared.Ports.MalformedOutput), context + " malformed");
                    Require(!evaluator.Read(declared.Ports.OverflowOutput), context + " overflow");
                    Require(!evaluator.Read(declared.Ports.AcceptedOutput), context + " accepted");
                    Require(ReadUnsigned(evaluator, declared.Ports.MagnitudeOutputs) == 0, context + " magnitude");
                }
            }
        }
    }

    [Fact]
    public void EncoderDecoderRoundTripIsExactOnItsDeclaredCommonDomain()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var encoder = RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(width);
            var decoder = RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(width);
            var encode = new FastNandEvaluator(encoder.Netlist);
            var decode = new FastNandEvaluator(decoder.Netlist);
            var roundTrips = 0;
            var unsupported = 0;
            for (var magnitude = 0; magnitude <= domain.MaximumMagnitude; magnitude++)
            {
                encode.Reset();
                SetUnsigned(encode, encoder.Ports.MagnitudeInputs, magnitude);
                encode.Run();
                if (!encode.Read(encoder.Ports.SupportedOutput))
                {
                    unsupported++;
                    continue;
                }

                decode.Reset();
                decode.Set(decoder.Ports.ZeroInput, encode.Read(encoder.Ports.ZeroOutput));
                for (var lane = 0; lane < domain.LaneCount; lane++)
                {
                    var exponent = ReadUnsigned(encode, encoder.Ports.Lanes[lane].ExponentOutputs);
                    SetUnsigned(decode, decoder.Ports.Lanes[lane].ExponentInputs, exponent);
                    decode.Set(decoder.Ports.Lanes[lane].SaturationInput, false);
                }

                decode.Run();
                var context = $"round-trip W={width} magnitude={magnitude}";
                Require(decode.Read(decoder.Ports.AcceptedOutput), context + " accepted");
                Require(!decode.Read(decoder.Ports.MalformedOutput), context + " malformed");
                Require(!decode.Read(decoder.Ports.OverflowOutput), context + " overflow");
                Require(ReadUnsigned(decode, decoder.Ports.MagnitudeOutputs) == magnitude, context + " magnitude");
                roundTrips++;
            }

            Assert.True(roundTrips > 0);
            Assert.True(unsupported > 0);
        }
    }

    [Fact]
    public void BinaryPresenceProjectionExhaustsEveryRawPayloadAndZeroTag()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var declared = RepresentationAdapterHardware.BuildBinaryExponentPresence(width);
            var evaluator = new FastNandEvaluator(declared.Netlist);
            var payloadWidth = declared.Ports.Lanes.Sum(lane => lane.PayloadInputs.Count);
            for (var packed = 0; packed < 1 << payloadWidth; packed++)
            {
                var exponents = UnpackPresenceExponents(declared.Ports.Lanes, packed);
                var legal = Enumerable.Range(0, domain.LaneCount)
                    .All(lane => exponents[lane] <= domain.CapAt(lane));
                for (var zeroTag = 0; zeroTag <= 1; zeroTag++)
                {
                    evaluator.Reset();
                    SetPresencePayload(evaluator, declared.Ports, packed, zeroTag != 0);
                    evaluator.Run();
                    var malformed = !legal || (zeroTag != 0 && exponents.Any(value => value != 0));
                    var context = $"binary presence W={width} packed={packed} zero={zeroTag}";
                    Require(evaluator.Read(declared.Ports.MalformedOutput) == malformed, context + " malformed");
                    Require(evaluator.Read(declared.Ports.AcceptedOutput) == !malformed, context + " accepted");
                    for (var lane = 0; lane < domain.LaneCount; lane++)
                    {
                        Require(
                            evaluator.Read(declared.Ports.Lanes[lane].PresenceOutput) ==
                            (!malformed && zeroTag == 0 && exponents[lane] > 0),
                            context + $" lane={lane}");
                    }
                }
            }

            AssertSaturationRejected(declared, evaluator);
        }
    }

    [Fact]
    public void ThermometerPresenceProjectionExhaustsEveryRawPayloadAndZeroTag()
    {
        foreach (var width in Widths)
        {
            var declared = RepresentationAdapterHardware.BuildThermometerPresence(width);
            var evaluator = new FastNandEvaluator(declared.Netlist);
            var payloadWidth = declared.Ports.Lanes.Sum(lane => lane.PayloadInputs.Count);
            for (var packed = 0; packed < 1 << payloadWidth; packed++)
            {
                var lanes = UnpackThermometer(declared.Ports.Lanes, packed);
                var canonical = lanes.All(IsCanonicalThermometer);
                var payloadNonzero = lanes.SelectMany(bits => bits).Any(value => value);
                for (var zeroTag = 0; zeroTag <= 1; zeroTag++)
                {
                    evaluator.Reset();
                    SetPresencePayload(evaluator, declared.Ports, packed, zeroTag != 0);
                    evaluator.Run();
                    var malformed = !canonical || (zeroTag != 0 && payloadNonzero);
                    var context = $"thermometer presence W={width} packed={packed} zero={zeroTag}";
                    Require(evaluator.Read(declared.Ports.MalformedOutput) == malformed, context + " malformed");
                    Require(evaluator.Read(declared.Ports.AcceptedOutput) == !malformed, context + " accepted");
                    for (var lane = 0; lane < lanes.Length; lane++)
                    {
                        Require(
                            evaluator.Read(declared.Ports.Lanes[lane].PresenceOutput) ==
                            (!malformed && zeroTag == 0 && lanes[lane][0]),
                            context + $" lane={lane}");
                    }
                }
            }

            AssertSaturationRejected(declared, evaluator);
        }
    }

    [Fact]
    public void AdapterEvaluationCapturesSettledTransitionsAndOnlyUsesNandCells()
    {
        foreach (var width in Widths)
        {
            var encoder = RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(width);
            var allOffInputs = InputStates(encoder.Netlist);
            var first = encoder.Netlist.Evaluate(allOffInputs, compareWithAllOff: true);
            var nextInputs = InputStates(encoder.Netlist);
            foreach (var name in encoder.Ports.MagnitudeInputs)
            {
                nextInputs[name] = BitState.On;
            }

            var second = encoder.Netlist.Evaluate(nextInputs, previous: first);
            Assert.Equal(width, second.InputTransitions);
            Assert.True(second.NandOutputTransitions > 0);
            Assert.Equal(encoder.Netlist.Metrics.Nand2Static, second.NandEvaluations);
            Assert.All(
                encoder.Netlist.Nodes.Where(node => node.Kind == NandNodeKind.Nand2),
                node =>
                {
                    Assert.NotNull(node.LeftId);
                    Assert.NotNull(node.RightId);
                });

            var decoder = RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(width);
            var decoderInputs = InputStates(decoder.Netlist);
            var identity = decoder.Netlist.Evaluate(decoderInputs, compareWithAllOff: true);
            decoderInputs[decoder.Ports.Lanes[0].ExponentInputs[0]] = BitState.On;
            var two = decoder.Netlist.Evaluate(decoderInputs, previous: identity);
            Assert.Equal(1, two.InputTransitions);
            Assert.True(two.NandOutputTransitions > 0);
            Assert.Equal(BitState.On, two.Outputs[decoder.Ports.AcceptedOutput]);
            Assert.Equal(2, ReadUnsigned(two, decoder.Ports.MagnitudeOutputs));
        }
    }

    private static void AssertDeclaredGraph(NandNetlist netlist, string evidenceClass)
    {
        Assert.Equal("STRUCTURAL_DECLARED", evidenceClass);
        Assert.Equal(CombinationalLoopStatus.Acyclic, netlist.Metrics.CombinationalLoopStatus);
        Assert.True(netlist.Metrics.Nand2Static > 0);
        Assert.Equal(0, netlist.Metrics.DffStatic);
        Assert.Equal(0, netlist.Metrics.StateBits);
        Assert.Equal(Enumerable.Range(0, netlist.Nodes.Count), netlist.Nodes.Select(node => node.Id));
        Assert.Equal(netlist.Nodes.Count, netlist.Nodes.Select(node => node.Name).Distinct().Count());
    }

    private static void AssertPortsExist(
        NandNetlist netlist,
        IEnumerable<string> inputNames,
        IEnumerable<string> outputNames)
    {
        var actualInputs = netlist.Nodes
            .Where(node => node.Kind == NandNodeKind.Input)
            .Select(node => node.Name)
            .ToHashSet(StringComparer.Ordinal);
        var actualOutputs = netlist.NamedOutputs
            .Select(output => output.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(inputNames, name => Assert.Contains(name, actualInputs));
        Assert.All(outputNames, name => Assert.Contains(name, actualOutputs));
    }

    private static void AssertSaturationRejected(
        DeclaredPresenceAdapterCircuit declared,
        FastNandEvaluator evaluator)
    {
        foreach (var lane in declared.Ports.Lanes)
        {
            foreach (var zeroTag in new[] { false, true })
            {
                evaluator.Reset();
                SetPresencePayload(evaluator, declared.Ports, packed: 0, zeroTag);
                evaluator.Set(lane.SaturationInput, true);
                evaluator.Run();
                Require(evaluator.Read(declared.Ports.MalformedOutput), "saturation malformed");
                Require(!evaluator.Read(declared.Ports.AcceptedOutput), "saturation accepted");
                Require(
                    declared.Ports.Lanes.All(item => !evaluator.Read(item.PresenceOutput)),
                    "saturation presence not cleared");
            }
        }
    }

    private static void SetDecoderPayload(
        FastNandEvaluator evaluator,
        MagnitudeDecoderPortLayout ports,
        int packed,
        bool zeroTag)
    {
        evaluator.Set(ports.ZeroInput, zeroTag);
        var offset = 0;
        foreach (var lane in ports.Lanes)
        {
            for (var bit = 0; bit < lane.ExponentInputs.Count; bit++)
            {
                evaluator.Set(lane.ExponentInputs[bit], ((packed >> (offset + bit)) & 1) != 0);
            }

            evaluator.Set(lane.SaturationInput, false);
            offset += lane.ExponentInputs.Count;
        }
    }

    private static void SetPresencePayload(
        FastNandEvaluator evaluator,
        PresenceAdapterPortLayout ports,
        int packed,
        bool zeroTag)
    {
        evaluator.Set(ports.ZeroInput, zeroTag);
        var offset = 0;
        foreach (var lane in ports.Lanes)
        {
            for (var bit = 0; bit < lane.PayloadInputs.Count; bit++)
            {
                evaluator.Set(lane.PayloadInputs[bit], ((packed >> (offset + bit)) & 1) != 0);
            }

            evaluator.Set(lane.SaturationInput, false);
            offset += lane.PayloadInputs.Count;
        }
    }

    private static int[] UnpackExponents(
        IReadOnlyList<MagnitudeDecoderLanePortLayout> lanes,
        int packed)
    {
        var exponents = new int[lanes.Count];
        var offset = 0;
        foreach (var lane in lanes)
        {
            var mask = (1 << lane.ExponentInputs.Count) - 1;
            exponents[lane.Lane] = (packed >> offset) & mask;
            offset += lane.ExponentInputs.Count;
        }

        return exponents;
    }

    private static int[] UnpackPresenceExponents(
        IReadOnlyList<PresenceAdapterLanePortLayout> lanes,
        int packed)
    {
        var exponents = new int[lanes.Count];
        var offset = 0;
        foreach (var lane in lanes)
        {
            var mask = (1 << lane.PayloadInputs.Count) - 1;
            exponents[lane.Lane] = (packed >> offset) & mask;
            offset += lane.PayloadInputs.Count;
        }

        return exponents;
    }

    private static bool[][] UnpackThermometer(
        IReadOnlyList<PresenceAdapterLanePortLayout> lanes,
        int packed)
    {
        var result = new bool[lanes.Count][];
        var offset = 0;
        foreach (var lane in lanes)
        {
            result[lane.Lane] = new bool[lane.PayloadInputs.Count];
            for (var bit = 0; bit < lane.PayloadInputs.Count; bit++)
            {
                result[lane.Lane][bit] = ((packed >> (offset + bit)) & 1) != 0;
            }

            offset += lane.PayloadInputs.Count;
        }

        return result;
    }

    private static bool IsCanonicalThermometer(IReadOnlyList<bool> bits)
    {
        var falseSeen = false;
        foreach (var bit in bits)
        {
            if (!bit)
            {
                falseSeen = true;
            }
            else if (falseSeen)
            {
                return false;
            }
        }

        return true;
    }

    private static (int[] Exponents, bool Smooth) Factor(
        ValuationHardwareDomain domain,
        int magnitude)
    {
        if (magnitude == 0)
        {
            return (new int[domain.LaneCount], false);
        }

        var remainder = magnitude;
        var exponents = new int[domain.LaneCount];
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            var prime = domain.PrimeAt(lane);
            while (remainder % prime == 0)
            {
                exponents[lane]++;
                remainder /= prime;
            }
        }

        return (exponents, remainder == 1);
    }

    private static long Product(ValuationHardwareDomain domain, IReadOnlyList<int> exponents)
    {
        var product = 1L;
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            for (var exponent = 0; exponent < exponents[lane]; exponent++)
            {
                product *= domain.PrimeAt(lane);
            }
        }

        return product;
    }

    private static int[] ExpectedBinaryWidths(int width) => width switch
    {
        4 => [2, 2, 1, 1],
        6 => [3, 2, 2, 2],
        8 => [3, 3, 2, 2],
        _ => throw new ArgumentOutOfRangeException(nameof(width)),
    };

    private static void SetUnsigned(
        FastNandEvaluator evaluator,
        IReadOnlyList<string> names,
        int value)
    {
        for (var bit = 0; bit < names.Count; bit++)
        {
            evaluator.Set(names[bit], ((value >> bit) & 1) != 0);
        }
    }

    private static int ReadUnsigned(FastNandEvaluator evaluator, IReadOnlyList<string> names)
    {
        var value = 0;
        for (var bit = 0; bit < names.Count; bit++)
        {
            if (evaluator.Read(names[bit]))
            {
                value |= 1 << bit;
            }
        }

        return value;
    }

    private static int ReadUnsigned(NandEvaluation evaluation, IReadOnlyList<string> names)
    {
        var value = 0;
        for (var bit = 0; bit < names.Count; bit++)
        {
            if (evaluation.Outputs[names[bit]] == BitState.On)
            {
                value |= 1 << bit;
            }
        }

        return value;
    }

    private static Dictionary<string, BitState> InputStates(NandNetlist netlist) =>
        netlist.Nodes
            .Where(node => node.Kind == NandNodeKind.Input)
            .ToDictionary(node => node.Name, _ => BitState.Off, StringComparer.Ordinal);

    private static void Require(bool condition, string context)
    {
        if (!condition)
        {
            throw new XunitException(context);
        }
    }

    private sealed class FastNandEvaluator
    {
        private readonly NandNetlist _netlist;
        private readonly Dictionary<string, int> _inputs;
        private readonly Dictionary<string, int> _outputs;
        private readonly bool[] _values;

        public FastNandEvaluator(NandNetlist netlist)
        {
            _netlist = netlist;
            _inputs = netlist.Nodes
                .Where(node => node.Kind == NandNodeKind.Input)
                .ToDictionary(node => node.Name, node => node.Id, StringComparer.Ordinal);
            _outputs = netlist.NamedOutputs
                .ToDictionary(output => output.Name, output => output.NodeId, StringComparer.Ordinal);
            _values = new bool[netlist.Nodes.Count];
        }

        public void Reset() => Array.Clear(_values);

        public void Set(string name, bool value) => _values[_inputs[name]] = value;

        public bool Read(string name) => _values[_outputs[name]];

        public void Run()
        {
            foreach (var nodeId in _netlist.TopologicalNodeIds)
            {
                var node = _netlist.Nodes[nodeId];
                _values[nodeId] = node.Kind switch
                {
                    NandNodeKind.Input => _values[nodeId],
                    NandNodeKind.Constant or NandNodeKind.State => node.InitialValue == BitState.On,
                    NandNodeKind.Nand2 =>
                        !(_values[node.LeftId!.Value] && _values[node.RightId!.Value]),
                    _ => throw new InvalidOperationException($"Unknown NAND node kind {node.Kind}."),
                };
            }
        }
    }
}
