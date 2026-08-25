using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Tests;

public sealed class HardwareWarmStructuralMachineTests
{
    private static readonly int[] Widths = [4, 6, 8];
    private static readonly WarmStructuralOperation[] Operations =
    [
        WarmStructuralOperation.Scale,
        WarmStructuralOperation.Cancel,
    ];

    private static readonly IReadOnlyDictionary<int, MetricExpectation> StructuralMetrics =
        new Dictionary<int, MetricExpectation>
        {
            [4] = new(564, 11, 11, 3, 17, 20, 592, 1156, 71, 36, 464, 0),
            [6] = new(789, 14, 14, 3, 20, 23, 820, 1612, 105, 38, 644, 0),
            [8] = new(864, 15, 15, 3, 21, 24, 896, 1764, 108, 38, 704, 0),
        };

    private static readonly IReadOnlyDictionary<int, MetricExpectation> BinaryMetrics =
        new Dictionary<int, MetricExpectation>
        {
            [4] = new(1107, 4, 4, 3, 10, 13, 1116, 2228, 158, 140, 851, 0),
            [6] = new(2313, 6, 6, 3, 12, 15, 2324, 4644, 274, 270, 1735, 0),
            [8] = new(3959, 8, 8, 3, 14, 17, 3972, 7940, 414, 448, 2931, 0),
        };

    [Fact]
    public void IntegratedMachinesHaveStableLayoutsAndExactStaticCosts()
    {
        foreach (var width in Widths)
        {
            var structural = WarmStructuralHardware.BuildScaleCancelMachine(width);
            var structuralAgain = WarmStructuralHardware.BuildScaleCancelMachine(width);
            Assert.Equal(structural.Netlist.Nodes, structuralAgain.Netlist.Nodes);
            Assert.Equal(structural.Netlist.NamedOutputs, structuralAgain.Netlist.NamedOutputs);
            Assert.Equal(structural.Netlist.DffBoundaries, structuralAgain.Netlist.DffBoundaries);
            Assert.Equal("STRUCTURAL_DECLARED_INTEGRATED", structural.EvidenceClass);
            AssertMetrics(structural.Netlist, StructuralMetrics[width]);
            Assert.Equal(ValuationHardwareDomain.S4, structural.Ports.Lanes.Select(lane => lane.Prime));
            Assert.Equal(
                ValuationHardwareDomain.ForWidth(width).Caps,
                structural.Ports.Lanes.Select(lane => lane.Cap));
            Assert.All(
                structural.Netlist.Nodes.Where(node => node.Kind is not NandNodeKind.Input and
                    not NandNodeKind.State and not NandNodeKind.Constant),
                node => Assert.Equal(NandNodeKind.Nand2, node.Kind));

            var binary = WarmStructuralHardware.BuildBinaryScaleCancelMachine(width);
            var binaryAgain = WarmStructuralHardware.BuildBinaryScaleCancelMachine(width);
            Assert.Equal(binary.Netlist.Nodes, binaryAgain.Netlist.Nodes);
            Assert.Equal(binary.Netlist.NamedOutputs, binaryAgain.Netlist.NamedOutputs);
            Assert.Equal(binary.Netlist.DffBoundaries, binaryAgain.Netlist.DffBoundaries);
            Assert.Equal("STRUCTURAL_DECLARED_INTEGRATED", binary.EvidenceClass);
            AssertMetrics(binary.Netlist, BinaryMetrics[width]);
            Assert.Equal(structural.Ports.OperationInput, binary.Ports.OperationInput);
            Assert.Equal(structural.Ports.PrimeSelectInputs, binary.Ports.PrimeSelectInputs);
        }
    }

