using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Tests;

public sealed class HardwareBinaryMagnitudeDatapathTests
{
    private static readonly int[] Widths = [4, 6, 8];

    private static readonly IReadOnlyDictionary<int, MetricExpectation> Metrics =
        new Dictionary<int, MetricExpectation>
        {
            [4] = new(1262, 4, 4, 9, 9, 18, 1277, 2537, 155, 146, 968, 0),
            [6] = new(2532, 6, 6, 11, 11, 22, 2551, 5081, 271, 276, 1900, 0),
            [8] = new(4242, 8, 8, 13, 13, 26, 4265, 8505, 411, 456, 3144, 0),
        };

    [Fact]
    public void LayoutCostsAndSharedControlBoundaryAreStableAndNandOnly()
    {
        foreach (var width in Widths)
        {
            var machine = BinaryMagnitudeDatapathHardware.Build(width);
            var again = BinaryMagnitudeDatapathHardware.Build(width);
            var sidecar = SidecarDatapathHardware.Build(width);
            Assert.Equal(machine.Netlist.Nodes, again.Netlist.Nodes);
            Assert.Equal(machine.Netlist.NamedOutputs, again.Netlist.NamedOutputs);
            Assert.Equal(machine.Netlist.DffBoundaries, again.Netlist.DffBoundaries);
            Assert.Equal("STRUCTURAL_DECLARED_INTEGRATED", machine.EvidenceClass);
            Assert.Equal(sidecar.Ports.OpcodeInputs, machine.Ports.OpcodeInputs);
            Assert.Equal(sidecar.Ports.PrimeSelectInputs, machine.Ports.PrimeSelectInputs);
            Assert.Equal(sidecar.Ports.OperandInputs, machine.Ports.OperandInputs);
            Assert.Equal(sidecar.Ports.StateMagnitudeNames, machine.Ports.StateMagnitudeNames);
            Assert.Equal(sidecar.Ports.MagnitudeOutputNames, machine.Ports.MagnitudeOutputNames);
            Assert.Equal((int)SidecarDatapathOperation.Load, (int)BinaryMagnitudeDatapathOperation.Load);
            Assert.Equal((int)SidecarDatapathOperation.Scale, (int)BinaryMagnitudeDatapathOperation.Scale);
            Assert.Equal((int)SidecarDatapathOperation.Cancel, (int)BinaryMagnitudeDatapathOperation.Cancel);
            Assert.Equal(
                (int)SidecarDatapathOperation.AddMagnitude,
                (int)BinaryMagnitudeDatapathOperation.AddMagnitude);
            AssertMetrics(machine.Netlist, Metrics[width]);
            Assert.All(
                machine.Netlist.Nodes.Where(node => node.Kind is not NandNodeKind.Input and
                    not NandNodeKind.State and not NandNodeKind.Constant),
                node => Assert.Equal(NandNodeKind.Nand2, node.Kind));
        }
    }

