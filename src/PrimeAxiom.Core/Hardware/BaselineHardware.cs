using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hardware;

public enum BaselineFuOperation : byte
{
    Add = 0,
    Subtract = 1,
    Multiply = 2,
    Compare = 3,
}

public readonly record struct NandAddBitResult(NandSignal Sum, NandSignal Carry);

public sealed record NandWordStatusResult(NandSignal[] Value, NandSignal Status);

public readonly record struct NandCompareSignalResult(
    NandSignal Less,
    NandSignal Equal,
    NandSignal Greater);

/// <summary>
/// Reusable NAND-only combinational constructors. A caller supplies stable
/// prefixes and regions, so later conventional and valuation builders share
/// exactly the same gate implementations rather than copying formulas.
/// </summary>
public static class NandLogic
{
    public static NandSignal Not(
        NandNetlistBuilder builder,
        NandSignal input,
        string prefix,
        string region)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Nand($"{prefix}.nand", input, input, region);
    }

    public static NandSignal And(
        NandNetlistBuilder builder,
        NandSignal left,
        NandSignal right,
        string prefix,
        string region)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var notAnd = builder.Nand($"{prefix}.nand", left, right, region);
        return builder.Nand($"{prefix}.invert", notAnd, notAnd, region);
    }

    public static NandSignal Or(
        NandNetlistBuilder builder,
        NandSignal left,
        NandSignal right,
        string prefix,
        string region)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var notLeft = builder.Nand($"{prefix}.not_left", left, left, region);
        var notRight = builder.Nand($"{prefix}.not_right", right, right, region);
        return builder.Nand($"{prefix}.nand", notLeft, notRight, region);
    }

    public static NandSignal Xor(
        NandNetlistBuilder builder,
        NandSignal left,
        NandSignal right,
        string prefix,
        string region)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var shared = builder.Nand($"{prefix}.shared", left, right, region);
        var leftArm = builder.Nand($"{prefix}.left_arm", left, shared, region);
        var rightArm = builder.Nand($"{prefix}.right_arm", right, shared, region);
        return builder.Nand($"{prefix}.output", leftArm, rightArm, region);
    }

    public static NandSignal Xnor(
        NandNetlistBuilder builder,
        NandSignal left,
        NandSignal right,
        string prefix,
        string region)
    {
        var xor = Xor(builder, left, right, $"{prefix}.xor", region);
        return Not(builder, xor, $"{prefix}.not", region);
    }

    public static NandSignal Mux(
        NandNetlistBuilder builder,
        NandSignal select,
        NandSignal whenOn,
        NandSignal whenOff,
        string prefix,
        string region)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var notSelect = builder.Nand($"{prefix}.not_select", select, select, region);
        var onArm = builder.Nand($"{prefix}.on_arm", select, whenOn, region);
        var offArm = builder.Nand($"{prefix}.off_arm", notSelect, whenOff, region);
        return builder.Nand($"{prefix}.output", onArm, offArm, region);
    }

    public static NandAddBitResult HalfAdd(
        NandNetlistBuilder builder,
        NandSignal left,
        NandSignal right,
        string prefix,
        string region) =>
        new(
            Xor(builder, left, right, $"{prefix}.sum", region),
            And(builder, left, right, $"{prefix}.carry", region));

    public static NandAddBitResult FullAdd(
        NandNetlistBuilder builder,
        NandSignal left,
        NandSignal right,
        NandSignal carryIn,
        string prefix,
        string region)
    {
        var pairXor = Xor(builder, left, right, $"{prefix}.xor_ab", region);
        var sum = Xor(builder, pairXor, carryIn, $"{prefix}.sum", region);
        var pairCarry = And(builder, left, right, $"{prefix}.carry_ab", region);
        var inputCarry = And(builder, pairXor, carryIn, $"{prefix}.carry_input", region);
        var carry = Or(builder, pairCarry, inputCarry, $"{prefix}.carry_out", region);
        return new NandAddBitResult(sum, carry);
    }

    public static NandWordStatusResult AddWord(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> left,
        IReadOnlyList<NandSignal> right,
        NandSignal carryIn,
        string prefix,
        string region)
    {
        EnsureSameWidth(left, right);
        var sum = new NandSignal[left.Count];
        var carry = carryIn;
        for (var index = 0; index < left.Count; index++)
        {
            var bit = FullAdd(
                builder,
                left[index],
                right[index],
                carry,
                $"{prefix}.bit[{index}]",
                $"{region}/bit:{index}");
            sum[index] = bit.Sum;
            carry = bit.Carry;
        }

        return new NandWordStatusResult(sum, carry);
    }

    public static NandWordStatusResult SubtractWord(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> left,
        IReadOnlyList<NandSignal> right,
        NandSignal borrowIn,
        string prefix,
        string region)
    {
        EnsureSameWidth(left, right);
        var difference = new NandSignal[left.Count];
        var borrow = borrowIn;
        for (var index = 0; index < left.Count; index++)
        {
            var bitPrefix = $"{prefix}.bit[{index}]";
            var bitRegion = $"{region}/bit:{index}";
            var leftXorRight = Xor(builder, left[index], right[index], $"{bitPrefix}.xor_ab", bitRegion);
            difference[index] = Xor(builder, leftXorRight, borrow, $"{bitPrefix}.difference", bitRegion);
            var notLeft = Not(builder, left[index], $"{bitPrefix}.not_a", bitRegion);
            var rightOrBorrow = Or(builder, right[index], borrow, $"{bitPrefix}.b_or_borrow", bitRegion);
            var first = And(builder, notLeft, rightOrBorrow, $"{bitPrefix}.borrow_first", bitRegion);
            var second = And(builder, right[index], borrow, $"{bitPrefix}.borrow_second", bitRegion);
            borrow = Or(builder, first, second, $"{bitPrefix}.borrow_out", bitRegion);
        }

        return new NandWordStatusResult(difference, borrow);
    }

    public static NandCompareSignalResult CompareWord(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> left,
        IReadOnlyList<NandSignal> right,
        string prefix,
        string region)
    {
        EnsureSameWidth(left, right);
        var equal = builder.Constant($"{prefix}.initial_equal", BitState.On, $"{region}/initial");
        var greater = builder.Constant($"{prefix}.initial_greater", BitState.Off, $"{region}/initial");
        var less = builder.Constant($"{prefix}.initial_less", BitState.Off, $"{region}/initial");
        for (var index = left.Count - 1; index >= 0; index--)
        {
            var bitPrefix = $"{prefix}.bit[{index}]";
            var bitRegion = $"{region}/bit:{index}";
            var notLeft = Not(builder, left[index], $"{bitPrefix}.not_a", bitRegion);
            var notRight = Not(builder, right[index], $"{bitPrefix}.not_b", bitRegion);
            var leftGreater = And(builder, left[index], notRight, $"{bitPrefix}.a_gt_b", bitRegion);
            var leftLess = And(builder, notLeft, right[index], $"{bitPrefix}.a_lt_b", bitRegion);
            var newlyGreater = And(builder, equal, leftGreater, $"{bitPrefix}.new_greater", bitRegion);
            var newlyLess = And(builder, equal, leftLess, $"{bitPrefix}.new_less", bitRegion);
            greater = Or(builder, greater, newlyGreater, $"{bitPrefix}.greater", bitRegion);
            less = Or(builder, less, newlyLess, $"{bitPrefix}.less", bitRegion);
            var same = Xnor(builder, left[index], right[index], $"{bitPrefix}.same", bitRegion);
            equal = And(builder, equal, same, $"{bitPrefix}.equal", bitRegion);
        }

        return new NandCompareSignalResult(less, equal, greater);
    }

    private static void EnsureSameWidth(
        IReadOnlyCollection<NandSignal> left,
        IReadOnlyCollection<NandSignal> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Count == 0 || left.Count != right.Count)
        {
            throw new ArgumentException("Signal-vector operands must have equal positive widths.");
        }
    }
}

