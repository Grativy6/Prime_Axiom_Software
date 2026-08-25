using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using PrimeAxiom.Core.Build004.Lineage;

namespace PrimeAxiom.Core.Build004.Probes;

/// <summary>
/// A normalized exact rational used only by the bounded Build 004 boundary probes.
/// It is a semantic value, not a claim about gate-level execution or measurement truth.
/// </summary>
public sealed class ProbeExactRatio : IEquatable<ProbeExactRatio>, IComparable<ProbeExactRatio>
{
    public static ProbeExactRatio Zero { get; } = new(BigInteger.Zero, BigInteger.One);
    public static ProbeExactRatio One { get; } = new(BigInteger.One, BigInteger.One);

    public ProbeExactRatio(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException("An exact ratio requires a nonzero denominator.");
        }

        if (denominator.Sign < 0)
        {
            numerator = BigInteger.Negate(numerator);
            denominator = BigInteger.Negate(denominator);
        }

        if (numerator.IsZero)
        {
            Numerator = BigInteger.Zero;
            Denominator = BigInteger.One;
            return;
        }

        var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    public BigInteger Numerator { get; }

    public BigInteger Denominator { get; }

    public int Sign => Numerator.Sign;

    public ProbeExactRatio Multiply(ProbeExactRatio other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Cross-cancel before multiplication so the ordinary exact control is not a
        // deliberately inflated numerator/denominator baseline.
        var leftNumerator = Numerator;
        var leftDenominator = Denominator;
        var rightNumerator = other.Numerator;
        var rightDenominator = other.Denominator;

        var first = BigInteger.GreatestCommonDivisor(BigInteger.Abs(leftNumerator), rightDenominator);
        leftNumerator /= first;
        rightDenominator /= first;

        var second = BigInteger.GreatestCommonDivisor(BigInteger.Abs(rightNumerator), leftDenominator);
        rightNumerator /= second;
        leftDenominator /= second;

        return new ProbeExactRatio(
            checked(leftNumerator * rightNumerator),
            checked(leftDenominator * rightDenominator));
    }

    public ProbeExactRatio Divide(ProbeExactRatio other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Numerator.IsZero)
        {
            throw new DivideByZeroException("Cannot divide by a zero exact ratio.");
        }

