using System.Globalization;
using System.Numerics;
using PrimeAxiom.Core.Calculator;

namespace PrimeAxiom.Tests;

public sealed class PrimeReceiptCalculatorTests
{
    [Fact]
    public void ReceiptKindsKeepZeroUnitsSignAndPrimeFactorsDistinct()
    {
        var zero = PrimeReceiptCalculator.Analyze(BigInteger.Zero);
        var one = PrimeReceiptCalculator.Analyze(BigInteger.One);
        var negativeOne = PrimeReceiptCalculator.Analyze(BigInteger.MinusOne);
        var negativeComposite = PrimeReceiptCalculator.Analyze(new BigInteger(-360));

        Assert.Equal(PrimeReceiptStatus.ExactZero, zero.Status);
        Assert.Empty(zero.PrimePowers);
        Assert.Equal("0", zero.Structure);
        Assert.Equal(BigInteger.Zero, PrimeReceiptCalculator.Reconstruct(zero));

        Assert.Equal(PrimeReceiptStatus.ExactUnit, one.Status);
        Assert.Equal(PrimeReceiptStatus.ExactUnit, negativeOne.Status);
        Assert.Empty(one.PrimePowers);
        Assert.Empty(negativeOne.PrimePowers);
        Assert.Equal("1", one.Structure);
        Assert.Equal("-1", negativeOne.Structure);

        Assert.Equal(-1, negativeComposite.Sign);
        Assert.Equal("-(2^3 * 3^2 * 5)", negativeComposite.Structure);
        Assert.False(negativeComposite.IntegerIsPrime);
        Assert.Equal(new BigInteger(-360), PrimeReceiptCalculator.Reconstruct(negativeComposite));
    }

    [Fact]
    public void CompleteReceiptReturnsSortedCertifiedPrimePowers()
    {
        var receipt = PrimeReceiptCalculator.Analyze(new BigInteger(360));

        Assert.Equal(PrimeReceiptStatus.ExactFactorization, receipt.Status);
        Assert.Equal("2^3 * 3^2 * 5", receipt.Structure);
        Assert.Equal("1", receipt.UnresolvedCofactorDecimal);
        Assert.True(receipt.ReconstructionVerified);
        Assert.Equal(
            new[] { ("2", 3), ("3", 2), ("5", 1) },
            receipt.PrimePowers.Select(factor => (factor.PrimeDecimal, factor.Exponent)).ToArray());
        Assert.All(receipt.PrimePowers, factor => Assert.True(IsPrime(int.Parse(factor.PrimeDecimal, CultureInfo.InvariantCulture))));
    }

    [Fact]
    public void ExhaustiveSmallSignedDomainReconstructsAndDoesNotInventPrimes()
    {
        for (var value = -4_096; value <= 4_096; value++)
        {
            var receipt = PrimeReceiptCalculator.Analyze(new BigInteger(value));
            Assert.NotEqual(PrimeReceiptStatus.PartialBudget, receipt.Status);
            Assert.True(receipt.ReconstructionVerified);
            Assert.Equal(new BigInteger(value), PrimeReceiptCalculator.Reconstruct(receipt));

            var previous = BigInteger.Zero;
            foreach (var factor in receipt.PrimePowers)
            {
                var prime = BigInteger.Parse(factor.PrimeDecimal, CultureInfo.InvariantCulture);
                Assert.True(prime > previous);
                Assert.True(factor.Exponent > 0);
                Assert.True(IsPrime(checked((int)prime)));
                previous = prime;
            }
        }
    }

