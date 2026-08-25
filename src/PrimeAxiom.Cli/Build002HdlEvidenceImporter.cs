using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PrimeAxiom.Cli;

public sealed record Build002HdlImportReceipt(
    bool Complete,
    string Status,
    int VerificationCaseCount,
    int FormalCaseCount,
    int SynthesisRowCount,
    int WarningCountsMeasured,
    int WarningCountsNotMeasured,
    string VerificationSummarySourceSha256,
    string SynthesisMetricsSourceSha256,
    string ToolchainBootstrapSourceSha256,
    string SynthesisMetricsOutputSha256,
    string FormalReceiptsOutputSha256,
    string ToolchainOutputSha256);

/// <summary>
/// Validates and sanitizes frozen Build 002 HDL receipts. Source paths and raw
/// logs never cross the generated-evidence boundary; only allowlisted fields,
/// content hashes, and warning counts are retained.
/// </summary>
public static partial class Build002HdlEvidenceImporter
{
    private const string VerificationSchema = "prime-axiom-build002-hdl-verification-v1";
    private const string ToolchainSchema = "prime-axiom-hdl-toolchain-bootstrap-v1";
    private const string FullScope = "FULL_W4_W6_W8";
    private const string NotSupplied = "NOT_SUPPLIED";
    private const string NotMeasured = "NOT_MEASURED";
    private const string ToolchainRelease = "2026-08-24";
    private static readonly int[] RequiredWidths = [4, 6, 8];
    private static readonly string[] RequiredToolVersions =
    [
        "yosys",
        "iverilog",
        "vvp",
        "verilator",
        "sby",
        "yosys-smtbmc",
        "z3",
    ];

    private static readonly Dictionary<string, PinnedAsset> PinnedAssets =
        new Dictionary<string, PinnedAsset>(StringComparer.Ordinal)
        {
            ["windows-x64"] = new(
                "oss-cad-suite-windows-x64-20260824.tgz",
                595_298_533,
                "95d3cf2a59d1617f2363ee9370bb3577799f33a07e9c66e126ddeb68e8e5814c"),
            ["linux-x64"] = new(
                "oss-cad-suite-linux-x64-20260824.tgz",
                741_360_658,
                "9d7f79975ef624e1119fc9690fd9b9839b67026925aff3e2a1192d861b8dbb7c"),
        };
    private static readonly JsonSerializerOptions SourceJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private static readonly string[] SynthesisHeaders =
    [
        "protocol_id",
        "platform",
        "tool_version",
        "top",
        "width",
        "implementation",
        "architecture",
        "evidence_class",
        "status",
        "support_status",
        "nand2_static",
        "dff_static",
        "state_bits",
        "input_bits",
        "output_bits",
        "port_bits",
        "wire_bits",
        "connections_static",
        "max_fanout",
        "cross_lane_connections",
        "unit_nand_critical_depth",
        "combinational_loop_status",
        "netlist_sha256",
        "warning_count",
        "notes",
    ];

    private static readonly string[] RequiredSourceSynthesisHeaders =
    [
        "protocol",
        "top",
        "evidence_class",
        "nand2_static",
        "dff_static",
        "state_bits",
        "input_bits",
        "output_bits",
        "port_bits",
        "wire_bits",
        "connections_static",
        "max_fanout",
        "cross_lane_connections",
        "unit_nand_critical_depth",
        "combinational_loop_status",
        "netlist_sha256",
        "validation_status",
    ];

