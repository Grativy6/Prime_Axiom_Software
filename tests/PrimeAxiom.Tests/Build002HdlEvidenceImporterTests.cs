using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PrimeAxiom.Cli;

namespace PrimeAxiom.Tests;

public sealed class Build002HdlEvidenceImporterTests
{
    [Fact]
    public void AbsentEvidenceWritesDeterministicNotSuppliedPlaceholders()
    {
        var root = NewTemporaryDirectory();
        try
        {
            var firstOutput = Path.Combine(root, "first");
            var secondOutput = Path.Combine(root, "second");
            var first = Build002HdlEvidenceImporter.Import(firstOutput);
            var second = Build002HdlEvidenceImporter.Import(secondOutput, "", null, "   ");

            Assert.False(first.Complete);
            Assert.Equal("NOT_SUPPLIED", first.Status);
            Assert.Equal("NOT_SUPPLIED", first.Platform);
            Assert.Equal(0, first.VerificationCaseCount);
            Assert.Equal(0, first.FormalCaseCount);
            Assert.Equal(0, first.SynthesisRowCount);
            Assert.Equal(0, first.WarningCountsMeasured);
            Assert.Equal(0, first.WarningCountsNotMeasured);
            Assert.Equal("NOT_SUPPLIED", first.VerificationSummarySourceSha256);
            Assert.Equal("NOT_SUPPLIED", first.SynthesisMetricsSourceSha256);
            Assert.Equal("NOT_SUPPLIED", first.ToolchainBootstrapSourceSha256);
            Assert.Equal(RequiredOutputs, Directory.GetFiles(firstOutput).Select(Path.GetFileName).Order());
            Assert.Equal(first.SynthesisMetricsOutputSha256, second.SynthesisMetricsOutputSha256);
            Assert.Equal(first.FormalReceiptsOutputSha256, second.FormalReceiptsOutputSha256);
            Assert.Equal(first.ToolchainOutputSha256, second.ToolchainOutputSha256);

            foreach (var name in RequiredOutputs)
            {
                var firstBytes = File.ReadAllBytes(Path.Combine(firstOutput, name));
                var secondBytes = File.ReadAllBytes(Path.Combine(secondOutput, name));
                Assert.Equal(firstBytes, secondBytes);
                Assert.DoesNotContain((byte)'\r', firstBytes);
            }

            Assert.Contains("NOT_SUPPLIED", File.ReadAllText(Path.Combine(firstOutput, "synthesis_metrics.csv")));
            Assert.Contains("NOT_MEASURED", File.ReadAllText(Path.Combine(firstOutput, "synthesis_metrics.csv")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CompleteFixtureIsValidatedEnrichedSanitizedAndDeterministic()
    {
        var root = NewTemporaryDirectory();
        try
        {
            var fixture = WriteCompleteFixture(Path.Combine(root, "source"));
            var firstOutput = Path.Combine(root, "first");
            var secondOutput = Path.Combine(root, "second");
            var first = Build002HdlEvidenceImporter.Import(
                firstOutput,
                fixture.Summary,
                fixture.Synthesis,
                fixture.Toolchain);
            var second = Build002HdlEvidenceImporter.Import(
                secondOutput,
                fixture.Summary,
                fixture.Synthesis,
                fixture.Toolchain);

            Assert.True(first.Complete);
            Assert.Equal("COMPLETE_VERIFIED", first.Status);
            Assert.Equal("windows-x64", first.Platform);
            Assert.Equal(260, first.VerificationCaseCount);
            Assert.Equal(15, first.FormalCaseCount);
            Assert.Equal(150, first.SynthesisRowCount);
            Assert.Equal(149, first.WarningCountsMeasured);
            Assert.Equal(1, first.WarningCountsNotMeasured);
            Assert.Equal(Hash(fixture.Summary), first.VerificationSummarySourceSha256);
            Assert.Equal(Hash(fixture.Synthesis), first.SynthesisMetricsSourceSha256);
            Assert.Equal(Hash(fixture.Toolchain), first.ToolchainBootstrapSourceSha256);
            Assert.Equal(RequiredOutputs, Directory.GetFiles(firstOutput).Select(Path.GetFileName).Order());
            Assert.Equal(first.SynthesisMetricsOutputSha256, second.SynthesisMetricsOutputSha256);
            Assert.Equal(first.FormalReceiptsOutputSha256, second.FormalReceiptsOutputSha256);
            Assert.Equal(first.ToolchainOutputSha256, second.ToolchainOutputSha256);

            var synthesis = File.ReadAllText(Path.Combine(firstOutput, "synthesis_metrics.csv"));
            Assert.Contains("implementation", synthesis);
            Assert.Contains("architecture", synthesis);
            Assert.Contains("evidence_class", synthesis);
            Assert.Contains("support_status", synthesis);
            Assert.Contains("SYNTHESIZED_AND_VALIDATED", synthesis);
            Assert.Contains("BINARY_POSITIONAL", synthesis);
            Assert.Contains(",0,", synthesis);
            Assert.Contains(",2,Warning count", synthesis);
            Assert.Contains(",NOT_MEASURED,Warning log was unavailable", synthesis);

            var formal = File.ReadAllText(Path.Combine(firstOutput, "formal_receipts.json"));
            Assert.Contains("formal_binary_w4", formal);
            Assert.Contains("[REDACTED_PATH]", formal);
            Assert.DoesNotContain("\"log\"", formal);
            Assert.Contains(first.VerificationSummarySourceSha256, formal);

            var toolchain = File.ReadAllText(Path.Combine(firstOutput, "toolchain.json"));
            Assert.Contains("TOOLCHAIN_VERIFIED", toolchain);
            Assert.Contains("2026-08-24", toolchain);
            Assert.Contains("95d3cf2a59d1617f2363ee9370bb3577799f33a07e9c66e126ddeb68e8e5814c", toolchain);
            Assert.DoesNotContain("sourceUrl", toolchain);
            Assert.DoesNotContain("resolvedCommands", toolchain);
            Assert.DoesNotContain("installLayout", toolchain);

            foreach (var name in RequiredOutputs)
            {
                var content = File.ReadAllText(Path.Combine(firstOutput, name));
                Assert.DoesNotMatch(WindowsAbsolutePath, content);
                Assert.DoesNotMatch(UnixAbsolutePath, content);
                Assert.DoesNotContain(root, content, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain('\r', content);
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(firstOutput, name)),
                    File.ReadAllBytes(Path.Combine(secondOutput, name)));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingPhaseRelationshipCannotEarnCompleteStatus()
    {
        var root = NewTemporaryDirectory();
        try
        {
            var fixture = WriteCompleteFixture(Path.Combine(root, "source"));
            var lines = File.ReadAllLines(fixture.Synthesis)
                .Where(line => !line.Contains("pa_bin_add_w8,STRUCTURAL_OPTIMIZED", StringComparison.Ordinal));
            File.WriteAllText(fixture.Synthesis, string.Join('\n', lines) + "\n", Utf8NoBom);

            var receipt = Build002HdlEvidenceImporter.Import(
                Path.Combine(root, "output"),
                fixture.Summary,
                fixture.Synthesis,
                fixture.Toolchain);

            Assert.False(receipt.Complete);
            Assert.Equal("INCOMPLETE_PHASE_COVERAGE", receipt.Status);
            Assert.Equal(149, receipt.SynthesisRowCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProtocolAndPinnedToolchainTamperingAreRejected()
    {
        var root = NewTemporaryDirectory();
        try
        {
            var fixture = WriteCompleteFixture(Path.Combine(root, "source"));
            var summary = File.ReadAllText(fixture.Summary)
                .Replace("PAH-BUILD002-CONF0001", "PAH-BUILD002-TAMPERED", StringComparison.Ordinal);
            File.WriteAllText(fixture.Summary, summary, Utf8NoBom);
            Assert.Throws<InvalidDataException>(() => Build002HdlEvidenceImporter.Import(
                Path.Combine(root, "summary-output"),
                fixture.Summary,
                fixture.Synthesis,
                fixture.Toolchain));

            fixture = WriteCompleteFixture(Path.Combine(root, "source-two"));
            var toolchain = File.ReadAllText(fixture.Toolchain)
                .Replace(
                    "95d3cf2a59d1617f2363ee9370bb3577799f33a07e9c66e126ddeb68e8e5814c",
                    new string('0', 64),
                    StringComparison.Ordinal);
            File.WriteAllText(fixture.Toolchain, toolchain, Utf8NoBom);
            Assert.Throws<InvalidDataException>(() => Build002HdlEvidenceImporter.Import(
                Path.Combine(root, "toolchain-output"),
                fixture.Summary,
                fixture.Synthesis,
                fixture.Toolchain));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static FixturePaths WriteCompleteFixture(string sourceDirectory)
    {
        Directory.CreateDirectory(sourceDirectory);
        var cases = new List<object>
        {
            Case("ANALYZER_REGRESSION", "netlist_alias_and_failure_guards"),
            Case("SIMULATION", "tb_primitives"),
        };
        foreach (var width in new[] { 4, 6, 8 })
        {
            foreach (var pattern in RequiredTopPatterns)
            {
                var top = string.Format(CultureInfo.InvariantCulture, pattern, width);
                cases.Add(Case("LINT", top));
                cases.Add(Case("SYNTHESIS_DECLARED", top));
                cases.Add(Case("SYNTHESIS_OPTIMIZED", top));
            }

            foreach (var family in RequiredSimulationFamilies)
            {
                cases.Add(Case("SIMULATION", $"tb_{family}_w{width}"));
            }

            foreach (var family in RequiredFormalFamilies)
            {
                var detail = width == 4 && family == "binary"
                    ? @"proof at E:\secret lab\formal.log"
                    : string.Empty;
                cases.Add(Case("FORMAL", $"formal_{family}_w{width}", detail));
            }
        }

        var summaryPath = Path.Combine(sourceDirectory, "verification-summary.json");
        WriteJson(summaryPath, new
        {
            schema = "prime-axiom-build002-hdl-verification-v1",
            protocol = "PAH-BUILD002-CONF0001",
            scope = "FULL_W4_W6_W8",
            status = "PASS",
            total_cases = cases.Count,
            passed_cases = cases.Count,
            failed_cases = 0,
            cases,
        });

        var synthesisPath = Path.Combine(sourceDirectory, "synthesis-metrics.csv");
        var csv = new StringBuilder();
        csv.AppendLine("protocol,top,evidence_class,nand2_static,dff_static,state_bits,input_bits,output_bits,port_bits,wire_bits,connections_static,max_fanout,cross_lane_connections,unit_nand_critical_depth,combinational_loop_status,netlist_sha256,validation_status");
        var rowNumber = 0;
        foreach (var width in new[] { 4, 6, 8 })
        {
            foreach (var pattern in RequiredTopPatterns)
            {
                var top = string.Format(CultureInfo.InvariantCulture, pattern, width);
                foreach (var evidenceClass in new[] { "STRUCTURAL_DECLARED", "STRUCTURAL_OPTIMIZED" })
                {
                    var hashCharacter = "0123456789abcdef"[rowNumber++ % 16];
                    csv.Append("PAH-BUILD002-CONF0001,")
                        .Append(top)
                        .Append(',')
                        .Append(evidenceClass)
                        .Append(",10,0,0,2,1,3,13,23,4,0,5,ACYCLIC,")
                        .Append(new string(hashCharacter, 64))
                        .AppendLine(",PASS");
                }
            }
        }

        File.WriteAllText(synthesisPath, csv.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), Utf8NoBom);
        var synthesisLogDirectory = Path.Combine(sourceDirectory, "synthesis");
        Directory.CreateDirectory(synthesisLogDirectory);
        foreach (var width in new[] { 4, 6, 8 })
        {
            foreach (var pattern in RequiredTopPatterns)
            {
                var top = string.Format(CultureInfo.InvariantCulture, pattern, width);
                foreach (var mode in new[] { "declared", "optimized" })
                {
                    if (top == "pa_bin_vsc_w8" && mode == "optimized")
                    {
                        continue;
                    }

                    var content = top == "pa_bin_add_w4" && mode == "optimized"
                        ? "Warning: first. warning: second.\n"
                        : top == "pa_bin_add_w6" && mode == "optimized"
                            ? "ABC Warning: one.\n"
                            : "No diagnostics.\n";
                    File.WriteAllText(
                        Path.Combine(synthesisLogDirectory, $"{top}.{mode}.log"),
                        content,
                        Utf8NoBom);
                }
            }
        }

        var toolchainPath = Path.Combine(sourceDirectory, "toolchain-bootstrap.json");
        WriteJson(toolchainPath, new
        {
            schema = "prime-axiom-hdl-toolchain-bootstrap-v1",
            protocol = "PAH-BUILD002-CONF0001",
            release = "2026-08-24",
            platform = "windows-x64",
            asset = "oss-cad-suite-windows-x64-20260824.tgz",
            expected_bytes = 595_298_533,
            expected_sha256 = "95d3cf2a59d1617f2363ee9370bb3577799f33a07e9c66e126ddeb68e8e5814c",
            archive_bytes = 595_298_533,
            archive_sha256 = "95d3cf2a59d1617f2363ee9370bb3577799f33a07e9c66e126ddeb68e8e5814c",
            archive_status = "HASH_VERIFIED",
            source_url = "https://example.invalid/source",
            install_layout = @"E:\secret\oss-cad-suite\bin",
            resolved_commands = new { yosys = @"E:\secret\yosys.exe" },
            tool_versions = new Dictionary<string, string>
            {
                ["yosys"] = "Yosys 0.68 GNU /usr/bin/compiler 15.2.1",
                ["iverilog"] = "Icarus Verilog 14.0",
                ["vvp"] = "Icarus runtime 14.0",
                ["verilator"] = "Verilator 5.051",
                ["sby"] = "SBY v0.68",
                ["yosys-smtbmc"] = "yosys-smtbmc options",
                ["z3"] = "Z3 4.15.5",
            },
            status = "TOOLCHAIN_VERIFIED",
        });
        return new FixturePaths(summaryPath, synthesisPath, toolchainPath);
    }

    private static object Case(string phase, string name, string detail = "") => new
    {
        phase,
        @case = name,
        status = "PASS",
        log = @"E:\private\raw.log",
        detail,
    };

    private static void WriteJson(string path, object value)
    {
        var json = JsonSerializer.Serialize(value, FixtureJsonOptions);
        File.WriteAllText(path, json + "\n", Utf8NoBom);
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string NewTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "prime-axiom-hdl-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static readonly string[] RequiredOutputs =
    [
        "formal_receipts.json",
        "synthesis_metrics.csv",
        "toolchain.json",
    ];
    private static readonly string[] RequiredTopPatterns =
    [
        "pa_bin_add_w{0}", "pa_bin_sub_w{0}", "pa_bin_compare_w{0}", "pa_bin_mul_w{0}",
        "pa_bin_fu_w{0}", "pa_bin_counter_w{0}", "pa_bin_fu_registered_w{0}",
        "pa_binexp_compose_w{0}", "pa_binexp_checked_compose_w{0}", "pa_binexp_cancel_w{0}",
        "pa_binexp_meet_w{0}", "pa_binexp_join_w{0}", "pa_binexp_divides_w{0}",
        "pa_binexp_valuation_w{0}", "pa_binexp_power_w{0}",
        "pa_therm_compose_w{0}", "pa_therm_meet_w{0}", "pa_therm_join_w{0}",
        "pa_therm_divides_w{0}", "pa_therm_validate_w{0}", "pa_bin_to_therm_w{0}",
        "pa_therm_to_bin_w{0}", "pa_cold_encode_w{0}", "pa_vsc_query_w{0}", "pa_bin_vsc_w{0}",
    ];
    private static readonly string[] RequiredSimulationFamilies =
        ["binary", "counter", "binexp", "checked", "therm", "sidecar"];
    private static readonly string[] RequiredFormalFamilies =
        ["binary", "binexp", "checked", "therm", "sidecar"];

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions FixtureJsonOptions = new() { WriteIndented = true };
    private static readonly Regex WindowsAbsolutePath = new(
        "(?i)(?:[a-z]:[\\\\/]|\\\\\\\\)",
        RegexOptions.CultureInvariant);
    private static readonly Regex UnixAbsolutePath = new(
        "(?<![:A-Za-z0-9])/(?:[^/\\s\"']+/)*[^/\\s\"']+",
        RegexOptions.CultureInvariant);

    private sealed record FixturePaths(string Summary, string Synthesis, string Toolchain);
}
