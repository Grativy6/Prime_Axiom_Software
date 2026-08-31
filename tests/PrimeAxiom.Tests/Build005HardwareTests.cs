using PrimeAxiom.Core.Build005.Hardware;
using PrimeAxiom.Core.Hardware;

namespace PrimeAxiom.Tests;

public sealed class Build005HardwareTests
{
    private static readonly int[] Widths = [8, 16, 32];

    [Fact]
    public void FrozenHardwareDomainRejectsUnregisteredWidthsAndCapacities()
    {
        Assert.Equal(Widths, Build005HardwareDomain.SupportedWidths);
        Assert.Equal([0, 1, 2, 4], Build005HardwareDomain.SupportedCacheCapacities);
        Assert.Equal([2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31], Build005HardwareDomain.PrimeCatalogue);
        Assert.Throws<ArgumentOutOfRangeException>(() => RadixAwareValuationHardware.BuildCtz(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => RadixAwareValuationHardware.BuildOddDivmodMachine(64));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrontierCacheHardware.Build(8, 3));
    }

    [Fact]
    public void CtzIsExhaustiveAtW8AndCorrectAtDecisionWidthBoundaries()
    {
        var ctz8 = RadixAwareValuationHardware.BuildCtz(8);
        for (uint value = 0; value < 256; value++)
        {
            var actual = ctz8.Evaluate(value);
            Assert.Equal(value == 0 ? 8 : OracleCtz(value), actual.Count);
            Assert.Equal(value == 0, actual.Zero);
            Assert.Equal(ctz8.Metrics.Nand2Static, actual.NandEvaluations);
        }

        foreach (var width in new[] { 16, 32 })
        {
            var ctz = RadixAwareValuationHardware.BuildCtz(width);
            foreach (var value in BoundaryValues(width))
            {
                var actual = ctz.Evaluate(value, compareWithAllOff: true);
                Assert.Equal(value == 0 ? width : OracleCtz(value), actual.Count);
                Assert.Equal(value == 0, actual.Zero);
                Assert.InRange(actual.NandOutputTransitions, 0, ctz.Metrics.Nand2Static);
            }
        }
    }

    [Fact]
    public void PrimeCatalogueSelectorRejectsSpareIndicesAndSeparatesRadixTwo()
    {
        foreach (var width in Widths)
        {
            var catalogue = RadixAwareValuationHardware.BuildPrimeCatalogueSelector(width);
            for (uint index = 0; index < 16; index++)
            {
                var actual = catalogue.Evaluate(index);
                var valid = index < Build005HardwareDomain.PrimeCatalogue.Count;
                Assert.Equal(valid, actual.Valid);
                Assert.Equal(index == 0, actual.IsTwo);
                Assert.Equal(valid && index != 0, actual.IsOdd);
                Assert.Equal(
                    valid ? checked((uint)Build005HardwareDomain.PrimeCatalogue[checked((int)index)]) : 0U,
                    actual.Divisor);
            }

            Assert.Equal(0, catalogue.Metrics.DffStatic);
            Assert.Equal(CombinationalLoopStatus.Acyclic, catalogue.Metrics.CombinationalLoopStatus);
        }
    }

    [Fact]
    public void CtzAndOddDividerHaveDeterministicAcyclicNandOnlyGraphs()
    {
        foreach (var width in Widths)
        {
            var firstCtz = RadixAwareValuationHardware.BuildCtz(width);
            var secondCtz = RadixAwareValuationHardware.BuildCtz(width);
            Assert.Equal(firstCtz.Netlist.Nodes, secondCtz.Netlist.Nodes);
            Assert.Equal(firstCtz.Netlist.Metrics, secondCtz.Netlist.Metrics);
            Assert.Equal(0, firstCtz.Metrics.DffStatic);
            Assert.Equal(CombinationalLoopStatus.Acyclic, firstCtz.Metrics.CombinationalLoopStatus);

            var firstDivider = RadixAwareValuationHardware.BuildOddDivmodMachine(width);
            var secondDivider = RadixAwareValuationHardware.BuildOddDivmodMachine(width);
            Assert.Equal(firstDivider.Netlist.Nodes, secondDivider.Netlist.Nodes);
            Assert.Equal(firstDivider.Netlist.DffBoundaries, secondDivider.Netlist.DffBoundaries);
            Assert.Equal(firstDivider.Netlist.Metrics, secondDivider.Netlist.Metrics);
            Assert.Equal((4 * width) + firstDivider.CountWidth + 4, firstDivider.Metrics.DffStatic);
            Assert.Equal(firstDivider.Metrics.DffStatic, firstDivider.Metrics.StateBits);
            Assert.Equal(CombinationalLoopStatus.Acyclic, firstDivider.Metrics.CombinationalLoopStatus);
            Assert.All(
                firstDivider.Netlist.Nodes.Where(node => node.Kind == NandNodeKind.Nand2),
                node => Assert.NotNull(node.LeftId));
        }
    }

