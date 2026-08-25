using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Tests;

public sealed class HardwareSidecarDatapathTests
{
    private static readonly int[] Widths = [4, 6, 8];

    private static readonly IReadOnlyDictionary<int, MetricExpectation> Metrics =
        new Dictionary<int, MetricExpectation>
        {
            [4] = new(2307, 12, 12, 12, 21, 33, 2337, 4647, 159, 148, 1903, 0),
            [6] = new(5638, 19, 19, 14, 28, 42, 5677, 11323, 275, 278, 4271, 0),
            [8] = new(15871, 26, 26, 16, 35, 51, 15919, 31803, 415, 458, 10747, 0),
        };

    [Fact]
    public void LayoutAndExactStaticCostsAreStableAndNandOnly()
    {
        foreach (var width in Widths)
        {
            var machine = SidecarDatapathHardware.Build(width);
            var again = SidecarDatapathHardware.Build(width);
            Assert.Equal(machine.Netlist.Nodes, again.Netlist.Nodes);
            Assert.Equal(machine.Netlist.NamedOutputs, again.Netlist.NamedOutputs);
            Assert.Equal(machine.Netlist.DffBoundaries, again.Netlist.DffBoundaries);
            Assert.Equal("STRUCTURAL_DECLARED_INTEGRATED", machine.EvidenceClass);
            Assert.Equal(ValuationHardwareDomain.S4, machine.Ports.Lanes.Select(lane => lane.Prime));
            Assert.Equal(
                ValuationHardwareDomain.ForWidth(width).Caps,
                machine.Ports.Lanes.Select(lane => lane.Cap));
            AssertMetrics(machine.Netlist, Metrics[width]);
            Assert.All(
                machine.Netlist.Nodes.Where(node => node.Kind is not NandNodeKind.Input and
                    not NandNodeKind.State and not NandNodeKind.Constant),
                node => Assert.Equal(NandNodeKind.Nand2, node.Kind));
        }
    }

    [Fact]
    public void LoadAndRefreshExhaustEveryMagnitudeAndRepairState()
    {
        foreach (var width in Widths)
        {
            var machine = SidecarDatapathHardware.Build(width);
            var domain = ValuationHardwareDomain.ForWidth(width);
            var malformedZero = SidecarDatapathHardware.EncodeRawState(
                machine,
                magnitude: 0,
                valid: false,
                new int[domain.LaneCount]);
            for (var magnitude = 0; magnitude <= domain.MaximumMagnitude; magnitude++)
            {
                var loaded = machine.Netlist.Evaluate(
                    SidecarDatapathHardware.EncodeInputs(
                        machine,
                        SidecarDatapathOperation.Load,
                        operand: magnitude),
                    malformedZero);
                AssertStatus(machine, loaded, accepted: true);
                Assert.Equal(BitState.Off, loaded.Outputs[machine.Ports.StateWellFormedOutput]);
                AssertSnapshot(
                    SidecarDatapathHardware.CreateExactState(width, magnitude),
                    SidecarDatapathHardware.DecodeNextState(machine, loaded));

                var lowerBoundState = SidecarDatapathHardware.EncodeRawState(
                    machine,
                    magnitude,
                    valid: false,
                    new int[domain.LaneCount]);
                var refreshed = machine.Netlist.Evaluate(
                    SidecarDatapathHardware.EncodeInputs(
                        machine,
                        SidecarDatapathOperation.Refresh),
                    lowerBoundState);
                AssertStatus(machine, refreshed, accepted: true);
                AssertSnapshot(
                    SidecarDatapathHardware.CreateExactState(width, magnitude),
                    SidecarDatapathHardware.DecodeNextState(machine, refreshed));
            }
        }
    }

