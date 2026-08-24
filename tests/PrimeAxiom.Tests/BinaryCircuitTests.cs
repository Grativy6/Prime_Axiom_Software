using System.Numerics;
using PrimeAxiom.Core.Circuits;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Tests;

public sealed class BinaryCircuitTests
{
    [Fact]
    public void BinaryWordRoundTripsEveryFourBitValueAndDefensivelyCopies()
    {
        for (var value = 0; value < 16; value++)
        {
            var word = BinaryWord.FromUnsigned(value, width: 4);
            Assert.Equal(new BigInteger(value), word.ToUnsigned());
            Assert.Equal(Convert.ToString(value, 2).PadLeft(4, '0'), word.ToMostSignificantFirstString());
            Assert.Equal(word, BinaryWord.ParseMostSignificantFirst(word.ToString()));

            var copy = word.CopyBits();
            copy[0] = copy[0] == BitState.On ? BitState.Off : BitState.On;
            Assert.Equal(new BigInteger(value), word.ToUnsigned());
        }

        Assert.Equal(BigInteger.One, BinaryWord.One(4).ToUnsigned());

        Assert.Throws<ArgumentException>(() => new BinaryWord(Array.Empty<BitState>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryWord.Zero(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryWord.One(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryWord.FromUnsigned(-1, 4));
        Assert.Throws<OverflowException>(() => BinaryWord.FromUnsigned(16, 4));
        Assert.Throws<FormatException>(() => BinaryWord.ParseMostSignificantFirst("10x1"));
    }

    [Fact]
    public void HalfAndFullAddersHaveCompleteTruthTables()
    {
        foreach (var left in States)
        {
            foreach (var right in States)
            {
                var half = BinaryCircuit.HalfAdd(left, right);
                var total = ToInt(left) + ToInt(right);
                Assert.Equal(State((total & 1) != 0), half.Sum);
                Assert.Equal(State(total >= 2), half.Carry);
                Assert.Equal(6, half.Cost.NandEvaluations);

                foreach (var carryIn in States)
                {
                    var full = BinaryCircuit.FullAdd(left, right, carryIn);
                    total = ToInt(left) + ToInt(right) + ToInt(carryIn);
                    Assert.Equal(State((total & 1) != 0), full.Sum);
                    Assert.Equal(State(total >= 2), full.Carry);
                    Assert.Equal(15, full.Cost.NandEvaluations);
                }
            }
        }
    }

    [Fact]
    public void FourBitAdditionIsExhaustivelyEquivalentIncludingCarryInAndOverflow()
    {
        for (var left = 0; left < 16; left++)
        {
            for (var right = 0; right < 16; right++)
            {
                foreach (var carryIn in States)
                {
                    var result = BinaryCircuit.Add(Word(left), Word(right), carryIn);
                    var expected = left + right + ToInt(carryIn);

                    Assert.Equal(new BigInteger(expected & 0x0F), result.Value.ToUnsigned());
                    Assert.Equal(State(expected >= 16), result.Carry);
                    Assert.Equal(60, result.Cost.NandEvaluations);
                    Assert.True(result.Cost.CriticalPathDepth > 0);
                }
            }
        }
    }

    [Fact]
    public void FourBitSubtractionIsExhaustivelyEquivalentIncludingBorrowAndWrap()
    {
        for (var left = 0; left < 16; left++)
        {
            for (var right = 0; right < 16; right++)
            {
                var result = BinaryCircuit.Subtract(Word(left), Word(right));

                Assert.Equal(new BigInteger((left - right) & 0x0F), result.Value.ToUnsigned());
                Assert.Equal(State(left < right), result.Borrow);
                Assert.Equal(76, result.Cost.NandEvaluations);
                Assert.True(result.Cost.CriticalPathDepth > 0);
            }
        }
    }

    [Fact]
    public void FourBitComparisonIsExhaustivelyEquivalentAndOneHot()
    {
        for (var left = 0; left < 16; left++)
        {
            for (var right = 0; right < 16; right++)
            {
                var result = BinaryCircuit.Compare(Word(left), Word(right));

                Assert.Equal(State(left < right), result.Less);
                Assert.Equal(State(left == right), result.Equal);
                Assert.Equal(State(left > right), result.Greater);
                Assert.Equal(1, new[] { result.Less, result.Equal, result.Greater }
                    .Count(state => state == BitState.On));
                Assert.Equal(92, result.Cost.NandEvaluations);
            }
        }
    }

    [Fact]
    public void FourBitMultiplicationIsExhaustivelyEquivalentAtDoubleWidth()
    {
        for (var left = 0; left < 16; left++)
        {
            for (var right = 0; right < 16; right++)
            {
                var result = BinaryCircuit.Multiply(Word(left), Word(right));

                Assert.Equal(8, result.Value.Width);
                Assert.Equal(new BigInteger(left * right), result.Value.ToUnsigned());
                Assert.Equal(512, result.Cost.NandEvaluations);
                Assert.Equal(55, result.Cost.CriticalPathDepth);
            }
        }
    }

    [Fact]
    public void BinaryCircuitsRejectMixedWidths()
    {
        var shortWord = BinaryWord.Zero(3);
        var longWord = BinaryWord.Zero(4);

        Assert.Throws<ArgumentException>(() => BinaryCircuit.Add(shortWord, longWord));
        Assert.Throws<ArgumentException>(() => BinaryCircuit.Subtract(shortWord, longWord));
        Assert.Throws<ArgumentException>(() => BinaryCircuit.Compare(shortWord, longWord));
        Assert.Throws<ArgumentException>(() => BinaryCircuit.Multiply(shortWord, longWord));
    }

    private static IReadOnlyList<BitState> States { get; } =
        new[] { BitState.Off, BitState.On };

    private static BinaryWord Word(int value) => BinaryWord.FromUnsigned(value, width: 4);

    private static int ToInt(BitState state) => state == BitState.On ? 1 : 0;

    private static BitState State(bool value) =>
        value ? BitState.On : BitState.Off;
}
