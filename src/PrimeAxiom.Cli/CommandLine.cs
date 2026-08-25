using System.Globalization;
using System.Numerics;
using PrimeAxiom.Core.Circuits;
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
    }
}
