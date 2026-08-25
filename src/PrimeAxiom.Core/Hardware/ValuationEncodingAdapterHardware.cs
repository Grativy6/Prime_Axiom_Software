using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hardware;

/// <summary>
/// Stable ports for one same-cap valuation-encoding lane. Binary payload bits
/// are little-endian; thermometer payload bits are ordered by increasing
/// threshold. Saturation denotes a lower bound at the declared lane cap.
/// </summary>
public sealed record ValuationEncodingAdapterLanePortLayout(
    int Lane,
    int Prime,
    int Cap,
    IReadOnlyList<string> SourcePayloadInputs,
    string SourceSaturationInput,
    IReadOnlyList<string> ResultPayloadOutputs,
    string ResultSaturationOutput);

public sealed record ValuationEncodingAdapterPortLayout(
    int Width,
    ExperimentalValuationEncoding SourceEncoding,
    ExperimentalValuationEncoding ResultEncoding,
    string SourceZeroInput,
    string ResultZeroOutput,
    string AcceptedOutput,
    string MalformedOutput,
    IReadOnlyList<ValuationEncodingAdapterLanePortLayout> Lanes);

public sealed record DeclaredValuationEncodingAdapterCircuit(
    NandNetlist Netlist,
    ValuationEncodingAdapterPortLayout Ports,
    string EvidenceClass = "STRUCTURAL_DECLARED");

/// <summary>
/// Declared NAND-only, same-cap conversion between the two Build 002 S4
/// payload encodings. Conversion preserves an exact cap separately from a
/// saturated lower bound at cap. Malformed sources reject atomically.
/// </summary>
public static class ValuationEncodingAdapterHardware
{
    private const string AggregateRegion = "status/lane:aggregate";

    public static DeclaredValuationEncodingAdapterCircuit BuildBinaryExponentToThermometer(
        int width) =>
        Build(
            width,
            ExperimentalValuationEncoding.BinaryExponent,
            ExperimentalValuationEncoding.Thermometer);

    public static DeclaredValuationEncodingAdapterCircuit BuildThermometerToBinaryExponent(
        int width) =>
        Build(
            width,
            ExperimentalValuationEncoding.Thermometer,
            ExperimentalValuationEncoding.BinaryExponent);

    private static DeclaredValuationEncodingAdapterCircuit Build(
        int width,
        ExperimentalValuationEncoding sourceEncoding,
        ExperimentalValuationEncoding resultEncoding)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        var ports = CreateLayout(domain, sourceEncoding, resultEncoding);
        var sourceName = sourceEncoding == ExperimentalValuationEncoding.BinaryExponent
            ? "BINEXP"
            : "THERM";
        var resultName = resultEncoding == ExperimentalValuationEncoding.BinaryExponent
            ? "BINEXP"
            : "THERM";
        var builder = new NandNetlistBuilder($"ADAPTER.{sourceName}_TO_{resultName}.S4.W{width}");
        var zero = builder.Constant("const.zero", BitState.Off);
        var one = builder.Constant("const.one", BitState.On);
        var sourceZero = builder.Input(ports.SourceZeroInput, "ports/input/status");
        var sourcePayload = new NandSignal[domain.LaneCount][];
        var sourceSaturated = new NandSignal[domain.LaneCount];
        var rawResults = new NandSignal[domain.LaneCount][];
        var laneValid = new NandSignal[domain.LaneCount];
        var saturationValid = new NandSignal[domain.LaneCount];
        var payloadOrSaturation = new List<NandSignal>();

