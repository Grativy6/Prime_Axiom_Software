using System.Numerics;
using PrimeAxiom.Core.Build004.Combinatorics;
using PrimeAxiom.Core.Build004.Lineage;

namespace PrimeAxiom.Tests;

public sealed class Build004CombinatoricsTests
{
    [Fact]
    public void NumericFactorProjectionDeclaresEveryFrozenKnowledgeAxis()
    {
        var combinatorics = new PrimeCombinatorics(32);
        var coordinates = combinatorics.HypergeometricPoint(10, 4, 3, 2).Coordinates;

        Assert.Equal(
            SignedPrimeCoordinates.UniversalNumericPrimeBasisId,
            coordinates.BasisId);
        Assert.Equal(LineageCompleteness.Exact, coordinates.Completeness);
        Assert.Equal(PayloadReplayability.ReplayableExact, coordinates.PayloadReplayability);
        Assert.Contains("positive rational coefficient", SignedPrimeCoordinates.Contract.Preserves);
        Assert.Contains("additive-event structure", SignedPrimeCoordinates.Contract.Discards);
        Assert.Contains("only the projected positive rational coefficient", SignedPrimeCoordinates.Contract.ReplayabilitySemantics);
    }

    [Fact]
    public void CoordinateCanonicalFormBindsContractAndKnowledgeAxesWhileDisplayStaysMathematical()
    {
        var combinatorics = new PrimeCombinatorics(16);
        var first = combinatorics.HypergeometricPoint(10, 4, 3, 2).Coordinates;
        var second = combinatorics.HypergeometricPoint(10, 4, 3, 2).Coordinates;

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(first.ToCanonicalString(), second.ToCanonicalString());
        Assert.Contains(SignedPrimeCoordinates.Contract.Name, first.ToCanonicalString());
        Assert.Contains(SignedPrimeCoordinates.UniversalNumericPrimeBasisId, first.ToCanonicalString());
        Assert.Contains(LineageCompleteness.Exact.ToString(), first.ToCanonicalString());
        Assert.Contains(PayloadReplayability.ReplayableExact.ToString(), first.ToCanonicalString());
        Assert.Equal("2^-1 * 3 * 5^-1", first.ToString());
    }

    [Fact]
    public void ExactRationalHasOneCanonicalRepresentation()
    {
        var half = new ExactRational(2, 4);
        var negativeHalf = new ExactRational(2, -4);
        var zero = new ExactRational(0, -99);

        Assert.Equal(new BigInteger(1), half.Numerator);
        Assert.Equal(new BigInteger(2), half.Denominator);
        Assert.Equal(new ExactRational(-1, 2), negativeHalf);
        Assert.Equal(ExactRational.Zero, zero);
        Assert.Equal(BigInteger.One, zero.Denominator);
        Assert.Equal(ExactRational.Zero, default(ExactRational));
        Assert.Equal(BigInteger.One, default(ExactRational).Denominator);
        Assert.Equal(half, default(ExactRational) + half);
        Assert.Equal(new ExactRational(5, 6), half + new ExactRational(1, 3));
        Assert.Equal(new ExactRational(1, 6), half - new ExactRational(1, 3));
        Assert.Equal(new ExactRational(1, 6), half * new ExactRational(3, 9));
        Assert.Equal(new ExactRational(3, 2), half / new ExactRational(1, 3));
        Assert.True(negativeHalf < ExactRational.Zero);
        Assert.Throws<DivideByZeroException>(() => new ExactRational(1, 0));
        Assert.Throws<DivideByZeroException>(() => half / ExactRational.Zero);
    }

