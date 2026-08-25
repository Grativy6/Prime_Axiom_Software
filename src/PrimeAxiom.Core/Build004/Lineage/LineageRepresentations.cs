using System.Globalization;
using System.Numerics;

namespace PrimeAxiom.Core.Build004.Lineage;

public sealed record ProjectionContract(
    string Name,
    string Preserves,
    string Discards,
    string ReplayabilitySemantics);

public sealed class SupportProjection : IEquatable<SupportProjection>
{
    public static ProjectionContract Contract { get; } = new(
        "EXACT_ACTIVE_SOURCE_SUPPORT",
        "Atom occurrence identity and exact membership when completeness is Exact.",
        "Multiplicity, joint-versus-alternative structure, operation order, payload, authenticity, and authority.",
        "Carries an explicit payload-replayability declaration; the projection does not prove that declaration.");

    private readonly AtomKey[] atoms;
    private readonly IReadOnlyList<AtomKey> readOnlyAtoms;

    private SupportProjection(
        string registryId,
        IEnumerable<AtomKey> atoms,
        LineageCompleteness completeness,
        PayloadReplayability payloadReplayability)
    {
        RegistryId = LineageHash.RequireSha256(registryId, nameof(registryId));
        Completeness = completeness;
        PayloadReplayability = payloadReplayability;
        this.atoms = atoms
            .Distinct()
            .OrderBy(atom => atom, AtomKeyOrdering.Comparer)
            .ToArray();
        readOnlyAtoms = Array.AsReadOnly(this.atoms);
    }

    public string RegistryId { get; }
    public LineageCompleteness Completeness { get; }
    public PayloadReplayability PayloadReplayability { get; }
    public IReadOnlyList<AtomKey> Atoms => readOnlyAtoms;
    public int Count => atoms.Length;

    public static SupportProjection Create(
        LineageRegistry registry,
        IEnumerable<AtomKey> atoms,
        LineageCompleteness completeness = LineageCompleteness.Exact,
        PayloadReplayability payloadReplayability = PayloadReplayability.DigestOnly)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(atoms);
        var frozen = atoms.ToArray();
        if (frozen.Any(atom => atom is null || !registry.Contains(atom)))
        {
            throw new ArgumentException("Every support atom must be present in the declared registry.", nameof(atoms));
        }