    [Fact]
    public void ScaleCancelAndQueryExhaustEveryLegalStateAndSelection()
    {
        foreach (var width in Widths)
        {
            var machine = SidecarDatapathHardware.Build(width);
            var domain = ValuationHardwareDomain.ForWidth(width);
            foreach (var state in LegalStates(width))
            {
                var encoded = SidecarDatapathHardware.EncodeState(machine, state);
                foreach (var lane in machine.Ports.Lanes)
                {
                    VerifyScaleOrCancel(
                        machine,
                        state,
                        encoded,
                        lane,
                        SidecarDatapathOperation.Scale);
                    VerifyScaleOrCancel(
                        machine,
                        state,
                        encoded,
                        lane,
                        SidecarDatapathOperation.Cancel);

                    for (var threshold = 1; threshold <= 7; threshold++)
                    {
                        var evaluated = machine.Netlist.Evaluate(
                            SidecarDatapathHardware.EncodeInputs(
                                machine,
                                SidecarDatapathOperation.Query,
                                lane.Prime,
                                threshold),
                            encoded);
                        var supported = threshold <= lane.Cap;
                        var predicate = supported && state.LowerBounds[lane.Lane] >= threshold;
                        var known = supported && (state.Valid || predicate);
                        AssertStatus(machine, evaluated, accepted: known);
                        Assert.Equal(State(predicate && known), evaluated.Outputs[machine.Ports.QueryPredicateOutput]);
                        Assert.Equal(State(known), evaluated.Outputs[machine.Ports.QueryKnownOutput]);
                        Assert.Equal(State(known && state.Valid), evaluated.Outputs[machine.Ports.QueryExactOutput]);
                        Assert.Equal(BitState.On, evaluated.Outputs[machine.Ports.StateWellFormedOutput]);
                        AssertStateHeld(encoded, evaluated.DffNextStates);
                    }
                }
            }
        }
    }

    [Fact]
    public void AddMagnitudeExhaustsW4AndW6PairsAgainstTheSemanticSidecar()
    {
        foreach (var width in new[] { 4, 6 })
        {
            var machine = SidecarDatapathHardware.Build(width);
            var maximum = (1 << width) - 1;
            for (var left = 0; left <= maximum; left++)
            {
                var state = SidecarDatapathHardware.CreateExactState(width, left);
                var encoded = SidecarDatapathHardware.EncodeState(machine, state);
                for (var right = 0; right <= maximum; right++)
                {
                    VerifyExactAdd(machine, state, encoded, right);
                }
            }
        }
    }

    [Fact]
    public void AddMagnitudeUsesAFullW8SemanticSplitAndFrozenTwentyThousandGateCases()
    {
        const int width = 8;
        var maximum = (1 << width) - 1;
        for (var left = 0; left <= maximum; left++)
        {
            var leftSemantic = BinaryValuationSidecar.Encode(width, left).Value!;
            for (var right = 0; right <= maximum; right++)
            {
                var rightSemantic = BinaryValuationSidecar.Encode(width, right).Value!;
                var semantic = leftSemantic.Add(rightSemantic);
                Assert.Equal(left <= maximum - right, semantic.Succeeded);
                if (semantic.Succeeded)
                {
                    var expected = ExpectedAdd(
                        SidecarDatapathHardware.CreateExactState(width, left),
                        right);
                    Assert.Equal(expected.Magnitude, semantic.Value!.Magnitude);
                    Assert.Equal(expected.Valid, semantic.Value.Valid);
                    Assert.Equal(
                        expected.LowerBounds,
                        ValuationHardwareDomain.S4
                            .Select(semantic.Value.LowerBoundAtPrime));
                }
            }
        }

        var machine = SidecarDatapathHardware.Build(width);
        foreach (var pair in FrozenW8AddPairs())
        {
            var state = SidecarDatapathHardware.CreateExactState(width, pair.Left);
            VerifyExactAdd(
                machine,
                state,
                SidecarDatapathHardware.EncodeState(machine, state),
                pair.Right);
        }
    }