    [Fact]
    public void ScaleAndCancelExhaustEveryMagnitudePrimeAndWidth()
    {
        foreach (var width in Widths)
        {
            var machine = BinaryMagnitudeDatapathHardware.Build(width);
            var maximum = (1 << width) - 1;
            for (var magnitude = 0; magnitude <= maximum; magnitude++)
            {
                var state = BinaryMagnitudeDatapathHardware.EncodeState(machine, magnitude);
                foreach (var prime in ValuationHardwareDomain.S4)
                {
                    var scaled = machine.Netlist.Evaluate(
                        BinaryMagnitudeDatapathHardware.EncodeInputs(
                            machine,
                            BinaryMagnitudeDatapathOperation.Scale,
                            prime),
                        state);
                    var scaleOverflow = magnitude > maximum / prime;
                    AssertCurrent(machine, scaled, magnitude);
                    AssertStatus(machine, scaled, accepted: !scaleOverflow);
                    Assert.Equal(State(scaleOverflow), scaled.Outputs[machine.Ports.OverflowOutput]);
                    Assert.Equal(BitState.Off, scaled.Outputs[machine.Ports.NotDivisibleOutput]);
                    Assert.Equal(
                        scaleOverflow ? magnitude : magnitude * prime,
                        BinaryMagnitudeDatapathHardware.DecodeNextMagnitude(machine, scaled));
                    if (scaleOverflow)
                    {
                        AssertStateHeld(state, scaled.DffNextStates);
                    }

                    var cancelled = machine.Netlist.Evaluate(
                        BinaryMagnitudeDatapathHardware.EncodeInputs(
                            machine,
                            BinaryMagnitudeDatapathOperation.Cancel,
                            prime),
                        state);
                    var notDivisible = magnitude % prime != 0;
                    AssertCurrent(machine, cancelled, magnitude);
                    AssertStatus(machine, cancelled, accepted: !notDivisible);
                    Assert.Equal(BitState.Off, cancelled.Outputs[machine.Ports.OverflowOutput]);
                    Assert.Equal(State(notDivisible), cancelled.Outputs[machine.Ports.NotDivisibleOutput]);
                    Assert.Equal(
                        notDivisible ? magnitude : magnitude / prime,
                        BinaryMagnitudeDatapathHardware.DecodeNextMagnitude(machine, cancelled));
                    if (notDivisible)
                    {
                        AssertStateHeld(state, cancelled.DffNextStates);
                    }
                }
            }
        }
    }

    [Fact]
    public void LoadAndAddExhaustW4W6AndUseTheMatchedFrozenW8Pairs()
    {
        foreach (var width in new[] { 4, 6 })
        {
            var maximum = (1 << width) - 1;
            var pairs = from left in Enumerable.Range(0, maximum + 1)
                        from right in Enumerable.Range(0, maximum + 1)
                        select new MagnitudePair(left, right);
            VerifyLoadAndAddPairs(BinaryMagnitudeDatapathHardware.Build(width), pairs);
        }

        VerifyLoadAndAddPairs(
            BinaryMagnitudeDatapathHardware.Build(8),
            FrozenW8Pairs());
    }

    [Fact]
    public void InvalidOpcodesHoldAndSettledTransitionReplayIsExact()
    {
        foreach (var width in Widths)
        {
            var machine = BinaryMagnitudeDatapathHardware.Build(width);
            var state = BinaryMagnitudeDatapathHardware.EncodeState(machine, 1);
            var invalid = BinaryMagnitudeDatapathHardware.EncodeInputs(
                machine,
                BinaryMagnitudeDatapathOperation.Load,
                operand: 2);
            WriteUnsigned(invalid, machine.Ports.OpcodeInputs, 7);
            var rejected = machine.Netlist.Evaluate(invalid, state);
            AssertStatus(machine, rejected, accepted: false);
            AssertStateHeld(state, rejected.DffNextStates);

            NandEvaluation? previous = null;
            Dictionary<string, BitState>? previousState = null;
            var controls = new[]
            {
                BinaryMagnitudeDatapathHardware.EncodeInputs(
                    machine,
                    BinaryMagnitudeDatapathOperation.Load,
                    operand: 6),
                BinaryMagnitudeDatapathHardware.EncodeInputs(
                    machine,
                    BinaryMagnitudeDatapathOperation.Scale,
                    prime: 3),
                BinaryMagnitudeDatapathHardware.EncodeInputs(
                    machine,
                    BinaryMagnitudeDatapathOperation.Cancel,
                    prime: 2),
                BinaryMagnitudeDatapathHardware.EncodeInputs(
                    machine,
                    BinaryMagnitudeDatapathOperation.AddMagnitude,
                    operand: 1),
                BinaryMagnitudeDatapathHardware.EncodeInputs(
                    machine,
                    BinaryMagnitudeDatapathOperation.Cancel,
                    prime: 7),
            };
            foreach (var control in controls)
            {
                var evaluated = machine.Netlist.Evaluate(
                    control,
                    state,
                    previous,
                    compareWithAllOff: previous is null);
                var expectedStateTransitions = previousState is null
                    ? state.Values.Count(value => value == BitState.On)
                    : state.Count(pair => previousState[pair.Key] != pair.Value);
                Assert.Equal(expectedStateTransitions, evaluated.StateBitTransitions);
                Assert.True(evaluated.NandOutputTransitions > 0);
                Assert.Equal(
                    evaluated.SettledTransitions.OrderBy(transition => transition.NodeId),
                    evaluated.SettledTransitions);
                previousState = new Dictionary<string, BitState>(state, StringComparer.Ordinal);
                state = BinaryMagnitudeDatapathHardware.AdvanceState(evaluated);
                previous = evaluated;
            }
        }
    }