/// <summary>
/// Transparent conventional hardware constructions for the frozen Build 002
/// widths. All combinational cells are introduced through
/// <see cref="NandNetlistBuilder.Nand"/>.
/// </summary>
public static class BaselineHardware
{
    private static readonly int[] SupportedWidths = [4, 6, 8];

    public static NandNetlist BuildNotGate()
    {
        var builder = new NandNetlistBuilder("BIN.NOT");
        var input = builder.Input("a");
        builder.Output("y", NandLogic.Not(builder, input, "not", "logic/not"));
        return builder.Build();
    }

    public static NandNetlist BuildAndGate()
    {
        var builder = new NandNetlistBuilder("BIN.AND");
        var left = builder.Input("a");
        var right = builder.Input("b");
        builder.Output("y", NandLogic.And(builder, left, right, "and", "logic/and"));
        return builder.Build();
    }

    public static NandNetlist BuildOrGate()
    {
        var builder = new NandNetlistBuilder("BIN.OR");
        var left = builder.Input("a");
        var right = builder.Input("b");
        builder.Output("y", NandLogic.Or(builder, left, right, "or", "logic/or"));
        return builder.Build();
    }

    public static NandNetlist BuildXorGate()
    {
        var builder = new NandNetlistBuilder("BIN.XOR");
        var left = builder.Input("a");
        var right = builder.Input("b");
        builder.Output("y", NandLogic.Xor(builder, left, right, "xor", "logic/xor"));
        return builder.Build();
    }

