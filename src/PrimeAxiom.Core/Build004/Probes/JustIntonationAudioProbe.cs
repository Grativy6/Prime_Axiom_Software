using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PrimeAxiom.Core.Build004.Probes;

/// <summary>
/// A bounded, exact rational interval receipt. Prime-coordinate music notation
/// is established prior art; this type only exposes the exact-to-PCM boundary.
/// </summary>
public sealed class ProbeJustIntervalReceipt
{
    private ProbeJustIntervalReceipt(
        string intervalId,
        string derivationReceiptId,
        IReadOnlyList<string> parentDerivationReceiptIds,
        ProbeExactRatio ratio,
        ProbeSignedPrimeCoordinates coordinates)
    {
        if (string.IsNullOrWhiteSpace(intervalId))
        {
            throw new ArgumentException("An interval identity is required.", nameof(intervalId));
        }

        if (string.IsNullOrWhiteSpace(derivationReceiptId))
        {
            throw new ArgumentException("A derivation identity is required.", nameof(derivationReceiptId));
        }

        if (ratio.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ratio), "A frequency interval must be positive.");
        }

        if (!coordinates.ToRatio().Equals(ratio))
        {
            throw new ArgumentException("Prime coordinates do not reconstruct the exact ratio.", nameof(coordinates));
        }

        IntervalId = intervalId;
        DerivationReceiptId = derivationReceiptId;
        ParentDerivationReceiptIds = Array.AsReadOnly(parentDerivationReceiptIds.ToArray());
        Ratio = ratio;
        Coordinates = coordinates;
    }

    public string IntervalId { get; }

    public string DerivationReceiptId { get; }

    public IReadOnlyList<string> ParentDerivationReceiptIds { get; }

    public ProbeExactRatio Ratio { get; }

    public ProbeSignedPrimeCoordinates Coordinates { get; }

    public static ProbeJustIntervalReceipt FromRatio(
        string intervalId,
        string derivationReceiptId,
        ProbeExactRatio ratio) =>
        new(
            intervalId,
            derivationReceiptId,
            Array.Empty<string>(),
            ratio,
            ProbeSignedPrimeCoordinates.FromRatio(ratio));

    public ProbeJustIntervalReceipt Compose(
        ProbeJustIntervalReceipt other,
        string intervalId,
        string derivationReceiptId)
    {
        ArgumentNullException.ThrowIfNull(other);
        var ratio = Ratio.Multiply(other.Ratio);
        var coordinates = Coordinates.Compose(other.Coordinates);
        return new ProbeJustIntervalReceipt(
            intervalId,
            derivationReceiptId,
            new[] { DerivationReceiptId, other.DerivationReceiptId },
            ratio,
            coordinates);
    }

    public ProbeJustIntervalReceipt Invert(string intervalId, string derivationReceiptId) =>
        new(
            intervalId,
            derivationReceiptId,
            new[] { DerivationReceiptId },
            Ratio.Invert(),
            Coordinates.Invert());

    public ProbeExactRatio NominalFrequency(ProbeExactRatio baseFrequencyHertz)
    {
        ArgumentNullException.ThrowIfNull(baseFrequencyHertz);
        if (baseFrequencyHertz.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseFrequencyHertz), "Base frequency must be positive.");
        }

        return baseFrequencyHertz.Multiply(Ratio);
    }

    public ProbeOctaveProjection ProjectToOctave()
    {
        var normalized = Ratio;
        var appliedPowerOfTwo = 0;
        var two = new ProbeExactRatio(2, 1);
        while (normalized.CompareTo(ProbeExactRatio.One) < 0)
        {
            normalized = normalized.Multiply(two);
            appliedPowerOfTwo = checked(appliedPowerOfTwo + 1);
        }

        while (normalized.CompareTo(two) >= 0)
        {
            normalized = normalized.Divide(two);
            appliedPowerOfTwo = checked(appliedPowerOfTwo - 1);
        }

        return new ProbeOctaveProjection(Ratio, normalized, appliedPowerOfTwo);
    }
}

public sealed record ProbeOctaveProjection(
    ProbeExactRatio OriginalRatio,
    ProbeExactRatio PitchClassRatio,
    int AppliedPowerOfTwo);

public enum ProbePcmRoundingPolicy
{
    NearestAwayFromZero,
}

public enum ProbePcmClippingPolicy
{
    Saturate,
}

