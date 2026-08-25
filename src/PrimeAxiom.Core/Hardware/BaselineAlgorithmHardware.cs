using System.Collections.ObjectModel;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hardware;

public readonly record struct SubtractiveGcdState(
    int Left,
    int Right,
    bool Running,
    bool Done);

public sealed record SubtractiveGcdStepReceipt(
    int Cycle,
    bool Load,
    SubtractiveGcdState Before,
    SubtractiveGcdState After);

/// <summary>
/// A deterministic architectural-cycle receipt. The cycle count follows the
/// same load/step transition realized by the NAND netlist. It is semantic
/// evidence, not a gate-transition or physical-energy measurement.
/// </summary>
public sealed record SubtractiveGcdRunReceipt(
    int Width,
    int InputLeft,
    int InputRight,
    int Result,
    int Cycles,
    int SubtractionCycles,
    IReadOnlyList<SubtractiveGcdStepReceipt> Steps)
{
    public const string EvidenceClass = "SEMANTIC_STEP";
}

/// <summary>
/// Conventional algorithm circuits used as Build 002 context controls.
/// Every combinational operation is constructed through the shared
/// NAND-only <see cref="NandLogic"/> layer.
/// </summary>
public static class BaselineAlgorithmHardware
{
    private static readonly int[] SupportedWidths = [4, 6, 8];

    /// <summary>
    /// Constructs an unrolled unsigned restoring divider. Division by zero is
    /// explicit and returns quotient zero, the unchanged dividend as remainder,
    /// and exact=false. No host division is present in the circuit.
    /// </summary>
    public static NandNetlist BuildUnsignedRestoringDivider(int width)
    {
        ValidateWidth(width);
        var builder = new NandNetlistBuilder($"BIN.DIVIDE_RESTORING.W{width}");
        var dividend = Inputs(builder, "dividend", width, "ports/input/dividend");
        var divisor = Inputs(builder, "divisor", width, "ports/input/divisor");
        var zero = builder.Constant("const.zero", BitState.Off, "configuration/constants");

        var divisorNonZero = ReduceOr(
            builder,
            divisor,
            "divide.divisor_nonzero",
            "arithmetic/divide/status/divisor");
        var divideByZero = NandLogic.Not(
            builder,
            divisorNonZero,
            "divide.divide_by_zero",
            "arithmetic/divide/status/divisor");

        var extendedDivisor = new NandSignal[width + 1];
        for (var index = 0; index < width; index++)
        {
            extendedDivisor[index] = divisor[index];
        }

        extendedDivisor[width] = zero;
        var remainder = Enumerable.Repeat(zero, width + 1).ToArray();
        var quotient = new NandSignal[width];

        for (var stage = 0; stage < width; stage++)
        {
            var dividendIndex = width - stage - 1;
            var stagePrefix = $"divide.stage[{stage}]";
            var stageRegion = $"arithmetic/divide/stage:{stage}";
            var shifted = new NandSignal[width + 1];
            shifted[0] = dividend[dividendIndex];
            for (var index = 1; index < shifted.Length; index++)
            {
                shifted[index] = remainder[index - 1];
            }

            var difference = NandLogic.SubtractWord(
                builder,
                shifted,
                extendedDivisor,
                zero,
                $"{stagePrefix}.subtract",
                $"{stageRegion}/subtract");
            var noBorrow = NandLogic.Not(
                builder,
                difference.Status,
                $"{stagePrefix}.no_borrow",
                $"{stageRegion}/control");
            var takeDifference = NandLogic.And(
                builder,
                divisorNonZero,
                noBorrow,
                $"{stagePrefix}.take_difference",
                $"{stageRegion}/control");

            remainder = MuxWord(
                builder,
                takeDifference,
                difference.Value,
                shifted,
                $"{stagePrefix}.restore",
                $"{stageRegion}/restore");
            quotient[dividendIndex] = takeDifference;
        }

        var remainderAny = ReduceOr(
            builder,
            remainder,
            "divide.remainder_any",
            "arithmetic/divide/status/remainder");
        var remainderZero = NandLogic.Not(
            builder,
            remainderAny,
            "divide.remainder_zero",
            "arithmetic/divide/status/remainder");
        var exact = NandLogic.And(
            builder,
            divisorNonZero,
            remainderZero,
            "divide.exact",
            "arithmetic/divide/status/exact");

        Outputs(builder, "quotient", quotient, "ports/output/quotient");
        Outputs(builder, "remainder", remainder.Take(width).ToArray(), "ports/output/remainder");
        builder.Output("divide_by_zero", divideByZero, "ports/output/status");
        builder.Output("exact", exact, "ports/output/status");
        return builder.Build();
    }