    [Fact]
    public void FactorialsAreBuiltFromLegendreCoordinatesAndCacheExplicitly()
    {
        var combinatorics = new PrimeCombinatorics(64);
        var expected = BigInteger.One;

        for (var n = 0; n <= 64; n++)
        {
            if (n > 1)
            {
                expected *= n;
            }

            var receipt = combinatorics.Factorial(n);
            Assert.Equal(expected, receipt.Value);
            Assert.True(receipt.Coordinates.IsIntegral);
            Assert.Equal(CombinatorialBoundaryStatus.PrimeCoordinateLocal, receipt.Boundary);
            Assert.Equal(PrimeCombinatorics.FactorialAlgorithm, receipt.Algorithm);
        }

        var ten = combinatorics.Factorial(10);
        Assert.Equal(8, ten.Coordinates.GetExponent(2));
        Assert.Equal(4, ten.Coordinates.GetExponent(3));
        Assert.Equal(2, ten.Coordinates.GetExponent(5));
        Assert.Equal(1, ten.Coordinates.GetExponent(7));
        Assert.Equal(1, ten.Work.FactorialCacheHits);
        Assert.Equal(0, ten.Work.FactorialCacheMisses);
        Assert.NotEmpty(combinatorics.PrimeBasis);
        Assert.True(combinatorics.BasisWork.PrimeBasisCompositeMarks > 0);
    }

    [Fact]
    public void ReceiptIdentityIsSemanticWhileConstructionWorkKeepsColdAndWarmPathsDistinct()
    {
        var combinatorics = new PrimeCombinatorics(32);
        var cold = combinatorics.Factorial(20);
        var warm = combinatorics.Factorial(20);

        Assert.Equal(cold.ReceiptId, warm.ReceiptId);
        Assert.Equal(1, cold.Work.FactorialCacheMisses);
        Assert.Equal(0, cold.Work.FactorialCacheHits);
        Assert.Equal(0, warm.Work.FactorialCacheMisses);
        Assert.Equal(1, warm.Work.FactorialCacheHits);
        Assert.Empty(typeof(FactorialReceipt).GetConstructors());
        Assert.Empty(typeof(BinomialReceipt).GetConstructors());
        Assert.Empty(typeof(HypergeometricPointReceipt).GetConstructors());
        Assert.Empty(typeof(HypergeometricEventReceipt).GetConstructors());

        var coordinateList = Assert.IsAssignableFrom<IList<PrimeCoordinate>>(cold.Coordinates.Coordinates);
        Assert.Throws<NotSupportedException>(() => coordinateList.Add(new PrimeCoordinate(31, 1)));
    }

    [Fact]
    public void EveryBinomialThroughSixtyFourMatchesIndependentMagnitudeControl()
    {
        var combinatorics = new PrimeCombinatorics(64);
        for (var n = 0; n <= 64; n++)
        {
            for (var k = 0; k <= n; k++)
            {
                var structural = combinatorics.Binomial(n, k);
                var ordinary = OrdinaryExactCombinatorics.Binomial(n, k);

                Assert.Equal(ordinary.Value, structural.Value);
                Assert.True(structural.Coordinates.IsIntegral);
                Assert.Equal(structural.Value, combinatorics.Binomial(n, n - k).Value);
            }
        }
    }

    [Fact]
    public void HypergeometricPointExposesSignedCoordinatesWithoutFactoringTheFraction()
    {
        var combinatorics = new PrimeCombinatorics(10);
        var receipt = combinatorics.HypergeometricPoint(10, 4, 3, 2);

        Assert.Equal(new ExactRational(3, 10), receipt.Probability);
        Assert.Equal(-1, receipt.Coordinates.GetExponent(2));
        Assert.Equal(1, receipt.Coordinates.GetExponent(3));
        Assert.Equal(-1, receipt.Coordinates.GetExponent(5));
        Assert.Equal(CombinatorialValueStatus.ExactPositive, receipt.Status);
        Assert.Equal(CombinatorialBoundaryStatus.PrimeCoordinateLocal, receipt.Boundary);
        Assert.True(receipt.Work.LegendreQuotientSteps > 0);
        Assert.True(receipt.Work.Reconstructions > 0);
    }

