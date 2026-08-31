using PrimeAxiom.Core.Build005.Valuation;

namespace PrimeAxiom.Tests;

public sealed class Build005ValuationTests
{
    private static readonly int[] FrozenCompositeDivisors = [4, 6, 9, 10, 15, 21, 25, 27, 33, 35];
    private static readonly int[] SmallCompositeDivisors = [6, 9];

    [Theory]
    [InlineData(8, 255UL)]
    [InlineData(16, 65_535UL)]
    [InlineData(32, 4_294_967_295UL)]
    public void FrozenWidthsHaveExactUnsignedRanges(int width, ulong maximum)
    {
        var service = new DemandValuationService(
            width,
            ValuationCachePolicy.BinDirectBest,
            cacheCapacity: 0);

        Assert.Equal(maximum, service.MaximumMagnitude);
        Assert.Equal(DemandValuationService.SlotCount, 4);
    }

    [Fact]
    public void ConstructorRejectsUnfrozenWidthsCapacitiesAndDirectCache()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DemandValuationService(64, ValuationCachePolicy.BinDirectBest, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DemandValuationService(8, ValuationCachePolicy.BinFrontierNoPropK, 3));
        Assert.Throws<ArgumentException>(
            () => new DemandValuationService(8, ValuationCachePolicy.BinDirectBest, 1));
    }

    [Fact]
    public void CataloguePrimeIdentityIsNotACallerAssertion()
    {
        var service = FrontierService(width: 8, capacity: 1);
        Assert.True(service.Load(0, 36).Succeeded);

        var composite = service.Valuation(0, 6);

        Assert.False(composite.Succeeded);
        Assert.Equal(ValuationFailure.InvalidPrime, composite.Failure);
        Assert.Equal(1, composite.MetricsDelta.RejectedOperations);
        Assert.All(service.SnapshotCache(), line => Assert.False(line.Valid));
    }

    [Fact]
    public void PublicFrontierValidatorEnforcesEquationTerminalAndZeroContracts()
    {
        var exact = new ValuationFrontier(
            Valid: true,
            Slot: 0,
            Generation: 1,
            PrimeIndex: PrimeIndex(3),
            LowerBound: 3,
            Residual: 8,
            Terminal: true,
            Infinite: false);
        Assert.True(exact.Satisfies(216, out var exactDetail), exactDetail);

        var partial = exact with { LowerBound = 2, Residual = 24, Terminal = false };
        Assert.True(partial.Satisfies(216, out var partialDetail), partialDetail);

        var malformed = exact with { Residual = 9 };
        Assert.False(malformed.Satisfies(216, out _));
        Assert.False((exact with { Terminal = true, LowerBound = 2, Residual = 24 }).Satisfies(216, out _));

        var zero = exact with
        {
            LowerBound = 0,
            Residual = 0,
            Terminal = true,
            Infinite = true,
        };
        Assert.True(zero.Satisfies(0, out var zeroDetail), zeroDetail);
        Assert.False(zero.Satisfies(1, out _));
        Assert.False((zero with { LowerBound = 1 }).Satisfies(0, out _));
        Assert.False((zero with { Residual = 1 }).Satisfies(0, out _));
    }

    [Fact]
    public void ZeroIsInfiniteAndOneIsTerminalZeroWithoutOrdinaryFactorPayload()
    {
        var service = FrontierService(width: 8, capacity: 2);
        Assert.True(service.Load(0, 0).Succeeded);
        Assert.True(service.Load(1, 1).Succeeded);

        var zero = AssertSuccess(service.Valuation(0, 31));
        var one = AssertSuccess(service.StripAll(1, 31));
        var arbitraryZeroThreshold = AssertSuccess(service.TestPower(0, 31, 1000));

        Assert.True(zero.Infinite);
        Assert.Equal(0, zero.Exponent);
        Assert.Equal(0UL, zero.Residual);
        Assert.True(arbitraryZeroThreshold.IsAtLeastThreshold);
        Assert.True(arbitraryZeroThreshold.Infinite);
        Assert.False(one.Infinite);
        Assert.Equal(0, one.Exponent);
        Assert.Equal(1UL, one.Residual);
    }

    [Fact]
    public void ThresholdFrontierResumesWithoutRepeatingExactDivisions()
    {
        var service = FrontierService(width: 8, capacity: 1);
        Assert.True(service.Load(0, 216).Succeeded);

        var first = service.TestPower(0, 3, 2);
        var second = service.TestPower(0, 3, 3);
        var exact = service.Valuation(0, 3);
        var repeated = service.Valuation(0, 3);

        Assert.True(AssertSuccess(first).IsAtLeastThreshold);
        Assert.Equal(2, first.Value!.CertifiedLowerBound);
        Assert.False(first.Value.Terminal);
        Assert.Equal(24UL, first.Value.Residual);
        Assert.Equal(2, first.MetricsDelta.DivModCalls);
        Assert.Equal(2, first.MetricsDelta.ExactDivisions);
        Assert.Equal(1, first.MetricsDelta.CacheFills);

        Assert.True(AssertSuccess(second).IsAtLeastThreshold);
        Assert.Equal(3, second.Value!.CertifiedLowerBound);
        Assert.Equal(8UL, second.Value.Residual);
        Assert.Equal(1, second.MetricsDelta.CacheHits);
        Assert.Equal(1, second.MetricsDelta.DivModCalls);
        Assert.Equal(1, second.MetricsDelta.ExactDivisions);
        Assert.Equal(1, second.MetricsDelta.CacheUpdates);

        Assert.Equal(3, AssertSuccess(exact).Exponent);
        Assert.Equal(8UL, exact.Value!.Residual);
        Assert.Equal(1, exact.MetricsDelta.DivModCalls);
        Assert.Equal(0, exact.MetricsDelta.ExactDivisions);
        Assert.Equal(1, exact.MetricsDelta.FailedDivisibilityProbes);
        Assert.Equal(1, exact.MetricsDelta.TerminalCertificatesEarned);

        Assert.Equal(3, AssertSuccess(repeated).Exponent);
        Assert.Equal(1, repeated.MetricsDelta.CacheHits);
        Assert.Equal(0, repeated.MetricsDelta.DivModCalls);

        var line = Assert.Single(service.SnapshotCache(), candidate => candidate.Valid);
        Assert.Equal(3, line.LowerBound);
        Assert.Equal(8UL, line.Residual);
        Assert.True(line.Terminal);
    }

    [Fact]
    public void FirstFailedOddProbeIsACacheableNegativeTerminalReceipt()
    {
        var service = FrontierService(width: 8, capacity: 1);
        Assert.True(service.Load(0, 216).Succeeded);

        var cold = service.TestPower(0, 5, 1);
        var warm = service.TestPower(0, 5, 1);

        Assert.False(AssertSuccess(cold).IsAtLeastThreshold);
        Assert.True(cold.Value!.Terminal);
        Assert.Equal(0, cold.Value.CertifiedLowerBound);
        Assert.Equal(216UL, cold.Value.Residual);
        Assert.Equal(1, cold.MetricsDelta.DivModCalls);
        Assert.Equal(1, cold.MetricsDelta.FailedDivisibilityProbes);
        Assert.Equal(1, cold.MetricsDelta.CacheFills);

        Assert.False(AssertSuccess(warm).IsAtLeastThreshold);
        Assert.Equal(1, warm.MetricsDelta.CacheHits);
        Assert.Equal(1, warm.MetricsDelta.NegativeCacheHits);
        Assert.Equal(0, warm.MetricsDelta.DivModCalls);
    }

    [Fact]
    public void RadixTwoPathUsesCtzAccountingAndNotOddDivmod()
    {
        var service = DirectService(width: 8);
        Assert.True(service.Load(0, 40).Succeeded);

        var answer = service.Valuation(0, 2);

        Assert.Equal(3, AssertSuccess(answer).Exponent);
        Assert.Equal(5UL, answer.Value!.Residual);
        Assert.Equal(1, answer.MetricsDelta.CtzCalls);
        Assert.Equal(4, answer.MetricsDelta.CtzBitInspections);
        Assert.Equal(3, answer.MetricsDelta.ExactDivisions);
        Assert.Equal(1, answer.MetricsDelta.FailedDivisibilityProbes);
        Assert.Equal(0, answer.MetricsDelta.DivModCalls);
        Assert.Equal(0, answer.MetricsDelta.CacheLookups);
    }

    [Fact]
    public void ContentCacheReusesEqualImmutableMagnitudesAcrossSlotIdentity()
    {
        var service = new DemandValuationService(
            8,
            ValuationCachePolicy.BinContentAnswerLruK,
            cacheCapacity: 2);
        Assert.True(service.Load(0, 72).Succeeded);
        var cold = service.Valuation(0, 3);
        Assert.True(service.Load(1, 72).Succeeded);

        var warm = service.Valuation(1, 3);

        Assert.Equal(2, AssertSuccess(cold).Exponent);
        Assert.Equal(2, AssertSuccess(warm).Exponent);
        Assert.Equal(1, warm.MetricsDelta.CacheHits);
        Assert.Equal(0, warm.MetricsDelta.DivModCalls);
        var line = Assert.Single(service.SnapshotCache(), candidate => candidate.Valid);
        Assert.Equal(72UL, line.ContentMagnitude);
        Assert.Equal(-1, line.Slot);
    }

    [Fact]
    public void ContentAnswerControlDiscardsPositivePartialThresholdWork()
    {
        var service = new DemandValuationService(
            16,
            ValuationCachePolicy.BinContentAnswerLruK,
            cacheCapacity: 2);
        Assert.True(service.Load(0, 243).Succeeded);

        var threshold = service.TestPower(0, 3, 2);
        var exact = service.Valuation(0, 3);

        Assert.True(AssertSuccess(threshold).IsAtLeastThreshold);
        Assert.All(service.SnapshotCache().Where(line => line.PrimeIndex == PrimeIndex(3)), line => Assert.True(line.Terminal));
        Assert.Equal(5, AssertSuccess(exact).Exponent);
        Assert.Equal(5, exact.MetricsDelta.ExactDivisions);
        Assert.Equal(6, exact.MetricsDelta.DivModCalls);
        Assert.Equal(1, exact.MetricsDelta.CacheFills);
    }

    [Fact]
    public void DeterministicLruUsesFirstEmptyThenLeastRecentPhysicalLine()
    {
        var service = new DemandValuationService(
            8,
            ValuationCachePolicy.BinContentAnswerLruK,
            cacheCapacity: 2);
        Assert.True(service.Load(0, 30).Succeeded);
        Assert.True(service.Valuation(0, 2).Succeeded); // physical 0
        Assert.True(service.Valuation(0, 3).Succeeded); // physical 1
        Assert.True(service.Valuation(0, 2).Succeeded); // physical 0 becomes MRU

        var eviction = service.Valuation(0, 5);

        Assert.Equal(1, eviction.MetricsDelta.CacheEvictions);
        var lines = service.SnapshotCache();
        Assert.Equal(PrimeIndex(2), lines[0].PrimeIndex);
        Assert.Equal(PrimeIndex(5), lines[1].PrimeIndex);
        Assert.True(lines[0].LastUseTick < lines[1].LastUseTick);
    }

    [Fact]
    public void MutationVersionsPriorFrontierAndStaleTagIsACountedMissNotAHint()
    {
        var service = FrontierService(width: 8, capacity: 2);
        Assert.True(service.Load(0, 12).Succeeded);
        Assert.True(service.Load(1, 1).Succeeded);
        Assert.True(service.Valuation(0, 3).Succeeded);
        var oldGeneration = service.SnapshotSlot(0).Generation;

        var mutation = service.Add(0, 0, 1);
        var fresh = service.Valuation(0, 3);

        Assert.Equal(1, mutation.MetricsDelta.CacheInvalidations);
        Assert.Equal(13UL, service.SnapshotSlot(0).Magnitude);
        Assert.NotEqual(oldGeneration, service.SnapshotSlot(0).Generation);
        Assert.Equal(1, fresh.MetricsDelta.RejectedStaleHitAttempts);
        Assert.Equal(1, fresh.MetricsDelta.CacheMisses);
        Assert.Equal(1, fresh.MetricsDelta.DivModCalls);
        Assert.Equal(0, AssertSuccess(fresh).Exponent);

        var generations = service.SnapshotCache()
            .Where(line => line.Valid && line.Slot == 0 && line.PrimeIndex == PrimeIndex(3))
            .Select(line => line.Generation)
            .Order()
            .ToArray();
        Assert.Equal([oldGeneration, service.SnapshotSlot(0).Generation], generations);
    }

    [Fact]
    public void EightBitGenerationWrapFlushesEveryValidLineBeforeTagReuse()
    {
        var service = FrontierService(width: 8, capacity: 2);
        Assert.True(service.Load(0, 12).Succeeded); // generation 1
        Assert.True(service.Valuation(0, 3).Succeeded);
        for (var index = 0; index < 254; index++)
        {
            Assert.True(service.Load(0, 12).Succeeded);
        }

        Assert.Equal(byte.MaxValue, service.SnapshotSlot(0).Generation);
        Assert.Contains(service.SnapshotCache(), line => line.Valid);

        var wrap = service.Load(0, 12);

        Assert.True(AssertSuccess(wrap).GenerationWrapped);
        Assert.Equal((byte)0, wrap.Value!.Generation);
        Assert.Equal(1, wrap.MetricsDelta.GenerationWrapFlushes);
        Assert.Equal(1, wrap.MetricsDelta.CacheLinesFlushed);
        Assert.All(service.SnapshotCache(), line => Assert.False(line.Valid));
    }

    [Fact]
    public void ContentCacheSurvivesIrrelevantSlotGenerationWrap()
    {
        var service = new DemandValuationService(
            8,
            ValuationCachePolicy.BinContentAnswerLruK,
            1);
        Assert.True(service.Load(0, 12).Succeeded);
        Assert.Equal(1, AssertSuccess(service.Valuation(0, 3)).Exponent);
        for (var index = 0; index < 254; index++)
        {
            Assert.True(service.Load(0, 12).Succeeded);
        }

        var wrap = service.Load(0, 12);
        var beforeReuse = service.Metrics;
        var reused = service.Valuation(0, 3);

        Assert.True(AssertSuccess(wrap).GenerationWrapped);
        Assert.Equal(0, wrap.MetricsDelta.GenerationWrapFlushes);
        Assert.Equal(0, wrap.MetricsDelta.CacheLinesFlushed);
        Assert.Equal(beforeReuse.DivModCalls, service.Metrics.DivModCalls);
        Assert.Equal(beforeReuse.CacheHits + 1, service.Metrics.CacheHits);
        Assert.Equal(1, AssertSuccess(reused).Exponent);
    }

    [Theory]
    [InlineData(8, 200UL, 2UL)]
    [InlineData(16, 40_000UL, 2UL)]
    [InlineData(32, 4_294_967_295UL, 2UL)]
    public void MultiplicationOverflowIsAtomicIncludingAliasAndCacheState(
        int width,
        ulong left,
        ulong right)
    {
        var service = PrimePropagationService(width, capacity: 2);
        Assert.True(service.Load(0, left).Succeeded);
        Assert.True(service.Load(1, right).Succeeded);
        Assert.True(service.Valuation(0, 2).Succeeded);
        var slotBefore = service.SnapshotSlot(0);
        var cacheBefore = service.SnapshotCache().ToArray();

        var rejected = service.Multiply(0, 0, 1);

        Assert.False(rejected.Succeeded);
        Assert.Equal(ValuationFailure.Overflow, rejected.Failure);
        Assert.Equal(1, rejected.MetricsDelta.RejectedOperations);
        Assert.Equal(0, rejected.MetricsDelta.CacheLookups);
        Assert.Equal(slotBefore, service.SnapshotSlot(0));
        Assert.Equal(cacheBefore, service.SnapshotCache());
    }

    [Fact]
    public void AdditionOverflowIsAtomic()
    {
        var service = FrontierService(width: 8, capacity: 1);
        Assert.True(service.Load(0, 250).Succeeded);
        Assert.True(service.Load(1, 6).Succeeded);
        Assert.True(service.Valuation(0, 2).Succeeded);
        var slotBefore = service.SnapshotSlot(0);
        var cacheBefore = service.SnapshotCache().ToArray();

        var rejected = service.Add(0, 0, 1);

        Assert.False(rejected.Succeeded);
        Assert.Equal(ValuationFailure.Overflow, rejected.Failure);
        Assert.Equal(slotBefore, service.SnapshotSlot(0));
        Assert.Equal(cacheBefore, service.SnapshotCache());
    }

    [Fact]
    public void SuccessfulArithmeticCapturesAliasOperandsBeforeDestinationCommit()
    {
        var service = DirectService(width: 8);
        Assert.True(service.Load(0, 6).Succeeded);
        Assert.True(service.Load(1, 5).Succeeded);

        Assert.Equal(30UL, AssertSuccess(service.Multiply(0, 0, 1)).Magnitude);
        Assert.Equal(35UL, AssertSuccess(service.Add(0, 0, 1)).Magnitude);
        Assert.True(service.Load(0, 12).Succeeded);
        Assert.Equal(144UL, AssertSuccess(service.Multiply(0, 0, 0)).Magnitude);
    }

    [Fact]
    public void PrimeTerminalReceiptsPropagateAcrossMultiplyWithoutNewSearch()
    {
        var service = PrimePropagationService(width: 16, capacity: 4);
        Assert.True(service.Load(0, 8).Succeeded);
        Assert.True(service.Load(1, 9).Succeeded);
        Assert.Equal(3, AssertSuccess(service.Valuation(0, 2)).Exponent);
        Assert.Equal(0, AssertSuccess(service.Valuation(1, 2)).Exponent);

        var multiply = service.Multiply(2, 0, 1);
        var output = service.Valuation(2, 2);

        Assert.Equal(72UL, AssertSuccess(multiply).Magnitude);
        Assert.Equal(1, multiply.Value!.PropagatedFrontiers);
        Assert.Equal(1, multiply.MetricsDelta.CacheTransfers);
        Assert.Equal(1, multiply.MetricsDelta.TerminalCertificatesPropagated);
        Assert.Equal(3, AssertSuccess(output).Exponent);
        Assert.Equal(9UL, output.Value!.Residual);
        Assert.Equal(1, output.MetricsDelta.CacheHits);
        Assert.Equal(0, output.MetricsDelta.CtzCalls);
        Assert.Equal(0, output.MetricsDelta.ExactDivisions);
    }

    [Fact]
    public void NoPropagationControlMustSearchTheSameProduct()
    {
        var service = FrontierService(width: 16, capacity: 4);
        Assert.True(service.Load(0, 8).Succeeded);
        Assert.True(service.Load(1, 9).Succeeded);
        Assert.True(service.Valuation(0, 2).Succeeded);
        Assert.True(service.Valuation(1, 2).Succeeded);

        var multiply = service.Multiply(2, 0, 1);
        var output = service.Valuation(2, 2);

        Assert.Equal(0, AssertSuccess(multiply).PropagatedFrontiers);
        Assert.Equal(3, AssertSuccess(output).Exponent);
        Assert.Equal(1, output.MetricsDelta.CtzCalls);
        Assert.Equal(3, output.MetricsDelta.ExactDivisions);
    }

    [Fact]
    public void PartialFrontiersAlsoCombineAsExactEquationsThenResume()
    {
        var service = PrimePropagationService(width: 8, capacity: 4);
        Assert.True(service.Load(0, 27).Succeeded);
        Assert.True(service.Load(1, 9).Succeeded);
        Assert.True(service.TestPower(0, 3, 1).Succeeded);
        Assert.True(service.TestPower(1, 3, 1).Succeeded);

        var multiply = service.Multiply(2, 0, 1);
        var threshold = service.TestPower(2, 3, 2);
        var exact = service.Valuation(2, 3);

        Assert.Equal(243UL, AssertSuccess(multiply).Magnitude);
        Assert.Equal(1, multiply.MetricsDelta.LowerBoundCertificatesPropagated);
        Assert.True(AssertSuccess(threshold).IsAtLeastThreshold);
        Assert.Equal(2, threshold.Value!.CertifiedLowerBound);
        Assert.Equal(0, threshold.MetricsDelta.DivModCalls);
        Assert.Equal(5, AssertSuccess(exact).Exponent);
        Assert.Equal(3, exact.MetricsDelta.ExactDivisions);
        Assert.Equal(4, exact.MetricsDelta.DivModCalls);
    }

    [Fact]
    public void MultiplyByPrimeConstructsAResumableLowerBoundAndTransfersOtherReceipts()
    {
        var service = PrimePropagationService(width: 16, capacity: 2);
        Assert.True(service.Load(0, 10).Succeeded);
        Assert.Equal(1, AssertSuccess(service.Valuation(0, 2)).Exponent);

        var multiply = service.MultiplyByPrime(1, 0, 3);
        var p3Threshold = service.TestPower(1, 3, 1);
        var p3Exact = service.Valuation(1, 3);
        var p2Exact = service.Valuation(1, 2);

        Assert.Equal(30UL, AssertSuccess(multiply).Magnitude);
        Assert.Equal(2, multiply.Value!.PropagatedFrontiers);
        Assert.Equal(2, multiply.MetricsDelta.CacheTransfers);
        Assert.True(AssertSuccess(p3Threshold).IsAtLeastThreshold);
        Assert.Equal(0, p3Threshold.MetricsDelta.DivModCalls);
        Assert.Equal(1, AssertSuccess(p3Exact).Exponent);
        Assert.Equal(1, p3Exact.MetricsDelta.DivModCalls);
        Assert.Equal(1, AssertSuccess(p2Exact).Exponent);
        Assert.Equal(15UL, p2Exact.Value!.Residual);
        Assert.Equal(0, p2Exact.MetricsDelta.CtzCalls);
    }

    [Fact]
    public void AdditionNeverReceivesPrimePropagationCredit()
    {
        var service = PrimePropagationService(width: 8, capacity: 4);
        Assert.True(service.Load(0, 8).Succeeded);
        Assert.True(service.Load(1, 1).Succeeded);
        Assert.True(service.Valuation(0, 2).Succeeded);

        var add = service.Add(2, 0, 1);
        var output = service.Valuation(2, 2);

        Assert.Equal(9UL, AssertSuccess(add).Magnitude);
        Assert.Equal(0, add.Value!.PropagatedFrontiers);
        Assert.Equal(0, add.MetricsDelta.CacheTransfers);
        Assert.Equal(0, AssertSuccess(output).Exponent);
        Assert.Equal(1, output.MetricsDelta.CtzCalls);
    }

    [Fact]
    public void ExhaustiveW8AllValuesAllCataloguePrimesMatchDirectOracleForEveryPolicy()
    {
        var services = new[]
        {
            DirectService(8),
            new DemandValuationService(8, ValuationCachePolicy.BinContentAnswerLruK, 4),
            FrontierService(8, 4),
            PrimePropagationService(8, 4),
        };

        for (ulong magnitude = 0; magnitude <= byte.MaxValue; magnitude++)
        {
            foreach (var service in services)
            {
                Assert.True(service.Load(0, magnitude).Succeeded);
            }

            foreach (var prime in Build005PrimeCatalogue.Primes)
            {
                var expected = Oracle(magnitude, prime);
                foreach (var service in services)
                {
                    var actual = AssertSuccess(service.StripAll(0, prime));
                    Assert.Equal(expected.Infinite, actual.Infinite);
                    Assert.Equal(expected.Exponent, actual.Exponent);
                    Assert.Equal(expected.Residual, actual.Residual);
                }
            }
        }
    }

    [Fact]
    public void ExhaustiveW8CheckedAddAndMultiplyMatchUnsignedArithmeticAndRejectOverflow()
    {
        var service = DirectService(8);
        for (ulong left = 0; left <= byte.MaxValue; left++)
        {
            Assert.True(service.Load(0, left).Succeeded);
            for (ulong right = 0; right <= byte.MaxValue; right++)
            {
                Assert.True(service.Load(1, right).Succeeded);

                var add = service.Add(2, 0, 1);
                var expectedSum = left + right;
                Assert.Equal(expectedSum <= byte.MaxValue, add.Succeeded);
                if (add.Succeeded)
                {
                    Assert.Equal(expectedSum, add.Value!.Magnitude);
                }
                else
                {
                    Assert.Equal(ValuationFailure.Overflow, add.Failure);
                }

                var multiply = service.Multiply(3, 0, 1);
                var expectedProduct = left * right;
                Assert.Equal(expectedProduct <= byte.MaxValue, multiply.Succeeded);
                if (multiply.Succeeded)
                {
                    Assert.Equal(expectedProduct, multiply.Value!.Magnitude);
                }
                else
                {
                    Assert.Equal(ValuationFailure.Overflow, multiply.Failure);
                }
            }
        }
    }

    [Theory]
    [InlineData(16, 65_535UL, 3, 1, 21_845UL)]
    [InlineData(32, 4_294_967_295UL, 3, 1, 1_431_655_765UL)]
    [InlineData(32, 4_294_967_296UL, 2, -1, 0UL)]
    public void W16AndW32BoundariesAreExactOrTypedRejections(
        int width,
        ulong magnitude,
        int prime,
        int expectedExponent,
        ulong expectedResidual)
    {
        var service = FrontierService(width, 1);
        var load = service.Load(0, magnitude);
        if (expectedExponent < 0)
        {
            Assert.False(load.Succeeded);
            Assert.Equal(ValuationFailure.MagnitudeOutOfRange, load.Failure);
            return;
        }

        Assert.True(load.Succeeded);
        var answer = AssertSuccess(service.StripAll(0, prime));
        Assert.Equal(expectedExponent, answer.Exponent);
        Assert.Equal(expectedResidual, answer.Residual);
    }

    [Fact]
    public void InvalidAndUninitializedOperationsReturnTypedFailuresWithoutOutput()
    {
        var service = FrontierService(8, 1);

        var uninitialized = service.Valuation(0, 2);
        var invalidSlot = service.Load(4, 1);
        Assert.True(service.Load(0, 1).Succeeded);
        var invalidThreshold = service.TestPower(0, 2, -1);

        Assert.False(uninitialized.Succeeded);
        Assert.Equal(ValuationFailure.UninitializedSlot, uninitialized.Failure);
        Assert.Null(uninitialized.Value);
        Assert.Equal(ValuationFailure.InvalidSlot, invalidSlot.Failure);
        Assert.Equal(ValuationFailure.InvalidThreshold, invalidThreshold.Failure);
    }

    [Fact]
    public void CompositeControlUsesTheSameFrontierCacheButNeverPrimePropagation()
    {
        var service = DemandValuationService.CreateCompositeControl(
            8,
            ValuationCachePolicy.BinPrimeFrontierPropK,
            4,
            FrozenCompositeDivisors);

        Assert.False(service.MultiplicativePropagationEnabled);
        Assert.Equal(FrozenCompositeDivisors, service.DivisorCatalogue);
        Assert.True(service.Load(0, 2).Succeeded);
        Assert.True(service.Load(1, 3).Succeeded);
        Assert.Equal(0, AssertSuccess(service.Valuation(0, 6)).Exponent);
        Assert.Equal(0, AssertSuccess(service.Valuation(1, 6)).Exponent);

        var beforeMultiply = service.Metrics;
        Assert.True(service.Multiply(2, 0, 1).Succeeded);
        Assert.Equal(
            beforeMultiply.TerminalCertificatesPropagated,
            service.Metrics.TerminalCertificatesPropagated);
        Assert.DoesNotContain(
            service.SnapshotCache(),
            line => line.Valid && line.Slot == 2 && line.PrimeIndex == 1);

        var answer = AssertSuccess(service.Valuation(2, 6));
        Assert.Equal(1, answer.Exponent);
        Assert.Equal(1UL, answer.Residual);
    }

    [Fact]
    public void CompositeContentControlReusesOnlyTheSameMagnitudeAndDivisor()
    {
        var service = DemandValuationService.CreateCompositeControl(
            16,
            ValuationCachePolicy.BinContentAnswerLruK,
            1,
            SmallCompositeDivisors);
        Assert.True(service.Load(0, 216).Succeeded);

        var first = AssertSuccess(service.Valuation(0, 6));
        var beforeHit = service.Metrics;
        var second = AssertSuccess(service.Valuation(0, 6));
        Assert.Equal(first, second);
        Assert.Equal(beforeHit.DivModCalls, service.Metrics.DivModCalls);
        Assert.Equal(beforeHit.CacheHits + 1, service.Metrics.CacheHits);

        _ = AssertSuccess(service.Valuation(0, 9));
        Assert.True(service.Metrics.CacheEvictions > 0);
    }

    private static DemandValuationService DirectService(int width) =>
        new(width, ValuationCachePolicy.BinDirectBest, 0);

    private static DemandValuationService FrontierService(int width, int capacity) =>
        new(width, ValuationCachePolicy.BinFrontierNoPropK, capacity);

    private static DemandValuationService PrimePropagationService(int width, int capacity) =>
        new(width, ValuationCachePolicy.BinPrimeFrontierPropK, capacity);

    private static int PrimeIndex(int prime)
    {
        Assert.True(Build005PrimeCatalogue.TryGetIndex(prime, out var index));
        return index;
    }

    private static T AssertSuccess<T>(ValuationOperationResult<T> result)
        where T : class
    {
        Assert.True(result.Succeeded, $"{result.Failure}: {result.Detail}");
        return Assert.IsType<T>(result.Value);
    }

    private static ExactValuationAnswer Oracle(ulong magnitude, int prime)
    {
        if (magnitude == 0)
        {
            return new ExactValuationAnswer(0, 0, Infinite: true);
        }

        var exponent = 0;
        var residual = magnitude;
        while (residual % (ulong)prime == 0)
        {
            residual /= (ulong)prime;
            exponent++;
        }

        return new ExactValuationAnswer(exponent, residual, Infinite: false);
    }
}
