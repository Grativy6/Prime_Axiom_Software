using System.Numerics;
using PrimeAxiom.Core.Hybrid;

namespace PrimeAxiom.Tests;

public sealed class HybridArithmeticTests
{
    private static readonly int[] SelectedBankPrimes = [3, 11, 13];

    [Fact]
    public void BinaryIngressIsExactAcrossSignedFiniteDomainAndBankSizes()
    {
        foreach (var bankSize in new[] { 0, 1, 4, 8 })
        {
            var bank = ValuationBank.First(bankSize);
            for (var value = -256; value <= 256; value++)
            {
                var encoded = HybridInteger.FromBinary(value, bank, exponentWidth: 16);
                Assert.True(encoded.Receipt.Succeeded);
                Assert.Equal(new BigInteger(value), encoded.Value!.Reconstruct().Value);
                Assert.Equal(HybridValidity.Canonical, encoded.Value.Validity);
                if (!encoded.Value.IsZero)
                {
                    for (var lane = 0; lane < bank.Count; lane++)
                    {
                        Assert.NotEqual(BigInteger.Zero, encoded.Value.Cofactor % bank[lane]);
                        Assert.Equal(ValuationKnowledge.KnownExact, encoded.Value.KnowledgeAt(lane));
                    }
                }
            }
        }
    }

    [Fact]
    public void MultiplyAndAdditionDifferentiallyMatchSignedArithmetic()
    {
        var bank = ValuationBank.First(4);
        for (var left = -48; left <= 48; left++)
        {
            for (var right = -48; right <= 48; right++)
            {
                var encodedLeft = HybridInteger.FromBinary(left, bank, 16).Value!;
                var encodedRight = HybridInteger.FromBinary(right, bank, 16).Value!;

                var product = encodedLeft.Multiply(encodedRight);
                Assert.True(product.Receipt.Succeeded);
                Assert.Equal(new BigInteger(left * right), product.Value!.Reconstruct().Value);

                var sum = encodedLeft.AddPreservingValuations(encodedRight);
                Assert.True(sum.Receipt.Succeeded);
                Assert.Equal(new BigInteger(left + right), sum.Value!.Reconstruct().Value);
                var normalized = sum.Value.Normalize();
                Assert.True(normalized.Receipt.Succeeded);
                Assert.Equal(HybridValidity.Canonical, normalized.Value!.Validity);
                Assert.Equal(new BigInteger(left + right), normalized.Value.Reconstruct().Value);
            }
        }
    }

    [Fact]
    public void AdditionMarksEqualValuationAsLowerBoundAndRefreshEarnsExactValue()
    {
        var bank = ValuationBank.First(4);
        var twelve = HybridInteger.FromBinary(12, bank, 8).Value!;
        var twenty = HybridInteger.FromBinary(20, bank, 8).Value!;

        var sum = twelve.AddPreservingValuations(twenty);

        Assert.True(sum.Receipt.Succeeded);
        Assert.Equal(new BigInteger(32), sum.Value!.Reconstruct().Value);
        Assert.Equal(new BigInteger(2), sum.Value.ExponentAt(0));
        Assert.Equal(ValuationKnowledge.CertifiedLowerBound, sum.Value.KnowledgeAt(0));
        Assert.False(sum.Value.Valuation(0).IsKnown);
        Assert.True(sum.Value.IsEven().Value);

        var refreshed = sum.Value.RefreshLane(0);

        Assert.True(refreshed.Receipt.Succeeded);
        Assert.Equal(new BigInteger(5), refreshed.Value!.ExponentAt(0));
        Assert.Equal(ValuationKnowledge.KnownExact, refreshed.Value.KnowledgeAt(0));
        Assert.Equal(new BigInteger(32), refreshed.Value.Reconstruct().Value);
        Assert.True(refreshed.Value.IsEven().Value);
        Assert.True(refreshed.Receipt.Cost.Maintenance.TrialRemainders > 0);

        var zeroBound = HybridInteger.FromBinary(3, bank, 8).Value!
            .AddPreservingValuations(HybridInteger.FromBinary(5, bank, 8).Value!).Value!;
        Assert.Equal(BigInteger.Zero, zeroBound.ExponentAt(0));
        Assert.Equal(ValuationKnowledge.CertifiedLowerBound, zeroBound.KnowledgeAt(0));
        Assert.Null(zeroBound.IsEven().Value);
    }

