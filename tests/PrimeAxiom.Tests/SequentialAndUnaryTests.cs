using System.Numerics;
using PrimeAxiom.Core.Circuits;
using PrimeAxiom.Core.Representations;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Tests;

public sealed class SequentialAndUnaryTests
{
    [Fact]
    public void SrNandLatchSetsHoldsResetsAndExposesForbiddenBehavior()
    {
        var latch = new SrNandLatch();
        Assert.Equal(BitState.Off, latch.Q);
        Assert.Equal(BitState.On, latch.QBar);

        var initialHold = latch.Apply(BitState.On, BitState.On);
        AssertStable(initialHold, BitState.Off);

        var set = latch.Apply(BitState.Off, BitState.On);
        AssertStable(set, BitState.On);
        Assert.True(set.PropagationRounds >= 1);
        Assert.True(set.Cost.NandEvaluations >= 2);

        var hold = latch.Apply(BitState.On, BitState.On);
        AssertStable(hold, BitState.On);

        var reset = latch.Apply(BitState.On, BitState.Off);
        AssertStable(reset, BitState.Off);

        var forbidden = latch.Apply(BitState.Off, BitState.Off);
        Assert.Equal(LatchStatus.ForbiddenInput, forbidden.Status);
        Assert.Equal(BitState.On, forbidden.Q);
        Assert.Equal(BitState.On, forbidden.QBar);
        Assert.Equal(new GateCost(2, 1), forbidden.Cost);

        var unresolvedRelease = latch.Apply(BitState.On, BitState.On, maximumRounds: 4);
        Assert.Equal(LatchStatus.DidNotSettle, unresolvedRelease.Status);
        Assert.Equal(4, unresolvedRelease.PropagationRounds);
        Assert.Equal(new GateCost(8, 4), unresolvedRelease.Cost);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            latch.Apply(BitState.On, BitState.On, maximumRounds: 0));
    }

    [Fact]
    public void GatedDLatchTracksOnlyWhileEnabled()
    {
        var latch = new GatedDLatch();

        var set = latch.Apply(BitState.On, BitState.On);
        AssertStable(set, BitState.On);
        Assert.True(set.Cost.NandEvaluations > 2);

        var disabled = latch.Apply(BitState.Off, BitState.Off);
        AssertStable(disabled, BitState.On);
        Assert.Equal(BitState.On, latch.Q);

        var reset = latch.Apply(BitState.Off, BitState.On);
        AssertStable(reset, BitState.Off);
        Assert.Equal(BitState.Off, latch.Q);
    }

    [Fact]
    public void BinaryRegisterWritesInParallelAndHonorsEnable()
    {
        var register = new BinaryRegister(width: 4);
        Assert.Equal(BigInteger.Zero, register.Read().ToUnsigned());

        var firstValue = BinaryWord.ParseMostSignificantFirst("1010");
        var writeCost = register.Write(firstValue);
        Assert.Equal(firstValue, register.Read());
        Assert.True(writeCost.NandEvaluations > 0);
        Assert.True(writeCost.CriticalPathDepth > 0);

        register.Write(BinaryWord.ParseMostSignificantFirst("0101"), BitState.Off);
        Assert.Equal(firstValue, register.Read());

        var secondValue = BinaryWord.ParseMostSignificantFirst("0011");
        register.Write(secondValue, BitState.On);
        Assert.Equal(secondValue, register.Read());

        Assert.Throws<ArgumentException>(() => register.Write(BinaryWord.Zero(3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BinaryRegister(0));
    }

    [Fact]
    public void FourBitCounterTraversesEveryStateAndReportsOnlyWrapOverflow()
    {
        var counter = new BinaryCounter(width: 4);

        for (var value = 0; value < 16; value++)
        {
            var tick = counter.Tick();
            Assert.Equal(new BigInteger(value), tick.Before.ToUnsigned());
            Assert.Equal(new BigInteger((value + 1) & 0x0F), tick.After.ToUnsigned());
            Assert.Equal(value == 15 ? BitState.On : BitState.Off, tick.Overflow);
            Assert.True(tick.Cost.NandEvaluations > 0);
            Assert.True(tick.Cost.CriticalPathDepth > 0);
        }

        Assert.Equal(BigInteger.Zero, counter.Read().ToUnsigned());
    }

    [Fact]
    public void UnaryRegisterRepresentsCountByMarksAndEnforcesBothBounds()
    {
        var unary = new UnaryRegister(capacity: 4);
        Assert.Equal(0, unary.Count);
        Assert.Equal(4, unary.Capacity);
        Assert.All(unary.Marks, mark => Assert.Equal(BitState.Off, mark));

        for (var expected = 1; expected <= unary.Capacity; expected++)
        {
            var transition = unary.Increment();
            Assert.Equal(new StateTransition(BitState.Off, BitState.On), transition);
            Assert.Equal(expected, unary.Count);
            Assert.Equal(
                Enumerable.Repeat(BitState.On, expected)
                    .Concat(Enumerable.Repeat(BitState.Off, unary.Capacity - expected)),
                unary.Marks);
        }

        Assert.Throws<OverflowException>(() => unary.Increment());

        for (var expected = unary.Capacity - 1; expected >= 0; expected--)
        {
            var transition = unary.Decrement();
            Assert.Equal(new StateTransition(BitState.On, BitState.Off), transition);
            Assert.Equal(expected, unary.Count);
            Assert.Equal(
                Enumerable.Repeat(BitState.On, expected)
                    .Concat(Enumerable.Repeat(BitState.Off, unary.Capacity - expected)),
                unary.Marks);
        }

        Assert.Throws<InvalidOperationException>(() => unary.Decrement());
        Assert.Throws<ArgumentOutOfRangeException>(() => new UnaryRegister(0));
    }

    private static void AssertStable(LatchResult result, BitState expectedQ)
    {
        Assert.Equal(LatchStatus.Stable, result.Status);
        Assert.Equal(expectedQ, result.Q);
        Assert.Equal(expectedQ == BitState.On ? BitState.Off : BitState.On, result.QBar);
    }
}
