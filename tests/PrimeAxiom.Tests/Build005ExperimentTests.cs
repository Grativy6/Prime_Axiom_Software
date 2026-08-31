using System.Text.Json;
using PrimeAxiom.Cli;

namespace PrimeAxiom.Tests;

public sealed class Build005ExperimentTests
{
    private const int ExpectedFamilyCount = 18;
    private const int ExpectedPolicyCount = 16;
    private const int ExpectedWidthCount = 3;
    private const int ExpectedRowsPerFamily = ExpectedWidthCount * ExpectedPolicyCount;
    private const int ExpectedWorkloadRows = ExpectedFamilyCount * ExpectedRowsPerFamily;
    private const int ExpectedBreakEvenRows = ExpectedFamilyCount * ExpectedWidthCount * 21;
    private const int ExpectedStaticRows = ExpectedWidthCount * 4 * 4;

    private static readonly Lazy<Build005CampaignResult> Campaign =
        new(Build005Campaign.Run, LazyThreadSafetyMode.ExecutionAndPublication);

    [Fact]
    public void ImplementedMatrixPassesButFrozenCoverageRemainsIncomplete()
    {
        var campaign = Campaign.Value;

        Assert.Equal(ExpectedPolicyCount, Build005Campaign.Policies.Count);
        Assert.Equal(ExpectedWorkloadRows, campaign.WorkloadRows.Count);
        Assert.Equal(ExpectedBreakEvenRows, campaign.BreakEvenRows.Count);
        Assert.Equal(ExpectedStaticRows, campaign.StaticCosts.Count);
        Assert.Equal(ExpectedFamilyCount, campaign.Families.Count);
        Assert.True(campaign.ImplementedTraceCoverageComplete);
        Assert.False(campaign.CompleteFrozenCoverage);
        Assert.Equal(10, campaign.EvidenceGates.Count);
        Assert.Single(campaign.EvidenceGates, gate => gate.Satisfied);
        Assert.Equal(9, campaign.EvidenceGates.Count(gate => !gate.Satisfied));
        Assert.True(campaign.Checks > 0);
        Assert.Equal(0, campaign.Failures);
        Assert.True(campaign.Correctness.Checks > 0);
        Assert.Equal(0, campaign.Correctness.Failures);
        Assert.Empty(campaign.Correctness.FailureDetails);
        Assert.All(campaign.WorkloadRows, row =>
        {
            Assert.Equal(Build005Campaign.CompleteStatus, row.Status);
            Assert.True(row.ControlMetrics.CorrectnessChecks > 0);
            Assert.Equal(0, row.ControlMetrics.CorrectnessFailures);
        });
        Assert.All(campaign.Families, family =>
        {
            Assert.Equal(ExpectedRowsPerFamily, family.ExpectedRows);
            Assert.Equal(ExpectedRowsPerFamily, family.Rows);
            Assert.True(family.Checks > 0);
            Assert.Equal(0, family.Failures);
            Assert.Equal(Build005Campaign.CompleteStatus, family.Status);
        });
        Assert.All(campaign.BreakEvenRows, row => Assert.False(row.EligibleForFrozenDecision));
    }

