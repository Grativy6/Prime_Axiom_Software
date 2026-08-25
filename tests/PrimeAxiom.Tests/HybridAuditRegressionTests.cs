using System.Numerics;
using PrimeAxiom.Core.Hybrid;
using PrimeAxiom.Core.Machine;

namespace PrimeAxiom.Tests;

public sealed class HybridAuditRegressionTests
{
    private static readonly int[] PrimesTwoThree = [2, 3];

    [Fact]
    public void ZeroValuationIsPositiveInfinityRatherThanFiniteZero()
    {
        var zero = HybridInteger.Zero(ValuationBank.First(1), 8);

        var answer = zero.Valuation(0);

        Assert.True(answer.IsKnown);
        Assert.Equal(ValuationResultKind.PositiveInfinity, answer.Value!.Kind);
    }

    [Fact]
    public void UnknownDivisibilityReceiptIsNotMarkedSuccessful()
    {
        var bank = ValuationBank.First(4);
        var partial = HybridInteger.FromBinary(3, bank, 8).Value!
            .AddPreservingValuations(HybridInteger.FromBinary(5, bank, 8).Value!).Value!;
        var two = HybridInteger.FromBinary(2, bank, 8).Value!;

        var answer = two.Divides(partial);

        Assert.False(answer.IsKnown);
        Assert.Null(answer.Value);
        Assert.False(answer.Receipt.Succeeded);
        Assert.Equal(HybridFailure.RequiresCanonical, answer.Receipt.Failure);
    }

    [Fact]
    public void FailedScalarQueryDoesNotLeakPriorScalar()
    {
        var machine = new HybridMachine(ValuationBank.First(4), 8);

        var receipt = machine.Run(new HybridProgram(new HybridInstruction[]
        {
            new HybridLoadBinary(0, 6),
            new HybridReconstruct(0),
            new HybridReconstruct(99),
        }));

        Assert.False(receipt.Completed);
        Assert.Null(receipt.LastScalar);
        Assert.Equal(HybridFailure.InvalidRegister, receipt.Trace[^1].Receipt.Failure);
    }

    [Fact]
    public void MalformedInstructionIsRejectedAndDestinationInvalidated()
    {
        var machine = new HybridMachine(ValuationBank.First(4), 8);

        var receipt = machine.Run(new HybridProgram(new HybridInstruction[]
        {
            new HybridLoadBinary(0, 6),
            new HybridLoadBinary(1, 99),
            new HybridMigrateBank(1, 0, null!),
        }));

        Assert.False(receipt.Completed);
        Assert.Equal(HybridFailure.InvalidInstruction, receipt.Trace[^1].Receipt.Failure);
        Assert.False(machine.IsRegisterValid(1));
        Assert.Equal(new BigInteger(6), machine.ReadRegister(0)!.Reconstruct().Value);
    }

    [Fact]
    public void BankAndWidthAllocationCapsAreSymmetricAcrossFactories()
    {
        Assert.Throws<ArgumentException>(() => new ValuationBank(Enumerable.Range(0, ValuationBank.MaximumLanes + 1)
            .Select(index => PrimeAt(index))));
        Assert.False(HybridInteger.FromBinary(1, ValuationBank.Empty, HybridInteger.MaximumExponentWidth + 1).Receipt.Succeeded);

        static int PrimeAt(int index)
        {
            var found = -1;
            for (var candidate = 2; ; candidate++)
            {
                if (!IsPrime(candidate))
                {
                    continue;
                }

                found++;
                if (found == index)
                {
                    return candidate;
                }
            }
        }

        static bool IsPrime(int value)
        {
            for (var divisor = 2; (long)divisor * divisor <= value; divisor++)
            {
                if (value % divisor == 0)
                {
                    return false;
                }
            }

            return value >= 2;
        }
    }

    [Fact]
    public void MigrationRejectsOversizedWidthWithoutThrowing()
    {
        var value = HybridInteger.FromBinary(2, ValuationBank.First(1), 2).Value!;

        var migrated = value.MigrateBank(ValuationBank.First(1), HybridInteger.MaximumExponentWidth + 1);

        Assert.False(migrated.Receipt.Succeeded);
        Assert.Equal(HybridFailure.ExponentWidthMismatch, migrated.Receipt.Failure);
    }

    [Fact]
    public void CofactorOnlyPowerDoesNotRequireScalarToFitLaneWidth()
    {
        var value = HybridInteger.FromBinary(17, ValuationBank.Empty, 2).Value!;

        var powered = value.Power(4);

        Assert.True(powered.Receipt.Succeeded);
        Assert.Equal(new BigInteger(83_521), powered.Value!.Reconstruct().Value);
        Assert.True(powered.Receipt.Cost.Native.CofactorMultiplications > 0);

        var zero = HybridInteger.Zero(ValuationBank.First(1), 2).Power(4);
        Assert.True(zero.Receipt.Succeeded);
        Assert.True(zero.Value!.IsZero);
    }

