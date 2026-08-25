using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using PrimeAxiom.Core.Build004.Lineage;

namespace PrimeAxiom.Core.Build004.Combinatorics;

public enum CombinatorialValueStatus
{
    ExactZero,
    ExactOne,
    ExactPositive,
}

public enum CombinatorialBoundaryStatus
{
    PrimeCoordinateLocal,
    BinaryMagnitudeLocal,
    AdditiveMagnitudeRequired,
}

public sealed record PrimeCoordinate(int Prime, int Exponent);

/// <summary>
/// A finite signed valuation vector. Positive exponents reconstruct into the numerator and
/// negative exponents into the denominator. The empty vector denotes one, never zero.
/// </summary>
public sealed class SignedPrimeCoordinates : IEquatable<SignedPrimeCoordinates>
{
    public const string UniversalNumericPrimeBasisId =
        "NUMERIC_PRIME_IDENTITY__ALL_POSITIVE_PRIMES__V1";

    public static ProjectionContract Contract { get; } = new(
        "EXACT_COMBINATORIAL_NUMERIC_FACTOR_PROJECTION",
        "The exact positive rational coefficient and every nonzero numeric-prime valuation; the empty vector denotes one.",
        "Zero and negative coefficients, combinatorial inputs, additive-event structure, derivation topology, work history, units, source occurrence identity, evidence, authenticity, authority, and physical realization.",
        "ReplayableExact concerns only the projected positive rational coefficient: the coordinates reconstruct it exactly. It does not claim replay of the combinatorial construction, an additive event, or any external payload.");

    private readonly ReadOnlyCollection<PrimeCoordinate> coordinates;
    private readonly string basisId;
    private readonly LineageCompleteness completeness;
    private readonly PayloadReplayability payloadReplayability;

    internal SignedPrimeCoordinates(IEnumerable<KeyValuePair<int, int>> coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        basisId = UniversalNumericPrimeBasisId;
        completeness = LineageCompleteness.Exact;
        payloadReplayability = PayloadReplayability.ReplayableExact;
        var frozen = coordinates
            .Where(entry => entry.Value != 0)
            .OrderBy(entry => entry.Key)
            .Select(entry => new PrimeCoordinate(entry.Key, entry.Value))
            .ToArray();

        if (frozen.Any(entry => entry.Prime < 2))
        {
            throw new ArgumentOutOfRangeException(nameof(coordinates), "Prime-coordinate keys must be at least two.");
        }

        for (var index = 1; index < frozen.Length; index++)
        {
            if (frozen[index - 1].Prime >= frozen[index].Prime)
            {
                throw new ArgumentException("Prime-coordinate keys must be unique and strictly increasing.", nameof(coordinates));
            }
        }

        this.coordinates = Array.AsReadOnly(frozen);
    }

    public IReadOnlyList<PrimeCoordinate> Coordinates => coordinates;

    /// <summary>
    /// Numeric primes identify their own universal coordinate lanes. This identity is not the
    /// finite, implementation-local prime table retained by a <see cref="PrimeCombinatorics"/> context.
    /// </summary>
    public string BasisId => basisId;

    /// <summary>
    /// Instances are emitted only after exact valuation construction; unknown factors are not
    /// represented by omitted zero lanes.
    /// </summary>
    public LineageCompleteness Completeness => completeness;

    /// <summary>
    /// The projected positive rational coefficient can be reconstructed exactly. This declaration
    /// does not make the discarded combinatorial inputs or derivation history replayable.
    /// </summary>
    public PayloadReplayability PayloadReplayability => payloadReplayability;

    public int Count => coordinates.Count;

    public bool IsIntegral => coordinates.All(entry => entry.Exponent >= 0);

    public int GetExponent(int prime)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(prime, 2);
        foreach (var coordinate in coordinates)
        {
            if (coordinate.Prime == prime)
            {
                return coordinate.Exponent;
            }

            if (coordinate.Prime > prime)
            {
                break;
            }
        }