        foreach (var lane in ports.Lanes)
        {
            var region = LaneRegion(lane.Lane);
            sourcePayload[lane.Lane] = lane.SourcePayloadInputs
                .Select((name, bit) => builder.Input(
                    name,
                    sourceEncoding == ExperimentalValuationEncoding.BinaryExponent
                        ? $"ports/input/lane:{lane.Lane}/exponent/bit:{bit}"
                        : $"ports/input/lane:{lane.Lane}/threshold:{bit + 1}"))
                .ToArray();
            sourceSaturated[lane.Lane] = builder.Input(
                lane.SourceSaturationInput,
                $"ports/input/lane:{lane.Lane}/status");
            payloadOrSaturation.AddRange(sourcePayload[lane.Lane]);
            payloadOrSaturation.Add(sourceSaturated[lane.Lane]);

            var inverted = sourcePayload[lane.Lane]
                .Select((signal, bit) => NandLogic.Not(
                    builder,
                    signal,
                    $"lane[{lane.Lane}].source_not[{bit}]",
                    region))
                .ToArray();
            var legalTerms = new NandSignal[lane.Cap + 1];
            for (var exponent = 0; exponent <= lane.Cap; exponent++)
            {
                var pattern = SourcePattern(
                    sourceEncoding,
                    sourcePayload[lane.Lane].Length,
                    exponent);
                legalTerms[exponent] = BuildMinterm(
                    builder,
                    sourcePayload[lane.Lane],
                    inverted,
                    pattern,
                    $"lane[{lane.Lane}].decode[{exponent}]",
                    region);
            }

            laneValid[lane.Lane] = legalTerms.Length == (1 << sourcePayload[lane.Lane].Length)
                ? one
                : OrMany(
                    builder,
                    legalTerms,
                    zero,
                    $"lane[{lane.Lane}].valid_payload",
                    region);
            var notAtCap = NandLogic.Not(
                builder,
                legalTerms[lane.Cap],
                $"lane[{lane.Lane}].not_at_cap",
                region);
            var saturationBelowCap = NandLogic.And(
                builder,
                sourceSaturated[lane.Lane],
                notAtCap,
                $"lane[{lane.Lane}].saturation_below_cap",
                region);
            saturationValid[lane.Lane] = NandLogic.Not(
                builder,
                saturationBelowCap,
                $"lane[{lane.Lane}].saturation_valid",
                region);

            rawResults[lane.Lane] = new NandSignal[lane.ResultPayloadOutputs.Count];
            for (var bit = 0; bit < rawResults[lane.Lane].Length; bit++)
            {
                var selected = Enumerable.Range(0, legalTerms.Length)
                    .Where(exponent => ResultBit(resultEncoding, exponent, bit))
                    .Select(exponent => legalTerms[exponent]);
                rawResults[lane.Lane][bit] = OrMany(
                    builder,
                    selected,
                    zero,
                    $"lane[{lane.Lane}].result_raw[{bit}]",
                    region);
            }
        }

        var allPayloadsValid = AndMany(
            builder,
            laneValid,
            "status.all_payloads_valid",
            AggregateRegion);
        var allSaturationsValid = AndMany(
            builder,
            saturationValid,
            "status.all_saturations_valid",
            AggregateRegion);
        var payloadSet = OrMany(
            builder,
            payloadOrSaturation,
            zero,
            "status.payload_set",
            AggregateRegion);
        var zeroCarriesPayload = NandLogic.And(
            builder,
            sourceZero,
            payloadSet,
            "status.zero_carries_payload",
            AggregateRegion);
        var notPayloadsValid = NandLogic.Not(
            builder,
            allPayloadsValid,
            "status.not_payloads_valid",
            AggregateRegion);
        var notSaturationsValid = NandLogic.Not(
            builder,
            allSaturationsValid,
            "status.not_saturations_valid",
            AggregateRegion);
        var malformed = OrMany(
            builder,
            [notPayloadsValid, notSaturationsValid, zeroCarriesPayload],
            zero,
            "status.malformed",
            AggregateRegion);
        var accepted = NandLogic.Not(builder, malformed, "status.accepted", AggregateRegion);
        var notZero = NandLogic.Not(builder, sourceZero, "status.not_zero", AggregateRegion);
        var emitPayload = NandLogic.And(
            builder,
            accepted,
            notZero,
            "result.emit_payload",
            AggregateRegion);
        var resultZero = NandLogic.And(
            builder,
            accepted,
            sourceZero,
            "result.zero",
            AggregateRegion);

        builder.Output(ports.ResultZeroOutput, resultZero, "ports/output/status");
        builder.Output(ports.AcceptedOutput, accepted, "ports/output/status");
        builder.Output(ports.MalformedOutput, malformed, "ports/output/status");
        foreach (var lane in ports.Lanes)
        {
            var region = LaneRegion(lane.Lane);
            for (var bit = 0; bit < lane.ResultPayloadOutputs.Count; bit++)
            {
                var output = NandLogic.And(
                    builder,
                    emitPayload,
                    rawResults[lane.Lane][bit],
                    $"lane[{lane.Lane}].result[{bit}]",
                    region);
                builder.Output(
                    lane.ResultPayloadOutputs[bit],
                    output,
                    $"ports/output/lane:{lane.Lane}/payload");
            }

            var resultSaturated = NandLogic.And(
                builder,
                emitPayload,
                sourceSaturated[lane.Lane],
                $"lane[{lane.Lane}].result_saturated",
                region);
            builder.Output(
                lane.ResultSaturationOutput,
                resultSaturated,
                $"ports/output/lane:{lane.Lane}/status");
        }

