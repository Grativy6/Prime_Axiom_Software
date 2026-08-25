using System.Security.Cryptography;
using System.Numerics;
using System.Text.Json;
using System.Globalization;
using PrimeAxiom.Cli;
using PrimeAxiom.Core.Calculator;

namespace PrimeAxiom.Tests;

public sealed class Build003ExperimentTests
{
    [Fact]
    public void FrozenPlanHashMatchesProtocolConstant()
    {
        var root = FindRepositoryRoot();
        var plan = Path.Combine(root, "research", "build003_experiment_plan.md");

        Assert.Equal(Build003Protocol.FrozenPlanSha256, Build003Protocol.FileSha256(plan));
    }

    [Fact]
    public void GeneratorReplaysByteIdenticallyAndManifestVerifies()
    {
        var root = FindRepositoryRoot();
        var first = Path.Combine(Path.GetTempPath(), $"prime-axiom-build003-first-{Guid.NewGuid():N}");
        var second = Path.Combine(Path.GetTempPath(), $"prime-axiom-build003-second-{Guid.NewGuid():N}");
        try
        {
            var firstReceipt = Build003ExperimentRunner.Run(root, first);
            var firstRerunReceipt = Build003ExperimentRunner.Run(root, first);
            var secondReceipt = Build003ExperimentRunner.Run(root, second);

            Assert.Equal(Build003Protocol.FrameworkStatus, firstReceipt.Status);
            Assert.Equal(Build003Protocol.FrameworkStatus, firstRerunReceipt.Status);
            Assert.Equal(0, firstReceipt.FailureCount);
            Assert.Equal(Build003ExperimentRunner.ExpectedCorrectnessChecks, firstReceipt.CheckCount);
            Assert.Equal(firstReceipt.CheckCount, secondReceipt.CheckCount);
            var firstFiles = Directory.EnumerateFiles(first)
                .Select(path => Path.GetFileName(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var secondFiles = Directory.EnumerateFiles(second)
                .Select(path => Path.GetFileName(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(firstFiles, secondFiles);
            foreach (var file in firstFiles)
            {
                Assert.Equal(
                    SHA256.HashData(File.ReadAllBytes(Path.Combine(first, file))),
                    SHA256.HashData(File.ReadAllBytes(Path.Combine(second, file))));
            }

            VerifyManifest(first);
            VerifyManifest(second);
        }
        finally
        {
            if (Directory.Exists(first))
            {
                Directory.Delete(first, recursive: true);
            }

            if (Directory.Exists(second))
            {
                Directory.Delete(second, recursive: true);
            }
        }
    }

    [Fact]
    public void Build003JsonWritesPotentiallyLargeIntegersAsDecimalStrings()
    {
        var receipt = PrimeReceiptCalculator.Analyze(
            new BigInteger(15),
            new PrimeReceiptPolicy(9_007_199_254_740_993));
        var json = JsonSerializer.Serialize(receipt, Build003Protocol.JsonOptions);
        using var document = JsonDocument.Parse(json);

        var maximum = document.RootElement.GetProperty("policy").GetProperty("maxOddCandidates");
        Assert.Equal(JsonValueKind.String, maximum.ValueKind);
        Assert.Equal("9007199254740993", maximum.GetString());
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("absoluteBitLength").ValueKind);
    }

    [Fact]
    public void GeneratorRefusesUnownedOrRepositoryRootOutputWithoutClobbering()
    {
        var root = FindRepositoryRoot();
        var unowned = Path.Combine(Path.GetTempPath(), $"prime-axiom-build003-unowned-{Guid.NewGuid():N}");
        Directory.CreateDirectory(unowned);
        var readme = Path.Combine(unowned, "README.md");
        File.WriteAllText(readme, "keep me");
        try
        {
            Assert.Throws<InvalidOperationException>(() => Build003ExperimentRunner.Run(root, unowned));
            Assert.Equal("keep me", File.ReadAllText(readme));
            File.WriteAllText(
                Path.Combine(unowned, "manifest.json"),
                "{\"protocolId\":\"PAS-BUILD003-PRIME-RECEIPT-0001\"}");
            Assert.Throws<InvalidOperationException>(() => Build003ExperimentRunner.Run(root, unowned));
            Assert.Equal("keep me", File.ReadAllText(readme));
            Assert.Throws<InvalidOperationException>(() => Build003ExperimentRunner.Run(root, root));
        }
        finally
        {
            Directory.Delete(unowned, recursive: true);
        }
    }

    [Fact]
    public void GeneratedTextIsLfAndBomFree()
    {
        var root = FindRepositoryRoot();
        var output = Path.Combine(Path.GetTempPath(), $"prime-axiom-build003-encoding-{Guid.NewGuid():N}");
        try
        {
            Build003ExperimentRunner.Run(root, output);
            foreach (var file in Directory.EnumerateFiles(output))
            {
                var bytes = File.ReadAllBytes(file);
                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
                Assert.DoesNotContain("\r\n", File.ReadAllText(file), StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static void VerifyManifest(string output)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "manifest.json")));
        var root = document.RootElement;
        Assert.Equal(Build003Protocol.FrameworkStatus, root.GetProperty("status").GetString());
        Assert.Equal(Build003Protocol.FrozenPlanSha256, root.GetProperty("frozenPlanSha256").GetString());
        Assert.Equal(
            Build003ExperimentRunner.ExpectedCorrectnessChecks.ToString(CultureInfo.InvariantCulture),
            root.GetProperty("correctnessChecks").GetString());
        Assert.Equal(
            Build003ExperimentRunner.FrozenComparisonIds,
            root.GetProperty("frozenComparisonIds").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(
            Build003ExperimentRunner.ExpectedFiles.OrderBy(path => path, StringComparer.Ordinal),
            Directory.EnumerateFiles(output)
                .Select(Path.GetFileName)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(Build003ExperimentRunner.ExpectedFiles.Count - 1, root.GetProperty("files").GetArrayLength());
        foreach (var entry in root.GetProperty("files").EnumerateArray())
        {
            var relative = entry.GetProperty("path").GetString();
            Assert.NotNull(relative);
            var actual = Build003Protocol.FileSha256(Path.Combine(output, relative));
            Assert.Equal(entry.GetProperty("sha256").GetString(), actual);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PrimeAxiom.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the Prime Axiom repository root.");
    }
}