    [Fact]
    public void LowerBoundAdditionRetainsOnlyEarnedFactsAndCanReestablishExactness()
    {
        var machine = SidecarDatapathHardware.Build(6);
        var invalid = new SidecarDatapathStateSnapshot(6, 6, false, [1, 0, 0, 0]);
        var encoded = SidecarDatapathHardware.EncodeState(machine, invalid);

        var nonzero = machine.Netlist.Evaluate(
            SidecarDatapathHardware.EncodeInputs(
                machine,
                SidecarDatapathOperation.AddMagnitude,
                operand: 1),
            encoded);
        AssertSnapshot(
            new SidecarDatapathStateSnapshot(6, 7, false, [0, 0, 0, 0]),
            SidecarDatapathHardware.DecodeNextState(machine, nonzero));

        var addZero = machine.Netlist.Evaluate(
            SidecarDatapathHardware.EncodeInputs(
                machine,
                SidecarDatapathOperation.AddMagnitude,
                operand: 0),
            encoded);
        AssertSnapshot(invalid, SidecarDatapathHardware.DecodeNextState(machine, addZero));

        var allUnequal = SidecarDatapathHardware.CreateExactState(6, 10);
        var exact = machine.Netlist.Evaluate(
            SidecarDatapathHardware.EncodeInputs(
                machine,
                SidecarDatapathOperation.AddMagnitude,
                operand: 21),
            SidecarDatapathHardware.EncodeState(machine, allUnequal));
        AssertSnapshot(
            SidecarDatapathHardware.CreateExactState(6, 31),
            SidecarDatapathHardware.DecodeNextState(machine, exact));

        var equalValuations = SidecarDatapathHardware.CreateExactState(6, 1);
        var invalidated = machine.Netlist.Evaluate(
            SidecarDatapathHardware.EncodeInputs(
                machine,
                SidecarDatapathOperation.AddMagnitude,
                operand: 1),
            SidecarDatapathHardware.EncodeState(machine, equalValuations));
        AssertSnapshot(
            new SidecarDatapathStateSnapshot(6, 2, false, [0, 0, 0, 0]),
            SidecarDatapathHardware.DecodeNextState(machine, invalidated));
    }

    [Fact]
    public void MalformedStatesRejectAndHoldExceptForRepairOperations()
    {
        foreach (var width in Widths)
        {
            var machine = SidecarDatapathHardware.Build(width);
            var domain = ValuationHardwareDomain.ForWidth(width);
            var malformed = new List<Dictionary<string, BitState>>
            {
                SidecarDatapathHardware.EncodeRawState(
                    machine,
                    0,
                    valid: false,
                    domain.Caps),
                SidecarDatapathHardware.EncodeRawState(
                    machine,
                    1,
                    valid: false,
                    [1, 0, 0, 0]),
                SidecarDatapathHardware.EncodeRawState(
                    machine,
                    2,
                    valid: true,
                    [0, 0, 0, 0]),
            };
            var nonmonotone = SidecarDatapathHardware.EncodeRawState(
                machine,
                4,
                valid: false,
                [0, 0, 0, 0]);
            nonmonotone[machine.Ports.Lanes[0].StateThresholdNames[1]] = BitState.On;
            malformed.Add(nonmonotone);

            foreach (var state in malformed)
            {
                foreach (var operation in new[]
                         {
                             SidecarDatapathOperation.Query,
                             SidecarDatapathOperation.Scale,
                             SidecarDatapathOperation.Cancel,
                             SidecarDatapathOperation.AddMagnitude,
                         })
                {
                    var evaluated = machine.Netlist.Evaluate(
                        SidecarDatapathHardware.EncodeInputs(
                            machine,
                            operation,
                            operand: 1),
                        state);
                    Assert.Equal(BitState.Off, evaluated.Outputs[machine.Ports.StateWellFormedOutput]);
                    AssertStatus(machine, evaluated, accepted: false);
                    AssertStateHeld(state, evaluated.DffNextStates);
                }

                var load = machine.Netlist.Evaluate(
                    SidecarDatapathHardware.EncodeInputs(
                        machine,
                        SidecarDatapathOperation.Load,
                        operand: 5),
                    state);
                AssertStatus(machine, load, accepted: true);
                AssertSnapshot(
                    SidecarDatapathHardware.CreateExactState(width, 5),
                    SidecarDatapathHardware.DecodeNextState(machine, load));

                var refresh = machine.Netlist.Evaluate(
                    SidecarDatapathHardware.EncodeInputs(machine, SidecarDatapathOperation.Refresh),
                    state);
                AssertStatus(machine, refresh, accepted: true);
                AssertSnapshot(
                    SidecarDatapathHardware.CreateExactState(
                        width,
                        ReadUnsigned(state, machine.Ports.StateMagnitudeNames)),
                    SidecarDatapathHardware.DecodeNextState(machine, refresh));
            }
        }
    }

