using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hardware;

public enum ExperimentalValuationEncoding
{
    BinaryExponent,
    Thermometer,
}

public enum ExperimentalValuationOperation
{
    Compose,
    Cancel,
    Meet,
    Join,
    Divides,
    CanonicalValidate,
    ThresholdQuery,
    FunctionalUnit,
}

public enum BinaryExponentFuOperation : byte
{
    Compose = 0,
    Cancel = 1,
    Meet = 2,
    Join = 3,
    Divides = 4,
}

/// <summary>
/// Stable port names and frozen S4 geometry for one valuation lane. Payload
/// names are LSB-first for binary exponents and increasing-threshold order for
/// thermometer vectors.
/// </summary>
public sealed record ExperimentalLanePortLayout(
    int Lane,
    int Prime,
    int Cap,
    int PayloadWidth,
    IReadOnlyList<string> LeftPayloadInputs,
    IReadOnlyList<string> RightPayloadInputs,
    IReadOnlyList<string> ResultPayloadOutputs,
    string LeftSaturationInput,
    string? RightSaturationInput,
    string? ResultSaturationOutput);

/// <summary>
/// Machine-readable layout for encoding inputs and decoding outputs without
/// inferring names from a circuit implementation.
/// </summary>
public sealed record ExperimentalPortLayout(
    int Width,
    ExperimentalValuationEncoding Encoding,
    ExperimentalValuationOperation Operation,
    string LeftZeroInput,
    string? RightZeroInput,
    string? ResultZeroOutput,
    IReadOnlyList<string> OpcodeInputs,
    string? RejectOutput,
    string? AcceptedOutput,
    string? PredicateOutput,
    string? ExactOutput,
    string? CanonicalOutput,
    int? QueryPrime,
    int? QueryExponent,
    IReadOnlyList<ExperimentalLanePortLayout> Lanes);

public sealed record DeclaredExperimentalCircuit(
    NandNetlist Netlist,
    ExperimentalPortLayout Ports,
    string EvidenceClass = "STRUCTURAL_DECLARED");

/// <summary>
/// Explicit NAND-only constructions for the frozen Build 002 experimental
/// lineage. Host arithmetic is used only while constructing fixed geometry;
/// evaluation is performed entirely by <see cref="NandNetlist.Evaluate"/>.
/// </summary>
public static class ExperimentalHardware
{
    public static DeclaredExperimentalCircuit BuildBinaryExponentCompose(int width) =>
        BuildBinaryExponent(width, ExperimentalValuationOperation.Compose);

    public static DeclaredExperimentalCircuit BuildBinaryExponentCancel(int width) =>
        BuildBinaryExponent(width, ExperimentalValuationOperation.Cancel);

    public static DeclaredExperimentalCircuit BuildBinaryExponentMeet(int width) =>
        BuildBinaryExponent(width, ExperimentalValuationOperation.Meet);

    public static DeclaredExperimentalCircuit BuildBinaryExponentJoin(int width) =>
        BuildBinaryExponent(width, ExperimentalValuationOperation.Join);

    public static DeclaredExperimentalCircuit BuildBinaryExponentDivides(int width) =>
        BuildBinaryExponent(width, ExperimentalValuationOperation.Divides);

    public static DeclaredExperimentalCircuit BuildBinaryExponentFunctionalUnit(int width)
    {
        var ports = CreateLayout(
            width,
            ExperimentalValuationEncoding.BinaryExponent,
            ExperimentalValuationOperation.FunctionalUnit,
            hasRight: true,
            hasResult: true,
            opcodeWidth: 3);
        var builder = new NandNetlistBuilder($"VFU.BINEXP.S4.FU.W{width}");
        var zero = builder.Constant("const.zero", BitState.Off);
        var one = builder.Constant("const.one", BitState.On);
        var left = CreateOperand(builder, ports, isLeft: true, zero, one);
        var right = CreateOperand(builder, ports, isLeft: false, zero, one);
        var opcode = ports.OpcodeInputs
            .Select(name => builder.Input(name, "ports/input/opcode"))
            .ToArray();

        var operations = new[]
        {
            BuildBinaryCompose(builder, ports, left, right, zero, one, "fu.compose"),
            BuildBinaryCancel(builder, ports, left, right, zero, one, "fu.cancel"),
            BuildBinaryMeet(builder, ports, left, right, zero, one, "fu.meet"),
            BuildBinaryJoin(builder, ports, left, right, zero, one, "fu.join"),
            BuildBinaryDivides(builder, ports, left, right, zero, one, "fu.divides"),
        };
        var selectors = Enumerable.Range(0, operations.Length)
            .Select(value => DecodeUnsigned(builder, opcode, value, zero, one, $"fu.decode[{value}]"))
            .ToArray();
        var validOpcode = OrMany(builder, selectors, zero, "fu.valid_opcode", AggregateRegion);
        var invalidOpcode = NandLogic.Not(
            builder,
            validOpcode,
            "fu.invalid_opcode",
            AggregateRegion);
        var selectedRejects = operations
            .Select((operation, index) => NandLogic.And(
                builder,
                selectors[index],
                operation.Reject,
                $"fu.selected_reject[{index}]",
                AggregateRegion))
            .ToArray();
        var selectedReject = OrMany(
            builder,
            selectedRejects,
            zero,
            "fu.selected_reject.any",
            AggregateRegion);
        var reject = NandLogic.Or(
            builder,
            invalidOpcode,
            selectedReject,
            "fu.reject",
            AggregateRegion);
        var accepted = NandLogic.Not(builder, reject, "fu.accepted", AggregateRegion);

        var resultZeroCandidates = Enumerable.Range(0, 4)
            .Select(index => NandLogic.And(
                builder,
                selectors[index],
                operations[index].Zero,
                $"fu.result_zero.selected[{index}]",
                AggregateRegion))
            .ToArray();
        var resultZero = OrMany(
            builder,
            resultZeroCandidates,
            zero,
            "fu.result_zero",
            AggregateRegion);

        var payload = NewPayload(ports);
        var saturated = new NandSignal[ports.Lanes.Count];
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var region = LaneRegion(lane);
            for (var bit = 0; bit < payload[lane].Length; bit++)
            {
                var candidates = Enumerable.Range(0, 4)
                    .Select(index => NandLogic.And(
                        builder,
                        selectors[index],
                        operations[index].Payload[lane][bit],
                        $"fu.lane[{lane}].bit[{bit}].selected[{index}]",
                        region))
                    .ToArray();
                payload[lane][bit] = OrMany(
                    builder,
                    candidates,
                    zero,
                    $"fu.lane[{lane}].bit[{bit}]",
                    region);
            }

            var saturationCandidates = Enumerable.Range(0, 4)
                .Select(index => NandLogic.And(
                    builder,
                    selectors[index],
                    operations[index].Saturated[lane],
                    $"fu.lane[{lane}].sat.selected[{index}]",
                    region))
                .ToArray();
            saturated[lane] = OrMany(
                builder,
                saturationCandidates,
                zero,
                $"fu.lane[{lane}].sat",
                region);
        }