        return Multiply(new ProbeExactRatio(other.Denominator, other.Numerator));
    }

    public ProbeExactRatio Add(ProbeExactRatio other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var common = BigInteger.GreatestCommonDivisor(Denominator, other.Denominator);
        var leftScale = other.Denominator / common;
        var rightScale = Denominator / common;
        return new ProbeExactRatio(
            checked(Numerator * leftScale + other.Numerator * rightScale),
            checked(Denominator * leftScale));
    }

    public ProbeExactRatio Invert()
    {
        if (Numerator.IsZero)
        {
            throw new DivideByZeroException("Zero has no multiplicative inverse.");
        }

        return new ProbeExactRatio(Denominator, Numerator);
    }

    public double ToDouble()
    {
        var value = (double)Numerator / (double)Denominator;
        if (!double.IsFinite(value))
        {
            throw new OverflowException("The exact ratio cannot be represented as a finite Double.");
        }

        return value;
    }

    public string ToCanonicalString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Numerator}/{Denominator}");

    public int CompareTo(ProbeExactRatio? other)
    {
        if (other is null)
        {
            return 1;
        }

        return (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
    }

    public bool Equals(ProbeExactRatio? other) =>
        other is not null && Numerator == other.Numerator && Denominator == other.Denominator;

    public override bool Equals(object? obj) => obj is ProbeExactRatio other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public static bool operator ==(ProbeExactRatio? left, ProbeExactRatio? right) =>
        Equals(left, right);

    public static bool operator !=(ProbeExactRatio? left, ProbeExactRatio? right) =>
        !Equals(left, right);

    public static bool operator <(ProbeExactRatio left, ProbeExactRatio right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(ProbeExactRatio left, ProbeExactRatio right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(ProbeExactRatio left, ProbeExactRatio right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(ProbeExactRatio left, ProbeExactRatio right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) >= 0;
    }

    public override string ToString() => ToCanonicalString();
}

/// <summary>
/// Signed rational prime coordinates. The sign and denominator exponents are
/// explicit; zero is deliberately outside this multiplicative representation.
/// </summary>
public sealed class ProbeSignedPrimeCoordinates : IEquatable<ProbeSignedPrimeCoordinates>
{
    public const int DefaultMaximumTrialDivisor = 1_000_000;
    public const string DefaultBasisId =
        "PROBE_SIGNED_RATIONAL_PRIME_BASIS__ALL_POSITIVE_PRIMES__V1";

    public static ProjectionContract Contract { get; } = new(
        "SIGNED_PRIME_COORDINATE_NUMERIC_FACTOR_PROJECTION",
        "Nonzero exact rational sign and prime exponents under the declared basis; Exact plus ReplayableExact declarations reconstruct the numeric coefficient.",
        "Unit dimensions, derivation topology, source occurrence identity, calibration evidence, authenticity, authority, and physical realization.",
        "PayloadReplayability concerns the projected numeric coefficient only; it does not claim that a measurement, derivation, or evidence envelope remains replayable.");

    private readonly ReadOnlyDictionary<BigInteger, int> _exponents;

    private ProbeSignedPrimeCoordinates(
        int sign,
        IDictionary<BigInteger, int> exponents,
        string basisId,
        LineageCompleteness completeness,
        PayloadReplayability payloadReplayability)
    {
        if (sign is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(sign), "A nonzero multiplicative value has sign -1 or 1.");
        }

        BasisId = ProbeProjectionKnowledge.RequireBasisId(basisId, nameof(basisId));
        Completeness = completeness;
        PayloadReplayability = payloadReplayability;
        Sign = sign;
        var normalized = new SortedDictionary<BigInteger, int>();
        foreach (var (prime, exponent) in exponents)
        {
            if (prime < 2 || !ProbePrimeMath.IsPrime(prime))
            {
                throw new ArgumentException($"Coordinate {prime} is not a prime.", nameof(exponents));
            }

            if (exponent != 0)
            {
                normalized.Add(prime, exponent);
            }
        }

        _exponents = new ReadOnlyDictionary<BigInteger, int>(normalized);
    }

    public int Sign { get; }

    public string BasisId { get; }

    public LineageCompleteness Completeness { get; }

    public PayloadReplayability PayloadReplayability { get; }

    public IReadOnlyDictionary<BigInteger, int> Exponents => _exponents;

    public static ProbeSignedPrimeCoordinates FromRatio(
        ProbeExactRatio ratio,
        int maximumTrialDivisor = DefaultMaximumTrialDivisor,
        string basisId = DefaultBasisId,
        LineageCompleteness completeness = LineageCompleteness.Exact,
        PayloadReplayability payloadReplayability = PayloadReplayability.ReplayableExact)
    {
        ArgumentNullException.ThrowIfNull(ratio);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTrialDivisor);
        if (ratio.Numerator.IsZero)
        {
            throw new ArgumentOutOfRangeException(nameof(ratio), "Zero has no prime-exponent coordinate receipt.");
        }

        var exponents = new SortedDictionary<BigInteger, int>();
        ProbePrimeMath.AccumulateFactorization(
            BigInteger.Abs(ratio.Numerator),
            +1,
            exponents,
            maximumTrialDivisor);
        ProbePrimeMath.AccumulateFactorization(
            ratio.Denominator,
            -1,
            exponents,
            maximumTrialDivisor);

        return new ProbeSignedPrimeCoordinates(
            ratio.Sign,
            exponents,
            basisId,
            completeness,
            payloadReplayability);
    }

    public ProbeSignedPrimeCoordinates Compose(ProbeSignedPrimeCoordinates other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureCompatibleBasis(other);
        var merged = new SortedDictionary<BigInteger, int>(_exponents);
        foreach (var (prime, exponent) in other._exponents)
        {
            merged.TryGetValue(prime, out var current);
            var result = checked(current + exponent);
            if (result == 0)
            {
                merged.Remove(prime);
            }
            else
            {
                merged[prime] = result;
            }
        }

        return new ProbeSignedPrimeCoordinates(
            checked(Sign * other.Sign),
            merged,
            BasisId,
            ProbeProjectionKnowledge.CombineCompleteness(Completeness, other.Completeness),
            ProbeProjectionKnowledge.CombineReplayability(PayloadReplayability, other.PayloadReplayability));
    }

    public ProbeSignedPrimeCoordinates Invert()
    {
        if (Completeness != LineageCompleteness.Exact)
        {
            throw new InvalidOperationException(
                "A lower-bound or conflicting numeric-factor projection cannot be inverted without an upper-bound or interval knowledge state.");
        }

        var inverted = _exponents.ToDictionary(pair => pair.Key, pair => checked(-pair.Value));
        return new ProbeSignedPrimeCoordinates(
            Sign,
            inverted,
            BasisId,
            Completeness,
            PayloadReplayability);
    }

    public ProbeExactRatio ToRatio()
    {
        if (Completeness != LineageCompleteness.Exact)
        {
            throw new InvalidOperationException(
                "Only an Exact numeric-factor projection can reconstruct an exact rational coefficient.");
        }

        if (PayloadReplayability != PayloadReplayability.ReplayableExact)
        {
            throw new InvalidOperationException(
                "The numeric-factor projection does not declare its projected coefficient replayable exactly.");
        }

        var numerator = new BigInteger(Sign);
        var denominator = BigInteger.One;
        foreach (var (prime, exponent) in _exponents)
        {
            if (exponent > 0)
            {
                numerator *= BigInteger.Pow(prime, exponent);
            }
            else
            {
                denominator *= BigInteger.Pow(prime, checked(-exponent));
            }
        }

        return new ProbeExactRatio(numerator, denominator);
    }

    public string ToCanonicalString()
    {
        var fields = new List<string>
        {
            Contract.Name,
            BasisId,
            Completeness.ToString(),
            PayloadReplayability.ToString(),
            Sign.ToString(CultureInfo.InvariantCulture),
            _exponents.Count.ToString(CultureInfo.InvariantCulture),
        };
        foreach (var (prime, exponent) in _exponents)
        {
            fields.Add(prime.ToString(CultureInfo.InvariantCulture));
            fields.Add(exponent.ToString(CultureInfo.InvariantCulture));
        }

        return ProbeProjectionCanonical.Fields(fields);
    }

    public bool Equals(ProbeSignedPrimeCoordinates? other) =>
        other is not null &&
        string.Equals(BasisId, other.BasisId, StringComparison.Ordinal) &&
        Completeness == other.Completeness &&
        PayloadReplayability == other.PayloadReplayability &&
        Sign == other.Sign &&
        _exponents.SequenceEqual(other._exponents);

    public override bool Equals(object? obj) =>
        obj is ProbeSignedPrimeCoordinates other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BasisId, StringComparer.Ordinal);
        hash.Add(Completeness);
        hash.Add(PayloadReplayability);
        hash.Add(Sign);
        foreach (var pair in _exponents)
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => ToCanonicalString();

    private void EnsureCompatibleBasis(ProbeSignedPrimeCoordinates other)
    {
        if (!string.Equals(BasisId, other.BasisId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Numeric-factor projections from different prime-basis identities cannot compose.");
        }
    }
}