    [Fact]
    public void InvalidOpcodeAndThresholdRejectAtomicallyAndSettledReplayIsExact()
    {
        foreach (var width in Widths)
        {
            var machine = SidecarDatapathHardware.Build(width);
            var state = SidecarDatapathHardware.EncodeState(
                machine,
                SidecarDatapathHardware.CreateExactState(width, 1));
            var invalidOpcode = SidecarDatapathHardware.EncodeInputs(
                machine,
                SidecarDatapathOperation.Load,
                operand: 2);
            WriteUnsigned(invalidOpcode, machine.Ports.OpcodeInputs, 7);
            var rejected = machine.Netlist.Evaluate(invalidOpcode, state);
            AssertStatus(machine, rejected, accepted: false);
            AssertStateHeld(state, rejected.DffNextStates);

            var invalidThreshold = machine.Netlist.Evaluate(
                SidecarDatapathHardware.EncodeInputs(
                    machine,
                    SidecarDatapathOperation.Query,
                    threshold: 0),
                state);
            AssertStatus(machine, invalidThreshold, accepted: false);
            AssertStateHeld(state, invalidThreshold.DffNextStates);

            NandEvaluation? previous = null;
            Dictionary<string, BitState>? previousState = null;
            var controls = new[]
            {
                SidecarDatapathHardware.EncodeInputs(machine, SidecarDatapathOperation.Load, operand: 6),
                SidecarDatapathHardware.EncodeInputs(machine, SidecarDatapathOperation.Query, 2, 1),
                SidecarDatapathHardware.EncodeInputs(machine, SidecarDatapathOperation.Scale, 3),
                SidecarDatapathHardware.EncodeInputs(machine, SidecarDatapathOperation.Cancel, 2),
                SidecarDatapathHardware.EncodeInputs(machine, SidecarDatapathOperation.AddMagnitude, operand: 1),
                SidecarDatapathHardware.EncodeInputs(machine, SidecarDatapathOperation.Refresh),
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
                state = SidecarDatapathHardware.AdvanceState(evaluated);
                previous = evaluated;
            }
        }
    }

