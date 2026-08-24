using System.Numerics;

namespace PrimeAxiom.Core.Representations;

public readonly record struct PrimePower(int LaneIndex, BigInteger Exponent);

public readonly record struct SparseCost(long IndexComparisons, long ExponentAdds, long EntryWrites);

public sealed record SparseOperationResult(SparsePrimeCoordinates Value, SparseCost Cost);

/// <summary>
/// Sparse coordinates trade fixed parallel lanes for indices, comparisons,
/// routing, and variable-length storage. Costs here are abstract data-structure
/// operations, not NAND-equivalent hardware counts.
/// </summary>
public sealed class SparsePrimeCoordinates
{
    private readonly PrimePower[] _powers;

    public SparsePrimeCoordinates(PrimeBasis basis, IEnumerable<PrimePower> powers)
    {
        Basis = basis ?? throw new ArgumentNullException(nameof(basis));
        _powers = powers.Where(power => power.Exponent != BigInteger.Zero).ToArray();
        if (_powers.Any(power =>
                power.LaneIndex < 0 ||
                power.LaneIndex >= basis.Count ||
                power.Exponent < BigInteger.Zero))
        {
            throw new ArgumentException("Sparse powers must use valid lanes and nonnegative exponents.", nameof(powers));
        }

        if (!_powers.Select(power => power.LaneIndex).SequenceEqual(
                _powers.Select(power => power.LaneIndex).Distinct().Order()))
        {
            throw new ArgumentException("Sparse powers must be unique and strictly lane-sorted.", nameof(powers));
        }
    }

    public PrimeBasis Basis { get; }

    public IReadOnlyList<PrimePower> Powers => Array.AsReadOnly(_powers);

    public int NonzeroLaneCount => _powers.Length;

    public static SparsePrimeCoordinates FromDense(PrimeCoordinates dense)
    {
        ArgumentNullException.ThrowIfNull(dense);
        var powers = Enumerable.Range(0, dense.LaneCount)
            .Select(lane => new PrimePower(lane, dense.ExponentAt(lane).ToUnsigned()))
            .Where(power => power.Exponent != BigInteger.Zero);
        return new SparsePrimeCoordinates(dense.Basis, powers);
    }

    public SparseOperationResult Compose(SparsePrimeCoordinates other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!Basis.Equals(other.Basis))
        {
            throw new ArgumentException("Sparse operands must use the same basis.", nameof(other));
        }

        var merged = new List<PrimePower>(_powers.Length + other._powers.Length);
        var left = 0;
        var right = 0;
        long comparisons = 0;
        long additions = 0;
        while (left < _powers.Length || right < other._powers.Length)
        {
            if (left >= _powers.Length)
            {
                merged.Add(other._powers[right++]);
                continue;
            }

            if (right >= other._powers.Length)
            {
                merged.Add(_powers[left++]);
                continue;
            }

            comparisons++;
            var leftPower = _powers[left];
            var rightPower = other._powers[right];
            if (leftPower.LaneIndex == rightPower.LaneIndex)
            {
                additions++;
                merged.Add(new PrimePower(leftPower.LaneIndex, leftPower.Exponent + rightPower.Exponent));
                left++;
                right++;
            }
            else if (leftPower.LaneIndex < rightPower.LaneIndex)
            {
                merged.Add(leftPower);
                left++;
            }
            else
            {
                merged.Add(rightPower);
                right++;
            }
        }

        return new SparseOperationResult(
            new SparsePrimeCoordinates(Basis, merged),
            new SparseCost(comparisons, additions, merged.Count));
    }

    public long PayloadBits(int laneIndexWidth, int exponentWidth, int lengthHeaderWidth = 0)
    {
        if (laneIndexWidth <= 0 || exponentWidth <= 0 || lengthHeaderWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(laneIndexWidth));
        }

        var requiredLaneIndexWidth = BitsRequiredForUnsignedValue(Basis.Count - 1);
        if (laneIndexWidth < requiredLaneIndexWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(laneIndexWidth),
                $"At least {requiredLaneIndexWidth} bits are required for this basis.");
        }

        var requiredExponentWidth = _powers.Length == 0
            ? 1L
            : _powers.Max(power => power.Exponent.GetBitLength());
        if (exponentWidth < requiredExponentWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exponentWidth),
                $"At least {requiredExponentWidth} bits are required by the stored exponents.");
        }

        if (lengthHeaderWidth > 0)
        {
            var requiredLengthWidth = BitsRequiredForUnsignedValue(_powers.Length);
            if (lengthHeaderWidth < requiredLengthWidth)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lengthHeaderWidth),
                    $"At least {requiredLengthWidth} bits are required by the stored entry count.");
            }
        }

        return checked(lengthHeaderWidth + (long)_powers.Length * (laneIndexWidth + exponentWidth));
    }

    private static int BitsRequiredForUnsignedValue(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var width = 1;
        var capacity = 2L;
        while (capacity <= value)
        {
            width++;
            capacity <<= 1;
        }

        return width;
    }
}