        return 0;
    }

    public bool Equals(SignedPrimeCoordinates? other) =>
        other is not null &&
        string.Equals(BasisId, other.BasisId, StringComparison.Ordinal) &&
        Completeness == other.Completeness &&
        PayloadReplayability == other.PayloadReplayability &&
        coordinates.SequenceEqual(other.coordinates);

    public override bool Equals(object? obj) => Equals(obj as SignedPrimeCoordinates);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BasisId, StringComparer.Ordinal);
        hash.Add(Completeness);
        hash.Add(PayloadReplayability);
        foreach (var coordinate in coordinates)
        {
            hash.Add(coordinate);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Canonical semantic form used by receipt identities. Unlike <see cref="ToString"/>, this
    /// binds the projection contract, basis, completeness, and replayability declarations.
    /// </summary>
    public string ToCanonicalString()
    {
        var fields = new List<string>
        {
            Contract.Name,
            BasisId,
            Completeness.ToString(),
            PayloadReplayability.ToString(),
            coordinates.Count.ToString(CultureInfo.InvariantCulture),
        };
        foreach (var coordinate in coordinates)
        {
            fields.Add(coordinate.Prime.ToString(CultureInfo.InvariantCulture));
            fields.Add(coordinate.Exponent.ToString(CultureInfo.InvariantCulture));
        }

        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            builder.Append(field.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(field);
        }

        return builder.ToString();
    }

    public override string ToString() => coordinates.Count == 0
        ? "1"
        : string.Join(
            " * ",
            coordinates.Select(entry => entry.Exponent == 1
                ? entry.Prime.ToString(CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture, $"{entry.Prime}^{entry.Exponent}")));
}

public sealed record CombinatorialWork(
    long PrimeBasisCandidates = 0,
    long PrimeBasisCompositeMarks = 0,
    long PrimeBasisPrimes = 0,
    long FactorialCacheHits = 0,
    long FactorialCacheMisses = 0,
    long FactorialValuationCalls = 0,
    long LegendreQuotientSteps = 0,
    long CoordinateReads = 0,
    long CoordinateWrites = 0,
    long CoordinateAdditions = 0,
    long CoordinateZeroEliminations = 0,
    long BigIntegerPowers = 0,
    long BigIntegerMultiplications = 0,
    long BigIntegerExactDivisions = 0,
    long BigIntegerAdditions = 0,
    long GreatestCommonDivisors = 0,
    long Reconstructions = 0,
    long AdditiveNodes = 0,
    long AdditiveTerms = 0,
    long ExactRationalReductions = 0)
{
    public static CombinatorialWork Zero { get; } = new();

    public static CombinatorialWork operator +(CombinatorialWork left, CombinatorialWork right) =>
        new(
            checked(left.PrimeBasisCandidates + right.PrimeBasisCandidates),
            checked(left.PrimeBasisCompositeMarks + right.PrimeBasisCompositeMarks),
            checked(left.PrimeBasisPrimes + right.PrimeBasisPrimes),
            checked(left.FactorialCacheHits + right.FactorialCacheHits),
            checked(left.FactorialCacheMisses + right.FactorialCacheMisses),
            checked(left.FactorialValuationCalls + right.FactorialValuationCalls),
            checked(left.LegendreQuotientSteps + right.LegendreQuotientSteps),
            checked(left.CoordinateReads + right.CoordinateReads),
            checked(left.CoordinateWrites + right.CoordinateWrites),
            checked(left.CoordinateAdditions + right.CoordinateAdditions),
            checked(left.CoordinateZeroEliminations + right.CoordinateZeroEliminations),
            checked(left.BigIntegerPowers + right.BigIntegerPowers),
            checked(left.BigIntegerMultiplications + right.BigIntegerMultiplications),
            checked(left.BigIntegerExactDivisions + right.BigIntegerExactDivisions),
            checked(left.BigIntegerAdditions + right.BigIntegerAdditions),
            checked(left.GreatestCommonDivisors + right.GreatestCommonDivisors),
            checked(left.Reconstructions + right.Reconstructions),
            checked(left.AdditiveNodes + right.AdditiveNodes),
            checked(left.AdditiveTerms + right.AdditiveTerms),
            checked(left.ExactRationalReductions + right.ExactRationalReductions));
}

public sealed class FactorialReceipt
{
    internal FactorialReceipt(
        string receiptId,
        int n,
        SignedPrimeCoordinates coordinates,
        BigInteger value,
        CombinatorialWork work,
        CombinatorialBoundaryStatus boundary,
        string algorithm,
        string claimCeiling)
    {
        ReceiptId = receiptId;
        N = n;
        Coordinates = coordinates;
        Value = value;
        Work = work;
        Boundary = boundary;
        Algorithm = algorithm;
        ClaimCeiling = claimCeiling;
    }

    public string ReceiptId { get; }
    public int N { get; }
    public SignedPrimeCoordinates Coordinates { get; }
    public BigInteger Value { get; }
    public CombinatorialWork Work { get; }
    public CombinatorialBoundaryStatus Boundary { get; }
    public string Algorithm { get; }
    public string ClaimCeiling { get; }
}

public sealed class BinomialReceipt
{
    internal BinomialReceipt(
        string receiptId,
        int n,
        int k,
        SignedPrimeCoordinates coordinates,
        BigInteger value,
        CombinatorialWork work,
        CombinatorialBoundaryStatus boundary,
        string algorithm,
        string claimCeiling)
    {
        ReceiptId = receiptId;
        N = n;
        K = k;
        Coordinates = coordinates;
        Value = value;
        Work = work;
        Boundary = boundary;
        Algorithm = algorithm;
        ClaimCeiling = claimCeiling;
    }

