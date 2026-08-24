using System.Numerics;
using PrimeAxiom.Core.Circuits;
using PrimeAxiom.Core.Representations;

namespace PrimeAxiom.Tests;

public sealed class PrimeRepresentationTests
{
    private static readonly int[] FirstSixPrimes = { 2, 3, 5, 7, 11, 13 };
    private static readonly int[] FirstThreePrimes = { 2, 3, 5 };
    private static readonly int[] BasisWithOne = { 1, 2, 3 };
    private static readonly int[] BasisWithComposite = { 2, 3, 9 };
    private static readonly int[] BasisWithDuplicate = { 2, 2, 3 };
    private static readonly int[] DescendingBasis = { 3, 2 };
    private static readonly int[] AlternativeThreePrimeBasis = { 2, 3, 7 };
    private static readonly int[] FirstFourPrimes = { 2, 3, 5, 7 };
    private static readonly int[] AlternativeFourPrimeBasis = { 2, 3, 7, 11 };
    private static readonly PrimePower[] SparseLeftPowers =
    {
        new(0, 2),
        new(2, 1),
        new(4, 3),
    };
    private static readonly PrimePower[] SparseRightPowers =
    {
        new(1, 4),
        new(2, 5),
        new(5, 1),
    };
    private static readonly PrimePower[] SparseMergedPowers =
    {
        new(0, 2),
        new(1, 4),
        new(2, 6),
        new(4, 3),
        new(5, 1),
    };
    private static readonly PrimePower[] DenseSixtyPowers =
    {
        new(0, 2),
        new(1, 1),
        new(2, 1),
    };
    private static readonly PrimePower[] UnsortedPowers = { new(1, 1), new(0, 1) };
    private static readonly PrimePower[] DuplicatePowers = { new(0, 1), new(0, 2) };
    private static readonly PrimePower[] NegativeLanePower = { new(-1, 1) };
    private static readonly PrimePower[] NegativeExponentPower = { new(0, -1) };

