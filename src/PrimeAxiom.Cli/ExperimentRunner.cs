using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PrimeAxiom.Core.Circuits;
using PrimeAxiom.Core.Representations;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Cli;

internal sealed record ExperimentRunReceipt(string OutputDirectory, long CheckCount, long FailureCount);

internal static class ExperimentRunner
{
    private const int Seed = 0x5EED000;
    private const int ResidentWorkloadSeed = Seed ^ 0x51DE;
    private const int AdditionDisruptionSeed = Seed ^ 0xADD;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static object? _benchmarkSink;

    public static ExperimentRunReceipt Run(
        string outputDirectory,
        bool includeBenchmarks,
        string invocationOutputArgument)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationOutputArgument);
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(Path.Combine(outputDirectory, "figures"));

        var correctness = RunCorrectnessChecks();
        WriteJson(Path.Combine(outputDirectory, "correctness.json"), correctness);

        var fairDomain = BuildFairDomainCosts();
        WriteCsv(Path.Combine(outputDirectory, "fair_domain_costs.csv"), fairDomain.Headers, fairDomain.Rows);
        WriteFairDomainSvg(Path.Combine(outputDirectory, "figures", "fair_domain_gate_counts.svg"), fairDomain.Rows);

        var representations = BuildRepresentationCases();
        WriteCsv(Path.Combine(outputDirectory, "representation_cases.csv"), representations.Headers, representations.Rows);

        var resident = BuildResidentWorkloads();
        WriteCsv(Path.Combine(outputDirectory, "resident_multiplication_workloads.csv"), resident.Headers, resident.Rows);

        var disruption = BuildAdditionDisruption();
        WriteCsv(Path.Combine(outputDirectory, "addition_disruption.csv"), disruption.Headers, disruption.Rows);

        var reversibility = BuildPreimageCounts();
        WriteCsv(Path.Combine(outputDirectory, "operation_preimages.csv"), reversibility.Headers, reversibility.Rows);

        var microbenchmarkPath = Path.Combine(outputDirectory, "microbenchmarks.csv");
        if (includeBenchmarks)
        {
            var benchmarks = RunMicrobenchmarks();
            WriteCsv(microbenchmarkPath, benchmarks.Headers, benchmarks.Rows);
        }
        else
        {
            // A no-benchmark run must not leave a receipt from an earlier host/run
            // that the new manifest could be mistaken as claiming.
            File.Delete(microbenchmarkPath);
        }

        WriteManifest(outputDirectory, invocationOutputArgument, includeBenchmarks, correctness);
        return new ExperimentRunReceipt(outputDirectory, correctness.CheckCount, correctness.Failures.Count);
    }

    private static CorrectnessReceipt RunCorrectnessChecks()
    {
        long checks = 0;
        var failures = new List<string>();

        foreach (var left in new[] { BitState.Off, BitState.On })
        {
            foreach (var right in new[] { BitState.Off, BitState.On })
            {
                var network = new GateNetwork();
                var actual = network.Nand(network.Input(left), network.Input(right)).State;
                var expected = BitStateExtensions.FromBoolean(!(left.ToBoolean() && right.ToBoolean()));
                Check(actual == expected, $"NAND({left},{right})", ref checks, failures);
            }
        }

        const int width = 4;
        for (var left = 0; left < (1 << width); left++)
        {
            for (var right = 0; right < (1 << width); right++)
            {
                var a = BinaryWord.FromUnsigned(left, width);
                var b = BinaryWord.FromUnsigned(right, width);
                var add = BinaryCircuit.Add(a, b);
                Check(
                    add.Value.ToUnsigned() == (left + right) % (1 << width) &&
                    add.Carry.ToBoolean() == (left + right >= (1 << width)),
                    $"ADD({left},{right})",
                    ref checks,
                    failures);

                var subtract = BinaryCircuit.Subtract(a, b);
                var expectedDifference = (left - right + (1 << width)) % (1 << width);
                Check(
                    subtract.Value.ToUnsigned() == expectedDifference &&
                    subtract.Borrow.ToBoolean() == (left < right),
                    $"SUB({left},{right})",
                    ref checks,
                    failures);

                var compare = BinaryCircuit.Compare(a, b);
                Check(
                    compare.Less.ToBoolean() == (left < right) &&
                    compare.Equal.ToBoolean() == (left == right) &&
                    compare.Greater.ToBoolean() == (left > right),
                    $"COMPARE({left},{right})",
                    ref checks,
                    failures);

                var multiply = BinaryCircuit.Multiply(a, b);
                Check(multiply.Value.ToUnsigned() == left * right, $"MUL({left},{right})", ref checks, failures);
            }
        }

        var basis = new PrimeBasis(PrimesThrough(127));
        const int exponentWidth = 8;
        for (var value = 1; value <= 128; value++)
        {
            var encoded = PrimeCoordinates.Encode(value, basis, exponentWidth);
            Check(encoded.Value is not null, $"ENCODE({value})", ref checks, failures);
            if (encoded.Value is not null)
            {
                Check(encoded.Value.Reconstruct().Value == value, $"ROUNDTRIP({value})", ref checks, failures);
            }
        }

        for (var left = 1; left <= 64; left++)
        {
            var a = PrimeCoordinates.Encode(left, basis, exponentWidth).Value!;
            for (var right = 1; right <= 64; right++)
            {
                var b = PrimeCoordinates.Encode(right, basis, exponentWidth).Value!;
                Check(a.Compose(b).Value!.Reconstruct().Value == left * right, $"COMPOSE({left},{right})", ref checks, failures);
                Check(a.GreatestCommonDivisor(b).Value!.Reconstruct().Value == BigInteger.GreatestCommonDivisor(left, right), $"GCD({left},{right})", ref checks, failures);
                var expectedLcm = left / BigInteger.GreatestCommonDivisor(left, right) * right;
                Check(a.LeastCommonMultiple(b).Value!.Reconstruct().Value == expectedLcm, $"LCM({left},{right})", ref checks, failures);
                Check(a.Divides(b).Divides == (right % left == 0), $"DIVIDES({left},{right})", ref checks, failures);

                var cancel = a.Cancel(b);
                Check(
                    cancel.Receipt.Succeeded == (left % right == 0) &&
                    (!cancel.Receipt.Succeeded || cancel.Value!.Reconstruct().Value == left / right),
                    $"CANCEL({left},{right})",
                    ref checks,
                    failures);
            }
        }

        var random = new Random(Seed);
        for (var trial = 0; trial < 5_000; trial++)
        {
            var left = random.Next(1, 128);
            var right = random.Next(1, 128);
            var a = PrimeCoordinates.Encode(left, basis, exponentWidth).Value!;
            var b = PrimeCoordinates.Encode(right, basis, exponentWidth).Value!;
            Check(a.Compose(b).Value!.Reconstruct().Value == left * right, $"RANDOM_COMPOSE({trial})", ref checks, failures);
        }

        return new CorrectnessReceipt(
            "PASS means exact only for the enumerated and seeded domains below.",
            checks,
            failures,
            new
            {
                GateTruthRows = 4,
                BinaryWidth = width,
                BinaryOrderedPairs = 256,
                PrimeRoundTripRange = "1..128",
                PrimeOrderedPairRange = "1..64 x 1..64",
                RandomSeed = Seed,
                RandomTrials = 5_000,
            });
    }

    private static CsvTable BuildFairDomainCosts()
    {
        var headers = new[]
        {
            "max_input", "binary_input_bits", "binary_multiplier_nands", "binary_multiplier_depth",
            "prime_lanes", "prime_exponent_bits", "prime_dense_payload_bits", "prime_compose_nands",
            "prime_compose_depth", "basis_self_description_bits", "sparse_one_factor_bits",
        };
        var rows = new List<string[]>();
        foreach (var bound in new[] { 16, 32, 64, 128, 256, 512, 1_024, 2_048, 4_096 })
        {
            var binaryWidth = CeilLog2(bound + 1);
            var binaryMultiply = BinaryCircuit.Multiply(BinaryWord.Zero(binaryWidth), BinaryWord.Zero(binaryWidth));
            var primeList = PrimesThrough(bound);
            var basis = new PrimeBasis(primeList);
            var maximumProductExponent = 2 * FloorLog2(bound);
            var exponentWidth = CeilLog2(maximumProductExponent + 1);
            var identity = PrimeCoordinates.Identity(basis, exponentWidth);
            var compose = identity.Compose(identity);
            var catalogBits = primeList.Sum(prime => CeilLog2(prime + 1));
            var indexBits = CeilLog2(primeList.Length);
            var lengthBits = CeilLog2(primeList.Length + 1);
            rows.Add(ToRow(
                bound,
                binaryWidth,
                binaryMultiply.Cost.NandEvaluations,
                binaryMultiply.Cost.CriticalPathDepth,
                primeList.Length,
                exponentWidth,
                identity.DensePayloadBits,
                compose.Receipt.Cost.Gates.NandEvaluations,
                compose.Receipt.Cost.Gates.CriticalPathDepth,
                catalogBits,
                indexBits + exponentWidth + lengthBits));
        }

        return new CsvTable(headers, rows);
    }

    private static CsvTable BuildRepresentationCases()
    {
        var headers = new[]
        {
            "magnitude", "class", "binary_bits", "encode_status", "failure", "basis_residual",
            "dense_payload_bits", "sparse_payload_bits", "nonzero_lanes", "trial_remainders", "factor_divisions",
        };
        var basis = PrimeBasis.First(25);
        const int exponentWidth = 8;
        var indexWidth = CeilLog2(basis.Count);
        var lengthWidth = CeilLog2(basis.Count + 1);
        var cases = new (BigInteger Magnitude, string Class)[]
        {
            (1, "unit"), (2, "prime-in-basis"), (4, "prime-power"), (12, "smooth"),
            (30, "square-free-smooth"), (64, "prime-power"), (97, "largest-basis-prime"),
            (101, "prime-basis-escape"), (210, "smooth"), (2_310, "primorial"),
            (30_030, "primorial"), (99_991, "large-prime"), (104_729, "10000th-prime"),
        };

        var rows = new List<string[]>();
        foreach (var (magnitude, @class) in cases)
        {
            var encoded = PrimeCoordinates.Encode(magnitude, basis, exponentWidth);
            var sparseBits = string.Empty;
            var nonzero = string.Empty;
            var denseBits = checked((long)basis.Count * exponentWidth).ToString(CultureInfo.InvariantCulture);
            if (encoded.Value is not null)
            {
                var sparse = SparsePrimeCoordinates.FromDense(encoded.Value);
                sparseBits = sparse.PayloadBits(indexWidth, exponentWidth, lengthWidth).ToString(CultureInfo.InvariantCulture);
                nonzero = sparse.NonzeroLaneCount.ToString(CultureInfo.InvariantCulture);
            }

            rows.Add(new[]
            {
                magnitude.ToString(CultureInfo.InvariantCulture), @class,
                magnitude.GetBitLength().ToString(CultureInfo.InvariantCulture),
                encoded.Receipt.Succeeded ? "success" : "failure", encoded.Receipt.Failure.ToString(),
                encoded.UnrepresentedResidual.ToString(CultureInfo.InvariantCulture), denseBits, sparseBits, nonzero,
                encoded.Receipt.Cost.TrialRemainders.ToString(CultureInfo.InvariantCulture),
                encoded.Receipt.Cost.FactorDivisions.ToString(CultureInfo.InvariantCulture),
            });
        }

        return new CsvTable(headers, rows);
    }

    private static CsvTable BuildResidentWorkloads()
    {
        var headers = new[]
        {
            "case", "left_bits", "right_bits", "binary_width", "binary_payload_bits_two_operands",
            "prime_payload_bits_two_operands", "binary_multiplier_nands", "binary_multiplier_depth",
            "prime_compose_nands", "prime_compose_depth", "result_equal",
        };
        var basis = PrimeBasis.First(8);
        const int exponentWidth = 8;
        var random = new Random(ResidentWorkloadSeed);
        var rows = new List<string[]>();
        for (var sample = 0; sample < 64; sample++)
        {
            var left = CoordinatesFromRandomExponents(basis, exponentWidth, random, 4);
            var right = CoordinatesFromRandomExponents(basis, exponentWidth, random, 4);
            var leftMagnitude = left.Reconstruct().Value;
            var rightMagnitude = right.Reconstruct().Value;
            var binaryWidth = checked((int)Math.Max(leftMagnitude.GetBitLength(), rightMagnitude.GetBitLength()));
            binaryWidth = Math.Max(1, binaryWidth);
            var binary = BinaryCircuit.Multiply(
                BinaryWord.FromUnsigned(leftMagnitude, binaryWidth),
                BinaryWord.FromUnsigned(rightMagnitude, binaryWidth));
            var prime = left.Compose(right);
            rows.Add(ToRow(
                sample, leftMagnitude.GetBitLength(), rightMagnitude.GetBitLength(), binaryWidth, 2L * binaryWidth,
                2L * left.DensePayloadBits, binary.Cost.NandEvaluations, binary.Cost.CriticalPathDepth,
                prime.Receipt.Cost.Gates.NandEvaluations, prime.Receipt.Cost.Gates.CriticalPathDepth,
                binary.Value.ToUnsigned() == prime.Value!.Reconstruct().Value));
        }

        return new CsvTable(headers, rows);
    }

    private static CsvTable BuildAdditionDisruption()
    {
        var headers = new[]
        {
            "case", "left", "right", "operand_support_union", "product_support_delta",
            "sum_support", "addition_support_symmetric_difference", "addition_trial_remainders",
            "addition_factor_divisions",
        };
        var basis = new PrimeBasis(PrimesThrough(512));
        const int exponentWidth = 10;
        var random = new Random(AdditionDisruptionSeed);
        var rows = new List<string[]>();
        for (var sample = 0; sample < 256; sample++)
        {
            var leftMagnitude = random.Next(1, 257);
            var rightMagnitude = random.Next(1, 257);
            var left = PrimeCoordinates.Encode(leftMagnitude, basis, exponentWidth).Value!;
            var right = PrimeCoordinates.Encode(rightMagnitude, basis, exponentWidth).Value!;
            var product = left.Compose(right).Value!;
            var sum = PrimeCoordinates.Encode(leftMagnitude + rightMagnitude, basis, exponentWidth);
            var union = Support(left).Union(Support(right)).ToHashSet();
            var productSupport = Support(product);
            var sumSupport = Support(sum.Value!);
            rows.Add(ToRow(
                sample, leftMagnitude, rightMagnitude, union.Count, SymmetricDifferenceCount(union, productSupport),
                sumSupport.Count, SymmetricDifferenceCount(union, sumSupport),
                sum.Receipt.Cost.TrialRemainders, sum.Receipt.Cost.FactorDivisions));
        }

        return new CsvTable(headers, rows);
    }

    private static CsvTable BuildPreimageCounts()
    {
        var headers = new[]
        {
            "operation", "domain", "ordered_inputs", "distinct_outputs", "colliding_outputs",
            "maximum_preimage", "fixed_right_distinct_outputs", "interpretation",
        };
        const int maximum = 64;
        var addition = new Dictionary<int, int>();
        var multiplication = new Dictionary<int, int>();
        for (var left = 1; left <= maximum; left++)
        {
            for (var right = 1; right <= maximum; right++)
            {
                Increment(addition, left + right);
                Increment(multiplication, left * right);
            }
        }

        const int fixedRight = 37;
        var additionFixedRightOutputs = Enumerable.Range(1, maximum)
            .Select(left => left + fixedRight)
            .Distinct()
            .Count();
        var multiplicationFixedRightOutputs = Enumerable.Range(1, maximum)
            .Select(left => left * fixedRight)
            .Distinct()
            .Count();
        return new CsvTable(headers, new List<string[]>
        {
            PreimageRow("addition", addition, maximum, additionFixedRightOutputs, "Many-to-one only when operands are discarded"),
            PreimageRow("multiplication/compose", multiplication, maximum, multiplicationFixedRightOutputs, "Many-to-one only when operands are discarded"),
        });
    }

    private static CsvTable RunMicrobenchmarks()
    {
        var headers = new[]
        {
            "benchmark", "iterations_per_trial", "trials", "median_ns_per_operation", "min_ns_per_operation",
            "max_ns_per_operation", "claim_ceiling",
        };
        var rows = new List<string[]>();
        var binaryLeft = BinaryWord.FromUnsigned(43_211, 16);
        var binaryRight = BinaryWord.FromUnsigned(12_345, 16);
        var multiplyLeft = BinaryWord.FromUnsigned(173, 8);
        var multiplyRight = BinaryWord.FromUnsigned(211, 8);
        var basis = PrimeBasis.First(16);
        var denseLeft = CoordinatesFromExponent(basis, 8, 2);
        var denseRight = CoordinatesFromExponent(basis, 8, 3);
        var hostLeft = BigInteger.Pow(2, 256) - 189;
        var hostRight = BigInteger.Pow(3, 128) + 211;

        rows.Add(Measure("binary_add_gate_model_16", 10_000, () => _benchmarkSink = BinaryCircuit.Add(binaryLeft, binaryRight)));
        rows.Add(Measure("binary_multiply_gate_model_8", 1_000, () => _benchmarkSink = BinaryCircuit.Multiply(multiplyLeft, multiplyRight)));
        rows.Add(Measure("prime_compose_dense_16x8", 2_000, () => _benchmarkSink = denseLeft.Compose(denseRight)));
        rows.Add(Measure("prime_gcd_dense_16x8", 1_000, () => _benchmarkSink = denseLeft.GreatestCommonDivisor(denseRight)));
        rows.Add(Measure("prime_encode_trial_basis16", 5_000, () => _benchmarkSink = PrimeCoordinates.Encode(30_030, basis, 8)));
        rows.Add(Measure("prime_reconstruct_dense_16x8", 5_000, () => _benchmarkSink = denseLeft.Reconstruct()));
        var twelve = PrimeCoordinates.Encode(12, basis, 8).Value!;
        var eighteen = PrimeCoordinates.Encode(18, basis, 8).Value!;
        rows.Add(Measure("prime_add_refactor_12_18", 5_000, () => _benchmarkSink = twelve.AddViaMagnitudeAndRefactor(eighteen)));
        rows.Add(Measure("host_bigint_add_256x203_bits", 100_000, () => _benchmarkSink = hostLeft + hostRight));
        rows.Add(Measure("host_bigint_multiply_256x203_bits", 50_000, () => _benchmarkSink = hostLeft * hostRight));
        GC.KeepAlive(_benchmarkSink);
        return new CsvTable(headers, rows);
    }

    private static string[] Measure(string name, int iterations, Action action)
    {
        const int trials = 7;
        for (var warmup = 0; warmup < Math.Min(1_000, iterations); warmup++)
        {
            action();
        }

        var samples = new double[trials];
        for (var trial = 0; trial < trials; trial++)
        {
            var stopwatch = Stopwatch.StartNew();
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                action();
            }

            stopwatch.Stop();
            samples[trial] = stopwatch.Elapsed.TotalNanoseconds / iterations;
        }

        Array.Sort(samples);
        return new[]
        {
            name, iterations.ToString(CultureInfo.InvariantCulture), trials.ToString(CultureInfo.InvariantCulture),
            samples[trials / 2].ToString("F3", CultureInfo.InvariantCulture),
            samples[0].ToString("F3", CultureInfo.InvariantCulture),
            samples[^1].ToString("F3", CultureInfo.InvariantCulture),
            "One managed implementation on the manifest host; not hardware latency",
        };
    }

    private static void WriteManifest(
        string outputDirectory,
        string invocationOutputArgument,
        bool includedBenchmarks,
        CorrectnessReceipt correctness)
    {
        var coreAssemblyPath = typeof(PrimeCoordinates).Assembly.Location;
        var cliAssembly = typeof(ExperimentRunner).Assembly;
        var cliAssemblyPath = cliAssembly.Location;
        var buildConfiguration = cliAssembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?
            .Configuration ?? "unknown";
        var files = new List<string>
        {
            "correctness.json", "fair_domain_costs.csv", "representation_cases.csv",
            "resident_multiplication_workloads.csv", "addition_disruption.csv",
            "operation_preimages.csv", "figures/fair_domain_gate_counts.svg",
        };
        if (includedBenchmarks)
        {
            files.Add("microbenchmarks.csv");
        }

        var benchmarkArgument = includedBenchmarks ? string.Empty : " --skip-benchmarks";
        var invocationArguments = new List<string> { "experiment", "--output", invocationOutputArgument };
        if (!includedBenchmarks)
        {
            invocationArguments.Add("--skip-benchmarks");
        }

        var manifest = new
        {
            Schema = "prime-axiom-build000-experiment-manifest-v2",
            CanonicalReproductionCommand = $"dotnet run --project src/PrimeAxiom.Cli --configuration {buildConfiguration} --no-build -- experiment --output results/build000{benchmarkArgument}",
            ActualInvocationArguments = invocationArguments,
            BuildConfiguration = buildConfiguration,
            DeterministicSeed = Seed,
            RandomStreams = new
            {
                CorrectnessComposition = new
                {
                    Seed,
                    Trials = 5_000,
                    Distribution = "independent uniform integer operands with 1 <= value < 128",
                },
                FactorResident = new
                {
                    Seed = ResidentWorkloadSeed,
                    Pairs = 64,
                    Distribution = "eight independent exponent lanes per operand; each exponent uniform in {0,1,2,3}",
                },
                AdditionDisruption = new
                {
                    Seed = AdditionDisruptionSeed,
                    Pairs = 256,
                    Distribution = "independent uniform integer operands with 1 <= value <= 256",
                },
            },
            IncludedWallClockBenchmarks = includedBenchmarks,
            ClaimStatus = correctness.Failures.Count == 0 ? "BOUNDED_PASS" : "FAILED",
            ClaimCeiling = "Exact only for declared finite checks; timings describe this managed build and host.",
            Framework = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "not reported",
            LogicalProcessors = Environment.ProcessorCount,
            CoreAssembly = Path.GetFileName(coreAssemblyPath),
            CoreAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(coreAssemblyPath))),
            CliAssembly = Path.GetFileName(cliAssemblyPath),
            CliAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cliAssemblyPath))),
            Files = files,
        };
        WriteJson(Path.Combine(outputDirectory, "manifest.json"), manifest);
    }

    private static void WriteFairDomainSvg(string path, IReadOnlyList<string[]> rows)
    {
        const int width = 960;
        const int height = 560;
        const int leftMargin = 90;
        const int rightMargin = 40;
        const int topMargin = 70;
        const int bottomMargin = 90;
        var plotWidth = width - leftMargin - rightMargin;
        var plotHeight = height - topMargin - bottomMargin;
        var binary = rows.Select(row => double.Parse(row[2], CultureInfo.InvariantCulture)).ToArray();
        var prime = rows.Select(row => double.Parse(row[7], CultureInfo.InvariantCulture)).ToArray();
        var all = binary.Concat(prime).ToArray();
        var maximumLog = Math.Ceiling(Math.Log10(all.Max()));
        var minimumLog = Math.Floor(Math.Log10(all.Min()));

        double X(int index) => leftMargin + index * (plotWidth / (double)(rows.Count - 1));
        double Y(double value) => topMargin + (maximumLog - Math.Log10(value)) / (maximumLog - minimumLog) * plotHeight;
        string Points(IEnumerable<double> values) => string.Join(
            " ",
            values.Select((value, index) =>
                $"{X(index).ToString("F1", CultureInfo.InvariantCulture)},{Y(value).ToString("F1", CultureInfo.InvariantCulture)}"));

        var svg = new StringBuilder();
        svg.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"960\" height=\"560\" viewBox=\"0 0 960 560\" role=\"img\" aria-labelledby=\"title desc\">");
        svg.AppendLine("<title id=\"title\">Matched-domain logical NAND evaluation costs</title>");
        svg.AppendLine("<desc id=\"desc\">Log scale comparison of a gate-modeled binary multiplier and dense prime-coordinate composition when each must represent every input through the stated bound.</desc>");
        svg.AppendLine("<rect width=\"960\" height=\"560\" fill=\"#fbfaf7\"/>");
        svg.AppendLine("<text x=\"90\" y=\"30\" font-family=\"Segoe UI, sans-serif\" font-size=\"20\" font-weight=\"600\" fill=\"#18212b\">Matched-domain logical work (log scale)</text>");
        for (var power = (int)minimumLog; power <= (int)maximumLog; power++)
        {
            var y = Y(Math.Pow(10, power));
            svg.AppendLine(CultureInfo.InvariantCulture, $"<line x1=\"{leftMargin}\" y1=\"{y:F1}\" x2=\"{width - rightMargin}\" y2=\"{y:F1}\" stroke=\"#d9dde2\"/>");
            svg.AppendLine(CultureInfo.InvariantCulture, $"<text x=\"{leftMargin - 12}\" y=\"{y + 5:F1}\" text-anchor=\"end\" font-family=\"Consolas, monospace\" font-size=\"12\" fill=\"#4a5561\">10^{power}</text>");
        }

        svg.AppendLine(CultureInfo.InvariantCulture, $"<polyline points=\"{Points(binary)}\" fill=\"none\" stroke=\"#276fbf\" stroke-width=\"3\"/>");
        svg.AppendLine(CultureInfo.InvariantCulture, $"<polyline points=\"{Points(prime)}\" fill=\"none\" stroke=\"#c84b31\" stroke-width=\"3\"/>");
        for (var index = 0; index < rows.Count; index++)
        {
            var label = rows[index][0];
            var x = X(index);
            svg.AppendLine(CultureInfo.InvariantCulture, $"<text x=\"{x:F1}\" y=\"{height - bottomMargin + 25}\" transform=\"rotate(45 {x:F1} {height - bottomMargin + 25})\" font-family=\"Consolas, monospace\" font-size=\"11\" fill=\"#4a5561\">{label}</text>");
        }

        svg.AppendLine("<line x1=\"600\" y1=\"32\" x2=\"630\" y2=\"32\" stroke=\"#276fbf\" stroke-width=\"3\"/><text x=\"638\" y=\"37\" font-family=\"Segoe UI, sans-serif\" font-size=\"13\">binary shift-add model</text>");
        svg.AppendLine("<line x1=\"600\" y1=\"52\" x2=\"630\" y2=\"52\" stroke=\"#c84b31\" stroke-width=\"3\"/><text x=\"638\" y=\"57\" font-family=\"Segoe UI, sans-serif\" font-size=\"13\">dense coordinate compose</text>");
        svg.AppendLine("<text x=\"480\" y=\"545\" text-anchor=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"13\" fill=\"#35414c\">maximum supported input magnitude (basis contains every prime through bound)</text>");
        svg.AppendLine("</svg>");
        File.WriteAllText(path, svg.ToString(), new UTF8Encoding(false));
    }

    private static PrimeCoordinates CoordinatesFromRandomExponents(
        PrimeBasis basis,
        int exponentWidth,
        Random random,
        int exclusiveMaximum) =>
        new(
            basis,
            Enumerable.Range(0, basis.Count)
                .Select(_ => BinaryWord.FromUnsigned(random.Next(exclusiveMaximum), exponentWidth)));

    private static PrimeCoordinates CoordinatesFromExponent(PrimeBasis basis, int exponentWidth, int exponent) =>
        new(
            basis,
            Enumerable.Range(0, basis.Count)
                .Select(_ => BinaryWord.FromUnsigned(exponent, exponentWidth)));

    private static HashSet<int> Support(PrimeCoordinates value) =>
        Enumerable.Range(0, value.LaneCount)
            .Where(lane => value.ExponentAt(lane).ToUnsigned() != BigInteger.Zero)
            .ToHashSet();

    private static int SymmetricDifferenceCount(ISet<int> left, ISet<int> right)
    {
        var copy = new HashSet<int>(left);
        copy.SymmetricExceptWith(right);
        return copy.Count;
    }

    private static int[] PrimesThrough(int maximum)
    {
        var sieve = Enumerable.Repeat(true, maximum + 1).ToArray();
        sieve[0] = false;
        if (maximum >= 1)
        {
            sieve[1] = false;
        }

        for (var candidate = 2; (long)candidate * candidate <= maximum; candidate++)
        {
            if (!sieve[candidate])
            {
                continue;
            }

            for (var composite = candidate * candidate; composite <= maximum; composite += candidate)
            {
                sieve[composite] = false;
            }
        }

        return Enumerable.Range(2, Math.Max(0, maximum - 1)).Where(value => sieve[value]).ToArray();
    }

    private static int CeilLog2(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        return value <= 1 ? 1 : checked((int)Math.Ceiling(Math.Log2(value)));
    }

    private static int FloorLog2(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        return checked((int)Math.Floor(Math.Log2(value)));
    }

    private static void Check(bool condition, string label, ref long checks, List<string> failures)
    {
        checks++;
        if (!condition)
        {
            failures.Add(label);
        }
    }

    private static void Increment(Dictionary<int, int> counts, int key) =>
        counts[key] = counts.GetValueOrDefault(key) + 1;

    private static string[] PreimageRow(
        string operation,
        Dictionary<int, int> counts,
        int maximum,
        int fixedRightDistinctOutputs,
        string interpretation) =>
        ToRow(
            operation, $"ordered pairs in 1..{maximum}", maximum * maximum, counts.Count,
            counts.Count(pair => pair.Value > 1), counts.Values.Max(), fixedRightDistinctOutputs, interpretation);

    private static string[] ToRow(params object[] values) => values
        .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
        .ToArray();

    private static void WriteJson<T>(string path, T value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine, new UTF8Encoding(false));

    private static void WriteCsv(string path, IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', headers.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(EscapeCsv)));
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    private static string EscapeCsv(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;

    private sealed record CorrectnessReceipt(
        string StatusCeiling,
        long CheckCount,
        IReadOnlyList<string> Failures,
        object Domain);

    private sealed record CsvTable(IReadOnlyList<string> Headers, IReadOnlyList<string[]> Rows);
}
