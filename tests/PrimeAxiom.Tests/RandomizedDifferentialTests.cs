using System.Numerics;
using PrimeAxiom.Core.Circuits;
using PrimeAxiom.Core.Representations;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Tests;

public sealed class RandomizedDifferentialTests
{
    private const int Seed = 0x51A7E;

    [Fact]
    public void SeededBinaryDifferentialChecksMatchBigIntegerOracle()
    {
        var random = new Random(Seed);

        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var width = random.Next(1, 17);
            var modulus = BigInteger.One << width;
            var leftMagnitude = random.Next(1 << width);
            var rightMagnitude = random.Next(1 << width);
            var carryIn = random.Next(2) == 0 ? BitState.Off : BitState.On;
            var left = BinaryWord.FromUnsigned(leftMagnitude, width);
            var right = BinaryWord.FromUnsigned(rightMagnitude, width);

            var sum = BinaryCircuit.Add(left, right, carryIn);
            var exactSum = new BigInteger(leftMagnitude + rightMagnitude + ToInt(carryIn));
            Assert.Equal(exactSum % modulus, sum.Value.ToUnsigned());
            Assert.Equal(exactSum >= modulus ? BitState.On : BitState.Off, sum.Carry);

            var difference = BinaryCircuit.Subtract(left, right);
            var wrappedDifference = (new BigInteger(leftMagnitude - rightMagnitude) + modulus) % modulus;
            Assert.Equal(wrappedDifference, difference.Value.ToUnsigned());
            Assert.Equal(leftMagnitude < rightMagnitude ? BitState.On : BitState.Off, difference.Borrow);

            var comparison = BinaryCircuit.Compare(left, right);
            Assert.Equal(leftMagnitude < rightMagnitude ? BitState.On : BitState.Off, comparison.Less);
            Assert.Equal(leftMagnitude == rightMagnitude ? BitState.On : BitState.Off, comparison.Equal);
            Assert.Equal(leftMagnitude > rightMagnitude ? BitState.On : BitState.Off, comparison.Greater);

            var product = BinaryCircuit.Multiply(left, right);
            Assert.Equal(
                new BigInteger(leftMagnitude) * rightMagnitude,
                product.Value.ToUnsigned());
        }
    }

    [Fact]
    public void SeededCoordinateDifferentialChecksMatchExponentAndMagnitudeOracles()
    {
        var random = new Random(Seed);
        var basis = PrimeBasis.First(8);
        const int exponentWidth = 5;

        for (var iteration = 0; iteration < 500; iteration++)
        {
            var leftExponents = RandomExponents(random, basis.Count, exclusiveUpperBound: 16);
            var rightExponents = RandomExponents(random, basis.Count, exclusiveUpperBound: 16);
            var left = Coordinates(basis, exponentWidth, leftExponents);
            var right = Coordinates(basis, exponentWidth, rightExponents);
            var expectedLeftMagnitude = ReconstructOracle(basis, leftExponents);
            var expectedRightMagnitude = ReconstructOracle(basis, rightExponents);

            Assert.Equal(expectedLeftMagnitude, left.Reconstruct().Value);
            Assert.Equal(expectedRightMagnitude, right.Reconstruct().Value);
            Assert.Equal(left, Encode(expectedLeftMagnitude, basis, exponentWidth));
            Assert.Equal(right, Encode(expectedRightMagnitude, basis, exponentWidth));

            var composed = left.Compose(right);
            Assert.True(composed.Receipt.Succeeded);
            Assert.False(composed.Receipt.UsedMagnitudeDomain);
            var composedValue = Assert.IsType<PrimeCoordinates>(composed.Value);
            Assert.Equal(expectedLeftMagnitude * expectedRightMagnitude, composedValue.Reconstruct().Value);
            for (var lane = 0; lane < basis.Count; lane++)
            {
                Assert.Equal(
                    new BigInteger(leftExponents[lane] + rightExponents[lane]),
                    composedValue.ExponentAt(lane).ToUnsigned());
            }

            var dividendExponents = leftExponents.Zip(rightExponents, Math.Max).ToArray();
            var divisorExponents = leftExponents.Zip(rightExponents, Math.Min).ToArray();
            var dividend = Coordinates(basis, exponentWidth, dividendExponents);
            var divisor = Coordinates(basis, exponentWidth, divisorExponents);
            var cancelled = dividend.Cancel(divisor);
            Assert.True(cancelled.Receipt.Succeeded);
            Assert.Equal(
                dividend.Reconstruct().Value / divisor.Reconstruct().Value,
                Assert.IsType<PrimeCoordinates>(cancelled.Value).Reconstruct().Value);
        }
    }

    private static int[] RandomExponents(Random random, int count, int exclusiveUpperBound) =>
        Enumerable.Range(0, count)
            .Select(_ => random.Next(exclusiveUpperBound))
            .ToArray();

    private static PrimeCoordinates Coordinates(
        PrimeBasis basis,
        int exponentWidth,
        IEnumerable<int> exponents) =>
        new(
            basis,
            exponents.Select(exponent => BinaryWord.FromUnsigned(exponent, exponentWidth)));

    private static PrimeCoordinates Encode(
        BigInteger magnitude,
        PrimeBasis basis,
        int exponentWidth)
    {
        var result = PrimeCoordinates.Encode(magnitude, basis, exponentWidth);
        Assert.True(result.Receipt.Succeeded);
        return Assert.IsType<PrimeCoordinates>(result.Value);
    }

    private static BigInteger ReconstructOracle(PrimeBasis basis, IReadOnlyList<int> exponents)
    {
        var value = BigInteger.One;
        for (var lane = 0; lane < basis.Count; lane++)
        {
            value *= BigInteger.Pow(basis[lane], exponents[lane]);
        }

        return value;
    }

    private static int ToInt(BitState state) => state == BitState.On ? 1 : 0;
}
