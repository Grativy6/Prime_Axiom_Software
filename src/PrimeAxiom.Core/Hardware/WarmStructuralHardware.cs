using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hardware;

public enum WarmStructuralOperation : byte
{
    Scale = 0,
    Cancel = 1,
}

public sealed record WarmStructuralLaneLayout(
    int Lane,
    int Prime,
    int Cap,
    int PayloadWidth,
    IReadOnlyList<string> StatePayloadNames,
    string StateSaturationName,
    IReadOnlyList<string> PayloadOutputNames,
    string SaturationOutputName);

public sealed record WarmStructuralPortLayout(
    int Width,
    string OperationInput,
    IReadOnlyList<string> PrimeSelectInputs,
    string StateZeroName,
    string ZeroOutput,
    string SaturationValidOutput,
    string CanonicalValidOutput,
    string RejectOutput,
    string AcceptedOutput,
    string OverflowOutput,
    string UnderflowOutput,
    IReadOnlyList<WarmStructuralLaneLayout> Lanes);

public sealed record DeclaredWarmStructuralMachine(
    NandNetlist Netlist,
    WarmStructuralPortLayout Ports,
    string EvidenceClass = "STRUCTURAL_DECLARED_INTEGRATED");

public sealed record WarmBinaryPortLayout(
    int Width,
    string OperationInput,
    IReadOnlyList<string> PrimeSelectInputs,
    IReadOnlyList<string> StateMagnitudeNames,
    IReadOnlyList<string> MagnitudeOutputNames,
    string ZeroOutput,
    string RejectOutput,
    string AcceptedOutput,
    string OverflowOutput,
    string NotDivisibleOutput,
    string DivisionExactOutput);

public sealed record DeclaredWarmBinaryMachine(
    NandNetlist Netlist,
    WarmBinaryPortLayout Ports,
    string EvidenceClass = "STRUCTURAL_DECLARED_INTEGRATED");

