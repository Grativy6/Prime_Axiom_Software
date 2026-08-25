using System.Numerics;
using PrimeAxiom.Core.Hybrid;

namespace PrimeAxiom.Tests;

public sealed class HybridContractTests
{
    private static readonly int[] PrimeTwo = [2];
    private static readonly int[] PrimesTwoThree = [2, 3];
    private static readonly int[] PrimesThreeFiveSeven = [3, 5, 7];
    private static readonly ValuationKnowledge[] LowerBoundThenExact =
        [ValuationKnowledge.CertifiedLowerBound, ValuationKnowledge.KnownExact];

    [Fact]
    public void ZeroIdentityAndSignedBinaryIngressHaveExactCanonicalForms()
    {
        var bank = ValuationBank.First(3);
        const int width = 8;

        var zero = HybridInteger.Zero(bank, width);
        Assert.True(zero.IsZero);
        Assert.False(zero.IsIdentity);
        Assert.Equal(0, zero.Sign);
        Assert.Equal(BigInteger.Zero, zero.Cofactor);
        Assert.Equal(HybridValidity.Canonical, zero.Validity);
        Assert.Equal(BigInteger.Zero, zero.Reconstruct().Value);
        for (var lane = 0; lane < bank.Count; lane++)
        {
            Assert.Equal(BigInteger.Zero, zero.ExponentAt(lane));
            Assert.Equal(ValuationKnowledge.KnownExact, zero.KnowledgeAt(lane));
            Assert.Equal(LaneProvenance.Zero, zero.ProvenanceAt(lane));
        }

        var identity = HybridInteger.Identity(bank, width);
        Assert.False(identity.IsZero);
        Assert.True(identity.IsIdentity);
        Assert.Equal(1, identity.Sign);
        Assert.Equal(BigInteger.One, identity.Cofactor);
        Assert.Equal(BigInteger.One, identity.Reconstruct().Value);

        var negative = Ingress(-360, bank, width);
        Assert.Equal(-1, negative.Sign);
        Assert.Equal(BigInteger.One, negative.Cofactor);
        Assert.Equal(new BigInteger(3), negative.ExponentAt(0));
        Assert.Equal(new BigInteger(2), negative.ExponentAt(1));
        Assert.Equal(BigInteger.One, negative.ExponentAt(2));
        Assert.Equal(new BigInteger(-360), negative.Reconstruct().Value);
        Assert.Equal(HybridValidity.Canonical, negative.Validity);
    }

    [Fact]
    public void CanonicalIngressRetainsTheExactOutsideBankCofactor()
    {
        var bank = new ValuationBank(PrimesTwoThree);
        var value = Ingress(2 * 2 * 2 * 3 * 3 * 5 * 7, bank, exponentWidth: 8);

        Assert.Equal(new BigInteger(3), value.ExponentAt(0));
        Assert.Equal(new BigInteger(2), value.ExponentAt(1));
        Assert.Equal(new BigInteger(35), value.Cofactor);
        Assert.NotEqual(BigInteger.Zero, value.Cofactor % 2);
        Assert.NotEqual(BigInteger.Zero, value.Cofactor % 3);
        Assert.Equal(new BigInteger(2520), value.Reconstruct().Value);

        var contradictory = HybridInteger.FromStructured(
            sign: 1,
            cofactor: 10,
            exponents: new BigInteger[] { 1, 0 },
            bank,
            exponentWidth: 8,
            knowledge: new[] { ValuationKnowledge.KnownExact, ValuationKnowledge.KnownExact });

        Assert.False(contradictory.Receipt.Succeeded);
        Assert.Equal(HybridFailure.InvalidStructuredIngress, contradictory.Receipt.Failure);
        Assert.Null(contradictory.Value);
    }