/// <summary>
/// Physical dimensions live in a different coordinate space from numeric
/// prime factors. Axis names are supplied by the declared unit system.
/// </summary>
public sealed class ProbeUnitDimensionVector : IEquatable<ProbeUnitDimensionVector>
{
    public const string DefaultBasisId =
        "PROBE_UNIT_DIMENSION_BASIS__CALLER_DECLARED_CASE_SENSITIVE_AXES__V1";

    public static ProjectionContract Contract { get; } = new(
        "SIGNED_UNIT_DIMENSION_PROJECTION",
        "Signed unit-axis exponents under the declared basis identity; exact declarations preserve the supplied dimension vector.",
        "Numeric coefficient, unit scale or offset, derivation topology, source payload, calibration evidence, authenticity, uncertainty, and physical realization.",
        "PayloadReplayability concerns the underlying source or measurement payload, which the dimension vector does not itself retain or authenticate.");

    private readonly ReadOnlyDictionary<string, int> _axes;

    private ProbeUnitDimensionVector(
        IDictionary<string, int> axes,
        string basisId,
        LineageCompleteness completeness,
        PayloadReplayability payloadReplayability)
    {
        BasisId = ProbeProjectionKnowledge.RequireBasisId(basisId, nameof(basisId));
        Completeness = completeness;
        PayloadReplayability = payloadReplayability;
        var normalized = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (axis, exponent) in axes)
        {
            var checkedAxis = ProbeProjectionKnowledge.RequireAxisId(axis, nameof(axes));

            if (exponent != 0)
            {
                normalized.Add(checkedAxis, exponent);
            }
        }

