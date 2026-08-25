using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;

namespace PrimeAxiom.Core.Build004.Combinatorics;

/// <summary>
/// Exact factorial-ratio construction over a declared finite prime basis. The implementation
/// derives valuations from combinatorial parameters; it never factors a completed factorial,
/// binomial coefficient, or probability magnitude.
/// </summary>
public sealed class PrimeCombinatorics
{
    public const int MaximumSupportedArgument = 1_000_000;
    public const string ProtocolId = "PAS-BUILD004-EXACT-LINEAGE-0001";
    public const string FactorialAlgorithm = "LEGENDRE_FACTORIAL_VECTOR_V1";
    public const string BinomialAlgorithm = "SIGNED_FACTORIAL_RATIO_V1";
    public const string HypergeometricPointAlgorithm = "SIGNED_HYPERGEOMETRIC_RATIO_V1";
    public const string HypergeometricEventAlgorithm = "EXACT_ADDITIVE_EVENT_SUM_V1";

    private const string MultiplicativeClaimCeiling =
        "Exact under the declared finite argument bound using Legendre valuations; no completed magnitude was factored and no performance or hardware advantage is claimed.";

    private const string AdditiveClaimCeiling =
        "The exact event preserves its point receipts but crosses to rational addition; it has no exponent-merge result and earns no performance or hardware claim.";

    private readonly int[] primes;
    private readonly ReadOnlyCollection<int> readOnlyPrimes;
    private readonly Dictionary<int, SignedPrimeCoordinates> factorialCache = new();

