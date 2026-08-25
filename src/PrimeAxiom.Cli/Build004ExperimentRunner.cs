using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PrimeAxiom.Cli;

internal sealed record Build004RunReceipt(
    string OutputDirectory,
    long CheckCount,
    long FailureCount,
    string Status,
    string CandidateFrameworkStatus);

internal static class Build004ExperimentRunner
{
    internal static readonly IReadOnlyList<string> ExpectedFiles = Array.AsReadOnly(new[]
    {
        "README.md",
        "combinatorics.json",
        "lineage.json",
        "fusion.json",
        "boundary_probes.json",
        "just_intonation_demo.wav",
        "structural_costs.csv",
        "correctness.json",
        "protocol_coverage.json",
        "manifest.json",
    });

    public static Build004RunReceipt Run(string repositoryRoot, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        outputDirectory = Path.GetFullPath(outputDirectory);
        ValidateFrozenPlan(repositoryRoot);
        ValidateOutputLocation(repositoryRoot, outputDirectory);
        PrepareOutput(outputDirectory);

        var campaign = Build004Campaign.Run();
        var candidateFrameworkStatus = campaign.CompleteFrozenCoverage && campaign.Failures == 0
            ? Build004Protocol.FrameworkStatus
            : Build004Protocol.PartialStatus;
        var status = Build004Protocol.PartialStatus;

        Build004Protocol.WriteJson(Path.Combine(outputDirectory, "combinatorics.json"), campaign.Combinatorics);
        Build004Protocol.WriteJson(Path.Combine(outputDirectory, "lineage.json"), campaign.Lineage);
        Build004Protocol.WriteJson(Path.Combine(outputDirectory, "fusion.json"), campaign.Fusion);
        Build004Protocol.WriteJson(Path.Combine(outputDirectory, "boundary_probes.json"), campaign.BoundaryProbes);
        File.WriteAllBytes(Path.Combine(outputDirectory, "just_intonation_demo.wav"), campaign.AudioBytes);
        WriteStructuralCosts(Path.Combine(outputDirectory, "structural_costs.csv"), campaign.StructuralCosts);
        WriteCorrectness(Path.Combine(outputDirectory, "correctness.json"), campaign, status);
        WriteCoverage(
            Path.Combine(outputDirectory, "protocol_coverage.json"),
            campaign,
            status,
            candidateFrameworkStatus);
        WriteReadme(
            Path.Combine(outputDirectory, "README.md"),
            campaign,
            status,
            candidateFrameworkStatus);
        WriteManifest(outputDirectory, campaign, status, candidateFrameworkStatus);

        return new Build004RunReceipt(
            outputDirectory,
            campaign.Checks,
            campaign.Failures,
            status,
            candidateFrameworkStatus);
    }

    private static void WriteCorrectness(
        string path,
        Build004CampaignResult campaign,
        string status) =>
        Build004Protocol.WriteJson(path, new
        {
            Schema = "prime-axiom-build004-correctness-v1",
            Build004Protocol.ProtocolId,
            MasterSeed = Build004Protocol.MasterSeed.ToString("X16", CultureInfo.InvariantCulture),
            Status = campaign.Failures == 0 && campaign.CompleteFrozenCoverage ? "BOUNDED_PASS" : status,
            campaign.Checks,
            campaign.Failures,
            Families = campaign.Families,
            ZeroFailure = campaign.Failures == 0,
            ExactFrozenCaseCounts = campaign.CompleteFrozenCoverage,
            Build004Protocol.ClaimCeiling,
        });