/// <summary>
/// Persistent exact S4 state for the frozen warm SCALE/CANCEL workload. The
/// graph includes state DFFs, cap/underflow control, malformed-state rejection,
/// and atomic hold muxes. Only NAND2 is used for combinational logic.
/// </summary>
public static class WarmStructuralHardware
{
    public static DeclaredWarmStructuralMachine BuildScaleCancelMachine(int width)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        var ports = CreateLayout(domain);
        var builder = new NandNetlistBuilder($"VFU.BINEXP.S4.WARM_SCALE_CANCEL.W{width}");
        var cancel = builder.Input(ports.OperationInput, "ports/input/control/operation");
        var primeSelect = ports.PrimeSelectInputs
            .Select(name => builder.Input(name, "ports/input/control/prime_select"))
            .ToArray();
        var zero = builder.Constant("const.zero", BitState.Off, "configuration/constants");
        var one = builder.Constant("const.one", BitState.On, "configuration/constants");
        var zeroState = builder.State(ports.StateZeroName, BitState.Off, "state/value/zero");
        var payload = new NandSignal[domain.LaneCount][];
        var saturated = new NandSignal[domain.LaneCount];
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            var layout = ports.Lanes[lane];
            payload[lane] = layout.StatePayloadNames
                .Select((name, bit) => builder.State(
                    name,
                    BitState.Off,
                    $"state/value/lane:{lane}/payload/bit:{bit}"))
                .ToArray();
            saturated[lane] = builder.State(
                layout.StateSaturationName,
                BitState.Off,
                $"state/value/lane:{lane}/saturation");
        }

        var notSelect0 = NandLogic.Not(
            builder,
            primeSelect[0],
            "control.select.not[0]",
            "control/select");
        var notSelect1 = NandLogic.Not(
            builder,
            primeSelect[1],
            "control.select.not[1]",
            "control/select");
        var selectors = new[]
        {
            NandLogic.And(builder, notSelect0, notSelect1, "control.select.lane[0]", "control/select"),
            NandLogic.And(builder, primeSelect[0], notSelect1, "control.select.lane[1]", "control/select"),
            NandLogic.And(builder, notSelect0, primeSelect[1], "control.select.lane[2]", "control/select"),
            NandLogic.And(builder, primeSelect[0], primeSelect[1], "control.select.lane[3]", "control/select"),
        };

        var laneAtCap = new NandSignal[domain.LaneCount];
        var laneAtZero = new NandSignal[domain.LaneCount];
        var laneWithinCap = new NandSignal[domain.LaneCount];
        var incremented = new NandSignal[domain.LaneCount][];
        var decremented = new NandSignal[domain.LaneCount][];
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            var region = $"datapath/lane:{lane}";
            var layout = ports.Lanes[lane];
            var cap = ConstantWord(layout.Cap, layout.PayloadWidth, zero, one);
            var comparison = NandLogic.CompareWord(
                builder,
                payload[lane],
                cap,
                $"lane[{lane}].cap_compare",
                $"{region}/validation");
            laneAtCap[lane] = comparison.Equal;
            laneWithinCap[lane] = NandLogic.Not(
                builder,
                comparison.Greater,
                $"lane[{lane}].within_cap",
                $"{region}/validation");
            laneAtZero[lane] = IsZero(
                builder,
                payload[lane],
                zero,
                $"lane[{lane}].zero",
                $"{region}/validation");

            var unit = ConstantWord(1, layout.PayloadWidth, zero, one);
            incremented[lane] = NandLogic.AddWord(
                builder,
                payload[lane],
                unit,
                zero,
                $"lane[{lane}].increment",
                $"{region}/increment").Value;
            decremented[lane] = NandLogic.SubtractWord(
                builder,
                payload[lane],
                unit,
                zero,
                $"lane[{lane}].decrement",
                $"{region}/decrement").Value;
        }

        var anySaturated = OrMany(
            builder,
            saturated,
            zero,
            "validation.any_saturated",
            "control/validation");
        var saturationValid = NandLogic.Not(
            builder,
            anySaturated,
            "validation.saturation_valid",
            "control/validation");
        var capsValid = AndMany(
            builder,
            laneWithinCap,
            one,
            "validation.caps",
            "control/validation");
        var payloadAny = OrMany(
            builder,
            payload.SelectMany(lane => lane).ToArray(),
            zero,
            "validation.payload_any",
            "control/validation");
        var payloadZero = NandLogic.Not(
            builder,
            payloadAny,
            "validation.payload_zero",
            "control/validation");
        var notZeroState = NandLogic.Not(
            builder,
            zeroState,
            "validation.not_zero_state",
            "control/validation");
        var zeroConsistent = NandLogic.Or(
            builder,
            notZeroState,
            payloadZero,
            "validation.zero_consistent",
            "control/validation");
        var canonicalValid = AndMany(
            builder,
            [saturationValid, capsValid, zeroConsistent],
            one,
            "validation.canonical",
            "control/validation");
        var invalid = NandLogic.Not(
            builder,
            canonicalValid,
            "validation.invalid",
            "control/validation");

        var selectedAtCap = SelectAndReduce(
            builder,
            selectors,
            laneAtCap,
            zero,
            "control.selected_at_cap");
        var selectedAtZero = SelectAndReduce(
            builder,
            selectors,
            laneAtZero,
            zero,
            "control.selected_at_zero");
        var scale = NandLogic.Not(builder, cancel, "control.scale", "control/status");
        var rawOverflow = NandLogic.And(
            builder,
            scale,
            selectedAtCap,
            "control.overflow.raw",
            "control/status");
        var overflow = NandLogic.And(
            builder,
            canonicalValid,
            rawOverflow,
            "control.overflow",
            "control/status");
        var cancelAtZeroExponent = NandLogic.And(
            builder,
            cancel,
            selectedAtZero,
            "control.underflow.selected_zero",
            "control/status");
        var nonzeroUnderflow = NandLogic.And(
            builder,
            notZeroState,
            cancelAtZeroExponent,
            "control.underflow.nonzero",
            "control/status");
        var underflow = NandLogic.And(
            builder,
            canonicalValid,
            nonzeroUnderflow,
            "control.underflow",
            "control/status");
        var reject = OrMany(
            builder,
            [invalid, overflow, underflow],
            zero,
            "control.reject",
            "control/status");
        var accepted = NandLogic.Not(builder, reject, "control.accepted", "control/status");
        var activeUpdate = NandLogic.And(
            builder,
            accepted,
            notZeroState,
            "control.active_update",
            "control/status");

        builder.Dff("zero_reg", zeroState, zeroState, "state/value/zero");
        builder.Output(ports.ZeroOutput, zeroState, "ports/output/state");
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            var layout = ports.Lanes[lane];
            var region = $"datapath/lane:{lane}";
            for (var bit = 0; bit < payload[lane].Length; bit++)
            {
                var operationValue = NandLogic.Mux(
                    builder,
                    cancel,
                    decremented[lane][bit],
                    incremented[lane][bit],
                    $"lane[{lane}].operation.bit[{bit}]",
                    $"{region}/select");
                var selectedValue = NandLogic.Mux(
                    builder,
                    selectors[lane],
                    operationValue,
                    payload[lane][bit],
                    $"lane[{lane}].selected.bit[{bit}]",
                    $"{region}/select");
                var next = NandLogic.Mux(
                    builder,
                    activeUpdate,
                    selectedValue,
                    payload[lane][bit],
                    $"lane[{lane}].atomic_hold.bit[{bit}]",
                    $"{region}/atomic_hold");
                builder.Dff(
                    $"lane[{lane}].payload_reg[{bit}]",
                    next,
                    payload[lane][bit],
                    $"state/value/lane:{lane}/payload");
                builder.Output(
                    layout.PayloadOutputNames[bit],
                    payload[lane][bit],
                    $"ports/output/state/lane:{lane}");
            }

            builder.Dff(
                $"lane[{lane}].saturation_reg",
                saturated[lane],
                saturated[lane],
                $"state/value/lane:{lane}/saturation");
            builder.Output(
                layout.SaturationOutputName,
                saturated[lane],
                $"ports/output/state/lane:{lane}");
        }

        builder.Output(ports.SaturationValidOutput, saturationValid, "ports/output/status");
        builder.Output(ports.CanonicalValidOutput, canonicalValid, "ports/output/status");
        builder.Output(ports.RejectOutput, reject, "ports/output/status");
        builder.Output(ports.AcceptedOutput, accepted, "ports/output/status");
        builder.Output(ports.OverflowOutput, overflow, "ports/output/status");
        builder.Output(ports.UnderflowOutput, underflow, "ports/output/status");
        return new DeclaredWarmStructuralMachine(builder.Build(), ports);
    }

    /// <summary>
    /// Matched conventional warm machine with the same operation/select inputs
    /// and an authoritative W-bit magnitude register. Scale uses an explicit
    /// shift-add multiplier; cancel uses an explicit restoring divider.
    /// </summary>
    public static DeclaredWarmBinaryMachine BuildBinaryScaleCancelMachine(int width)
    {
        _ = ValuationHardwareDomain.ForWidth(width);
        var ports = CreateBinaryLayout(width);
        var builder = new NandNetlistBuilder($"BIN.WARM_SCALE_CANCEL.W{width}");
        var cancel = builder.Input(ports.OperationInput, "ports/input/control/operation");
        var primeSelect = ports.PrimeSelectInputs
            .Select(name => builder.Input(name, "ports/input/control/prime_select"))
            .ToArray();
        var zero = builder.Constant("const.zero", BitState.Off, "configuration/constants");
        var one = builder.Constant("const.one", BitState.On, "configuration/constants");
        var magnitude = ports.StateMagnitudeNames
            .Select((name, bit) => builder.State(
                name,
                bit == 0 ? BitState.On : BitState.Off,
                $"state/magnitude/bit:{bit}"))
            .ToArray();
        var factor = SelectFactorWord(builder, primeSelect, width, zero, one);
        var product = MultiplyWords(
            builder,
            magnitude,
            factor,
            zero,
            "binary.scale",
            "datapath/binary/scale");
        var overflowAny = OrMany(
            builder,
            product.Skip(width).ToArray(),
            zero,
            "binary.scale.overflow_any",
            "control/status");
        var scale = NandLogic.Not(builder, cancel, "binary.scale_select", "control/status");
        var overflow = NandLogic.And(
            builder,
            scale,
            overflowAny,
            "binary.overflow",
            "control/status");

        var division = DivideWords(
            builder,
            magnitude,
            factor,
            zero,
            "binary.cancel",
            "datapath/binary/cancel");
        var notExact = NandLogic.Not(
            builder,
            division.Exact,
            "binary.cancel.not_exact",
            "control/status");
        var notDivisible = NandLogic.And(
            builder,
            cancel,
            notExact,
            "binary.not_divisible",
            "control/status");
        var reject = NandLogic.Or(
            builder,
            overflow,
            notDivisible,
            "binary.reject",
            "control/status");
        var accepted = NandLogic.Not(builder, reject, "binary.accepted", "control/status");
        var operationResult = new NandSignal[width];
        for (var bit = 0; bit < width; bit++)
        {
            operationResult[bit] = NandLogic.Mux(
                builder,
                cancel,
                division.Quotient[bit],
                product[bit],
                $"binary.operation.bit[{bit}]",
                $"datapath/binary/select/bit:{bit}");
            var next = NandLogic.Mux(
                builder,
                accepted,
                operationResult[bit],
                magnitude[bit],
                $"binary.atomic_hold.bit[{bit}]",
                $"datapath/binary/atomic_hold/bit:{bit}");
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
            "binary.magnitude_zero",
            "control/status");
        builder.Output(ports.ZeroOutput, magnitudeZero, "ports/output/status");
        builder.Output(ports.RejectOutput, reject, "ports/output/status");
        builder.Output(ports.AcceptedOutput, accepted, "ports/output/status");
        builder.Output(ports.OverflowOutput, overflow, "ports/output/status");
        builder.Output(ports.NotDivisibleOutput, notDivisible, "ports/output/status");
        builder.Output(ports.DivisionExactOutput, division.Exact, "ports/output/status");
        return new DeclaredWarmBinaryMachine(builder.Build(), ports);
    }

    public static Dictionary<string, BitState> EncodeControl(
        DeclaredWarmStructuralMachine machine,
        int prime,
        WarmStructuralOperation operation)
    {
        ArgumentNullException.ThrowIfNull(machine);
        return EncodeControl(
            machine.Ports.OperationInput,
            machine.Ports.PrimeSelectInputs,
            machine.Ports.Lanes.Select(lane => lane.Prime).ToArray(),
            prime,
            operation);
    }

    public static Dictionary<string, BitState> EncodeControl(
        DeclaredWarmBinaryMachine machine,
        int prime,
        WarmStructuralOperation operation)
    {
        ArgumentNullException.ThrowIfNull(machine);
        return EncodeControl(
            machine.Ports.OperationInput,
            machine.Ports.PrimeSelectInputs,
            ValuationHardwareDomain.S4,
            prime,
            operation);
    }

    public static Dictionary<string, BitState> EncodeExactState(
        DeclaredWarmStructuralMachine machine,
        ValuationHardwareState state)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(state);
        if (state.Width != machine.Ports.Width)
        {
            throw new ArgumentException("Warm-machine state width does not match the circuit.", nameof(state));
        }

        if (!state.IsExact || !state.IsCanonical)
        {
            throw new ArgumentException("Warm-machine ingress requires an exact canonical state.", nameof(state));
        }

        var encoded = new Dictionary<string, BitState>(StringComparer.Ordinal)
        {
            [machine.Ports.StateZeroName] = State(state.IsZero),
        };
        for (var lane = 0; lane < machine.Ports.Lanes.Count; lane++)
        {
            var layout = machine.Ports.Lanes[lane];
            for (var bit = 0; bit < layout.StatePayloadNames.Count; bit++)
            {
                encoded[layout.StatePayloadNames[bit]] =
                    State((state.ExponentAt(lane) & (1 << bit)) != 0);
            }

            encoded[layout.StateSaturationName] = BitState.Off;
        }

        return encoded;
    }

    public static Dictionary<string, BitState> EncodeMagnitudeState(
        DeclaredWarmBinaryMachine machine,
        int magnitude)
    {
        ArgumentNullException.ThrowIfNull(machine);
        var maximum = (1 << machine.Ports.Width) - 1;
        if (magnitude < 0 || magnitude > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(magnitude));
        }

        return machine.Ports.StateMagnitudeNames
            .Select((name, bit) => new KeyValuePair<string, BitState>(
                name,
                State((magnitude & (1 << bit)) != 0)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    public static int DecodeNextMagnitude(
        DeclaredWarmBinaryMachine machine,
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

    public static ValuationStateResult<ValuationHardwareState> DecodeNextExactState(
        DeclaredWarmStructuralMachine machine,
        NandEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(evaluation);
        if (machine.Ports.Lanes.Any(lane =>
                evaluation.DffNextStates[lane.StateSaturationName] == BitState.On))
        {
            return ValuationStateResult<ValuationHardwareState>.Reject(
                ValuationStateFailure.SaturatedInput,
                "The next warm-machine state contains a saturation flag.");
        }

        var exponents = machine.Ports.Lanes
            .Select(lane => ReadUnsigned(evaluation.DffNextStates, lane.StatePayloadNames))
            .ToArray();
        return ValuationHardwareState.Create(
            machine.Ports.Width,
            evaluation.DffNextStates[machine.Ports.StateZeroName] == BitState.On,
            exponents);
    }

    private static Dictionary<string, BitState> EncodeControl(
        string operationInput,
        IReadOnlyList<string> primeSelectInputs,
        IReadOnlyList<int> primes,
        int prime,
        WarmStructuralOperation operation)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        var lane = primes.ToList().IndexOf(prime);
        if (lane < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prime));
        }

        return new Dictionary<string, BitState>(StringComparer.Ordinal)
        {
            [operationInput] = State(operation == WarmStructuralOperation.Cancel),
            [primeSelectInputs[0]] = State((lane & 1) != 0),
            [primeSelectInputs[1]] = State((lane & 2) != 0),
        };
    }

    private static WarmBinaryPortLayout CreateBinaryLayout(int width) =>
        new(
            width,
            "cancel",
            ["prime_select[0]", "prime_select[1]"],
            Enumerable.Range(0, width).Select(bit => $"magnitude_q[{bit}]").ToArray(),
            Enumerable.Range(0, width).Select(bit => $"magnitude[{bit}]").ToArray(),
            "zero",
            "reject",
            "accepted",
            "overflow",
            "not_divisible",
            "division_exact");

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
                $"binary.factor.low.bit[{bit}]",
                $"control/factor/bit:{bit}");
            var high = NandLogic.Mux(
                builder,
                select[0],
                factors[3][bit],
                factors[2][bit],
                $"binary.factor.high.bit[{bit}]",
                $"control/factor/bit:{bit}");
            selected[bit] = NandLogic.Mux(
                builder,
                select[1],
                high,
                low,
                $"binary.factor.final.bit[{bit}]",
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
        var outputWidth = checked(left.Count * 2);
        var accumulator = Enumerable.Repeat(zero, outputWidth).ToArray();
        for (var multiplierBit = 0; multiplierBit < right.Count; multiplierBit++)
        {
            var partial = Enumerable.Repeat(zero, outputWidth).ToArray();
            for (var multiplicandBit = 0; multiplicandBit < left.Count; multiplicandBit++)
            {
                var destination = multiplierBit + multiplicandBit;
                partial[destination] = NandLogic.And(
                    builder,
                    left[multiplicandBit],
                    right[multiplierBit],
                    $"{prefix}.partial[{multiplierBit},{multiplicandBit}]",
                    $"{region}/partial/row:{multiplierBit}/bit:{destination}");
            }

            accumulator = NandLogic.AddWord(
                builder,
                accumulator,
                partial,
                zero,
                $"{prefix}.accumulate[{multiplierBit}]",
                $"{region}/accumulate/row:{multiplierBit}").Value;
        }

        return accumulator;
    }

    private static DivideSignals DivideWords(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> dividend,
        IReadOnlyList<NandSignal> divisor,
        NandSignal zero,
        string prefix,
        string region)
    {
        var width = dividend.Count;
        var extendedDivisor = new NandSignal[width + 1];
        for (var bit = 0; bit < width; bit++)
        {
            extendedDivisor[bit] = divisor[bit];
        }

        extendedDivisor[width] = zero;
        var remainder = Enumerable.Repeat(zero, width + 1).ToArray();
        var quotient = new NandSignal[width];
        for (var stage = 0; stage < width; stage++)
        {
            var dividendBit = width - stage - 1;
            var shifted = new NandSignal[width + 1];
            shifted[0] = dividend[dividendBit];
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
            var take = NandLogic.Not(
                builder,
                difference.Status,
                $"{prefix}.stage[{stage}].take",
                $"{region}/stage:{stage}/control");
            remainder = MuxWord(
                builder,
                take,
                difference.Value,
                shifted,
                $"{prefix}.stage[{stage}].restore",
                $"{region}/stage:{stage}/restore");
            quotient[dividendBit] = take;
        }

        var exact = IsZero(
            builder,
            remainder,
            zero,
            $"{prefix}.remainder_zero",
            $"{region}/status");
        return new DivideSignals(quotient, remainder.Take(width).ToArray(), exact);
    }

    private static NandSignal[] MuxWord(
        NandNetlistBuilder builder,
        NandSignal select,
        IReadOnlyList<NandSignal> whenOn,
        IReadOnlyList<NandSignal> whenOff,
        string prefix,
        string region)
    {
        var result = new NandSignal[whenOn.Count];
        for (var bit = 0; bit < result.Length; bit++)
        {
            result[bit] = NandLogic.Mux(
                builder,
                select,
                whenOn[bit],
                whenOff[bit],
                $"{prefix}.bit[{bit}]",
                $"{region}/bit:{bit}");
        }

        return result;
    }

    private static WarmStructuralPortLayout CreateLayout(ValuationHardwareDomain domain)
    {
        var lanes = new WarmStructuralLaneLayout[domain.LaneCount];
        for (var lane = 0; lane < lanes.Length; lane++)
        {
            var payloadWidth = BitsRequired(domain.CapAt(lane));
            lanes[lane] = new WarmStructuralLaneLayout(
                lane,
                domain.PrimeAt(lane),
                domain.CapAt(lane),
                payloadWidth,
                Enumerable.Range(0, payloadWidth).Select(bit => $"lane[{lane}].exponent_q[{bit}]").ToArray(),
                $"lane[{lane}].saturated_q",
                Enumerable.Range(0, payloadWidth).Select(bit => $"exponent[{lane}][{bit}]").ToArray(),
                $"saturated[{lane}]");
        }

        return new WarmStructuralPortLayout(
            domain.Width,
            "cancel",
            ["prime_select[0]", "prime_select[1]"],
            "zero_q",
            "zero",
            "saturation_valid",
            "canonical_valid",
            "reject",
            "accepted",
            "overflow",
            "underflow",
            lanes);
    }

    private static NandSignal SelectAndReduce(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> selectors,
        IReadOnlyList<NandSignal> values,
        NandSignal zero,
        string prefix)
    {
        var selected = new NandSignal[selectors.Count];
        for (var lane = 0; lane < selected.Length; lane++)
        {
            selected[lane] = NandLogic.And(
                builder,
                selectors[lane],
                values[lane],
                $"{prefix}.lane[{lane}]",
                $"control/select/lane:{lane}");
        }

        return OrMany(builder, selected, zero, $"{prefix}.any", "control/select");
    }

    private static NandSignal IsZero(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> signals,
        NandSignal zero,
        string prefix,
        string region)
    {
        var any = OrMany(builder, signals, zero, $"{prefix}.any", region);
        return NandLogic.Not(builder, any, $"{prefix}.not", region);
    }

    private static NandSignal OrMany(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> signals,
        NandSignal zero,
        string prefix,
        string region)
    {
        var result = zero;
        for (var index = 0; index < signals.Count; index++)
        {
            result = NandLogic.Or(
                builder,
                result,
                signals[index],
                $"{prefix}.or[{index}]",
                region);
        }

        return result;
    }

    private static NandSignal AndMany(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> signals,
        NandSignal one,
        string prefix,
        string region)
    {
        var result = one;
        for (var index = 0; index < signals.Count; index++)
        {
            result = NandLogic.And(
                builder,
                result,
                signals[index],
                $"{prefix}.and[{index}]",
                region);
        }

        return result;
    }

    private static NandSignal[] ConstantWord(
        int value,
        int width,
        NandSignal zero,
        NandSignal one) =>
        Enumerable.Range(0, width)
            .Select(bit => (value & (1 << bit)) == 0 ? zero : one)
            .ToArray();

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

    private static int BitsRequired(int maximum)
    {
        var bits = 0;
        do
        {
            bits++;
            maximum >>= 1;
        }
        while (maximum != 0);

        return bits;
    }

    private static BitState State(bool value) => value ? BitState.On : BitState.Off;

    private sealed record DivideSignals(
        NandSignal[] Quotient,
        NandSignal[] Remainder,
        NandSignal Exact);
}
