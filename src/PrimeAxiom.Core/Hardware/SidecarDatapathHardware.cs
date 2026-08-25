using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hardware;

public enum SidecarDatapathOperation : byte
{
    Load = 0,
    Refresh = 1,
    Query = 2,
    Scale = 3,
    Cancel = 4,
    AddMagnitude = 5,
}

public sealed record SidecarThresholdLaneLayout(
    int Lane,
    int Prime,
    int Cap,
    IReadOnlyList<string> StateThresholdNames,
    IReadOnlyList<string> ThresholdOutputNames);

public sealed record SidecarDatapathPortLayout(
    int Width,
    IReadOnlyList<string> OpcodeInputs,
    IReadOnlyList<string> PrimeSelectInputs,
    IReadOnlyList<string> ThresholdSelectInputs,
    IReadOnlyList<string> OperandInputs,
    IReadOnlyList<string> StateMagnitudeNames,
    string StateValidName,
    IReadOnlyList<string> MagnitudeOutputNames,
    string ValidOutput,
    string ZeroOutput,
    string StateWellFormedOutput,
    string RejectOutput,
    string AcceptedOutput,
    string OverflowOutput,
    string NotDivisibleOutput,
    string QueryPredicateOutput,
    string QueryKnownOutput,
    string QueryExactOutput,
    IReadOnlyList<SidecarThresholdLaneLayout> Lanes);

public sealed record DeclaredSidecarDatapath(
    NandNetlist Netlist,
    SidecarDatapathPortLayout Ports,
    string EvidenceClass = "STRUCTURAL_DECLARED_INTEGRATED");

public sealed record SidecarDatapathStateSnapshot(
    int Width,
    int Magnitude,
    bool Valid,
    IReadOnlyList<int> LowerBounds);

/// <summary>
/// Persistent authoritative binary magnitude with an S4 valuation-threshold
/// sidecar. All runtime decisions and state transitions are emitted NAND2
/// logic. The finite valuation truth tables are construction-time constants,
/// not host arithmetic performed during evaluation.
/// </summary>
public static class SidecarDatapathHardware
{
    private const int OpcodeWidth = 3;
    private const int ThresholdSelectWidth = 3;