    public static Build002HdlImportReceipt Import(
        string outputDirectory,
        string? verificationSummaryPath = null,
        string? synthesisMetricsPath = null,
        string? toolchainBootstrapPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        verificationSummaryPath = NormalizeOptionalPath(verificationSummaryPath);
        synthesisMetricsPath = NormalizeOptionalPath(synthesisMetricsPath);
        toolchainBootstrapPath = NormalizeOptionalPath(toolchainBootstrapPath);

        var summary = verificationSummaryPath is null
            ? null
            : LoadVerificationSummary(verificationSummaryPath);
        var synthesis = synthesisMetricsPath is null
            ? null
            : LoadSynthesisMetrics(synthesisMetricsPath);
        var toolchain = toolchainBootstrapPath is null
            ? null
            : LoadToolchain(toolchainBootstrapPath);
        var verificationSourceSha256 = HashOptional(verificationSummaryPath);
        var synthesisSourceSha256 = HashOptional(synthesisMetricsPath);
        var toolchainSourceSha256 = HashOptional(toolchainBootstrapPath);

        var summaryCoverage = summary is not null && HasFullSummaryCoverage(summary);
        var relationshipCoverage = summary is not null && synthesis is not null &&
                                   HasCompleteSynthesisRelationships(summary, synthesis.Rows);
        var summaryPassed = summary is not null &&
                            string.Equals(summary.Status, "PASS", StringComparison.Ordinal) &&
                            summary.FailedCases == 0 &&
                            summary.Cases.All(item => string.Equals(item.Status, "PASS", StringComparison.Ordinal));
        var synthesisPassed = synthesis is not null &&
                              synthesis.Rows.All(row => string.Equals(row.ValidationStatus, "PASS", StringComparison.Ordinal));
        var toolchainVerified = toolchain is not null && toolchain.Verified;
        var allSupplied = summary is not null && synthesis is not null && toolchain is not null;
        var complete = allSupplied && summaryCoverage && relationshipCoverage &&
                       summaryPassed && synthesisPassed && toolchainVerified;
        var status = DetermineStatus(
            complete,
            summary,
            synthesis,
            toolchain,
            summaryCoverage,
            relationshipCoverage,
            summaryPassed,
            synthesisPassed,
            toolchainVerified);

        var synthesisOutputPath = Path.Combine(outputDirectory, "synthesis_metrics.csv");
        var formalOutputPath = Path.Combine(outputDirectory, "formal_receipts.json");
        var toolchainOutputPath = Path.Combine(outputDirectory, "toolchain.json");
        EnsureOutputsDoNotAliasSources(
            [synthesisOutputPath, formalOutputPath, toolchainOutputPath],
            [verificationSummaryPath, synthesisMetricsPath, toolchainBootstrapPath]);
        Directory.CreateDirectory(outputDirectory);

        var platform = toolchain?.Platform ?? NotSupplied;
        var yosysVersion = toolchain is null
            ? NotSupplied
            : SanitizeText(toolchain.ToolVersions["yosys"]);
        var importedSynthesisRows = synthesis is null
            ? []
            : BuildImportedSynthesisRows(synthesis, platform, yosysVersion, toolchainVerified);
        WriteSynthesis(synthesisOutputPath, importedSynthesisRows);
        WriteFormalReceipts(
            formalOutputPath,
            summary,
            verificationSourceSha256,
            summaryCoverage && summaryPassed);
        WriteToolchain(toolchainOutputPath, toolchain);

        EnsureNoAbsolutePaths(synthesisOutputPath);
        EnsureNoAbsolutePaths(formalOutputPath);
        EnsureNoAbsolutePaths(toolchainOutputPath);

        return new Build002HdlImportReceipt(
            complete,
            status,
            summary?.Cases.Count ?? 0,
            summary?.Cases.Count(item => string.Equals(item.Phase, "FORMAL", StringComparison.Ordinal)) ?? 0,
            importedSynthesisRows.Count,
            importedSynthesisRows.Count(row => !string.Equals(row.WarningCount, NotMeasured, StringComparison.Ordinal)),
            importedSynthesisRows.Count(row => string.Equals(row.WarningCount, NotMeasured, StringComparison.Ordinal)),
            verificationSourceSha256,
            synthesisSourceSha256,
            toolchainSourceSha256,
            Build002Protocol.HashFile(synthesisOutputPath),
            Build002Protocol.HashFile(formalOutputPath),
            Build002Protocol.HashFile(toolchainOutputPath));
    }

    private static VerificationSummarySource LoadVerificationSummary(string path)
    {
        EnsureFileExists(path);
        var value = Deserialize<VerificationSummarySource>(path, "verification summary");
        RequireEqual(value.Schema, VerificationSchema, "verification summary schema");
        RequireEqual(value.Protocol, Build002Protocol.Id, "verification summary protocol");
        RequireNonempty(value.Scope, "verification summary scope");
        if (value.Status is not "PASS" and not "FAIL")
        {
            throw Invalid("verification summary status must be PASS or FAIL");
        }

        if (value.Cases is null)
        {
            throw Invalid("verification summary cases are missing");
        }

        if (value.TotalCases < 0 || value.PassedCases < 0 || value.FailedCases < 0 ||
            value.TotalCases != value.Cases.Count ||
            value.PassedCases + value.FailedCases != value.TotalCases)
        {
            throw Invalid("verification summary counts are inconsistent");
        }

        var duplicateKeys = new HashSet<string>(StringComparer.Ordinal);
        var observedPassed = 0;
        var observedFailed = 0;
        foreach (var item in value.Cases)
        {
            RequireToken(item.Phase, "verification phase", UpperTokenRegex());
            RequireToken(item.Case, "verification case", LowerTokenRegex());
            if (item.Status is not "PASS" and not "FAIL")
            {
                throw Invalid($"verification case '{item.Case}' has an invalid status");
            }

            if (!duplicateKeys.Add($"{item.Phase}\0{item.Case}"))
            {
                throw Invalid($"verification case '{item.Phase}/{item.Case}' is duplicated");
            }

            if (item.Status == "PASS")
            {
                observedPassed++;
            }
            else
            {
                observedFailed++;
            }

            if (item.Phase is "LINT" or "SYNTHESIS_DECLARED" or "SYNTHESIS_OPTIMIZED")
            {
                _ = ParseWidth(item.Case);
            }
        }

        if (observedPassed != value.PassedCases || observedFailed != value.FailedCases)
        {
            throw Invalid("verification summary aggregate counts do not match case statuses");
        }

        if ((value.Status == "PASS") != (observedFailed == 0))
        {
            throw Invalid("verification summary overall status contradicts its cases");
        }

        return value;
    }