    [Fact]
    public void StructuralMachineExhaustsEveryLegalStateSelectAndOperation()
    {
        foreach (var width in Widths)
        {
            var machine = WarmStructuralHardware.BuildScaleCancelMachine(width);
            foreach (var state in LegalStates(width))
            {
                foreach (var lane in machine.Ports.Lanes)
                {
                    foreach (var operation in Operations)
                    {
                        var evaluated = machine.Netlist.Evaluate(
                            WarmStructuralHardware.EncodeControl(machine, lane.Prime, operation),
                            WarmStructuralHardware.EncodeExactState(machine, state));
                        var selectedExponent = state.ExponentAt(lane.Lane);
                        var overflow = !state.IsZero &&
                                       operation == WarmStructuralOperation.Scale &&
                                       selectedExponent == lane.Cap;
                        var underflow = !state.IsZero &&
                                        operation == WarmStructuralOperation.Cancel &&
                                        selectedExponent == 0;
                        var reject = overflow || underflow;
                        Assert.Equal(State(reject), evaluated.Outputs[machine.Ports.RejectOutput]);
                        Assert.Equal(State(!reject), evaluated.Outputs[machine.Ports.AcceptedOutput]);
                        Assert.Equal(State(overflow), evaluated.Outputs[machine.Ports.OverflowOutput]);
                        Assert.Equal(State(underflow), evaluated.Outputs[machine.Ports.UnderflowOutput]);
                        Assert.Equal(BitState.On, evaluated.Outputs[machine.Ports.SaturationValidOutput]);
                        Assert.Equal(BitState.On, evaluated.Outputs[machine.Ports.CanonicalValidOutput]);
                        Assert.Equal(State(state.IsZero), evaluated.Outputs[machine.Ports.ZeroOutput]);
                        AssertCurrentStructuralOutputs(machine, evaluated, state);

                        var expected = ExpectedStructuralTransition(state, lane.Lane, operation, reject);
                        var decoded = WarmStructuralHardware.DecodeNextExactState(machine, evaluated);
                        Assert.True(decoded.Succeeded);
                        AssertStructuralState(expected, decoded.Value!);
                        if (reject)
                        {
                            AssertStateHeld(
                                WarmStructuralHardware.EncodeExactState(machine, state),
                                evaluated.DffNextStates);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void BinaryMachineExhaustsEveryMagnitudeSelectAndOperation()
    {
        foreach (var width in Widths)
        {
            var machine = WarmStructuralHardware.BuildBinaryScaleCancelMachine(width);
            var maximum = (1 << width) - 1;
            for (var magnitude = 0; magnitude <= maximum; magnitude++)
            {
                foreach (var prime in ValuationHardwareDomain.S4)
                {
                    foreach (var operation in Operations)
                    {
                        var evaluated = machine.Netlist.Evaluate(
                            WarmStructuralHardware.EncodeControl(machine, prime, operation),
                            WarmStructuralHardware.EncodeMagnitudeState(machine, magnitude));
                        var overflow = operation == WarmStructuralOperation.Scale && magnitude > maximum / prime;
                        var notDivisible = operation == WarmStructuralOperation.Cancel && magnitude % prime != 0;
                        var reject = overflow || notDivisible;
                        var expected = reject
                            ? magnitude
                            : operation == WarmStructuralOperation.Scale
                                ? magnitude * prime
                                : magnitude / prime;
                        Assert.Equal(State(magnitude == 0), evaluated.Outputs[machine.Ports.ZeroOutput]);
                        Assert.Equal(State(reject), evaluated.Outputs[machine.Ports.RejectOutput]);
                        Assert.Equal(State(!reject), evaluated.Outputs[machine.Ports.AcceptedOutput]);
                        Assert.Equal(State(overflow), evaluated.Outputs[machine.Ports.OverflowOutput]);
                        Assert.Equal(State(notDivisible), evaluated.Outputs[machine.Ports.NotDivisibleOutput]);
                        if (operation == WarmStructuralOperation.Cancel)
                        {
                            Assert.Equal(
                                State(magnitude % prime == 0),
                                evaluated.Outputs[machine.Ports.DivisionExactOutput]);
                        }

                        Assert.Equal(expected, WarmStructuralHardware.DecodeNextMagnitude(machine, evaluated));
                        Assert.Equal(
                            magnitude,
                            ReadUnsigned(evaluated.Outputs, machine.Ports.MagnitudeOutputNames));
                        if (reject)
                        {
                            AssertStateHeld(
                                WarmStructuralHardware.EncodeMagnitudeState(machine, magnitude),
                                evaluated.DffNextStates);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void StructuralMachineRejectsAndHoldsMalformedOrSaturatedState()
    {
        foreach (var width in Widths)
        {
            var machine = WarmStructuralHardware.BuildScaleCancelMachine(width);
            var control = WarmStructuralHardware.EncodeControl(
                machine,
                prime: 2,
                WarmStructuralOperation.Scale);

            foreach (var lane in machine.Ports.Lanes)
            {
                var saturated = WarmStructuralHardware.EncodeExactState(
                    machine,
                    ValuationHardwareState.Identity(width));
                saturated[lane.StateSaturationName] = BitState.On;
                AssertMalformedRejectedAndHeld(machine, control, saturated, saturationValid: false);
            }

            var zeroWithPayload = WarmStructuralHardware.EncodeExactState(
                machine,
                ValuationHardwareState.Zero(width));
            zeroWithPayload[machine.Ports.Lanes[0].StatePayloadNames[0]] = BitState.On;
            AssertMalformedRejectedAndHeld(machine, control, zeroWithPayload, saturationValid: true);

            var aboveCapLane = machine.Ports.Lanes.First(
                lane => lane.Cap < (1 << lane.PayloadWidth) - 1);
            var aboveCap = WarmStructuralHardware.EncodeExactState(
                machine,
                ValuationHardwareState.Identity(width));
            WriteUnsigned(aboveCap, aboveCapLane.StatePayloadNames, aboveCapLane.Cap + 1);
            AssertMalformedRejectedAndHeld(machine, control, aboveCap, saturationValid: true);
        }
    }

    [Fact]
    public void SettledReplayCapturesRegisteredStateAndGateTransitions()
    {
        foreach (var width in Widths)
        {
            ReplayStructural(width);
            ReplayBinary(width);
        }
    }

    [Fact]
    public void HelpersRejectInvalidWidthsPrimesOperationsAndStateDomains()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WarmStructuralHardware.BuildScaleCancelMachine(5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WarmStructuralHardware.BuildBinaryScaleCancelMachine(5));
        var structural = WarmStructuralHardware.BuildScaleCancelMachine(4);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WarmStructuralHardware.EncodeControl(structural, 11, WarmStructuralOperation.Scale));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WarmStructuralHardware.EncodeControl(structural, 2, (WarmStructuralOperation)99));
        Assert.Throws<ArgumentException>(() =>
            WarmStructuralHardware.EncodeExactState(structural, ValuationHardwareState.Identity(6)));
        var binary = WarmStructuralHardware.BuildBinaryScaleCancelMachine(4);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WarmStructuralHardware.EncodeMagnitudeState(binary, 16));
    }

    private static void ReplayStructural(int width)
    {
        var machine = WarmStructuralHardware.BuildScaleCancelMachine(width);
        var state = WarmStructuralHardware.EncodeExactState(
            machine,
            ValuationHardwareState.Identity(width));
        Replay(
            machine.Netlist,
            state,
            new[]
            {
                WarmStructuralHardware.EncodeControl(machine, 2, WarmStructuralOperation.Cancel),
                WarmStructuralHardware.EncodeControl(machine, 2, WarmStructuralOperation.Scale),
                WarmStructuralHardware.EncodeControl(machine, 3, WarmStructuralOperation.Scale),
                WarmStructuralHardware.EncodeControl(machine, 2, WarmStructuralOperation.Cancel),
                WarmStructuralHardware.EncodeControl(machine, 3, WarmStructuralOperation.Cancel),
            });
    }

    private static void ReplayBinary(int width)
    {
        var machine = WarmStructuralHardware.BuildBinaryScaleCancelMachine(width);
        var state = WarmStructuralHardware.EncodeMagnitudeState(machine, 1);
        Replay(
            machine.Netlist,
            state,
            new[]
            {
                WarmStructuralHardware.EncodeControl(machine, 2, WarmStructuralOperation.Cancel),
                WarmStructuralHardware.EncodeControl(machine, 2, WarmStructuralOperation.Scale),
                WarmStructuralHardware.EncodeControl(machine, 3, WarmStructuralOperation.Scale),
                WarmStructuralHardware.EncodeControl(machine, 2, WarmStructuralOperation.Cancel),
                WarmStructuralHardware.EncodeControl(machine, 3, WarmStructuralOperation.Cancel),
            });
    }

    private static void Replay(
        NandNetlist netlist,
        Dictionary<string, BitState> state,
        IEnumerable<Dictionary<string, BitState>> controls)
    {
        NandEvaluation? previous = null;
        Dictionary<string, BitState>? previouslyObservedState = null;
        foreach (var control in controls)
        {
            var evaluated = netlist.Evaluate(
                control,
                state,
                previous,
                compareWithAllOff: previous is null);
            var expectedStateTransitions = previouslyObservedState is null
                ? state.Values.Count(value => value == BitState.On)
                : state.Count(pair => previouslyObservedState[pair.Key] != pair.Value);
            Assert.Equal(expectedStateTransitions, evaluated.StateBitTransitions);
            Assert.True(evaluated.NandOutputTransitions > 0);
            Assert.Equal(
                evaluated.SettledTransitions.OrderBy(transition => transition.NodeId),
                evaluated.SettledTransitions);
            previouslyObservedState = new Dictionary<string, BitState>(state, StringComparer.Ordinal);
            state = WarmStructuralHardware.AdvanceState(evaluated);
            previous = evaluated;
        }
    }

    private static void AssertMalformedRejectedAndHeld(
        DeclaredWarmStructuralMachine machine,
        IReadOnlyDictionary<string, BitState> control,
        Dictionary<string, BitState> state,
        bool saturationValid)
    {
        var evaluated = machine.Netlist.Evaluate(control, state);
        Assert.Equal(BitState.On, evaluated.Outputs[machine.Ports.RejectOutput]);
        Assert.Equal(BitState.Off, evaluated.Outputs[machine.Ports.AcceptedOutput]);
        Assert.Equal(BitState.Off, evaluated.Outputs[machine.Ports.OverflowOutput]);
        Assert.Equal(BitState.Off, evaluated.Outputs[machine.Ports.UnderflowOutput]);
        Assert.Equal(State(saturationValid), evaluated.Outputs[machine.Ports.SaturationValidOutput]);
        Assert.Equal(BitState.Off, evaluated.Outputs[machine.Ports.CanonicalValidOutput]);
        AssertStateHeld(state, evaluated.DffNextStates);
    }

    private static void AssertCurrentStructuralOutputs(
        DeclaredWarmStructuralMachine machine,
        NandEvaluation evaluated,
        ValuationHardwareState state)
    {
        for (var lane = 0; lane < machine.Ports.Lanes.Count; lane++)
        {
            var layout = machine.Ports.Lanes[lane];
            Assert.Equal(state.ExponentAt(lane), ReadUnsigned(evaluated.Outputs, layout.PayloadOutputNames));
            Assert.Equal(BitState.Off, evaluated.Outputs[layout.SaturationOutputName]);
        }
    }

    private static ValuationHardwareState ExpectedStructuralTransition(
        ValuationHardwareState state,
        int selectedLane,
        WarmStructuralOperation operation,
        bool reject)
    {
        if (reject || state.IsZero)
        {
            return state;
        }

        var exponents = state.Exponents.ToArray();
        exponents[selectedLane] += operation == WarmStructuralOperation.Scale ? 1 : -1;
        return ValuationHardwareState.Create(state.Width, false, exponents).Value!;
    }

    private static IEnumerable<ValuationHardwareState> LegalStates(int width)
    {
        yield return ValuationHardwareState.Zero(width);
        var domain = ValuationHardwareDomain.ForWidth(width);
        foreach (var exponents in ExponentVectors(domain))
        {
            yield return ValuationHardwareState.Create(width, false, exponents).Value!;
        }
    }

    private static IEnumerable<int[]> ExponentVectors(ValuationHardwareDomain domain)
    {
        var current = new int[domain.LaneCount];
        return Visit(lane: 0);

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
                foreach (var value in Visit(lane + 1))
                {
                    yield return value;
                }
            }
        }
    }

    private static void AssertStructuralState(
        ValuationHardwareState expected,
        ValuationHardwareState actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.IsZero, actual.IsZero);
        Assert.Equal(expected.Exponents, actual.Exponents);
        Assert.Equal(expected.SaturatedLanes, actual.SaturatedLanes);
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
        var value = 0;
        for (var bit = 0; bit < names.Count; bit++)
        {
            if (values[names[bit]] == BitState.On)
            {
                value |= 1 << bit;
            }
        }

        return value;
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