        _axes = new ReadOnlyDictionary<string, int>(normalized);
    }

    public static ProbeUnitDimensionVector Dimensionless { get; } =
        new(
            new Dictionary<string, int>(StringComparer.Ordinal),
            DefaultBasisId,
            LineageCompleteness.Exact,
            PayloadReplayability.MissingDependency);

    public string BasisId { get; }

    public LineageCompleteness Completeness { get; }

    public PayloadReplayability PayloadReplayability { get; }

    public IReadOnlyDictionary<string, int> Axes => _axes;

    public static ProbeUnitDimensionVector Create(params (string Axis, int Exponent)[] axes)
        => CreateDeclared(
            DefaultBasisId,
            LineageCompleteness.Exact,
            PayloadReplayability.MissingDependency,
            axes);

    public static ProbeUnitDimensionVector CreateDeclared(
        string basisId,
        LineageCompleteness completeness,
        PayloadReplayability payloadReplayability,
        params (string Axis, int Exponent)[] axes)
    {
        ArgumentNullException.ThrowIfNull(axes);
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (axis, exponent) in axes)
        {
            var checkedAxis = ProbeProjectionKnowledge.RequireAxisId(axis, nameof(axes));

            result.TryGetValue(checkedAxis, out var current);
            var next = checked(current + exponent);
            if (next == 0)
            {
                result.Remove(checkedAxis);
            }
            else
            {
                result[checkedAxis] = next;
            }
        }

        return new ProbeUnitDimensionVector(result, basisId, completeness, payloadReplayability);
    }

    public ProbeUnitDimensionVector Multiply(ProbeUnitDimensionVector other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureCompatibleBasis(other);
        var merged = _axes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var (axis, exponent) in other._axes)
        {
            merged.TryGetValue(axis, out var current);
            var result = checked(current + exponent);
            if (result == 0)
            {
                merged.Remove(axis);
            }
            else
            {
                merged[axis] = result;
            }
        }

        return new ProbeUnitDimensionVector(
            merged,
            BasisId,
            ProbeProjectionKnowledge.CombineCompleteness(Completeness, other.Completeness),
            ProbeProjectionKnowledge.CombineReplayability(PayloadReplayability, other.PayloadReplayability));
    }

    public ProbeUnitDimensionVector Divide(ProbeUnitDimensionVector other) =>
        Multiply(other.Invert());

    public ProbeUnitDimensionVector Invert()
    {
        if (Completeness != LineageCompleteness.Exact)
        {
            throw new InvalidOperationException(
                "A lower-bound or conflicting unit-dimension projection cannot be inverted without an upper-bound or interval knowledge state.");
        }

        return new ProbeUnitDimensionVector(
            _axes.ToDictionary(pair => pair.Key, pair => checked(-pair.Value), StringComparer.Ordinal),
            BasisId,
            Completeness,
            PayloadReplayability);
    }

    public string ToCanonicalString()
    {
        var fields = new List<string>
        {
            Contract.Name,
            BasisId,
            Completeness.ToString(),
            PayloadReplayability.ToString(),
            _axes.Count.ToString(CultureInfo.InvariantCulture),
        };
        foreach (var (axis, exponent) in _axes)
        {
            fields.Add(axis);
            fields.Add(exponent.ToString(CultureInfo.InvariantCulture));
        }

        return ProbeProjectionCanonical.Fields(fields);
    }

    public bool Equals(ProbeUnitDimensionVector? other) =>
        other is not null &&
        string.Equals(BasisId, other.BasisId, StringComparison.Ordinal) &&
        Completeness == other.Completeness &&
        PayloadReplayability == other.PayloadReplayability &&
        _axes.SequenceEqual(other._axes);

    public override bool Equals(object? obj) =>
        obj is ProbeUnitDimensionVector other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BasisId, StringComparer.Ordinal);
        hash.Add(Completeness);
        hash.Add(PayloadReplayability);
        foreach (var pair in _axes)
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => ToCanonicalString();

    private void EnsureCompatibleBasis(ProbeUnitDimensionVector other)
    {
        if (!string.Equals(BasisId, other.BasisId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Unit-dimension projections from different basis identities cannot compose.");
        }
    }
}

