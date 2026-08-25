using System.Collections;

namespace PrimeAxiom.Core.Hybrid;

public enum BankStrategy
{
    None,
    FixedPrefix,
    WorkloadSelected,
    Adaptive,
    Configured,
}

/// <summary>
/// A bounded, ordered set of prime labels. The bank is configuration, not part
/// of an individual numeric payload. Empty banks deliberately recover ordinary
/// signed binary magnitude in the cofactor.
/// </summary>
public sealed class ValuationBank : IReadOnlyList<int>, IEquatable<ValuationBank>
{
    public const int MaximumLanes = 4_096;
    private readonly int[] _primes;

    public ValuationBank(IEnumerable<int> primes, BankStrategy strategy = BankStrategy.Configured, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(primes);
        _primes = primes.Take(MaximumLanes + 1).ToArray();
        if (_primes.Length > MaximumLanes)
        {
            throw new ArgumentException($"A bank may contain at most {MaximumLanes} lanes.", nameof(primes));
        }
        long validationTrialDivisions = 0;
        foreach (var prime in _primes)
        {
            if (!IsPrime(prime, out var trialDivisions))
            {
                throw new ArgumentException("Every bank label must be a positive prime that fits in Int32.", nameof(primes));
            }

            validationTrialDivisions = checked(validationTrialDivisions + trialDivisions);
        }

        if (!_primes.SequenceEqual(_primes.OrderBy(prime => prime)) || _primes.Distinct().Count() != _primes.Length)
        {
            throw new ArgumentException("Bank primes must be strictly increasing and unique.", nameof(primes));
        }

        Strategy = _primes.Length == 0 ? BankStrategy.None : strategy;
        Name = string.IsNullOrWhiteSpace(name) ? DefaultName(Strategy, _primes.Length) : name.Trim();
        ValidationTrialDivisions = validationTrialDivisions;
    }

    public int Count => _primes.Length;

    public int this[int index] => _primes[index];

    public BankStrategy Strategy { get; }

    public string Name { get; }

    public long ValidationTrialDivisions { get; }

    public string CanonicalId => _primes.Length == 0 ? "bank:empty" : $"bank:{string.Join(',', _primes)}";

    public long CatalogPayloadBits => _primes.Sum(prime => BitLength((uint)prime));

    public static ValuationBank Empty { get; } = new(Array.Empty<int>(), BankStrategy.None, "no-bank");

    public static ValuationBank First(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaximumLanes);
        if (count == 0)
        {
            return Empty;
        }

        var primes = new List<int>(count);
        for (var candidate = 2; primes.Count < count; candidate++)
        {
            if (IsPrime(candidate))
            {
                primes.Add(candidate);
            }
        }

        return new ValuationBank(primes, BankStrategy.FixedPrefix, $"first-{count}");
    }

    public static ValuationBank WorkloadSelected(IEnumerable<int> primes, string name)
    {
        ArgumentNullException.ThrowIfNull(primes);
        var bounded = primes.Take(MaximumLanes + 1).ToArray();
        if (bounded.Length > MaximumLanes)
        {
            throw new ArgumentException($"A bank may contain at most {MaximumLanes} lanes.", nameof(primes));
        }

        return new ValuationBank(bounded.OrderBy(prime => prime), BankStrategy.WorkloadSelected, name);
    }

    public int IndexOf(int prime) => Array.BinarySearch(_primes, prime) is var index && index >= 0 ? index : -1;

    public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)_primes).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _primes.GetEnumerator();

    public bool Equals(ValuationBank? other) => other is not null && _primes.SequenceEqual(other._primes);

    public override bool Equals(object? obj) => obj is ValuationBank other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var prime in _primes)
        {
            hash.Add(prime);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => $"{Name} [{string.Join(',', _primes)}]";

    private static bool IsPrime(int value) => IsPrime(value, out _);

    private static bool IsPrime(int value, out long trialDivisions)
    {
        trialDivisions = 0;
        if (value < 2)
        {
            return false;
        }

        if (value == 2)
        {
            return true;
        }

        if ((value & 1) == 0)
        {
            return false;
        }

        for (var divisor = 3; (long)divisor * divisor <= value; divisor += 2)
        {
            trialDivisions++;
            if (value % divisor == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static int BitLength(uint value)
    {
        var bits = 0;
        do
        {
            bits++;
            value >>= 1;
        }
        while (value != 0);

        return bits;
    }

    private static string DefaultName(BankStrategy strategy, int count) => $"{strategy.ToString().ToLowerInvariant()}-{count}";
}
