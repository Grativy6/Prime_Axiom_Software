using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace PrimeAxiom.Core.Build004.Probes;

public enum ProbeSecurityPropertyState
{
    NotProvided,
}

public sealed record ProbeStructuralSecurityBoundary(
    string CryptographicClassification,
    string PrivacyClassification,
    ProbeSecurityPropertyState AuthenticatedCommitment,
    ProbeSecurityPropertyState MembershipProof,
    ProbeSecurityPropertyState ZeroKnowledgeProof)
{
    public static ProbeStructuralSecurityBoundary TransparentOnly { get; } = new(
        "NOT_CRYPTOGRAPHIC",
        "NO_PRIVACY",
        ProbeSecurityPropertyState.NotProvided,
        ProbeSecurityPropertyState.NotProvided,
        ProbeSecurityPropertyState.NotProvided);
}

/// <summary>
/// Versioned, public element-to-prime assignments. The binding digest detects
/// byte-equivalent registry content; it does not authenticate the registry.
/// </summary>
public sealed class ProbeStructuralPrimeRegistry
{
    private readonly ReadOnlyDictionary<string, BigInteger> _assignments;

    public ProbeStructuralPrimeRegistry(
        string registryId,
        int assignmentEpoch,
        IEnumerable<KeyValuePair<string, BigInteger>> assignments)
    {
        if (string.IsNullOrWhiteSpace(registryId))
        {
            throw new ArgumentException("A registry identity is required.", nameof(registryId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(assignmentEpoch);
        ArgumentNullException.ThrowIfNull(assignments);

        var map = new SortedDictionary<string, BigInteger>(StringComparer.Ordinal);
        var primes = new HashSet<BigInteger>();
        foreach (var (elementId, prime) in assignments)
        {
            if (string.IsNullOrWhiteSpace(elementId))
            {
                throw new ArgumentException("Registry elements require nonempty identities.", nameof(assignments));
            }

            if (!ProbePrimeMath.IsPrime(prime))
            {
                throw new ArgumentException($"Assigned coordinate {prime} is not prime.", nameof(assignments));
            }

            if (!map.TryAdd(elementId, prime))
            {
                throw new ArgumentException($"Duplicate registry element {elementId}.", nameof(assignments));
            }

            if (!primes.Add(prime))
            {
                throw new ArgumentException($"Prime coordinate {prime} was assigned more than once.", nameof(assignments));
            }
        }

        RegistryId = registryId;
        AssignmentEpoch = assignmentEpoch;
        _assignments = new ReadOnlyDictionary<string, BigInteger>(map);
        BindingSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ToCanonicalString())));
    }

    public string RegistryId { get; }

    public int AssignmentEpoch { get; }

    public IReadOnlyDictionary<string, BigInteger> Assignments => _assignments;

    public string BindingSha256 { get; }

    public BigInteger GetPrime(string elementId)
    {
        if (!_assignments.TryGetValue(elementId, out var prime))
        {
            throw new KeyNotFoundException($"Element {elementId} is not assigned in this registry epoch.");
        }

        return prime;
    }

    public string ToCanonicalString() => string.Join(
        '|',
        "schema=PAS-BUILD004-STRUCTURAL-REGISTRY-2",
        $"registry.utf16={EncodeText(RegistryId)}",
        $"epoch={AssignmentEpoch.ToString(CultureInfo.InvariantCulture)}",
        $"assignment-count={_assignments.Count.ToString(CultureInfo.InvariantCulture)}",
        string.Join(';', _assignments.Select(pair => string.Join(
            ',',
            $"element.utf16={EncodeText(pair.Key)}",
            $"prime={pair.Value.ToString(CultureInfo.InvariantCulture)}"))));

    internal bool HasIdenticalBinding(ProbeStructuralPrimeRegistry other) =>
        RegistryId == other.RegistryId &&
        AssignmentEpoch == other.AssignmentEpoch &&
        BindingSha256 == other.BindingSha256;

    private static string EncodeText(string value)
    {
        var encoded = new StringBuilder(value.Length * 4);
        foreach (var codeUnit in value)
        {
            encoded.Append(((ushort)codeUnit).ToString("X4", CultureInfo.InvariantCulture));
        }

        return encoded.ToString();
    }
}

public sealed record ProbeTransparentMembershipResult(
    string ElementId,
    bool IsMember,
    string Method,
    bool MembershipIsPubliclyLeaked,
    ProbeSecurityPropertyState CryptographicMembershipProof);