    public static NandNetlist BuildXnorGate()
    {
        var builder = new NandNetlistBuilder("BIN.XNOR");
        var left = builder.Input("a");
        var right = builder.Input("b");
        builder.Output("y", NandLogic.Xnor(builder, left, right, "xnor", "logic/xnor"));
        return builder.Build();
    }

    public static NandNetlist BuildMuxGate()
    {
        var builder = new NandNetlistBuilder("BIN.MUX2");
        var select = builder.Input("select");
        var whenOn = builder.Input("when_on");
        var whenOff = builder.Input("when_off");
        builder.Output("y", NandLogic.Mux(builder, select, whenOn, whenOff, "mux", "logic/mux"));
        return builder.Build();
    }

    public static NandNetlist BuildHalfAdder()
    {
        var builder = new NandNetlistBuilder("BIN.HALF_ADDER");
        var left = builder.Input("a");
        var right = builder.Input("b");
        var result = NandLogic.HalfAdd(builder, left, right, "half", "arithmetic/half_adder");
        builder.Output("sum", result.Sum);
        builder.Output("carry", result.Carry);
        return builder.Build();
    }

    public static NandNetlist BuildFullAdder()
    {
        var builder = new NandNetlistBuilder("BIN.FULL_ADDER");
        var left = builder.Input("a");
        var right = builder.Input("b");
        var carry = builder.Input("carry_in");
        var result = NandLogic.FullAdd(builder, left, right, carry, "full", "arithmetic/full_adder");
        builder.Output("sum", result.Sum);
        builder.Output("carry_out", result.Carry);
        return builder.Build();
    }

    public static NandNetlist BuildRippleAdder(int width)
    {
        ValidateWidth(width);
        var builder = new NandNetlistBuilder($"BIN.ADD.W{width}");
        var left = Inputs(builder, "a", width);
        var right = Inputs(builder, "b", width);
        var carry = builder.Input("carry_in");
        var result = NandLogic.AddWord(builder, left, right, carry, "add", "arithmetic/add");
        Outputs(builder, "sum", result.Value);
        builder.Output("carry_out", result.Status);
        return builder.Build();
    }

    public static NandNetlist BuildRippleSubtractor(int width)
    {
        ValidateWidth(width);
        var builder = new NandNetlistBuilder($"BIN.SUB.W{width}");
        var left = Inputs(builder, "a", width);
        var right = Inputs(builder, "b", width);
        var zero = builder.Constant("const.zero", BitState.Off);
        var result = NandLogic.SubtractWord(builder, left, right, zero, "sub", "arithmetic/subtract");
        Outputs(builder, "difference", result.Value);
        builder.Output("borrow_out", result.Status);
        return builder.Build();
    }

    public static NandNetlist BuildComparator(int width)
    {
        ValidateWidth(width);
        var builder = new NandNetlistBuilder($"BIN.COMPARE.W{width}");
        var left = Inputs(builder, "a", width);
        var right = Inputs(builder, "b", width);
        var result = NandLogic.CompareWord(builder, left, right, "compare", "arithmetic/compare");
        builder.Output("less", result.Less);
        builder.Output("equal", result.Equal);
        builder.Output("greater", result.Greater);
        return builder.Build();
    }

    public static NandNetlist BuildShiftAddMultiplier(int width)
    {
        ValidateWidth(width);
        var builder = new NandNetlistBuilder($"BIN.MULTIPLY_SHIFT_ADD.W{width}");
        var left = Inputs(builder, "a", width);
        var right = Inputs(builder, "b", width);
        var zero = builder.Constant("const.zero", BitState.Off);
        var product = MultiplyWords(builder, left, right, zero, "multiply", "arithmetic/multiply");
        Outputs(builder, "product", product);
        return builder.Build();
    }