    [Fact]
    public void OverflowReceiptsExcludeUnexecutedCofactorWork()
    {
        var bank = ValuationBank.First(1);
        var eight = HybridInteger.FromBinary(8, bank, 2).Value!;
        var two = HybridInteger.FromBinary(2, bank, 2).Value!;

        var multiply = eight.Multiply(two);
        var power = eight.Power(2);

        Assert.Equal(HybridFailure.ExponentOverflow, multiply.Receipt.Failure);
        Assert.Equal(0, multiply.Receipt.Cost.Native.CofactorMultiplications);
        Assert.Equal(0, multiply.Receipt.Cost.Native.ModeledBinaryNands);
        Assert.Equal(HybridFailure.ExponentOverflow, power.Receipt.Failure);
        Assert.Equal(0, power.Receipt.Cost.Native.CofactorMultiplications);
        Assert.Equal(0, power.Receipt.Cost.Native.ModeledBinaryNands);
    }

    [Fact]
    public void EqualityAndClaimVerificationChargeExactMagnitudeComparison()
    {
        var bank = ValuationBank.First(4);
        var left = HybridInteger.FromBinary(12, bank, 8).Value!;
        var right = HybridInteger.FromBinary(12, bank, 8).Value!;

        var equal = left.NumericEquals(right);
        var mismatch = HybridInteger.FromClaimedMagnitude(
            13,
            1,
            1,
            new BigInteger[] { 2, 1, 0, 0 },
            bank,
            8);

        Assert.True(equal.Value);
        Assert.True(equal.Receipt.Cost.Native.LaneReads > 0);
        Assert.Equal(1, equal.Receipt.Cost.Native.CofactorComparisons);
        Assert.False(mismatch.Receipt.Succeeded);
        Assert.Equal(HybridFailure.ClaimedMagnitudeMismatch, mismatch.Receipt.Failure);
        Assert.Equal(1, mismatch.Receipt.Cost.Ingress.CofactorComparisons);
    }

    [Fact]
    public void StrictDecoderRejectsLexicalNegativeZero()
    {
        var canonical = HybridInteger.Zero(ValuationBank.Empty, 8).Serialize().Data;

        var decoded = HybridInteger.Deserialize(canonical.Replace("\"sign\":0", "\"sign\":-0", StringComparison.Ordinal));

        Assert.False(decoded.Receipt.Succeeded);
        Assert.Equal(HybridFailure.InvalidSerialization, decoded.Receipt.Failure);
    }

    [Fact]
    public void OversizedSerializationReceiptRetainsWorkPerformedBeforeFailure()
    {
        var bank = ValuationBank.First(900);
        var exponent = (BigInteger.One << HybridInteger.MaximumExponentWidth) - BigInteger.One;
        var value = HybridInteger.FromStructured(
            1,
            BigInteger.One,
            Enumerable.Repeat(exponent, bank.Count),
            bank,
            HybridInteger.MaximumExponentWidth).Value!;

        var serialized = value.Serialize();

        Assert.False(serialized.Receipt.Succeeded);
        Assert.Equal(HybridFailure.InvalidSerialization, serialized.Receipt.Failure);
        Assert.Empty(serialized.Data);
        Assert.Equal(bank.Count, serialized.Receipt.Cost.Egress.LaneReads);
        Assert.Equal(bank.Count + 3L, serialized.Receipt.Cost.Egress.MetadataReads);
        Assert.True(serialized.Receipt.Cost.Egress.SerializedBytes > 1_048_576);
    }

    [Fact]
    public void EarlyMigrationOverflowDoesNotChargeUnwrittenTargetLanes()
    {
        var source = HybridInteger.FromBinary(16, ValuationBank.Empty, 2).Value!;
        var target = source.MigrateBank(new ValuationBank(PrimesTwoThree), targetExponentWidth: 2);

        Assert.False(target.Receipt.Succeeded);
        Assert.Equal(HybridFailure.ExponentOverflow, target.Receipt.Failure);
        Assert.Equal(0, target.Receipt.Cost.Maintenance.LaneWrites);
        Assert.Equal(3, target.Receipt.Cost.Maintenance.MetadataReads);
        Assert.Equal(0, target.Receipt.Cost.Maintenance.MetadataWrites);
    }

    [Fact]
    public void SignMismatchDoesNotChargeAShortCircuitedCofactorComparison()
    {
        var bank = ValuationBank.First(1);
        var positive = HybridInteger.FromBinary(2, bank, 8).Value!;
        var negative = HybridInteger.FromBinary(-2, bank, 8).Value!;

        var equal = positive.NumericEquals(negative);

        Assert.True(equal.Receipt.Succeeded);
        Assert.False(equal.Value);
        Assert.Equal(0, equal.Receipt.Cost.Native.CofactorComparisons);
        Assert.Equal(0, equal.Receipt.Cost.Native.BinaryOperandBits);
    }

    [Fact]
    public void PublicEnumerableIngressStopsAtDeclaredBound()
    {
        var bank = ValuationBank.First(1);

        var result = HybridInteger.FromStructured(
            1,
            1,
            Infinite(BigInteger.Zero),
            bank,
            8,
            Infinite(ValuationKnowledge.KnownExact));

        Assert.False(result.Receipt.Succeeded);
        Assert.Equal(HybridFailure.InvalidStructuredIngress, result.Receipt.Failure);

        static IEnumerable<T> Infinite<T>(T value)
        {
            while (true)
            {
                yield return value;
            }
        }
    }

    [Fact]
    public void NullInstructionListReturnsTypedProgramFailure()
    {
        var machine = new HybridMachine(ValuationBank.Empty, 8);

        var receipt = machine.Run(new HybridProgram(null!));

        Assert.False(receipt.Completed);
        Assert.Equal(HybridFailure.InvalidInstruction, receipt.Trace.Single().Receipt.Failure);
    }
}