    [Fact]
    public void BudgetExhaustionReturnsExactResidualWithoutPrimeClaim()
    {
        var value = new BigInteger(2L * 3 * 1_009 * 1_013);
        var receipt = PrimeReceiptCalculator.Analyze(value, new PrimeReceiptPolicy(maxOddCandidates: 1));

        Assert.Equal(PrimeReceiptStatus.PartialBudget, receipt.Status);
        Assert.Equal(
            new[] { ("2", 1), ("3", 1) },
            receipt.PrimePowers.Select(factor => (factor.PrimeDecimal, factor.Exponent)).ToArray());
        Assert.Equal((1_009L * 1_013).ToString(CultureInfo.InvariantCulture), receipt.UnresolvedCofactorDecimal);
        Assert.Null(receipt.MagnitudeIsPrime);
        Assert.Null(receipt.IntegerIsPrime);
        Assert.True(receipt.ReconstructionVerified);
        Assert.Contains("UNRESOLVED", receipt.Structure, StringComparison.Ordinal);
        Assert.Equal(value, PrimeReceiptCalculator.Reconstruct(receipt));
    }

    [Fact]
    public void ReceiptIdentifiersAreDeterministicAndPolicySensitive()
    {
        var first = PrimeReceiptCalculator.Analyze(new BigInteger(12_345));
        var second = PrimeReceiptCalculator.Analyze(new BigInteger(12_345));
        var bounded = PrimeReceiptCalculator.Analyze(new BigInteger(12_345), new PrimeReceiptPolicy(0));

        Assert.Equal(first.ReceiptId, second.ReceiptId);
        Assert.Equal(first.PrimePowers, second.PrimePowers);
        Assert.Equal(first.Work, second.Work);
        Assert.NotEqual(first.ReceiptId, bounded.ReceiptId);
        Assert.True(PrimeReceiptCalculator.VerifyIntegrity(first));
        Assert.True(PrimeReceiptCalculator.VerifyIntegrity(bounded));
    }

    [Fact]
    public void ReceiptCollectionsAreReadOnlyAndTamperingCannotBeComposed()
    {
        var receipt = PrimeReceiptCalculator.Analyze(new BigInteger(6));
        var factors = Assert.IsAssignableFrom<IList<PrimePowerReceipt>>(receipt.PrimePowers);

        Assert.Throws<NotSupportedException>(() =>
            factors.Add(new PrimePowerReceipt("5", 1, PrimeFactorProofKind.TerminalResidualBound)));

        Assert.Empty(typeof(PrimeReceipt).GetConstructors());
        Assert.True(PrimeReceiptCalculator.VerifyIntegrity(receipt));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("1", true)]
    [InlineData("-1", true)]
    [InlineData("999999999999999999", true)]
    [InlineData("", false)]
    [InlineData("+1", false)]
    [InlineData("-0", false)]
    [InlineData("01", false)]
    [InlineData(" 1", false)]
    [InlineData("1_000", false)]
    [InlineData("1e3", false)]
    public void CanonicalIntegerParserRejectsAmbiguousText(string text, bool expected)
    {
        var parsed = PrimeReceiptCalculator.TryParseCanonicalInteger(text, 64, out _, out var error);

        Assert.Equal(expected, parsed);
        Assert.Equal(expected, string.IsNullOrEmpty(error));
    }