    private static SynthesisSource LoadSynthesisMetrics(string path)
    {
        EnsureFileExists(path);
        var parsed = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
        if (parsed.Count == 0)
        {
            throw Invalid("synthesis metrics CSV is empty");
        }

        var headers = parsed[0];
        if (headers.Count != headers.Distinct(StringComparer.Ordinal).Count())
        {
            throw Invalid("synthesis metrics CSV contains duplicate headers");
        }

        var headerMap = headers
            .Select((name, index) => (name, index))
            .ToDictionary(pair => pair.name, pair => pair.index, StringComparer.Ordinal);
        foreach (var required in RequiredSourceSynthesisHeaders)
        {
            if (!headerMap.ContainsKey(required))
            {
                throw Invalid($"synthesis metrics CSV is missing '{required}'");
            }
        }

        var rows = new List<SourceSynthesisRow>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 1; index < parsed.Count; index++)
        {
            var fields = parsed[index];
            if (fields.Count != headers.Count)
            {
                throw Invalid($"synthesis metrics row {index + 1} has the wrong field count");
            }

            string Field(string name) => fields[headerMap[name]];
            RequireEqual(Field("protocol"), Build002Protocol.Id, $"synthesis row {index} protocol");
            var top = Field("top");
            RequireToken(top, $"synthesis row {index} top", LowerTokenRegex());
            var width = ParseWidth(top);
            var evidenceClass = Field("evidence_class");
            if (evidenceClass is not "STRUCTURAL_DECLARED" and not "STRUCTURAL_OPTIMIZED")
            {
                throw Invalid($"synthesis row {index} has an invalid evidence class");
            }

            var validationStatus = Field("validation_status");
            if (validationStatus is not "PASS" and not "FAIL")
            {
                throw Invalid($"synthesis row {index} has an invalid validation status");
            }

            var loopStatus = Field("combinational_loop_status");
            if (!string.Equals(loopStatus, "ACYCLIC", StringComparison.Ordinal))
            {
                throw Invalid($"synthesis row {index} does not certify an acyclic netlist");
            }

            var sha256 = Field("netlist_sha256");
            if (!Sha256Regex().IsMatch(sha256))
            {
                throw Invalid($"synthesis row {index} has an invalid netlist hash");
            }

            var row = new SourceSynthesisRow(
                top,
                width,
                evidenceClass,
                validationStatus,
                ParseNonnegative(Field("nand2_static"), "nand2_static", index),
                ParseNonnegative(Field("dff_static"), "dff_static", index),
                ParseNonnegative(Field("state_bits"), "state_bits", index),
                ParseNonnegative(Field("input_bits"), "input_bits", index),
                ParseNonnegative(Field("output_bits"), "output_bits", index),
                ParseNonnegative(Field("port_bits"), "port_bits", index),
                ParseNonnegative(Field("wire_bits"), "wire_bits", index),
                ParseNonnegative(Field("connections_static"), "connections_static", index),
                ParseNonnegative(Field("max_fanout"), "max_fanout", index),
                ParseNonnegative(Field("cross_lane_connections"), "cross_lane_connections", index),
                ParseNonnegative(Field("unit_nand_critical_depth"), "unit_nand_critical_depth", index),
                loopStatus,
                sha256.ToLowerInvariant());
            if (row.PortBits != row.InputBits + row.OutputBits)
            {
                throw Invalid($"synthesis row {index} port count is inconsistent");
            }

            if (!keys.Add($"{top}\0{evidenceClass}"))
            {
                throw Invalid($"synthesis row '{top}/{evidenceClass}' is duplicated");
            }

            rows.Add(row);
        }

