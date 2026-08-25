using System.Globalization;
using System.Numerics;
using System.Text.Json;
using PrimeAxiom.Cli;

namespace PrimeAxiom.Tests;

public sealed class Build004ExperimentTests
{
    [Fact]
    public void FrozenPlanHashMatchesProtocolConstant()
    {
        var root = FindRepositoryRoot();
        var plan = Path.Combine(root, "research", "build004_experiment_plan.md");

        Assert.Equal(Build004Protocol.FrozenPlanSha256, Build004Protocol.FileSha256(plan));
        Assert.Equal("31dd150540bac79de3ee5925b44afdb7abaf327a", Build004Protocol.BaselineCommit);
    }

    [Fact]
    public void FrozenFamilyCaseAndCheckCountsAreExactAndDistinct()
    {
        Assert.Equal(14, Build004Campaign.ExpectedCases.Count);
        Assert.Equal(14, Build004Campaign.ExpectedChecks.Count);
        Assert.Equal(33_153, Build004Campaign.ExpectedCases["BINOMIAL_ALL_0_256"]);
        Assert.Equal(20_475, Build004Campaign.ExpectedCases["HYPERGEOMETRIC_POINTS_0_24"]);
        Assert.Equal(12_529, Build004Campaign.ExpectedCases["HYPERGEOMETRIC_NORMALIZATION_0_32"]);
        Assert.Equal(65_536, Build004Campaign.ExpectedCases["SUPPORT_PROJECTION_U8"]);
        Assert.Equal(6_561, Build004Campaign.ExpectedCases["MULTIPLICITY_PROJECTION_U4_E0_2"]);
        Assert.Equal(66_306, Build004Campaign.ExpectedChecks["BINOMIAL_ALL_0_256"]);
        Assert.Equal(393_216, Build004Campaign.ExpectedChecks["SUPPORT_PROJECTION_U8"]);
        Assert.Equal(78_732, Build004Campaign.ExpectedChecks["MULTIPLICITY_PROJECTION_U4_E0_2"]);
        Assert.Equal(
            Build004Campaign.ExpectedCases.Count,
            Build004Campaign.ExpectedCases.Keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Build004Campaign.ExpectedCases.Keys.Order(StringComparer.Ordinal),
            Build004Campaign.ExpectedChecks.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(887_072, Build004Campaign.ExpectedChecks.Values.Sum());
    }

    [Fact]
    public void JsonUsesCanonicalDecimalStringsForBigIntegersAndKeys()
    {
        var value = BigInteger.Pow(10, 100) + 17;
        var json = JsonSerializer.Serialize(
            new
            {
                Value = value,
                Map = new Dictionary<BigInteger, int> { [97] = -3 },
            },
            Build004Protocol.JsonOptions);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            value.ToString(CultureInfo.InvariantCulture),
            document.RootElement.GetProperty("value").GetString());
        Assert.Equal("-3", document.RootElement.GetProperty("map").GetProperty("97").GetString());
    }

    [Fact]
    public void GeneratorRefusesUnownedAndRepositoryRootOutputsWithoutClobbering()
    {
        var root = FindRepositoryRoot();
        var output = Path.Combine(Path.GetTempPath(), $"prime-axiom-build004-unowned-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);
        var readme = Path.Combine(output, "README.md");
        File.WriteAllText(readme, "keep me");
        try
        {
            Assert.Throws<InvalidOperationException>(() => Build004ExperimentRunner.Run(root, output));
            Assert.Equal("keep me", File.ReadAllText(readme));
            File.WriteAllText(
                Path.Combine(output, "manifest.json"),
                "{\"protocolId\":\"PAS-BUILD004-EXACT-LINEAGE-0001\"}");
            Assert.Throws<InvalidOperationException>(() => Build004ExperimentRunner.Run(root, output));
            Assert.Equal("keep me", File.ReadAllText(readme));
            Assert.Throws<InvalidOperationException>(() => Build004ExperimentRunner.Run(root, root));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
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
