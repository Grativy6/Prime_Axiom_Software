using System.Globalization;
using System.Numerics;

namespace PrimeAxiom.Core.Build004.Lineage;

public sealed record LineageAtomRegistration
{
    public LineageAtomRegistration(AtomKey key, BigInteger primeLabel, int bitIndex)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        ArgumentOutOfRangeException.ThrowIfLessThan(primeLabel, new BigInteger(2));

        ArgumentOutOfRangeException.ThrowIfNegative(bitIndex);
        PrimeLabel = primeLabel;
        BitIndex = bitIndex;
    }

    public AtomKey Key { get; }
    public BigInteger PrimeLabel { get; }
    public int BitIndex { get; }
}

public sealed class LineageRegistry
{
    private readonly IReadOnlyList<LineageAtomRegistration> registrations;
    private readonly Dictionary<AtomKey, LineageAtomRegistration> byKey;

    public LineageRegistry(
        string namespaceId,
        string assignmentEpoch,
        IEnumerable<LineageAtomRegistration> registrations)
    {
        NamespaceId = LineageText.RequireToken(namespaceId, nameof(namespaceId));
        AssignmentEpoch = LineageText.RequireToken(assignmentEpoch, nameof(assignmentEpoch));
        ArgumentNullException.ThrowIfNull(registrations);

        var frozen = registrations.ToArray();
        if (frozen.Any(registration => registration is null))
        {
            throw new ArgumentException("A registry cannot contain a null registration.", nameof(registrations));
        }

        if (frozen.Any(registration =>
                !string.Equals(registration.Key.NamespaceId, NamespaceId, StringComparison.Ordinal) ||
                !string.Equals(registration.Key.AssignmentEpoch, AssignmentEpoch, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Every atom key must belong to the registry namespace and epoch.", nameof(registrations));
        }

        if (frozen.Select(registration => registration.Key).Distinct().Count() != frozen.Length)
        {
            throw new ArgumentException("Atom keys must be unique.", nameof(registrations));
        }

        if (frozen.Select(registration => registration.PrimeLabel).Distinct().Count() != frozen.Length)
        {
            throw new ArgumentException("Prime labels must be injective.", nameof(registrations));
        }

        if (frozen.Select(registration => registration.BitIndex).Distinct().Count() != frozen.Length)
        {
            throw new ArgumentException("Bit positions must be injective.", nameof(registrations));
        }

        foreach (var registration in frozen)
        {
            if (!IsPrime(registration.PrimeLabel))
            {
                throw new ArgumentException(
                    $"Registry label {registration.PrimeLabel.ToString(CultureInfo.InvariantCulture)} is not prime.",
                    nameof(registrations));
            }
        }

        var ordered = frozen
            .OrderBy(registration => registration.BitIndex)
            .ThenBy(registration => registration.Key, AtomKeyOrdering.Comparer)
            .ToArray();
        this.registrations = Array.AsReadOnly(ordered);
        byKey = new Dictionary<AtomKey, LineageAtomRegistration>(
            ordered.ToDictionary(registration => registration.Key));
        UniverseSize = ordered.Length == 0 ? 0 : checked(ordered[^1].BitIndex + 1);
        RegistryId = LineageHash.Sha256(LineageText.Fields(
            "prime-axiom-lineage-registry-v1",
            NamespaceId,
            AssignmentEpoch,
            string.Concat(ordered.Select(CanonicalRegistration))));
    }

    public string NamespaceId { get; }
    public string AssignmentEpoch { get; }
    public string RegistryId { get; }
    public int UniverseSize { get; }
    public IReadOnlyList<LineageAtomRegistration> Registrations => registrations;

    public bool Contains(AtomKey key) => key is not null && byKey.ContainsKey(key);

    public LineageAtomRegistration Get(AtomKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return byKey.TryGetValue(key, out var registration)
            ? registration
            : throw new KeyNotFoundException($"Atom {key} is not registered in {RegistryId}.");
    }

    public static LineageRegistry CreateSequential(
        string namespaceId,
        string assignmentEpoch,
        IEnumerable<string> occurrenceIds)
    {
        ArgumentNullException.ThrowIfNull(occurrenceIds);
        var ids = occurrenceIds.ToArray();
        var registrations = new List<LineageAtomRegistration>(ids.Length);
        var candidate = new BigInteger(2);
        for (var index = 0; index < ids.Length; index++)
        {
            while (!IsPrime(candidate))
            {
                candidate++;
            }

            registrations.Add(new LineageAtomRegistration(
                new AtomKey(namespaceId, assignmentEpoch, ids[index]),
                candidate,
                index));
            candidate++;
        }

        return new LineageRegistry(namespaceId, assignmentEpoch, registrations);
    }

    private static string CanonicalRegistration(LineageAtomRegistration registration) =>
        LineageText.Fields(
            registration.Key.Canonical,
            registration.PrimeLabel.ToString(CultureInfo.InvariantCulture),
            registration.BitIndex.ToString(CultureInfo.InvariantCulture));

    private static bool IsPrime(BigInteger value)
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

        for (var divisor = new BigInteger(3); divisor <= value / divisor; divisor += 2)
        {
            if (value % divisor == BigInteger.Zero)
            {
                return false;
            }
        }

        return true;
    }
}