    public string ReceiptId { get; }
    public int N { get; }
    public int K { get; }
    public SignedPrimeCoordinates Coordinates { get; }
    public BigInteger Value { get; }
    public CombinatorialWork Work { get; }
    public CombinatorialBoundaryStatus Boundary { get; }
    public string Algorithm { get; }
    public string ClaimCeiling { get; }
}

public sealed class HypergeometricPointReceipt
{
    internal HypergeometricPointReceipt(
        string receiptId,
        int population,
        int successStates,
        int draws,
        int observedSuccesses,
        int supportMinimum,
        int supportMaximum,
        CombinatorialValueStatus status,
        SignedPrimeCoordinates coordinates,
        ExactRational probability,
        CombinatorialWork work,
        CombinatorialBoundaryStatus boundary,
        string algorithm,
        string claimCeiling)
    {
        ReceiptId = receiptId;
        Population = population;
        SuccessStates = successStates;
        Draws = draws;
        ObservedSuccesses = observedSuccesses;
        SupportMinimum = supportMinimum;
        SupportMaximum = supportMaximum;
        Status = status;
        Coordinates = coordinates;
        Probability = probability;
        Work = work;
        Boundary = boundary;
        Algorithm = algorithm;
        ClaimCeiling = claimCeiling;
    }

    public string ReceiptId { get; }
    public int Population { get; }
    public int SuccessStates { get; }
    public int Draws { get; }
    public int ObservedSuccesses { get; }
    public int SupportMinimum { get; }
    public int SupportMaximum { get; }
    public CombinatorialValueStatus Status { get; }
    public SignedPrimeCoordinates Coordinates { get; }
    public ExactRational Probability { get; }
    public CombinatorialWork Work { get; }
    public CombinatorialBoundaryStatus Boundary { get; }
    public string Algorithm { get; }
    public string ClaimCeiling { get; }
}

public sealed class HypergeometricEventReceipt
{
    private readonly ReadOnlyCollection<int> includedObservations;
    private readonly ReadOnlyCollection<HypergeometricPointReceipt> pointReceipts;

    internal HypergeometricEventReceipt(
        string receiptId,
        int population,
        int successStates,
        int draws,
        IEnumerable<int> includedObservations,
        IEnumerable<HypergeometricPointReceipt> pointReceipts,
        ExactRational probability,
        CombinatorialValueStatus status,
        bool primeCoordinatesAvailable,
        CombinatorialWork work,
        CombinatorialBoundaryStatus boundary,
        string algorithm,
        string claimCeiling)
    {
        ReceiptId = receiptId;
        Population = population;
        SuccessStates = successStates;
        Draws = draws;
        this.includedObservations = Array.AsReadOnly(includedObservations.ToArray());
        this.pointReceipts = Array.AsReadOnly(pointReceipts.ToArray());
        Probability = probability;
        Status = status;
        PrimeCoordinatesAvailable = primeCoordinatesAvailable;
        Work = work;
        Boundary = boundary;
        Algorithm = algorithm;
        ClaimCeiling = claimCeiling;
    }

    public string ReceiptId { get; }
    public int Population { get; }
    public int SuccessStates { get; }
    public int Draws { get; }
    public IReadOnlyList<int> IncludedObservations => includedObservations;
    public IReadOnlyList<HypergeometricPointReceipt> PointReceipts => pointReceipts;
    public ExactRational Probability { get; }
    public CombinatorialValueStatus Status { get; }
    public bool PrimeCoordinatesAvailable { get; }
    public CombinatorialWork Work { get; }
    public CombinatorialBoundaryStatus Boundary { get; }
    public string Algorithm { get; }
    public string ClaimCeiling { get; }
}

public sealed record OrdinaryIntegerResult(BigInteger Value, CombinatorialWork Work, string Algorithm);

public sealed record OrdinaryProbabilityResult(
    ExactRational Probability,
    CombinatorialWork Work,
    CombinatorialBoundaryStatus Boundary,
    string Algorithm);

public sealed record AdjacentHypergeometricSeries(
    int SupportMinimum,
    int SupportMaximum,
    IReadOnlyList<ExactRational> Probabilities,
    CombinatorialWork Work,
    string Algorithm);

internal static class CombinatorialIdentity
{
    public static string Create(string canonicalPayload)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload));
        return Convert.ToHexString(digest);
    }

    public static string Coordinates(SignedPrimeCoordinates coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        return coordinates.ToCanonicalString();
    }
}