    public static NandNetlist BuildFunctionalUnit(int width)
    {
        ValidateWidth(width);
        var builder = new NandNetlistBuilder($"BIN.FU.COMBINATIONAL.W{width}");
        var left = Inputs(builder, "a", width);
        var right = Inputs(builder, "b", width);
        var opcode = Inputs(builder, "opcode", 2);
        var result = FunctionalResult(builder, left, right, opcode, "fu");
        Outputs(builder, "result", result);
        return builder.Build();
    }

    public static NandNetlist BuildRegisteredFunctionalUnit(int width)
    {
        ValidateWidth(width);
        var builder = new NandNetlistBuilder($"BIN.FU.REGISTERED.W{width}");
        var leftInput = Inputs(builder, "a_in", width, "ports/input/a");
        var rightInput = Inputs(builder, "b_in", width, "ports/input/b");
        var opcodeInput = Inputs(builder, "opcode_in", 2, "ports/input/opcode");

        var leftState = States(builder, "a_q", width, "state/input/a");
        var rightState = States(builder, "b_q", width, "state/input/b");
        var opcodeState = States(builder, "opcode_q", 2, "state/input/opcode");
        for (var index = 0; index < width; index++)
        {
            builder.Dff($"a_reg[{index}]", leftInput[index], leftState[index], "state/input/a");
            builder.Dff($"b_reg[{index}]", rightInput[index], rightState[index], "state/input/b");
        }

        for (var index = 0; index < opcodeState.Length; index++)
        {
            builder.Dff(
                $"opcode_reg[{index}]",
                opcodeInput[index],
                opcodeState[index],
                "state/input/opcode");
        }

        var resultData = FunctionalResult(builder, leftState, rightState, opcodeState, "fu");
        var resultState = States(builder, "result_q", checked(width * 2), "state/output/result");
        for (var index = 0; index < resultState.Length; index++)
        {
            builder.Dff(
                $"result_reg[{index}]",
                resultData[index],
                resultState[index],
                "state/output/result");
            builder.Output($"result[{index}]", resultState[index], "ports/output/result");
        }

        return builder.Build();
    }

    public static NandNetlist BuildRegisterBoundary(int width)
    {
        ValidateWidth(width);
        var builder = new NandNetlistBuilder($"BIN.REGISTER.W{width}");
        var data = Inputs(builder, "data", width, "ports/input/data");
        var enable = builder.Input("enable", "ports/input/control");
        var state = States(builder, "q", width, "state/register");
        for (var index = 0; index < width; index++)
        {
            var next = NandLogic.Mux(
                builder,
                enable,
                data[index],
                state[index],
                $"register.bit[{index}].enable",
                $"state/register/bit:{index}");
            builder.Dff($"register[{index}]", next, state[index], "state/register");
            builder.Output($"q[{index}]", state[index], "ports/output/q");
        }

        return builder.Build();
    }

    public static NandNetlist BuildCounterBoundary(int width)
    {
        ValidateWidth(width);
        var builder = new NandNetlistBuilder($"BIN.COUNTER.W{width}");
        var enable = builder.Input("enable", "ports/input/control");
        var state = States(builder, "q", width, "state/counter");
        var zero = builder.Constant("const.zero", BitState.Off);
        var one = builder.Constant("const.one", BitState.On);
        var increment = new NandSignal[width];
        increment[0] = one;
        for (var index = 1; index < width; index++)
        {
            increment[index] = zero;
        }

        var added = NandLogic.AddWord(
            builder,
            state,
            increment,
            zero,
            "counter.increment",
            "state/counter/increment");
        for (var index = 0; index < width; index++)
        {
            var next = NandLogic.Mux(
                builder,
                enable,
                added.Value[index],
                state[index],
                $"counter.bit[{index}].enable",
                $"state/counter/bit:{index}");
            builder.Dff($"counter[{index}]", next, state[index], "state/counter");
            builder.Output($"q[{index}]", state[index], "ports/output/q");
        }

        var overflow = NandLogic.And(
            builder,
            enable,
            added.Status,
            "counter.overflow_enabled",
            "state/counter/status");
        builder.Output("overflow", overflow, "ports/output/status");
        return builder.Build();
    }

    public static bool IsSupportedWidth(int width) => SupportedWidths.Contains(width);

