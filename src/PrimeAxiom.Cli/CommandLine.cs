using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using PrimeAxiom.Core.Build004.Combinatorics;
using PrimeAxiom.Core.Build004.Lineage;
using PrimeAxiom.Core.Build004.Probes;
using PrimeAxiom.Core.Circuits;
using PrimeAxiom.Core.Calculator;
using PrimeAxiom.Core.Machine;
using PrimeAxiom.Core.Representations;

namespace PrimeAxiom.Cli;

internal static class CommandLine
{
    private static readonly string[] LineageDemoOccurrenceIds = ["a", "b", "c", "d"];

    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        return args[0] switch
        {
            "demo" => RunDemo(),
            "experiment" => RunExperiment(args[1..]),
            "experiment-build001" => RunBuild001Experiment(args[1..]),
            "experiment-build002" => RunBuild002Experiment(args[1..]),
            "experiment-build003" => RunBuild003Experiment(args[1..]),
            "experiment-build004" => RunBuild004Experiment(args[1..]),
            "experiment-build005" => RunBuild005Experiment(args[1..]),
            "prime-receipt" => RunPrimeReceipt(args[1..]),
            "compare-arithmetic" => RunArithmeticComparison(args[1..]),
            "binomial-receipt" => RunBinomialReceipt(args[1..]),
            "hypergeometric-receipt" => RunHypergeometricReceipt(args[1..]),
            "lineage-demo" => RunLineageDemo(args[1..]),
            "render-just-interval" => RunJustIntervalRenderer(args[1..]),
            _ => Unknown(args[0]),
        };
    }

    private static int RunDemo()
    {
        var binaryTwelve = BinaryWord.FromUnsigned(12, 8);
        var basis = PrimeBasis.First(8);
        var twelve = PrimeCoordinates.Encode(12, basis, exponentWidth: 8).Value!;
        var eighteen = PrimeCoordinates.Encode(18, basis, exponentWidth: 8).Value!;
        var product = twelve.Compose(eighteen);
        var sum = twelve.AddViaMagnitudeAndRefactor(eighteen);

        Console.WriteLine("Shared lower substrate: BitState -> NAND -> latch/register -> representation");
        Console.WriteLine($"ordinary magnitude 12: {binaryTwelve}");
        Console.WriteLine($"prime coordinates 12: {twelve}");
        Console.WriteLine(
            $"COMPOSE 12 x 18: {product.Value} = {product.Value!.Reconstruct().Value}; " +
            $"{product.Receipt.Cost.Gates.NandEvaluations.ToString(CultureInfo.InvariantCulture)} NAND evaluations");
        Console.WriteLine(
            $"ADD_REFACTOR 12 + 18: {sum.Value} = {sum.Value!.Reconstruct().Value}; " +
            $"magnitude crossing={sum.Receipt.UsedMagnitudeDomain}, " +
            $"trial remainders={sum.Receipt.Cost.TrialRemainders.ToString(CultureInfo.InvariantCulture)}");

        var vm = new PrimeMachine(basis, exponentWidth: 8);
        var receipt = vm.Run(new PrimeProgram(new PrimeInstruction[]
        {
            new LoadCertifiedCoordinates(0, new BigInteger[] { 2, 1, 0, 0, 0, 0, 0, 0 }, true),
            new LoadMagnitude(1, 18),
            new Compose(2, 0, 1),
            new ReconstructMagnitude(2),
        }));
        Console.WriteLine($"VM result: {receipt.LastScalar}; completed={receipt.Completed}");
        return 0;
    }

    private static int RunExperiment(string[] args)
    {
        var output = "results/build000";
        var skipBenchmarks = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--output" when index + 1 < args.Length:
                    output = args[++index];
                    break;
                case "--skip-benchmarks":
                    skipBenchmarks = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown experiment option: {args[index]}");
                    return 2;
            }
        }

        var receipt = ExperimentRunner.Run(
            Path.GetFullPath(output),
            includeBenchmarks: !skipBenchmarks,
            invocationOutputArgument: output);
        Console.WriteLine($"Wrote Build 000 evidence to {receipt.OutputDirectory}");
        Console.WriteLine(
            $"Checks: {receipt.CheckCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"failures: {receipt.FailureCount.ToString(CultureInfo.InvariantCulture)}");
        return receipt.FailureCount == 0 ? 0 : 1;
    }

    private static int RunBuild001Experiment(string[] args)
    {
        var output = "results/build001";
        var skipBenchmarks = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--output" when index + 1 < args.Length:
                    output = args[++index];
                    break;
                case "--skip-benchmarks":
                    skipBenchmarks = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown Build 001 experiment option: {args[index]}");
                    return 2;
            }
        }

        var receipt = Build001ExperimentRunner.Run(
            Path.GetFullPath(output),
            includeBenchmarks: !skipBenchmarks,
            invocationOutputArgument: output);
        Console.WriteLine($"Wrote Build 001 evidence to {receipt.OutputDirectory}");
        Console.WriteLine(
            $"Checks: {receipt.CheckCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"failures: {receipt.FailureCount.ToString(CultureInfo.InvariantCulture)}");
        return receipt.FailureCount == 0 ? 0 : 1;
    }

    private static int RunBuild002Experiment(string[] args)
    {
        var output = "results/build002";
        string? hdlVerificationSummary = null;
        string? hdlSynthesisMetrics = null;
        string? hdlToolchainBootstrap = null;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--output" when index + 1 < args.Length:
                    output = args[++index];
                    break;
                case "--hdl-verification-summary" when index + 1 < args.Length:
                    hdlVerificationSummary = args[++index];
                    break;
                case "--hdl-synthesis-metrics" when index + 1 < args.Length:
                    hdlSynthesisMetrics = args[++index];
                    break;
                case "--hdl-toolchain" when index + 1 < args.Length:
                    hdlToolchainBootstrap = args[++index];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown Build 002 experiment option: {args[index]}");
                    return 2;
            }
        }

        var repositoryRoot = Directory.GetCurrentDirectory();
        var receipt = Build002ExperimentRunner.Run(
            repositoryRoot,
            Path.GetFullPath(output),
            output,
            hdlVerificationSummary,
            hdlSynthesisMetrics,
            hdlToolchainBootstrap);
        Console.WriteLine($"Wrote Build 002 evidence to {receipt.OutputDirectory}");
        Console.WriteLine(
            $"Checks: {receipt.CheckCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"failures: {receipt.FailureCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"classification: {receipt.Classification}");
        return receipt.FailureCount == 0 ? 0 : 1;
    }

    private static int RunBuild003Experiment(string[] args)
    {
        var output = "results/build003";
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--output" when index + 1 < args.Length:
                    output = args[++index];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown Build 003 experiment option: {args[index]}");
                    return 2;
            }
        }

        Build003RunReceipt receipt;
        try
        {
            receipt = Build003ExperimentRunner.Run(
                Directory.GetCurrentDirectory(),
                Path.GetFullPath(output));
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        Console.WriteLine($"Wrote Build 003 evidence to {receipt.OutputDirectory}");
        Console.WriteLine(
            $"Checks: {receipt.CheckCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"failures: {receipt.FailureCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"status: {receipt.Status}");
        return receipt.FailureCount == 0 &&
            string.Equals(receipt.Status, Build003Protocol.FrameworkStatus, StringComparison.Ordinal)
            ? 0
            : 1;
    }

    private static int RunPrimeReceipt(string[] args)
    {
        if (!TryParseCalculatorArguments(args, out var positionals, out var policy, out var json, out var error))
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        if (positionals.Count != 1)
        {
            Console.Error.WriteLine("Usage: prime-receipt INTEGER [--max-odd-candidates N] [--format text|json]");
            return 2;
        }

        if (!TryParseCalculatorInteger(positionals[0], out var value))
        {
            return 2;
        }

        var receipt = PrimeReceiptCalculator.Analyze(value, policy);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(receipt, Build003Protocol.JsonOptions));
        }
        else
        {
            PrintPrimeReceipt(receipt);
        }

        return 0;
    }

    private static int RunBuild004Experiment(string[] args)
    {
        var output = "results/build004";
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--output" when index + 1 < args.Length:
                    output = args[++index];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown Build 004 experiment option: {args[index]}");
                    return 2;
            }
        }

        try
        {
            var receipt = Build004ExperimentRunner.Run(
                Directory.GetCurrentDirectory(),
                Path.GetFullPath(output));
            Console.WriteLine($"Wrote Build 004 evidence to {receipt.OutputDirectory}");
            Console.WriteLine(
                 $"Checks: {receipt.CheckCount.ToString(CultureInfo.InvariantCulture)}; " +
                 $"failures: {receipt.FailureCount.ToString(CultureInfo.InvariantCulture)}; " +
                 $"generated status: {receipt.Status}; " +
                 $"candidate after external verification: {receipt.CandidateFrameworkStatus}");
            return receipt.FailureCount == 0 &&
                receipt.Status == Build004Protocol.PartialStatus &&
                receipt.CandidateFrameworkStatus == Build004Protocol.FrameworkStatus
                ? 0
                : 1;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static int RunBuild005Experiment(string[] args)
    {
        var output = "results/build005";
        string? environmentReceipt = null;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--output" when index + 1 < args.Length:
                    output = args[++index];
                    break;
                case "--environment-receipt" when index + 1 < args.Length:
                    environmentReceipt = args[++index];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown Build 005 experiment option: {args[index]}");
                    return 2;
            }
        }

        try
        {
            var fullOutput = Path.GetFullPath(output);
            var fullEnvironmentReceipt = environmentReceipt is null
                ? null
                : Path.GetFullPath(environmentReceipt);
            if (fullEnvironmentReceipt is not null &&
                IsPathInsideDirectory(fullEnvironmentReceipt, fullOutput))
            {
                throw new InvalidOperationException(
                    "The verifier-owned generator-environment receipt must stay outside the deterministic Build 005 result directory.");
            }

            var receipt = Build005ExperimentRunner.Run(
                Directory.GetCurrentDirectory(),
                fullOutput);
            if (fullEnvironmentReceipt is not null)
            {
                Build005GeneratorEnvironment.WriteNew(fullEnvironmentReceipt);
            }
            Console.WriteLine($"Wrote Build 005 evidence to {receipt.OutputDirectory}");
            Console.WriteLine(
                $"Checks: {receipt.CheckCount.ToString(CultureInfo.InvariantCulture)}; " +
                $"failures: {receipt.FailureCount.ToString(CultureInfo.InvariantCulture)}; " +
                $"generated status: {receipt.GeneratedStatus}; " +
                $"candidate after external verification: {receipt.CandidateTerminalLabel}");
            Console.WriteLine($"search policy: {receipt.SearchPolicy}");
            Console.WriteLine($"attribution: {receipt.Attribution}");
            Console.WriteLine($"boundary: {receipt.EvidenceBoundary}");
            return receipt.FailureCount == 0 &&
                string.Equals(
                    receipt.GeneratedStatus,
                    Build005Protocol.PartialStatus,
                    StringComparison.Ordinal)
                ? 0
                : 1;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedDirectory = directory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return string.Equals(path, normalizedDirectory, comparison) ||
            path.StartsWith(normalizedDirectory + Path.DirectorySeparatorChar, comparison);
    }

    private static int RunBinomialReceipt(string[] args)
    {
        if (args.Length != 2 ||
            !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ||
            !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var k) ||
            n < 0 || k < 0 || k > n || n > 1_000_000)
        {
            Console.Error.WriteLine("Usage: binomial-receipt N K (0 <= K <= N <= 1000000)");
            return 2;
        }

        var receipt = new PrimeCombinatorics(n).Binomial(n, k);
        Console.WriteLine(JsonSerializer.Serialize(receipt, Build004Protocol.JsonOptions));
        return 0;
    }

    private static int RunHypergeometricReceipt(string[] args)
    {
        if (args.Length != 4 ||
            !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out var population) ||
            !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var successes) ||
            !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var draws) ||
            !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var observed) ||
            population < 0 || population > 1_000_000 ||
            successes < 0 || successes > population ||
            draws < 0 || draws > population)
        {
            Console.Error.WriteLine("Usage: hypergeometric-receipt POPULATION SUCCESS_STATES DRAWS OBSERVED (population <= 1000000)");
            return 2;
        }

        var receipt = new PrimeCombinatorics(population).HypergeometricPoint(
            population,
            successes,
            draws,
            observed);
        Console.WriteLine(JsonSerializer.Serialize(receipt, Build004Protocol.JsonOptions));
        return 0;
    }

    private static int RunLineageDemo(string[] args)
    {
        if (args.Length != 0)
        {
            Console.Error.WriteLine("Usage: lineage-demo");
            return 2;
        }

        var registry = LineageRegistry.CreateSequential(
            "cli-demo",
            "epoch-1",
            LineageDemoOccurrenceIds);
        var descriptors = registry.Registrations.Select((registration, index) => new AtomDescriptor(
            registration.Key,
            $"source-{index.ToString(CultureInfo.InvariantCulture)}",
            Build004Protocol.BytesSha256(Encoding.UTF8.GetBytes($"payload-{index.ToString(CultureInfo.InvariantCulture)}")))).ToArray();
        var dag = new DerivationDag();
        var nodes = descriptors.Select(dag.AddAtom).ToArray();
        var first = dag.AddAlternative(dag.AddJoint(nodes[0], nodes[1]), dag.AddJoint(nodes[2], nodes[3]));
        var second = dag.AddAlternative(dag.AddJoint(nodes[0], nodes[2]), dag.AddJoint(nodes[1], nodes[3]));
        var firstSupport = dag.ProjectSupport(first, registry);
        var secondSupport = dag.ProjectSupport(second, registry);
        var firstMultiplicity = dag.ProjectMultiplicity(first, registry);
        var secondMultiplicity = dag.ProjectMultiplicity(second, registry);

        Console.WriteLine("first:  a*b + c*d");
        Console.WriteLine($"root:   {first}");
        Console.WriteLine("second: a*c + b*d");
        Console.WriteLine($"root:   {second}");
        Console.WriteLine($"same source support: {firstSupport.Equals(secondSupport)}");
        Console.WriteLine($"same source multiplicity: {firstMultiplicity.Equals(secondMultiplicity)}");
        Console.WriteLine($"same derivation root: {first == second}");
        Console.WriteLine("PEV/set support loses the pairing; the retained DAG does not.");
        return 0;
    }

    private static int RunJustIntervalRenderer(string[] args)
    {
        if (args.Length != 4 ||
            !BigInteger.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out var numerator) ||
            !BigInteger.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var denominator) ||
            !BigInteger.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var baseHertz) ||
            numerator <= 0 || denominator <= 0 || baseHertz <= 0)
        {
            Console.Error.WriteLine("Usage: render-just-interval NUMERATOR DENOMINATOR BASE_HZ OUTPUT.wav");
            return 2;
        }

        var output = Path.GetFullPath(args[3]);
        if (File.Exists(output))
        {
            Console.Error.WriteLine($"Refusing to overwrite existing file: {output}");
            return 2;
        }

        var interval = ProbeJustIntervalReceipt.FromRatio(
            "cli-interval",
            "cli-supplied-ratio",
            new ProbeExactRatio(numerator, denominator));
        var policy = new ProbeAudioApproximationPolicy(
            sampleRate: 48_000,
            sampleCount: 48_000,
            phaseRadians: 0,
            peakAmplitude: 0.25,
            linearAttackSamples: 480,
            linearReleaseSamples: 480);
        ProbePcmWaveReceipt receipt;
        try
        {
            receipt = ProbePcmWaveRenderer.RenderSine(
                "cli-render",
                interval,
                new ProbeExactRatio(baseHertz, 1),
                policy);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }

        var parent = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllBytes(output, receipt.GetWavBytes());
        Console.WriteLine($"Wrote {receipt.WavByteLength.ToString(CultureInfo.InvariantCulture)} bytes to {output}");
        Console.WriteLine($"exact nominal frequency: {receipt.NominalFrequencyHertz}");
        Console.WriteLine($"rendered binary64 frequency: {receipt.RenderedFrequencyHertz.ToString("R", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"SHA-256: {receipt.WavSha256}");
        return 0;
    }

    private static int RunArithmeticComparison(string[] args)
    {
        if (!TryParseCalculatorArguments(args, out var positionals, out var policy, out var json, out var error))
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        if (positionals.Count < 3)
        {
            Console.Error.WriteLine(
                "Usage: compare-arithmetic add A B | compare-arithmetic multiply A B [C ...] [--max-odd-candidates N] [--format text|json]");
            return 2;
        }

        var operation = positionals[0];
        var operands = new List<BigInteger>(positionals.Count - 1);
        foreach (var text in positionals.Skip(1))
        {
            if (!TryParseCalculatorInteger(text, out var value))
            {
                return 2;
            }

            operands.Add(value);
        }

        ArithmeticComparisonReceipt comparison;
        try
        {
            comparison = operation switch
            {
                "add" when operands.Count == 2 => PrimeArithmeticComparison.CompareAddition(
                    "CLI_ADD",
                    operands[0],
                    operands[1],
                    policy),
                "multiply" when operands.Count >= 2 => PrimeArithmeticComparison.CompareMultiplication(
                    "CLI_MULTIPLY",
                    operands,
                    policy),
                "add" => throw new ArgumentException("Addition requires exactly two operands."),
                "multiply" => throw new ArgumentException("Multiplication requires at least two operands."),
                _ => throw new ArgumentException("Operation must be 'add' or 'multiply'."),
            };
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(comparison, Build003Protocol.JsonOptions));
        }
        else
        {
            PrintArithmeticComparison(comparison);
        }

        return comparison.PathsAgree ? 0 : 1;
    }

    private static bool TryParseCalculatorArguments(
        IReadOnlyList<string> args,
        out List<string> positionals,
        out PrimeReceiptPolicy policy,
        out bool json,
        out string error)
    {
        positionals = new List<string>();
        var maxOddCandidates = PrimeReceiptPolicy.DefaultMaxOddCandidates;
        json = false;
        error = string.Empty;
        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--max-odd-candidates" when index + 1 < args.Count:
                    if (!long.TryParse(args[++index], NumberStyles.None, CultureInfo.InvariantCulture, out maxOddCandidates) ||
                        maxOddCandidates < 0)
                    {
                        policy = new PrimeReceiptPolicy();
                        error = "--max-odd-candidates requires a nonnegative Int64 value.";
                        return false;
                    }

                    break;
                case "--format" when index + 1 < args.Count:
                    var format = args[++index];
                    if (format == "json")
                    {
                        json = true;
                    }
                    else if (format == "text")
                    {
                        json = false;
                    }
                    else
                    {
                        policy = new PrimeReceiptPolicy();
                        error = "--format must be 'text' or 'json'.";
                        return false;
                    }

                    break;
                case var option when option.StartsWith("--", StringComparison.Ordinal):
                    policy = new PrimeReceiptPolicy();
                    error = $"Unknown calculator option: {option}";
                    return false;
                default:
                    positionals.Add(args[index]);
                    break;
            }
        }

        policy = new PrimeReceiptPolicy(maxOddCandidates);
        return true;
    }

    private static bool TryParseCalculatorInteger(string text, out BigInteger value)
    {
        if (PrimeReceiptCalculator.TryParseCanonicalInteger(
            text,
            PrimeReceiptCalculator.DefaultCliMaxDecimalDigits,
            out value,
            out var error))
        {
            return true;
        }

        Console.Error.WriteLine($"Invalid integer '{text}': {error}");
        return false;
    }

    private static void PrintPrimeReceipt(PrimeReceipt receipt)
    {
        Console.WriteLine($"input: {receipt.CanonicalInputDecimal}");
        Console.WriteLine($"status: {receipt.Status}");
        Console.WriteLine($"prime structure: {receipt.Structure}");
        Console.WriteLine($"unresolved cofactor: {receipt.UnresolvedCofactorDecimal}");
        Console.WriteLine($"reconstruction verified: {receipt.ReconstructionVerified}");
        Console.WriteLine(
            "work: " +
            $"radix extractions={receipt.Work.RadixExtractions.ToString(CultureInfo.InvariantCulture)}, " +
            $"odd candidates={receipt.Work.OddCandidatesExamined.ToString(CultureInfo.InvariantCulture)}, " +
            $"remainder checks={receipt.Work.RemainderChecks.ToString(CultureInfo.InvariantCulture)}, " +
            $"factor divisions={receipt.Work.ExactFactorDivisions.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"receipt id: {receipt.ReceiptId}");
        Console.WriteLine($"claim ceiling: {receipt.ClaimCeiling}");
    }

    private static void PrintArithmeticComparison(ArithmeticComparisonReceipt comparison)
    {
        Console.WriteLine($"problem: {comparison.Expression}");
        Console.WriteLine($"answer: {comparison.OrdinaryPath.ResultDecimal}");
        Console.WriteLine(
            $"ordinary visible path: {comparison.OrdinaryPath.Strategy}/{comparison.OrdinaryPath.Algorithm}; " +
            $"columns={comparison.OrdinaryPath.Work.DecimalColumns.ToString(CultureInfo.InvariantCulture)}; " +
            $"carries={comparison.OrdinaryPath.Work.CarryEvents.ToString(CultureInfo.InvariantCulture)}; " +
            $"magnitude multiplications={comparison.OrdinaryPath.Work.SequentialMagnitudeMultiplications.ToString(CultureInfo.InvariantCulture)}");
        if (comparison.OrdinaryPath.MultiplicationSteps.Count > 0)
        {
            Console.WriteLine(
                "ordinary intermediates: " +
                string.Join(" -> ", comparison.OrdinaryPath.MultiplicationSteps.Select(step => step.AccumulatorAfterDecimal)));
        }

        Console.WriteLine($"prime receipt: {comparison.PrimePath.OutputReceipt.Structure}");
        Console.WriteLine($"prime path: {comparison.PrimePath.LocalityConclusion}");
        Console.WriteLine(
            "prime-path work: " +
            $"factor calls={comparison.PrimePath.Work.FactorizationCalls.ToString(CultureInfo.InvariantCulture)}, " +
            $"odd candidates={comparison.PrimePath.Work.OddCandidatesExamined.ToString(CultureInfo.InvariantCulture)}, " +
            $"factor divisions={comparison.PrimePath.Work.ExactFactorDivisions.ToString(CultureInfo.InvariantCulture)}, " +
            $"exponent merges={comparison.PrimePath.Work.ExponentMerges.ToString(CultureInfo.InvariantCulture)}, " +
            $"reconstructions={comparison.PrimePath.Work.Reconstructions.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"paths agree: {comparison.PathsAgree}");
        Console.WriteLine($"boundary: {comparison.AiComparisonBoundary}");
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Prime Axiom Software");
        Console.WriteLine("  demo");
        Console.WriteLine("  experiment [--output DIRECTORY] [--skip-benchmarks]");
        Console.WriteLine("  experiment-build001 [--output DIRECTORY] [--skip-benchmarks]");
        Console.WriteLine("  experiment-build002 [--output DIRECTORY] [--hdl-verification-summary FILE] [--hdl-synthesis-metrics FILE] [--hdl-toolchain FILE]");
        Console.WriteLine("  experiment-build003 [--output DIRECTORY]");
        Console.WriteLine("  experiment-build004 [--output DIRECTORY]");
        Console.WriteLine("  experiment-build005 [--output DIRECTORY] [--environment-receipt FILE]");
        Console.WriteLine("  prime-receipt INTEGER [--max-odd-candidates N] [--format text|json]");
        Console.WriteLine("  compare-arithmetic add A B [--max-odd-candidates N] [--format text|json]");
        Console.WriteLine("  compare-arithmetic multiply A B [C ...] [--max-odd-candidates N] [--format text|json]");
        Console.WriteLine("  binomial-receipt N K");
        Console.WriteLine("  hypergeometric-receipt POPULATION SUCCESS_STATES DRAWS OBSERVED");
        Console.WriteLine("  lineage-demo");
        Console.WriteLine("  render-just-interval NUMERATOR DENOMINATOR BASE_HZ OUTPUT.wav");
    }
}