/// <summary>
/// A deliberately transparent square-free prime product. It demonstrates set
/// membership and leakage only. It is not an RSA accumulator or commitment.
/// </summary>
public sealed class ProbeTransparentStructuralAccumulator
{
    public const string CryptographicClassification = "NOT_CRYPTOGRAPHIC";
    public const string PrivacyClassification = "NO_PRIVACY";
    public const string LeakageStatement =
        "PUBLIC_REGISTRY_PLUS_STRUCTURAL_TOKEN_REVEALS_EACH_REGISTERED_MEMBERSHIP_BY_DIVISIBILITY";

    private readonly ProbeStructuralPrimeRegistry _registry;
    private readonly ReadOnlyCollection<string> _support;

    private ProbeTransparentStructuralAccumulator(
        ProbeStructuralPrimeRegistry registry,
        BigInteger structuralProduct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(structuralProduct, BigInteger.One);

        _registry = registry;
        StructuralProduct = structuralProduct;
        var decoded = registry.Assignments
            .Where(pair => structuralProduct % pair.Value == 0)
            .Select(pair => pair.Key)
            .ToArray();

        var reconstructed = decoded.Aggregate(BigInteger.One, (current, elementId) =>
            current * registry.GetPrime(elementId));
        if (reconstructed != structuralProduct)
        {
            throw new ArgumentException(
                "The structural product contains a factor outside the declared square-free registry.",
                nameof(structuralProduct));
        }

        _support = Array.AsReadOnly(decoded);
        IntegrityDigestSha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(ToCanonicalString())));
    }

    public BigInteger StructuralProduct { get; }

    public string StructuralToken => StructuralProduct.ToString(CultureInfo.InvariantCulture);

    public IReadOnlyList<string> PubliclyDecodableSupport => _support;

    public string IntegrityDigestSha256 { get; }

    public static ProbeStructuralSecurityBoundary SecurityBoundary =>
        ProbeStructuralSecurityBoundary.TransparentOnly;

    public string RegistryId => _registry.RegistryId;

    public int AssignmentEpoch => _registry.AssignmentEpoch;

    public string RegistryBindingSha256 => _registry.BindingSha256;

    public static ProbeTransparentStructuralAccumulator Create(
        ProbeStructuralPrimeRegistry registry,
        IEnumerable<string> elementIds)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(elementIds);
        var unique = new SortedSet<string>(elementIds, StringComparer.Ordinal);
        var product = BigInteger.One;
        foreach (var elementId in unique)
        {
            product *= registry.GetPrime(elementId);
        }

        return new ProbeTransparentStructuralAccumulator(registry, product);
    }

    public ProbeTransparentMembershipResult TestMembership(string elementId)
    {
        var prime = _registry.GetPrime(elementId);
        return new ProbeTransparentMembershipResult(
            elementId,
            StructuralProduct % prime == 0,
            "PUBLIC_EXACT_DIVISIBILITY",
            MembershipIsPubliclyLeaked: true,
            ProbeSecurityPropertyState.NotProvided);
    }

    public ProbeTransparentStructuralAccumulator Union(
        ProbeTransparentStructuralAccumulator other)
    {
        RequireSameRegistry(other);
        var gcd = BigInteger.GreatestCommonDivisor(StructuralProduct, other.StructuralProduct);
        var lcm = StructuralProduct / gcd * other.StructuralProduct;
        return new ProbeTransparentStructuralAccumulator(_registry, lcm);
    }

    public ProbeTransparentStructuralAccumulator Intersect(
        ProbeTransparentStructuralAccumulator other)
    {
        RequireSameRegistry(other);
        return new ProbeTransparentStructuralAccumulator(
            _registry,
            BigInteger.GreatestCommonDivisor(StructuralProduct, other.StructuralProduct));
    }

    public string ToCanonicalString() => string.Join(
        '|',
        "schema=PAS-BUILD004-TRANSPARENT-ACCUMULATOR-2",
        $"registry-binding={_registry.BindingSha256}",
        $"epoch={_registry.AssignmentEpoch.ToString(CultureInfo.InvariantCulture)}",
        $"product={StructuralProduct.ToString(CultureInfo.InvariantCulture)}",
        $"cryptographic={CryptographicClassification}",
        $"privacy={PrivacyClassification}");

    private void RequireSameRegistry(ProbeTransparentStructuralAccumulator other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!_registry.HasIdenticalBinding(other._registry))
        {
            throw new InvalidOperationException(
                "Structural accumulators cannot cross registry identity, binding, or assignment epoch.");
        }
    }
}