    private static void WriteCoverage(
        string path,
        Build004CampaignResult campaign,
        string status,
        string candidateFrameworkStatus) =>
        Build004Protocol.WriteJson(path, new
        {
            Schema = "prime-axiom-build004-protocol-coverage-v1",
            Build004Protocol.ProtocolId,
            Build004Protocol.FrozenPlanSha256,
            Build004Protocol.BaselineCommit,
            Status = status,
            CandidateFrameworkStatus = candidateFrameworkStatus,
            ExternalVerificationRequired = true,
            RequiredFamilies = Build004Campaign.ExpectedCases.Keys.ToArray(),
            FamilyCoverage = campaign.Families,
            DeterministicReplay = "ESTABLISHED_ONLY_BY_VERIFY_BUILD004_TWO_EXTERNAL_INVOCATIONS",
            InheritedEvidence = "PROTECTED_AGAINST_MERGED_BUILD003_BASELINE_BY_VERIFY_BUILD004",
            Results = new
            {
                ExactPointProbability = "PRIME_COORDINATE_LOCAL_AFTER_LEGENDRE_CONSTRUCTION",
                ProbabilityEvent = "ADDITIVE_DAG_OR_EXACT_MAGNITUDE_SUM_REQUIRED",
                ActiveLineage = "PEV_SET_AND_PRIME_PRODUCT_EQUIVALENT_UNDER_VALID_REGISTRY",
                FullDerivation = "TOPOLOGY_PRESERVING_RECEIPT_REQUIRED__PERSISTENT_TYPED_DAG_TESTED",
                Etl = "IRREDUCIBLE_RELATIVE_TO_SCALAR_RESULT__POSITIVE_ALGEBRA_BOUND",
                Calibration = "RATIO_SCALE_LOCAL__AFFINE_LOG_NONLINEAR_ROUNDED_CORRELATED_OR_UNEVALUATED_UNCERTAINTY_REQUIRE_TYPED_CROSSINGS",
                Audio = "EXACT_RATIO_RETAINED_BESIDE_APPROXIMATE_PCM_READOUT",
                Accumulator = Build004Protocol.SecurityStatus,
                Privacy = Build004Protocol.PrivacyStatus,
            },
            CostLedgers = new
            {
                TotalRows = campaign.StructuralCosts.Count,
                AbstractStructureRows = campaign.StructuralCosts.Count(row =>
                    row.Ledger == Build004Campaign.AbstractStructureLedger),
                HostSoftwareDiagnosticRows = campaign.StructuralCosts.Count(row =>
                    row.Ledger == Build004Campaign.HostSoftwareLedger),
                PhysicalHardwareImplicationRows = campaign.StructuralCosts.Count(row =>
                    row.Ledger == Build004Campaign.PhysicalHardwareLedger),
                HostWallClock = "NOT_MEASURED",
                HostAllocation = "NOT_MEASURED",
                PhysicalHardwareMeasurementStatus = Build004Protocol.HardwareStatus,
                CrossLedgerRanking = "NOT_PERFORMED",
            },
            FrameworkComparison = "AFTER_THE_FACT_REMOVABLE_LENSES_ONLY__NOT_IMPLEMENTATION_EVIDENCE",
            Build004Protocol.ClaimCeiling,
        });

