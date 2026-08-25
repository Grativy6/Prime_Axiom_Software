using PrimeAxiom.Cli;

namespace PrimeAxiom.Tests;

public sealed class Build002GeneratorProvenanceTests
{
    [Fact]
    public void GeneratorCommandRecordsActualRepositoryRelativeInputs()
    {
        var root = Path.Combine(Path.GetTempPath(), "prime-axiom-provenance-root");
        var verification = Path.Combine(root, ".artifacts", "linux receipts", "verification-summary.json");

        var command = Build002ExperimentRunner.BuildGeneratorCommand(
            root,
            Path.Combine(root, "results", "build002"),
            verification,
            ".artifacts/linux receipts/synthesis-metrics.csv",
            ".artifacts/linux receipts/toolchain-bootstrap.json");

        Assert.Equal(
            "dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build002 " +
            "--output 'results/build002' " +
            "--hdl-verification-summary '.artifacts/linux receipts/verification-summary.json' " +
            "--hdl-synthesis-metrics '.artifacts/linux receipts/synthesis-metrics.csv' " +
            "--hdl-toolchain '.artifacts/linux receipts/toolchain-bootstrap.json'",
            command);
    }

    [Fact]
    public void GeneratorCommandPreservesPartialInputsAndRedactsExternalRoots()
    {
        var root = Path.Combine(Path.GetTempPath(), "prime-axiom-provenance-root");
        var externalRoot = Path.Combine(Path.GetTempPath(), "private-user-directory");
        var externalVerification = Path.Combine(externalRoot, "verification-summary.json");

        var command = Build002ExperimentRunner.BuildGeneratorCommand(
            root,
            "results/build002",
            externalVerification,
            null,
            null);

        Assert.Contains("--hdl-verification-summary '<external>/verification-summary.json'", command);
        Assert.DoesNotContain(externalRoot, command, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--hdl-synthesis-metrics", command, StringComparison.Ordinal);
        Assert.DoesNotContain("--hdl-toolchain", command, StringComparison.Ordinal);
    }
}
