using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;
using Xunit;
using Xunit.Sdk;

namespace PrimeAxiom.Tests;

public sealed class HardwareValuationEncodingAdapterTests
{
    private static readonly int[] Widths = [4, 6, 8];

    [Fact]
    public void AdaptersExposeStableSameCapAcyclicNandGeometry()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            foreach (var first in AllAdapters(width))
            {
                var second = first.Ports.SourceEncoding == ExperimentalValuationEncoding.BinaryExponent
                    ? ValuationEncodingAdapterHardware.BuildBinaryExponentToThermometer(width)
                    : ValuationEncodingAdapterHardware.BuildThermometerToBinaryExponent(width);
                Assert.Equal("STRUCTURAL_DECLARED", first.EvidenceClass);
                Assert.Equal(first.Netlist.Nodes, second.Netlist.Nodes);
                Assert.Equal(first.Netlist.NamedOutputs, second.Netlist.NamedOutputs);
                Assert.Equal(CombinationalLoopStatus.Acyclic, first.Netlist.Metrics.CombinationalLoopStatus);
                Assert.True(first.Netlist.Metrics.Nand2Static > 0);
                Assert.Equal(0, first.Netlist.Metrics.DffStatic);
                Assert.Equal(0, first.Netlist.Metrics.StateBits);
                Assert.Equal(Enumerable.Range(0, first.Netlist.Nodes.Count), first.Netlist.Nodes.Select(node => node.Id));
                Assert.Equal(domain.Caps, first.Ports.Lanes.Select(lane => lane.Cap));
                Assert.Equal(ValuationHardwareDomain.S4, first.Ports.Lanes.Select(lane => lane.Prime));
                Assert.NotEqual(first.Ports.SourceEncoding, first.Ports.ResultEncoding);
                AssertPortsMatchGraph(first);

                foreach (var lane in first.Ports.Lanes)
                {
                    var expectedSourceWidth = first.Ports.SourceEncoding == ExperimentalValuationEncoding.BinaryExponent
                        ? BinaryWidth(lane.Cap)
                        : lane.Cap;
                    var expectedResultWidth = first.Ports.ResultEncoding == ExperimentalValuationEncoding.BinaryExponent
                        ? BinaryWidth(lane.Cap)
                        : lane.Cap;
                    Assert.Equal(expectedSourceWidth, lane.SourcePayloadInputs.Count);
                    Assert.Equal(expectedResultWidth, lane.ResultPayloadOutputs.Count);
                    Assert.Contains(
                        first.Netlist.Nodes,
                        node => node.Region.Split('/').Contains($"lane:{lane.Lane}", StringComparer.Ordinal));
                }
            }
        }

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ValuationEncodingAdapterHardware.BuildBinaryExponentToThermometer(5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ValuationEncodingAdapterHardware.BuildThermometerToBinaryExponent(16));
    }

    [Fact]
    public void BinaryToThermometerExhaustsEveryRawPayloadZeroAndSaturationCombination()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var declared = ValuationEncodingAdapterHardware.BuildBinaryExponentToThermometer(width);
            var evaluator = new FastNandEvaluator(declared.Netlist);
            var payloadWidth = declared.Ports.Lanes.Sum(lane => lane.SourcePayloadInputs.Count);
            for (var packed = 0; packed < 1 << payloadWidth; packed++)
            {
                var exponents = UnpackBinary(declared.Ports.Lanes, packed);
                var payloadValid = Enumerable.Range(0, domain.LaneCount)
                    .All(lane => exponents[lane] <= domain.CapAt(lane));
                for (var zeroTag = 0; zeroTag <= 1; zeroTag++)
                {
                    for (var saturationMask = 0; saturationMask < 1 << domain.LaneCount; saturationMask++)
                    {
                        evaluator.Reset();
                        SetBinarySource(evaluator, declared.Ports, packed, zeroTag != 0, saturationMask);
                        evaluator.Run();
                        var saturationValid = Enumerable.Range(0, domain.LaneCount)
                            .All(lane => ((saturationMask >> lane) & 1) == 0 ||
                                         exponents[lane] == domain.CapAt(lane));
                        var zeroValid = zeroTag == 0 ||
                                        packed == 0 && saturationMask == 0;
                        var accepted = payloadValid && saturationValid && zeroValid;
                        AssertResult(
                            evaluator,
                            declared.Ports,
                            accepted,
                            zeroTag != 0,
                            exponents,
                            saturationMask,
                            $"bin->therm W={width} packed={packed} zero={zeroTag} sat={saturationMask}");
                    }
                }
            }
        }
    }

    [Fact]
    public void ThermometerToBinaryExhaustsEveryRawPayloadAndZeroWithoutSaturation()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var declared = ValuationEncodingAdapterHardware.BuildThermometerToBinaryExponent(width);
            var evaluator = new FastNandEvaluator(declared.Netlist);
            var payloadWidth = declared.Ports.Lanes.Sum(lane => lane.SourcePayloadInputs.Count);
            for (var packed = 0; packed < 1 << payloadWidth; packed++)
            {
                var thresholds = UnpackThermometer(declared.Ports.Lanes, packed);
                var payloadValid = thresholds.All(IsCanonicalThermometer);
                var exponents = thresholds.Select(lane => lane.Count(bit => bit)).ToArray();
                for (var zeroTag = 0; zeroTag <= 1; zeroTag++)
                {
                    evaluator.Reset();
                    SetThermometerRawSource(evaluator, declared.Ports, packed, zeroTag != 0, saturationMask: 0);
                    evaluator.Run();
                    var zeroValid = zeroTag == 0 || packed == 0;
                    AssertResult(
                        evaluator,
                        declared.Ports,
                        payloadValid && zeroValid,
                        zeroTag != 0,
                        exponents,
                        saturationMask: 0,
                        $"therm->bin raw W={width} packed={packed} zero={zeroTag}");
                }
            }
        }
    }

    [Fact]
    public void ThermometerToBinaryExhaustsEverySaturationMaskOverLegalPayloads()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var declared = ValuationEncodingAdapterHardware.BuildThermometerToBinaryExponent(width);
            var evaluator = new FastNandEvaluator(declared.Netlist);
            foreach (var exponents in ExponentVectors(domain))
            {
                for (var saturationMask = 0; saturationMask < 1 << domain.LaneCount; saturationMask++)
                {
                    evaluator.Reset();
                    SetThermometerSource(evaluator, declared.Ports, exponents, isZero: false, saturationMask);
                    evaluator.Run();
                    var saturationValid = Enumerable.Range(0, domain.LaneCount)
                        .All(lane => ((saturationMask >> lane) & 1) == 0 ||
                                     exponents[lane] == domain.CapAt(lane));
                    AssertResult(
                        evaluator,
                        declared.Ports,
                        saturationValid,
                        sourceZero: false,
                        exponents,
                        saturationMask,
                        $"therm->bin legal W={width} exp=[{string.Join(',', exponents)}] sat={saturationMask}");
                }
            }

            var identity = new int[domain.LaneCount];
            for (var saturationMask = 0; saturationMask < 1 << domain.LaneCount; saturationMask++)
            {
                evaluator.Reset();
                SetThermometerSource(evaluator, declared.Ports, identity, isZero: true, saturationMask);
                evaluator.Run();
                AssertResult(
                    evaluator,
                    declared.Ports,
                    accepted: saturationMask == 0,
                    sourceZero: true,
                    identity,
                    saturationMask,
                    $"therm->bin zero W={width} sat={saturationMask}");
            }
        }
    }

    [Fact]
    public void RoundTripExhaustsEveryCanonicalExactAndSaturatedState()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var toThermometer = ValuationEncodingAdapterHardware.BuildBinaryExponentToThermometer(width);
            var toBinary = ValuationEncodingAdapterHardware.BuildThermometerToBinaryExponent(width);
            var binaryEvaluator = new FastNandEvaluator(toThermometer.Netlist);
            var thermometerEvaluator = new FastNandEvaluator(toBinary.Netlist);
            var cases = 0;
            foreach (var exponents in ExponentVectors(domain))
            {
                for (var saturationMask = 0; saturationMask < 1 << domain.LaneCount; saturationMask++)
                {
                    if (Enumerable.Range(0, domain.LaneCount)
                        .Any(lane => ((saturationMask >> lane) & 1) != 0 &&
                                     exponents[lane] != domain.CapAt(lane)))
                    {
                        continue;
                    }

                    binaryEvaluator.Reset();
                    SetBinarySource(binaryEvaluator, toThermometer.Ports, exponents, isZero: false, saturationMask);
                    binaryEvaluator.Run();
                    Require(binaryEvaluator.Read(toThermometer.Ports.AcceptedOutput), "round-trip first adapter rejected");

                    thermometerEvaluator.Reset();
                    CopyResultToSource(
                        binaryEvaluator,
                        toThermometer.Ports,
                        thermometerEvaluator,
                        toBinary.Ports);
                    thermometerEvaluator.Run();
                    AssertResult(
                        thermometerEvaluator,
                        toBinary.Ports,
                        accepted: true,
                        sourceZero: false,
                        exponents,
                        saturationMask,
                        $"round-trip W={width} exp=[{string.Join(',', exponents)}] sat={saturationMask}");
                    cases++;
                }
            }

            binaryEvaluator.Reset();
            SetBinarySource(
                binaryEvaluator,
                toThermometer.Ports,
                new int[domain.LaneCount],
                isZero: true,
                saturationMask: 0);
            binaryEvaluator.Run();
            thermometerEvaluator.Reset();
            CopyResultToSource(
                binaryEvaluator,
                toThermometer.Ports,
                thermometerEvaluator,
                toBinary.Ports);
            thermometerEvaluator.Run();
            AssertResult(
                thermometerEvaluator,
                toBinary.Ports,
                accepted: true,
                sourceZero: true,
                new int[domain.LaneCount],
                saturationMask: 0,
                $"round-trip zero W={width}");
            Assert.True(cases > 0);
        }
    }

    [Fact]
    public void PublicEvaluationCapturesSettledTransitionsAcrossBothDirections()
    {
        foreach (var declared in Widths.SelectMany(AllAdapters))
        {
            var inputs = InputStates(declared.Netlist);
            var identity = declared.Netlist.Evaluate(inputs, compareWithAllOff: true);
            Assert.Equal(BitState.On, identity.Outputs[declared.Ports.AcceptedOutput]);
            Assert.Equal(BitState.Off, identity.Outputs[declared.Ports.MalformedOutput]);

            inputs[declared.Ports.SourceZeroInput] = BitState.On;
            var zero = declared.Netlist.Evaluate(inputs, previous: identity);
            Assert.Equal(1, zero.InputTransitions);
            Assert.True(zero.NandOutputTransitions > 0);
            Assert.Equal(BitState.On, zero.Outputs[declared.Ports.ResultZeroOutput]);
            Assert.Equal(declared.Netlist.Metrics.Nand2Static, zero.NandEvaluations);
        }
    }

    private static IEnumerable<DeclaredValuationEncodingAdapterCircuit> AllAdapters(int width)
    {
        yield return ValuationEncodingAdapterHardware.BuildBinaryExponentToThermometer(width);
        yield return ValuationEncodingAdapterHardware.BuildThermometerToBinaryExponent(width);
    }

    private static void AssertPortsMatchGraph(DeclaredValuationEncodingAdapterCircuit declared)
    {
        var inputs = declared.Netlist.Nodes
            .Where(node => node.Kind == NandNodeKind.Input)
            .Select(node => node.Name)
            .ToHashSet(StringComparer.Ordinal);
        var outputs = declared.Netlist.NamedOutputs
            .Select(output => output.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(declared.Ports.SourceZeroInput, inputs);
        Assert.Contains(declared.Ports.ResultZeroOutput, outputs);
        Assert.Contains(declared.Ports.AcceptedOutput, outputs);
        Assert.Contains(declared.Ports.MalformedOutput, outputs);
        foreach (var lane in declared.Ports.Lanes)
        {
            Assert.All(lane.SourcePayloadInputs, name => Assert.Contains(name, inputs));
            Assert.Contains(lane.SourceSaturationInput, inputs);
            Assert.All(lane.ResultPayloadOutputs, name => Assert.Contains(name, outputs));
            Assert.Contains(lane.ResultSaturationOutput, outputs);
        }
    }

    private static void AssertResult(
        FastNandEvaluator evaluator,
        ValuationEncodingAdapterPortLayout ports,
        bool accepted,
        bool sourceZero,
        IReadOnlyList<int> exponents,
        int saturationMask,
        string context)
    {
        Require(evaluator.Read(ports.AcceptedOutput) == accepted, context + " accepted");
        Require(evaluator.Read(ports.MalformedOutput) == !accepted, context + " malformed");
        Require(evaluator.Read(ports.ResultZeroOutput) == (accepted && sourceZero), context + " zero");
        foreach (var lane in ports.Lanes)
        {
            for (var bit = 0; bit < lane.ResultPayloadOutputs.Count; bit++)
            {
                var expected = accepted && !sourceZero &&
                               (ports.ResultEncoding == ExperimentalValuationEncoding.BinaryExponent
                                   ? ((exponents[lane.Lane] >> bit) & 1) != 0
                                   : exponents[lane.Lane] >= bit + 1);
                Require(
                    evaluator.Read(lane.ResultPayloadOutputs[bit]) == expected,
                    context + $" lane={lane.Lane} bit={bit}");
            }

            var expectedSaturation = accepted && !sourceZero &&
                                     ((saturationMask >> lane.Lane) & 1) != 0;
            Require(
                evaluator.Read(lane.ResultSaturationOutput) == expectedSaturation,
                context + $" lane={lane.Lane} saturation");
        }
    }

    private static void SetBinarySource(
        FastNandEvaluator evaluator,
        ValuationEncodingAdapterPortLayout ports,
        int packed,
        bool isZero,
        int saturationMask)
    {
        evaluator.Set(ports.SourceZeroInput, isZero);
        var offset = 0;
        foreach (var lane in ports.Lanes)
        {
            for (var bit = 0; bit < lane.SourcePayloadInputs.Count; bit++)
            {
                evaluator.Set(lane.SourcePayloadInputs[bit], ((packed >> (offset + bit)) & 1) != 0);
            }

            evaluator.Set(lane.SourceSaturationInput, ((saturationMask >> lane.Lane) & 1) != 0);
            offset += lane.SourcePayloadInputs.Count;
        }
    }

    private static void SetBinarySource(
        FastNandEvaluator evaluator,
        ValuationEncodingAdapterPortLayout ports,
        IReadOnlyList<int> exponents,
        bool isZero,
        int saturationMask)
    {
        evaluator.Set(ports.SourceZeroInput, isZero);
        foreach (var lane in ports.Lanes)
        {
            SetUnsigned(evaluator, lane.SourcePayloadInputs, exponents[lane.Lane]);
            evaluator.Set(lane.SourceSaturationInput, ((saturationMask >> lane.Lane) & 1) != 0);
        }
    }

    private static void SetThermometerRawSource(
        FastNandEvaluator evaluator,
        ValuationEncodingAdapterPortLayout ports,
        int packed,
        bool isZero,
        int saturationMask)
    {
        evaluator.Set(ports.SourceZeroInput, isZero);
        var offset = 0;
        foreach (var lane in ports.Lanes)
        {
            for (var bit = 0; bit < lane.SourcePayloadInputs.Count; bit++)
            {
                evaluator.Set(lane.SourcePayloadInputs[bit], ((packed >> (offset + bit)) & 1) != 0);
            }

            evaluator.Set(lane.SourceSaturationInput, ((saturationMask >> lane.Lane) & 1) != 0);
            offset += lane.SourcePayloadInputs.Count;
        }
    }

    private static void SetThermometerSource(
        FastNandEvaluator evaluator,
        ValuationEncodingAdapterPortLayout ports,
        IReadOnlyList<int> exponents,
        bool isZero,
        int saturationMask)
    {
        evaluator.Set(ports.SourceZeroInput, isZero);
        foreach (var lane in ports.Lanes)
        {
            for (var threshold = 1; threshold <= lane.SourcePayloadInputs.Count; threshold++)
            {
                evaluator.Set(
                    lane.SourcePayloadInputs[threshold - 1],
                    exponents[lane.Lane] >= threshold);
            }

            evaluator.Set(lane.SourceSaturationInput, ((saturationMask >> lane.Lane) & 1) != 0);
        }
    }

    private static void CopyResultToSource(
        FastNandEvaluator sourceEvaluator,
        ValuationEncodingAdapterPortLayout sourcePorts,
        FastNandEvaluator destinationEvaluator,
        ValuationEncodingAdapterPortLayout destinationPorts)
    {
        destinationEvaluator.Set(
            destinationPorts.SourceZeroInput,
            sourceEvaluator.Read(sourcePorts.ResultZeroOutput));
        for (var lane = 0; lane < sourcePorts.Lanes.Count; lane++)
        {
            for (var bit = 0; bit < sourcePorts.Lanes[lane].ResultPayloadOutputs.Count; bit++)
            {
                destinationEvaluator.Set(
                    destinationPorts.Lanes[lane].SourcePayloadInputs[bit],
                    sourceEvaluator.Read(sourcePorts.Lanes[lane].ResultPayloadOutputs[bit]));
            }

            destinationEvaluator.Set(
                destinationPorts.Lanes[lane].SourceSaturationInput,
                sourceEvaluator.Read(sourcePorts.Lanes[lane].ResultSaturationOutput));
        }
    }

    private static int[] UnpackBinary(
        IReadOnlyList<ValuationEncodingAdapterLanePortLayout> lanes,
        int packed)
    {
        var exponents = new int[lanes.Count];
        var offset = 0;
        foreach (var lane in lanes)
        {
            var mask = (1 << lane.SourcePayloadInputs.Count) - 1;
            exponents[lane.Lane] = (packed >> offset) & mask;
            offset += lane.SourcePayloadInputs.Count;
        }

        return exponents;
    }

    private static bool[][] UnpackThermometer(
        IReadOnlyList<ValuationEncodingAdapterLanePortLayout> lanes,
        int packed)
    {
        var result = new bool[lanes.Count][];
        var offset = 0;
        foreach (var lane in lanes)
        {
            result[lane.Lane] = new bool[lane.SourcePayloadInputs.Count];
            for (var bit = 0; bit < result[lane.Lane].Length; bit++)
            {
                result[lane.Lane][bit] = ((packed >> (offset + bit)) & 1) != 0;
            }

            offset += lane.SourcePayloadInputs.Count;
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

    private static IEnumerable<int[]> ExponentVectors(ValuationHardwareDomain domain)
    {
        var current = new int[domain.LaneCount];
        return Visit(0);

        IEnumerable<int[]> Visit(int lane)
        {
            if (lane == domain.LaneCount)
            {
                yield return (int[])current.Clone();
                yield break;
            }

            for (var exponent = 0; exponent <= domain.CapAt(lane); exponent++)
            {
                current[lane] = exponent;
                foreach (var vector in Visit(lane + 1))
                {
                    yield return vector;
                }
            }
        }
    }

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

    private static int BinaryWidth(int cap)
    {
        var width = 1;
        while ((1 << width) <= cap)
        {
            width++;
        }

        return width;
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