    private static void WriteReadme(
        string path,
        Build004CampaignResult campaign,
        string status,
        string candidateFrameworkStatus)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Build 004 generated evidence");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"> **Generated-evidence status: `{status}`**");
        builder.AppendLine(CultureInfo.InvariantCulture, $"> Campaign candidate after external verification: `{candidateFrameworkStatus}`");
        builder.AppendLine();
        builder.AppendLine("Build 004 tests a dual receipt: a replaceable exact active-source projection plus a persistent typed derivation DAG. It also tests exact combinatorial probability, exact-overlap fusion, calibration/unit crossings, just-intonation PCM readout, a transparent non-cryptographic accumulator, and a deliberately small BOM receipt.");
        builder.AppendLine();
        builder.AppendLine("## Frozen coverage");
        builder.AppendLine();
        foreach (var family in campaign.Families)
        {
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"- `{family.Family}`: {family.Cases.ToString(CultureInfo.InvariantCulture)}/{family.ExpectedCases.ToString(CultureInfo.InvariantCulture)} cases; {family.Checks.ToString(CultureInfo.InvariantCulture)}/{family.ExpectedChecks.ToString(CultureInfo.InvariantCulture)} checks; {family.FailureCount.ToString(CultureInfo.InvariantCulture)} failures; `{family.Status}`");
        }

        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Total assertions: {campaign.Checks.ToString(CultureInfo.InvariantCulture)}; failures: {campaign.Failures.ToString(CultureInfo.InvariantCulture)}.");
        builder.AppendLine();
        builder.AppendLine("## Bounded conclusions");
        builder.AppendLine();
        builder.AppendLine("- A hypergeometric point is constructed and retained as an exact signed prime-exponent ratio, then reconstructed once for exact magnitude/output; no completed result is factored. A tail or event sum crosses to exact rational addition.");
        builder.AppendLine("- A binary PEV, explicit set, and squarefree prime product answer the same unique-support questions under one valid registry.");
        builder.AppendLine("- Support and multiplicity projections cannot distinguish `a*b+c*d` from `a*c+b*d`; the retained DAG can.");
        builder.AppendLine("- Exact overlap identity is insufficient for exact fusion when the shared likelihood payload cannot be replayed.");
        builder.AppendLine("- Ratio-scale units compose structurally; affine, logarithmic, nonlinear, rounded, correlated, or expired cases require explicit transforms or remain unresolved.");
        builder.AppendLine("- The included membership token is `NOT_CRYPTOGRAPHIC` and `NO_PRIVACY`; public membership leakage is a tested property.");
        builder.AppendLine("- Hardware, wall-clock performance, allocation, and cross-ledger ranking are `NOT_MEASURED`.");
        builder.AppendLine();
        builder.AppendLine("The WAV artifact is a finite PCM approximation of a declared exact 3/2 interval above 220 Hz. The explicit numerical/render policy is recorded; deterministic byte replay is established only by the external verifier on its recorded host/runtime. It is not a perceptual or physical-audio validation.");
        builder.AppendLine();
        builder.AppendLine("Regenerate with:");
        builder.AppendLine();
        builder.AppendLine("```powershell");
        builder.AppendLine("dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build004 --output results/build004");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine(Build004Protocol.ClaimCeiling);
        File.WriteAllText(path, NormalizeLf(builder.ToString()), new UTF8Encoding(false));
    }

    private static void WriteStructuralCosts(
        string path,
        IReadOnlyList<Build004StructuralCostRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ledger,domain,case_id,metric,value,unit,software_meaning,hardware_implication");
        foreach (var row in rows)
        {
            builder.Append(Csv(row.Ledger));
            builder.Append(',');
            builder.Append(Csv(row.Domain));
            builder.Append(',');
            builder.Append(Csv(row.CaseId));
            builder.Append(',');
            builder.Append(Csv(row.Metric));
            builder.Append(',');
            builder.Append(Csv(row.Value));
            builder.Append(',');
            builder.Append(Csv(row.Unit));
            builder.Append(',');
            builder.Append(Csv(row.SoftwareMeaning));
            builder.Append(',');
            builder.AppendLine(Csv(row.HardwareImplication));
        }

        File.WriteAllText(path, NormalizeLf(builder.ToString()), new UTF8Encoding(false));
    }

    private static void WriteManifest(
        string outputDirectory,
        Build004CampaignResult campaign,
        string status,
        string candidateFrameworkStatus)
    {
        var files = ExpectedFiles
            .Where(file => file != "manifest.json")
            .Select(file => new
            {
                Path = file,
                Sha256 = Build004Protocol.FileSha256(Path.Combine(outputDirectory, file)),
                Bytes = new FileInfo(Path.Combine(outputDirectory, file)).Length,
            })
            .ToArray();
        Build004Protocol.WriteJson(Path.Combine(outputDirectory, "manifest.json"), new
        {
            Schema = "prime-axiom-build004-manifest-v1",
            Build004Protocol.ProtocolId,
            Build004Protocol.FrozenPlanSha256,
            Build004Protocol.BaselineCommit,
            MasterSeed = Build004Protocol.MasterSeed.ToString("X16", CultureInfo.InvariantCulture),
            RuntimeContract = "net8.0",
            SdkPolicy = "8.0.423 with rollForward=latestPatch",
            CanonicalReproductionCommand =
                "dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build004 --output results/build004",
            Status = status,
            CandidateFrameworkStatus = candidateFrameworkStatus,
            ExternalVerificationRequired = true,
            campaign.Checks,
            campaign.Failures,
            FamilyCases = campaign.Families.ToDictionary(
                family => family.Family,
                family => family.Cases,
                StringComparer.Ordinal),
            IncludedWallClockMeasurements = false,
            IncludedHardwareMeasurements = false,
            Files = files,
            Build004Protocol.ClaimCeiling,
            Notes = "manifest.json intentionally excludes itself; generated text is LF and UTF-8 without BOM; the WAV is deterministic binary PCM on the recorded verifier host/runtime.",
        });
    }

    private static string Csv(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string NormalizeLf(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static void ValidateFrozenPlan(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, "research", "build004_experiment_plan.md");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The frozen Build 004 experiment plan is missing.");
        }

        var actual = Build004Protocol.FileSha256(path);
        if (!string.Equals(actual, Build004Protocol.FrozenPlanSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Frozen Build 004 plan hash mismatch. Expected {Build004Protocol.FrozenPlanSha256}, found {actual}.");
        }
    }

    private static void PrepareOutput(string outputDirectory)
    {
        if (File.Exists(outputDirectory))
        {
            throw new InvalidOperationException($"Build 004 output is a file, not a directory: {outputDirectory}");
        }

        if (Directory.Exists(outputDirectory))
        {
            var collisions = ExpectedFiles.Where(file => File.Exists(Path.Combine(outputDirectory, file))).ToArray();
            if (collisions.Length > 0 && !IsOwnedBuild004Directory(outputDirectory))
            {
                throw new InvalidOperationException(
                    "Refusing to replace evidence-like filenames in a directory that is not an intact Build 004 result directory.");
            }
        }

        Directory.CreateDirectory(outputDirectory);
        foreach (var file in ExpectedFiles)
        {
            var path = Path.Combine(outputDirectory, file);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static bool IsOwnedBuild004Directory(string outputDirectory)
    {
        var manifestPath = Path.Combine(outputDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("schema", out var schema) ||
                schema.GetString() != "prime-axiom-build004-manifest-v1" ||
                !root.TryGetProperty("protocolId", out var protocol) ||
                protocol.GetString() != Build004Protocol.ProtocolId ||
                !root.TryGetProperty("frozenPlanSha256", out var plan) ||
                plan.GetString() != Build004Protocol.FrozenPlanSha256 ||
                !root.TryGetProperty("files", out var files) ||
                files.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var expectedInventory = ExpectedFiles.Order(StringComparer.Ordinal).ToArray();
            var actualInventory = Directory.EnumerateFileSystemEntries(outputDirectory)
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (!actualInventory.SequenceEqual(expectedInventory, StringComparer.Ordinal))
            {
                return false;
            }

            var expectedManifestFiles = ExpectedFiles
                .Where(file => file != "manifest.json")
                .ToHashSet(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in files.EnumerateArray())
            {
                var relative = entry.GetProperty("path").GetString();
                var expectedHash = entry.GetProperty("sha256").GetString();
                if (relative is null || expectedHash is null ||
                    Path.GetFileName(relative) != relative ||
                    !expectedManifestFiles.Contains(relative) ||
                    !seen.Add(relative))
                {
                    return false;
                }

                var fullPath = Path.Combine(outputDirectory, relative);
                if (!File.Exists(fullPath) ||
                    !string.Equals(Build004Protocol.FileSha256(fullPath), expectedHash, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return seen.SetEquals(expectedManifestFiles);
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void ValidateOutputLocation(string repositoryRoot, string outputDirectory)
    {
        RejectReparsePointTraversal(outputDirectory);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var root = repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var output = outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(root, output, comparison))
        {
            throw new InvalidOperationException("The repository root cannot be used as the Build 004 output directory.");
        }

        var rootPrefix = root + Path.DirectorySeparatorChar;
        if (!output.StartsWith(rootPrefix, comparison))
        {
            return;
        }

        var committedResults = Path.Combine(root, "results", "build004");
        var artifacts = Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar;
        var hiddenArtifacts = Path.Combine(root, ".artifacts") + Path.DirectorySeparatorChar;
        if (!string.Equals(output, committedResults, comparison) &&
            !output.StartsWith(artifacts, comparison) &&
            !output.StartsWith(hiddenArtifacts, comparison))
        {
            throw new InvalidOperationException(
                "Repository-local Build 004 output must be results/build004 or a descendant of artifacts/ or .artifacts/.");
        }
    }

    private static void RejectReparsePointTraversal(string outputDirectory)
    {
        var current = outputDirectory;
        while (!File.Exists(current) && !Directory.Exists(current))
        {
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }

        while (!string.IsNullOrEmpty(current) && (File.Exists(current) || Directory.Exists(current)))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    $"Build 004 output may not traverse a symbolic link or junction: {current}");
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }
    }
}
