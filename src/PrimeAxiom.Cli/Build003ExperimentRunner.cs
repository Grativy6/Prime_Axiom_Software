using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using PrimeAxiom.Core.Calculator;

namespace PrimeAxiom.Cli;

internal sealed record Build003RunReceipt(
    string OutputDirectory,
    long CheckCount,
    int FailureCount,
    string Status);

internal static class Build003ExperimentRunner
{
    internal const long ExpectedCorrectnessChecks = 52_914;
    internal const int ExpectedComparisonRows = 6;

    private static readonly int[] SmallPrimes = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31];
    internal static readonly IReadOnlyList<string> ExpectedFiles = Array.AsReadOnly(new[]
    {
        "README.md",
        "calculator_examples.json",
        "arithmetic_comparisons.json",
        "correctness.json",
        "protocol_coverage.json",
        "manifest.json",
    });
    internal static readonly IReadOnlyList<string> RequiredFamilies = Array.AsReadOnly(new[]
    {
        "EXHAUSTIVE_SIGNED_SMALL_DOMAIN",
        "SEEDED_FACTORED_PRODUCTS",
        "PARTIAL_BUDGET",
        "CANONICAL_CLI_GRAMMAR",
        "FROZEN_ARITHMETIC_COMPARISONS",
        "DETERMINISTIC_REPLAY",
    });
    internal static readonly IReadOnlyList<string> FrozenComparisonIds = Array.AsReadOnly(new[]
    {
        "USER_ADD_001",
        "USER_MUL_001",
        "ADD_REORGANIZE_001",
        "ADD_RADIX_BOUNDARY_001",
        "MUL_COLD_PRIMES_001",
        "MUL_FACTOR_RICH_001",
    });

    public static Build003RunReceipt Run(string repositoryRoot, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        outputDirectory = Path.GetFullPath(outputDirectory);
        ValidateFrozenPlan(repositoryRoot);
        ValidateOutputLocation(repositoryRoot, outputDirectory);
        PrepareOutput(outputDirectory);

        var policy = new PrimeReceiptPolicy();
        var comparisons = BuildComparisons(policy);
        var examples = BuildCalculatorExamples(policy);
        var correctness = RunCorrectness(comparisons, examples);
        var completeFrozenCoverage = HasExactFrozenComparisonSet(comparisons);
        var status = correctness.Failures.Count == 0 &&
            correctness.Checks == ExpectedCorrectnessChecks &&
            completeFrozenCoverage
            ? Build003Protocol.FrameworkStatus
            : correctness.Failures.Count == 0 ? "PARTIAL" : "FAILED";

        Build003Protocol.WriteJson(
            Path.Combine(outputDirectory, "calculator_examples.json"),
            new
            {
                Schema = "prime-axiom-build003-calculator-examples-v1",
                Build003Protocol.ProtocolId,
                Policy = policy,
                Receipts = examples,
                ClaimCeiling = Build003Protocol.ClaimCeiling,
            });
        Build003Protocol.WriteJson(
            Path.Combine(outputDirectory, "arithmetic_comparisons.json"),
            new
            {
                Schema = "prime-axiom-build003-arithmetic-comparisons-v1",
                Build003Protocol.ProtocolId,
                OutputContract = "MAGNITUDE_AND_RECEIPT",
                Comparisons = comparisons,
                Conclusions = new
                {
                    Multiplication = Build003Protocol.MultiplicationConclusion,
                    Addition = Build003Protocol.AdditionConclusion,
                    GeneralLlmImprovement = "NOT_MEASURED",
                },
                ClaimCeiling = Build003Protocol.ClaimCeiling,
            });
        Build003Protocol.WriteJson(Path.Combine(outputDirectory, "correctness.json"), correctness);
        WriteCoverage(Path.Combine(outputDirectory, "protocol_coverage.json"), comparisons, status, correctness);
        WriteReadme(Path.Combine(outputDirectory, "README.md"), comparisons, status, correctness);
        WriteManifest(outputDirectory, status, correctness, comparisons);

        return new Build003RunReceipt(outputDirectory, correctness.Checks, correctness.Failures.Count, status);
    }

    private static ArithmeticComparisonReceipt[] BuildComparisons(PrimeReceiptPolicy policy) =>
    [
        PrimeArithmeticComparison.CompareAddition(
            "USER_ADD_001",
            new BigInteger(125_891_290_390),
            new BigInteger(12_589_127_501_265),
            policy),
        PrimeArithmeticComparison.CompareMultiplication(
            "USER_MUL_001",
            new BigInteger[] { 218, 489, 175, 17 },
            policy),
        PrimeArithmeticComparison.CompareAddition(
            "ADD_REORGANIZE_001",
            new BigInteger(9_999_999_967),
            new BigInteger(33),
            policy),
        PrimeArithmeticComparison.CompareAddition(
            "ADD_RADIX_BOUNDARY_001",
            BigInteger.One << 32,
            BigInteger.One,
            policy),
        PrimeArithmeticComparison.CompareMultiplication(
            "MUL_COLD_PRIMES_001",
            new BigInteger[] { 104_729, 99_991, 97, 89 },
            policy),
        PrimeArithmeticComparison.CompareMultiplication(
            "MUL_FACTOR_RICH_001",
            new BigInteger[] { 360_360, 720_720, 17 },
            policy),
    ];

    private static PrimeReceipt[] BuildCalculatorExamples(PrimeReceiptPolicy policy) =>
    [
        PrimeReceiptCalculator.Analyze(BigInteger.Zero, policy),
        PrimeReceiptCalculator.Analyze(BigInteger.MinusOne, policy),
        PrimeReceiptCalculator.Analyze(new BigInteger(-72), policy),
        PrimeReceiptCalculator.Analyze(new BigInteger(360), policy),
        PrimeReceiptCalculator.Analyze(new BigInteger(99_991), policy),
        PrimeReceiptCalculator.Analyze(new BigInteger(1_009L * 1_013), new PrimeReceiptPolicy(0)),
    ];

    private static Build003CorrectnessReceipt RunCorrectness(
        IReadOnlyList<ArithmeticComparisonReceipt> comparisons,
        IReadOnlyList<PrimeReceipt> examples)
    {
        long checks = 0;
        var failures = new List<string>();

        for (var value = -4_096; value <= 4_096; value++)
        {
            var integer = new BigInteger(value);
            var receipt = PrimeReceiptCalculator.Analyze(integer);
            Check(receipt.Status != PrimeReceiptStatus.PartialBudget, $"small-domain-complete:{value}", ref checks, failures);
            Check(PrimeReceiptCalculator.VerifyIntegrity(receipt), $"small-domain-receipt-integrity:{value}", ref checks, failures);
            Check(PrimeReceiptCalculator.Reconstruct(receipt) == integer, $"small-domain-reconstruct:{value}", ref checks, failures);
            Check(ValidatePrimePowers(receipt), $"small-domain-primes:{value}", ref checks, failures);
        }

        var namedCases = new[]
        {
            (Name: "zero", Value: BigInteger.Zero, Status: PrimeReceiptStatus.ExactZero, Structure: "0"),
            (Name: "positive-unit", Value: BigInteger.One, Status: PrimeReceiptStatus.ExactUnit, Structure: "1"),
            (Name: "negative-unit", Value: BigInteger.MinusOne, Status: PrimeReceiptStatus.ExactUnit, Structure: "-1"),
            (Name: "negative-prime", Value: new BigInteger(-97), Status: PrimeReceiptStatus.ExactFactorization, Structure: "-(97)"),
            (Name: "repeated-power", Value: new BigInteger(1_024), Status: PrimeReceiptStatus.ExactFactorization, Structure: "2^10"),
            (Name: "square", Value: new BigInteger(49), Status: PrimeReceiptStatus.ExactFactorization, Structure: "7^2"),
            (Name: "semiprime", Value: new BigInteger(77), Status: PrimeReceiptStatus.ExactFactorization, Structure: "7 * 11"),
            (Name: "positive-prime", Value: new BigInteger(97), Status: PrimeReceiptStatus.ExactFactorization, Structure: "97"),
        };
        foreach (var namedCase in namedCases)
        {
            var receipt = PrimeReceiptCalculator.Analyze(namedCase.Value);
            Check(receipt.Status == namedCase.Status, $"named-status:{namedCase.Name}", ref checks, failures);
            Check(receipt.Structure == namedCase.Structure, $"named-structure:{namedCase.Name}", ref checks, failures);
            Check(PrimeReceiptCalculator.VerifyIntegrity(receipt), $"named-integrity:{namedCase.Name}", ref checks, failures);
            Check(PrimeReceiptCalculator.Reconstruct(receipt) == namedCase.Value, $"named-reconstruct:{namedCase.Name}", ref checks, failures);
            Check(ValidatePrimePowers(receipt), $"named-primes:{namedCase.Name}", ref checks, failures);
        }

        var generator = new SplitMix64(Build003Protocol.MasterSeed);
        const int randomizedTrials = 5_000;
        for (var trial = 0; trial < randomizedTrials; trial++)
        {
            var factorCount = 1 + (int)(generator.Next() % 5);
            var magnitude = BigInteger.One;
            for (var factorIndex = 0; factorIndex < factorCount; factorIndex++)
            {
                var prime = SmallPrimes[(int)(generator.Next() % (ulong)SmallPrimes.Length)];
                var exponent = 1 + (int)(generator.Next() % 4);
                magnitude *= BigInteger.Pow(prime, exponent);
            }

            var value = (generator.Next() & 1UL) == 0 ? magnitude : -magnitude;
            var receipt = PrimeReceiptCalculator.Analyze(value);
            Check(receipt.Status == PrimeReceiptStatus.ExactFactorization, $"random-complete:{trial}", ref checks, failures);
            Check(PrimeReceiptCalculator.VerifyIntegrity(receipt), $"random-receipt-integrity:{trial}", ref checks, failures);
            Check(PrimeReceiptCalculator.Reconstruct(receipt) == value, $"random-reconstruct:{trial}", ref checks, failures);
            Check(ValidatePrimePowers(receipt), $"random-primes:{trial}", ref checks, failures);
        }

        var partialValue = new BigInteger(2L * 3 * 1_009 * 1_013);
        foreach (var budget in new long[] { 0, 1, 3 })
        {
            var receipt = PrimeReceiptCalculator.Analyze(partialValue, new PrimeReceiptPolicy(budget));
            Check(receipt.Status == PrimeReceiptStatus.PartialBudget, $"partial-status:{budget}", ref checks, failures);
            Check(PrimeReceiptCalculator.VerifyIntegrity(receipt), $"partial-integrity:{budget}", ref checks, failures);
            Check(PrimeReceiptCalculator.Reconstruct(receipt) == partialValue, $"partial-reconstruct:{budget}", ref checks, failures);
            Check(receipt.MagnitudeIsPrime is null, $"partial-no-prime-claim:{budget}", ref checks, failures);
            Check(BigInteger.Parse(receipt.UnresolvedCofactorDecimal, CultureInfo.InvariantCulture) > BigInteger.One, $"partial-residual:{budget}", ref checks, failures);
        }

        var validGrammar = new[] { "0", "1", "-1", "999999999999999999" };
        foreach (var text in validGrammar)
        {
            Check(
                PrimeReceiptCalculator.TryParseCanonicalInteger(text, 64, out _, out _),
                $"valid-grammar:{text}",
                ref checks,
                failures);
        }

        var invalidGrammar = new[] { string.Empty, "+1", "-0", "01", " 1", "1_000", "1e3" };
        foreach (var text in invalidGrammar)
        {
            Check(
                !PrimeReceiptCalculator.TryParseCanonicalInteger(text, 64, out _, out _),
                $"invalid-grammar:{text}",
                ref checks,
                failures);
        }

        var maximumDigits = new string('9', PrimeReceiptCalculator.DefaultCliMaxDecimalDigits);
        var tooManyDigits = $"1{maximumDigits}";
        Check(
            PrimeReceiptCalculator.TryParseCanonicalInteger(
                maximumDigits,
                PrimeReceiptCalculator.DefaultCliMaxDecimalDigits,
                out _,
                out _),
            "maximum-digit-grammar",
            ref checks,
            failures);
        Check(
            !PrimeReceiptCalculator.TryParseCanonicalInteger(
                tooManyDigits,
                PrimeReceiptCalculator.DefaultCliMaxDecimalDigits,
                out _,
                out _),
            "over-limit-grammar",
            ref checks,
            failures);

        for (var index = 0; index < examples.Count; index++)
        {
            Check(
                PrimeReceiptCalculator.VerifyIntegrity(examples[index]),
                $"calculator-example-integrity:{index}",
                ref checks,
                failures);
        }

        foreach (var comparison in comparisons)
        {
            Check(comparison.PathsAgree, $"comparison-agreement:{comparison.Id}", ref checks, failures);
            Check(comparison.OrdinaryPath.OracleVerified, $"ordinary-oracle:{comparison.Id}", ref checks, failures);
            Check(comparison.PrimePath.OracleVerified, $"prime-oracle:{comparison.Id}", ref checks, failures);
            Check(comparison.PrimePath.ExactStructureCompleted, $"comparison-complete:{comparison.Id}", ref checks, failures);
            Check(ValidatePrimePowers(comparison.PrimePath.OutputReceipt), $"comparison-output-primes:{comparison.Id}", ref checks, failures);
            var comparisonReceipts = comparison.PrimePath.InputReceipts
                .Append(comparison.PrimePath.OutputReceipt)
                .ToArray();
            for (var receiptIndex = 0; receiptIndex < comparisonReceipts.Length; receiptIndex++)
            {
                Check(
                    PrimeReceiptCalculator.VerifyIntegrity(comparisonReceipts[receiptIndex]),
                    $"comparison-receipt-integrity:{comparison.Id}:{receiptIndex}",
                    ref checks,
                    failures);
            }
            if (comparison.Operation == "ADD")
            {
                Check(comparison.PrimePath.Work.MagnitudeAdditions == 1, $"addition-magnitude:{comparison.Id}", ref checks, failures);
                Check(comparison.PrimePath.Work.ExponentMerges == 0, $"addition-no-merge:{comparison.Id}", ref checks, failures);
                Check(
                    comparison.PrimePath.Events.Any(item => item.Operation == "FACTOR_SUM" && item.OperationClass == "REQUIRES_FACTOR_DISCOVERY"),
                    $"addition-refactor:{comparison.Id}",
                    ref checks,
                    failures);
            }
            else
            {
                Check(
                    comparison.PrimePath.OutputReceipt.Algorithm == PrimeReceiptCalculator.CompositionAlgorithm,
                    $"multiplication-derived:{comparison.Id}",
                    ref checks,
                    failures);
                Check(
                    comparison.PrimePath.OutputReceipt.Work == PrimeReceiptWork.Zero,
                    $"multiplication-no-output-discovery:{comparison.Id}",
                    ref checks,
                    failures);
            }
        }

        return new Build003CorrectnessReceipt(
            "prime-axiom-build003-correctness-v1",
            Build003Protocol.ProtocolId,
            Build003Protocol.MasterSeed.ToString("X16", CultureInfo.InvariantCulture),
            checks,
            failures,
            new
            {
                ExhaustiveSignedMinimum = -4_096,
                ExhaustiveSignedMaximum = 4_096,
                RandomizedFactoredProducts = randomizedTrials,
                PartialBudgets = new long[] { 0, 1, 3 },
                NamedCases = namedCases.Select(namedCase => namedCase.Name).ToArray(),
                CalculatorExamples = examples.Count,
                FrozenComparisons = comparisons.Count,
                CliMaximumDecimalDigits = PrimeReceiptCalculator.DefaultCliMaxDecimalDigits,
            },
            failures.Count == 0 ? "BOUNDED_PASS" : "FAILED",
            Build003Protocol.ClaimCeiling);
    }

    private static void WriteCoverage(
        string path,
        IReadOnlyList<ArithmeticComparisonReceipt> comparisons,
        string status,
        Build003CorrectnessReceipt correctness)
    {
        Build003Protocol.WriteJson(path, new
        {
            Schema = "prime-axiom-build003-protocol-coverage-v1",
            Build003Protocol.ProtocolId,
            Build003Protocol.FrozenPlanSha256,
            Build003Protocol.BaselineCommit,
            Status = status,
            RequiredFamilies,
            RequiredFamilyEvidence = new
            {
                ExhaustiveSignedSmallDomain = "correctness.json",
                SeededFactoredProducts = "correctness.json",
                PartialBudget = "correctness.json",
                CanonicalCliGrammar = "correctness.json plus xUnit canonical-integer parser regressions",
                FrozenArithmeticComparisons = "arithmetic_comparisons.json plus correctness.json",
                DeterministicReplay = "ESTABLISHED_BY_VERIFY_BUILD003_NOT_BY_A_SINGLE_GENERATOR_INVOCATION",
            },
            FrozenComparisonIds = comparisons.Select(comparison => comparison.Id).ToArray(),
            ExactFrozenComparisonSet = HasExactFrozenComparisonSet(comparisons),
            FrozenComparisonRows = comparisons.Count,
            CorrectnessChecks = correctness.Checks,
            CorrectnessFailures = correctness.Failures.Count,
            Multiplication = Build003Protocol.MultiplicationConclusion,
            Addition = Build003Protocol.AdditionConclusion,
            GeneralLlmImprovement = "NOT_MEASURED",
            HardwareClaim = "NOT_APPLICABLE",
            ClaimCeiling = Build003Protocol.ClaimCeiling,
        });
    }

    private static void WriteReadme(
        string path,
        IReadOnlyList<ArithmeticComparisonReceipt> comparisons,
        string status,
        Build003CorrectnessReceipt correctness)
    {
        var userAddition = comparisons.Single(comparison => comparison.Id == "USER_ADD_001");
        var userMultiplication = comparisons.Single(comparison => comparison.Id == "USER_MUL_001");
        var builder = new StringBuilder();
        builder.AppendLine("# Build 003 generated evidence");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"> **Framework status: `{status}`**");
        builder.AppendLine();
        builder.AppendLine("This is deterministic software-path evidence under `PAS-BUILD003-PRIME-RECEIPT-0001`. It is not a hardware or LLM-cognition benchmark.");
        builder.AppendLine();
        builder.AppendLine("## User examples");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"- `{userAddition.Expression} = {userAddition.OrdinaryPath.ResultDecimal}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  - output receipt: `{userAddition.PrimePath.OutputReceipt.Structure}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  - path: `{userAddition.PrimePath.LocalityConclusion}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- `{userMultiplication.Expression} = {userMultiplication.OrdinaryPath.ResultDecimal}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  - output receipt: `{userMultiplication.PrimePath.OutputReceipt.Structure}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  - path: `{userMultiplication.PrimePath.LocalityConclusion}`");
        builder.AppendLine();
        builder.AppendLine("## Coverage");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"- correctness checks: {correctness.Checks.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- failures: {correctness.Failures.Count.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- frozen arithmetic rows: {comparisons.Count.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine("- wall-clock and hardware metrics: `NOT_MEASURED`");
        builder.AppendLine("- general LLM improvement: `NOT_MEASURED`");
        builder.AppendLine();
        builder.AppendLine("Regenerate with:");
        builder.AppendLine();
        builder.AppendLine("```powershell");
        builder.AppendLine("dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build003 --output results/build003");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine(Build003Protocol.ClaimCeiling);
        File.WriteAllText(path, builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), new UTF8Encoding(false));
    }

    private static void WriteManifest(
        string outputDirectory,
        string status,
        Build003CorrectnessReceipt correctness,
        IReadOnlyCollection<ArithmeticComparisonReceipt> comparisons)
    {
        var files = ExpectedFiles
            .Where(file => file != "manifest.json")
            .Select(file => new
            {
                Path = file,
                Sha256 = Build003Protocol.FileSha256(Path.Combine(outputDirectory, file)),
            })
            .ToArray();
        Build003Protocol.WriteJson(Path.Combine(outputDirectory, "manifest.json"), new
        {
            Schema = "prime-axiom-build003-manifest-v1",
            Build003Protocol.ProtocolId,
            Build003Protocol.FrozenPlanSha256,
            Build003Protocol.BaselineCommit,
            MasterSeed = Build003Protocol.MasterSeed.ToString("X16", CultureInfo.InvariantCulture),
            DefaultPolicy = new PrimeReceiptPolicy(),
            CliMaximumDecimalDigits = PrimeReceiptCalculator.DefaultCliMaxDecimalDigits,
            RuntimeContract = "net8.0",
            SdkPolicy = "8.0.423 with rollForward=latestPatch",
            CanonicalReproductionCommand =
                "dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build003 --output results/build003",
            Status = status,
            CorrectnessChecks = correctness.Checks,
            CorrectnessFailures = correctness.Failures.Count,
            FrozenComparisonRows = comparisons.Count,
            FrozenComparisonIds = comparisons.Select(comparison => comparison.Id).ToArray(),
            RequiredFamilies,
            IncludedWallClockMeasurements = false,
            Files = files,
            ClaimCeiling = Build003Protocol.ClaimCeiling,
            Notes = "manifest.json intentionally excludes itself; all generated text is LF and UTF-8 without BOM.",
        });
    }

    private static void ValidateFrozenPlan(string repositoryRoot)
    {
        var planPath = Path.Combine(repositoryRoot, "research", "build003_experiment_plan.md");
        if (!File.Exists(planPath))
        {
            throw new InvalidOperationException("The frozen Build 003 plan is missing.");
        }

        var actual = Build003Protocol.FileSha256(planPath);
        if (!string.Equals(actual, Build003Protocol.FrozenPlanSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Frozen Build 003 plan hash mismatch. Expected {Build003Protocol.FrozenPlanSha256}, found {actual}.");
        }
    }

    private static bool HasExactFrozenComparisonSet(
        IReadOnlyList<ArithmeticComparisonReceipt> comparisons) =>
        comparisons.Count == ExpectedComparisonRows &&
        comparisons.Select(comparison => comparison.Id).SequenceEqual(FrozenComparisonIds, StringComparer.Ordinal) &&
        comparisons.Select(comparison => comparison.Id).Distinct(StringComparer.Ordinal).Count() == ExpectedComparisonRows;

    private static void PrepareOutput(string outputDirectory)
    {
        if (File.Exists(outputDirectory))
        {
            throw new InvalidOperationException($"Build 003 output is a file, not a directory: {outputDirectory}");
        }

        if (Directory.Exists(outputDirectory))
        {
            var existingExpectedFiles = ExpectedFiles
                .Where(file => File.Exists(Path.Combine(outputDirectory, file)))
                .ToArray();
            if (existingExpectedFiles.Length > 0 && !IsOwnedBuild003Directory(outputDirectory))
            {
                throw new InvalidOperationException(
                    "Refusing to replace generic evidence filenames in a directory that is not marked by a Build 003 manifest.");
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

    private static void ValidateOutputLocation(string repositoryRoot, string outputDirectory)
    {
        RejectReparsePointTraversal(outputDirectory);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var root = repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(root, outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), comparison))
        {
            throw new InvalidOperationException("The repository root cannot be used as the Build 003 output directory.");
        }

        var rootPrefix = root + Path.DirectorySeparatorChar;
        if (!outputDirectory.StartsWith(rootPrefix, comparison))
        {
            return;
        }

        var committedResults = Path.Combine(root, "results", "build003");
        var artifacts = Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar;
        var hiddenArtifacts = Path.Combine(root, ".artifacts") + Path.DirectorySeparatorChar;
        if (!string.Equals(outputDirectory, committedResults, comparison) &&
            !outputDirectory.StartsWith(artifacts, comparison) &&
            !outputDirectory.StartsWith(hiddenArtifacts, comparison))
        {
            throw new InvalidOperationException(
                "Repository-local Build 003 output must be results/build003 or a descendant of artifacts/ or .artifacts/.");
        }
    }

    private static bool IsOwnedBuild003Directory(string outputDirectory)
    {
        var manifestPath = Path.Combine(outputDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("schema", out var schema) ||
                !string.Equals(schema.GetString(), "prime-axiom-build003-manifest-v1", StringComparison.Ordinal) ||
                !root.TryGetProperty("protocolId", out var protocol) ||
                !string.Equals(protocol.GetString(), Build003Protocol.ProtocolId, StringComparison.Ordinal) ||
                !root.TryGetProperty("frozenPlanSha256", out var planHash) ||
                !string.Equals(planHash.GetString(), Build003Protocol.FrozenPlanSha256, StringComparison.Ordinal) ||
                !root.TryGetProperty("files", out var files) ||
                files.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var actualInventory = Directory.EnumerateFileSystemEntries(outputDirectory)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var expectedInventory = ExpectedFiles
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (!actualInventory.SequenceEqual(expectedInventory, StringComparer.Ordinal))
            {
                return false;
            }

            var expectedManifestFiles = ExpectedFiles
                .Where(name => !string.Equals(name, "manifest.json", StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in files.EnumerateArray())
            {
                if (!file.TryGetProperty("path", out var pathProperty) ||
                    !file.TryGetProperty("sha256", out var hashProperty))
                {
                    return false;
                }

                var relativePath = pathProperty.GetString();
                var expectedHash = hashProperty.GetString();
                if (relativePath is null ||
                    expectedHash is null ||
                    !expectedManifestFiles.Contains(relativePath) ||
                    !seen.Add(relativePath) ||
                    Path.GetFileName(relativePath) != relativePath)
                {
                    return false;
                }

                var fullPath = Path.Combine(outputDirectory, relativePath);
                if (!File.Exists(fullPath) ||
                    !string.Equals(Build003Protocol.FileSha256(fullPath), expectedHash, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return seen.SetEquals(expectedManifestFiles);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return false;
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
                    $"Build 003 output may not traverse a symbolic link or junction: {current}");
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }
    }

    private static bool ValidatePrimePowers(PrimeReceipt receipt)
    {
        var previous = BigInteger.Zero;
        foreach (var factor in receipt.PrimePowers)
        {
            if (!BigInteger.TryParse(factor.PrimeDecimal, NumberStyles.None, CultureInfo.InvariantCulture, out var prime) ||
                prime <= previous || factor.Exponent <= 0 || !IsPrimeIndependent(prime))
            {
                return false;
            }

            previous = prime;
        }

        return true;
    }

    private static bool IsPrimeIndependent(BigInteger value)
    {
        if (value < 2)
        {
            return false;
        }

        if (value == 2)
        {
            return true;
        }

        if (value.IsEven)
        {
            return false;
        }

        for (var candidate = new BigInteger(3); candidate <= value / candidate; candidate += 2)
        {
            if (value % candidate == BigInteger.Zero)
            {
                return false;
            }
        }

        return true;
    }

    private static void Check(bool condition, string name, ref long checks, List<string> failures)
    {
        checks++;
        if (!condition)
        {
            failures.Add(name);
        }
    }

    private sealed class SplitMix64
    {
        private ulong _state;

        public SplitMix64(ulong seed)
        {
            _state = seed;
        }

        public ulong Next()
        {
            _state += 0x9E3779B97F4A7C15UL;
            var value = _state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}

internal sealed record Build003CorrectnessReceipt(
    string Schema,
    string ProtocolId,
    string MasterSeed,
    long Checks,
    IReadOnlyList<string> Failures,
    object Domains,
    string Status,
    string ClaimCeiling);