internal sealed class CombinatorialWorkCounter
{
    public long PrimeBasisCandidates { get; set; }
    public long PrimeBasisCompositeMarks { get; set; }
    public long PrimeBasisPrimes { get; set; }
    public long FactorialCacheHits { get; set; }
    public long FactorialCacheMisses { get; set; }
    public long FactorialValuationCalls { get; set; }
    public long LegendreQuotientSteps { get; set; }
    public long CoordinateReads { get; set; }
    public long CoordinateWrites { get; set; }
    public long CoordinateAdditions { get; set; }
    public long CoordinateZeroEliminations { get; set; }
    public long BigIntegerPowers { get; set; }
    public long BigIntegerMultiplications { get; set; }
    public long BigIntegerExactDivisions { get; set; }
    public long BigIntegerAdditions { get; set; }
    public long GreatestCommonDivisors { get; set; }
    public long Reconstructions { get; set; }
    public long AdditiveNodes { get; set; }
    public long AdditiveTerms { get; set; }
    public long ExactRationalReductions { get; set; }

    public void Add(CombinatorialWork work)
    {
        PrimeBasisCandidates = checked(PrimeBasisCandidates + work.PrimeBasisCandidates);
        PrimeBasisCompositeMarks = checked(PrimeBasisCompositeMarks + work.PrimeBasisCompositeMarks);
        PrimeBasisPrimes = checked(PrimeBasisPrimes + work.PrimeBasisPrimes);
        FactorialCacheHits = checked(FactorialCacheHits + work.FactorialCacheHits);
        FactorialCacheMisses = checked(FactorialCacheMisses + work.FactorialCacheMisses);
        FactorialValuationCalls = checked(FactorialValuationCalls + work.FactorialValuationCalls);
        LegendreQuotientSteps = checked(LegendreQuotientSteps + work.LegendreQuotientSteps);
        CoordinateReads = checked(CoordinateReads + work.CoordinateReads);
        CoordinateWrites = checked(CoordinateWrites + work.CoordinateWrites);
        CoordinateAdditions = checked(CoordinateAdditions + work.CoordinateAdditions);
        CoordinateZeroEliminations = checked(CoordinateZeroEliminations + work.CoordinateZeroEliminations);
        BigIntegerPowers = checked(BigIntegerPowers + work.BigIntegerPowers);
        BigIntegerMultiplications = checked(BigIntegerMultiplications + work.BigIntegerMultiplications);
        BigIntegerExactDivisions = checked(BigIntegerExactDivisions + work.BigIntegerExactDivisions);
        BigIntegerAdditions = checked(BigIntegerAdditions + work.BigIntegerAdditions);
        GreatestCommonDivisors = checked(GreatestCommonDivisors + work.GreatestCommonDivisors);
        Reconstructions = checked(Reconstructions + work.Reconstructions);
        AdditiveNodes = checked(AdditiveNodes + work.AdditiveNodes);
        AdditiveTerms = checked(AdditiveTerms + work.AdditiveTerms);
        ExactRationalReductions = checked(ExactRationalReductions + work.ExactRationalReductions);
    }

    public CombinatorialWork Snapshot() =>
        new(
            PrimeBasisCandidates,
            PrimeBasisCompositeMarks,
            PrimeBasisPrimes,
            FactorialCacheHits,
            FactorialCacheMisses,
            FactorialValuationCalls,
            LegendreQuotientSteps,
            CoordinateReads,
            CoordinateWrites,
            CoordinateAdditions,
            CoordinateZeroEliminations,
            BigIntegerPowers,
            BigIntegerMultiplications,
            BigIntegerExactDivisions,
            BigIntegerAdditions,
            GreatestCommonDivisors,
            Reconstructions,
            AdditiveNodes,
            AdditiveTerms,
            ExactRationalReductions);
}

internal static class HypergeometricDomain
{
    public static (int Minimum, int Maximum) ValidateAndGetSupport(
        int population,
        int successStates,
        int draws)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(population);
        if (successStates < 0 || successStates > population)
        {
            throw new ArgumentOutOfRangeException(
                nameof(successStates),
                "Success states must be between zero and the population size.");
        }

        if (draws < 0 || draws > population)
        {
            throw new ArgumentOutOfRangeException(
                nameof(draws),
                "Draws must be between zero and the population size.");
        }

        return (Math.Max(0, draws - (population - successStates)), Math.Min(draws, successStates));
    }

    public static CombinatorialValueStatus Status(ExactRational value) => value.IsZero
        ? CombinatorialValueStatus.ExactZero
        : value.IsOne
            ? CombinatorialValueStatus.ExactOne
            : CombinatorialValueStatus.ExactPositive;
}