    [Theory]
    [InlineData(8, 693, 504, 650, 40)]
    [InlineData(16, 1045, 1248, 1140, 73)]
    [InlineData(32, 1749, 2976, 2086, 138)]
    public void DeclaredComponentsExposeFrozenExactStaticMetrics(
        int width,
        int catalogueNands,
        int ctzNands,
        int dividerNands,
        int dividerDffs)
    {
        var family = RadixAwareValuationHardware.BuildFamily(width);
        Assert.Equal(catalogueNands, family.Catalogue.Metrics.Nand2Static);
        Assert.Equal(ctzNands, family.Ctz.Metrics.Nand2Static);
        Assert.Equal(dividerNands, family.OddDivmod.Metrics.Nand2Static);
        Assert.Equal(dividerDffs, family.OddDivmod.Metrics.DffStatic);
    }

    [Fact]
    public void OddRestoringControllerIsExhaustiveForW8CatalogueDivisors()
    {
        var machine = RadixAwareValuationHardware.BuildOddDivmodMachine(8);
        var oddPrimes = Build005HardwareDomain.PrimeCatalogue.Where(prime => prime != 2);
        for (uint dividend = 0; dividend < 256; dividend++)
        {
            foreach (var prime in oddPrimes)
            {
                var divisor = checked((uint)prime);
                var actual = machine.Run(dividend, divisor);
                Assert.False(actual.Rejected);
                Assert.Equal(dividend / divisor, actual.Quotient);
                Assert.Equal(dividend % divisor, actual.Remainder);
                Assert.Equal(dividend % divisor == 0, actual.Exact);
                Assert.Equal(9, actual.ClockCycles);
                Assert.Equal(10, actual.CombinationalEvaluations);
                Assert.Equal(
                    (long)machine.Metrics.Nand2Static * actual.CombinationalEvaluations,
                    actual.NandEvaluations);
            }
        }
    }

    [Fact]
    public void OddRestoringControllerRejectsZeroAndEvenDivisorsAtomically()
    {
        var machine = RadixAwareValuationHardware.BuildOddDivmodMachine(8);
        foreach (var divisor in new uint[] { 0, 2, 4, 254 })
        {
            var actual = machine.Run(255, divisor);
            Assert.True(actual.Rejected);
            Assert.False(actual.Exact);
            Assert.Equal(0U, actual.Quotient);
            Assert.Equal(0U, actual.Remainder);
            Assert.Equal(1, actual.ClockCycles);
            Assert.Equal(2, actual.CombinationalEvaluations);
        }
    }

    [Fact]
    public void OddRestoringControllerMatchesBoundarySamplesAtW16AndW32()
    {
        foreach (var width in new[] { 16, 32 })
        {
            var machine = RadixAwareValuationHardware.BuildOddDivmodMachine(width);
            foreach (var dividend in BoundaryValues(width))
            {
                foreach (var divisor in new uint[] { 3, 5, 31 })
                {
                    var actual = machine.Run(dividend, divisor);
                    Assert.False(actual.Rejected);
                    Assert.Equal(dividend / divisor, actual.Quotient);
                    Assert.Equal(dividend % divisor, actual.Remainder);
                    Assert.Equal(dividend % divisor == 0, actual.Exact);
                    Assert.Equal(width + 1, actual.ClockCycles);
                }
            }
        }
    }

    [Fact]
    public void FamilySharesTheExactSameRadixAndDividerObjectsAcrossCacheVariants()
    {
        foreach (var width in Widths)
        {
            var family = RadixAwareValuationHardware.BuildFamily(width);
            foreach (var capacity in Build005HardwareDomain.SupportedCacheCapacities)
            {
                var service = family.Services[capacity];
                Assert.Same(family.Catalogue, service.Catalogue);
                Assert.Same(family.Ctz, service.Ctz);
                Assert.Same(family.OddDivmod, service.OddDivmod);
                Assert.False(CompositionalServiceCost.IsIntegratedNetlist);
                Assert.Equal(
                    service.Cost.Components.Sum(component => component.Metrics.Nand2Static),
                    service.Cost.Nand2StaticAdditive);
                Assert.Equal(
                    capacity * Build005HardwareDomain.FrontierLineBits(width),
                    service.Cost.CacheLineBits);
                Assert.Equal(
                    service.Ctz.Metrics.DffStatic +
                    service.OddDivmod.Metrics.DffStatic +
                    service.Cache.Metrics.DffStatic,
                    service.Cost.DffStaticAdditive);
            }
        }
    }