    public PrimeCombinatorics(int maximumN)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumN);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumN, MaximumSupportedArgument);
        MaximumN = maximumN;
        var basisCounter = new CombinatorialWorkCounter();
        primes = BuildPrimeBasis(maximumN, basisCounter);
        readOnlyPrimes = Array.AsReadOnly(primes);
        BasisWork = basisCounter.Snapshot();
    }

    public int MaximumN { get; }

    public IReadOnlyList<int> PrimeBasis => readOnlyPrimes;

    public CombinatorialWork BasisWork { get; }

    public int CachedFactorialCount => factorialCache.Count;

    public void ClearFactorialCache() => factorialCache.Clear();

    public FactorialReceipt Factorial(int n)
    {
        ValidateArgument(n, nameof(n));
        var counter = new CombinatorialWorkCounter();
        var coordinates = GetFactorialCoordinates(n, counter);
        var value = Reconstruct(coordinates, counter);
        var work = counter.Snapshot();
        var receiptId = CombinatorialIdentity.Create(string.Create(
            CultureInfo.InvariantCulture,
            $"{ProtocolId}|factorial|{n}|{CombinatorialIdentity.Coordinates(coordinates)}|{value}"));

        return new FactorialReceipt(
            receiptId,
            n,
            coordinates,
            value.Numerator,
            work,
            CombinatorialBoundaryStatus.PrimeCoordinateLocal,
            FactorialAlgorithm,
            MultiplicativeClaimCeiling);
    }

    public BinomialReceipt Binomial(int n, int k)
    {
        ValidateBinomial(n, k);
        var counter = new CombinatorialWorkCounter();
        var coordinates = ComposeFactorials(
            new[] { (n, 1), (k, -1), (n - k, -1) },
            counter);

        if (!coordinates.IsIntegral)
        {
            throw new InvalidOperationException("A binomial coefficient produced a negative prime exponent.");
        }

        var reconstructed = Reconstruct(coordinates, counter);
        if (!reconstructed.Denominator.IsOne)
        {
            throw new InvalidOperationException("A binomial coefficient did not reconstruct as an integer.");
        }

        var work = counter.Snapshot();
        var receiptId = CombinatorialIdentity.Create(string.Create(
            CultureInfo.InvariantCulture,
            $"{ProtocolId}|binomial|{n}|{k}|{CombinatorialIdentity.Coordinates(coordinates)}|{reconstructed.Numerator}"));

        return new BinomialReceipt(
            receiptId,
            n,
            k,
            coordinates,
            reconstructed.Numerator,
            work,
            CombinatorialBoundaryStatus.PrimeCoordinateLocal,
            BinomialAlgorithm,
            MultiplicativeClaimCeiling);
    }

    public HypergeometricPointReceipt HypergeometricPoint(
        int population,
        int successStates,
        int draws,
        int observedSuccesses)
    {
        ValidateContextBound(population, nameof(population));
        var support = HypergeometricDomain.ValidateAndGetSupport(population, successStates, draws);

        if (observedSuccesses < support.Minimum || observedSuccesses > support.Maximum)
        {
            var zeroCoordinates = new SignedPrimeCoordinates(Array.Empty<KeyValuePair<int, int>>());
            var zeroReceiptId = CombinatorialIdentity.Create(string.Create(
                CultureInfo.InvariantCulture,
                $"{ProtocolId}|hypergeometric-point|{population}|{successStates}|{draws}|{observedSuccesses}|zero"));

            return new HypergeometricPointReceipt(
                zeroReceiptId,
                population,
                successStates,
                draws,
                observedSuccesses,
                support.Minimum,
                support.Maximum,
                CombinatorialValueStatus.ExactZero,
                zeroCoordinates,
                ExactRational.Zero,
                CombinatorialWork.Zero,
                CombinatorialBoundaryStatus.PrimeCoordinateLocal,
                HypergeometricPointAlgorithm,
                MultiplicativeClaimCeiling);
        }

        var x = observedSuccesses;
        var counter = new CombinatorialWorkCounter();
        var coordinates = ComposeFactorials(
            new[]
            {
                (successStates, 1),
                (population - successStates, 1),
                (draws, 1),
                (population - draws, 1),
                (x, -1),
                (successStates - x, -1),
                (draws - x, -1),
                (population - successStates - draws + x, -1),
                (population, -1),
            },
            counter);
        var probability = Reconstruct(coordinates, counter);

        if (probability.Sign <= 0 || probability > ExactRational.One)
        {
            throw new InvalidOperationException("An in-support hypergeometric probability was outside (0, 1].");
        }

        var status = HypergeometricDomain.Status(probability);
        var work = counter.Snapshot();
        var receiptId = CombinatorialIdentity.Create(string.Create(
            CultureInfo.InvariantCulture,
            $"{ProtocolId}|hypergeometric-point|{population}|{successStates}|{draws}|{x}|{status}|{CombinatorialIdentity.Coordinates(coordinates)}|{probability}"));

        return new HypergeometricPointReceipt(
            receiptId,
            population,
            successStates,
            draws,
            x,
            support.Minimum,
            support.Maximum,
            status,
            coordinates,
            probability,
            work,
            CombinatorialBoundaryStatus.PrimeCoordinateLocal,
            HypergeometricPointAlgorithm,
            MultiplicativeClaimCeiling);
    }

    public HypergeometricEventReceipt HypergeometricEvent(
        int population,
        int successStates,
        int draws,
        IEnumerable<int> includedObservations)
    {
        ArgumentNullException.ThrowIfNull(includedObservations);
        ValidateContextBound(population, nameof(population));
        _ = HypergeometricDomain.ValidateAndGetSupport(population, successStates, draws);

        var observations = includedObservations.Distinct().Order().ToArray();
        var points = new HypergeometricPointReceipt[observations.Length];
        var counter = new CombinatorialWorkCounter
        {
            AdditiveNodes = 1,
            AdditiveTerms = observations.LongLength,
        };
        var probability = ExactRational.Zero;

        for (var index = 0; index < observations.Length; index++)
        {
            var point = HypergeometricPoint(population, successStates, draws, observations[index]);
            points[index] = point;
            counter.Add(point.Work);
            probability = MeasuredExactRational.Add(probability, point.Probability, counter);
        }

        if (probability < ExactRational.Zero || probability > ExactRational.One)
        {
            throw new InvalidOperationException("A hypergeometric event probability was outside [0, 1].");
        }

        var frozenObservations = Array.AsReadOnly(observations);
        var frozenPoints = Array.AsReadOnly(points);
        var status = HypergeometricDomain.Status(probability);
        var receiptId = CombinatorialIdentity.Create(string.Create(
            CultureInfo.InvariantCulture,
            $"{ProtocolId}|hypergeometric-event|{population}|{successStates}|{draws}|{string.Join(',', observations)}|{string.Join(',', points.Select(point => point.ReceiptId))}|{probability}|additive"));

        return new HypergeometricEventReceipt(
            receiptId,
            population,
            successStates,
            draws,
            frozenObservations,
            frozenPoints,
            probability,
            status,
            primeCoordinatesAvailable: false,
            counter.Snapshot(),
            CombinatorialBoundaryStatus.AdditiveMagnitudeRequired,
            HypergeometricEventAlgorithm,
            AdditiveClaimCeiling);
    }

    public HypergeometricEventReceipt HypergeometricRange(
        int population,
        int successStates,
        int draws,
        int minimumObservedSuccesses,
        int maximumObservedSuccesses)
    {
        if (minimumObservedSuccesses > maximumObservedSuccesses)
        {
            throw new ArgumentException("The event range minimum cannot exceed its maximum.");
        }

        return HypergeometricEvent(
            population,
            successStates,
            draws,
            Enumerable.Range(
                minimumObservedSuccesses,
                checked(maximumObservedSuccesses - minimumObservedSuccesses + 1)));
    }

    private static int[] BuildPrimeBasis(int maximumN, CombinatorialWorkCounter counter)
    {
        if (maximumN < 2)
        {
            return Array.Empty<int>();
        }

        var composite = new bool[checked(maximumN + 1)];
        counter.PrimeBasisCandidates = maximumN - 1L;
        for (var prime = 2; prime <= maximumN / prime; prime++)
        {
            if (composite[prime])
            {
                continue;
            }

            for (var multiple = prime * prime; multiple <= maximumN; multiple += prime)
            {
                composite[multiple] = true;
                counter.PrimeBasisCompositeMarks++;
            }
        }

        var result = new List<int>();
        for (var candidate = 2; candidate <= maximumN; candidate++)
        {
            if (!composite[candidate])
            {
                result.Add(candidate);
            }
        }

        counter.PrimeBasisPrimes = result.Count;
        return result.ToArray();
    }

    private SignedPrimeCoordinates GetFactorialCoordinates(int n, CombinatorialWorkCounter counter)
    {
        if (factorialCache.TryGetValue(n, out var cached))
        {
            counter.FactorialCacheHits++;
            return cached;
        }

        counter.FactorialCacheMisses++;
        var coordinates = new List<KeyValuePair<int, int>>();
        foreach (var prime in primes)
        {
            if (prime > n)
            {
                break;
            }

            counter.FactorialValuationCalls++;
            var quotient = n;
            var exponent = 0;
            while (quotient >= prime)
            {
                quotient /= prime;
                exponent = checked(exponent + quotient);
                counter.LegendreQuotientSteps++;
            }

            coordinates.Add(new KeyValuePair<int, int>(prime, exponent));
            counter.CoordinateWrites++;
        }

        var result = new SignedPrimeCoordinates(coordinates);
        factorialCache.Add(n, result);
        return result;
    }

    private SignedPrimeCoordinates ComposeFactorials(
        IEnumerable<(int FactorialArgument, int Sign)> terms,
        CombinatorialWorkCounter counter)
    {
        var exponents = new SortedDictionary<int, int>();
        foreach (var (argument, sign) in terms)
        {
            if (sign is not (1 or -1))
            {
                throw new ArgumentOutOfRangeException(nameof(terms), "Factorial term signs must be +1 or -1.");
            }

            var factorial = GetFactorialCoordinates(argument, counter);
            foreach (var coordinate in factorial.Coordinates)
            {
                counter.CoordinateReads++;
                exponents.TryGetValue(coordinate.Prime, out var current);
                if (current != 0)
                {
                    counter.CoordinateAdditions++;
                }

                var updated = checked(current + (sign * coordinate.Exponent));
                if (updated == 0)
                {
                    if (current != 0)
                    {
                        exponents.Remove(coordinate.Prime);
                        counter.CoordinateZeroEliminations++;
                    }

                    continue;
                }

                exponents[coordinate.Prime] = updated;
                counter.CoordinateWrites++;
            }
        }

        return new SignedPrimeCoordinates(exponents);
    }

    private static ExactRational Reconstruct(
        SignedPrimeCoordinates coordinates,
        CombinatorialWorkCounter counter)
    {
        var numerator = BigInteger.One;
        var denominator = BigInteger.One;
        foreach (var coordinate in coordinates.Coordinates)
        {
            var magnitude = BigInteger.Pow(coordinate.Prime, Math.Abs(coordinate.Exponent));
            counter.BigIntegerPowers++;
            if (coordinate.Exponent > 0)
            {
                numerator *= magnitude;
            }
            else
            {
                denominator *= magnitude;
            }

            counter.BigIntegerMultiplications++;
        }

        counter.Reconstructions++;
        return ExactRational.CreateCanonical(numerator, denominator);
    }

    private void ValidateBinomial(int n, int k)
    {
        ValidateArgument(n, nameof(n));
        if (k < 0 || k > n)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "K must be between zero and N.");
        }
    }

    private void ValidateArgument(int value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
        ValidateContextBound(value, parameterName);
    }

    private void ValidateContextBound(int value, string parameterName)
    {
        if (value > MaximumN)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The value {value} exceeds this context's explicit maximum {MaximumN}."));
        }
    }
}

