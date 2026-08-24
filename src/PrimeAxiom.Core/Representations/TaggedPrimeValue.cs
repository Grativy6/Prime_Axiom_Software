using System.Numerics;

namespace PrimeAxiom.Core.Representations;

/// <summary>
/// Explicitly pays for the cases omitted by the positive free-commutative-monoid
/// model. Zero is a separate tag; negative values add a sign tag.
/// </summary>
public sealed record TaggedPrimeValue
{
    private TaggedPrimeValue(int sign, PrimeCoordinates? magnitude)
    {
        if (sign is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sign), "Sign must be -1, 0, or 1.");
        }

        if ((sign == 0) != (magnitude is null))
        {
            throw new ArgumentException("Zero must omit magnitude; nonzero values must supply it.", nameof(magnitude));
        }

        Sign = sign;
        Magnitude = magnitude;
    }

    public int Sign { get; }

    public PrimeCoordinates? Magnitude { get; }

    public bool IsZero => Sign == 0;

    public static TaggedPrimeValue Zero { get; } = new(0, null);

    public static (TaggedPrimeValue? Value, CoordinateReceipt Receipt) Encode(
        BigInteger value,
        PrimeBasis basis,
        int exponentWidth)
    {
        if (value.IsZero)
        {
            return (
                Zero,
                new CoordinateReceipt(
                    "TAG_ZERO",
                    true,
                    CoordinateFailure.None,
                    CoordinateCost.Zero,
                    UsedMagnitudeDomain: true,
                    Scope: "Signed integers with explicit zero/sign tags"));
        }

        var encoded = PrimeCoordinates.Encode(BigInteger.Abs(value), basis, exponentWidth);
        return encoded.Value is null
            ? (null, encoded.Receipt)
            : (new TaggedPrimeValue(value.Sign, encoded.Value), encoded.Receipt);
    }

    public (TaggedPrimeValue? Value, CoordinateReceipt Receipt) Compose(TaggedPrimeValue other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (IsZero || other.IsZero)
        {
            return (
                Zero,
                new CoordinateReceipt(
                    "SIGNED_COMPOSE",
                    true,
                    CoordinateFailure.None,
                    CoordinateCost.Zero,
                    UsedMagnitudeDomain: false,
                    Scope: "Zero tag absorbs multiplication"));
        }

        var composed = Magnitude!.Compose(other.Magnitude!);
        return composed.Value is null
            ? (null, composed.Receipt)
            : (new TaggedPrimeValue(Sign * other.Sign, composed.Value), composed.Receipt);
    }

    public BigInteger Reconstruct()
    {
        if (IsZero)
        {
            return BigInteger.Zero;
        }

        return Sign * Magnitude!.Reconstruct().Value;
    }
}
