using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hardware;

public enum ValuationPresenceEncoding
{
    BinaryExponent,
    Thermometer,
}

/// <summary>
/// Stable S4 lane ports for a cold magnitude encoder. Exponent bits are
/// little-endian and describe the exact valuation of each configured prime,
/// including when the remaining cofactor is unsupported by S4.
/// </summary>
public sealed record MagnitudeEncoderLanePortLayout(
    int Lane,
    int Prime,
    int Cap,
    IReadOnlyList<string> ExponentOutputs);

public sealed record MagnitudeEncoderPortLayout(
    int Width,
    IReadOnlyList<string> MagnitudeInputs,
    string ZeroOutput,
    string SupportedOutput,
    string SmoothOutput,
    IReadOnlyList<MagnitudeEncoderLanePortLayout> Lanes);

public sealed record DeclaredMagnitudeEncoderCircuit(
    NandNetlist Netlist,
    MagnitudeEncoderPortLayout Ports,
    string EvidenceClass = "STRUCTURAL_DECLARED");

/// <summary>
/// Stable S4 lane ports for structural decoding. Exponent inputs are
/// little-endian. Saturation is explicit because a lower bound cannot be
/// reconstructed as though it were an exact exponent.
/// </summary>
public sealed record MagnitudeDecoderLanePortLayout(
    int Lane,
    int Prime,
    int Cap,
    IReadOnlyList<string> ExponentInputs,
    string SaturationInput);

public sealed record MagnitudeDecoderPortLayout(
    int Width,
    string ZeroInput,
    IReadOnlyList<string> MagnitudeOutputs,
    string AcceptedOutput,
    string OverflowOutput,
    string MalformedOutput,
    IReadOnlyList<MagnitudeDecoderLanePortLayout> Lanes);

public sealed record DeclaredMagnitudeDecoderCircuit(
    NandNetlist Netlist,
    MagnitudeDecoderPortLayout Ports,
    string EvidenceClass = "STRUCTURAL_DECLARED");

public sealed record PresenceAdapterLanePortLayout(
    int Lane,
    int Prime,
    int Cap,
    IReadOnlyList<string> PayloadInputs,
    string SaturationInput,
    string PresenceOutput);

public sealed record PresenceAdapterPortLayout(
    int Width,
    ValuationPresenceEncoding Encoding,
    string ZeroInput,
    string AcceptedOutput,
    string MalformedOutput,
    IReadOnlyList<PresenceAdapterLanePortLayout> Lanes);

public sealed record DeclaredPresenceAdapterCircuit(
    NandNetlist Netlist,
    PresenceAdapterPortLayout Ports,
    string EvidenceClass = "STRUCTURAL_DECLARED");

/// <summary>
/// Declared-NAND representation adapters at the Build 002 fork. Host integer
/// arithmetic is used only while constructing fixed truth-table geometry.
/// Every runtime result is produced by the emitted acyclic NAND graph.
/// </summary>
public static class RepresentationAdapterHardware
{
    private const string AggregateRegion = "status/lane:aggregate";

    /// <summary>
    /// Builds a cold W-bit magnitude ingress circuit. The four exponent lanes
    /// are exact S4 valuations for every nonzero input. Smooth is true only for
    /// positive S4-smooth inputs; Supported additionally includes the explicit
    /// structural-zero encoding.
    /// </summary>
    public static DeclaredMagnitudeEncoderCircuit BuildMagnitudeToBinaryExponent(int width)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        var ports = CreateEncoderLayout(domain);
        var builder = new NandNetlistBuilder($"ADAPTER.MAG_TO_BINEXP.S4.W{width}");
        var zero = builder.Constant("const.zero", BitState.Off);
        var magnitude = ports.MagnitudeInputs
            .Select((name, bit) => builder.Input(name, $"ports/input/magnitude/bit:{bit}"))
            .ToArray();
        var notMagnitude = magnitude
            .Select((signal, bit) => NandLogic.Not(
                builder,
                signal,
                $"decode.input_not[{bit}]",
                "decode/magnitude"))
            .ToArray();
        var selectedValues = new NandSignal[domain.MaximumMagnitude + 1];
        for (var value = 0; value <= domain.MaximumMagnitude; value++)
        {
            selectedValues[value] = BuildMinterm(
                builder,
                magnitude,
                notMagnitude,
                value,
                $"decode.value[{value}]",
                "decode/magnitude");
        }

        var zeroSignal = selectedValues[0];
        var smoothTerms = new List<NandSignal>();
        var factorization = new (int[] Exponents, bool Smooth)[selectedValues.Length];
        for (var value = 1; value < selectedValues.Length; value++)
        {
            factorization[value] = FactorPositive(domain, value);
            if (factorization[value].Smooth)
            {
                smoothTerms.Add(selectedValues[value]);
            }
        }