        return new SupportProjection(registry.RegistryId, frozen, completeness, payloadReplayability);
    }

    public bool Contains(AtomKey atom)
    {
        ArgumentNullException.ThrowIfNull(atom);
        return Array.BinarySearch(atoms, atom, AtomKeyOrdering.Comparer) >= 0;
    }

    public SupportProjection Union(SupportProjection other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureCompatible(other);
        return new SupportProjection(
            RegistryId,
            atoms.Concat(other.atoms),
            CombineKnowledge(Completeness, other.Completeness),
            ProjectionKnowledge.CombineReplayability(PayloadReplayability, other.PayloadReplayability));
    }

    public SupportProjection Intersect(SupportProjection other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureCompatible(other);
        var right = new HashSet<AtomKey>(other.atoms);
        return new SupportProjection(
            RegistryId,
            atoms.Where(right.Contains),
            CombineKnowledge(Completeness, other.Completeness),
            ProjectionKnowledge.CombineReplayability(PayloadReplayability, other.PayloadReplayability));
    }

    public SupportProjection ExceptExact(SupportProjection removed)
    {
        ArgumentNullException.ThrowIfNull(removed);
        EnsureCompatible(removed);
        if (Completeness != LineageCompleteness.Exact || removed.Completeness != LineageCompleteness.Exact)
        {
            throw new InvalidOperationException("Exact support subtraction requires two exact support projections.");
        }

        var exclusions = new HashSet<AtomKey>(removed.atoms);
        return new SupportProjection(
            RegistryId,
            atoms.Where(atom => !exclusions.Contains(atom)),
            LineageCompleteness.Exact,
            ProjectionKnowledge.CombineReplayability(PayloadReplayability, removed.PayloadReplayability));
    }

    public bool Equals(SupportProjection? other) =>
        other is not null &&
        string.Equals(RegistryId, other.RegistryId, StringComparison.Ordinal) &&
        Completeness == other.Completeness &&
        PayloadReplayability == other.PayloadReplayability &&
        atoms.SequenceEqual(other.atoms);

    public override bool Equals(object? obj) => Equals(obj as SupportProjection);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RegistryId, StringComparer.Ordinal);
        hash.Add(Completeness);
        hash.Add(PayloadReplayability);
        foreach (var atom in atoms)
        {
            hash.Add(atom);
        }

        return hash.ToHashCode();
    }

    internal string Canonical => LineageText.Fields(
        RegistryId,
        Completeness.ToString(),
        PayloadReplayability.ToString(),
        string.Concat(atoms.Select(atom => LineageText.Fields(atom.Canonical))));

    private static LineageCompleteness CombineKnowledge(
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

    private void EnsureCompatible(SupportProjection other)
    {
        if (!string.Equals(RegistryId, other.RegistryId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Support projections from different registry epochs cannot be combined implicitly.");
        }
    }
}

public sealed record AtomMultiplicity
{
    public AtomMultiplicity(AtomKey atom, int count)
    {
        Atom = atom ?? throw new ArgumentNullException(nameof(atom));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        Count = count;
    }

    public AtomKey Atom { get; }
    public int Count { get; }
}

public sealed class MultiplicityProjection : IEquatable<MultiplicityProjection>
{
    public static ProjectionContract Contract { get; } = new(
        "TOTAL_ATOM_OCCURRENCE_MULTIPLICITY",
        "Atom identity and total syntactic occurrence count across the positive derivation expression.",
        "Grouping into joint terms, alternative branches, operation order, payload, authenticity, and authority.",
        "Carries an explicit payload-replayability declaration; the projection does not prove that declaration.");

    private readonly AtomMultiplicity[] entries;
    private readonly IReadOnlyList<AtomMultiplicity> readOnlyEntries;

    private MultiplicityProjection(
        string registryId,
        IEnumerable<AtomMultiplicity> entries,
        LineageCompleteness completeness,
        PayloadReplayability payloadReplayability)
    {
        RegistryId = LineageHash.RequireSha256(registryId, nameof(registryId));
        Completeness = completeness;
        PayloadReplayability = payloadReplayability;
        this.entries = entries
            .GroupBy(entry => entry.Atom)
            .Select(group => new AtomMultiplicity(group.Key, checked(group.Sum(entry => entry.Count))))
            .OrderBy(entry => entry.Atom, AtomKeyOrdering.Comparer)
            .ToArray();
        readOnlyEntries = Array.AsReadOnly(this.entries);
    }

    public string RegistryId { get; }
    public LineageCompleteness Completeness { get; }
    public PayloadReplayability PayloadReplayability { get; }
    public IReadOnlyList<AtomMultiplicity> Entries => readOnlyEntries;

    public static MultiplicityProjection Create(
        LineageRegistry registry,
        IEnumerable<AtomMultiplicity> entries,
        LineageCompleteness completeness = LineageCompleteness.Exact,
        PayloadReplayability payloadReplayability = PayloadReplayability.DigestOnly)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(entries);
        var frozen = entries.ToArray();
        if (frozen.Any(entry => entry is null || !registry.Contains(entry.Atom)))
        {
            throw new ArgumentException("Every multiplicity atom must be present in the declared registry.", nameof(entries));
        }

        return new MultiplicityProjection(registry.RegistryId, frozen, completeness, payloadReplayability);
    }

    public int GetCount(AtomKey atom)
    {
        ArgumentNullException.ThrowIfNull(atom);
        var match = entries.FirstOrDefault(entry => entry.Atom == atom);
        return match?.Count ?? 0;
    }

    public MultiplicityProjection Add(MultiplicityProjection other) =>
        Combine(other, static (left, right) => checked(left + right));

    public MultiplicityProjection Min(MultiplicityProjection other) =>
        Combine(other, Math.Min);

    public MultiplicityProjection Max(MultiplicityProjection other) =>
        Combine(other, Math.Max);

    public bool Equals(MultiplicityProjection? other) =>
        other is not null &&
        string.Equals(RegistryId, other.RegistryId, StringComparison.Ordinal) &&
        Completeness == other.Completeness &&
        PayloadReplayability == other.PayloadReplayability &&
        entries.SequenceEqual(other.entries);

    public override bool Equals(object? obj) => Equals(obj as MultiplicityProjection);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RegistryId, StringComparer.Ordinal);
        hash.Add(Completeness);
        hash.Add(PayloadReplayability);
        foreach (var entry in entries)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }

    internal string Canonical => LineageText.Fields(
        RegistryId,
        Completeness.ToString(),
        PayloadReplayability.ToString(),
        string.Concat(entries.Select(entry => LineageText.Fields(
            entry.Atom.Canonical,
            entry.Count.ToString(CultureInfo.InvariantCulture)))));

    private MultiplicityProjection Combine(
        MultiplicityProjection other,
        Func<int, int, int> operation)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!string.Equals(RegistryId, other.RegistryId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Multiplicity projections from different registry epochs cannot be combined implicitly.");
        }

        var left = entries.ToDictionary(entry => entry.Atom, entry => entry.Count);
        var right = other.entries.ToDictionary(entry => entry.Atom, entry => entry.Count);
        var atoms = left.Keys.Concat(right.Keys).Distinct();
        var result = new List<AtomMultiplicity>();
        foreach (var atom in atoms)
        {
            left.TryGetValue(atom, out var leftCount);
            right.TryGetValue(atom, out var rightCount);
            var count = operation(leftCount, rightCount);
            if (count > 0)
            {
                result.Add(new AtomMultiplicity(atom, count));
            }
        }

        var completeness = Completeness == LineageCompleteness.Exact && other.Completeness == LineageCompleteness.Exact
            ? LineageCompleteness.Exact
            : LineageCompleteness.KnownLowerBound;
        if (Completeness == LineageCompleteness.Conflict || other.Completeness == LineageCompleteness.Conflict)
        {
            completeness = LineageCompleteness.Conflict;
        }

        return new MultiplicityProjection(
            RegistryId,
            result,
            completeness,
            ProjectionKnowledge.CombineReplayability(PayloadReplayability, other.PayloadReplayability));
    }
}