    [Fact]
    public void EveryInSupportPointThroughTwelveMatchesIndependentOrdinaryControl()
    {
        var combinatorics = new PrimeCombinatorics(12);
        var cases = 0;

        for (var population = 0; population <= 12; population++)
        {
            for (var successStates = 0; successStates <= population; successStates++)
            {
                for (var draws = 0; draws <= population; draws++)
                {
                    var minimum = Math.Max(0, draws - (population - successStates));
                    var maximum = Math.Min(draws, successStates);
                    for (var observed = minimum; observed <= maximum; observed++)
                    {
                        var structural = combinatorics.HypergeometricPoint(
                            population,
                            successStates,
                            draws,
                            observed);
                        var ordinary = OrdinaryExactCombinatorics.HypergeometricPoint(
                            population,
                            successStates,
                            draws,
                            observed);

                        Assert.Equal(ordinary.Probability, structural.Probability);
                        Assert.True(structural.Probability >= ExactRational.Zero);
                        Assert.True(structural.Probability <= ExactRational.One);
                        cases++;
                    }
                }
            }
        }

        Assert.Equal(1_820, cases);
    }

    [Fact]
    public void EveryDistributionThroughFourteenNormalizesExactlyAcrossAdditiveBoundary()
    {
        var combinatorics = new PrimeCombinatorics(14);
        var distributions = 0;

        for (var population = 0; population <= 14; population++)
        {
            for (var successStates = 0; successStates <= population; successStates++)
            {
                for (var draws = 0; draws <= population; draws++)
                {
                    var minimum = Math.Max(0, draws - (population - successStates));
                    var maximum = Math.Min(draws, successStates);
                    var receipt = combinatorics.HypergeometricRange(
                        population,
                        successStates,
                        draws,
                        minimum,
                        maximum);

                    Assert.Equal(ExactRational.One, receipt.Probability);
                    Assert.False(receipt.PrimeCoordinatesAvailable);
                    Assert.Equal(CombinatorialBoundaryStatus.AdditiveMagnitudeRequired, receipt.Boundary);
                    Assert.Equal(1, receipt.Work.AdditiveNodes);
                    distributions++;
                }
            }
        }

        Assert.Equal(1_240, distributions);
    }

    [Fact]
    public void AdjacentOrdinaryRecurrenceMatchesEveryStructuralPointThroughTwenty()
    {
        var combinatorics = new PrimeCombinatorics(20);
        for (var population = 0; population <= 20; population++)
        {
            for (var successStates = 0; successStates <= population; successStates++)
            {
                for (var draws = 0; draws <= population; draws++)
                {
                    var stream = OrdinaryExactCombinatorics.HypergeometricStream(
                        population,
                        successStates,
                        draws);
                    for (var observed = stream.SupportMinimum; observed <= stream.SupportMaximum; observed++)
                    {
                        var structural = combinatorics.HypergeometricPoint(
                            population,
                            successStates,
                            draws,
                            observed);
                        Assert.Equal(
                            structural.Probability,
                            stream.Probabilities[observed - stream.SupportMinimum]);
                    }
                }
            }
        }
    }

    [Fact]
    public void SeededPointsMatchWithoutSharingTheStructuralAlgorithm()
    {
        var combinatorics = new PrimeCombinatorics(256);
        var random = new Random(unchecked((int)0x50415334));

        for (var index = 0; index < 1_000; index++)
        {
            var population = random.Next(0, 257);
            var successStates = random.Next(0, population + 1);
            var draws = random.Next(0, population + 1);
            var minimum = Math.Max(0, draws - (population - successStates));
            var maximum = Math.Min(draws, successStates);
            var observed = random.Next(minimum, maximum + 1);

            var structural = combinatorics.HypergeometricPoint(
                population,
                successStates,
                draws,
                observed);
            var ordinary = OrdinaryExactCombinatorics.HypergeometricPoint(
                population,
                successStates,
                draws,
                observed);

            Assert.Equal(ordinary.Probability, structural.Probability);
        }
    }