    /// <summary>
    /// Constructs one clocked subtractive-GCD machine. Load has priority and
    /// atomically replaces both operands while clearing done. A terminal step
    /// normalizes state to (gcd, 0), clears running, and sets done.
    /// </summary>
    public static NandNetlist BuildSubtractiveGcdMachine(int width)
    {
        ValidateWidth(width);
        var builder = new NandNetlistBuilder($"BIN.GCD_SUBTRACTIVE.W{width}");
        var load = builder.Input("load", "ports/input/control");
        var leftInput = Inputs(builder, "left_in", width, "ports/input/left");
        var rightInput = Inputs(builder, "right_in", width, "ports/input/right");
        var leftState = States(builder, "left_q", width, "state/gcd/left");
        var rightState = States(builder, "right_q", width, "state/gcd/right");
        var runningState = builder.State("running_q", BitState.Off, "state/gcd/control");
        var doneState = builder.State("done_q", BitState.Off, "state/gcd/control");
        var zero = builder.Constant("const.zero", BitState.Off, "configuration/constants");
        var one = builder.Constant("const.one", BitState.On, "configuration/constants");
        var zeroWord = Enumerable.Repeat(zero, width).ToArray();

        var leftZero = IsZero(
            builder,
            leftState,
            "gcd.left_zero",
            "arithmetic/gcd/status/left");
        var rightZero = IsZero(
            builder,
            rightState,
            "gcd.right_zero",
            "arithmetic/gcd/status/right");
        var comparison = NandLogic.CompareWord(
            builder,
            leftState,
            rightState,
            "gcd.compare",
            "arithmetic/gcd/compare");
        var eitherZero = NandLogic.Or(
            builder,
            leftZero,
            rightZero,
            "gcd.either_zero",
            "arithmetic/gcd/control");
        var terminal = NandLogic.Or(
            builder,
            eitherZero,
            comparison.Equal,
            "gcd.terminal",
            "arithmetic/gcd/control");

        var leftMinusRight = NandLogic.SubtractWord(
            builder,
            leftState,
            rightState,
            zero,
            "gcd.left_minus_right",
            "arithmetic/gcd/subtract/left");
        var rightMinusLeft = NandLogic.SubtractWord(
            builder,
            rightState,
            leftState,
            zero,
            "gcd.right_minus_left",
            "arithmetic/gcd/subtract/right");
        var terminalResult = MuxWord(
            builder,
            leftZero,
            rightState,
            leftState,
            "gcd.terminal_result",
            "arithmetic/gcd/terminal");
        var subtractLeft = MuxWord(
            builder,
            comparison.Greater,
            leftMinusRight.Value,
            leftState,
            "gcd.subtract_left",
            "arithmetic/gcd/select/left");
        var subtractRight = MuxWord(
            builder,
            comparison.Less,
            rightMinusLeft.Value,
            rightState,
            "gcd.subtract_right",
            "arithmetic/gcd/select/right");
        var executeLeft = MuxWord(
            builder,
            terminal,
            terminalResult,
            subtractLeft,
            "gcd.execute_left",
            "arithmetic/gcd/execute/left");
        var executeRight = MuxWord(
            builder,
            terminal,
            zeroWord,
            subtractRight,
            "gcd.execute_right",
            "arithmetic/gcd/execute/right");
        var activeLeft = MuxWord(
            builder,
            runningState,
            executeLeft,
            leftState,
            "gcd.active_left",
            "arithmetic/gcd/control/left");
        var activeRight = MuxWord(
            builder,
            runningState,
            executeRight,
            rightState,
            "gcd.active_right",
            "arithmetic/gcd/control/right");
        var nextLeft = MuxWord(
            builder,
            load,
            leftInput,
            activeLeft,
            "gcd.load_left",
            "arithmetic/gcd/load/left");
        var nextRight = MuxWord(
            builder,
            load,
            rightInput,
            activeRight,
            "gcd.load_right",
            "arithmetic/gcd/load/right");

        var notTerminal = NandLogic.Not(
            builder,
            terminal,
            "gcd.not_terminal",
            "arithmetic/gcd/control");
        var continueRunning = NandLogic.And(
            builder,
            runningState,
            notTerminal,
            "gcd.continue_running",
            "arithmetic/gcd/control");
        var nextRunning = NandLogic.Mux(
            builder,
            load,
            one,
            continueRunning,
            "gcd.load_running",
            "arithmetic/gcd/control");
        var activeTerminal = NandLogic.And(
            builder,
            runningState,
            terminal,
            "gcd.active_terminal",
            "arithmetic/gcd/control");
        var doneOrTerminal = NandLogic.Or(
            builder,
            doneState,
            activeTerminal,
            "gcd.done_or_terminal",
            "arithmetic/gcd/control");
        var nextDone = NandLogic.Mux(
            builder,
            load,
            zero,
            doneOrTerminal,
            "gcd.load_done",
            "arithmetic/gcd/control");

        for (var index = 0; index < width; index++)
        {
            builder.Dff($"left_reg[{index}]", nextLeft[index], leftState[index], "state/gcd/left");
            builder.Dff($"right_reg[{index}]", nextRight[index], rightState[index], "state/gcd/right");
            builder.Output($"left[{index}]", leftState[index], "ports/output/left");
            builder.Output($"right[{index}]", rightState[index], "ports/output/right");
            builder.Output($"result[{index}]", leftState[index], "ports/output/result");
        }

        builder.Dff("running_reg", nextRunning, runningState, "state/gcd/control");
        builder.Dff("done_reg", nextDone, doneState, "state/gcd/control");
        builder.Output("running", runningState, "ports/output/status");
        builder.Output("done", doneState, "ports/output/status");
        return builder.Build();
    }

