namespace PrimeAxiom.Core.Representations;

/// <summary>
/// A configured generator catalogue. Prime labels give the coordinate lanes
/// their ordinary-integer semantics; they are configuration, not datapath state.
/// </summary>
public sealed class PrimeBasis : IEquatable<PrimeBasis>
{
    private readonly int[] _primes;

    public PrimeBasis(IEnumerable<int> primes)
    {
        _primes = primes.ToArray();
        if (_primes.Length == 0)
        {
            throw new ArgumentException("A prime basis must contain at least one generator.", nameof(primes));
        }

        if (_primes.Any(prime => prime < 2 || !IsPrime(prime)))
        {
            throw new ArgumentException("Every basis label must be prime in ordinary integer arithmetic.", nameof(primes));
        }

        if (!_primes.SequenceEqual(_primes.Distinct().Order()))
        {
            throw new ArgumentException("Basis labels must be unique and strictly increasing.", nameof(primes));
        }
    }

    public int Count => _primes.Length;

    public int this[int index] => _primes[index];

    public IReadOnlyList<int> Primes => Array.AsReadOnly(_primes);

    public static PrimeBasis First(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var primes = new List<int>(count);
        for (var candidate = 2; primes.Count < count; candidate++)
        {
            if (IsPrime(candidate))
            {
                primes.Add(candidate);
            }
        }

        return new PrimeBasis(primes);
    }

    public bool Equals(PrimeBasis? other) => other is not null && _primes.SequenceEqual(other._primes);

    public override bool Equals(object? obj) => obj is PrimeBasis other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var prime in _primes)
        {
            hash.Add(prime);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => $"[{string.Join(",", _primes)}]";

    private static bool IsPrime(int candidate)
    {
        if (candidate < 2)
        {
            return false;
        }

        if (candidate == 2)
        {
            return true;
        }

        if (candidate % 2 == 0)
        {
            return false;
        }

        for (var divisor = 3; (long)divisor * divisor <= candidate; divisor += 2)
        {
            if (candidate % divisor == 0)
            {
                return false;
            }
        }

        return true;
    }
}