    [Fact]
    public void CacheStateBitsAreExactAndReplacementPolicyRemainsAnExplicitBoundary()
    {
        foreach (var width in Widths)
        {
            foreach (var capacity in Build005HardwareDomain.SupportedCacheCapacities)
            {
                var cache = FrontierCacheHardware.Build(width, capacity);
                var expectedState = capacity * Build005HardwareDomain.FrontierLineBits(width);
                Assert.Equal(expectedState, cache.Metrics.DffStatic);
                Assert.Equal(expectedState, cache.Metrics.StateBits);
                Assert.Equal(CombinationalLoopStatus.Acyclic, cache.Metrics.CombinationalLoopStatus);
                Assert.Contains("NOT_INTEGRATED", DeclaredFrontierCacheCircuit.ReplacementBoundary);
                Assert.Equal(cache.Metrics.DffStatic, cache.Netlist.DffBoundaries.Count);
            }
        }
    }

    [Theory]
    [InlineData(8, 0, 0, 0)]
    [InlineData(8, 1, 438, 29)]
    [InlineData(8, 2, 881, 58)]
    [InlineData(8, 4, 1788, 116)]
    [InlineData(16, 0, 0, 0)]
    [InlineData(16, 1, 546, 38)]
    [InlineData(16, 2, 1097, 76)]
    [InlineData(16, 4, 2220, 152)]
    [InlineData(32, 0, 0, 0)]
    [InlineData(32, 1, 750, 55)]
    [InlineData(32, 2, 1505, 110)]
    [InlineData(32, 4, 3036, 220)]
    public void CacheFrontEndsExposeFrozenExactStaticMetrics(
        int width,
        int capacity,
        int expectedNands,
        int expectedDffs)
    {
        var cache = FrontierCacheHardware.Build(width, capacity);
        Assert.Equal(expectedNands, cache.Metrics.Nand2Static);
        Assert.Equal(expectedDffs, cache.Metrics.DffStatic);
    }

    [Fact]
    public void CacheLookupUpdateStaleTagInvalidationAndFlushAreExact()
    {
        var cache = FrontierCacheHardware.Build(8, 2);
        var line0 = new FrontierCacheLineInput(1, 9, 2, 3, 5, Terminal: true, Infinite: false);
        var first = cache.Evaluate(Cycle(querySlot: 1, generation: 9, prime: 2, update: line0, updateIndex: 0));
        Assert.False(first.Observation.Hit);
        Assert.True(first.Observation.UpdateAccepted);
        Assert.False(first.Observation.UpdateRejected);

        var hit0 = cache.Evaluate(
            Cycle(querySlot: 1, generation: 9, prime: 2),
            first.NextState,
            first.Evaluation);
        Assert.True(hit0.Observation.Hit);
        Assert.Equal(0U, hit0.Observation.HitIndex);
        Assert.Equal(3U, hit0.Observation.Exponent);
        Assert.Equal(5U, hit0.Observation.Residual);
        Assert.True(hit0.Observation.Terminal);

        var stale = cache.Evaluate(
            Cycle(querySlot: 1, generation: 10, prime: 2),
            hit0.NextState,
            hit0.Evaluation);
        Assert.False(stale.Observation.Hit);

        var duplicateWrite = cache.Evaluate(
            Cycle(querySlot: 1, generation: 9, prime: 2, update: line0, updateIndex: 1),
            stale.NextState,
            stale.Evaluation);
        var duplicate = cache.Evaluate(
            Cycle(querySlot: 1, generation: 9, prime: 2),
            duplicateWrite.NextState,
            duplicateWrite.Evaluation);
        Assert.True(duplicate.Observation.Hit);
        Assert.True(duplicate.Observation.DuplicateMatch);
        Assert.Equal(0U, duplicate.Observation.HitIndex);

        var invalidated = cache.Evaluate(
            Cycle(querySlot: 1, generation: 9, prime: 2, invalidateSlot: 1),
            duplicate.NextState,
            duplicate.Evaluation);
        var afterInvalidation = cache.Evaluate(
            Cycle(querySlot: 1, generation: 9, prime: 2),
            invalidated.NextState,
            invalidated.Evaluation);
        Assert.False(afterInvalidation.Observation.Hit);

        var rewrite = cache.Evaluate(
            Cycle(querySlot: 1, generation: 9, prime: 2, update: line0, updateIndex: 0),
            afterInvalidation.NextState,
            afterInvalidation.Evaluation);
        var flushed = cache.Evaluate(
            Cycle(querySlot: 1, generation: 9, prime: 2, flush: true),
            rewrite.NextState,
            rewrite.Evaluation);
        var afterFlush = cache.Evaluate(
            Cycle(querySlot: 1, generation: 9, prime: 2),
            flushed.NextState,
            flushed.Evaluation);
        Assert.False(afterFlush.Observation.Hit);
    }

