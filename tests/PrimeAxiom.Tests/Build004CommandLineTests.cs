using System.Text;
using System.Text.Json;
using PrimeAxiom.Cli;

namespace PrimeAxiom.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Build004CommandLineTestGroup
{
    public const string Name = "Build 004 command-line console";
}

[Collection(Build004CommandLineTestGroup.Name)]
public sealed class Build004CommandLineTests
{
    [Fact]
    public void ExactCombinatorialCommandsExposeJsonAndRejectInvalidGrammar()
    {
        var binomial = Invoke("binomial-receipt", "5", "2");
        Assert.Equal(0, binomial.ExitCode);
        using (var document = JsonDocument.Parse(binomial.StandardOutput))
        {
            Assert.Equal("10", document.RootElement.GetProperty("value").GetString());
            Assert.Equal("PrimeCoordinateLocal", document.RootElement.GetProperty("boundary").GetString());
        }

        var hypergeometric = Invoke("hypergeometric-receipt", "10", "4", "3", "2");
        Assert.Equal(0, hypergeometric.ExitCode);
        using (var document = JsonDocument.Parse(hypergeometric.StandardOutput))
        {
            var probability = document.RootElement.GetProperty("probability");
            Assert.Equal("3", probability.GetProperty("numerator").GetString());
            Assert.Equal("10", probability.GetProperty("denominator").GetString());
        }

        Assert.Equal(2, Invoke("binomial-receipt", "5", "2", "unexpected").ExitCode);
        Assert.Equal(2, Invoke("hypergeometric-receipt", "10", "11", "3", "2").ExitCode);
    }

    [Fact]
    public void LineageDemoExposesProjectionCollisionAndRejectsExtraArguments()
    {
        var demo = Invoke("lineage-demo");

        Assert.Equal(0, demo.ExitCode);
        Assert.Contains("same source support: True", demo.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("same source multiplicity: True", demo.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("same derivation root: False", demo.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(2, Invoke("lineage-demo", "unexpected").ExitCode);
    }

    [Fact]
    public void AudioRendererWritesDeclaredWavAndRefusesOverwrite()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"prime-axiom-build004-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "fifth.wav");
        var invalidOutput = Path.Combine(directory, "invalid.wav");
        try
        {
            var first = Invoke("render-just-interval", "3", "2", "220", output);
            Assert.Equal(0, first.ExitCode);
            var bytes = File.ReadAllBytes(output);
            Assert.Equal(96_044, bytes.Length);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));

            var second = Invoke("render-just-interval", "3", "2", "220", output);
            Assert.Equal(2, second.ExitCode);
            Assert.Equal(bytes, File.ReadAllBytes(output));

            Assert.Equal(2, Invoke("render-just-interval", "3", "0", "220", invalidOutput).ExitCode);
            Assert.False(File.Exists(invalidOutput));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CommandResult Invoke(params string[] args)
    {
        var originalOutput = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var exitCode = CommandLine.Run(args);
            return new CommandResult(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
}
