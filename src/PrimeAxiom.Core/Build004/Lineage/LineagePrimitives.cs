using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace PrimeAxiom.Core.Build004.Lineage;

public enum LineageCompleteness
{
    Exact,
    KnownLowerBound,
    Conflict,
}

public enum PayloadReplayability
{
    ReplayableExact,
    DigestOnly,
    MissingDependency,
    UnsupportedTransform,
}

public enum IssuerAuthenticity
{
    NotProvided,
    ExternalClaimNotVerified,
}

public sealed record AtomKey
{
    public AtomKey(string namespaceId, string assignmentEpoch, string occurrenceId)
    {
        NamespaceId = LineageText.RequireToken(namespaceId, nameof(namespaceId));
        AssignmentEpoch = LineageText.RequireToken(assignmentEpoch, nameof(assignmentEpoch));
        OccurrenceId = LineageText.RequireToken(occurrenceId, nameof(occurrenceId));
    }

    public string NamespaceId { get; }
    public string AssignmentEpoch { get; }
    public string OccurrenceId { get; }

    internal string Canonical => LineageText.Fields(NamespaceId, AssignmentEpoch, OccurrenceId);

    public override string ToString() => $"{NamespaceId}/{AssignmentEpoch}/{OccurrenceId}";
}

public sealed record AtomDescriptor
{
    public AtomDescriptor(AtomKey key, string sourceId, string payloadSha256)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        SourceId = LineageText.RequireToken(sourceId, nameof(sourceId));
        PayloadSha256 = LineageHash.RequireSha256(payloadSha256, nameof(payloadSha256));
    }

    public AtomKey Key { get; }
    public string SourceId { get; }
    public string PayloadSha256 { get; }

    internal string Canonical => LineageText.Fields(Key.Canonical, SourceId, PayloadSha256);
}

public sealed class ExactPositiveRational : IEquatable<ExactPositiveRational>
{
    public ExactPositiveRational(BigInteger numerator, BigInteger denominator)
    {
        if (numerator <= BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator), "A likelihood weight must be strictly positive.");
        }

        if (denominator <= BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), "A rational denominator must be strictly positive.");
        }

        var divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    public static ExactPositiveRational One { get; } = new(BigInteger.One, BigInteger.One);

    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }

    public static ExactPositiveRational operator *(
        ExactPositiveRational left,
        ExactPositiveRational right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftCross = BigInteger.GreatestCommonDivisor(left.Numerator, right.Denominator);
        var rightCross = BigInteger.GreatestCommonDivisor(right.Numerator, left.Denominator);
        return new ExactPositiveRational(
            (left.Numerator / leftCross) * (right.Numerator / rightCross),
            (left.Denominator / rightCross) * (right.Denominator / leftCross));
    }

    public static ExactPositiveRational operator /(
        ExactPositiveRational dividend,
        ExactPositiveRational divisor)
    {
        ArgumentNullException.ThrowIfNull(dividend);
        ArgumentNullException.ThrowIfNull(divisor);
        return dividend * new ExactPositiveRational(divisor.Denominator, divisor.Numerator);
    }

    public static ExactPositiveRational operator +(
        ExactPositiveRational left,
        ExactPositiveRational right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var common = BigInteger.GreatestCommonDivisor(left.Denominator, right.Denominator);
        var leftScale = right.Denominator / common;
        var rightScale = left.Denominator / common;
        return new ExactPositiveRational(
            (left.Numerator * leftScale) + (right.Numerator * rightScale),
            left.Denominator * leftScale);
    }

    public bool Equals(ExactPositiveRational? other) =>
        other is not null && Numerator == other.Numerator && Denominator == other.Denominator;

    public override bool Equals(object? obj) => Equals(obj as ExactPositiveRational);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public override string ToString() =>
        Denominator.IsOne
            ? Numerator.ToString(CultureInfo.InvariantCulture)
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{Numerator}/{Denominator}");

    internal string Canonical => LineageText.Fields(
        Numerator.ToString(CultureInfo.InvariantCulture),
        Denominator.ToString(CultureInfo.InvariantCulture));
}

public sealed record TwoStateLikelihood
{
    public TwoStateLikelihood(
        ExactPositiveRational stateZero,
        ExactPositiveRational stateOne)
    {
        StateZero = stateZero ?? throw new ArgumentNullException(nameof(stateZero));
        StateOne = stateOne ?? throw new ArgumentNullException(nameof(stateOne));
    }

    public static TwoStateLikelihood One { get; } = new(
        ExactPositiveRational.One,
        ExactPositiveRational.One);

    public ExactPositiveRational StateZero { get; }
    public ExactPositiveRational StateOne { get; }

    public ExactPositiveRational ProbabilityOfOne => StateOne / (StateZero + StateOne);

    public static TwoStateLikelihood operator *(
        TwoStateLikelihood left,
        TwoStateLikelihood right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return new TwoStateLikelihood(
            left.StateZero * right.StateZero,
            left.StateOne * right.StateOne);
    }

    public static TwoStateLikelihood operator /(
        TwoStateLikelihood dividend,
        TwoStateLikelihood divisor)
    {
        ArgumentNullException.ThrowIfNull(dividend);
        ArgumentNullException.ThrowIfNull(divisor);
        return new TwoStateLikelihood(
            dividend.StateZero / divisor.StateZero,
            dividend.StateOne / divisor.StateOne);
    }

    internal string Canonical => LineageText.Fields(StateZero.Canonical, StateOne.Canonical);
}

internal static class AtomKeyOrdering
{
    public static IComparer<AtomKey> Comparer { get; } =
        System.Collections.Generic.Comparer<AtomKey>.Create(Compare);

    public static int Compare(AtomKey? left, AtomKey? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        var result = string.CompareOrdinal(left.NamespaceId, right.NamespaceId);
        if (result != 0)
        {
            return result;
        }

        result = string.CompareOrdinal(left.AssignmentEpoch, right.AssignmentEpoch);
        return result != 0
            ? result
            : string.CompareOrdinal(left.OccurrenceId, right.OccurrenceId);
    }
}

internal static class LineageText
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static string RequireToken(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        _ = GetUtf8Bytes(value, parameterName);
        return value;
    }

    public static string Fields(params string[] values)
    {
        var builder = new StringBuilder();
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = GetUtf8Bytes(value, nameof(values));
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
        }

        return builder.ToString();
    }

    public static byte[] GetUtf8Bytes(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        try
        {
            return StrictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                "Lineage identities and canonical fields must contain well-formed UTF-16.",
                parameterName,
                exception);
        }
    }
}

internal static class LineageHash
{
    public static string Sha256(string canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        return Convert.ToHexString(SHA256.HashData(LineageText.GetUtf8Bytes(canonical, nameof(canonical))));
    }

    public static string RequireSha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A canonical SHA-256 value must contain exactly 64 hexadecimal characters.", parameterName);
        }

        return value.ToUpperInvariant();
    }
}