public sealed class RawPrimeProductRepresentation
{
    public static ProjectionContract Contract { get; } = new(
        "RAW_PRIME_PRODUCT_SUPPORT",
        "Active support with declared completeness; exact only when completeness is Exact under one injective prime registry.",
        "Joint-versus-alternative structure, operation history, payload, issuer authenticity, and registry-free meaning.",
        "Carries the embedded support projection's explicit payload-replayability declaration; the product does not prove it.");

    private RawPrimeProductRepresentation(
        string registryId,
        BigInteger product,
        SupportProjection support)
    {
        RegistryId = registryId;
        Product = product;
        Support = support;
        BitLength = product.GetBitLength();
    }

    public string RegistryId { get; }
    public BigInteger Product { get; }
    public long BitLength { get; }
    public SupportProjection Support { get; }
    public PayloadReplayability PayloadReplayability => Support.PayloadReplayability;

    public static RawPrimeProductRepresentation Encode(
        SupportProjection support,
        LineageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(support);
        ArgumentNullException.ThrowIfNull(registry);
        EnsureRegistry(support.RegistryId, registry.RegistryId);
        var product = BigInteger.One;
        foreach (var atom in support.Atoms)
        {
            product *= registry.Get(atom).PrimeLabel;
        }

        return new RawPrimeProductRepresentation(registry.RegistryId, product, support);
    }

    public RawPrimeProductRepresentation Union(
        RawPrimeProductRepresentation other,
        LineageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(registry);
        EnsureRegistry(RegistryId, other.RegistryId);
        EnsureRegistry(RegistryId, registry.RegistryId);
        var divisor = BigInteger.GreatestCommonDivisor(Product, other.Product);
        var product = (Product / divisor) * other.Product;
        return FromValidatedProduct(product, Support.Union(other.Support), registry);
    }

