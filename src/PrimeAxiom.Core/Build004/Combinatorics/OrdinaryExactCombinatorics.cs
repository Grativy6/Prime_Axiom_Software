using System.Collections.ObjectModel;
using System.Numerics;

namespace PrimeAxiom.Core.Build004.Combinatorics;

/// <summary>
/// Independent ordinary-magnitude controls. These algorithms use cross-cancelled products and
/// adjacent recurrence; they never materialize factorials.
/// </summary>
public static class OrdinaryExactCombinatorics
{
    public const string BinomialAlgorithm = "BIGINTEGER_CROSS_CANCEL_BINOMIAL_V1";
    public const string HypergeometricPointAlgorithm = "BIGINTEGER_EXACT_HYPERGEOMETRIC_POINT_V1";
    public const string AdjacentStreamAlgorithm = "BIGINTEGER_ADJACENT_HYPERGEOMETRIC_V1";
    public const string EventAlgorithm = "BIGINTEGER_ADJACENT_EVENT_SUM_V1";

    public static OrdinaryIntegerResult Binomial(int n, int k)
    {
        ValidateBinomial(n, k);
        var counter = new CombinatorialWorkCounter();
        var value = BinomialCore(n, k, counter);
        return new OrdinaryIntegerResult(value, counter.Snapshot(), BinomialAlgorithm);
    }

    public static OrdinaryProbabilityResult HypergeometricPoint(
        int population,
        int successStates,
        int draws,
        int observedSuccesses)
    {
        var support = HypergeometricDomain.ValidateAndGetSupport(population, successStates, draws);
        if (observedSuccesses < support.Minimum || observedSuccesses > support.Maximum)
        {
            return new OrdinaryProbabilityResult(
                ExactRational.Zero,
                CombinatorialWork.Zero,
                CombinatorialBoundaryStatus.BinaryMagnitudeLocal,
                HypergeometricPointAlgorithm);
        }

        var counter = new CombinatorialWorkCounter();
        var first = BinomialCore(successStates, observedSuccesses, counter);
        var second = BinomialCore(
            population - successStates,
            draws - observedSuccesses,
            counter);
        var denominator = BinomialCore(population, draws, counter);
        var numerator = first * second;
        counter.BigIntegerMultiplications++;

        var divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
        counter.GreatestCommonDivisors++;
        if (!divisor.IsOne)
        {
            numerator /= divisor;
            denominator /= divisor;
            counter.BigIntegerExactDivisions += 2;
        }

        var probability = ExactRational.CreateCanonical(numerator, denominator);
        return new OrdinaryProbabilityResult(
            probability,
            counter.Snapshot(),
            CombinatorialBoundaryStatus.BinaryMagnitudeLocal,
            HypergeometricPointAlgorithm);
    }

    public static AdjacentHypergeometricSeries HypergeometricStream(
        int population,
        int successStates,
        int draws)
    {
        var support = HypergeometricDomain.ValidateAndGetSupport(population, successStates, draws);
        var initial = HypergeometricPoint(population, successStates, draws, support.Minimum);
        var counter = new CombinatorialWorkCounter();
        counter.Add(initial.Work);
        var probabilities = new ExactRational[checked(support.Maximum - support.Minimum + 1)];
        probabilities[0] = initial.Probability;

        for (var x = support.Minimum; x < support.Maximum; x++)
        {
            var numeratorFactor =
                new BigInteger(successStates - x) * new BigInteger(draws - x);
            var denominatorFactor =
                new BigInteger(x + 1) *
                new BigInteger(population - successStates - draws + x + 1);
            counter.BigIntegerMultiplications += 2;
            probabilities[x - support.Minimum + 1] = MeasuredExactRational.MultiplyByFraction(
                probabilities[x - support.Minimum],
                numeratorFactor,
                denominatorFactor,
                counter);
        }

        return new AdjacentHypergeometricSeries(
            support.Minimum,
            support.Maximum,
            Array.AsReadOnly(probabilities),
            counter.Snapshot(),
            AdjacentStreamAlgorithm);
    }

    public static OrdinaryProbabilityResult HypergeometricEvent(
        int population,
        int successStates,
        int draws,
        IEnumerable<int> includedObservations)
    {
        ArgumentNullException.ThrowIfNull(includedObservations);
        var requested = includedObservations.Distinct().ToHashSet();
        var stream = HypergeometricStream(population, successStates, draws);
        var counter = new CombinatorialWorkCounter
        {
            AdditiveNodes = 1,
            AdditiveTerms = requested.Count,
        };
        counter.Add(stream.Work);
        var result = ExactRational.Zero;

        for (var x = stream.SupportMinimum; x <= stream.SupportMaximum; x++)
        {
            if (requested.Contains(x))
            {
                result = MeasuredExactRational.Add(
                    result,
                    stream.Probabilities[x - stream.SupportMinimum],
                    counter);
            }
        }

        // Requested observations outside support are exact-zero event terms. They remain counted
        // above but require no arithmetic update.
        return new OrdinaryProbabilityResult(
            result,
            counter.Snapshot(),
            CombinatorialBoundaryStatus.AdditiveMagnitudeRequired,
            EventAlgorithm);
    }

    public static OrdinaryProbabilityResult HypergeometricRange(
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

    private static BigInteger BinomialCore(int n, int k, CombinatorialWorkCounter counter)
    {
        k = Math.Min(k, n - k);
        var result = BigInteger.One;
        for (var index = 1; index <= k; index++)
        {
            var numerator = new BigInteger(n - k + index);
            var denominator = new BigInteger(index);

            var localCancellation = BigInteger.GreatestCommonDivisor(numerator, denominator);
            counter.GreatestCommonDivisors++;
            if (!localCancellation.IsOne)
            {
                numerator /= localCancellation;
                denominator /= localCancellation;
                counter.BigIntegerExactDivisions += 2;
            }

            var carriedCancellation = BigInteger.GreatestCommonDivisor(result, denominator);
            counter.GreatestCommonDivisors++;
            if (!carriedCancellation.IsOne)
            {
                result /= carriedCancellation;
                denominator /= carriedCancellation;
                counter.BigIntegerExactDivisions += 2;
            }

            if (!denominator.IsOne)
            {
                throw new InvalidOperationException("The cross-cancelled binomial step was not exactly divisible.");
            }

            result *= numerator;
            counter.BigIntegerMultiplications++;
        }

        return result;
    }

    private static void ValidateBinomial(int n, int k)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        if (k < 0 || k > n)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "K must be between zero and N.");
        }
    }
}