    public static DeclaredSidecarDatapath Build(int width)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        var ports = CreateLayout(domain);
        var builder = new NandNetlistBuilder($"BIN.VSC.S4.DATAPATH.W{width}");
        var opcode = Inputs(builder, ports.OpcodeInputs, "ports/input/control/opcode");
        var primeSelect = Inputs(builder, ports.PrimeSelectInputs, "ports/input/control/prime");
        var thresholdSelect = Inputs(
            builder,
            ports.ThresholdSelectInputs,
            "ports/input/control/threshold");
        var operand = Inputs(builder, ports.OperandInputs, "ports/input/operand");
        var magnitude = States(builder, ports.StateMagnitudeNames, "state/magnitude");
        var valid = builder.State(ports.StateValidName, BitState.Off, "state/sidecar/valid");
        var thresholds = new NandSignal[domain.LaneCount][];
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            thresholds[lane] = States(
                builder,
                ports.Lanes[lane].StateThresholdNames,
                $"state/sidecar/lane:{lane}/thresholds");
        }

        var zero = builder.Constant("const.zero", BitState.Off, "configuration/constants");
        var one = builder.Constant("const.one", BitState.On, "configuration/constants");
        var opcodeDecode = DecodeWord(
            builder,
            opcode,
            Enum.GetValues<SidecarDatapathOperation>().Select(value => (int)value),
            "control.opcode",
            "control/decode/opcode");
        var laneDecode = DecodeWord(
            builder,
            primeSelect,
            Enumerable.Range(0, domain.LaneCount),
            "control.prime",
            "control/decode/prime");
        var thresholdDecode = DecodeWord(
            builder,
            thresholdSelect,
            Enumerable.Range(1, 7),
            "control.threshold",
            "control/decode/threshold");

        var magnitudeFacts = BuildValuationTruthTable(
            builder,
            magnitude,
            domain,
            "current",
            "valuation/current");
        var operandFacts = BuildValuationTruthTable(
            builder,
            operand,
            domain,
            "operand",
            "valuation/operand");

        var validationFacts = new List<NandSignal>();
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            for (var threshold = 0; threshold < domain.CapAt(lane); threshold++)
            {
                var bitPrefix = $"validation.lane[{lane}].threshold[{threshold + 1}]";
                var bitRegion = $"validation/lane:{lane}/threshold:{threshold + 1}";
                var notStored = NandLogic.Not(
                    builder,
                    thresholds[lane][threshold],
                    $"{bitPrefix}.not_stored",
                    bitRegion);
                validationFacts.Add(NandLogic.Or(
                    builder,
                    notStored,
                    magnitudeFacts.Thresholds[lane][threshold],
                    $"{bitPrefix}.truthful",
                    bitRegion));
                if (threshold > 0)
                {
                    validationFacts.Add(NandLogic.Or(
                        builder,
                        notStored,
                        thresholds[lane][threshold - 1],
                        $"{bitPrefix}.monotone",
                        bitRegion));
                }
            }
        }

        var exactMatches = new List<NandSignal>();
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            for (var threshold = 0; threshold < domain.CapAt(lane); threshold++)
            {
                exactMatches.Add(NandLogic.Xnor(
                    builder,
                    thresholds[lane][threshold],
                    magnitudeFacts.Thresholds[lane][threshold],
                    $"validation.exact.lane[{lane}].threshold[{threshold + 1}]",
                    $"validation/exact/lane:{lane}"));
            }
        }

        var exactMatch = AndMany(
            builder,
            exactMatches,
            one,
            "validation.exact_match",
            "validation/exact");
        var notValid = NandLogic.Not(builder, valid, "validation.not_valid", "validation/validity");
        validationFacts.Add(NandLogic.Or(
            builder,
            notValid,
            exactMatch,
            "validation.valid_implies_exact",
            "validation/validity"));
        var notMagnitudeZero = NandLogic.Not(
            builder,
            magnitudeFacts.Zero,
            "validation.not_zero",
            "validation/zero");
        validationFacts.Add(NandLogic.Or(
            builder,
            notMagnitudeZero,
            valid,
            "validation.zero_implies_valid",
            "validation/zero"));
        var stateWellFormed = AndMany(
            builder,
            validationFacts,
            one,
            "validation.state_well_formed",
            "validation/result");

        var querySelection = SelectThreshold(
            builder,
            laneDecode,
            thresholdDecode,
            thresholds,
            domain,
            zero);
        var validOrKnownTrue = NandLogic.Or(
            builder,
            valid,
            querySelection.Value,
            "query.valid_or_known_true",
            "query/status");
        var queryPrecondition = AndMany(
            builder,
            [stateWellFormed, querySelection.Supported, validOrKnownTrue],
            one,
            "query.precondition",
            "query/status");
        var queryAccepted = NandLogic.And(
            builder,
            opcodeDecode[(int)SidecarDatapathOperation.Query],
            queryPrecondition,
            "query.accepted",
            "query/status");
        var queryPredicate = NandLogic.And(
            builder,
            queryAccepted,
            querySelection.Value,
            "query.predicate",
            "query/status");
        var queryExact = NandLogic.And(
            builder,
            queryAccepted,
            valid,
            "query.exact",
            "query/status");

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
            product.Skip(width).ToArray(),
            zero,
            "scale.overflow",
            "datapath/scale/status");
        var notScaleOverflow = NandLogic.Not(
            builder,
            scaleOverflow,
            "scale.not_overflow",
            "datapath/scale/status");
        var scalePrecondition = AndMany(
            builder,
            [stateWellFormed, notScaleOverflow],
            one,
            "scale.precondition",
            "datapath/scale/status");
        var scaleAccepted = NandLogic.And(
            builder,
            opcodeDecode[(int)SidecarDatapathOperation.Scale],
            scalePrecondition,
            "scale.accepted",
            "datapath/scale/status");

        var division = DivideWords(
            builder,
            magnitude,
            factor,
            zero,
            "cancel.divide",
            "datapath/cancel/divide");
        var cancelPrecondition = AndMany(
            builder,
            [stateWellFormed, division.Exact],
            one,
            "cancel.precondition",
            "datapath/cancel/status");
        var cancelAccepted = NandLogic.And(
            builder,
            opcodeDecode[(int)SidecarDatapathOperation.Cancel],
            cancelPrecondition,
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
        var addPrecondition = AndMany(
            builder,
            [stateWellFormed, notAddOverflow],
            one,
            "add.precondition",
            "datapath/add/status");
        var addAccepted = NandLogic.And(
            builder,
            opcodeDecode[(int)SidecarDatapathOperation.AddMagnitude],
            addPrecondition,
            "add.accepted",
            "datapath/add/status");

        var laneValuationsUnequal = new NandSignal[domain.LaneCount];
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            var equalBits = new NandSignal[domain.CapAt(lane)];
            for (var threshold = 0; threshold < equalBits.Length; threshold++)
            {
                equalBits[threshold] = NandLogic.Xnor(
                    builder,
                    thresholds[lane][threshold],
                    operandFacts.Thresholds[lane][threshold],
                    $"add.valuation_equal.lane[{lane}].threshold[{threshold + 1}]",
                    $"datapath/add/exactness/lane:{lane}");
            }

            var laneEqual = AndMany(
                builder,
                equalBits,
                one,
                $"add.valuation_equal.lane[{lane}]",
                $"datapath/add/exactness/lane:{lane}");
            laneValuationsUnequal[lane] = NandLogic.Not(
                builder,
                laneEqual,
                $"add.valuation_unequal.lane[{lane}]",
                $"datapath/add/exactness/lane:{lane}");
        }

        var allLanesUnequal = AndMany(
            builder,
            laneValuationsUnequal,
            one,
            "add.all_lanes_unequal",
            "datapath/add/exactness");
        var nonzeroAddExact = AndMany(
            builder,
            [valid, allLanesUnequal],
            one,
            "add.nonzero_exact",
            "datapath/add/exactness");
        var operandZeroChoice = NandLogic.Mux(
            builder,
            operandFacts.Zero,
            valid,
            nonzeroAddExact,
            "add.valid_if_operand_zero",
            "datapath/add/exactness");
        var addResultValid = NandLogic.Mux(
            builder,
            magnitudeFacts.Zero,
            one,
            operandZeroChoice,
            "add.valid_if_current_zero",
            "datapath/add/exactness");

        var loadAccepted = opcodeDecode[(int)SidecarDatapathOperation.Load];
        var refreshAccepted = opcodeDecode[(int)SidecarDatapathOperation.Refresh];
        var accepted = OrMany(
            builder,
            [loadAccepted, refreshAccepted, queryAccepted, scaleAccepted, cancelAccepted, addAccepted],
            zero,
            "status.accepted",
            "control/status");
        var reject = NandLogic.Not(builder, accepted, "status.reject", "control/status");
        var scaleOverflowStatus = NandLogic.And(
            builder,
            opcodeDecode[(int)SidecarDatapathOperation.Scale],
            scaleOverflow,
            "status.scale_overflow",
            "control/status");
        var addOverflowStatus = NandLogic.And(
            builder,
            opcodeDecode[(int)SidecarDatapathOperation.AddMagnitude],
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
            opcodeDecode[(int)SidecarDatapathOperation.Cancel],
            notDivisionExact,
            "status.not_divisible",
            "control/status");

        var candidateMagnitude = new NandSignal[width];
        for (var bit = 0; bit < width; bit++)
        {
            candidateMagnitude[bit] = SelectOperationValue(
                builder,
                opcodeDecode,
                magnitude[bit],
                operand[bit],
                magnitude[bit],
                magnitude[bit],
                product[bit],
                division.Quotient[bit],
                addition.Value[bit],
                $"next.magnitude.bit[{bit}]",
                $"datapath/next/magnitude/bit:{bit}");
            var next = NandLogic.Mux(
                builder,
                accepted,
                candidateMagnitude[bit],
                magnitude[bit],
                $"atomic_hold.magnitude.bit[{bit}]",
                $"datapath/atomic_hold/magnitude/bit:{bit}");
            builder.Dff($"magnitude_reg[{bit}]", next, magnitude[bit], "state/magnitude");
            builder.Output(
                ports.MagnitudeOutputNames[bit],
                magnitude[bit],
                $"ports/output/magnitude/bit:{bit}");
        }

        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            for (var threshold = 0; threshold < domain.CapAt(lane); threshold++)
            {
                var current = thresholds[lane][threshold];
                var scaleShifted = threshold == 0 ? one : thresholds[lane][threshold - 1];
                var scaleValue = NandLogic.Mux(
                    builder,
                    laneDecode[lane],
                    scaleShifted,
                    current,
                    $"scale.threshold.lane[{lane}].threshold[{threshold + 1}]",
                    $"datapath/scale/sidecar/lane:{lane}");
                var cancelShifted = threshold + 1 < domain.CapAt(lane)
                    ? thresholds[lane][threshold + 1]
                    : zero;
                cancelShifted = NandLogic.Mux(
                    builder,
                    magnitudeFacts.Zero,
                    one,
                    cancelShifted,
                    $"cancel.zero_threshold.lane[{lane}].threshold[{threshold + 1}]",
                    $"datapath/cancel/sidecar/lane:{lane}");
                var cancelValue = NandLogic.Mux(
                    builder,
                    laneDecode[lane],
                    cancelShifted,
                    current,
                    $"cancel.threshold.lane[{lane}].threshold[{threshold + 1}]",
                    $"datapath/cancel/sidecar/lane:{lane}");
                var commonLowerBound = NandLogic.And(
                    builder,
                    current,
                    operandFacts.Thresholds[lane][threshold],
                    $"add.common_lower_bound.lane[{lane}].threshold[{threshold + 1}]",
                    $"datapath/add/sidecar/lane:{lane}");
                var operandZeroBound = NandLogic.Mux(
                    builder,
                    operandFacts.Zero,
                    current,
                    commonLowerBound,
                    $"add.operand_zero_bound.lane[{lane}].threshold[{threshold + 1}]",
                    $"datapath/add/sidecar/lane:{lane}");
                var addValue = NandLogic.Mux(
                    builder,
                    magnitudeFacts.Zero,
                    operandFacts.Thresholds[lane][threshold],
                    operandZeroBound,
                    $"add.current_zero_bound.lane[{lane}].threshold[{threshold + 1}]",
                    $"datapath/add/sidecar/lane:{lane}");
                var candidate = SelectOperationValue(
                    builder,
                    opcodeDecode,
                    current,
                    operandFacts.Thresholds[lane][threshold],
                    magnitudeFacts.Thresholds[lane][threshold],
                    current,
                    scaleValue,
                    cancelValue,
                    addValue,
                    $"next.threshold.lane[{lane}].threshold[{threshold + 1}]",
                    $"datapath/next/sidecar/lane:{lane}");
                var next = NandLogic.Mux(
                    builder,
                    accepted,
                    candidate,
                    current,
                    $"atomic_hold.threshold.lane[{lane}].threshold[{threshold + 1}]",
                    $"datapath/atomic_hold/sidecar/lane:{lane}");
                builder.Dff(
                    $"threshold_reg[{lane}][{threshold + 1}]",
                    next,
                    current,
                    $"state/sidecar/lane:{lane}/thresholds");
                builder.Output(
                    ports.Lanes[lane].ThresholdOutputNames[threshold],
                    current,
                    $"ports/output/sidecar/lane:{lane}/threshold:{threshold + 1}");
            }
        }

        var candidateValid = SelectOperationValue(
            builder,
            opcodeDecode,
            valid,
            one,
            one,
            valid,
            valid,
            valid,
            addResultValid,
            "next.valid",
            "datapath/next/valid");
        var nextValid = NandLogic.Mux(
            builder,
            accepted,
            candidateValid,
            valid,
            "atomic_hold.valid",
            "datapath/atomic_hold/valid");
        builder.Dff("valid_reg", nextValid, valid, "state/sidecar/valid");

        builder.Output(ports.ValidOutput, valid, "ports/output/status");
        builder.Output(ports.ZeroOutput, magnitudeFacts.Zero, "ports/output/status");
        builder.Output(ports.StateWellFormedOutput, stateWellFormed, "ports/output/status");
        builder.Output(ports.RejectOutput, reject, "ports/output/status");
        builder.Output(ports.AcceptedOutput, accepted, "ports/output/status");
        builder.Output(ports.OverflowOutput, overflow, "ports/output/status");
        builder.Output(ports.NotDivisibleOutput, notDivisible, "ports/output/status");
        builder.Output(ports.QueryPredicateOutput, queryPredicate, "ports/output/query");
        builder.Output(ports.QueryKnownOutput, queryAccepted, "ports/output/query");
        builder.Output(ports.QueryExactOutput, queryExact, "ports/output/query");
        return new DeclaredSidecarDatapath(builder.Build(), ports);
    }

    public static Dictionary<string, BitState> EncodeInputs(
        DeclaredSidecarDatapath machine,
        SidecarDatapathOperation operation,
        int prime = 2,
        int threshold = 1,
        int operand = 0)
    {
        ArgumentNullException.ThrowIfNull(machine);
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        var lane = machine.Ports.Lanes.ToList().FindIndex(item => item.Prime == prime);
        if (lane < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prime));
        }

        if (threshold < 0 || threshold >= (1 << ThresholdSelectWidth))
        {
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }

        var maximum = (1 << machine.Ports.Width) - 1;
        if (operand < 0 || operand > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(operand));
        }

        var encoded = new Dictionary<string, BitState>(StringComparer.Ordinal);
        WriteUnsigned(encoded, machine.Ports.OpcodeInputs, (int)operation);
        WriteUnsigned(encoded, machine.Ports.PrimeSelectInputs, lane);
        WriteUnsigned(encoded, machine.Ports.ThresholdSelectInputs, threshold);
        WriteUnsigned(encoded, machine.Ports.OperandInputs, operand);
        return encoded;
    }

    public static SidecarDatapathStateSnapshot CreateExactState(int width, int magnitude)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        if (magnitude < 0 || magnitude > domain.MaximumMagnitude)
        {
            throw new ArgumentOutOfRangeException(nameof(magnitude));
        }

        return new SidecarDatapathStateSnapshot(
            width,
            magnitude,
            true,
            ExactLowerBounds(domain, magnitude));
    }

    public static Dictionary<string, BitState> EncodeState(
        DeclaredSidecarDatapath machine,
        SidecarDatapathStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(state);
        ValidateLegalSnapshot(machine, state);
        return EncodeRawState(machine, state.Magnitude, state.Valid, state.LowerBounds);
    }

    public static Dictionary<string, BitState> EncodeRawState(
        DeclaredSidecarDatapath machine,
        int magnitude,
        bool valid,
        IReadOnlyList<int> lowerBounds)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(lowerBounds);
        var maximum = (1 << machine.Ports.Width) - 1;
        if (magnitude < 0 || magnitude > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(magnitude));
        }

        if (lowerBounds.Count != machine.Ports.Lanes.Count)
        {
            throw new ArgumentException("One lower bound is required for each S4 lane.", nameof(lowerBounds));
        }

        var encoded = new Dictionary<string, BitState>(StringComparer.Ordinal);
        WriteUnsigned(encoded, machine.Ports.StateMagnitudeNames, magnitude);
        encoded[machine.Ports.StateValidName] = State(valid);
        for (var lane = 0; lane < machine.Ports.Lanes.Count; lane++)
        {
            var layout = machine.Ports.Lanes[lane];
            if (lowerBounds[lane] < 0 || lowerBounds[lane] > layout.Cap)
            {
                throw new ArgumentOutOfRangeException(nameof(lowerBounds));
            }

            for (var threshold = 0; threshold < layout.Cap; threshold++)
            {
                encoded[layout.StateThresholdNames[threshold]] = State(threshold < lowerBounds[lane]);
            }
        }

        return encoded;
    }

    public static SidecarDatapathStateSnapshot DecodeNextState(
        DeclaredSidecarDatapath machine,
        NandEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(evaluation);
        return DecodeState(machine, evaluation.DffNextStates);
    }

    public static Dictionary<string, BitState> AdvanceState(NandEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        return evaluation.DffNextStates.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
    }

    private static SidecarDatapathStateSnapshot DecodeState(
        DeclaredSidecarDatapath machine,
        IReadOnlyDictionary<string, BitState> values)
    {
        var lowerBounds = machine.Ports.Lanes
            .Select(lane => lane.StateThresholdNames.Count(name => values[name] == BitState.On))
            .ToArray();
        return new SidecarDatapathStateSnapshot(
            machine.Ports.Width,
            ReadUnsigned(values, machine.Ports.StateMagnitudeNames),
            values[machine.Ports.StateValidName] == BitState.On,
            lowerBounds);
    }

    private static void ValidateLegalSnapshot(
        DeclaredSidecarDatapath machine,
        SidecarDatapathStateSnapshot state)
    {
        if (state.Width != machine.Ports.Width)
        {
            throw new ArgumentException("Sidecar state width does not match the circuit.", nameof(state));
        }

        var domain = ValuationHardwareDomain.ForWidth(state.Width);
        if (state.Magnitude < 0 || state.Magnitude > domain.MaximumMagnitude ||
            state.LowerBounds.Count != domain.LaneCount)
        {
            throw new ArgumentException("Sidecar state is outside the declared domain.", nameof(state));
        }

        var exact = ExactLowerBounds(domain, state.Magnitude);
        if (state.Magnitude == 0 && (!state.Valid || !state.LowerBounds.SequenceEqual(exact)))
        {
            throw new ArgumentException("Zero must be valid with every finite threshold asserted.", nameof(state));
        }

        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            if (state.LowerBounds[lane] < 0 || state.LowerBounds[lane] > exact[lane])
            {
                throw new ArgumentException("A lower bound contradicts the authoritative magnitude.", nameof(state));
            }
        }

        if (state.Valid && !state.LowerBounds.SequenceEqual(exact))
        {
            throw new ArgumentException("A valid sidecar must equal every exact S4 valuation.", nameof(state));
        }
    }

    private static SidecarDatapathPortLayout CreateLayout(ValuationHardwareDomain domain)
    {
        var lanes = Enumerable.Range(0, domain.LaneCount)
            .Select(lane => new SidecarThresholdLaneLayout(
                lane,
                domain.PrimeAt(lane),
                domain.CapAt(lane),
                Enumerable.Range(1, domain.CapAt(lane))
                    .Select(threshold => $"threshold_q[{lane}][{threshold}]")
                    .ToArray(),
                Enumerable.Range(1, domain.CapAt(lane))
                    .Select(threshold => $"threshold[{lane}][{threshold}]")
                    .ToArray()))
            .ToArray();
        return new SidecarDatapathPortLayout(
            domain.Width,
            Enumerable.Range(0, OpcodeWidth).Select(bit => $"opcode[{bit}]").ToArray(),
            ["prime_select[0]", "prime_select[1]"],
            Enumerable.Range(0, ThresholdSelectWidth).Select(bit => $"threshold_select[{bit}]").ToArray(),
            Enumerable.Range(0, domain.Width).Select(bit => $"operand[{bit}]").ToArray(),
            Enumerable.Range(0, domain.Width).Select(bit => $"magnitude_q[{bit}]").ToArray(),
            "valid_q",
            Enumerable.Range(0, domain.Width).Select(bit => $"magnitude[{bit}]").ToArray(),
            "valid",
            "zero",
            "state_well_formed",
            "reject",
            "accepted",
            "overflow",
            "not_divisible",
            "query_predicate",
            "query_known",
            "query_exact",
            lanes);
    }

    private static Dictionary<int, NandSignal> DecodeWord(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> word,
        IEnumerable<int> values,
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
                (value & (1 << index)) != 0 ? bit : inverted[index]).ToArray();
            decoded.Add(value, AndMany(
                builder,
                literals,
                default,
                $"{prefix}.value[{value}]",
                region));
        }

        return decoded;
    }

    private static ValuationTruthTable BuildValuationTruthTable(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> word,
        ValuationHardwareDomain domain,
        string prefix,
        string region)
    {
        var one = builder.Constant($"{prefix}.truth.const.one", BitState.On, $"{region}/constants");
        var zero = builder.Constant($"{prefix}.truth.const.zero", BitState.Off, $"{region}/constants");
        var inverted = word.Select((bit, index) => NandLogic.Not(
            builder,
            bit,
            $"{prefix}.truth.not[{index}]",
            $"{region}/decode")).ToArray();
        var count = 1 << word.Count;
        var equal = new NandSignal[count];
        for (var value = 0; value < count; value++)
        {
            var literals = word.Select((bit, index) =>
                (value & (1 << index)) != 0 ? bit : inverted[index]).ToArray();
            equal[value] = AndMany(
                builder,
                literals,
                one,
                $"{prefix}.truth.equal[{value}]",
                $"{region}/decode/value:{value}");
        }

        var thresholds = new NandSignal[domain.LaneCount][];
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            thresholds[lane] = new NandSignal[domain.CapAt(lane)];
            var factor = 1;
            for (var threshold = 0; threshold < domain.CapAt(lane); threshold++)
            {
                factor *= domain.PrimeAt(lane);
                var matching = Enumerable.Range(0, count)
                    .Where(value => value == 0 || value % factor == 0)
                    .Select(value => equal[value])
                    .ToArray();
                thresholds[lane][threshold] = OrMany(
                    builder,
                    matching,
                    zero,
                    $"{prefix}.truth.lane[{lane}].threshold[{threshold + 1}]",
                    $"{region}/lane:{lane}/threshold:{threshold + 1}");
            }
        }

        return new ValuationTruthTable(equal[0], thresholds);
    }

    private static ThresholdSelection SelectThreshold(
        NandNetlistBuilder builder,
        IReadOnlyDictionary<int, NandSignal> laneDecode,
        IReadOnlyDictionary<int, NandSignal> thresholdDecode,
        IReadOnlyList<NandSignal[]> thresholds,
        ValuationHardwareDomain domain,
        NandSignal zero)
    {
        var supportedTerms = new List<NandSignal>();
        var valueTerms = new List<NandSignal>();
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            for (var threshold = 1; threshold <= domain.CapAt(lane); threshold++)
            {
                var prefix = $"query.select.lane[{lane}].threshold[{threshold}]";
                var region = $"query/select/lane:{lane}/threshold:{threshold}";
                var supported = NandLogic.And(
                    builder,
                    laneDecode[lane],
                    thresholdDecode[threshold],
                    $"{prefix}.supported",
                    region);
                supportedTerms.Add(supported);
                valueTerms.Add(NandLogic.And(
                    builder,
                    supported,
                    thresholds[lane][threshold - 1],
                    $"{prefix}.value",
                    region));
            }
        }

        return new ThresholdSelection(
            OrMany(builder, supportedTerms, zero, "query.select.supported", "query/select"),
            OrMany(builder, valueTerms, zero, "query.select.value", "query/select"));
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
        NandSignal refresh,
        NandSignal query,
        NandSignal scale,
        NandSignal cancel,
        NandSignal add,
        string prefix,
        string region)
    {
        var result = fallback;
        var candidates = new[] { load, refresh, query, scale, cancel, add };
        for (var operation = 0; operation < candidates.Length; operation++)
        {
            result = NandLogic.Mux(
                builder,
                opcodeDecode[operation],
                candidates[operation],
                result,
                $"{prefix}.operation[{operation}]",
                region);
        }

        return result;
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

    private static int[] ExactLowerBounds(ValuationHardwareDomain domain, int magnitude)
    {
        if (magnitude == 0)
        {
            return domain.Caps.ToArray();
        }

        var result = new int[domain.LaneCount];
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            var remainder = magnitude;
            while (remainder % domain.PrimeAt(lane) == 0)
            {
                result[lane]++;
                remainder /= domain.PrimeAt(lane);
            }
        }

        return result;
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
            values[names[bit]] = State((value & (1 << bit)) != 0);
        }
    }

    private static BitState State(bool value) => value ? BitState.On : BitState.Off;

    private sealed record ValuationTruthTable(NandSignal Zero, NandSignal[][] Thresholds);

    private readonly record struct ThresholdSelection(NandSignal Supported, NandSignal Value);

    private readonly record struct DivisionSignals(NandSignal[] Quotient, NandSignal Exact);
}