    [Fact]
    public void GeneratedStatusRemainsPartialUntilExternalVerification()
    {
        var decision = Campaign.Value.Decision;

        Assert.Equal(Build005Protocol.PartialStatus, decision.GeneratedStatus);
        Assert.Equal(Build005Protocol.PartialStatus, decision.CandidateTerminalLabel);
        Assert.False(decision.DecisionAxesEarned);
        Assert.Equal("NOT_EARNED", decision.SearchPolicy);
        Assert.Equal("NOT_ESTABLISHED", decision.Attribution);
        Assert.Equal("SEMANTIC", decision.EvidenceBoundary);
        Assert.Equal("EXPLORATORY_GENERIC_REUSE_PATTERN", decision.ExploratoryObservedPattern);
        Assert.Equal("BLIND_SPECULATION_INCURRED_WASTED_WORK", decision.ExploratorySearchObservation);
        Assert.False(decision.DemandPrimeCandidate);
        Assert.False(decision.SpeculativePrimeCandidate);
        Assert.False(decision.GenericCacheAdvantage);
        Assert.False(decision.ProducerOnlyAdvantage);
        Assert.False(decision.RadixTwoOnly);
        Assert.Empty(decision.QualifyingDemandFamilies);
        Assert.Empty(decision.QualifyingSpeculativeFamilies);
        Assert.Equal(9, decision.UnmetGates.Count);
        Assert.Contains("no universal arithmetic", decision.ClaimCeiling, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RunnerProducesByteIdenticalManifestAddressedReceipts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var firstDirectory = CreateTemporaryDirectory();
        var secondDirectory = CreateTemporaryDirectory();
        try
        {
            var first = Build005ExperimentRunner.Run(repositoryRoot, firstDirectory);
            var second = Build005ExperimentRunner.Run(repositoryRoot, secondDirectory);

            Assert.Equal(Build005Protocol.PartialStatus, first.GeneratedStatus);
            Assert.Equal(first with { OutputDirectory = second.OutputDirectory }, second);
            Assert.True(first.CheckCount > 0);
            Assert.Equal(0, first.FailureCount);
            AssertExactInventory(firstDirectory);
            AssertExactInventory(secondDirectory);

            foreach (var file in Build005ExperimentRunner.ExpectedFiles)
            {
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(firstDirectory, file)),
                    File.ReadAllBytes(Path.Combine(secondDirectory, file)));
            }

            AssertManifest(firstDirectory, first);
            AssertManifest(secondDirectory, second);
            AssertCoverageReceipt(firstDirectory);
        }
        finally
        {
            DeleteTemporaryDirectory(firstDirectory);
            DeleteTemporaryDirectory(secondDirectory);
        }
    }

    [Fact]
    public void RunnerRejectsRepositoryRootAndUnownedInRepositoryPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        var unownedPath = Path.Combine(repositoryRoot, "results", "build005-test-unowned");

        Assert.Throws<InvalidOperationException>(() =>
            Build005ExperimentRunner.Run(repositoryRoot, repositoryRoot));
        Assert.Throws<InvalidOperationException>(() =>
            Build005ExperimentRunner.Run(repositoryRoot, unownedPath));
        Assert.False(Directory.Exists(unownedPath));
    }

    [Fact]
    public void RunnerPreservesAndRejectsUnownedExternalFile()
    {
        var repositoryRoot = FindRepositoryRoot();
        var outputDirectory = CreateTemporaryDirectory();
        var sentinel = Path.Combine(outputDirectory, "do-not-delete.txt");
        File.WriteAllText(sentinel, "user-owned");
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                Build005ExperimentRunner.Run(repositoryRoot, outputDirectory));

            Assert.Contains("unowned file", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("user-owned", File.ReadAllText(sentinel));
            Assert.Single(Directory.EnumerateFiles(outputDirectory));
        }
        finally
        {
            DeleteTemporaryDirectory(outputDirectory);
        }
    }

    [Fact]
    public void RunnerRejectsUnexpectedExternalSubdirectory()
    {
        var repositoryRoot = FindRepositoryRoot();
        var outputDirectory = CreateTemporaryDirectory();
        var child = Directory.CreateDirectory(Path.Combine(outputDirectory, "user-owned"));
        var sentinel = Path.Combine(child.FullName, "keep.txt");
        File.WriteAllText(sentinel, "preserved");
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                Build005ExperimentRunner.Run(repositoryRoot, outputDirectory));

            Assert.Contains("unexpected subdirectory", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("preserved", File.ReadAllText(sentinel));
        }
        finally
        {
            DeleteTemporaryDirectory(outputDirectory);
        }
    }

    private static void AssertExactInventory(string outputDirectory)
    {
        var expected = Build005ExperimentRunner.ExpectedFiles
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
        var actual = Directory.EnumerateFiles(outputDirectory)
            .Select(Path.GetFileName)
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
        Assert.Empty(Directory.EnumerateDirectories(outputDirectory));
    }

    private static void AssertManifest(string outputDirectory, Build005RunReceipt receipt)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(outputDirectory, "manifest.json")));
        var root = document.RootElement;

        Assert.Equal("prime-axiom-build005-manifest-v1", root.GetProperty("schema").GetString());
        Assert.Equal(Build005Protocol.ProtocolId, root.GetProperty("protocolId").GetString());
        Assert.Equal(Build005Protocol.FrozenPlanSha256, root.GetProperty("frozenPlanSha256").GetString());
        Assert.Equal(Build005Protocol.PartialStatus, root.GetProperty("generatedStatus").GetString());
        Assert.Equal(receipt.CandidateTerminalLabel, root.GetProperty("candidateTerminalLabel").GetString());
        Assert.False(root.GetProperty("decisionAxesEarned").GetBoolean());
        Assert.Equal(receipt.CheckCount, root.GetProperty("checks").GetInt64());
        Assert.Equal(0, root.GetProperty("failures").GetInt64());
        Assert.True(root.GetProperty("selfExcluding").GetBoolean());

        var entries = root.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(Build005ExperimentRunner.ExpectedFiles.Count - 1, entries.Length);
        Assert.DoesNotContain(entries, entry =>
            string.Equals(entry.GetProperty("path").GetString(), "manifest.json", StringComparison.Ordinal));
        foreach (var entry in entries)
        {
            var relativePath = Assert.IsType<string>(entry.GetProperty("path").GetString());
            var path = Path.Combine(outputDirectory, relativePath);
            Assert.Contains(relativePath, Build005ExperimentRunner.ExpectedFiles);
            Assert.True(File.Exists(path));
            Assert.Equal(new FileInfo(path).Length, entry.GetProperty("bytes").GetInt64());
            Assert.Equal(Build005Protocol.FileSha256(path), entry.GetProperty("sha256").GetString());
        }
    }

    private static void AssertCoverageReceipt(string outputDirectory)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(outputDirectory, "protocol_coverage.json")));
        var root = document.RootElement;

        Assert.Equal(ExpectedWorkloadRows, root.GetProperty("workloadRows").GetInt32());
        Assert.Equal(ExpectedBreakEvenRows, root.GetProperty("breakEvenRows").GetInt32());
        Assert.Equal(ExpectedStaticRows, root.GetProperty("staticRows").GetInt32());
        Assert.True(root.GetProperty("implementedTraceCoverageComplete").GetBoolean());
        Assert.False(root.GetProperty("completeFrozenCoverage").GetBoolean());
        Assert.Equal(9, root.GetProperty("unmetGates").GetArrayLength());
        Assert.True(root.GetProperty("externalVerificationRequired").GetBoolean());
        Assert.False(root.GetProperty("evidence").GetProperty("integratedNetlist").GetBoolean());
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

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"PrimeAxiom-Build005Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