    [Fact]
    public void CacheK0AndOutOfRangeK2WriteIndexRejectWithoutCreatingEvidence()
    {
        var line = new FrontierCacheLineInput(0, 0, 1, 0, 1, Terminal: true, Infinite: false);
        var cache0 = FrontierCacheHardware.Build(8, 0);
        var rejected0 = cache0.Evaluate(Cycle(0, 0, 1, line, updateIndex: 0));
        Assert.True(rejected0.Observation.UpdateRejected);
        Assert.False(rejected0.Observation.UpdateAccepted);
        Assert.Empty(rejected0.NextState);

        var cache2 = FrontierCacheHardware.Build(8, 2);
        var rejected2 = cache2.Evaluate(Cycle(0, 0, 1, line, updateIndex: 3));
        Assert.True(rejected2.Observation.UpdateRejected);
        Assert.False(rejected2.Observation.UpdateAccepted);
        var queried = cache2.Evaluate(Cycle(0, 0, 1), rejected2.NextState, rejected2.Evaluation);
        Assert.False(queried.Observation.Hit);
    }

    [Fact]
    public void CacheK1TagMatchIsExhaustiveAcrossW8FrozenKeyDomain()
    {
        var cache = FrontierCacheHardware.Build(8, 1);
        var stored = new FrontierCacheLineInput(2, 173, 10, 7, 29, Terminal: true, Infinite: false);
        var write = cache.Evaluate(Cycle(0, 0, 0, stored, updateIndex: 0));
        for (uint slot = 0; slot < 4; slot++)
        {
            for (uint generation = 0; generation < 256; generation++)
            {
                for (uint prime = 0; prime < Build005HardwareDomain.PrimeCatalogue.Count; prime++)
                {
                    var actual = cache.Evaluate(
                        Cycle(slot, generation, prime),
                        write.NextState);
                    var expected = slot == stored.Slot &&
                        generation == stored.Generation &&
                        prime == stored.PrimeIndex;
                    Assert.Equal(expected, actual.Observation.Hit);
                    Assert.False(actual.Observation.DuplicateMatch);
                    if (expected)
                    {
                        Assert.Equal(stored.Exponent, actual.Observation.Exponent);
                        Assert.Equal(stored.Residual, actual.Observation.Residual);
                    }
                }
            }
        }
    }

    [Fact]
    public void CacheAcceptsFullW32ResidualAndZeroInfiniteReceipt()
    {
        var cache = FrontierCacheHardware.Build(32, 1);
        var maximum = new FrontierCacheLineInput(
            3,
            255,
            10,
            32,
            uint.MaxValue,
            Terminal: true,
            Infinite: false);
        var written = cache.Evaluate(Cycle(3, 255, 10, maximum, updateIndex: 0));
        var hit = cache.Evaluate(Cycle(3, 255, 10), written.NextState, written.Evaluation);
        Assert.True(hit.Observation.Hit);
        Assert.Equal(32U, hit.Observation.Exponent);
        Assert.Equal(uint.MaxValue, hit.Observation.Residual);

        var zero = maximum with { Exponent = 0, Residual = 0, Terminal = false, Infinite = true };
        var zeroWritten = cache.Evaluate(
            Cycle(3, 255, 10, zero, updateIndex: 0),
            hit.NextState,
            hit.Evaluation);
        var zeroHit = cache.Evaluate(
            Cycle(3, 255, 10),
            zeroWritten.NextState,
            zeroWritten.Evaluation);
        Assert.True(zeroHit.Observation.Infinite);
        Assert.False(zeroHit.Observation.Terminal);
    }

    private static FrontierCacheCycleInput Cycle(
        uint querySlot,
        uint generation,
        uint prime,
        FrontierCacheLineInput? update = null,
        uint updateIndex = 0,
        uint? invalidateSlot = null,
        bool flush = false) =>
        new(
            querySlot,
            generation,
            prime,
            UpdateEnable: update is not null,
            updateIndex,
            update ?? new FrontierCacheLineInput(0, 0, 0, 0, 0, false, false),
            InvalidateEnable: invalidateSlot.HasValue,
            invalidateSlot ?? 0,
            flush);

    private static IEnumerable<uint> BoundaryValues(int width)
    {
        var maximum = width == 32 ? uint.MaxValue : (1U << width) - 1;
        return [
            0,
            1,
            2,
            3,
            4,
            8,
            16,
            31,
            32,
            1U << (width - 1),
            maximum - 1,
            maximum,
        ];
    }

    private static int OracleCtz(uint value)
    {
        var count = 0;
        while ((value & 1U) == 0)
        {
            count++;
            value >>= 1;
        }

        return count;
    }
}