internal static class MeasuredExactRational
{
    public static ExactRational Add(
        ExactRational left,
        ExactRational right,
        CombinatorialWorkCounter counter)
    {
        var common = BigInteger.GreatestCommonDivisor(left.Denominator, right.Denominator);
        counter.GreatestCommonDivisors++;
        var leftScale = right.Denominator / common;
        var rightScale = left.Denominator / common;
        counter.BigIntegerExactDivisions += 2;

        var numerator = (left.Numerator * leftScale) + (right.Numerator * rightScale);
        counter.BigIntegerMultiplications += 2;
        counter.BigIntegerAdditions++;
        if (numerator.IsZero)
        {
            return ExactRational.Zero;
        }

        var residual = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), common);
        counter.GreatestCommonDivisors++;
        var denominator = left.Denominator * leftScale;
        counter.BigIntegerMultiplications++;
        if (!residual.IsOne)
        {
            numerator /= residual;
            denominator /= residual;
            counter.BigIntegerExactDivisions += 2;
            counter.ExactRationalReductions++;
        }

        return ExactRational.CreateCanonical(numerator, denominator);
    }

    public static ExactRational MultiplyByFraction(
        ExactRational value,
        BigInteger numeratorFactor,
        BigInteger denominatorFactor,
        CombinatorialWorkCounter counter)
    {
        if (denominatorFactor.Sign <= 0 || numeratorFactor.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(denominatorFactor),
                "Adjacent hypergeometric recurrence factors must be nonnegative over positive denominators.");
        }

        if (value.IsZero || numeratorFactor.IsZero)
        {
            return ExactRational.Zero;
        }

        var localCancellation = BigInteger.GreatestCommonDivisor(numeratorFactor, denominatorFactor);
        counter.GreatestCommonDivisors++;

        if (!localCancellation.IsOne)
        {
            numeratorFactor /= localCancellation;
            denominatorFactor /= localCancellation;
            counter.BigIntegerExactDivisions += 2;
        }

        // Cross-cancel only after reducing the recurrence fraction itself. This keeps the
        // internal known-canonical constructor honest rather than relying on a hidden GCD.
        var firstCancellation = BigInteger.GreatestCommonDivisor(
            BigInteger.Abs(value.Numerator),
            denominatorFactor);
        var secondCancellation = BigInteger.GreatestCommonDivisor(
            numeratorFactor,
            value.Denominator);
        counter.GreatestCommonDivisors += 2;

        var valueNumerator = value.Numerator;
        var valueDenominator = value.Denominator;
        if (!firstCancellation.IsOne)
        {
            valueNumerator /= firstCancellation;
            denominatorFactor /= firstCancellation;
            counter.BigIntegerExactDivisions += 2;
        }

        if (!secondCancellation.IsOne)
        {
            numeratorFactor /= secondCancellation;
            valueDenominator /= secondCancellation;
            counter.BigIntegerExactDivisions += 2;
        }

        counter.BigIntegerMultiplications += 2;
        return ExactRational.CreateCanonical(
            valueNumerator * numeratorFactor,
            valueDenominator * denominatorFactor);
    }
}