/// <summary>
/// User-level approximation policy choices are explicit fields. The runtime's
/// floating-point and trigonometric implementation remains part of the recorded
/// verifier environment for byte-level replay.
/// </summary>
public sealed record ProbeAudioApproximationPolicy
{
    public ProbeAudioApproximationPolicy(
        int sampleRate,
        int sampleCount,
        double phaseRadians,
        double peakAmplitude,
        int linearAttackSamples,
        int linearReleaseSamples,
        ProbePcmRoundingPolicy roundingPolicy = ProbePcmRoundingPolicy.NearestAwayFromZero,
        ProbePcmClippingPolicy clippingPolicy = ProbePcmClippingPolicy.Saturate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCount);
        if (!double.IsFinite(phaseRadians))
        {
            throw new ArgumentOutOfRangeException(nameof(phaseRadians));
        }

        if (!double.IsFinite(peakAmplitude) || peakAmplitude < 0 || peakAmplitude > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(peakAmplitude), "PCM peak amplitude must be in [0,1].");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(linearAttackSamples);
        ArgumentOutOfRangeException.ThrowIfNegative(linearReleaseSamples);
        if (checked(linearAttackSamples + linearReleaseSamples) > sampleCount)
        {
            throw new ArgumentException("Attack and release regions cannot exceed the rendered sample count.");
        }

        SampleRate = sampleRate;
        SampleCount = sampleCount;
        PhaseRadians = phaseRadians;
        PeakAmplitude = peakAmplitude;
        LinearAttackSamples = linearAttackSamples;
        LinearReleaseSamples = linearReleaseSamples;
        RoundingPolicy = roundingPolicy;
        ClippingPolicy = clippingPolicy;
    }

    public int SampleRate { get; }

    public int SampleCount { get; }

    public double PhaseRadians { get; }

    public double PeakAmplitude { get; }

    public int LinearAttackSamples { get; }

    public int LinearReleaseSamples { get; }

    public ProbePcmRoundingPolicy RoundingPolicy { get; }

    public ProbePcmClippingPolicy ClippingPolicy { get; }

    public ProbeExactRatio ExactDurationSeconds => new(SampleCount, SampleRate);

    public string ToCanonicalString() => string.Join(
        '|',
        $"sample_rate={SampleRate}",
        $"sample_count={SampleCount}",
        $"phase_radians={PhaseRadians.ToString("R", CultureInfo.InvariantCulture)}",
        $"peak_amplitude={PeakAmplitude.ToString("R", CultureInfo.InvariantCulture)}",
        $"attack_samples={LinearAttackSamples}",
        $"release_samples={LinearReleaseSamples}",
        $"envelope=LINEAR_EDGE_SAMPLES",
        $"rounding={RoundingPolicy}",
        $"clipping={ClippingPolicy}",
        "format=PCM16_LE_MONO");
}

public sealed class ProbePcmWaveReceipt
{
    public const string ApproximationDeclaration =
        "EXACT_RATIONAL_FREQUENCY__BINARY64_PHASE__SINE__LINEAR_ENVELOPE__PCM16_LE";

    private readonly byte[] _wavBytes;

    internal ProbePcmWaveReceipt(
        string renderReceiptId,
        string sourceIntervalId,
        string sourceDerivationReceiptId,
        ProbeExactRatio requestedRatio,
        ProbeExactRatio baseFrequencyHertz,
        ProbeExactRatio nominalFrequencyHertz,
        double renderedFrequencyHertz,
        ProbeAudioApproximationPolicy policy,
        int clippedSampleCount,
        byte[] wavBytes)
    {
        RenderReceiptId = renderReceiptId;
        SourceIntervalId = sourceIntervalId;
        SourceDerivationReceiptId = sourceDerivationReceiptId;
        RequestedRatio = requestedRatio;
        BaseFrequencyHertz = baseFrequencyHertz;
        NominalFrequencyHertz = nominalFrequencyHertz;
        RenderedFrequencyHertz = renderedFrequencyHertz;
        Policy = policy;
        ClippedSampleCount = clippedSampleCount;
        _wavBytes = (byte[])wavBytes.Clone();
        WavSha256 = Convert.ToHexString(SHA256.HashData(_wavBytes));
    }

    public string RenderReceiptId { get; }

    public string SourceIntervalId { get; }

    public string SourceDerivationReceiptId { get; }

    public ProbeExactRatio RequestedRatio { get; }

    public ProbeExactRatio BaseFrequencyHertz { get; }

    public ProbeExactRatio NominalFrequencyHertz { get; }

    public double RenderedFrequencyHertz { get; }

    public ProbeAudioApproximationPolicy Policy { get; }

    public int ClippedSampleCount { get; }

    public int WavByteLength => _wavBytes.Length;