    public RawPrimeProductRepresentation Intersect(
        RawPrimeProductRepresentation other,
        LineageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(registry);
        EnsureRegistry(RegistryId, other.RegistryId);
        EnsureRegistry(RegistryId, registry.RegistryId);
        var product = BigInteger.GreatestCommonDivisor(Product, other.Product);
        return FromValidatedProduct(product, Support.Intersect(other.Support), registry);
    }

    private static RawPrimeProductRepresentation FromValidatedProduct(
        BigInteger product,
        SupportProjection support,
        LineageRegistry registry)
    {
        var encoded = Encode(support, registry);
        if (encoded.Product != product)
        {
            throw new InvalidOperationException("Prime-product algebra disagreed with the explicit support oracle.");
        }

        return encoded;
    }

    private static void EnsureRegistry(string left, string right)
    {
        if (!string.Equals(left, right, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Prime products from different registry epochs cannot be combined implicitly.");
        }
    }
}

public sealed class SparseExponentRepresentation
{
    public static ProjectionContract Contract { get; } = new(
        "SPARSE_ATOM_EXPONENTS",
        "Registered atom identity and nonzero multiplicity with declared completeness; exact only when completeness is Exact.",
        "Joint-versus-alternative grouping, operation history, payload, authenticity, and unregistered atoms.",
        "Carries an explicit payload-replayability declaration; the sparse exponents do not prove it.");

    private readonly AtomMultiplicity[] entries;
    private readonly IReadOnlyList<AtomMultiplicity> readOnlyEntries;

    private SparseExponentRepresentation(MultiplicityProjection projection)
    {
        RegistryId = projection.RegistryId;
        Completeness = projection.Completeness;
        PayloadReplayability = projection.PayloadReplayability;
        entries = projection.Entries.ToArray();
        readOnlyEntries = Array.AsReadOnly(entries);
    }

    public string RegistryId { get; }
    public LineageCompleteness Completeness { get; }
    public PayloadReplayability PayloadReplayability { get; }
    public IReadOnlyList<AtomMultiplicity> Entries => readOnlyEntries;

    public static SparseExponentRepresentation Encode(MultiplicityProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return new SparseExponentRepresentation(projection);
    }

    public static SparseExponentRepresentation EncodeSupport(
        SupportProjection support,
        LineageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(support);
        ArgumentNullException.ThrowIfNull(registry);
        if (!string.Equals(support.RegistryId, registry.RegistryId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The support and registry identities differ.");
        }

        return new SparseExponentRepresentation(MultiplicityProjection.Create(
            registry,
            support.Atoms.Select(atom => new AtomMultiplicity(atom, 1)),
            support.Completeness,
            support.PayloadReplayability));
    }

    public SparseExponentRepresentation Add(
        SparseExponentRepresentation other,
        LineageRegistry registry) =>
        Combine(other, registry, static (left, right) => left.Add(right));

    public SparseExponentRepresentation Min(
        SparseExponentRepresentation other,
        LineageRegistry registry) =>
        Combine(other, registry, static (left, right) => left.Min(right));

    public SparseExponentRepresentation Max(
        SparseExponentRepresentation other,
        LineageRegistry registry) =>
        Combine(other, registry, static (left, right) => left.Max(right));

    public MultiplicityProjection ToProjection(LineageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (!string.Equals(RegistryId, registry.RegistryId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The sparse representation and registry identities differ.");
        }

        return MultiplicityProjection.Create(registry, entries, Completeness, PayloadReplayability);
    }

    private SparseExponentRepresentation Combine(
        SparseExponentRepresentation other,
        LineageRegistry registry,
        Func<MultiplicityProjection, MultiplicityProjection, MultiplicityProjection> operation)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new SparseExponentRepresentation(operation(ToProjection(registry), other.ToProjection(registry)));
    }
}

public sealed class DenseBitSetRepresentation
{
    public static ProjectionContract Contract { get; } = new(
        "DENSE_BINARY_PEV_SUPPORT",
        "Exact active support under a fixed registry when completeness is Exact.",
        "Multiplicity, joint-versus-alternative structure, operation history, payload, authenticity, and cross-epoch meaning.",
        "Carries an explicit payload-replayability declaration; the bitset does not prove it.");