    [Fact]
    public void CanonicalIntegerParserEnforcesDigitLimit()
    {
        Assert.True(PrimeReceiptCalculator.TryParseCanonicalInteger("9999", 4, out var value, out _));
        Assert.Equal(new BigInteger(9_999), value);
        Assert.False(PrimeReceiptCalculator.TryParseCanonicalInteger("10000", 4, out _, out var error));
        Assert.Contains("4-digit", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactCompositionMergesReceiptsWithoutRefactoringOutput()
    {
        var operands = new BigInteger[] { 218, 489, 175, 17 };
        var inputs = operands.Select(value => PrimeReceiptCalculator.Analyze(value)).ToArray();
        var product = PrimeReceiptCalculator.ComposeExact(inputs);

        Assert.Equal(PrimeReceiptCalculator.CompositionAlgorithm, product.Algorithm);
        Assert.Equal(PrimeReceiptOrigin.ComposedFromReceipts, product.Origin);
        Assert.Equal(inputs.Select(receipt => receipt.ReceiptId), product.ParentReceiptIds);
        Assert.Equal(PrimeReceiptWork.Zero, product.Work);
        Assert.Equal(new BigInteger(317_140_950), PrimeReceiptCalculator.Reconstruct(product));
        Assert.Equal(
            new[] { ("2", 1), ("3", 1), ("5", 2), ("7", 1), ("17", 1), ("109", 1), ("163", 1) },
            product.PrimePowers.Select(factor => (factor.PrimeDecimal, factor.Exponent)).ToArray());
        Assert.All(product.PrimePowers, factor => Assert.Equal(PrimeFactorProofKind.InheritedFromParentReceipts, factor.ProofKind));
    }

    [Fact]
    public void CompositionPreservesZeroAndSign()
    {
        var zero = PrimeReceiptCalculator.ComposeExact(new[]
        {
            PrimeReceiptCalculator.Analyze(new BigInteger(-72)),
            PrimeReceiptCalculator.Analyze(BigInteger.Zero),
        });
        var negative = PrimeReceiptCalculator.ComposeExact(new[]
        {
            PrimeReceiptCalculator.Analyze(new BigInteger(-72)),
            PrimeReceiptCalculator.Analyze(new BigInteger(50)),
        });

        Assert.Equal(PrimeReceiptStatus.ExactZero, zero.Status);
        Assert.Equal(BigInteger.Zero, PrimeReceiptCalculator.Reconstruct(zero));
        Assert.Equal(new BigInteger(-3_600), PrimeReceiptCalculator.Reconstruct(negative));
        Assert.Equal("-(2^4 * 3^2 * 5^2)", negative.Structure);
    }

    [Fact]
    public void CompositionPreservesPositiveAndNegativeUnits()
    {
        var positive = PrimeReceiptCalculator.ComposeExact(new[]
        {
            PrimeReceiptCalculator.Analyze(BigInteger.MinusOne),
            PrimeReceiptCalculator.Analyze(BigInteger.MinusOne),
        });
        var negative = PrimeReceiptCalculator.ComposeExact(new[]
        {
            PrimeReceiptCalculator.Analyze(BigInteger.MinusOne),
            PrimeReceiptCalculator.Analyze(BigInteger.One),
        });

        Assert.Equal(PrimeReceiptStatus.ExactUnit, positive.Status);
        Assert.Equal("1", positive.Structure);
        Assert.True(PrimeReceiptCalculator.VerifyIntegrity(positive));
        Assert.Equal(PrimeReceiptStatus.ExactUnit, negative.Status);
        Assert.Equal("-1", negative.Structure);
        Assert.True(PrimeReceiptCalculator.VerifyIntegrity(negative));
    }

    [Fact]
    public void CompositionAndComparisonSnapshotCallerCollectionsOnce()
    {
        var exactInputs = new[]
        {
            PrimeReceiptCalculator.Analyze(new BigInteger(6)),
            PrimeReceiptCalculator.Analyze(new BigInteger(7)),
        };
        var laterInputs = new[]
        {
            PrimeReceiptCalculator.Analyze(new BigInteger(1_009L * 1_013), new PrimeReceiptPolicy(0)),
            PrimeReceiptCalculator.Analyze(new BigInteger(7)),
        };
        var product = PrimeReceiptCalculator.ComposeExact(
            new ChangingReadOnlyList<PrimeReceipt>(exactInputs, laterInputs));
        Assert.Equal(new BigInteger(42), PrimeReceiptCalculator.Reconstruct(product));

        var comparison = PrimeArithmeticComparison.CompareMultiplication(
            "SNAPSHOT_OPERANDS",
            new ChangingReadOnlyList<BigInteger>(
                new BigInteger[] { 2, 3 },
                new BigInteger[] { 5, 7 }));
        Assert.True(comparison.PathsAgree);
        Assert.Equal("6", comparison.PrimePath.ResultDecimal);
        Assert.Collection(
            comparison.OperandsDecimal,
            operand => Assert.Equal("2", operand),
            operand => Assert.Equal("3", operand));
    }

    [Fact]
    public void UserAdditionShowsMagnitudeCrossingAndFreshFactorDiscovery()
    {
        var comparison = PrimeArithmeticComparison.CompareAddition(
            "USER_ADD_001",
            new BigInteger(125_891_290_390),
            new BigInteger(12_589_127_501_265));

        Assert.True(comparison.PathsAgree);
        Assert.Equal("12715018791655", comparison.OrdinaryPath.ResultDecimal);
        Assert.Equal(14, comparison.OrdinaryPath.Work.DecimalColumns);
        Assert.Equal(5, comparison.OrdinaryPath.Work.CarryEvents);
        Assert.Equal("MAGNITUDE_ADD_THEN_FRESH_FACTOR_DISCOVERY", comparison.PrimePath.LocalityConclusion);
        Assert.Equal(1, comparison.PrimePath.Work.MagnitudeAdditions);
        Assert.Equal(0, comparison.PrimePath.Work.ExponentMerges);
        Assert.Equal(9, comparison.PrimePath.Work.ExponentReads);
        Assert.Equal(1, comparison.PrimePath.Work.ExponentWrites);
        Assert.Equal(3, comparison.PrimePath.Work.ReceiptConstructionReconstructions);
        Assert.Equal(2, comparison.PrimePath.Work.IntegrityReplayReconstructions);
        Assert.Equal(1, comparison.PrimePath.Work.EgressReconstructions);
        Assert.Equal(6, comparison.PrimePath.Work.Reconstructions);
        Assert.Contains(
            comparison.PrimePath.Events,
            item => item.Operation == "FACTOR_SUM" && item.OperationClass == "REQUIRES_FACTOR_DISCOVERY");
        Assert.Equal(
            new[] { ("5", 1), ("23", 1), ("31", 1), ("103", 1), ("34627429", 1) },
            comparison.PrimePath.OutputReceipt.PrimePowers
                .Select(factor => (factor.PrimeDecimal, factor.Exponent))
                .ToArray());
    }

    [Fact]
    public void UnitIngressDoesNotInventAConstructionReconstruction()
    {
        var comparison = PrimeArithmeticComparison.CompareAddition(
            "ADD_RADIX_BOUNDARY_001",
            BigInteger.One << 32,
            BigInteger.One);

        Assert.True(comparison.PathsAgree);
        Assert.Equal(2, comparison.PrimePath.Work.ReceiptConstructionReconstructions);
        Assert.Equal(2, comparison.PrimePath.Work.IntegrityReplayReconstructions);
        Assert.Equal(1, comparison.PrimePath.Work.EgressReconstructions);
        Assert.Equal(5, comparison.PrimePath.Work.Reconstructions);
    }

    [Fact]
    public void UserMultiplicationShowsExplicitIngressLocalMergeAndEgress()
    {
        var comparison = PrimeArithmeticComparison.CompareMultiplication(
            "USER_MUL_001",
            new BigInteger[] { 218, 489, 175, 17 });

        Assert.True(comparison.PathsAgree);
        Assert.Equal("317140950", comparison.PrimePath.ResultDecimal);
        Assert.Collection(
            comparison.OrdinaryPath.MultiplicationSteps,
            step => Assert.Equal("106602", step.AccumulatorAfterDecimal),
            step => Assert.Equal("18655350", step.AccumulatorAfterDecimal),
            step => Assert.Equal("317140950", step.AccumulatorAfterDecimal));
        Assert.Equal(3, comparison.OrdinaryPath.Work.SequentialMagnitudeMultiplications);
        Assert.Equal(PrimeReceiptCalculator.CompositionAlgorithm, comparison.PrimePath.OutputReceipt.Algorithm);
        Assert.Equal(4, comparison.PrimePath.Work.FactorizationCalls);
        Assert.Equal(5, comparison.PrimePath.Work.ReceiptConstructionReconstructions);
        Assert.Equal(4, comparison.PrimePath.Work.IntegrityReplayReconstructions);
        Assert.Equal(1, comparison.PrimePath.Work.EgressReconstructions);
        Assert.Equal(10, comparison.PrimePath.Work.Reconstructions);
        Assert.Contains(
            comparison.PrimePath.Events,
            item => item.Operation == "MERGE_EXPONENTS" && item.OperationClass == "REPRESENTATION_LOCAL");
        Assert.Contains(
            comparison.PrimePath.Events,
            item => item.Operation == "RECONSTRUCT_MAGNITUDE" && item.OperationClass == "CROSS_REPRESENTATION");
    }

    [Fact]
    public void MixedSignAdditionIsRejectedUntilBorrowTraceExists()
    {
        Assert.Throws<ArgumentException>(() => PrimeArithmeticComparison.CompareAddition(
            "MIXED_SIGN",
            new BigInteger(100),
            new BigInteger(-7)));

        var zeroPlusNegative = PrimeArithmeticComparison.CompareAddition(
            "ZERO_PLUS_NEGATIVE",
            BigInteger.Zero,
            new BigInteger(-7));
        Assert.True(zeroPlusNegative.PathsAgree);
        Assert.Equal("-7", zeroPlusNegative.OrdinaryPath.ResultDecimal);
        Assert.NotEmpty(zeroPlusNegative.OrdinaryPath.ColumnSteps);
    }

    [Fact]
    public void ZeroProductShortCircuitsWithoutInventedExponentWork()
    {
        var comparison = PrimeArithmeticComparison.CompareMultiplication(
            "ZERO_PRODUCT",
            new BigInteger[] { 0, 6, 35 });

        Assert.True(comparison.PathsAgree);
        Assert.Equal("0", comparison.PrimePath.ResultDecimal);
        Assert.Equal(0, comparison.PrimePath.Work.ExponentReads);
        Assert.Equal(0, comparison.PrimePath.Work.ExponentWrites);
        Assert.Equal(0, comparison.PrimePath.Work.ExponentMerges);
        Assert.Equal(2, comparison.PrimePath.Work.ReceiptConstructionReconstructions);
        Assert.Equal(3, comparison.PrimePath.Work.IntegrityReplayReconstructions);
        Assert.Equal(1, comparison.PrimePath.Work.EgressReconstructions);
        Assert.Equal(6, comparison.PrimePath.Work.Reconstructions);
        Assert.Contains(comparison.PrimePath.Events, item => item.Operation == "ZERO_TAG_SHORT_CIRCUIT");
        Assert.DoesNotContain(comparison.PrimePath.Events, item => item.Operation == "MERGE_EXPONENTS");
    }

    [Fact]
    public void CommonFactorKeepsDerivedProvenanceKind()
    {
        var comparison = PrimeArithmeticComparison.CompareAddition(
            "COMMON_FACTOR",
            new BigInteger(5),
            new BigInteger(10));

        var common = Assert.Single(comparison.PrimePath.CommonCertifiedPrimePowers);
        Assert.Equal("5", common.PrimeDecimal);
        Assert.Equal(PrimeFactorProofKind.InheritedFromParentReceipts, common.ProofKind);
        Assert.Equal(3, comparison.PrimePath.Work.ExponentReads);
        Assert.Equal(1, comparison.PrimePath.Work.ExponentWrites);
    }

    private static bool IsPrime(int value)
    {
        if (value < 2)
        {
            return false;
        }

        if (value == 2)
        {
            return true;
        }

        if ((value & 1) == 0)
        {
            return false;
        }

        for (var candidate = 3; (long)candidate * candidate <= value; candidate += 2)
        {
            if (value % candidate == 0)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class ChangingReadOnlyList<T>(
        IReadOnlyList<T> firstEnumeration,
        IReadOnlyList<T> laterEnumerations) : IReadOnlyList<T>
    {
        private int _enumerations;

        public int Count => firstEnumeration.Count;

        public T this[int index] => firstEnumeration[index];

        public IEnumerator<T> GetEnumerator()
        {
            var source = _enumerations++ == 0 ? firstEnumeration : laterEnumerations;
            return source.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