    [Fact]
    public void ZeroAndOneRemainDistinctEvenThoughBothHaveEmptyCoordinates()
    {
        var combinatorics = new PrimeCombinatorics(4_096);
        var zero = combinatorics.HypergeometricPoint(4_096, 0, 2_048, 1);
        var one = combinatorics.HypergeometricPoint(4_096, 0, 2_048, 0);

        Assert.Equal(CombinatorialValueStatus.ExactZero, zero.Status);
        Assert.Equal(ExactRational.Zero, zero.Probability);
        Assert.Empty(zero.Coordinates.Coordinates);
        Assert.Equal(CombinatorialValueStatus.ExactOne, one.Status);
        Assert.Equal(ExactRational.One, one.Probability);
        Assert.Empty(one.Coordinates.Coordinates);
        Assert.NotEqual(zero.ReceiptId, one.ReceiptId);
    }

    [Fact]
    public void EventDeduplicatesRequestedOutcomesButNeverExponentMergesTheirProbabilities()
    {
        var combinatorics = new PrimeCombinatorics(128);
        var structural = combinatorics.HypergeometricEvent(128, 63, 64, new[] { 28, 29, 29, 30, -1 });
        var ordinary = OrdinaryExactCombinatorics.HypergeometricEvent(128, 63, 64, new[] { 28, 29, 29, 30, -1 });

        Assert.Equal(ordinary.Probability, structural.Probability);
        Assert.Equal(new[] { -1, 28, 29, 30 }, structural.IncludedObservations);
        Assert.Equal(4, structural.PointReceipts.Count);
        Assert.False(structural.PrimeCoordinatesAvailable);
        Assert.Equal(CombinatorialBoundaryStatus.AdditiveMagnitudeRequired, structural.Boundary);
        Assert.Equal(4, structural.Work.AdditiveTerms);
        Assert.InRange(structural.Work.ExactRationalReductions, 1, structural.Work.BigIntegerAdditions);
        Assert.Contains("no exponent-merge", structural.ClaimCeiling, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NamedAndHostileCasesRemainExactWithoutNaiveFactorials()
    {
        var namedContext = new PrimeCombinatorics(1_000);
        var named = namedContext.HypergeometricPoint(1_000, 413, 271, 117);
        var namedOrdinary = OrdinaryExactCombinatorics.HypergeometricPoint(1_000, 413, 271, 117);
        Assert.Equal(namedOrdinary.Probability, named.Probability);

        var centralContext = new PrimeCombinatorics(4_096);
        var central = centralContext.Binomial(4_096, 2_048);
        var centralOrdinary = OrdinaryExactCombinatorics.Binomial(4_096, 2_048);
        Assert.Equal(centralOrdinary.Value, central.Value);

        var hostileContext = new PrimeCombinatorics(100_000);
        var hostile = hostileContext.Binomial(100_000, 1);
        Assert.Equal(new BigInteger(100_000), hostile.Value);
        Assert.True(hostileContext.BasisWork.PrimeBasisPrimes > hostile.Coordinates.Count);
    }

    [Fact]
    public void InvalidDomainsAndExplicitContextBoundFailWithoutPartialReceipts()
    {
        var combinatorics = new PrimeCombinatorics(20);

        Assert.Throws<ArgumentOutOfRangeException>(() => combinatorics.Factorial(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => combinatorics.Factorial(21));
        Assert.Throws<ArgumentOutOfRangeException>(() => combinatorics.Binomial(10, 11));
        Assert.Throws<ArgumentOutOfRangeException>(() => combinatorics.HypergeometricPoint(21, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => combinatorics.HypergeometricPoint(20, 21, 1, 1));
        Assert.Throws<ArgumentException>(() => combinatorics.HypergeometricRange(20, 10, 10, 8, 7));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PrimeCombinatorics(PrimeCombinatorics.MaximumSupportedArgument + 1));
    }
}