        var smooth = OrMany(
            builder,
            smoothTerms,
            zero,
            "status.smooth",
            AggregateRegion);
        var supported = NandLogic.Or(
            builder,
            zeroSignal,
            smooth,
            "status.supported",
            AggregateRegion);

        builder.Output(ports.ZeroOutput, zeroSignal, "ports/output/status");
        builder.Output(ports.SmoothOutput, smooth, "ports/output/status");
        builder.Output(ports.SupportedOutput, supported, "ports/output/status");
        foreach (var lane in ports.Lanes)
        {
            for (var bit = 0; bit < lane.ExponentOutputs.Count; bit++)
            {
                var terms = Enumerable.Range(1, domain.MaximumMagnitude)
                    .Where(value => ((factorization[value].Exponents[lane.Lane] >> bit) & 1) != 0)
                    .Select(value => selectedValues[value]);
                var result = OrMany(
                    builder,
                    terms,
                    zero,
                    $"lane[{lane.Lane}].exp.bit[{bit}]",
                    LaneRegion(lane.Lane));
                builder.Output(
                    lane.ExponentOutputs[bit],
                    result,
                    $"ports/output/lane:{lane.Lane}/exponent");
            }
        }

        return new DeclaredMagnitudeEncoderCircuit(builder.Build(), ports);
    }

    /// <summary>
    /// Builds exact S4 reconstruction. Saturated lanes, payloads above a lane
    /// cap, and zero tags carrying a nonzero payload are Malformed. A legal
    /// product outside W bits is Overflow. These statuses are mutually
    /// exclusive and every rejected magnitude output is cleared atomically.
    /// </summary>
    public static DeclaredMagnitudeDecoderCircuit BuildBinaryExponentToMagnitude(int width)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        var ports = CreateDecoderLayout(domain);
        var builder = new NandNetlistBuilder($"ADAPTER.BINEXP_TO_MAG.S4.W{width}");
        var zero = builder.Constant("const.zero", BitState.Off);
        var sourceZero = builder.Input(ports.ZeroInput, "ports/input/status");
        var payload = new NandSignal[ports.Lanes.Count][];
        var saturation = new NandSignal[ports.Lanes.Count];
        var flatPayload = new List<NandSignal>();
        foreach (var lane in ports.Lanes)
        {
            payload[lane.Lane] = lane.ExponentInputs
                .Select((name, bit) => builder.Input(
                    name,
                    $"ports/input/lane:{lane.Lane}/exponent/bit:{bit}"))
                .ToArray();
            flatPayload.AddRange(payload[lane.Lane]);
            saturation[lane.Lane] = builder.Input(
                lane.SaturationInput,
                $"ports/input/lane:{lane.Lane}/status");
        }

        var flat = flatPayload.ToArray();
        var notFlat = flat
            .Select((signal, bit) => NandLogic.Not(
                builder,
                signal,
                $"decode.payload_not[{bit}]",
                "decode/payload"))
            .ToArray();
        var terms = new List<DecodedVector>();
        var ordinal = 0;
        foreach (var exponents in ExponentVectors(domain))
        {
            var packed = PackExponents(ports.Lanes, exponents);
            var selected = BuildMinterm(
                builder,
                flat,
                notFlat,
                packed,
                $"decode.vector[{ordinal}]",
                "decode/payload");
            terms.Add(new DecodedVector(selected, Product(domain, exponents)));
            ordinal++;
        }

        var anyLegal = OrMany(
            builder,
            terms.Select(term => term.Selected),
            zero,
            "decode.any_legal",
            AggregateRegion);
        var overCap = NandLogic.Not(builder, anyLegal, "status.over_cap", AggregateRegion);
        var anySaturated = OrMany(
            builder,
            saturation,
            zero,
            "status.any_saturated",
            AggregateRegion);
        var payloadNonzero = OrMany(
            builder,
            flat,
            zero,
            "status.payload_nonzero",
            AggregateRegion);
        var noncanonicalZero = NandLogic.And(
            builder,
            sourceZero,
            payloadNonzero,
            "status.noncanonical_zero",
            AggregateRegion);
        var malformed = OrMany(
            builder,
            [anySaturated, overCap, noncanonicalZero],
            zero,
            "status.malformed",
            AggregateRegion);
        var rawOverflow = OrMany(
            builder,
            terms.Where(term => term.Product > domain.MaximumMagnitude)
                .Select(term => term.Selected),
            zero,
            "status.raw_overflow",
            AggregateRegion);
        var notMalformed = NandLogic.Not(
            builder,
            malformed,
            "status.not_malformed",
            AggregateRegion);
        var notZero = NandLogic.Not(builder, sourceZero, "status.not_zero", AggregateRegion);
        var exactNonzero = NandLogic.And(
            builder,
            notMalformed,
            notZero,
            "status.exact_nonzero",
            AggregateRegion);
        var overflow = NandLogic.And(
            builder,
            exactNonzero,
            rawOverflow,
            "status.overflow",
            AggregateRegion);
        var notOverflow = NandLogic.Not(
            builder,
            overflow,
            "status.not_overflow",
            AggregateRegion);
        var accepted = NandLogic.And(
            builder,
            notMalformed,
            notOverflow,
            "status.accepted",
            AggregateRegion);
        var emitMagnitude = NandLogic.And(
            builder,
            accepted,
            notZero,
            "result.emit_magnitude",
            AggregateRegion);

        for (var bit = 0; bit < ports.MagnitudeOutputs.Count; bit++)
        {
            var selected = OrMany(
                builder,
                terms.Where(term => ((term.Product >> bit) & 1) != 0)
                    .Select(term => term.Selected),
                zero,
                $"result.magnitude.raw.bit[{bit}]",
                $"result/magnitude/bit:{bit}");
            var output = NandLogic.And(
                builder,
                selected,
                emitMagnitude,
                $"result.magnitude.bit[{bit}]",
                $"result/magnitude/bit:{bit}");
            builder.Output(
                ports.MagnitudeOutputs[bit],
                output,
                $"ports/output/magnitude/bit:{bit}");
        }

        builder.Output(ports.AcceptedOutput, accepted, "ports/output/status");
        builder.Output(ports.OverflowOutput, overflow, "ports/output/status");
        builder.Output(ports.MalformedOutput, malformed, "ports/output/status");
        return new DeclaredMagnitudeDecoderCircuit(builder.Build(), ports);
    }

    /// <summary>
    /// Builds a binary-exponent to S4-presence projection. Exactness and
    /// canonicality are checked; rejected presence outputs are cleared.
    /// </summary>
    public static DeclaredPresenceAdapterCircuit BuildBinaryExponentPresence(int width)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        var ports = CreatePresenceLayout(domain, ValuationPresenceEncoding.BinaryExponent);
        var builder = new NandNetlistBuilder($"ADAPTER.BINEXP_TO_PRESENCE.S4.W{width}");
        var zero = builder.Constant("const.zero", BitState.Off);
        var sourceZero = builder.Input(ports.ZeroInput, "ports/input/status");
        var payload = new NandSignal[ports.Lanes.Count][];
        var saturation = new NandSignal[ports.Lanes.Count];
        foreach (var lane in ports.Lanes)
        {
            payload[lane.Lane] = lane.PayloadInputs
                .Select((name, bit) => builder.Input(
                    name,
                    $"ports/input/lane:{lane.Lane}/exponent/bit:{bit}"))
                .ToArray();
            saturation[lane.Lane] = builder.Input(
                lane.SaturationInput,
                $"ports/input/lane:{lane.Lane}/status");
        }

        var laneValid = new NandSignal[ports.Lanes.Count];
        var rawPresence = new NandSignal[ports.Lanes.Count];
        for (var laneIndex = 0; laneIndex < ports.Lanes.Count; laneIndex++)
        {
            var lane = ports.Lanes[laneIndex];
            var region = LaneRegion(laneIndex);
            var notPayload = payload[laneIndex]
                .Select((signal, bit) => NandLogic.Not(
                    builder,
                    signal,
                    $"lane[{laneIndex}].not[{bit}]",
                    region))
                .ToArray();
            var legalTerms = Enumerable.Range(0, lane.Cap + 1)
                .Select(value => BuildMinterm(
                    builder,
                    payload[laneIndex],
                    notPayload,
                    value,
                    $"lane[{laneIndex}].legal[{value}]",
                    region));
            laneValid[laneIndex] = OrMany(
                builder,
                legalTerms,
                zero,
                $"lane[{laneIndex}].valid",
                region);
            rawPresence[laneIndex] = OrMany(
                builder,
                payload[laneIndex],
                zero,
                $"lane[{laneIndex}].present_raw",
                region);
        }

        EmitPresenceOutputs(
            builder,
            ports,
            sourceZero,
            payload.SelectMany(bits => bits),
            saturation,
            laneValid,
            rawPresence,
            zero);
        return new DeclaredPresenceAdapterCircuit(builder.Build(), ports);
    }

    /// <summary>
    /// Builds a canonical thermometer-to-S4-presence projection. Each lane is
    /// checked for a true prefix followed by a false suffix.
    /// </summary>
    public static DeclaredPresenceAdapterCircuit BuildThermometerPresence(int width)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        var ports = CreatePresenceLayout(domain, ValuationPresenceEncoding.Thermometer);
        var builder = new NandNetlistBuilder($"ADAPTER.THERM_TO_PRESENCE.S4.W{width}");
        var zero = builder.Constant("const.zero", BitState.Off);
        var one = builder.Constant("const.one", BitState.On);
        var sourceZero = builder.Input(ports.ZeroInput, "ports/input/status");
        var payload = new NandSignal[ports.Lanes.Count][];
        var saturation = new NandSignal[ports.Lanes.Count];
        var laneValid = new NandSignal[ports.Lanes.Count];
        var rawPresence = new NandSignal[ports.Lanes.Count];
        foreach (var lane in ports.Lanes)
        {
            var region = LaneRegion(lane.Lane);
            payload[lane.Lane] = lane.PayloadInputs
                .Select((name, threshold) => builder.Input(
                    name,
                    $"ports/input/lane:{lane.Lane}/threshold:{threshold + 1}"))
                .ToArray();
            saturation[lane.Lane] = builder.Input(
                lane.SaturationInput,
                $"ports/input/lane:{lane.Lane}/status");
            var violations = new List<NandSignal>();
            for (var threshold = 1; threshold < payload[lane.Lane].Length; threshold++)
            {
                var notPrevious = NandLogic.Not(
                    builder,
                    payload[lane.Lane][threshold - 1],
                    $"lane[{lane.Lane}].threshold[{threshold + 1}].not_previous",
                    region);
                violations.Add(NandLogic.And(
                    builder,
                    payload[lane.Lane][threshold],
                    notPrevious,
                    $"lane[{lane.Lane}].threshold[{threshold + 1}].violation",
                    region));
            }

            var anyViolation = OrMany(
                builder,
                violations,
                zero,
                $"lane[{lane.Lane}].any_violation",
                region);
            laneValid[lane.Lane] = violations.Count == 0
                ? one
                : NandLogic.Not(
                    builder,
                    anyViolation,
                    $"lane[{lane.Lane}].valid",
                    region);
            rawPresence[lane.Lane] = payload[lane.Lane][0];
        }

        EmitPresenceOutputs(
            builder,
            ports,
            sourceZero,
            payload.SelectMany(bits => bits),
            saturation,
            laneValid,
            rawPresence,
            zero);
        return new DeclaredPresenceAdapterCircuit(builder.Build(), ports);
    }

    private static void EmitPresenceOutputs(
        NandNetlistBuilder builder,
        PresenceAdapterPortLayout ports,
        NandSignal sourceZero,
        IEnumerable<NandSignal> payload,
        IReadOnlyList<NandSignal> saturation,
        IReadOnlyList<NandSignal> laneValid,
        IReadOnlyList<NandSignal> rawPresence,
        NandSignal zero)
    {
        var payloadNonzero = OrMany(
            builder,
            payload,
            zero,
            "status.payload_nonzero",
            AggregateRegion);
        var noncanonicalZero = NandLogic.And(
            builder,
            sourceZero,
            payloadNonzero,
            "status.noncanonical_zero",
            AggregateRegion);
        var anySaturated = OrMany(
            builder,
            saturation,
            zero,
            "status.any_saturated",
            AggregateRegion);
        var allLanesValid = AndMany(
            builder,
            laneValid,
            "status.all_lanes_valid",
            AggregateRegion);
        var invalidPayload = NandLogic.Not(
            builder,
            allLanesValid,
            "status.invalid_payload",
            AggregateRegion);
        var malformed = OrMany(
            builder,
            [anySaturated, invalidPayload, noncanonicalZero],
            zero,
            "status.malformed",
            AggregateRegion);
        var accepted = NandLogic.Not(builder, malformed, "status.accepted", AggregateRegion);
        var notZero = NandLogic.Not(builder, sourceZero, "status.not_zero", AggregateRegion);
        var emit = NandLogic.And(
            builder,
            accepted,
            notZero,
            "result.emit",
            AggregateRegion);
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var presence = NandLogic.And(
                builder,
                emit,
                rawPresence[lane],
                $"lane[{lane}].present",
                LaneRegion(lane));
            builder.Output(
                ports.Lanes[lane].PresenceOutput,
                presence,
                $"ports/output/lane:{lane}/presence");
        }

        builder.Output(ports.AcceptedOutput, accepted, "ports/output/status");
        builder.Output(ports.MalformedOutput, malformed, "ports/output/status");
    }

    private static MagnitudeEncoderPortLayout CreateEncoderLayout(ValuationHardwareDomain domain) =>
        new(
            domain.Width,
            Enumerable.Range(0, domain.Width)
                .Select(bit => $"magnitude.bit[{bit}]")
                .ToArray(),
            "result.zero",
            "status.supported",
            "status.smooth",
            Enumerable.Range(0, domain.LaneCount)
                .Select(lane => new MagnitudeEncoderLanePortLayout(
                    lane,
                    domain.PrimeAt(lane),
                    domain.CapAt(lane),
                    Enumerable.Range(0, BinaryWidth(domain.CapAt(lane)))
                        .Select(bit => $"result.lane[{lane}].exp.bit[{bit}]")
                        .ToArray()))
                .ToArray());

    private static MagnitudeDecoderPortLayout CreateDecoderLayout(ValuationHardwareDomain domain) =>
        new(
            domain.Width,
            "source.zero",
            Enumerable.Range(0, domain.Width)
                .Select(bit => $"result.magnitude.bit[{bit}]")
                .ToArray(),
            "status.accepted",
            "status.overflow",
            "status.malformed",
            Enumerable.Range(0, domain.LaneCount)
                .Select(lane => new MagnitudeDecoderLanePortLayout(
                    lane,
                    domain.PrimeAt(lane),
                    domain.CapAt(lane),
                    Enumerable.Range(0, BinaryWidth(domain.CapAt(lane)))
                        .Select(bit => $"source.lane[{lane}].exp.bit[{bit}]")
                        .ToArray(),
                    $"source.lane[{lane}].saturated"))
                .ToArray());

    private static PresenceAdapterPortLayout CreatePresenceLayout(
        ValuationHardwareDomain domain,
        ValuationPresenceEncoding encoding) =>
        new(
            domain.Width,
            encoding,
            "source.zero",
            "status.accepted",
            "status.malformed",
            Enumerable.Range(0, domain.LaneCount)
                .Select(lane => new PresenceAdapterLanePortLayout(
                    lane,
                    domain.PrimeAt(lane),
                    domain.CapAt(lane),
                    Enumerable.Range(
                            0,
                            encoding == ValuationPresenceEncoding.BinaryExponent
                                ? BinaryWidth(domain.CapAt(lane))
                                : domain.CapAt(lane))
                        .Select(bit => encoding == ValuationPresenceEncoding.BinaryExponent
                            ? $"source.lane[{lane}].exp.bit[{bit}]"
                            : $"source.lane[{lane}].threshold[{bit + 1}]")
                        .ToArray(),
                    $"source.lane[{lane}].saturated",
                    $"result.lane[{lane}].present"))
                .ToArray());

    private static (int[] Exponents, bool Smooth) FactorPositive(
        ValuationHardwareDomain domain,
        int value)
    {
        var remainder = value;
        var exponents = new int[domain.LaneCount];
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            var prime = domain.PrimeAt(lane);
            while (remainder % prime == 0)
            {
                exponents[lane]++;
                remainder /= prime;
            }
        }

        return (exponents, remainder == 1);
    }

    private static IEnumerable<int[]> ExponentVectors(ValuationHardwareDomain domain)
    {
        var current = new int[domain.LaneCount];
        return Visit(0);

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
                foreach (var vector in Visit(lane + 1))
                {
                    yield return vector;
                }
            }
        }
    }

    private static long Product(ValuationHardwareDomain domain, IReadOnlyList<int> exponents)
    {
        var product = 1L;
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            for (var exponent = 0; exponent < exponents[lane]; exponent++)
            {
                product *= domain.PrimeAt(lane);
            }
        }

        return product;
    }

    private static int PackExponents(
        IReadOnlyList<MagnitudeDecoderLanePortLayout> lanes,
        IReadOnlyList<int> exponents)
    {
        var packed = 0;
        var offset = 0;
        for (var lane = 0; lane < lanes.Count; lane++)
        {
            packed |= exponents[lane] << offset;
            offset += lanes[lane].ExponentInputs.Count;
        }

        return packed;
    }

    private static NandSignal BuildMinterm(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> bits,
        IReadOnlyList<NandSignal> notBits,
        int value,
        string prefix,
        string region)
    {
        var literals = new NandSignal[bits.Count];
        for (var bit = 0; bit < bits.Count; bit++)
        {
            literals[bit] = ((value >> bit) & 1) != 0 ? bits[bit] : notBits[bit];
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

    private sealed record DecodedVector(NandSignal Selected, long Product);
}