internal static class ProbeProjectionKnowledge
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static string RequireBasisId(string basisId, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basisId, parameterName);
        RequireWellFormedUtf16(basisId, parameterName, "Projection basis identities");
        return basisId;
    }

    public static string RequireAxisId(string axisId, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(axisId, parameterName);
        RequireWellFormedUtf16(axisId, parameterName, "Projection axis identities");
        return axisId;
    }

    private static void RequireWellFormedUtf16(
        string value,
        string parameterName,
        string identityKind)
    {
        try
        {
            _ = StrictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                $"{identityKind} must contain well-formed UTF-16.",
                parameterName,
                exception);
        }
    }

    public static LineageCompleteness CombineCompleteness(
        LineageCompleteness left,
        LineageCompleteness right)
    {
        if (left == LineageCompleteness.Conflict || right == LineageCompleteness.Conflict)
        {
            return LineageCompleteness.Conflict;
        }

        return left == LineageCompleteness.Exact && right == LineageCompleteness.Exact
            ? LineageCompleteness.Exact
            : LineageCompleteness.KnownLowerBound;
    }

    public static PayloadReplayability CombineReplayability(
        PayloadReplayability left,
        PayloadReplayability right)
    {
        if (left == PayloadReplayability.UnsupportedTransform ||
            right == PayloadReplayability.UnsupportedTransform)
        {
            return PayloadReplayability.UnsupportedTransform;
        }

        if (left == PayloadReplayability.MissingDependency ||
            right == PayloadReplayability.MissingDependency)
        {
            return PayloadReplayability.MissingDependency;
        }

        return left == PayloadReplayability.ReplayableExact &&
               right == PayloadReplayability.ReplayableExact
            ? PayloadReplayability.ReplayableExact
            : PayloadReplayability.DigestOnly;
    }
}

internal static class ProbeProjectionCanonical
{
    public static string Fields(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var builder = new StringBuilder();
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
        }

        return builder.ToString();
    }
}

internal static class ProbePrimeMath
{
    public static bool IsPrime(BigInteger value)
    {
        if (value < 2)
        {
            return false;
        }

        if (value == 2)
        {
            return true;
        }

        if (value.IsEven)
        {
            return false;
        }

        for (var candidate = new BigInteger(3); candidate * candidate <= value; candidate += 2)
        {
            if (value % candidate == 0)
            {
                return false;
            }
        }

        return true;
    }

    public static void AccumulateFactorization(
        BigInteger value,
        int direction,
        IDictionary<BigInteger, int> destination,
        int maximumTrialDivisor)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Factorization input must be positive.");
        }

        if (direction is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        var remaining = value;
        AddFactor(2);
        for (var candidate = new BigInteger(3); candidate * candidate <= remaining; candidate += 2)
        {
            if (candidate > maximumTrialDivisor)
            {
                throw new InvalidOperationException(
                    $"Prime-factor discovery exceeded the declared trial-divisor limit {maximumTrialDivisor}.");
            }

            AddFactor(candidate);
        }

        if (remaining > 1)
        {
            destination.TryGetValue(remaining, out var current);
            var next = checked(current + direction);
            if (next == 0)
            {
                destination.Remove(remaining);
            }
            else
            {
                destination[remaining] = next;
            }
        }

        void AddFactor(BigInteger prime)
        {
            var count = 0;
            while (remaining % prime == 0)
            {
                remaining /= prime;
                count++;
            }

            if (count == 0)
            {
                return;
            }

            destination.TryGetValue(prime, out var current);
            var next = checked(current + checked(direction * count));
            if (next == 0)
            {
                destination.Remove(prime);
            }
            else
            {
                destination[prime] = next;
            }
        }
    }
}
