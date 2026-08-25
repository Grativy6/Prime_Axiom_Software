using System.Globalization;
using System.Numerics;
using System.Text.Json;
using PrimeAxiom.Core.Circuits;
using PrimeAxiom.Core.Calculator;
using PrimeAxiom.Core.Machine;
using PrimeAxiom.Core.Representations;

namespace PrimeAxiom.Cli;

internal static class CommandLine
{
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
            "prime-receipt" => RunPrimeReceipt(args[1..]),
            "compare-arithmetic" => RunArithmeticComparison(args[1..]),
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
        Console.WriteLine("  prime-receipt INTEGER [--max-odd-candidates N] [--format text|json]");
        Console.WriteLine("  compare-arithmetic add A B [--max-odd-candidates N] [--format text|json]");
        Console.WriteLine("  compare-arithmetic multiply A B [C ...] [--max-odd-candidates N] [--format text|json]");
    }
}
