using System.Collections;
using System.Numerics;
using System.Text;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Circuits;

/// <summary>A fixed-width, least-significant-bit-first binary word.</summary>
public sealed class BinaryWord : IReadOnlyList<BitState>, IEquatable<BinaryWord>
{
    private readonly BitState[] _bits;

    public BinaryWord(IEnumerable<BitState> bits)
    {
        _bits = bits.ToArray();
        if (_bits.Length == 0)
        {
            throw new ArgumentException("A binary word must contain at least one state.", nameof(bits));
        }
    }

    public int Count => _bits.Length;

    public int Width => _bits.Length;

    public BitState this[int index] => _bits[index];

    public static BinaryWord Zero(int width) => new(Enumerable.Repeat(BitState.Off, ValidateWidth(width)));

    public static BinaryWord One(int width)
    {
        var bits = Enumerable.Repeat(BitState.Off, ValidateWidth(width)).ToArray();
        bits[0] = BitState.On;
        return new BinaryWord(bits);
    }

    public static BinaryWord FromUnsigned(BigInteger value, int width)
    {
        ValidateWidth(width);
        if (value < BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Unsigned words cannot encode a negative value.");
        }

        if (value >= (BigInteger.One << width))
        {
            throw new OverflowException($"{value} does not fit in {width} bits.");
        }

        var bits = new BitState[width];
        for (var index = 0; index < width; index++)
        {
            bits[index] = BitStateExtensions.FromBoolean(!value.IsEven);
            value >>= 1;
        }

        return new BinaryWord(bits);
    }

    public static BinaryWord ParseMostSignificantFirst(string digits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(digits);
        if (digits.Any(digit => digit is not ('0' or '1')))
        {
            throw new FormatException("A binary word may contain only 0 and 1.");
        }

        return new BinaryWord(digits.Reverse().Select(digit =>
            digit == '1' ? BitState.On : BitState.Off));
    }

    public BigInteger ToUnsigned()
    {
        var result = BigInteger.Zero;
        for (var index = Width - 1; index >= 0; index--)
        {
            result <<= 1;
            if (_bits[index] == BitState.On)
            {
                result += BigInteger.One;
            }
        }

        return result;
    }

    public string ToMostSignificantFirstString()
    {
        var builder = new StringBuilder(Width);
        for (var index = Width - 1; index >= 0; index--)
        {
            builder.Append(_bits[index].ToDigit());
        }

        return builder.ToString();
    }

    public BitState[] CopyBits() => (BitState[])_bits.Clone();

    public IEnumerator<BitState> GetEnumerator() => ((IEnumerable<BitState>)_bits).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _bits.GetEnumerator();

    public bool Equals(BinaryWord? other) => other is not null && _bits.SequenceEqual(other._bits);

    public override bool Equals(object? obj) => obj is BinaryWord other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var bit in _bits)
        {
            hash.Add(bit);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => ToMostSignificantFirstString();

    private static int ValidateWidth(int width) => width > 0
        ? width
        : throw new ArgumentOutOfRangeException(nameof(width));
}