    [Fact]
    public void HelpersRejectUnsupportedBoundaries()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMagnitudeDatapathHardware.Build(5));
        var machine = BinaryMagnitudeDatapathHardware.Build(4);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BinaryMagnitudeDatapathHardware.EncodeInputs(
                machine,
                (BinaryMagnitudeDatapathOperation)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BinaryMagnitudeDatapathHardware.EncodeInputs(
                machine,
                BinaryMagnitudeDatapathOperation.Scale,
                prime: 11));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BinaryMagnitudeDatapathHardware.EncodeInputs(
                machine,
                BinaryMagnitudeDatapathOperation.Load,
                operand: 16));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BinaryMagnitudeDatapathHardware.EncodeState(machine, 16));
    }

    private static void VerifyLoadAndAddPairs(
        DeclaredBinaryMagnitudeDatapath machine,
        IEnumerable<MagnitudePair> pairs)
    {
        var maximum = (1 << machine.Ports.Width) - 1;
        foreach (var pair in pairs)
        {
            var state = BinaryMagnitudeDatapathHardware.EncodeState(machine, pair.Left);
            var loaded = machine.Netlist.Evaluate(
                BinaryMagnitudeDatapathHardware.EncodeInputs(
                    machine,
                    BinaryMagnitudeDatapathOperation.Load,
                    operand: pair.Right),
                state);
            AssertCurrent(machine, loaded, pair.Left);
            AssertStatus(machine, loaded, accepted: true);
            Assert.Equal(BitState.Off, loaded.Outputs[machine.Ports.OverflowOutput]);
            Assert.Equal(BitState.Off, loaded.Outputs[machine.Ports.NotDivisibleOutput]);
            Assert.Equal(
                pair.Right,
                BinaryMagnitudeDatapathHardware.DecodeNextMagnitude(machine, loaded));

            var added = machine.Netlist.Evaluate(
                BinaryMagnitudeDatapathHardware.EncodeInputs(
                    machine,
                    BinaryMagnitudeDatapathOperation.AddMagnitude,
                    operand: pair.Right),
                state);
            var overflow = pair.Left > maximum - pair.Right;
            AssertCurrent(machine, added, pair.Left);
            AssertStatus(machine, added, accepted: !overflow);
            Assert.Equal(State(overflow), added.Outputs[machine.Ports.OverflowOutput]);
            Assert.Equal(BitState.Off, added.Outputs[machine.Ports.NotDivisibleOutput]);
            Assert.Equal(
                overflow ? pair.Left : pair.Left + pair.Right,
                BinaryMagnitudeDatapathHardware.DecodeNextMagnitude(machine, added));
            if (overflow)
            {
                AssertStateHeld(state, added.DffNextStates);
            }
        }
    }

    private static List<MagnitudePair> FrozenW8Pairs()
    {
        const int target = 20_000;
        var boundary = new[]
        {
            0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 21, 31, 32, 63, 64, 127, 128, 251, 254, 255,
        };
        var encoded = new HashSet<int>();
        var result = new List<MagnitudePair>(target);
        foreach (var left in boundary)
        {
            foreach (var right in boundary)
            {
                Add(left, right);
            }
        }

        ulong state = 0x5041_482D_4249_4E56UL;
        while (result.Count < target)
        {
            Add((int)(Next() & 0xff), (int)(Next() & 0xff));
        }

        return result;

        void Add(int left, int right)
        {
            var key = (left << 8) | right;
            if (encoded.Add(key) && result.Count < target)
            {
                result.Add(new MagnitudePair(left, right));
            }
        }

        ulong Next()
        {
            state += 0x9E37_79B9_7F4A_7C15UL;
            var value = state;
            value = (value ^ (value >> 30)) * 0xBF58_476D_1CE4_E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D0_49BB_1331_11EBUL;
            return value ^ (value >> 31);
        }
    }

    private static void AssertCurrent(
        DeclaredBinaryMagnitudeDatapath machine,
        NandEvaluation evaluated,
        int magnitude)
    {
        Assert.Equal(
            magnitude,
            ReadUnsigned(evaluated.Outputs, machine.Ports.MagnitudeOutputNames));
        Assert.Equal(State(magnitude == 0), evaluated.Outputs[machine.Ports.ZeroOutput]);
    }

    private static void AssertStatus(
        DeclaredBinaryMagnitudeDatapath machine,
        NandEvaluation evaluated,
        bool accepted)
    {
        Assert.Equal(State(accepted), evaluated.Outputs[machine.Ports.AcceptedOutput]);
        Assert.Equal(State(!accepted), evaluated.Outputs[machine.Ports.RejectOutput]);
    }

    private static void AssertStateHeld(
        IReadOnlyDictionary<string, BitState> expected,
        IReadOnlyDictionary<string, BitState> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, actual[pair.Key]);
        }
    }

    private static int ReadUnsigned(
        IReadOnlyDictionary<string, BitState> values,
        IReadOnlyList<string> names)
    {
        var result = 0;
        for (var bit = 0; bit < names.Count; bit++)
        {
            if (values[names[bit]] == BitState.On)
            {
                result |= 1 << bit;
            }
        }

        return result;
    }

    private static void WriteUnsigned(
        IDictionary<string, BitState> values,
        IReadOnlyList<string> names,
        int value)
    {
        for (var bit = 0; bit < names.Count; bit++)
        {
            values[names[bit]] = State((value & (1 << bit)) != 0);
        }
    }

    private static void AssertMetrics(NandNetlist netlist, MetricExpectation expected)
    {
        var metrics = netlist.Metrics;
        Assert.Equal(expected.Nand, metrics.Nand2Static);
        Assert.Equal(expected.Dff, metrics.DffStatic);
        Assert.Equal(expected.State, metrics.StateBits);
        Assert.Equal(expected.Input, metrics.InputBits);
        Assert.Equal(expected.Output, metrics.OutputBits);
        Assert.Equal(expected.Port, metrics.PortBits);
        Assert.Equal(expected.Wire, metrics.WireBits);
        Assert.Equal(expected.Nand * 2, metrics.NandInputPinConnections);
        Assert.Equal(expected.Connections, metrics.ConnectionsStatic);
        Assert.Equal(expected.Connections, metrics.TotalNetSinks);
        Assert.Equal(expected.Fanout, metrics.MaximumFanout);
        Assert.Equal(expected.Depth, metrics.UnitNandCriticalDepth);
        Assert.Equal(expected.CrossRegion, metrics.CrossRegionConnections);
        Assert.Equal(expected.CrossLane, metrics.CrossLaneConnections);
        Assert.Equal(CombinationalLoopStatus.Acyclic, metrics.CombinationalLoopStatus);
        Assert.Equal(expected.Dff, netlist.DffBoundaries.Count);
    }

    private static BitState State(bool value) => value ? BitState.On : BitState.Off;

    private readonly record struct MagnitudePair(int Left, int Right);

    private sealed record MetricExpectation(
        int Nand,
        int Dff,
        int State,
        int Input,
        int Output,
        int Port,
        int Wire,
        int Connections,
        int Fanout,
        int Depth,
        int CrossRegion,
        int CrossLane);
}