        var predicate = NandLogic.And(
            builder,
            selectors[(int)BinaryExponentFuOperation.Divides],
            operations[(int)BinaryExponentFuOperation.Divides].Predicate,
            "fu.predicate",
            AggregateRegion);
        EmitStructuralOutputs(builder, ports, new OperationSignals(
            resultZero,
            payload,
            saturated,
            reject,
            accepted,
            predicate));
        return new DeclaredExperimentalCircuit(builder.Build(), ports);
    }

    public static DeclaredExperimentalCircuit BuildThermometerCompose(int width) =>
        BuildThermometer(width, ExperimentalValuationOperation.Compose);

    public static DeclaredExperimentalCircuit BuildThermometerMeet(int width) =>
        BuildThermometer(width, ExperimentalValuationOperation.Meet);

    public static DeclaredExperimentalCircuit BuildThermometerJoin(int width) =>
        BuildThermometer(width, ExperimentalValuationOperation.Join);

    public static DeclaredExperimentalCircuit BuildThermometerDivides(int width) =>
        BuildThermometer(width, ExperimentalValuationOperation.Divides);

    public static DeclaredExperimentalCircuit BuildThermometerCanonicalValidator(int width)
    {
        var ports = CreateLayout(
            width,
            ExperimentalValuationEncoding.Thermometer,
            ExperimentalValuationOperation.CanonicalValidate,
            hasRight: false,
            hasResult: false);
        var builder = new NandNetlistBuilder($"VFU.THERM.S4.CANONICAL.W{width}");
        var zero = builder.Constant("const.zero", BitState.Off);
        var one = builder.Constant("const.one", BitState.On);
        var operand = CreateOperand(builder, ports, isLeft: true, zero, one);
        builder.Output(ports.CanonicalOutput!, operand.Canonical, "ports/output/status");
        return new DeclaredExperimentalCircuit(builder.Build(), ports);
    }

    public static DeclaredExperimentalCircuit BuildThermometerThresholdQuery(
        int width,
        int prime,
        int exponent)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        var lane = domain.IndexOfPrime(prime);
        if (lane < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prime));
        }

        if (exponent < 1 || exponent > domain.CapAt(lane))
        {
            throw new ArgumentOutOfRangeException(nameof(exponent));
        }

        var ports = CreateLayout(
            width,
            ExperimentalValuationEncoding.Thermometer,
            ExperimentalValuationOperation.ThresholdQuery,
            hasRight: false,
            hasResult: false,
            queryPrime: prime,
            queryExponent: exponent);
        var builder = new NandNetlistBuilder($"VFU.THERM.S4.QUERY.P{prime}.K{exponent}.W{width}");
        var zero = builder.Constant("const.zero", BitState.Off);
        var one = builder.Constant("const.one", BitState.On);
        var operand = CreateOperand(builder, ports, isLeft: true, zero, one);
        var reject = NandLogic.Not(builder, operand.Canonical, "query.reject", AggregateRegion);
        var rawPredicate = NandLogic.Or(
            builder,
            operand.Zero,
            operand.Payload[lane][exponent - 1],
            "query.raw_predicate",
            LaneRegion(lane));
        var predicate = NandLogic.And(
            builder,
            operand.Canonical,
            rawPredicate,
            "query.predicate",
            LaneRegion(lane));
        var notSaturated = NandLogic.Not(
            builder,
            operand.Saturated[lane],
            "query.not_saturated",
            LaneRegion(lane));
        var rawExact = NandLogic.Or(
            builder,
            operand.Zero,
            notSaturated,
            "query.raw_exact",
            LaneRegion(lane));
        var exact = NandLogic.And(
            builder,
            operand.Canonical,
            rawExact,
            "query.exact",
            LaneRegion(lane));
        builder.Output(ports.RejectOutput!, reject, "ports/output/status");
        builder.Output(ports.AcceptedOutput!, operand.Canonical, "ports/output/status");
        builder.Output(ports.PredicateOutput!, predicate, "ports/output/predicate");
        builder.Output(ports.ExactOutput!, exact, "ports/output/status");
        return new DeclaredExperimentalCircuit(builder.Build(), ports);
    }

    private const string AggregateRegion = "status/lane:aggregate";

    private static DeclaredExperimentalCircuit BuildBinaryExponent(
        int width,
        ExperimentalValuationOperation operation)
    {
        var hasResult = operation != ExperimentalValuationOperation.Divides;
        var ports = CreateLayout(
            width,
            ExperimentalValuationEncoding.BinaryExponent,
            operation,
            hasRight: true,
            hasResult);
        var builder = new NandNetlistBuilder($"VFU.BINEXP.S4.{operation.ToString().ToUpperInvariant()}.W{width}");
        var zero = builder.Constant("const.zero", BitState.Off);
        var one = builder.Constant("const.one", BitState.On);
        var left = CreateOperand(builder, ports, isLeft: true, zero, one);
        var right = CreateOperand(builder, ports, isLeft: false, zero, one);
        var signals = operation switch
        {
            ExperimentalValuationOperation.Compose =>
                BuildBinaryCompose(builder, ports, left, right, zero, one, "compose"),
            ExperimentalValuationOperation.Cancel =>
                BuildBinaryCancel(builder, ports, left, right, zero, one, "cancel"),
            ExperimentalValuationOperation.Meet =>
                BuildBinaryMeet(builder, ports, left, right, zero, one, "meet"),
            ExperimentalValuationOperation.Join =>
                BuildBinaryJoin(builder, ports, left, right, zero, one, "join"),
            ExperimentalValuationOperation.Divides =>
                BuildBinaryDivides(builder, ports, left, right, zero, one, "divides"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        EmitStructuralOutputs(builder, ports, signals);
        return new DeclaredExperimentalCircuit(builder.Build(), ports);
    }

    private static DeclaredExperimentalCircuit BuildThermometer(
        int width,
        ExperimentalValuationOperation operation)
    {
        var hasResult = operation != ExperimentalValuationOperation.Divides;
        var ports = CreateLayout(
            width,
            ExperimentalValuationEncoding.Thermometer,
            operation,
            hasRight: true,
            hasResult);
        var builder = new NandNetlistBuilder($"VFU.THERM.S4.{operation.ToString().ToUpperInvariant()}.W{width}");
        var zero = builder.Constant("const.zero", BitState.Off);
        var one = builder.Constant("const.one", BitState.On);
        var left = CreateOperand(builder, ports, isLeft: true, zero, one);
        var right = CreateOperand(builder, ports, isLeft: false, zero, one);
        var signals = operation switch
        {
            ExperimentalValuationOperation.Compose =>
                BuildThermometerCompose(builder, ports, left, right, zero, one, "compose"),
            ExperimentalValuationOperation.Meet =>
                BuildThermometerMeet(builder, ports, left, right, zero, one, "meet"),
            ExperimentalValuationOperation.Join =>
                BuildThermometerJoin(builder, ports, left, right, zero, one, "join"),
            ExperimentalValuationOperation.Divides =>
                BuildThermometerDivides(builder, ports, left, right, zero, one, "divides"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        EmitStructuralOutputs(builder, ports, signals);
        return new DeclaredExperimentalCircuit(builder.Build(), ports);
    }

    private static OperationSignals BuildBinaryCompose(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        NandSignal one,
        string prefix)
    {
        var reject = RejectMalformed(builder, left, right, zero, prefix);
        var accepted = NandLogic.Not(builder, reject, $"{prefix}.accepted", AggregateRegion);
        var rawZero = NandLogic.Or(
            builder,
            left.Zero,
            right.Zero,
            $"{prefix}.zero.raw",
            AggregateRegion);
        var resultZero = NandLogic.And(
            builder,
            accepted,
            rawZero,
            $"{prefix}.zero",
            AggregateRegion);
        var notRawZero = NandLogic.Not(
            builder,
            rawZero,
            $"{prefix}.not_zero",
            AggregateRegion);
        var active = NandLogic.And(
            builder,
            accepted,
            notRawZero,
            $"{prefix}.active",
            AggregateRegion);
        var payload = NewPayload(ports);
        var saturated = new NandSignal[ports.Lanes.Count];

        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var region = LaneRegion(lane);
            var sum = NandLogic.AddWord(
                builder,
                left.Payload[lane],
                right.Payload[lane],
                zero,
                $"{prefix}.lane[{lane}].add",
                region);
            var cap = ConstantWord(ports.Lanes[lane].Cap, ports.Lanes[lane].PayloadWidth, zero, one);
            var comparison = NandLogic.CompareWord(
                builder,
                sum.Value,
                cap,
                $"{prefix}.lane[{lane}].cap_compare",
                region);
            var beyondCap = NandLogic.Or(
                builder,
                sum.Status,
                comparison.Greater,
                $"{prefix}.lane[{lane}].beyond_cap",
                region);
            var inheritedSaturation = OrMany(
                builder,
                [left.Saturated[lane], right.Saturated[lane], beyondCap],
                zero,
                $"{prefix}.lane[{lane}].sat.raw",
                region);
            saturated[lane] = NandLogic.And(
                builder,
                active,
                inheritedSaturation,
                $"{prefix}.lane[{lane}].sat",
                region);
            for (var bit = 0; bit < payload[lane].Length; bit++)
            {
                var clamped = NandLogic.Mux(
                    builder,
                    inheritedSaturation,
                    cap[bit],
                    sum.Value[bit],
                    $"{prefix}.lane[{lane}].bit[{bit}].clamp",
                    region);
                payload[lane][bit] = NandLogic.And(
                    builder,
                    active,
                    clamped,
                    $"{prefix}.lane[{lane}].bit[{bit}]",
                    region);
            }
        }

        return new OperationSignals(resultZero, payload, saturated, reject, accepted, zero);
    }

    private static OperationSignals BuildBinaryCancel(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        NandSignal one,
        string prefix)
    {
        var differences = NewPayload(ports);
        var underflows = new NandSignal[ports.Lanes.Count];
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var result = NandLogic.SubtractWord(
                builder,
                left.Payload[lane],
                right.Payload[lane],
                zero,
                $"{prefix}.lane[{lane}].subtract",
                LaneRegion(lane));
            differences[lane] = result.Value;
            underflows[lane] = result.Status;
        }

        var malformed = RejectMalformed(builder, left, right, zero, $"{prefix}.malformed");
        var saturatedInput = NandLogic.Or(
            builder,
            left.AnySaturated,
            right.AnySaturated,
            $"{prefix}.saturated_input",
            AggregateRegion);
        var underflowAny = OrMany(
            builder,
            underflows,
            zero,
            $"{prefix}.underflow.any",
            AggregateRegion);
        var leftNonzero = NandLogic.Not(
            builder,
            left.Zero,
            $"{prefix}.left_nonzero",
            AggregateRegion);
        var effectiveUnderflow = NandLogic.And(
            builder,
            leftNonzero,
            underflowAny,
            $"{prefix}.underflow.effective",
            AggregateRegion);
        var reject = OrMany(
            builder,
            [malformed, saturatedInput, right.Zero, effectiveUnderflow],
            zero,
            $"{prefix}.reject",
            AggregateRegion);
        var accepted = NandLogic.Not(builder, reject, $"{prefix}.accepted", AggregateRegion);
        var resultZero = NandLogic.And(
            builder,
            accepted,
            left.Zero,
            $"{prefix}.zero",
            AggregateRegion);
        var active = NandLogic.And(
            builder,
            accepted,
            leftNonzero,
            $"{prefix}.active",
            AggregateRegion);
        var payload = NewPayload(ports);
        var saturated = new NandSignal[ports.Lanes.Count];
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var region = LaneRegion(lane);
            saturated[lane] = zero;
            for (var bit = 0; bit < payload[lane].Length; bit++)
            {
                payload[lane][bit] = NandLogic.And(
                    builder,
                    active,
                    differences[lane][bit],
                    $"{prefix}.lane[{lane}].bit[{bit}]",
                    region);
            }
        }

        return new OperationSignals(resultZero, payload, saturated, reject, accepted, zero);
    }

    private static OperationSignals BuildBinaryMeet(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        NandSignal one,
        string prefix) =>
        BuildBinaryExtremum(builder, ports, left, right, zero, prefix, isMeet: true);

    private static OperationSignals BuildBinaryJoin(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        NandSignal one,
        string prefix) =>
        BuildBinaryExtremum(builder, ports, left, right, zero, prefix, isMeet: false);

    private static OperationSignals BuildBinaryExtremum(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        string prefix,
        bool isMeet)
    {
        var malformed = RejectMalformed(builder, left, right, zero, $"{prefix}.malformed");
        var saturatedInput = NandLogic.Or(
            builder,
            left.AnySaturated,
            right.AnySaturated,
            $"{prefix}.saturated_input",
            AggregateRegion);
        var reject = NandLogic.Or(
            builder,
            malformed,
            saturatedInput,
            $"{prefix}.reject",
            AggregateRegion);
        var accepted = NandLogic.Not(builder, reject, $"{prefix}.accepted", AggregateRegion);
        var rawZero = isMeet
            ? NandLogic.And(builder, left.Zero, right.Zero, $"{prefix}.zero.raw", AggregateRegion)
            : NandLogic.Or(builder, left.Zero, right.Zero, $"{prefix}.zero.raw", AggregateRegion);
        var resultZero = NandLogic.And(
            builder,
            accepted,
            rawZero,
            $"{prefix}.zero",
            AggregateRegion);
        var notRawZero = NandLogic.Not(builder, rawZero, $"{prefix}.not_zero", AggregateRegion);
        var active = NandLogic.And(
            builder,
            accepted,
            notRawZero,
            $"{prefix}.active",
            AggregateRegion);
        var payload = NewPayload(ports);
        var saturated = Enumerable.Repeat(zero, ports.Lanes.Count).ToArray();

        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var region = LaneRegion(lane);
            var comparison = NandLogic.CompareWord(
                builder,
                left.Payload[lane],
                right.Payload[lane],
                $"{prefix}.lane[{lane}].compare",
                region);
            for (var bit = 0; bit < payload[lane].Length; bit++)
            {
                var extremum = NandLogic.Mux(
                    builder,
                    comparison.Less,
                    isMeet ? left.Payload[lane][bit] : right.Payload[lane][bit],
                    isMeet ? right.Payload[lane][bit] : left.Payload[lane][bit],
                    $"{prefix}.lane[{lane}].bit[{bit}].extremum",
                    region);
                var leftZeroChoice = NandLogic.Mux(
                    builder,
                    left.Zero,
                    isMeet ? right.Payload[lane][bit] : zero,
                    extremum,
                    $"{prefix}.lane[{lane}].bit[{bit}].left_zero",
                    region);
                var bothZeroChoice = NandLogic.Mux(
                    builder,
                    right.Zero,
                    isMeet ? left.Payload[lane][bit] : zero,
                    leftZeroChoice,
                    $"{prefix}.lane[{lane}].bit[{bit}].right_zero",
                    region);
                payload[lane][bit] = NandLogic.And(
                    builder,
                    active,
                    bothZeroChoice,
                    $"{prefix}.lane[{lane}].bit[{bit}]",
                    region);
            }
        }

        return new OperationSignals(resultZero, payload, saturated, reject, accepted, zero);
    }

    private static OperationSignals BuildBinaryDivides(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        NandSignal one,
        string prefix)
    {
        var malformed = RejectMalformed(builder, left, right, zero, $"{prefix}.malformed");
        var saturatedInput = NandLogic.Or(
            builder,
            left.AnySaturated,
            right.AnySaturated,
            $"{prefix}.saturated_input",
            AggregateRegion);
        var reject = NandLogic.Or(
            builder,
            malformed,
            saturatedInput,
            $"{prefix}.reject",
            AggregateRegion);
        var accepted = NandLogic.Not(builder, reject, $"{prefix}.accepted", AggregateRegion);
        var laneResults = new NandSignal[ports.Lanes.Count];
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var comparison = NandLogic.CompareWord(
                builder,
                left.Payload[lane],
                right.Payload[lane],
                $"{prefix}.lane[{lane}].compare",
                LaneRegion(lane));
            laneResults[lane] = NandLogic.Not(
                builder,
                comparison.Greater,
                $"{prefix}.lane[{lane}].le",
                LaneRegion(lane));
        }

        var allLanes = AndMany(
            builder,
            laneResults,
            one,
            $"{prefix}.all_lanes",
            AggregateRegion);
        var rightZeroOrLanes = NandLogic.Or(
            builder,
            right.Zero,
            allLanes,
            $"{prefix}.right_zero_or_lanes",
            AggregateRegion);
        var leftNonzero = NandLogic.Not(
            builder,
            left.Zero,
            $"{prefix}.left_nonzero",
            AggregateRegion);
        var nonzeroCase = NandLogic.And(
            builder,
            leftNonzero,
            rightZeroOrLanes,
            $"{prefix}.nonzero_case",
            AggregateRegion);
        var bothZero = NandLogic.And(
            builder,
            left.Zero,
            right.Zero,
            $"{prefix}.both_zero",
            AggregateRegion);
        var rawPredicate = NandLogic.Or(
            builder,
            bothZero,
            nonzeroCase,
            $"{prefix}.predicate.raw",
            AggregateRegion);
        var predicate = NandLogic.And(
            builder,
            accepted,
            rawPredicate,
            $"{prefix}.predicate",
            AggregateRegion);
        return new OperationSignals(zero, NewPayload(ports), new NandSignal[ports.Lanes.Count], reject, accepted, predicate);
    }

    private static OperationSignals BuildThermometerCompose(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        NandSignal one,
        string prefix)
    {
        var reject = RejectMalformed(builder, left, right, zero, prefix);
        var accepted = NandLogic.Not(builder, reject, $"{prefix}.accepted", AggregateRegion);
        var rawZero = NandLogic.Or(builder, left.Zero, right.Zero, $"{prefix}.zero.raw", AggregateRegion);
        var resultZero = NandLogic.And(builder, accepted, rawZero, $"{prefix}.zero", AggregateRegion);
        var notRawZero = NandLogic.Not(builder, rawZero, $"{prefix}.not_zero", AggregateRegion);
        var active = NandLogic.And(builder, accepted, notRawZero, $"{prefix}.active", AggregateRegion);
        var payload = NewPayload(ports);
        var saturated = new NandSignal[ports.Lanes.Count];

        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var region = LaneRegion(lane);
            for (var threshold = 1; threshold <= ports.Lanes[lane].Cap; threshold++)
            {
                var convolution = ConvolveThreshold(
                    builder,
                    left.Payload[lane],
                    right.Payload[lane],
                    threshold,
                    zero,
                    one,
                    $"{prefix}.lane[{lane}].threshold[{threshold}]",
                    region);
                payload[lane][threshold - 1] = NandLogic.And(
                    builder,
                    active,
                    convolution,
                    $"{prefix}.lane[{lane}].threshold[{threshold}].active",
                    region);
            }

            var overflow = ConvolveThreshold(
                builder,
                left.Payload[lane],
                right.Payload[lane],
                ports.Lanes[lane].Cap + 1,
                zero,
                one,
                $"{prefix}.lane[{lane}].overflow",
                region);
            var rawSaturation = OrMany(
                builder,
                [left.Saturated[lane], right.Saturated[lane], overflow],
                zero,
                $"{prefix}.lane[{lane}].sat.raw",
                region);
            saturated[lane] = NandLogic.And(
                builder,
                active,
                rawSaturation,
                $"{prefix}.lane[{lane}].sat",
                region);
        }

        return new OperationSignals(resultZero, payload, saturated, reject, accepted, zero);
    }

    private static OperationSignals BuildThermometerMeet(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        NandSignal one,
        string prefix) =>
        BuildThermometerExtremum(builder, ports, left, right, zero, prefix, isMeet: true);

    private static OperationSignals BuildThermometerJoin(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        NandSignal one,
        string prefix) =>
        BuildThermometerExtremum(builder, ports, left, right, zero, prefix, isMeet: false);

    private static OperationSignals BuildThermometerExtremum(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        string prefix,
        bool isMeet)
    {
        var malformed = RejectMalformed(builder, left, right, zero, $"{prefix}.malformed");
        var saturatedInput = NandLogic.Or(
            builder,
            left.AnySaturated,
            right.AnySaturated,
            $"{prefix}.saturated_input",
            AggregateRegion);
        var reject = NandLogic.Or(builder, malformed, saturatedInput, $"{prefix}.reject", AggregateRegion);
        var accepted = NandLogic.Not(builder, reject, $"{prefix}.accepted", AggregateRegion);
        var rawZero = isMeet
            ? NandLogic.And(builder, left.Zero, right.Zero, $"{prefix}.zero.raw", AggregateRegion)
            : NandLogic.Or(builder, left.Zero, right.Zero, $"{prefix}.zero.raw", AggregateRegion);
        var resultZero = NandLogic.And(builder, accepted, rawZero, $"{prefix}.zero", AggregateRegion);
        var notRawZero = NandLogic.Not(builder, rawZero, $"{prefix}.not_zero", AggregateRegion);
        var active = NandLogic.And(builder, accepted, notRawZero, $"{prefix}.active", AggregateRegion);
        var payload = NewPayload(ports);
        var saturated = Enumerable.Repeat(zero, ports.Lanes.Count).ToArray();

        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var region = LaneRegion(lane);
            for (var bit = 0; bit < payload[lane].Length; bit++)
            {
                var extremum = isMeet
                    ? NandLogic.And(
                        builder,
                        left.Payload[lane][bit],
                        right.Payload[lane][bit],
                        $"{prefix}.lane[{lane}].threshold[{bit + 1}].and",
                        region)
                    : NandLogic.Or(
                        builder,
                        left.Payload[lane][bit],
                        right.Payload[lane][bit],
                        $"{prefix}.lane[{lane}].threshold[{bit + 1}].or",
                        region);
                var leftZeroChoice = NandLogic.Mux(
                    builder,
                    left.Zero,
                    isMeet ? right.Payload[lane][bit] : zero,
                    extremum,
                    $"{prefix}.lane[{lane}].threshold[{bit + 1}].left_zero",
                    region);
                var bothZeroChoice = NandLogic.Mux(
                    builder,
                    right.Zero,
                    isMeet ? left.Payload[lane][bit] : zero,
                    leftZeroChoice,
                    $"{prefix}.lane[{lane}].threshold[{bit + 1}].right_zero",
                    region);
                payload[lane][bit] = NandLogic.And(
                    builder,
                    active,
                    bothZeroChoice,
                    $"{prefix}.lane[{lane}].threshold[{bit + 1}]",
                    region);
            }
        }

        return new OperationSignals(resultZero, payload, saturated, reject, accepted, zero);
    }

    private static OperationSignals BuildThermometerDivides(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        NandSignal one,
        string prefix)
    {
        var malformed = RejectMalformed(builder, left, right, zero, $"{prefix}.malformed");
        var saturatedInput = NandLogic.Or(
            builder,
            left.AnySaturated,
            right.AnySaturated,
            $"{prefix}.saturated_input",
            AggregateRegion);
        var reject = NandLogic.Or(builder, malformed, saturatedInput, $"{prefix}.reject", AggregateRegion);
        var accepted = NandLogic.Not(builder, reject, $"{prefix}.accepted", AggregateRegion);
        var implications = new List<NandSignal>();
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var region = LaneRegion(lane);
            for (var bit = 0; bit < ports.Lanes[lane].PayloadWidth; bit++)
            {
                var notLeft = NandLogic.Not(
                    builder,
                    left.Payload[lane][bit],
                    $"{prefix}.lane[{lane}].threshold[{bit + 1}].not_left",
                    region);
                implications.Add(NandLogic.Or(
                    builder,
                    notLeft,
                    right.Payload[lane][bit],
                    $"{prefix}.lane[{lane}].threshold[{bit + 1}].implies",
                    region));
            }
        }

        var allThresholds = AndMany(
            builder,
            implications,
            one,
            $"{prefix}.all_thresholds",
            AggregateRegion);
        var rightZeroOrThresholds = NandLogic.Or(
            builder,
            right.Zero,
            allThresholds,
            $"{prefix}.right_zero_or_thresholds",
            AggregateRegion);
        var leftNonzero = NandLogic.Not(builder, left.Zero, $"{prefix}.left_nonzero", AggregateRegion);
        var nonzeroCase = NandLogic.And(
            builder,
            leftNonzero,
            rightZeroOrThresholds,
            $"{prefix}.nonzero_case",
            AggregateRegion);
        var bothZero = NandLogic.And(
            builder,
            left.Zero,
            right.Zero,
            $"{prefix}.both_zero",
            AggregateRegion);
        var rawPredicate = NandLogic.Or(
            builder,
            bothZero,
            nonzeroCase,
            $"{prefix}.predicate.raw",
            AggregateRegion);
        var predicate = NandLogic.And(
            builder,
            accepted,
            rawPredicate,
            $"{prefix}.predicate",
            AggregateRegion);
        return new OperationSignals(zero, NewPayload(ports), new NandSignal[ports.Lanes.Count], reject, accepted, predicate);
    }

    private static OperandSignals CreateOperand(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        bool isLeft,
        NandSignal zero,
        NandSignal one)
    {
        var side = isLeft ? "a" : "b";
        var zeroName = isLeft ? ports.LeftZeroInput : ports.RightZeroInput!;
        var zeroSignal = builder.Input(zeroName, $"ports/input/{side}/control");
        var payload = new NandSignal[ports.Lanes.Count][];
        var saturated = new NandSignal[ports.Lanes.Count];
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var layout = ports.Lanes[lane];
            var names = isLeft ? layout.LeftPayloadInputs : layout.RightPayloadInputs;
            payload[lane] = names
                .Select(name => builder.Input(name, $"ports/input/{side}/{LaneRegion(lane)}"))
                .ToArray();
            saturated[lane] = builder.Input(
                isLeft ? layout.LeftSaturationInput : layout.RightSaturationInput!,
                $"ports/input/{side}/{LaneRegion(lane)}");
        }

        var canonical = ports.Encoding == ExperimentalValuationEncoding.BinaryExponent
            ? BuildBinaryCanonical(builder, ports, zeroSignal, payload, saturated, zero, one, $"input.{side}.canonical")
            : BuildThermometerCanonical(builder, ports, zeroSignal, payload, saturated, zero, one, $"input.{side}.canonical");
        var anySaturated = OrMany(
            builder,
            saturated,
            zero,
            $"input.{side}.saturated.any",
            AggregateRegion);
        return new OperandSignals(zeroSignal, payload, saturated, canonical, anySaturated);
    }

    private static NandSignal BuildBinaryCanonical(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        NandSignal isZero,
        IReadOnlyList<NandSignal[]> payload,
        IReadOnlyList<NandSignal> saturated,
        NandSignal zero,
        NandSignal one,
        string prefix)
    {
        var violations = new List<NandSignal>();
        var payloadOrSaturation = new List<NandSignal>();
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var region = LaneRegion(lane);
            var cap = ConstantWord(ports.Lanes[lane].Cap, ports.Lanes[lane].PayloadWidth, zero, one);
            var comparison = NandLogic.CompareWord(
                builder,
                payload[lane],
                cap,
                $"{prefix}.lane[{lane}].cap_compare",
                region);
            violations.Add(comparison.Greater);
            var notCap = NandLogic.Not(
                builder,
                comparison.Equal,
                $"{prefix}.lane[{lane}].not_cap",
                region);
            violations.Add(NandLogic.And(
                builder,
                saturated[lane],
                notCap,
                $"{prefix}.lane[{lane}].sat_without_cap",
                region));
            payloadOrSaturation.AddRange(payload[lane]);
            payloadOrSaturation.Add(saturated[lane]);
        }

        var payloadSet = OrMany(
            builder,
            payloadOrSaturation,
            zero,
            $"{prefix}.payload_set",
            AggregateRegion);
        violations.Add(NandLogic.And(
            builder,
            isZero,
            payloadSet,
            $"{prefix}.zero_payload",
            AggregateRegion));
        var invalid = OrMany(builder, violations, zero, $"{prefix}.invalid", AggregateRegion);
        return NandLogic.Not(builder, invalid, $"{prefix}.valid", AggregateRegion);
    }

    private static NandSignal BuildThermometerCanonical(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        NandSignal isZero,
        IReadOnlyList<NandSignal[]> payload,
        IReadOnlyList<NandSignal> saturated,
        NandSignal zero,
        NandSignal one,
        string prefix)
    {
        var violations = new List<NandSignal>();
        var payloadOrSaturation = new List<NandSignal>();
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var region = LaneRegion(lane);
            for (var bit = 1; bit < payload[lane].Length; bit++)
            {
                var notPrevious = NandLogic.Not(
                    builder,
                    payload[lane][bit - 1],
                    $"{prefix}.lane[{lane}].transition[{bit}].not_previous",
                    region);
                violations.Add(NandLogic.And(
                    builder,
                    notPrevious,
                    payload[lane][bit],
                    $"{prefix}.lane[{lane}].transition[{bit}].rise",
                    region));
            }

            var allThresholds = AndMany(
                builder,
                payload[lane],
                one,
                $"{prefix}.lane[{lane}].all_thresholds",
                region);
            var notAllThresholds = NandLogic.Not(
                builder,
                allThresholds,
                $"{prefix}.lane[{lane}].not_all_thresholds",
                region);
            violations.Add(NandLogic.And(
                builder,
                saturated[lane],
                notAllThresholds,
                $"{prefix}.lane[{lane}].sat_without_cap",
                region));
            payloadOrSaturation.AddRange(payload[lane]);
            payloadOrSaturation.Add(saturated[lane]);
        }

        var payloadSet = OrMany(
            builder,
            payloadOrSaturation,
            zero,
            $"{prefix}.payload_set",
            AggregateRegion);
        violations.Add(NandLogic.And(
            builder,
            isZero,
            payloadSet,
            $"{prefix}.zero_payload",
            AggregateRegion));
        var invalid = OrMany(builder, violations, zero, $"{prefix}.invalid", AggregateRegion);
        return NandLogic.Not(builder, invalid, $"{prefix}.valid", AggregateRegion);
    }

    private static NandSignal RejectMalformed(
        NandNetlistBuilder builder,
        OperandSignals left,
        OperandSignals right,
        NandSignal zero,
        string prefix)
    {
        var leftInvalid = NandLogic.Not(
            builder,
            left.Canonical,
            $"{prefix}.left_invalid",
            AggregateRegion);
        var rightInvalid = NandLogic.Not(
            builder,
            right.Canonical,
            $"{prefix}.right_invalid",
            AggregateRegion);
        return NandLogic.Or(
            builder,
            leftInvalid,
            rightInvalid,
            $"{prefix}.malformed",
            AggregateRegion);
    }

    private static NandSignal ConvolveThreshold(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> left,
        IReadOnlyList<NandSignal> right,
        int threshold,
        NandSignal zero,
        NandSignal one,
        string prefix,
        string region)
    {
        var terms = new List<NandSignal>();
        for (var leftExponent = 0; leftExponent <= threshold; leftExponent++)
        {
            var rightExponent = threshold - leftExponent;
            var leftSignal = ThresholdSignal(left, leftExponent, zero, one);
            var rightSignal = ThresholdSignal(right, rightExponent, zero, one);
            if (leftSignal == zero || rightSignal == zero)
            {
                continue;
            }

            if (leftSignal == one)
            {
                terms.Add(rightSignal);
            }
            else if (rightSignal == one)
            {
                terms.Add(leftSignal);
            }
            else
            {
                terms.Add(NandLogic.And(
                    builder,
                    leftSignal,
                    rightSignal,
                    $"{prefix}.term[{leftExponent}]",
                    region));
            }
        }

        return OrMany(builder, terms, zero, $"{prefix}.or", region);
    }

    private static NandSignal ThresholdSignal(
        IReadOnlyList<NandSignal> thresholds,
        int exponent,
        NandSignal zero,
        NandSignal one)
    {
        if (exponent == 0)
        {
            return one;
        }

        return exponent > thresholds.Count ? zero : thresholds[exponent - 1];
    }

    private static void EmitStructuralOutputs(
        NandNetlistBuilder builder,
        ExperimentalPortLayout ports,
        OperationSignals signals)
    {
        if (ports.ResultZeroOutput is not null)
        {
            builder.Output(ports.ResultZeroOutput, signals.Zero, "ports/output/result/control");
            for (var lane = 0; lane < ports.Lanes.Count; lane++)
            {
                var layout = ports.Lanes[lane];
                for (var bit = 0; bit < layout.ResultPayloadOutputs.Count; bit++)
                {
                    builder.Output(
                        layout.ResultPayloadOutputs[bit],
                        signals.Payload[lane][bit],
                        $"ports/output/result/{LaneRegion(lane)}");
                }

                builder.Output(
                    layout.ResultSaturationOutput!,
                    signals.Saturated[lane],
                    $"ports/output/result/{LaneRegion(lane)}");
            }
        }

        builder.Output(ports.RejectOutput!, signals.Reject, "ports/output/status");
        builder.Output(ports.AcceptedOutput!, signals.Accepted, "ports/output/status");
        if (ports.PredicateOutput is not null)
        {
            builder.Output(ports.PredicateOutput, signals.Predicate, "ports/output/predicate");
        }
    }

    private static ExperimentalPortLayout CreateLayout(
        int width,
        ExperimentalValuationEncoding encoding,
        ExperimentalValuationOperation operation,
        bool hasRight,
        bool hasResult,
        int opcodeWidth = 0,
        int? queryPrime = null,
        int? queryExponent = null)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        var lanes = new ExperimentalLanePortLayout[domain.LaneCount];
        for (var lane = 0; lane < lanes.Length; lane++)
        {
            var payloadWidth = encoding == ExperimentalValuationEncoding.BinaryExponent
                ? BinaryWidth(domain.CapAt(lane))
                : domain.CapAt(lane);
            var payloadLabel = encoding == ExperimentalValuationEncoding.BinaryExponent
                ? "bit"
                : "threshold";
            var left = Enumerable.Range(0, payloadWidth)
                .Select(index => $"a.lane[{lane}].{payloadLabel}[{DisplayIndex(encoding, index)}]")
                .ToArray();
            var right = hasRight
                ? Enumerable.Range(0, payloadWidth)
                    .Select(index => $"b.lane[{lane}].{payloadLabel}[{DisplayIndex(encoding, index)}]")
                    .ToArray()
                : [];
            var result = hasResult
                ? Enumerable.Range(0, payloadWidth)
                    .Select(index => $"result.lane[{lane}].{payloadLabel}[{DisplayIndex(encoding, index)}]")
                    .ToArray()
                : [];
            lanes[lane] = new ExperimentalLanePortLayout(
                lane,
                domain.PrimeAt(lane),
                domain.CapAt(lane),
                payloadWidth,
                Array.AsReadOnly(left),
                Array.AsReadOnly(right),
                Array.AsReadOnly(result),
                $"a.lane[{lane}].sat",
                hasRight ? $"b.lane[{lane}].sat" : null,
                hasResult ? $"result.lane[{lane}].sat" : null);
        }

        var opcode = Enumerable.Range(0, opcodeWidth).Select(index => $"opcode[{index}]").ToArray();
        var reportsStatus = operation != ExperimentalValuationOperation.CanonicalValidate;
        return new ExperimentalPortLayout(
            width,
            encoding,
            operation,
            "a.zero",
            hasRight ? "b.zero" : null,
            hasResult ? "result.zero" : null,
            Array.AsReadOnly(opcode),
            reportsStatus ? "status.reject" : null,
            reportsStatus ? "status.accepted" : null,
            operation is ExperimentalValuationOperation.Divides or
                ExperimentalValuationOperation.ThresholdQuery or
                ExperimentalValuationOperation.FunctionalUnit
                ? "predicate.result"
                : null,
            operation == ExperimentalValuationOperation.ThresholdQuery ? "status.exact" : null,
            operation == ExperimentalValuationOperation.CanonicalValidate ? "status.canonical" : null,
            queryPrime,
            queryExponent,
            Array.AsReadOnly(lanes));
    }

    private static int DisplayIndex(ExperimentalValuationEncoding encoding, int zeroBased) =>
        encoding == ExperimentalValuationEncoding.BinaryExponent ? zeroBased : zeroBased + 1;

    private static int BinaryWidth(int cap)
    {
        var width = 0;
        var states = 1;
        while (states < cap + 1)
        {
            width++;
            states <<= 1;
        }

        return Math.Max(width, 1);
    }

    private static NandSignal[][] NewPayload(ExperimentalPortLayout ports) =>
        ports.Lanes.Select(lane => new NandSignal[lane.PayloadWidth]).ToArray();

    private static NandSignal[] ConstantWord(
        int value,
        int width,
        NandSignal zero,
        NandSignal one) =>
        Enumerable.Range(0, width)
            .Select(bit => ((value >> bit) & 1) == 0 ? zero : one)
            .ToArray();

    private static NandSignal DecodeUnsigned(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> bits,
        int value,
        NandSignal zero,
        NandSignal one,
        string prefix)
    {
        var matches = new NandSignal[bits.Count];
        for (var bit = 0; bit < bits.Count; bit++)
        {
            var expected = ((value >> bit) & 1) == 0 ? zero : one;
            matches[bit] = NandLogic.Xnor(
                builder,
                bits[bit],
                expected,
                $"{prefix}.bit[{bit}]",
                "control/opcode");
        }

        return AndMany(builder, matches, one, $"{prefix}.all", "control/opcode");
    }

    private static NandSignal AndMany(
        NandNetlistBuilder builder,
        IEnumerable<NandSignal> signals,
        NandSignal one,
        string prefix,
        string region)
    {
        var array = signals.ToArray();
        if (array.Length == 0)
        {
            return one;
        }

        var result = array[0];
        for (var index = 1; index < array.Length; index++)
        {
            result = NandLogic.And(
                builder,
                result,
                array[index],
                $"{prefix}.and[{index}]",
                region);
        }

        return result;
    }

    private static NandSignal OrMany(
        NandNetlistBuilder builder,
        IEnumerable<NandSignal> signals,
        NandSignal zero,
        string prefix,
        string region)
    {
        var array = signals.ToArray();
        if (array.Length == 0)
        {
            return zero;
        }

        var result = array[0];
        for (var index = 1; index < array.Length; index++)
        {
            result = NandLogic.Or(
                builder,
                result,
                array[index],
                $"{prefix}.or[{index}]",
                region);
        }

        return result;
    }

    private static string LaneRegion(int lane) => $"lane:{lane}";

    private sealed record OperandSignals(
        NandSignal Zero,
        NandSignal[][] Payload,
        NandSignal[] Saturated,
        NandSignal Canonical,
        NandSignal AnySaturated);

    private sealed record OperationSignals(
        NandSignal Zero,
        NandSignal[][] Payload,
        NandSignal[] Saturated,
        NandSignal Reject,
        NandSignal Accepted,
        NandSignal Predicate);
}