    private readonly ulong[] words;
    private readonly IReadOnlyList<ulong> readOnlyWords;

    private DenseBitSetRepresentation(
        string registryId,
        LineageCompleteness completeness,
        PayloadReplayability payloadReplayability,
        ulong[] words)
    {
        RegistryId = registryId;
        Completeness = completeness;
        PayloadReplayability = payloadReplayability;
        this.words = (ulong[])words.Clone();
        readOnlyWords = Array.AsReadOnly(this.words);
    }

    public string RegistryId { get; }
    public LineageCompleteness Completeness { get; }
    public PayloadReplayability PayloadReplayability { get; }
    public IReadOnlyList<ulong> Words => readOnlyWords;

    public static DenseBitSetRepresentation Encode(
        SupportProjection support,
        LineageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(support);
        ArgumentNullException.ThrowIfNull(registry);
        EnsureRegistry(support.RegistryId, registry.RegistryId);
        var words = new ulong[(registry.UniverseSize + 63) / 64];
        foreach (var atom in support.Atoms)
        {
            var index = registry.Get(atom).BitIndex;
            words[index / 64] |= 1UL << (index % 64);
        }

        return new DenseBitSetRepresentation(
            registry.RegistryId,
            support.Completeness,
            support.PayloadReplayability,
            words);
    }

    public DenseBitSetRepresentation Union(DenseBitSetRepresentation other) =>
        Combine(other, static (left, right) => left | right);

    public DenseBitSetRepresentation Intersect(DenseBitSetRepresentation other) =>
        Combine(other, static (left, right) => left & right);

    public SupportProjection Decode(LineageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        EnsureRegistry(RegistryId, registry.RegistryId);
        var atoms = registry.Registrations
            .Where(registration =>
                (words[registration.BitIndex / 64] & (1UL << (registration.BitIndex % 64))) != 0)
            .Select(registration => registration.Key);
        return SupportProjection.Create(registry, atoms, Completeness, PayloadReplayability);
    }

    private DenseBitSetRepresentation Combine(
        DenseBitSetRepresentation other,
        Func<ulong, ulong, ulong> operation)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureRegistry(RegistryId, other.RegistryId);
        if (words.Length != other.words.Length)
        {
            throw new InvalidOperationException("Dense representations with different widths cannot be combined.");
        }

        var result = new ulong[words.Length];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = operation(words[index], other.words[index]);
        }

        var completeness = Completeness == LineageCompleteness.Exact && other.Completeness == LineageCompleteness.Exact
            ? LineageCompleteness.Exact
            : LineageCompleteness.KnownLowerBound;
        if (Completeness == LineageCompleteness.Conflict || other.Completeness == LineageCompleteness.Conflict)
        {
            completeness = LineageCompleteness.Conflict;
        }

        return new DenseBitSetRepresentation(
            RegistryId,
            completeness,
            ProjectionKnowledge.CombineReplayability(PayloadReplayability, other.PayloadReplayability),
            result);
    }

    private static void EnsureRegistry(string left, string right)
    {
        if (!string.Equals(left, right, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Dense PEVs from different registry epochs cannot be combined implicitly.");
        }
    }
}

internal static class ProjectionKnowledge
{
    public static PayloadReplayability CombineReplayability(
        PayloadReplayability left,
        PayloadReplayability right)
    {
        if (left == PayloadReplayability.UnsupportedTransform || right == PayloadReplayability.UnsupportedTransform)
        {
            return PayloadReplayability.UnsupportedTransform;
        }

        if (left == PayloadReplayability.MissingDependency || right == PayloadReplayability.MissingDependency)
        {
            return PayloadReplayability.MissingDependency;
        }

        return left == PayloadReplayability.ReplayableExact && right == PayloadReplayability.ReplayableExact
            ? PayloadReplayability.ReplayableExact
            : PayloadReplayability.DigestOnly;
    }
}