    [Fact]
    public void UnequalExactValuationsMakeAdditionMinimumExact()
    {
        var bank = ValuationBank.First(4);
        var twelve = HybridInteger.FromBinary(12, bank, 8).Value!;
        var eight = HybridInteger.FromBinary(8, bank, 8).Value!;

        var sum = twelve.AddPreservingValuations(eight);

        Assert.Equal(new BigInteger(20), sum.Value!.Reconstruct().Value);
        Assert.Equal(new BigInteger(2), sum.Value.ExponentAt(0));
        Assert.Equal(ValuationKnowledge.KnownExact, sum.Value.KnowledgeAt(0));
        Assert.True(sum.Value.Valuation(0).IsKnown);
    }

    [Fact]
    public void ExactDivideDivisibilityGcdAndLcmMatchOrdinaryArithmetic()
    {
        var bank = ValuationBank.First(8);
        for (var left = 0; left <= 80; left++)
        {
            for (var right = 1; right <= 40; right++)
            {
                var a = HybridInteger.FromBinary(left, bank, 16).Value!;
                var b = HybridInteger.FromBinary(right, bank, 16).Value!;
                var divides = b.Divides(a);
                Assert.True(divides.IsKnown);
                Assert.Equal(left % right == 0, divides.Value);

                var division = a.ExactDivide(b);
                Assert.Equal(left % right == 0, division.Receipt.Succeeded);
                if (division.Receipt.Succeeded)
                {
                    Assert.Equal(new BigInteger(left / right), division.Value!.Reconstruct().Value);
                }

                var gcd = a.GreatestCommonDivisor(b);
                Assert.Equal(BigInteger.GreatestCommonDivisor(left, right), gcd.Value!.Reconstruct().Value);
                var lcm = a.LeastCommonMultiple(b);
                var expectedLcm = left == 0 ? BigInteger.Zero : new BigInteger(left / (int)BigInteger.GreatestCommonDivisor(left, right) * right);
                Assert.Equal(expectedLcm, lcm.Value!.Reconstruct().Value);
            }
        }
    }

    [Fact]
    public void DeferredInputsMustBeRefreshedBeforeExactDivisionAndGcd()
    {
        var bank = ValuationBank.First(4);
        var partial = HybridInteger.FromBinary(12, bank, 8).Value!
            .AddPreservingValuations(HybridInteger.FromBinary(20, bank, 8).Value!).Value!;
        var two = HybridInteger.FromBinary(2, bank, 8).Value!;

        Assert.Equal(HybridValidity.Partial, partial.Validity);
        Assert.Equal(HybridFailure.RequiresCanonical, partial.ExactDivide(two).Receipt.Failure);
        Assert.Equal(HybridFailure.RequiresCanonical, partial.GreatestCommonDivisor(two).Receipt.Failure);
        Assert.False(two.Divides(partial).IsKnown);

        var canonical = partial.Normalize().Value!;
        Assert.Equal(new BigInteger(16), canonical.ExactDivide(two).Value!.Reconstruct().Value);
    }

