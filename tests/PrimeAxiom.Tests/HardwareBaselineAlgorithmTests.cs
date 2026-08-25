using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Tests;

public sealed class HardwareBaselineAlgorithmTests
{
    private static readonly int[] Widths = [4, 6, 8];

    [Theory]
    [InlineData(4, 497)]
    [InlineData(6, 1021)]
    [InlineData(8, 1729)]
    public void RestoringDividerHasExactDeclaredStructure(int width, int expectedNands)
    {
        var first = BaselineAlgorithmHardware.BuildUnsignedRestoringDivider(width);
        var second = BaselineAlgorithmHardware.BuildUnsignedRestoringDivider(width);

        Assert.Equal(first.Nodes, second.Nodes);
        Assert.Equal(first.NamedOutputs, second.NamedOutputs);
        Assert.Equal(expectedNands, first.Metrics.Nand2Static);
        Assert.Equal(0, first.Metrics.DffStatic);
        Assert.Equal(0, first.Metrics.StateBits);
        Assert.Equal(width * 2, first.Metrics.InputBits);
        Assert.Equal((width * 2) + 2, first.Metrics.OutputBits);
        Assert.Equal((width * 4) + 2, first.Metrics.PortBits);
        Assert.Equal(expectedNands + (2 * width) + 1, first.Metrics.WireBits);
        Assert.Equal((2 * expectedNands) + (2 * width) + 2, first.Metrics.ConnectionsStatic);
        Assert.Equal(15 * width, first.Metrics.MaximumFanout);
        Assert.Equal((6 * width * width) + (7 * width) + 10, first.Metrics.UnitNandCriticalDepth);
        Assert.Equal((19 * width * width) + (32 * width) + 4, first.Metrics.CrossRegionConnections);
        Assert.Equal(0, first.Metrics.CrossLaneConnections);
        Assert.Equal(CombinationalLoopStatus.Acyclic, first.Metrics.CombinationalLoopStatus);
        Assert.All(
            first.Nodes.Where(node => node.Kind is not NandNodeKind.Input and
                not NandNodeKind.Constant),
            node => Assert.Equal(NandNodeKind.Nand2, node.Kind));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void RestoringDividerIsExhaustivelyEquivalentToUnsignedArithmetic(int width)
    {
        var divider = BaselineAlgorithmHardware.BuildUnsignedRestoringDivider(width);
        var inputs = CreateWordInputs(width, "dividend", "divisor");
        var limit = 1 << width;
        for (var dividend = 0; dividend < limit; dividend++)
        {
            WriteWord(inputs, "dividend", width, dividend);
            for (var divisor = 0; divisor < limit; divisor++)
            {
                WriteWord(inputs, "divisor", width, divisor);
                var evaluated = divider.Evaluate(inputs);
                var quotient = ReadWord(evaluated.Outputs, "quotient", width);
                var remainder = ReadWord(evaluated.Outputs, "remainder", width);

                if (divisor == 0)
                {
                    Assert.Equal(0, quotient);
                    Assert.Equal(dividend, remainder);
                    Assert.Equal(BitState.On, evaluated.Outputs["divide_by_zero"]);
                    Assert.Equal(BitState.Off, evaluated.Outputs["exact"]);
                }
                else
                {
                    Assert.Equal(dividend / divisor, quotient);
                    Assert.Equal(dividend % divisor, remainder);
                    Assert.Equal(BitState.Off, evaluated.Outputs["divide_by_zero"]);
                    Assert.Equal(State(dividend % divisor == 0), evaluated.Outputs["exact"]);
                }
            }
        }
    }

    [Theory]
    [InlineData(4, 430)]
    [InlineData(6, 636)]
    [InlineData(8, 842)]
    public void SubtractiveGcdMachineHasExactStateAndControlBoundary(
        int width,
        int expectedNands)
    {
        var machine = BaselineAlgorithmHardware.BuildSubtractiveGcdMachine(width);

        Assert.Equal(expectedNands, machine.Metrics.Nand2Static);
        Assert.Equal((width * 2) + 2, machine.Metrics.DffStatic);
        Assert.Equal((width * 2) + 2, machine.Metrics.StateBits);
        Assert.Equal((width * 2) + 1, machine.Metrics.InputBits);
        Assert.Equal((width * 3) + 2, machine.Metrics.OutputBits);
        Assert.Equal((width * 5) + 3, machine.Metrics.PortBits);
        Assert.Equal(expectedNands + (4 * width) + 8, machine.Metrics.WireBits);
        Assert.Equal((2 * expectedNands) + (5 * width) + 4, machine.Metrics.ConnectionsStatic);
        Assert.Equal((6 * width) + 6, machine.Metrics.MaximumFanout);
        Assert.Equal((6 * width) + 5, machine.Metrics.UnitNandCriticalDepth);
        Assert.Equal(expectedNands, machine.Metrics.CrossRegionConnections);
        Assert.Equal(0, machine.Metrics.CrossLaneConnections);
        Assert.Equal(CombinationalLoopStatus.Acyclic, machine.Metrics.CombinationalLoopStatus);
        Assert.Equal(machine.Metrics.DffStatic, machine.DffBoundaries.Count);
        Assert.Equal(
            (width * 2) + 2,
            machine.DffBoundaries.Select(boundary => boundary.StateNodeId).Distinct().Count());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void ActiveGcdTransitionIsExhaustivelyEquivalentToNandMachine(int width)
    {
        var machine = BaselineAlgorithmHardware.BuildSubtractiveGcdMachine(width);
        var inputs = CreateGcdInputs(width, load: false, left: 0, right: 0);
        var state = CreateGcdState(width, left: 0, right: 0, running: true, done: false);
        var limit = 1 << width;
        for (var left = 0; left < limit; left++)
        {
            WriteWord(state, "left_q", width, left);
            for (var right = 0; right < limit; right++)
            {
                WriteWord(state, "right_q", width, right);
                var evaluated = machine.Evaluate(inputs, state);
                var actual = ReadNextGcdState(evaluated, width);
                var expected = BaselineAlgorithmHardware.StepSubtractiveGcd(
                    width,
                    new SubtractiveGcdState(left, right, Running: true, Done: false));
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void GcdLoadIsAtomicAndIdleAndDoneStatesHold()
    {
        foreach (var width in Widths)
        {
            var machine = BaselineAlgorithmHardware.BuildSubtractiveGcdMachine(width);
            var maximum = (1 << width) - 1;
            var staleState = CreateGcdState(
                width,
                left: maximum,
                right: maximum - 1,
                running: false,
                done: true);
            var loaded = machine.Evaluate(
                CreateGcdInputs(width, load: true, left: 0, right: maximum),
                staleState);
            Assert.Equal(
                new SubtractiveGcdState(0, maximum, Running: true, Done: false),
                ReadNextGcdState(loaded, width));

            var done = new SubtractiveGcdState(maximum, 0, Running: false, Done: true);
            var heldDone = machine.Evaluate(
                CreateGcdInputs(width, load: false, left: 0, right: 0),
                CreateGcdState(width, done.Left, done.Right, done.Running, done.Done));
            Assert.Equal(done, ReadNextGcdState(heldDone, width));

            var idle = new SubtractiveGcdState(0, 0, Running: false, Done: false);
            var heldIdle = machine.Evaluate(
                CreateGcdInputs(width, load: false, left: maximum, right: maximum),
                CreateGcdState(width, idle.Left, idle.Right, idle.Running, idle.Done));
            Assert.Equal(idle, ReadNextGcdState(heldIdle, width));
        }
    }

    [Fact]
    public void SemanticGcdRunsExhaustivelyToOrdinaryGcdWithoutUsingItAsImplementation()
    {
        foreach (var width in Widths)
        {
            var limit = 1 << width;
            var cycleBound = (2 * (limit - 1)) + 2;
            for (var left = 0; left < limit; left++)
            {
                for (var right = 0; right < limit; right++)
                {
                    var receipt = BaselineAlgorithmHardware.SimulateSubtractiveGcd(
                        width,
                        left,
                        right);
                    Assert.Equal(OracleGcd(left, right), receipt.Result);
                    Assert.InRange(receipt.Cycles, 2, cycleBound);
                    Assert.Empty(receipt.Steps);
                }
            }
        }
    }

    [Fact]
    public void SemanticGcdReceiptExposesLoadSubtractionsTerminalAndZeroBehavior()
    {
        var receipt = BaselineAlgorithmHardware.SimulateSubtractiveGcd(
            width: 4,
            left: 15,
            right: 10,
            captureSteps: true);

        Assert.Equal(SubtractiveGcdRunReceipt.EvidenceClass, "SEMANTIC_STEP");
        Assert.Equal(5, receipt.Result);
        Assert.Equal(4, receipt.Cycles);
        Assert.Equal(2, receipt.SubtractionCycles);
        Assert.Equal(receipt.Cycles, receipt.Steps.Count);
        Assert.True(receipt.Steps[0].Load);
        Assert.False(receipt.Steps[^1].Load);
        Assert.Equal(new SubtractiveGcdState(5, 0, Running: false, Done: true), receipt.Steps[^1].After);

        var leftZero = BaselineAlgorithmHardware.SimulateSubtractiveGcd(8, 0, 255);
        Assert.Equal(255, leftZero.Result);
        Assert.Equal(2, leftZero.Cycles);
        Assert.Equal(0, leftZero.SubtractionCycles);

        var bothZero = BaselineAlgorithmHardware.SimulateSubtractiveGcd(8, 0, 0);
        Assert.Equal(0, bothZero.Result);
        Assert.Equal(2, bothZero.Cycles);
    }

    [Fact]
    public void AlgorithmHardwareRejectsOutOfContractWidthsOperandsAndControllerState()
    {
        Assert.False(BaselineAlgorithmHardware.IsSupportedWidth(5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BaselineAlgorithmHardware.BuildUnsignedRestoringDivider(5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BaselineAlgorithmHardware.BuildSubtractiveGcdMachine(5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BaselineAlgorithmHardware.SimulateSubtractiveGcd(4, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BaselineAlgorithmHardware.SimulateSubtractiveGcd(4, 1, 16));
        Assert.Throws<ArgumentException>(() =>
            BaselineAlgorithmHardware.StepSubtractiveGcd(
                4,
                new SubtractiveGcdState(1, 1, Running: true, Done: true)));
    }

    private static Dictionary<string, BitState> CreateWordInputs(
        int width,
        string first,
        string second)
    {
        var result = new Dictionary<string, BitState>(StringComparer.Ordinal);
        for (var index = 0; index < width; index++)
        {
            result[$"{first}[{index}]"] = BitState.Off;
            result[$"{second}[{index}]"] = BitState.Off;
        }

        return result;
    }

    private static Dictionary<string, BitState> CreateGcdInputs(
        int width,
        bool load,
        int left,
        int right)
    {
        var result = CreateWordInputs(width, "left_in", "right_in");
        result["load"] = State(load);
        WriteWord(result, "left_in", width, left);
        WriteWord(result, "right_in", width, right);
        return result;
    }

    private static Dictionary<string, BitState> CreateGcdState(
        int width,
        int left,
        int right,
        bool running,
        bool done)
    {
        var result = CreateWordInputs(width, "left_q", "right_q");
        result["running_q"] = State(running);
        result["done_q"] = State(done);
        WriteWord(result, "left_q", width, left);
        WriteWord(result, "right_q", width, right);
        return result;
    }

    private static SubtractiveGcdState ReadNextGcdState(NandEvaluation evaluated, int width) =>
        new(
            ReadWord(evaluated.DffNextStates, "left_q", width),
            ReadWord(evaluated.DffNextStates, "right_q", width),
            evaluated.DffNextStates["running_q"] == BitState.On,
            evaluated.DffNextStates["done_q"] == BitState.On);

    private static int ReadWord(
        IReadOnlyDictionary<string, BitState> values,
        string name,
        int width)
    {
        var result = 0;
        for (var index = 0; index < width; index++)
        {
            if (values[$"{name}[{index}]"] == BitState.On)
            {
                result |= 1 << index;
            }
        }

        return result;
    }

    private static void WriteWord(
        IDictionary<string, BitState> values,
        string name,
        int width,
        int value)
    {
        for (var index = 0; index < width; index++)
        {
            values[$"{name}[{index}]"] = State((value & (1 << index)) != 0);
        }
    }

    private static int OracleGcd(int left, int right)
    {
        while (right != 0)
        {
            var remainder = left % right;
            left = right;
            right = remainder;
        }

        return left;
    }

    private static BitState State(bool value) =>
        value ? BitState.On : BitState.Off;
}