    [Fact]
    public void StructuredIngressRejectsMalformedZeroSignLaneAndCofactorStates()
    {
        var bank = new ValuationBank(PrimesTwoThree);

        AssertInvalidStructured(HybridInteger.FromStructured(
            0,
            1,
            new BigInteger[] { 0, 0 },
            bank,
            4));

        AssertInvalidStructured(HybridInteger.FromStructured(
            0,
            0,
            new BigInteger[] { 0, 0 },
            bank,
            4,
            LowerBoundThenExact));

        AssertInvalidStructured(HybridInteger.FromStructured(
            1,
            0,
            new BigInteger[] { 0, 0 },
            bank,
            4));

        AssertInvalidStructured(HybridInteger.FromStructured(
            2,
            1,
            new BigInteger[] { 0, 0 },
            bank,
            4));

        AssertInvalidStructured(HybridInteger.FromStructured(
            1,
            1,
            new BigInteger[] { 0 },
            bank,
            4));

        var overflow = HybridInteger.FromStructured(
            1,
            1,
            new BigInteger[] { 16, 0 },
            bank,
            4);
        Assert.False(overflow.Receipt.Succeeded);
        Assert.Equal(HybridFailure.ExponentOverflow, overflow.Receipt.Failure);
        Assert.Null(overflow.Value);
    }

    [Fact]
    public void StructuredIngressRejectsUndefinedKnowledgeStates()
    {
        var result = HybridInteger.FromStructured(
            sign: 1,
            cofactor: 1,
            exponents: new BigInteger[] { 0 },
            bank: new ValuationBank(PrimeTwo),
            exponentWidth: 4,
            knowledge: new[] { (ValuationKnowledge)999 });

        Assert.False(result.Receipt.Succeeded);
        Assert.Equal(HybridFailure.InvalidStructuredIngress, result.Receipt.Failure);
        Assert.Null(result.Value);
    }

    [Fact]
    public void AdditionProducesAnHonestUnknownValuationUntilRefresh()
    {
        var bank = new ValuationBank(PrimesTwoThree);
        var three = Ingress(3, bank, exponentWidth: 8);
        var five = Ingress(5, bank, exponentWidth: 8);

        var added = three.AddPreservingValuations(five);
        Assert.True(added.Receipt.Succeeded, added.Receipt.Detail);
        var partial = Assert.IsType<HybridInteger>(added.Value);
        Assert.Equal(new BigInteger(8), partial.Reconstruct().Value);
        Assert.Equal(HybridValidity.Partial, partial.Validity);
        Assert.Equal(BigInteger.Zero, partial.ExponentAt(0));
        Assert.Equal(new BigInteger(8), partial.Cofactor);
        Assert.Equal(ValuationKnowledge.CertifiedLowerBound, partial.KnowledgeAt(0));
        Assert.Equal(LaneProvenance.CommonLowerBoundAddition, partial.ProvenanceAt(0));

        var valuation = partial.Valuation(0);
        Assert.True(valuation.Receipt.Succeeded);
        Assert.False(valuation.IsKnown);
        Assert.NotNull(valuation.Value);
        Assert.Equal(BigInteger.Zero, valuation.Value!.LowerBound);
        Assert.False(valuation.Value.IsExact);

        var parity = partial.IsEven();
        Assert.True(parity.Receipt.Succeeded);
        Assert.False(parity.IsKnown);
        Assert.Null(parity.Value);

        var refreshed = partial.RefreshLane(0);
        Assert.True(refreshed.Receipt.Succeeded, refreshed.Receipt.Detail);
        var exact = Assert.IsType<HybridInteger>(refreshed.Value);
        Assert.Equal(HybridValidity.Canonical, exact.Validity);
        Assert.Equal(new BigInteger(3), exact.ExponentAt(0));
        Assert.Equal(BigInteger.One, exact.Cofactor);
        Assert.Equal(ValuationKnowledge.KnownExact, exact.KnowledgeAt(0));
        Assert.Equal(LaneProvenance.Refresh, exact.ProvenanceAt(0));
        Assert.Equal(new BigInteger(8), exact.Reconstruct().Value);
        Assert.True(exact.IsEven().IsKnown);
        Assert.True(exact.IsEven().Value);

        Assert.Equal(HybridValidity.Partial, partial.Validity);
        Assert.Equal(BigInteger.Zero, partial.ExponentAt(0));
        Assert.Equal(new BigInteger(8), partial.Cofactor);
    }