    /// <summary>
    /// Runs the exact registered state transition as a labeled semantic-step
    /// model. Only comparisons and subtraction are used. Gate correctness is
    /// established separately by exhaustive transition tests against the NAND
    /// machine; this method does not claim gate-transition measurements.
    /// </summary>
    public static SubtractiveGcdRunReceipt SimulateSubtractiveGcd(
        int width,
        int left,
        int right,
        bool captureSteps = false)
    {
        ValidateOperand(width, left, nameof(left));
        ValidateOperand(width, right, nameof(right));

        var idle = new SubtractiveGcdState(0, 0, Running: false, Done: false);
        var state = LoadSubtractiveGcd(width, left, right);
        var steps = captureSteps ? new List<SubtractiveGcdStepReceipt>() : null;
        steps?.Add(new SubtractiveGcdStepReceipt(1, Load: true, idle, state));

        var cycles = 1;
        var subtractionCycles = 0;
        var maximumCycles = checked((2 * ((1 << width) - 1)) + 3);
        while (!state.Done)
        {
            if (cycles >= maximumCycles)
            {
                throw new InvalidOperationException("Subtractive GCD exceeded its proven decreasing-sum bound.");
            }

            var before = state;
            var terminal = before.Left == 0 || before.Right == 0 || before.Left == before.Right;
            state = StepSubtractiveGcd(width, before);
            subtractionCycles += terminal ? 0 : 1;

            cycles++;
            steps?.Add(new SubtractiveGcdStepReceipt(cycles, Load: false, before, state));
        }

        return new SubtractiveGcdRunReceipt(
            width,
            left,
            right,
            state.Left,
            cycles,
            subtractionCycles,
            new ReadOnlyCollection<SubtractiveGcdStepReceipt>(steps?.ToArray() ?? []));
    }