    [Fact]
    public void BankMigrationIsExactAndChargesAdmissionAndEviction()
    {
        var firstFour = ValuationBank.First(4);
        var selected = ValuationBank.WorkloadSelected(SelectedBankPrimes, "selected");
        var source = HybridInteger.FromBinary(2 * 2 * 3 * 5 * 11 * 17, firstFour, 8).Value!;

        var migrated = source.MigrateBank(selected);

        Assert.True(migrated.Receipt.Succeeded);
        Assert.Equal(source.Reconstruct().Value, migrated.Value!.Reconstruct().Value);
        Assert.Equal(new BigInteger(1), migrated.Value.ExponentAt(selected.IndexOf(3)));
        Assert.Equal(new BigInteger(1), migrated.Value.ExponentAt(selected.IndexOf(11)));
        Assert.Equal(1, migrated.Receipt.Cost.Maintenance.Migrations);
        Assert.True(migrated.Receipt.Cost.Maintenance.TrialRemainders >= selected.Count - 1);
        Assert.True(migrated.Receipt.Cost.Maintenance.CofactorMultiplications > 0);
    }

    [Fact]
    public void ExponentOverflowFailsWithoutReturningTruncatedValue()
    {
        var twoBank = ValuationBank.First(1);

        var ingress = HybridInteger.FromBinary(16, twoBank, exponentWidth: 2);
        Assert.False(ingress.Receipt.Succeeded);
        Assert.Equal(HybridFailure.ExponentOverflow, ingress.Receipt.Failure);
        Assert.Null(ingress.Value);

        var eight = HybridInteger.FromBinary(8, twoBank, 2).Value!;
        var two = HybridInteger.FromBinary(2, twoBank, 2).Value!;
        var product = eight.Multiply(two);
        Assert.False(product.Receipt.Succeeded);
        Assert.Equal(HybridFailure.ExponentOverflow, product.Receipt.Failure);
        Assert.Null(product.Value);
        Assert.Equal(new BigInteger(8), eight.Reconstruct().Value);
    }

    [Fact]
    public void HybridCostDoesNotHideCofactorMultiplication()
    {
        var bank = ValuationBank.First(4);
        var left = HybridInteger.FromBinary(13, bank, 8).Value!;
        var right = HybridInteger.FromBinary(17, bank, 8).Value!;

        var product = left.Multiply(right);

        Assert.True(product.Receipt.Cost.Native.BankGates.NandEvaluations > 0);
        Assert.Equal(1, product.Receipt.Cost.Native.CofactorMultiplications);
        Assert.True(product.Receipt.Cost.Native.ModeledBinaryNands > 0);
        Assert.Equal(0, product.Receipt.Cost.Ingress.CofactorMultiplications);
        Assert.Equal(0, product.Receipt.Cost.Egress.CofactorMultiplications);
    }

    [Fact]
    public void RationalSimplificationCancelsBankAndOutsideBankFactors()
    {
        var bank = ValuationBank.First(4);
        var numerator = HybridInteger.FromBinary(-72, bank, 8).Value!;
        var denominator = HybridInteger.FromBinary(-60, bank, 8).Value!;
        var rational = HybridRational.Create(numerator, denominator).Value!;

        var simplified = rational.Simplify();

        Assert.True(simplified.Receipt.Succeeded);
        Assert.Equal(new BigInteger(6), simplified.Value!.Numerator.Reconstruct().Value);
        Assert.Equal(new BigInteger(5), simplified.Value.Denominator.Reconstruct().Value);
        Assert.True(simplified.Receipt.Cost.Native.CofactorGcds > 0);
    }

    [Fact]
    public void RandomizedOperationsMatchBigIntegerOracle()
    {
        var random = new Random(0x001_BA5E);
        var bank = ValuationBank.First(8);
        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var left = random.NextInt64(-1_000_000, 1_000_001);
            var right = random.NextInt64(-1_000_000, 1_000_001);
            var a = HybridInteger.FromBinary(left, bank, 32).Value!;
            var b = HybridInteger.FromBinary(right, bank, 32).Value!;

            Assert.Equal(new BigInteger(left) * right, a.Multiply(b).Value!.Reconstruct().Value);
            var sum = a.AddPreservingValuations(b);
            Assert.Equal(new BigInteger(left) + right, sum.Value!.Reconstruct().Value);
            Assert.Equal(new BigInteger(left) + right, sum.Value.Normalize().Value!.Reconstruct().Value);
        }
    }
}