    public string WavSha256 { get; }

    public byte[] GetWavBytes() => (byte[])_wavBytes.Clone();
}

public static class ProbePcmWaveRenderer
{
    private const int HeaderLength = 44;
    private const int BytesPerSample = 2;

    public static ProbePcmWaveReceipt RenderSine(
        string renderReceiptId,
        ProbeJustIntervalReceipt interval,
        ProbeExactRatio baseFrequencyHertz,
        ProbeAudioApproximationPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(renderReceiptId))
        {
            throw new ArgumentException("A render receipt identity is required.", nameof(renderReceiptId));
        }

        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(baseFrequencyHertz);
        ArgumentNullException.ThrowIfNull(policy);

        var exactFrequency = interval.NominalFrequency(baseFrequencyHertz);
        var frequency = exactFrequency.ToDouble();
        if (frequency <= 0 || frequency >= policy.SampleRate / 2d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                "The nominal frequency must be positive and strictly below the declared Nyquist frequency.");
        }

        var dataLength = checked(policy.SampleCount * BytesPerSample);
        var wav = new byte[checked(HeaderLength + dataLength)];
        WriteWaveHeader(wav, policy.SampleRate, dataLength);

        var phaseIncrement = Math.Tau * frequency / policy.SampleRate;
        var clipped = 0;
        for (var index = 0; index < policy.SampleCount; index++)
        {
            var envelope = EnvelopeAt(index, policy);
            var sample = policy.PeakAmplitude * envelope * Math.Sin(policy.PhaseRadians + phaseIncrement * index);
            var scaled = sample * short.MaxValue;
            var rounded = policy.RoundingPolicy switch
            {
                ProbePcmRoundingPolicy.NearestAwayFromZero =>
                    Math.Round(scaled, MidpointRounding.AwayFromZero),
                _ => throw new InvalidOperationException("Unsupported PCM rounding policy."),
            };

            if (rounded > short.MaxValue || rounded < short.MinValue)
            {
                clipped++;
            }

            var sampleValue = policy.ClippingPolicy switch
            {
                ProbePcmClippingPolicy.Saturate =>
                    checked((short)Math.Clamp(rounded, short.MinValue, short.MaxValue)),
                _ => throw new InvalidOperationException("Unsupported PCM clipping policy."),
            };

            BinaryPrimitives.WriteInt16LittleEndian(
                wav.AsSpan(HeaderLength + index * BytesPerSample, BytesPerSample),
                sampleValue);
        }

        return new ProbePcmWaveReceipt(
            renderReceiptId,
            interval.IntervalId,
            interval.DerivationReceiptId,
            interval.Ratio,
            baseFrequencyHertz,
            exactFrequency,
            frequency,
            policy,
            clipped,
            wav);
    }

    private static double EnvelopeAt(int index, ProbeAudioApproximationPolicy policy)
    {
        var envelope = 1d;
        if (policy.LinearAttackSamples > 0 && index < policy.LinearAttackSamples)
        {
            envelope = Math.Min(envelope, (index + 1d) / policy.LinearAttackSamples);
        }

        var samplesRemaining = policy.SampleCount - index;
        if (policy.LinearReleaseSamples > 0 && samplesRemaining <= policy.LinearReleaseSamples)
        {
            envelope = Math.Min(envelope, samplesRemaining / (double)policy.LinearReleaseSamples);
        }

        return envelope;
    }

    private static void WriteWaveHeader(Span<byte> destination, int sampleRate, int dataLength)
    {
        WriteAscii(destination[0..4], "RIFF");
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..8], checked(36 + dataLength));
        WriteAscii(destination[8..12], "WAVE");
        WriteAscii(destination[12..16], "fmt ");
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..20], 16);
        BinaryPrimitives.WriteInt16LittleEndian(destination[20..22], 1);
        BinaryPrimitives.WriteInt16LittleEndian(destination[22..24], 1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[24..28], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(destination[28..32], checked(sampleRate * BytesPerSample));
        BinaryPrimitives.WriteInt16LittleEndian(destination[32..34], BytesPerSample);
        BinaryPrimitives.WriteInt16LittleEndian(destination[34..36], 16);
        WriteAscii(destination[36..40], "data");
        BinaryPrimitives.WriteInt32LittleEndian(destination[40..44], dataLength);
    }

    private static void WriteAscii(Span<byte> destination, string value)
    {
        var written = Encoding.ASCII.GetBytes(value, destination);
        if (written != destination.Length)
        {
            throw new InvalidOperationException("Invalid WAV chunk identifier.");
        }
    }
}