        return new DeclaredValuationEncodingAdapterCircuit(builder.Build(), ports);
    }

    private static ValuationEncodingAdapterPortLayout CreateLayout(
        ValuationHardwareDomain domain,
        ExperimentalValuationEncoding sourceEncoding,
        ExperimentalValuationEncoding resultEncoding) =>
        new(
            domain.Width,
            sourceEncoding,
            resultEncoding,
            "source.zero",
            "result.zero",
            "status.accepted",
            "status.malformed",
            Enumerable.Range(0, domain.LaneCount)
                .Select(lane => new ValuationEncodingAdapterLanePortLayout(
                    lane,
                    domain.PrimeAt(lane),
                    domain.CapAt(lane),
                    Enumerable.Range(
                            0,
                            PayloadWidth(sourceEncoding, domain.CapAt(lane)))
                        .Select(bit => PayloadName("source", sourceEncoding, lane, bit))
                        .ToArray(),
                    $"source.lane[{lane}].saturated",
                    Enumerable.Range(
                            0,
                            PayloadWidth(resultEncoding, domain.CapAt(lane)))
                        .Select(bit => PayloadName("result", resultEncoding, lane, bit))
                        .ToArray(),
                    $"result.lane[{lane}].saturated"))
                .ToArray());

    private static string PayloadName(
        string side,
        ExperimentalValuationEncoding encoding,
        int lane,
        int bit) =>
        encoding == ExperimentalValuationEncoding.BinaryExponent
            ? $"{side}.lane[{lane}].exp.bit[{bit}]"
            : $"{side}.lane[{lane}].threshold[{bit + 1}]";

    private static int PayloadWidth(ExperimentalValuationEncoding encoding, int cap) =>
        encoding == ExperimentalValuationEncoding.BinaryExponent ? BinaryWidth(cap) : cap;

    private static bool[] SourcePattern(
        ExperimentalValuationEncoding encoding,
        int payloadWidth,
        int exponent)
    {
        var pattern = new bool[payloadWidth];
        for (var bit = 0; bit < pattern.Length; bit++)
        {
            pattern[bit] = encoding == ExperimentalValuationEncoding.BinaryExponent
                ? ((exponent >> bit) & 1) != 0
                : bit < exponent;
        }

        return pattern;
    }

    private static bool ResultBit(
        ExperimentalValuationEncoding encoding,
        int exponent,
        int bit) =>
        encoding == ExperimentalValuationEncoding.BinaryExponent
            ? ((exponent >> bit) & 1) != 0
            : exponent >= bit + 1;

    private static NandSignal BuildMinterm(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> payload,
        IReadOnlyList<NandSignal> inverted,
        IReadOnlyList<bool> pattern,
        string prefix,
        string region)
    {
        var literals = new NandSignal[payload.Count];
        for (var bit = 0; bit < literals.Length; bit++)
        {
            literals[bit] = pattern[bit] ? payload[bit] : inverted[bit];
        }

        return AndMany(builder, literals, prefix, region);
    }

    private static NandSignal AndMany(
        NandNetlistBuilder builder,
        IEnumerable<NandSignal> inputs,
        string prefix,
        string region)
    {
        var stage = inputs.ToList();
        if (stage.Count == 0)
        {
            throw new ArgumentException("AND reduction needs at least one input.", nameof(inputs));
        }

        var round = 0;
        while (stage.Count > 1)
        {
            var next = new List<NandSignal>((stage.Count + 1) / 2);
            for (var index = 0; index < stage.Count; index += 2)
            {
                if (index + 1 == stage.Count)
                {
                    next.Add(stage[index]);
                }
                else
                {
                    next.Add(NandLogic.And(
                        builder,
                        stage[index],
                        stage[index + 1],
                        $"{prefix}.round[{round}].pair[{index / 2}]",
                        region));
                }
            }

            stage = next;
            round++;
        }

        return stage[0];
    }

    private static NandSignal OrMany(
        NandNetlistBuilder builder,
        IEnumerable<NandSignal> inputs,
        NandSignal zero,
        string prefix,
        string region)
    {
        var stage = inputs.ToList();
        if (stage.Count == 0)
        {
            return zero;
        }

        var round = 0;
        while (stage.Count > 1)
        {
            var next = new List<NandSignal>((stage.Count + 1) / 2);
            for (var index = 0; index < stage.Count; index += 2)
            {
                if (index + 1 == stage.Count)
                {
                    next.Add(stage[index]);
                }
                else
                {
                    next.Add(NandLogic.Or(
                        builder,
                        stage[index],
                        stage[index + 1],
                        $"{prefix}.round[{round}].pair[{index / 2}]",
                        region));
                }
            }

            stage = next;
            round++;
        }

        return stage[0];
    }

    private static int BinaryWidth(int cap)
    {
        var width = 1;
        while ((1 << width) <= cap)
        {
            width++;
        }

        return width;
    }

    private static string LaneRegion(int lane) => $"datapath/lane:{lane}";
}