    public static SubtractiveGcdState LoadSubtractiveGcd(int width, int left, int right)
    {
        ValidateOperand(width, left, nameof(left));
        ValidateOperand(width, right, nameof(right));
        return new SubtractiveGcdState(left, right, Running: true, Done: false);
    }

    public static SubtractiveGcdState StepSubtractiveGcd(
        int width,
        SubtractiveGcdState state)
    {
        ValidateOperand(width, state.Left, nameof(state));
        ValidateOperand(width, state.Right, nameof(state));
        if (state.Running && state.Done)
        {
            throw new ArgumentException(
                "A subtractive-GCD controller state cannot be both running and done.",
                nameof(state));
        }

        if (!state.Running)
        {
            return state;
        }

        if (state.Left == 0 || state.Right == 0 || state.Left == state.Right)
        {
            var result = state.Left == 0 ? state.Right : state.Left;
            return new SubtractiveGcdState(result, 0, Running: false, Done: true);
        }

        return state.Left > state.Right
            ? state with { Left = state.Left - state.Right }
            : state with { Right = state.Right - state.Left };
    }

    public static bool IsSupportedWidth(int width) => SupportedWidths.Contains(width);

    private static NandSignal IsZero(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> word,
        string prefix,
        string region)
    {
        var any = ReduceOr(builder, word, $"{prefix}.any", region);
        return NandLogic.Not(builder, any, $"{prefix}.not", region);
    }

    private static NandSignal ReduceOr(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> signals,
        string prefix,
        string region)
    {
        ArgumentNullException.ThrowIfNull(signals);
        if (signals.Count == 0)
        {
            throw new ArgumentException("An OR reduction requires at least one signal.", nameof(signals));
        }

        var result = signals[0];
        for (var index = 1; index < signals.Count; index++)
        {
            result = NandLogic.Or(
                builder,
                result,
                signals[index],
                $"{prefix}.or[{index}]",
                $"{region}/step:{index}");
        }

        return result;
    }

    private static NandSignal[] MuxWord(
        NandNetlistBuilder builder,
        NandSignal select,
        IReadOnlyList<NandSignal> whenOn,
        IReadOnlyList<NandSignal> whenOff,
        string prefix,
        string region)
    {
        if (whenOn.Count == 0 || whenOn.Count != whenOff.Count)
        {
            throw new ArgumentException("Mux words must have equal positive widths.");
        }

        var result = new NandSignal[whenOn.Count];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = NandLogic.Mux(
                builder,
                select,
                whenOn[index],
                whenOff[index],
                $"{prefix}.bit[{index}]",
                $"{region}/bit:{index}");
        }

        return result;
    }

    private static NandSignal[] Inputs(
        NandNetlistBuilder builder,
        string name,
        int width,
        string region) =>
        Enumerable.Range(0, width)
            .Select(index => builder.Input($"{name}[{index}]", $"{region}/bit:{index}"))
            .ToArray();

    private static NandSignal[] States(
        NandNetlistBuilder builder,
        string name,
        int width,
        string region) =>
        Enumerable.Range(0, width)
            .Select(index => builder.State($"{name}[{index}]", BitState.Off, $"{region}/bit:{index}"))
            .ToArray();

    private static void Outputs(
        NandNetlistBuilder builder,
        string name,
        IReadOnlyList<NandSignal> signals,
        string region)
    {
        for (var index = 0; index < signals.Count; index++)
        {
            builder.Output($"{name}[{index}]", signals[index], $"{region}/bit:{index}");
        }
    }

    private static void ValidateOperand(int width, int value, string parameterName)
    {
        ValidateWidth(width);
        var maximum = (1 << width) - 1;
        if (value < 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Operand must be in [0, {maximum}].");
        }
    }

    private static void ValidateWidth(int width)
    {
        if (!IsSupportedWidth(width))
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "Build 002 conventional hardware widths are frozen to 4, 6, and 8 bits.");
        }
    }
}
