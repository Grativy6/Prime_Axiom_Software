using PrimeAxiom.Cli;

namespace PrimeAxiom.Tests;

public sealed class Build005ProtocolTests
{
    private static readonly int[] ExpectedPrimeCatalogue = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31];
    private static readonly int[] ExpectedCompositeControls = [4, 6, 9, 10, 15, 21, 25, 27, 33, 35];
    private static readonly string[] ExpectedSourceRegimes = ["COLD_MAG", "PRODUCER_GENERATED"];

    private static readonly string[] ExpectedFamilies =
    [
        "STATIC_REUSE",
        "THRESHOLD_STAIRCASE",
        "RATIONAL_CANCEL",
        "DIVISIBILITY_FILTER_PERSISTENT",
        "DIVISIBILITY_FILTER_STREAM",
        "SMOOTH_STRIP",
        "MULTIPLICATIVE_DAG",
        "PRODUCER_FACTORED",
        "ADDITION_MUTATION",
        "PHASE_SHIFT",
        "COMPOSITE_CONTROL",
        "RADIX_V2",
        "HOSTILE_SLOT_THRASH",
        "HOSTILE_PRIME_THRASH",
        "HOSTILE_MUTATE_AFTER_FILL",
        "HOSTILE_SPECULATION_POISON",
        "HOSTILE_BOUNDARY_FAILURE",
        "HOSTILE_GENERATION_WRAP",
    ];

    [Fact]
    public void FrozenPlanHashMatchesProtocolConstant()
    {
        var root = FindRepositoryRoot();
        Assert.Equal(
            Build005Protocol.FrozenPlanSha256,
            Build005Protocol.FileSha256(Path.Combine(root, "research", "build005_experiment_plan.md")));
    }

    [Fact]
    public void FrozenCataloguesAndSeedsAreStable()
    {
        Assert.Equal(ExpectedPrimeCatalogue, Build005Protocol.PrimeCatalog);
        Assert.Equal(ExpectedCompositeControls, Build005Protocol.CompositeControls);
        Assert.Equal(Build005Protocol.DeriveSeed(16, "STATIC_REUSE"), Build005Protocol.DeriveSeed(16, "STATIC_REUSE"));
        Assert.NotEqual(Build005Protocol.DeriveSeed(16, "STATIC_REUSE"), Build005Protocol.DeriveSeed(32, "STATIC_REUSE"));
        Assert.NotEqual(Build005Protocol.DeriveSeed(16, "STATIC_REUSE"), Build005Protocol.DeriveSeed(16, "PHASE_SHIFT"));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void FrozenWorkloadsAreDeterministicCompleteAndInDomain(int width)
    {
        var first = Build005Workloads.Create(width);
        var second = Build005Workloads.Create(width);
        Assert.Equal(first.Count, second.Count);
        for (var index = 0; index < first.Count; index++)
        {
            Assert.Equal(first[index] with { Events = Array.Empty<Build005TraceEvent>() }, second[index] with { Events = Array.Empty<Build005TraceEvent>() });
            Assert.Equal(first[index].Events, second[index].Events);
        }
        Assert.Equal(ExpectedFamilies, first.Select(trace => trace.Family));
        Assert.Equal(first.Count, first.Select(trace => trace.TraceId).Distinct(StringComparer.Ordinal).Count());

        var maximum = width == 32 ? uint.MaxValue : (1UL << width) - 1;
        foreach (var trace in first)
        {
            Assert.Equal(width, trace.Width);
            Assert.NotEmpty(trace.Events);
            Assert.Contains(trace.SourceRegime, ExpectedSourceRegimes);
            Assert.Equal("MAGNITUDE_FINAL", trace.OutputObligation);
            foreach (var item in trace.Events)
            {
                Assert.InRange(item.Destination, 0, 3);
                Assert.InRange(item.Left, 0, 3);
                Assert.InRange(item.Right, 0, 3);
                Assert.InRange(item.Magnitude, 0UL, maximum);
                if (item.Kind is Build005EventKind.TestPower or
                    Build005EventKind.Valuation or
                    Build005EventKind.StripAll or
                    Build005EventKind.ProducerPrimeFact)
                {
                    Assert.Contains(item.Divisor, Build005Protocol.PrimeCatalog);
                }

                if (item.Kind == Build005EventKind.CompositeValuationControl)
                {
                    Assert.Contains(item.Divisor, Build005Protocol.CompositeControls);
                }

                if (item.Kind == Build005EventKind.TestPower)
                {
                    Assert.True(item.Threshold > 0);
                }
            }
        }

        Assert.True(first.Single(trace => trace.Family == "MULTIPLICATIVE_DAG").PrimeAttributionEligible);
        Assert.False(first.Single(trace => trace.Family == "PRODUCER_FACTORED").SearchRepaymentEligible);
        Assert.False(first.Single(trace => trace.Family == "RADIX_V2").SearchRepaymentEligible);
        Assert.All(first.Where(trace => trace.Family.StartsWith("HOSTILE_", StringComparison.Ordinal)), trace => Assert.True(trace.Hostile));
    }

    [Fact]
    public void UnsupportedWidthIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Build005Workloads.Create(64));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PrimeAxiom.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