    [Fact]
    public void MigrationFoldsEvictedPowersAndStripsAdmittedPrimesExactly()
    {
        var sourceBank = new ValuationBank(PrimesTwoThree);
        var targetBank = new ValuationBank(PrimesThreeFiveSeven);
        var source = Ingress(360, sourceBank, exponentWidth: 8);
        var sourceSnapshot = source.Serialize().Data;

        var migratedResult = source.MigrateBank(targetBank);
        Assert.True(migratedResult.Receipt.Succeeded, migratedResult.Receipt.Detail);
        var migrated = Assert.IsType<HybridInteger>(migratedResult.Value);

        Assert.Equal(targetBank, migrated.Bank);
        Assert.Equal(new BigInteger(2), migrated.ExponentAt(0));
        Assert.Equal(BigInteger.One, migrated.ExponentAt(1));
        Assert.Equal(BigInteger.Zero, migrated.ExponentAt(2));
        Assert.Equal(new BigInteger(8), migrated.Cofactor);
        Assert.All(Enumerable.Range(0, migrated.LaneCount), lane =>
            Assert.Equal(ValuationKnowledge.KnownExact, migrated.KnowledgeAt(lane)));
        Assert.Equal(new BigInteger(360), migrated.Reconstruct().Value);

        var roundTripResult = migrated.MigrateBank(sourceBank);
        Assert.True(roundTripResult.Receipt.Succeeded, roundTripResult.Receipt.Detail);
        var roundTrip = Assert.IsType<HybridInteger>(roundTripResult.Value);
        Assert.Equal(source, roundTrip);
        Assert.Equal(sourceSnapshot, source.Serialize().Data);
    }

    [Fact]
    public void OverflowingOperationsReturnNoValueAndLeaveEverySourceUnchanged()
    {
        var twoBank = new ValuationBank(PrimeTwo);

        var left = Ingress(8, twoBank, exponentWidth: 2);
        var right = Ingress(2, twoBank, exponentWidth: 2);
        var leftSnapshot = left.Serialize().Data;
        var rightSnapshot = right.Serialize().Data;
        var multiplied = left.Multiply(right);
        AssertOverflow(multiplied);
        Assert.Equal(leftSnapshot, left.Serialize().Data);
        Assert.Equal(rightSnapshot, right.Serialize().Data);
        Assert.Equal(new BigInteger(8), left.Reconstruct().Value);
        Assert.Equal(new BigInteger(2), right.Reconstruct().Value);

        var partialResult = HybridInteger.FromStructured(
            sign: 1,
            cofactor: 2,
            exponents: new BigInteger[] { 3 },
            twoBank,
            exponentWidth: 2,
            knowledge: new[] { ValuationKnowledge.CertifiedLowerBound });
        Assert.True(partialResult.Receipt.Succeeded, partialResult.Receipt.Detail);
        var partial = Assert.IsType<HybridInteger>(partialResult.Value);
        var partialSnapshot = partial.Serialize().Data;
        var refreshed = partial.RefreshLane(0);
        AssertOverflow(refreshed);
        Assert.Equal(partialSnapshot, partial.Serialize().Data);
        Assert.Equal(new BigInteger(16), partial.Reconstruct().Value);

        var magnitudeOnly = Ingress(16, ValuationBank.Empty, exponentWidth: 2);
        var magnitudeSnapshot = magnitudeOnly.Serialize().Data;
        var migrated = magnitudeOnly.MigrateBank(twoBank, targetExponentWidth: 2);
        AssertOverflow(migrated);
        Assert.Equal(magnitudeSnapshot, magnitudeOnly.Serialize().Data);
        Assert.Equal(new BigInteger(16), magnitudeOnly.Reconstruct().Value);
    }

    private static HybridInteger Ingress(BigInteger value, ValuationBank bank, int exponentWidth)
    {
        var result = HybridInteger.FromBinary(value, bank, exponentWidth);
        Assert.True(result.Receipt.Succeeded, result.Receipt.Detail);
        return Assert.IsType<HybridInteger>(result.Value);
    }

    private static void AssertInvalidStructured(HybridResult<HybridInteger> result)
    {
        Assert.False(result.Receipt.Succeeded);
        Assert.Equal(HybridFailure.InvalidStructuredIngress, result.Receipt.Failure);
        Assert.Null(result.Value);
    }

    private static void AssertOverflow(HybridResult<HybridInteger> result)
    {
        Assert.False(result.Receipt.Succeeded);
        Assert.Equal(HybridFailure.ExponentOverflow, result.Receipt.Failure);
        Assert.Null(result.Value);
    }
}