    [Fact]
    public void PublicHelpersRejectUnsupportedOrContradictoryBoundaries()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SidecarDatapathHardware.Build(5));
        var machine = SidecarDatapathHardware.Build(4);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SidecarDatapathHardware.EncodeInputs(machine, SidecarDatapathOperation.Load, prime: 11));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SidecarDatapathHardware.EncodeInputs(machine, (SidecarDatapathOperation)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SidecarDatapathHardware.EncodeInputs(machine, SidecarDatapathOperation.Load, operand: 16));
        Assert.Throws<ArgumentException>(() => SidecarDatapathHardware.EncodeState(
            machine,
            new SidecarDatapathStateSnapshot(4, 1, true, [1, 0, 0, 0])));
        Assert.Throws<ArgumentException>(() => SidecarDatapathHardware.EncodeState(
            machine,
            new SidecarDatapathStateSnapshot(4, 0, false, [3, 2, 1, 1])));
        Assert.Throws<ArgumentException>(() => SidecarDatapathHardware.EncodeState(
            machine,
            SidecarDatapathHardware.CreateExactState(6, 1)));
    }

    private static void VerifyScaleOrCancel(
        DeclaredSidecarDatapath machine,
        SidecarDatapathStateSnapshot state,
        IReadOnlyDictionary<string, BitState> encoded,
        SidecarThresholdLaneLayout lane,
        SidecarDatapathOperation operation)
    {
        var evaluated = machine.Netlist.Evaluate(
            SidecarDatapathHardware.EncodeInputs(machine, operation, lane.Prime),
            encoded);
        var maximum = (1 << state.Width) - 1;
        var reject = operation == SidecarDatapathOperation.Scale
            ? state.Magnitude > maximum / lane.Prime
            : state.Magnitude % lane.Prime != 0;
        AssertStatus(machine, evaluated, accepted: !reject);
        Assert.Equal(
            State(operation == SidecarDatapathOperation.Scale && reject),
            evaluated.Outputs[machine.Ports.OverflowOutput]);
        Assert.Equal(
            State(operation == SidecarDatapathOperation.Cancel && reject),
            evaluated.Outputs[machine.Ports.NotDivisibleOutput]);
        if (reject)
        {
            AssertStateHeld(encoded, evaluated.DffNextStates);
            return;
        }

        var lowerBounds = state.LowerBounds.ToArray();
        var magnitude = state.Magnitude;
        if (magnitude != 0)
        {
            if (operation == SidecarDatapathOperation.Scale)
            {
                magnitude *= lane.Prime;
                lowerBounds[lane.Lane]++;
            }
            else
            {
                magnitude /= lane.Prime;
                lowerBounds[lane.Lane] = Math.Max(0, lowerBounds[lane.Lane] - 1);
            }
        }

        AssertSnapshot(
            new SidecarDatapathStateSnapshot(state.Width, magnitude, state.Valid, lowerBounds),
            SidecarDatapathHardware.DecodeNextState(machine, evaluated));
    }

    private static void VerifyExactAdd(
        DeclaredSidecarDatapath machine,
        SidecarDatapathStateSnapshot state,
        IReadOnlyDictionary<string, BitState> encoded,
        int addend)
    {
        var evaluated = machine.Netlist.Evaluate(
            SidecarDatapathHardware.EncodeInputs(
                machine,
                SidecarDatapathOperation.AddMagnitude,
                operand: addend),
            encoded);
        var maximum = (1 << state.Width) - 1;
        var overflow = state.Magnitude > maximum - addend;
        AssertStatus(machine, evaluated, accepted: !overflow);
        Assert.Equal(State(overflow), evaluated.Outputs[machine.Ports.OverflowOutput]);
        if (overflow)
        {
            AssertStateHeld(encoded, evaluated.DffNextStates);
        }
        else
        {
            AssertSnapshot(
                ExpectedAdd(state, addend),
                SidecarDatapathHardware.DecodeNextState(machine, evaluated));
        }
    }

    private static SidecarDatapathStateSnapshot ExpectedAdd(
        SidecarDatapathStateSnapshot left,
        int rightMagnitude)
    {
        var right = SidecarDatapathHardware.CreateExactState(left.Width, rightMagnitude);
        var lowerBounds = left.LowerBounds.Zip(right.LowerBounds, Math.Min).ToArray();
        var valid = left.Magnitude == 0 ||
                    rightMagnitude == 0 && left.Valid ||
                    left.Valid && left.LowerBounds.Zip(right.LowerBounds, (a, b) => a != b).All(value => value);
        return new SidecarDatapathStateSnapshot(
            left.Width,
            left.Magnitude + rightMagnitude,
            valid,
            lowerBounds);
    }

    private static IEnumerable<SidecarDatapathStateSnapshot> LegalStates(int width)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        yield return SidecarDatapathHardware.CreateExactState(width, 0);
        for (var magnitude = 1; magnitude <= domain.MaximumMagnitude; magnitude++)
        {
            var exact = SidecarDatapathHardware.CreateExactState(width, magnitude);
            yield return exact;
            foreach (var lowerBounds in LowerBoundVectors(exact.LowerBounds))
            {
                yield return new SidecarDatapathStateSnapshot(width, magnitude, false, lowerBounds);
            }
        }
    }

    private static IEnumerable<int[]> LowerBoundVectors(IReadOnlyList<int> exact)
    {
        var current = new int[exact.Count];
        return Visit(lane: 0);

        IEnumerable<int[]> Visit(int lane)
        {
            if (lane == exact.Count)
            {
                yield return (int[])current.Clone();
                yield break;
            }

            for (var lowerBound = 0; lowerBound <= exact[lane]; lowerBound++)
            {
                current[lane] = lowerBound;
                foreach (var result in Visit(lane + 1))
                {
                    yield return result;
                }
            }
        }
    }

    private static List<AddPair> FrozenW8AddPairs()
    {
        const int target = 20_000;
        var boundary = new[]
        {
            0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 21, 31, 32, 63, 64, 127, 128, 251, 254, 255,
        };
        var encoded = new HashSet<int>();
        var result = new List<AddPair>(target);
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
            var left = (int)(Next() & 0xff);
            var right = (int)(Next() & 0xff);
            Add(left, right);
        }

        return result;

        void Add(int left, int right)
        {
            var key = (left << 8) | right;
            if (encoded.Add(key) && result.Count < target)
            {
                result.Add(new AddPair(left, right));
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

    private static void AssertStatus(
        DeclaredSidecarDatapath machine,
        NandEvaluation evaluated,
        bool accepted)
    {
        Assert.Equal(State(accepted), evaluated.Outputs[machine.Ports.AcceptedOutput]);
        Assert.Equal(State(!accepted), evaluated.Outputs[machine.Ports.RejectOutput]);
    }

    private static void AssertSnapshot(
        SidecarDatapathStateSnapshot expected,
        SidecarDatapathStateSnapshot actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Magnitude, actual.Magnitude);
        Assert.Equal(expected.Valid, actual.Valid);
        Assert.Equal(expected.LowerBounds, actual.LowerBounds);
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

    private readonly record struct AddPair(int Left, int Right);

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