        if (rows.Count == 0)
        {
            throw Invalid("synthesis metrics CSV has no evidence rows");
        }

        return new SynthesisSource(path, rows);
    }

    private static ToolchainSource LoadToolchain(string path)
    {
        EnsureFileExists(path);
        var value = Deserialize<ToolchainBootstrapSource>(path, "toolchain bootstrap receipt");
        RequireEqual(value.Schema, ToolchainSchema, "toolchain schema");
        RequireEqual(value.Protocol, Build002Protocol.Id, "toolchain protocol");
        RequireEqual(value.Release, ToolchainRelease, "toolchain release");
        RequireNonempty(value.Platform, "toolchain platform");
        if (!PinnedAssets.TryGetValue(value.Platform!, out var pinned))
        {
            throw Invalid("toolchain platform is not one of the pinned Windows/Linux targets");
        }

        RequireEqual(value.Asset, pinned.Asset, "toolchain asset");
        if (value.ExpectedBytes != pinned.Bytes || value.ArchiveBytes != pinned.Bytes)
        {
            throw Invalid("toolchain archive byte count does not match the pinned asset");
        }

        RequireHash(value.ExpectedSha256, pinned.Sha256, "toolchain expected hash");
        RequireHash(value.ArchiveSha256, pinned.Sha256, "toolchain archive hash");
        if (value.Status is not "TOOLCHAIN_VERIFIED" and not "TOOLCHAIN_FAILED")
        {
            throw Invalid("toolchain status is not recognized");
        }

        if (value.ArchiveStatus is not "HASH_VERIFIED" and not "HASH_FAILED")
        {
            throw Invalid("toolchain archive status is not recognized");
        }

        if (value.ToolVersions is null)
        {
            throw Invalid("toolchain versions are missing");
        }

        foreach (var name in RequiredToolVersions)
        {
            if (!value.ToolVersions.TryGetValue(name, out var version) ||
                string.IsNullOrWhiteSpace(version))
            {
                throw Invalid($"toolchain version '{name}' is missing");
            }
        }

        var verified = value.Status == "TOOLCHAIN_VERIFIED" &&
                       value.ArchiveStatus == "HASH_VERIFIED";
        return new ToolchainSource(
            value.Platform!,
            value.Asset!,
            value.ArchiveBytes,
            pinned.Sha256,
            value.ArchiveStatus!,
            value.Status!,
            value.ToolVersions,
            Build002Protocol.HashFile(path),
            verified);
    }

    private static List<ImportedSynthesisRow> BuildImportedSynthesisRows(
        SynthesisSource source,
        string platform,
        string yosysVersion,
        bool toolchainVerified)
    {
        var artifactDirectory = Path.GetDirectoryName(Path.GetFullPath(source.Path))!;
        return source.Rows
            .OrderBy(row => row.Width)
            .ThenBy(row => row.Top, StringComparer.Ordinal)
            .ThenBy(row => row.EvidenceClass == "STRUCTURAL_DECLARED" ? 0 : 1)
            .Select(row =>
            {
                var mapping = MapTop(row.Top);
                var warningCount = ReadWarningCount(artifactDirectory, row);
                var support = row.ValidationStatus == "FAIL"
                    ? "VALIDATION_FAILED"
                    : toolchainVerified
                        ? "SYNTHESIZED_AND_VALIDATED"
                        : "VALIDATED_SOURCE_TOOLCHAIN_NOT_VERIFIED";
                return new ImportedSynthesisRow(
                    platform,
                    yosysVersion,
                    row.Top,
                    row.Width.ToString(CultureInfo.InvariantCulture),
                    mapping.Implementation,
                    mapping.Architecture,
                    row.EvidenceClass,
                    row.ValidationStatus,
                    support,
                    row.Nand2Static,
                    row.DffStatic,
                    row.StateBits,
                    row.InputBits,
                    row.OutputBits,
                    row.PortBits,
                    row.WireBits,
                    row.ConnectionsStatic,
                    row.MaximumFanout,
                    row.CrossLaneConnections,
                    row.UnitNandCriticalDepth,
                    row.CombinationalLoopStatus,
                    row.NetlistSha256,
                    warningCount,
                    warningCount == NotMeasured
                        ? "Warning log was unavailable; no zero count was inferred."
                        : "Warning count was parsed from the corresponding synthesis log.");
            })
            .ToList();
    }

    private static void WriteSynthesis(string path, IReadOnlyList<ImportedSynthesisRow> rows)
    {
        var body = rows.Count == 0
            ? new[]
            {
                (IReadOnlyList<string>)
                [
                    Build002Protocol.Id,
                    NotSupplied,
                    NotSupplied,
                    NotSupplied,
                    NotSupplied,
                    NotSupplied,
                    NotSupplied,
                    NotSupplied,
                    NotSupplied,
                    NotSupplied,
                    NotMeasured,
                    NotMeasured,
                    NotMeasured,
                    NotMeasured,
                    NotMeasured,
                    NotMeasured,
                    NotMeasured,
                    NotMeasured,
                    NotMeasured,
                    NotMeasured,
                    NotMeasured,
                    NotSupplied,
                    NotSupplied,
                    NotMeasured,
                    "HDL synthesis evidence was not supplied.",
                ],
            }
            : rows.Select(row => row.ToCsv()).ToArray();
        Build002Protocol.WriteCsv(path, new[] { (IReadOnlyList<string>)SynthesisHeaders }.Concat(body));
    }

    private static void WriteFormalReceipts(
        string path,
        VerificationSummarySource? summary,
        string sourceSummarySha256,
        bool complete)
    {
        var formalCases = summary?.Cases
            .Where(item => string.Equals(item.Phase, "FORMAL", StringComparison.Ordinal))
            .OrderBy(item => TryParseWidth(item.Case, out var width) ? width : int.MaxValue)
            .ThenBy(item => item.Case, StringComparer.Ordinal)
            .Select(item => new SanitizedFormalCase(
                item.Case!,
                item.Status!,
                SanitizeText(item.Detail ?? string.Empty)))
            .ToArray() ?? [];
        var failed = formalCases.Count(item => item.Status == "FAIL");
        Build002Protocol.WriteJson(
            path,
            new FormalEvidenceOutput(
                "prime-axiom-build002-formal-receipts-v1",
                Build002Protocol.Id,
                summary?.Scope ?? NotSupplied,
                summary?.Status ?? NotSupplied,
                complete,
                sourceSummarySha256,
                formalCases.Length,
                formalCases.Length - failed,
                failed,
                formalCases));
    }

    private static void WriteToolchain(string path, ToolchainSource? toolchain)
    {
        var versions = toolchain is null
            ? new SortedDictionary<string, string>(
                RequiredToolVersions.ToDictionary(
                    name => name,
                    _ => NotSupplied,
                    StringComparer.Ordinal),
                StringComparer.Ordinal)
            : new SortedDictionary<string, string>(
                RequiredToolVersions.ToDictionary(
                    name => name,
                    name => SanitizeText(toolchain.ToolVersions[name]),
                    StringComparer.Ordinal),
                StringComparer.Ordinal);
        Build002Protocol.WriteJson(
            path,
            new ToolchainEvidenceOutput(
                "prime-axiom-build002-toolchain-evidence-v1",
                Build002Protocol.Id,
                toolchain?.Status ?? NotSupplied,
                toolchain?.Verified ?? false,
                toolchain is null ? NotSupplied : ToolchainRelease,
                toolchain?.Platform ?? NotSupplied,
                toolchain?.Asset ?? NotSupplied,
                toolchain?.ArchiveBytes.ToString(CultureInfo.InvariantCulture) ?? NotMeasured,
                toolchain?.ArchiveSha256 ?? NotSupplied,
                toolchain?.ArchiveStatus ?? NotSupplied,
                versions,
                toolchain?.SourceSha256 ?? NotSupplied));
    }

    private static bool HasFullSummaryCoverage(VerificationSummarySource summary)
    {
        if (!string.Equals(summary.Scope, FullScope, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var width in RequiredWidths)
        {
            var lint = CasesFor(summary, "LINT", width);
            var declared = CasesFor(summary, "SYNTHESIS_DECLARED", width);
            var optimized = CasesFor(summary, "SYNTHESIS_OPTIMIZED", width);
            if (lint.Count == 0 || !lint.SetEquals(declared) || !lint.SetEquals(optimized))
            {
                return false;
            }

            if (CasesFor(summary, "SIMULATION", width).Count == 0 ||
                CasesFor(summary, "FORMAL", width).Count == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasCompleteSynthesisRelationships(
        VerificationSummarySource summary,
        IReadOnlyList<SourceSynthesisRow> rows)
    {
        foreach (var width in RequiredWidths)
        {
            var declaredCases = CasesFor(summary, "SYNTHESIS_DECLARED", width);
            var optimizedCases = CasesFor(summary, "SYNTHESIS_OPTIMIZED", width);
            var declaredRows = rows
                .Where(row => row.Width == width && row.EvidenceClass == "STRUCTURAL_DECLARED")
                .Select(row => row.Top)
                .ToHashSet(StringComparer.Ordinal);
            var optimizedRows = rows
                .Where(row => row.Width == width && row.EvidenceClass == "STRUCTURAL_OPTIMIZED")
                .Select(row => row.Top)
                .ToHashSet(StringComparer.Ordinal);
            if (!declaredCases.SetEquals(declaredRows) || !optimizedCases.SetEquals(optimizedRows))
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<string> CasesFor(
        VerificationSummarySource summary,
        string phase,
        int width) =>
        summary.Cases
            .Where(item => item.Phase == phase && TryParseWidth(item.Case, out var observed) && observed == width)
            .Select(item => item.Case!)
            .ToHashSet(StringComparer.Ordinal);

    private static string DetermineStatus(
        bool complete,
        VerificationSummarySource? summary,
        SynthesisSource? synthesis,
        ToolchainSource? toolchain,
        bool summaryCoverage,
        bool relationshipCoverage,
        bool summaryPassed,
        bool synthesisPassed,
        bool toolchainVerified)
    {
        if (complete)
        {
            return "COMPLETE_VERIFIED";
        }

        if (summary is null && synthesis is null && toolchain is null)
        {
            return NotSupplied;
        }

        if ((!summaryPassed && summary is not null) ||
            (!synthesisPassed && synthesis is not null))
        {
            return "EVIDENCE_FAILED";
        }

        if (summary is null || synthesis is null || toolchain is null)
        {
            return "INCOMPLETE_NOT_SUPPLIED";
        }

        if (!summaryCoverage || !relationshipCoverage)
        {
            return "INCOMPLETE_PHASE_COVERAGE";
        }

        return !toolchainVerified ? "TOOLCHAIN_NOT_VERIFIED" : "INCOMPLETE_EVIDENCE";
    }

    private static (string Implementation, string Architecture) MapTop(string top)
    {
        var stem = TopWidthSuffixRegex().Replace(top, string.Empty);
        if (stem.StartsWith("pa_binexp_", StringComparison.Ordinal))
        {
            return ("VFU-BINEXP-S4", "BINARY_EXPONENT");
        }

        if (stem is "pa_bin_to_therm" or "pa_therm_to_bin")
        {
            return ("REPRESENTATION-ADAPTER-S4", stem == "pa_bin_to_therm"
                ? "BINARY_EXPONENT_TO_THERMOMETER"
                : "THERMOMETER_TO_BINARY_EXPONENT");
        }

        if (stem.StartsWith("pa_therm_", StringComparison.Ordinal))
        {
            return ("VFU-THERM-S4", "THERMOMETER_THRESHOLD");
        }

        if (stem == "pa_cold_encode")
        {
            return ("COLD-ENCODER-S4", "BINARY_MAGNITUDE_TO_BINARY_EXPONENT");
        }

        if (stem is "pa_vsc_query" or "pa_bin_vsc")
        {
            return ("VALUATION-SIDECAR-S4", stem == "pa_vsc_query"
                ? "THRESHOLD_SIDECAR_QUERY"
                : "BINARY_WITH_VALUATION_SIDECAR");
        }

        if (stem.StartsWith("pa_bin_", StringComparison.Ordinal))
        {
            return ("BIN-FU", "BINARY_POSITIONAL");
        }

        return ("HDL-OTHER", stem.ToUpperInvariant());
    }

    private static string ReadWarningCount(string artifactDirectory, SourceSynthesisRow row)
    {
        var suffix = row.EvidenceClass == "STRUCTURAL_DECLARED" ? "declared" : "optimized";
        var synthesisDirectory = Path.GetFullPath(Path.Combine(artifactDirectory, "synthesis"));
        var logPath = Path.GetFullPath(Path.Combine(synthesisDirectory, $"{row.Top}.{suffix}.log"));
        if (!logPath.StartsWith(synthesisDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(logPath))
        {
            return NotMeasured;
        }

        try
        {
            long warnings = 0;
            foreach (var line in File.ReadLines(logPath))
            {
                warnings += WarningRegex().Matches(line).Count;
            }

            return warnings.ToString(CultureInfo.InvariantCulture);
        }
        catch (IOException)
        {
            return NotMeasured;
        }
        catch (UnauthorizedAccessException)
        {
            return NotMeasured;
        }
    }

    private static List<IReadOnlyList<string>> ParseCsv(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    if (row.Count != 1 || row[0].Length != 0)
                    {
                        rows.Add(row.ToArray());
                    }

                    row.Clear();
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (quoted)
        {
            throw Invalid("CSV contains an unterminated quoted field");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }

        if (rows.Count > 0 && rows[0].Count > 0)
        {
            var first = rows[0].ToArray();
            first[0] = first[0].TrimStart('\uFEFF');
            rows[0] = first;
        }

        return rows;
    }

    private static T Deserialize<T>(string path, string description)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(
                File.ReadAllText(path, Encoding.UTF8),
                SourceJsonOptions) ?? throw Invalid($"{description} deserialized to null");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid {description} JSON.", exception);
        }
    }

    private static long ParseNonnegative(string text, string field, int row)
    {
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            throw Invalid($"synthesis row {row} field '{field}' is not a nonnegative integer");
        }

        return value;
    }

    private static int ParseWidth(string? name)
    {
        if (!TryParseWidth(name, out var width))
        {
            throw Invalid($"case/top '{name}' does not end in a supported W4/W6/W8 width");
        }

        return width;
    }

    private static bool TryParseWidth(string? name, out int width)
    {
        width = 0;
        if (name is null)
        {
            return false;
        }

        var match = TopWidthSuffixRegex().Match(name);
        return match.Success && int.TryParse(
            match.Groups[1].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out width) && RequiredWidths.Contains(width);
    }

    private static string SanitizeText(string value)
    {
        var singleLine = value.Replace('\r', ' ').Replace('\n', ' ');
        singleLine = WindowsAbsolutePathRegex().Replace(singleLine, "[REDACTED_PATH]");
        singleLine = UnixAbsolutePathRegex().Replace(singleLine, "[REDACTED_PATH]");
        return singleLine.Trim();
    }

    private static void EnsureNoAbsolutePaths(string path)
    {
        var content = File.ReadAllText(path, Encoding.UTF8);
        if (WindowsAbsolutePathMarkerRegex().IsMatch(content) ||
            UnixAbsolutePathRegex().IsMatch(content))
        {
            throw new InvalidOperationException($"Sanitized evidence '{Path.GetFileName(path)}' contains an absolute path.");
        }
    }

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("An explicitly supplied HDL evidence file does not exist.", path);
        }
    }

    private static void EnsureOutputsDoNotAliasSources(
        IEnumerable<string> outputPaths,
        IEnumerable<string?> sourcePaths)
    {
        var sources = sourcePaths
            .Where(path => path is not null)
            .Select(path => Path.GetFullPath(path!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var output in outputPaths)
        {
            if (sources.Contains(Path.GetFullPath(output)))
            {
                throw new InvalidOperationException(
                    "The HDL evidence output directory would overwrite a supplied source receipt.");
            }
        }
    }

    private static string? NormalizeOptionalPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);

    private static string HashOptional(string? path) =>
        path is null ? NotSupplied : Build002Protocol.HashFile(path);

    private static void RequireEqual(string? actual, string expected, string description)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw Invalid($"{description} mismatch");
        }
    }

    private static void RequireHash(string? actual, string expected, string description)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid($"{description} mismatch");
        }
    }

    private static void RequireNonempty(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid($"{description} is missing");
        }
    }

    private static void RequireToken(string? value, string description, Regex regex)
    {
        if (value is null || !regex.IsMatch(value))
        {
            throw Invalid($"{description} is not a safe token");
        }
    }

    private static InvalidDataException Invalid(string message) => new(message + ".");

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex UpperTokenRegex();

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex LowerTokenRegex();

    [GeneratedRegex("_w(4|6|8)$", RegexOptions.CultureInvariant)]
    private static partial Regex TopWidthSuffixRegex();

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    [GeneratedRegex("\\bwarning\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WarningRegex();

    [GeneratedRegex("(?i)(?:[a-z]:[\\\\/]|\\\\\\\\)[^\"'\\r\\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsAbsolutePathRegex();

    [GeneratedRegex("(?i)(?:[a-z]:[\\\\/]|\\\\\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsAbsolutePathMarkerRegex();

    [GeneratedRegex("(?<![:A-Za-z0-9])/(?:[^/\\s\"']+/)*[^/\\s\"']+", RegexOptions.CultureInvariant)]
    private static partial Regex UnixAbsolutePathRegex();

    private sealed record PinnedAsset(string Asset, long Bytes, string Sha256);

    private sealed class VerificationSummarySource
    {
        [JsonPropertyName("schema")]
        public string? Schema { get; set; }

        [JsonPropertyName("protocol")]
        public string? Protocol { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("total_cases")]
        public int TotalCases { get; set; }

        [JsonPropertyName("passed_cases")]
        public int PassedCases { get; set; }

        [JsonPropertyName("failed_cases")]
        public int FailedCases { get; set; }

        [JsonPropertyName("cases")]
        public List<VerificationCaseSource> Cases { get; set; } = [];
    }

    private sealed class VerificationCaseSource
    {
        [JsonPropertyName("phase")]
        public string? Phase { get; set; }

        [JsonPropertyName("case")]
        public string? Case { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }
    }

    private sealed class ToolchainBootstrapSource
    {
        [JsonPropertyName("schema")]
        public string? Schema { get; set; }

        [JsonPropertyName("protocol")]
        public string? Protocol { get; set; }

        [JsonPropertyName("release")]
        public string? Release { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("asset")]
        public string? Asset { get; set; }

        [JsonPropertyName("expected_bytes")]
        public long ExpectedBytes { get; set; }

        [JsonPropertyName("expected_sha256")]
        public string? ExpectedSha256 { get; set; }

        [JsonPropertyName("archive_bytes")]
        public long ArchiveBytes { get; set; }

        [JsonPropertyName("archive_sha256")]
        public string? ArchiveSha256 { get; set; }

        [JsonPropertyName("archive_status")]
        public string? ArchiveStatus { get; set; }

        [JsonPropertyName("tool_versions")]
        public Dictionary<string, string>? ToolVersions { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    private sealed record ToolchainSource(
        string Platform,
        string Asset,
        long ArchiveBytes,
        string ArchiveSha256,
        string ArchiveStatus,
        string Status,
        IReadOnlyDictionary<string, string> ToolVersions,
        string SourceSha256,
        bool Verified);

    private sealed record SynthesisSource(string Path, IReadOnlyList<SourceSynthesisRow> Rows);

    private sealed record SourceSynthesisRow(
        string Top,
        int Width,
        string EvidenceClass,
        string ValidationStatus,
        long Nand2Static,
        long DffStatic,
        long StateBits,
        long InputBits,
        long OutputBits,
        long PortBits,
        long WireBits,
        long ConnectionsStatic,
        long MaximumFanout,
        long CrossLaneConnections,
        long UnitNandCriticalDepth,
        string CombinationalLoopStatus,
        string NetlistSha256);

    private sealed record ImportedSynthesisRow(
        string Platform,
        string ToolVersion,
        string Top,
        string Width,
        string Implementation,
        string Architecture,
        string EvidenceClass,
        string Status,
        string SupportStatus,
        long Nand2Static,
        long DffStatic,
        long StateBits,
        long InputBits,
        long OutputBits,
        long PortBits,
        long WireBits,
        long ConnectionsStatic,
        long MaximumFanout,
        long CrossLaneConnections,
        long UnitNandCriticalDepth,
        string CombinationalLoopStatus,
        string NetlistSha256,
        string WarningCount,
        string Notes)
    {
        public IReadOnlyList<string> ToCsv() =>
        [
            Build002Protocol.Id,
            Platform,
            ToolVersion,
            Top,
            Width,
            Implementation,
            Architecture,
            EvidenceClass,
            Status,
            SupportStatus,
            Nand2Static.ToString(CultureInfo.InvariantCulture),
            DffStatic.ToString(CultureInfo.InvariantCulture),
            StateBits.ToString(CultureInfo.InvariantCulture),
            InputBits.ToString(CultureInfo.InvariantCulture),
            OutputBits.ToString(CultureInfo.InvariantCulture),
            PortBits.ToString(CultureInfo.InvariantCulture),
            WireBits.ToString(CultureInfo.InvariantCulture),
            ConnectionsStatic.ToString(CultureInfo.InvariantCulture),
            MaximumFanout.ToString(CultureInfo.InvariantCulture),
            CrossLaneConnections.ToString(CultureInfo.InvariantCulture),
            UnitNandCriticalDepth.ToString(CultureInfo.InvariantCulture),
            CombinationalLoopStatus,
            NetlistSha256,
            WarningCount,
            Notes,
        ];
    }

    private sealed record SanitizedFormalCase(string Case, string Status, string Detail);

    private sealed record FormalEvidenceOutput(
        string Schema,
        string Protocol,
        string Scope,
        string Status,
        bool Complete,
        string SourceSummarySha256,
        int CaseCount,
        int PassedCaseCount,
        int FailedCaseCount,
        IReadOnlyList<SanitizedFormalCase> Cases);

    private sealed record ToolchainEvidenceOutput(
        string Schema,
        string Protocol,
        string Status,
        bool Complete,
        string Release,
        string Platform,
        string Asset,
        string ArchiveBytes,
        string ArchiveSha256,
        string ArchiveStatus,
        IReadOnlyDictionary<string, string> ToolVersions,
        string SourceBootstrapSha256);
}