    [Fact]
    public void PrimeBasisAcceptsOnlyUniqueIncreasingPrimeLabels()
    {
        Assert.Equal(FirstSixPrimes, PrimeBasis.First(6).Primes);
        Assert.Equal(new PrimeBasis(FirstThreePrimes), PrimeBasis.First(3));

        Assert.Throws<ArgumentException>(() => new PrimeBasis(Array.Empty<int>()));
        Assert.Throws<ArgumentException>(() => new PrimeBasis(BasisWithOne));
        Assert.Throws<ArgumentException>(() => new PrimeBasis(BasisWithComposite));
        Assert.Throws<ArgumentException>(() => new PrimeBasis(BasisWithDuplicate));
        Assert.Throws<ArgumentException>(() => new PrimeBasis(DescendingBasis));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimeBasis.First(0));
    }

    [Fact]
    public void EncodeAndReconstructAreExactForEveryMagnitudeOneThrough256()
    {
        var basis = PrimeBasis.First(54); // 251 is the greatest prime <= 256.

        for (var magnitude = 1; magnitude <= 256; magnitude++)
        {
            var encoded = PrimeCoordinates.Encode(magnitude, basis, exponentWidth: 8);
            var coordinates = Assert.IsType<PrimeCoordinates>(encoded.Value);

            Assert.True(encoded.Receipt.Succeeded);
            Assert.Equal(CoordinateFailure.None, encoded.Receipt.Failure);
            Assert.True(encoded.Receipt.UsedMagnitudeDomain);
            Assert.Equal(BigInteger.One, encoded.UnrepresentedResidual);
            Assert.Equal(new BigInteger(magnitude), coordinates.Reconstruct().Value);
            Assert.Equal(checked((long)basis.Count * 8), coordinates.DensePayloadBits);
        }
    }

    [Fact]
    public void CoordinateOperationsMatchOrdinaryArithmeticForEveryPairOneThrough32()
    {
        var basis = PrimeBasis.First(18); // Through 61, covering inputs and every sum <= 64.
        var encoded = Enumerable.Range(1, 32).ToDictionary(
            value => value,
            value => Encode(value, basis, exponentWidth: 8));

        foreach (var leftMagnitude in encoded.Keys)
        {
            foreach (var rightMagnitude in encoded.Keys)
            {
                var left = encoded[leftMagnitude];
                var right = encoded[rightMagnitude];

                var composed = left.Compose(right);
                AssertLocalSuccess(composed);
                Assert.Equal(
                    new BigInteger(leftMagnitude * rightMagnitude),
                    Assert.IsType<PrimeCoordinates>(composed.Value).Reconstruct().Value);

                var cancelled = left.Cancel(right);
                if (leftMagnitude % rightMagnitude == 0)
                {
                    AssertLocalSuccess(cancelled);
                    Assert.Equal(
                        new BigInteger(leftMagnitude / rightMagnitude),
                        Assert.IsType<PrimeCoordinates>(cancelled.Value).Reconstruct().Value);
                }
                else
                {
                    Assert.Null(cancelled.Value);
                    Assert.False(cancelled.Receipt.Succeeded);
                    Assert.Equal(CoordinateFailure.NotDivisible, cancelled.Receipt.Failure);
                    Assert.False(cancelled.Receipt.UsedMagnitudeDomain);
                }

                var gcd = left.GreatestCommonDivisor(right);
                AssertLocalSuccess(gcd);
                var expectedGcd = BigInteger.GreatestCommonDivisor(leftMagnitude, rightMagnitude);
                Assert.Equal(
                    expectedGcd,
                    Assert.IsType<PrimeCoordinates>(gcd.Value).Reconstruct().Value);

                var lcm = left.LeastCommonMultiple(right);
                AssertLocalSuccess(lcm);
                Assert.Equal(
                    leftMagnitude / expectedGcd * rightMagnitude,
                    Assert.IsType<PrimeCoordinates>(lcm.Value).Reconstruct().Value);

                var divides = left.Divides(right);
                Assert.True(divides.Receipt.Succeeded);
                Assert.False(divides.Receipt.UsedMagnitudeDomain);
                Assert.Equal(rightMagnitude % leftMagnitude == 0, divides.Divides);

                var sum = left.AddViaMagnitudeAndRefactor(right);
                Assert.True(sum.Receipt.Succeeded);
                Assert.True(sum.Receipt.UsedMagnitudeDomain);
                Assert.Equal(1, sum.Receipt.Cost.MagnitudeAdditions);
                Assert.Equal(
                    new BigInteger(leftMagnitude + rightMagnitude),
                    Assert.IsType<PrimeCoordinates>(sum.Value).Reconstruct().Value);
            }
        }
    }

    [Fact]
    public void PositiveCoordinateDomainExposesZeroNegativeBasisEscapeAndExponentOverflow()
    {
        var basis = new PrimeBasis(FirstThreePrimes);

        AssertEncodingFailure(
            PrimeCoordinates.Encode(0, basis, exponentWidth: 3),
            CoordinateFailure.ZeroOutsidePositiveDomain,
            BigInteger.Zero,
            expectedLaneWrites: 0);
        AssertEncodingFailure(
            PrimeCoordinates.Encode(-6, basis, exponentWidth: 3),
            CoordinateFailure.NegativeOutsidePositiveDomain,
            new BigInteger(-6),
            expectedLaneWrites: 0);
        AssertEncodingFailure(
            PrimeCoordinates.Encode(14, basis, exponentWidth: 3),
            CoordinateFailure.BasisEscape,
            new BigInteger(7),
            expectedLaneWrites: 3);
        AssertEncodingFailure(
            PrimeCoordinates.Encode(16, basis, exponentWidth: 2),
            CoordinateFailure.ExponentOverflow,
            BigInteger.One,
            expectedLaneWrites: 0);

        var one = Encode(1, basis, exponentWidth: 3);
        var six = Encode(6, basis, exponentWidth: 3);
        var escapedSum = one.AddViaMagnitudeAndRefactor(six);
        Assert.False(escapedSum.Receipt.Succeeded);
        Assert.Equal(CoordinateFailure.BasisEscape, escapedSum.Receipt.Failure);
        Assert.Equal(new BigInteger(7), escapedSum.Receipt.UnrepresentedResidual);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrimeCoordinates.Encode(1, basis, exponentWidth: 0));
    }

    [Fact]
    public void CoordinateOperationsExposeOverflowUnderflowAndCompatibilityFailures()
    {
        var basis = new PrimeBasis(FirstThreePrimes);
        var eight = Encode(8, basis, exponentWidth: 2);
        var two = Encode(2, basis, exponentWidth: 2);

        var overflow = eight.Compose(two);
        Assert.Null(overflow.Value);
        Assert.False(overflow.Receipt.Succeeded);
        Assert.Equal(CoordinateFailure.ExponentOverflow, overflow.Receipt.Failure);
        Assert.Equal(96, overflow.Receipt.Cost.Gates.NandEvaluations);

        var underflow = two.Cancel(eight);
        Assert.Null(underflow.Value);
        Assert.False(underflow.Receipt.Succeeded);
        Assert.Equal(CoordinateFailure.NotDivisible, underflow.Receipt.Failure);

        var otherBasis = Encode(2, new PrimeBasis(AlternativeThreePrimeBasis), exponentWidth: 2);
        var basisMismatch = two.Compose(otherBasis);
        Assert.Null(basisMismatch.Value);
        Assert.Equal(CoordinateFailure.BasisMismatch, basisMismatch.Receipt.Failure);

        var wider = Encode(2, basis, exponentWidth: 3);
        var widthMismatch = two.Compose(wider);
        Assert.Null(widthMismatch.Value);
        Assert.Equal(CoordinateFailure.ExponentWidthMismatch, widthMismatch.Receipt.Failure);
    }

    [Fact]
    public void IdentityAndTaggedValuesPayForIdentityZeroAndSignExplicitly()
    {
        var basis = new PrimeBasis(FirstFourPrimes);
        var identity = PrimeCoordinates.Identity(basis, exponentWidth: 4);
        Assert.Equal(BigInteger.One, identity.Reconstruct().Value);
        Assert.Equal("1", identity.ToFactorizationString());

        var zero = TaggedPrimeValue.Encode(0, basis, exponentWidth: 4);
        var zeroValue = Assert.IsType<TaggedPrimeValue>(zero.Value);
        Assert.True(zero.Receipt.Succeeded);
        Assert.True(zeroValue.IsZero);
        Assert.Equal(BigInteger.Zero, zeroValue.Reconstruct());

        var negativeSix = TaggedPrimeValue.Encode(-6, basis, exponentWidth: 4);
        var negativeTen = TaggedPrimeValue.Encode(-10, basis, exponentWidth: 4);
        var left = Assert.IsType<TaggedPrimeValue>(negativeSix.Value);
        var right = Assert.IsType<TaggedPrimeValue>(negativeTen.Value);
        Assert.Equal(-1, left.Sign);
        Assert.Equal(new BigInteger(-6), left.Reconstruct());

        var positiveProduct = left.Compose(right);
        var product = Assert.IsType<TaggedPrimeValue>(positiveProduct.Value);
        Assert.True(positiveProduct.Receipt.Succeeded);
        Assert.Equal(1, product.Sign);
        Assert.Equal(new BigInteger(60), product.Reconstruct());

        var zeroProduct = zeroValue.Compose(left);
        Assert.True(zeroProduct.Receipt.Succeeded);
        Assert.True(Assert.IsType<TaggedPrimeValue>(zeroProduct.Value).IsZero);
        Assert.Equal(BigInteger.Zero, Assert.IsType<TaggedPrimeValue>(zeroProduct.Value).Reconstruct());
    }

    [Fact]
    public void SparseComposeIsAStableSortedMergeWithAuditableAbstractCosts()
    {
        var basis = PrimeBasis.First(6);
        var left = new SparsePrimeCoordinates(
            basis,
            SparseLeftPowers);
        var right = new SparsePrimeCoordinates(
            basis,
            SparseRightPowers);

        var result = left.Compose(right);

        Assert.Equal(
            SparseMergedPowers,
            result.Value.Powers);
        Assert.Equal(new SparseCost(4, 1, 5), result.Cost);
        Assert.Equal(5, result.Value.NonzeroLaneCount);
        Assert.Equal(44, result.Value.PayloadBits(laneIndexWidth: 3, exponentWidth: 5, lengthHeaderWidth: 4));
    }

    [Fact]
    public void SparseCoordinatesRoundTripDensePayloadAndRejectInvalidMetadata()
    {
        var basis = PrimeBasis.First(4);
        var dense = Encode(60, basis, exponentWidth: 4);
        var sparse = SparsePrimeCoordinates.FromDense(dense);

        Assert.Equal(
            DenseSixtyPowers,
            sparse.Powers);
        Assert.Equal(3, sparse.NonzeroLaneCount);

        Assert.Throws<ArgumentException>(() => new SparsePrimeCoordinates(
            basis,
            UnsortedPowers));
        Assert.Throws<ArgumentException>(() => new SparsePrimeCoordinates(
            basis,
            DuplicatePowers));
        Assert.Throws<ArgumentException>(() => new SparsePrimeCoordinates(
            basis,
            NegativeLanePower));
        Assert.Throws<ArgumentException>(() => new SparsePrimeCoordinates(
            basis,
            NegativeExponentPower));
        Assert.Throws<ArgumentException>(() => sparse.Compose(
            new SparsePrimeCoordinates(new PrimeBasis(AlternativeFourPrimeBasis), Array.Empty<PrimePower>())));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            sparse.PayloadBits(laneIndexWidth: 0, exponentWidth: 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            sparse.PayloadBits(laneIndexWidth: 1, exponentWidth: 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            sparse.PayloadBits(laneIndexWidth: 2, exponentWidth: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            sparse.PayloadBits(laneIndexWidth: 2, exponentWidth: 4, lengthHeaderWidth: 1));

        var hugeExponent = new SparsePrimeCoordinates(
            basis,
            new[] { new PrimePower(0, BigInteger.One << 100) });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            hugeExponent.PayloadBits(laneIndexWidth: 2, exponentWidth: 100));
    }

    private static PrimeCoordinates Encode(
        BigInteger magnitude,
        PrimeBasis basis,
        int exponentWidth)
    {
        var result = PrimeCoordinates.Encode(magnitude, basis, exponentWidth);
        Assert.True(result.Receipt.Succeeded);
        Assert.Equal(CoordinateFailure.None, result.Receipt.Failure);
        return Assert.IsType<PrimeCoordinates>(result.Value);
    }

    private static void AssertLocalSuccess(CoordinateResult result)
    {
        Assert.True(result.Receipt.Succeeded);
        Assert.Equal(CoordinateFailure.None, result.Receipt.Failure);
        Assert.False(result.Receipt.UsedMagnitudeDomain);
        Assert.NotNull(result.Value);
        Assert.True(result.Receipt.Cost.Gates.NandEvaluations > 0);
    }

    private static void AssertEncodingFailure(
        EncodingResult result,
        CoordinateFailure expectedFailure,
        BigInteger expectedResidual,
        long expectedLaneWrites)
    {
        Assert.Null(result.Value);
        Assert.False(result.Receipt.Succeeded);
        Assert.Equal(expectedFailure, result.Receipt.Failure);
        Assert.True(result.Receipt.UsedMagnitudeDomain);
        Assert.Equal(expectedResidual, result.UnrepresentedResidual);
        Assert.Equal(expectedResidual, result.Receipt.UnrepresentedResidual);
        Assert.Equal(expectedLaneWrites, result.Receipt.Cost.LaneWrites);
    }
}
