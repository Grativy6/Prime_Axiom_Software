using System.Globalization;
using System.Numerics;

namespace PrimeAxiom.Core.Build004.Combinatorics;

/// <summary>
/// A canonical arbitrary-precision rational. The denominator is positive, numerator and
/// denominator are coprime, and zero has the unique representation 0/1.
/// </summary>
public readonly struct ExactRational : IEquatable<ExactRational>, IComparable<ExactRational>
{
    private readonly BigInteger denominator;

    public ExactRational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException("An exact rational denominator cannot be zero.");
        }

        if (numerator.IsZero)
        {
            Numerator = BigInteger.Zero;
            this.denominator = BigInteger.One;
            return;
        }

        if (denominator.Sign < 0)
        {
            numerator = BigInteger.Negate(numerator);
            denominator = BigInteger.Negate(denominator);
        }

        var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        Numerator = numerator / divisor;
        this.denominator = denominator / divisor;
    }

    private ExactRational(BigInteger canonicalNumerator, BigInteger canonicalDenominator, bool canonical)
    {
        if (!canonical || canonicalDenominator.Sign <= 0)
        {
            throw new ArgumentException("The internal rational constructor requires canonical input.", nameof(canonical));
        }

        Numerator = canonicalNumerator;
        denominator = canonicalNumerator.IsZero ? BigInteger.One : canonicalDenominator;
    }

    public static ExactRational Zero { get; } = new(BigInteger.Zero, BigInteger.One, canonical: true);

    public static ExactRational One { get; } = new(BigInteger.One, BigInteger.One, canonical: true);

    public BigInteger Numerator { get; }

    public BigInteger Denominator => Numerator.IsZero ? BigInteger.One : denominator;

    public int Sign => Numerator.Sign;

    public bool IsZero => Numerator.IsZero;

    public bool IsOne => Numerator.IsOne && Denominator.IsOne;

    public static ExactRational FromInteger(BigInteger value) =>
        new(value, BigInteger.One, canonical: true);

    public static ExactRational operator +(ExactRational left, ExactRational right)
    {
        var common = BigInteger.GreatestCommonDivisor(left.Denominator, right.Denominator);
        var leftScale = right.Denominator / common;
        var rightScale = left.Denominator / common;
        var numerator = (left.Numerator * leftScale) + (right.Numerator * rightScale);

        if (numerator.IsZero)
        {
            return Zero;
        }

        var residual = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), common);
        return CreateCanonical(
            numerator / residual,
            left.Denominator * leftScale / residual);
    }

    public static ExactRational operator -(ExactRational left, ExactRational right) =>
        left + new ExactRational(BigInteger.Negate(right.Numerator), right.Denominator, canonical: true);

    public static ExactRational operator *(ExactRational left, ExactRational right)
    {
        if (left.IsZero || right.IsZero)
        {
            return Zero;
        }

        var leftCancellation = BigInteger.GreatestCommonDivisor(
            BigInteger.Abs(left.Numerator),
            right.Denominator);
        var rightCancellation = BigInteger.GreatestCommonDivisor(
            BigInteger.Abs(right.Numerator),
            left.Denominator);

        return CreateCanonical(
            (left.Numerator / leftCancellation) * (right.Numerator / rightCancellation),
            (left.Denominator / rightCancellation) * (right.Denominator / leftCancellation));
    }

    public static ExactRational operator /(ExactRational left, ExactRational right)
    {
        if (right.IsZero)
        {
            throw new DivideByZeroException("Cannot divide by an exact rational zero.");
        }

        return left * new ExactRational(right.Denominator, right.Numerator);
    }

    public static bool operator <(ExactRational left, ExactRational right) => left.CompareTo(right) < 0;

    public static bool operator <=(ExactRational left, ExactRational right) => left.CompareTo(right) <= 0;

    public static bool operator >(ExactRational left, ExactRational right) => left.CompareTo(right) > 0;

    public static bool operator >=(ExactRational left, ExactRational right) => left.CompareTo(right) >= 0;

    public static bool operator ==(ExactRational left, ExactRational right) => left.Equals(right);

    public static bool operator !=(ExactRational left, ExactRational right) => !left.Equals(right);

    public int CompareTo(ExactRational other) =>
        (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    public bool Equals(ExactRational other) =>
        Numerator == other.Numerator && Denominator == other.Denominator;

    public override bool Equals(object? obj) => obj is ExactRational other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public override string ToString() => Denominator.IsOne
        ? Numerator.ToString(CultureInfo.InvariantCulture)
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{Numerator}/{Denominator}");

    internal static ExactRational CreateCanonical(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), "A canonical denominator must be positive.");
        }

        if (numerator.IsZero)
        {
            return Zero;
        }

        return new ExactRational(numerator, denominator, canonical: true);
    }
}