    private static NandSignal[] FunctionalResult(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> left,
        IReadOnlyList<NandSignal> right,
        IReadOnlyList<NandSignal> opcode,
        string prefix)
    {
        if (left.Count != right.Count || opcode.Count != 2)
        {
            throw new ArgumentException("Functional-unit operands or opcode have invalid widths.");
        }

        var width = left.Count;
        var resultWidth = checked(width * 2);
        var zero = builder.Constant($"{prefix}.const.zero", BitState.Off, $"{prefix}/configuration");
        var add = NandLogic.AddWord(builder, left, right, zero, $"{prefix}.add", $"{prefix}/add");
        var subtract = NandLogic.SubtractWord(
            builder,
            left,
            right,
            zero,
            $"{prefix}.subtract",
            $"{prefix}/subtract");
        var compare = NandLogic.CompareWord(
            builder,
            left,
            right,
            $"{prefix}.compare",
            $"{prefix}/compare");
        var multiply = MultiplyWords(
            builder,
            left,
            right,
            zero,
            $"{prefix}.multiply",
            $"{prefix}/multiply");

        var addBus = Enumerable.Repeat(zero, resultWidth).ToArray();
        var subtractBus = Enumerable.Repeat(zero, resultWidth).ToArray();
        var compareBus = Enumerable.Repeat(zero, resultWidth).ToArray();
        for (var index = 0; index < width; index++)
        {
            addBus[index] = add.Value[index];
            subtractBus[index] = subtract.Value[index];
        }

        addBus[width] = add.Status;
        subtractBus[width] = subtract.Status;
        compareBus[0] = compare.Less;
        compareBus[1] = compare.Equal;
        compareBus[2] = compare.Greater;

        var selected = new NandSignal[resultWidth];
        for (var index = 0; index < resultWidth; index++)
        {
            var addOrSubtract = NandLogic.Mux(
                builder,
                opcode[0],
                subtractBus[index],
                addBus[index],
                $"{prefix}.select.bit[{index}].low",
                $"{prefix}/select/bit:{index}");
            var multiplyOrCompare = NandLogic.Mux(
                builder,
                opcode[0],
                compareBus[index],
                multiply[index],
                $"{prefix}.select.bit[{index}].high",
                $"{prefix}/select/bit:{index}");
            selected[index] = NandLogic.Mux(
                builder,
                opcode[1],
                multiplyOrCompare,
                addOrSubtract,
                $"{prefix}.select.bit[{index}].final",
                $"{prefix}/select/bit:{index}");
        }

        return selected;
    }

    private static NandSignal[] MultiplyWords(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> left,
        IReadOnlyList<NandSignal> right,
        NandSignal zero,
        string prefix,
        string region)
    {
        EnsureSameWidth(left, right);
        var outputWidth = checked(left.Count * 2);
        var accumulator = Enumerable.Repeat(zero, outputWidth).ToArray();
        for (var multiplierIndex = 0; multiplierIndex < right.Count; multiplierIndex++)
        {
            var partial = Enumerable.Repeat(zero, outputWidth).ToArray();
            for (var multiplicandIndex = 0; multiplicandIndex < left.Count; multiplicandIndex++)
            {
                var destination = multiplicandIndex + multiplierIndex;
                partial[destination] = NandLogic.And(
                    builder,
                    left[multiplicandIndex],
                    right[multiplierIndex],
                    $"{prefix}.partial[{multiplierIndex},{multiplicandIndex}]",
                    $"{region}/row:{multiplierIndex}/bit:{destination}");
            }

            accumulator = NandLogic.AddWord(
                builder,
                accumulator,
                partial,
                zero,
                $"{prefix}.accumulate[{multiplierIndex}]",
                $"{region}/accumulate:{multiplierIndex}").Value;
        }

        return accumulator;
    }

    private static NandSignal[] Inputs(
        NandNetlistBuilder builder,
        string name,
        int width,
        string? region = null) =>
        Enumerable.Range(0, width)
            .Select(index => builder.Input($"{name}[{index}]", region ?? $"ports/input/{name}"))
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
        IReadOnlyList<NandSignal> signals)
    {
        for (var index = 0; index < signals.Count; index++)
        {
            builder.Output($"{name}[{index}]", signals[index], $"ports/output/{name}");
        }
    }

    private static void EnsureSameWidth(
        IReadOnlyList<NandSignal> left,
        IReadOnlyList<NandSignal> right)
    {
        if (left.Count == 0 || left.Count != right.Count)
        {
            throw new ArgumentException("Signal-vector operands must have equal positive widths.");
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
