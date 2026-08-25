using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hardware;

public enum BinaryMagnitudeDatapathOperation : byte
{
    Load = 0,
    Scale = 3,
    Cancel = 4,
    AddMagnitude = 5,
}

public sealed record BinaryMagnitudeDatapathPortLayout(
    int Width,
    IReadOnlyList<string> OpcodeInputs,
    IReadOnlyList<string> PrimeSelectInputs,
    IReadOnlyList<string> OperandInputs,
    IReadOnlyList<string> StateMagnitudeNames,
    IReadOnlyList<string> MagnitudeOutputNames,
    string ZeroOutput,
    string RejectOutput,
    string AcceptedOutput,
    string OverflowOutput,
    string NotDivisibleOutput);

public sealed record DeclaredBinaryMagnitudeDatapath(
    NandNetlist Netlist,
    BinaryMagnitudeDatapathPortLayout Ports,
    string EvidenceClass = "STRUCTURAL_DECLARED_INTEGRATED");

/// <summary>
/// Matched conventional baseline for integrated sidecar experiments. It keeps
/// only an authoritative W-bit magnitude, while retaining the same selected
/// S4 factor, arithmetic, status, and atomic-update boundary as the sidecar
/// machine. Every runtime combinational cell is NAND2.
/// </summary>
public static class BinaryMagnitudeDatapathHardware
{
    private const int OpcodeWidth = 3;

    public static DeclaredBinaryMagnitudeDatapath Build(int width)
    {
        if (!ValuationHardwareDomain.IsSupportedWidth(width))
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "Build 002 hardware widths are exactly 4, 6, and 8 bits.");
        }

        var ports = CreateLayout(width);
        var builder = new NandNetlistBuilder($"BIN.MAGNITUDE.DATAPATH.W{width}");
        var opcode = Inputs(builder, ports.OpcodeInputs, "ports/input/control/opcode");
        var primeSelect = Inputs(builder, ports.PrimeSelectInputs, "ports/input/control/prime");
        var operand = Inputs(builder, ports.OperandInputs, "ports/input/operand");
        var magnitude = States(builder, ports.StateMagnitudeNames, "state/magnitude");
        var zero = builder.Constant("const.zero", BitState.Off, "configuration/constants");
        var one = builder.Constant("const.one", BitState.On, "configuration/constants");

        var opcodeDecode = DecodeWord(
            builder,
            opcode,
            Enum.GetValues<BinaryMagnitudeDatapathOperation>().Select(value => (int)value),
            one,
            "control.opcode",
            "control/decode/opcode");
        var factor = SelectFactorWord(builder, primeSelect, width, zero, one);
        var product = MultiplyWords(
            builder,
            magnitude,
            factor,
            zero,
            "scale.multiply",
            "datapath/scale/multiply");
        var scaleOverflow = OrMany(
            builder,
            product.Skip(width),
            zero,
            "scale.overflow",
            "datapath/scale/status");
        var notScaleOverflow = NandLogic.Not(
            builder,
            scaleOverflow,
            "scale.not_overflow",
            "datapath/scale/status");
        var scaleAccepted = NandLogic.And(
            builder,
            opcodeDecode[(int)BinaryMagnitudeDatapathOperation.Scale],
            notScaleOverflow,
            "scale.accepted",
            "datapath/scale/status");

        var division = DivideWords(
            builder,
            magnitude,
            factor,
            zero,
            "cancel.divide",
            "datapath/cancel/divide");
        var cancelAccepted = NandLogic.And(
            builder,
            opcodeDecode[(int)BinaryMagnitudeDatapathOperation.Cancel],
            division.Exact,
            "cancel.accepted",
            "datapath/cancel/status");
        var notDivisionExact = NandLogic.Not(
            builder,
            division.Exact,
            "cancel.not_exact",
            "datapath/cancel/status");

        var addition = NandLogic.AddWord(
            builder,
            magnitude,
            operand,
            zero,
            "add.magnitude",
            "datapath/add/magnitude");
        var notAddOverflow = NandLogic.Not(
            builder,
            addition.Status,
            "add.not_overflow",
            "datapath/add/status");
        var addAccepted = NandLogic.And(
            builder,
            opcodeDecode[(int)BinaryMagnitudeDatapathOperation.AddMagnitude],
            notAddOverflow,
            "add.accepted",
            "datapath/add/status");
        var loadAccepted = opcodeDecode[(int)BinaryMagnitudeDatapathOperation.Load];
        var accepted = OrMany(
            builder,
            [loadAccepted, scaleAccepted, cancelAccepted, addAccepted],
            zero,
            "status.accepted",
            "control/status");
        var reject = NandLogic.Not(builder, accepted, "status.reject", "control/status");
        var scaleOverflowStatus = NandLogic.And(
            builder,
            opcodeDecode[(int)BinaryMagnitudeDatapathOperation.Scale],
            scaleOverflow,
            "status.scale_overflow",
            "control/status");
        var addOverflowStatus = NandLogic.And(
            builder,
            opcodeDecode[(int)BinaryMagnitudeDatapathOperation.AddMagnitude],
            addition.Status,
            "status.add_overflow",
            "control/status");
        var overflow = NandLogic.Or(
            builder,
            scaleOverflowStatus,
            addOverflowStatus,
            "status.overflow",
            "control/status");
        var notDivisible = NandLogic.And(
            builder,
            opcodeDecode[(int)BinaryMagnitudeDatapathOperation.Cancel],
            notDivisionExact,
            "status.not_divisible",
            "control/status");

        for (var bit = 0; bit < width; bit++)
        {
            var candidate = SelectOperationValue(
                builder,
                opcodeDecode,
                magnitude[bit],
                operand[bit],
                product[bit],
                division.Quotient[bit],
                addition.Value[bit],
                $"next.magnitude.bit[{bit}]",
                $"datapath/next/magnitude/bit:{bit}");
            var next = NandLogic.Mux(
                builder,
                accepted,
                candidate,
                magnitude[bit],
                $"atomic_hold.magnitude.bit[{bit}]",
                $"datapath/atomic_hold/magnitude/bit:{bit}");
            builder.Dff($"magnitude_reg[{bit}]", next, magnitude[bit], "state/magnitude");
            builder.Output(
                ports.MagnitudeOutputNames[bit],
                magnitude[bit],
                $"ports/output/magnitude/bit:{bit}");
        }

        var magnitudeZero = IsZero(
            builder,
            magnitude,
            zero,
            "status.zero",
            "control/status");
        builder.Output(ports.ZeroOutput, magnitudeZero, "ports/output/status");
        builder.Output(ports.RejectOutput, reject, "ports/output/status");
        builder.Output(ports.AcceptedOutput, accepted, "ports/output/status");
        builder.Output(ports.OverflowOutput, overflow, "ports/output/status");
        builder.Output(ports.NotDivisibleOutput, notDivisible, "ports/output/status");
        return new DeclaredBinaryMagnitudeDatapath(builder.Build(), ports);
    }

    public static Dictionary<string, BitState> EncodeInputs(
        DeclaredBinaryMagnitudeDatapath machine,
        BinaryMagnitudeDatapathOperation operation,
        int prime = 2,
        int operand = 0)
    {
        ArgumentNullException.ThrowIfNull(machine);
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        var lane = ValuationHardwareDomain.S4.ToList().IndexOf(prime);
        if (lane < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prime));
        }

        var maximum = (1 << machine.Ports.Width) - 1;
        if (operand < 0 || operand > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(operand));
        }

        var encoded = new Dictionary<string, BitState>(StringComparer.Ordinal);
        WriteUnsigned(encoded, machine.Ports.OpcodeInputs, (int)operation);
        WriteUnsigned(encoded, machine.Ports.PrimeSelectInputs, lane);
        WriteUnsigned(encoded, machine.Ports.OperandInputs, operand);
        return encoded;
    }

    public static Dictionary<string, BitState> EncodeState(
        DeclaredBinaryMagnitudeDatapath machine,
        int magnitude)
    {
        ArgumentNullException.ThrowIfNull(machine);
        var maximum = (1 << machine.Ports.Width) - 1;
        if (magnitude < 0 || magnitude > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(magnitude));
        }

        var encoded = new Dictionary<string, BitState>(StringComparer.Ordinal);
        WriteUnsigned(encoded, machine.Ports.StateMagnitudeNames, magnitude);
        return encoded;
    }

    public static int DecodeNextMagnitude(
        DeclaredBinaryMagnitudeDatapath machine,
        NandEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(evaluation);
        return ReadUnsigned(evaluation.DffNextStates, machine.Ports.StateMagnitudeNames);
    }

    public static Dictionary<string, BitState> AdvanceState(NandEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        return evaluation.DffNextStates.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
    }

    private static BinaryMagnitudeDatapathPortLayout CreateLayout(int width) =>
        new(
            width,
            Enumerable.Range(0, OpcodeWidth).Select(bit => $"opcode[{bit}]").ToArray(),
            ["prime_select[0]", "prime_select[1]"],
            Enumerable.Range(0, width).Select(bit => $"operand[{bit}]").ToArray(),
            Enumerable.Range(0, width).Select(bit => $"magnitude_q[{bit}]").ToArray(),
            Enumerable.Range(0, width).Select(bit => $"magnitude[{bit}]").ToArray(),
            "zero",
            "reject",
            "accepted",
            "overflow",
            "not_divisible");

    private static Dictionary<int, NandSignal> DecodeWord(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> word,
        IEnumerable<int> values,
        NandSignal identity,
        string prefix,
        string region)
    {
        var inverted = word.Select((bit, index) => NandLogic.Not(
            builder,
            bit,
            $"{prefix}.not[{index}]",
            region)).ToArray();
        var decoded = new Dictionary<int, NandSignal>();
        foreach (var value in values)
        {
            var literals = word.Select((bit, index) =>
                (value & (1 << index)) != 0 ? bit : inverted[index]);
            decoded.Add(value, AndMany(
                builder,
                literals,
                identity,
                $"{prefix}.value[{value}]",
                region));
        }

        return decoded;
    }

    private static NandSignal[] SelectFactorWord(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> select,
        int width,
        NandSignal zero,
        NandSignal one)
    {
        var factors = ValuationHardwareDomain.S4
            .Select(prime => ConstantWord(prime, width, zero, one))
            .ToArray();
        var selected = new NandSignal[width];
        for (var bit = 0; bit < width; bit++)
        {
            var low = NandLogic.Mux(
                builder,
                select[0],
                factors[1][bit],
                factors[0][bit],
                $"factor.low.bit[{bit}]",
                $"control/factor/bit:{bit}");
            var high = NandLogic.Mux(
                builder,
                select[0],
                factors[3][bit],
                factors[2][bit],
                $"factor.high.bit[{bit}]",
                $"control/factor/bit:{bit}");
            selected[bit] = NandLogic.Mux(
                builder,
                select[1],
                high,
                low,
                $"factor.select.bit[{bit}]",
                $"control/factor/bit:{bit}");
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
        var resultWidth = checked(left.Count + right.Count);
        var accumulated = Enumerable.Repeat(zero, resultWidth).ToArray();
        for (var row = 0; row < right.Count; row++)
        {
            var partial = Enumerable.Repeat(zero, resultWidth).ToArray();
            for (var bit = 0; bit < left.Count; bit++)
            {
                partial[row + bit] = NandLogic.And(
                    builder,
                    left[bit],
                    right[row],
                    $"{prefix}.row[{row}].partial[{bit}]",
                    $"{region}/row:{row}/partial");
            }

            accumulated = NandLogic.AddWord(
                builder,
                accumulated,
                partial,
                zero,
                $"{prefix}.row[{row}].add",
                $"{region}/row:{row}/add").Value;
        }

        return accumulated;
    }

    private static DivisionSignals DivideWords(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> dividend,
        IReadOnlyList<NandSignal> divisor,
        NandSignal zero,
        string prefix,
        string region)
    {
        var width = dividend.Count;
        var divisorNonzero = OrMany(
            builder,
            divisor,
            zero,
            $"{prefix}.divisor_nonzero",
            $"{region}/status");
        var extendedDivisor = divisor.Concat([zero]).ToArray();
        var remainder = Enumerable.Repeat(zero, width + 1).ToArray();
        var quotient = new NandSignal[width];
        for (var stage = 0; stage < width; stage++)
        {
            var sourceBit = width - stage - 1;
            var shifted = new NandSignal[width + 1];
            shifted[0] = dividend[sourceBit];
            for (var bit = 1; bit < shifted.Length; bit++)
            {
                shifted[bit] = remainder[bit - 1];
            }

            var difference = NandLogic.SubtractWord(
                builder,
                shifted,
                extendedDivisor,
                zero,
                $"{prefix}.stage[{stage}].subtract",
                $"{region}/stage:{stage}/subtract");
            var noBorrow = NandLogic.Not(
                builder,
                difference.Status,
                $"{prefix}.stage[{stage}].no_borrow",
                $"{region}/stage:{stage}/control");
            var take = NandLogic.And(
                builder,
                divisorNonzero,
                noBorrow,
                $"{prefix}.stage[{stage}].take",
                $"{region}/stage:{stage}/control");
            for (var bit = 0; bit < shifted.Length; bit++)
            {
                remainder[bit] = NandLogic.Mux(
                    builder,
                    take,
                    difference.Value[bit],
                    shifted[bit],
                    $"{prefix}.stage[{stage}].restore[{bit}]",
                    $"{region}/stage:{stage}/restore");
            }

            quotient[sourceBit] = take;
        }

        var remainderAny = OrMany(
            builder,
            remainder,
            zero,
            $"{prefix}.remainder_any",
            $"{region}/status");
        var remainderZero = NandLogic.Not(
            builder,
            remainderAny,
            $"{prefix}.remainder_zero",
            $"{region}/status");
        var exact = NandLogic.And(
            builder,
            divisorNonzero,
            remainderZero,
            $"{prefix}.exact",
            $"{region}/status");
        return new DivisionSignals(quotient, exact);
    }

    private static NandSignal SelectOperationValue(
        NandNetlistBuilder builder,
        IReadOnlyDictionary<int, NandSignal> opcodeDecode,
        NandSignal fallback,
        NandSignal load,
        NandSignal scale,
        NandSignal cancel,
        NandSignal add,
        string prefix,
        string region)
    {
        var result = fallback;
        var candidates = new Dictionary<BinaryMagnitudeDatapathOperation, NandSignal>
        {
            [BinaryMagnitudeDatapathOperation.Load] = load,
            [BinaryMagnitudeDatapathOperation.Scale] = scale,
            [BinaryMagnitudeDatapathOperation.Cancel] = cancel,
            [BinaryMagnitudeDatapathOperation.AddMagnitude] = add,
        };
        foreach (var pair in candidates)
        {
            result = NandLogic.Mux(
                builder,
                opcodeDecode[(int)pair.Key],
                pair.Value,
                result,
                $"{prefix}.operation[{(int)pair.Key}]",
                region);
        }

        return result;
    }

    private static NandSignal IsZero(
        NandNetlistBuilder builder,
        IEnumerable<NandSignal> signals,
        NandSignal zero,
        string prefix,
        string region)
    {
        var any = OrMany(builder, signals, zero, $"{prefix}.any", region);
        return NandLogic.Not(builder, any, $"{prefix}.not_any", region);
    }

    private static NandSignal AndMany(
        NandNetlistBuilder builder,
        IEnumerable<NandSignal> signals,
        NandSignal identity,
        string prefix,
        string region) => ReduceMany(builder, signals, identity, prefix, region, useAnd: true);

    private static NandSignal OrMany(
        NandNetlistBuilder builder,
        IEnumerable<NandSignal> signals,
        NandSignal identity,
        string prefix,
        string region) => ReduceMany(builder, signals, identity, prefix, region, useAnd: false);

    private static NandSignal ReduceMany(
        NandNetlistBuilder builder,
        IEnumerable<NandSignal> signals,
        NandSignal identity,
        string prefix,
        string region,
        bool useAnd)
    {
        var current = signals.ToList();
        if (current.Count == 0)
        {
            return identity;
        }

        var level = 0;
        while (current.Count > 1)
        {
            var next = new List<NandSignal>((current.Count + 1) / 2);
            for (var index = 0; index < current.Count; index += 2)
            {
                if (index + 1 == current.Count)
                {
                    next.Add(current[index]);
                    continue;
                }

                next.Add(useAnd
                    ? NandLogic.And(
                        builder,
                        current[index],
                        current[index + 1],
                        $"{prefix}.level[{level}].pair[{index / 2}]",
                        region)
                    : NandLogic.Or(
                        builder,
                        current[index],
                        current[index + 1],
                        $"{prefix}.level[{level}].pair[{index / 2}]",
                        region));
            }

            current = next;
            level++;
        }

        return current[0];
    }

    private static NandSignal[] ConstantWord(
        int value,
        int width,
        NandSignal zero,
        NandSignal one) => Enumerable.Range(0, width)
        .Select(bit => (value & (1 << bit)) != 0 ? one : zero)
        .ToArray();

    private static NandSignal[] Inputs(
        NandNetlistBuilder builder,
        IReadOnlyList<string> names,
        string region) => names.Select(name => builder.Input(name, region)).ToArray();

    private static NandSignal[] States(
        NandNetlistBuilder builder,
        IReadOnlyList<string> names,
        string region) => names.Select(name => builder.State(name, BitState.Off, region)).ToArray();

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
            values[names[bit]] = (value & (1 << bit)) != 0 ? BitState.On : BitState.Off;
        }
    }

    private readonly record struct DivisionSignals(NandSignal[] Quotient, NandSignal Exact);
}
