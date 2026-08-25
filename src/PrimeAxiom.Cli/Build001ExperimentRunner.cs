using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrimeAxiom.Core.Hybrid;
using PrimeAxiom.Core.Machine;
using PrimeAxiom.Core.Representations;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Cli;

internal sealed record Build001ExperimentReceipt(string OutputDirectory, long CheckCount, long FailureCount);

internal static class Build001ExperimentRunner
{
    private const int Seed = 0x5EED_001;
    private const string BaselineCommit = "7792b8b2a83c95693a6db48a0ed4b153bb0808f4";
    private const string FrozenPlanSha256 = "4A79873ADCBE477944FFBE1D90AD0969AD99560AA0C014F3CB1DD8639FF9DDEF";
    private const string Build000ManifestSha256 = "2F9ECD3DA3C2887EAA3E836D543FCCD7C0FF2139DD737FFA68475A9A5BE0935D";
    private static readonly int[] BankSizes = [4, 8, 16, 32];
    private static readonly int[] ExecutedBankSizes = [0, 4, 8, 16, 32];
    private static readonly int[] ReuseCounts = [1, 2, 4, 8, 16, 32, 64, 128, 256, 1_024, 4_096];
    private static readonly int[] ResidentFactors = [2, 3, 5, 7, 11, 13, 17, 19];
    private static readonly int[] RoughPrimes = [1_009, 1_013, 1_019, 1_021, 1_031, 1_033, 1_039, 1_049];
    private static readonly string[] UnexecutedProtocolCells =
    [
        "Frozen 1,800-trace confirmation matrix",
        "FACT and BANK_CERT source-mode replay",
        "five functional widths and eight confirmation replicates",
        "frozen exhaustive semantic domains and raw-encoding/status enumeration",
        "at least 10,000 randomized cases per important cell and 256 mixed sequences of length 64",
        "executable SPARSE_FULL replay and true Build 000 sparse-operation cost replay",
        "registered SplitMix64/FNV trace namespaces; the exploratory pilot uses seeded .NET Random under the recorded runtime",
        "complete MANAGED/STRUCTURAL Pareto profiles",
        "third-party PARI/FLINT/FriCAS/GMP performance baselines",
        "hardware elaboration and synthesis",
    ];
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static object? _benchmarkSink;

    public static Build001ExperimentReceipt Run(
        string outputDirectory,
        bool includeBenchmarks,
        string invocationOutputArgument)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(Path.Combine(outputDirectory, "figures"));

        var correctness = RunCorrectness();
        WriteJson(Path.Combine(outputDirectory, "correctness.json"), correctness);

        var contractCases = BuildContractCases();
        WriteCsv(Path.Combine(outputDirectory, "representation_contract_cases.csv"), ContractHeaders, contractCases);

        var workloads = BuildWorkloadMatrix();
        var workloadMatrixPath = Path.Combine(outputDirectory, "workload_matrix.csv");
        WriteCsv(workloadMatrixPath, PilotRow.Headers, workloads.Select(row => row.ToCsv()));

        var banks = BuildBankStrategyMatrix();
        var bankStrategyMatrixPath = Path.Combine(outputDirectory, "bank_strategy_matrix.csv");
        WriteCsv(bankStrategyMatrixPath, BankRow.Headers, banks.Select(row => row.ToCsv()));

        var freshness = BuildAdditionFreshness();
        WriteCsv(Path.Combine(outputDirectory, "addition_freshness.csv"), FreshnessRow.Headers, freshness.Select(row => row.ToCsv()));

        var thrash = BuildMigrationThrash();
        WriteCsv(Path.Combine(outputDirectory, "migration_thrash.csv"), MigrationRow.Headers, thrash.Select(row => row.ToCsv()));

        var crossovers = BuildCrossovers();
        WriteCsv(Path.Combine(outputDirectory, "crossovers.csv"), CrossoverRow.Headers, crossovers.Select(row => row.ToCsv()));

        var vmTrace = BuildVmTrace();
        WriteJson(Path.Combine(outputDirectory, "vm_trace.json"), vmTrace);

        WriteCoverage(Path.Combine(outputDirectory, "protocol_coverage.json"), workloads);
        var generatorCommand = BuildGeneratorCommand(invocationOutputArgument, includeBenchmarks);
        WritePhaseCostSvg(
            Path.Combine(outputDirectory, "figures", "phase_costs.svg"),
            workloads,
            workloadMatrixPath,
            generatorCommand);
        WriteBankTradeoffSvg(
            Path.Combine(outputDirectory, "figures", "bank_tradeoffs.svg"),
            banks,
            bankStrategyMatrixPath,
            generatorCommand);

        var benchmarkPath = Path.Combine(outputDirectory, "microbenchmarks.csv");
        if (includeBenchmarks)
        {
            WriteCsv(benchmarkPath, BenchmarkHeaders, RunMicrobenchmarks());
        }
        else
        {
            File.Delete(benchmarkPath);
        }

        WriteManifest(outputDirectory, invocationOutputArgument, includeBenchmarks, correctness, workloads);
        return new Build001ExperimentReceipt(outputDirectory, correctness.CheckCount, correctness.Failures.Count);
    }

    private static CorrectnessReceipt RunCorrectness()
    {
        long checks = 0;
        var failures = new List<string>();
        foreach (var size in new[] { 0, 4, 8 })
        {
            var bank = ValuationBank.First(size);
            for (var value = -128; value <= 128; value++)
            {
                var encoded = HybridInteger.FromBinary(value, bank, 16);
                Check(encoded.Receipt.Succeeded, $"INGRESS[{size}]({value})", ref checks, failures);
                if (encoded.Value is not null)
                {
                    Check(encoded.Value.Reconstruct().Value == value, $"ROUNDTRIP[{size}]({value})", ref checks, failures);
                    Check(InvariantHolds(encoded.Value), $"INVARIANT[{size}]({value})", ref checks, failures);
                    var roundtrip = HybridInteger.Deserialize(encoded.Value.Serialize().Data);
                    Check(roundtrip.Receipt.Succeeded && encoded.Value.Equals(roundtrip.Value), $"SERIALIZE[{size}]({value})", ref checks, failures);
                }
            }
        }

        var arithmeticBank = ValuationBank.First(8);
        for (var left = -32; left <= 32; left++)
        {
            var a = HybridInteger.FromBinary(left, arithmeticBank, 16).Value!;
            for (var right = -32; right <= 32; right++)
            {
                var b = HybridInteger.FromBinary(right, arithmeticBank, 16).Value!;
                var product = a.Multiply(b);
                Check(product.Receipt.Succeeded && product.Value!.Reconstruct().Value == left * right,
                    $"MUL({left},{right})", ref checks, failures);
                var sum = a.AddPreservingValuations(b);
                Check(sum.Receipt.Succeeded && sum.Value!.Reconstruct().Value == left + right,
                    $"ADD({left},{right})", ref checks, failures);
                var normalized = sum.Value?.Normalize();
                Check(normalized?.Receipt.Succeeded == true && normalized.Value!.Reconstruct().Value == left + right &&
                      InvariantHolds(normalized.Value), $"NORMALIZE({left},{right})", ref checks, failures);

                if (right != 0)
                {
                    var division = a.ExactDivide(b);
                    Check(division.Receipt.Succeeded == (left % right == 0), $"DIV_STATUS({left},{right})", ref checks, failures);
                    if (division.Value is not null)
                    {
                        Check(division.Value.Reconstruct().Value == left / right, $"DIV_VALUE({left},{right})", ref checks, failures);
                    }

                    var divides = b.Divides(a);
                    Check(divides.IsKnown && divides.Value == (left % right == 0), $"DIVIDES({left},{right})", ref checks, failures);
                }
            }
        }

        var random = new Random(Seed);
        for (var trial = 0; trial < 5_000; trial++)
        {
            var left = random.NextInt64(-1_000_000, 1_000_001);
            var right = random.NextInt64(-1_000_000, 1_000_001);
            var a = HybridInteger.FromBinary(left, arithmeticBank, 32).Value!;
            var b = HybridInteger.FromBinary(right, arithmeticBank, 32).Value!;
            Check(a.Multiply(b).Value!.Reconstruct().Value == new BigInteger(left) * right,
                $"RANDOM_MUL({trial})", ref checks, failures);
            var sum = a.AddPreservingValuations(b);
            Check(sum.Value!.Reconstruct().Value == new BigInteger(left) + right,
                $"RANDOM_ADD({trial})", ref checks, failures);
            Check(sum.Value.Normalize().Value!.Reconstruct().Value == new BigInteger(left) + right,
                $"RANDOM_REFRESH({trial})", ref checks, failures);
        }

        var invalidEnum = HybridInteger.FromStructured(
            1,
            2,
            new BigInteger[] { 0 },
            ValuationBank.First(1),
            8,
            new[] { (ValuationKnowledge)999 });
        Check(!invalidEnum.Receipt.Succeeded, "UNDEFINED_KNOWLEDGE_REJECTED", ref checks, failures);
        var zeroValuation = HybridInteger.Zero(ValuationBank.First(1), 8).Valuation(0);
        Check(zeroValuation.Value?.Kind == ValuationResultKind.PositiveInfinity, "ZERO_VALUATION_INFINITY", ref checks, failures);

        return new CorrectnessReceipt(
            "BOUNDED_PASS only if Failures is empty; no untested arithmetic domain is certified.",
            checks,
            failures,
            new
            {
                ExhaustiveSignedIngress = "-128..128 at bank sizes 0,4,8",
                ExhaustiveOrderedPairs = "-32..32 squared at bank size 8",
                RandomSeed = Seed,
                RandomTrials = 5_000,
                MalformedAndZeroValuationChecks = 2,
            });
    }

    private static List<string[]> BuildContractCases()
    {
        var bank = ValuationBank.First(4);
        var cases = new List<(string Name, HybridResult<HybridInteger> Result, string Expected)>
        {
            ("binary-zero", HybridInteger.FromBinary(0, bank, 8), "success/canonical-zero"),
            ("binary-negative", HybridInteger.FromBinary(-12, bank, 8), "success/exact-negative"),
            ("structured-identity", HybridInteger.FromStructured(1, 1, new BigInteger[] { 0, 0, 0, 0 }, bank, 8), "success/identity"),
            ("negative-zero", HybridInteger.FromStructured(0, 1, new BigInteger[] { 0, 0, 0, 0 }, bank, 8), "reject"),
            ("exact-lane-contradicted", HybridInteger.FromStructured(1, 2, new BigInteger[] { 0, 0, 0, 0 }, bank, 8), "reject"),
            ("lower-bound-residual", HybridInteger.FromStructured(1, 8, new BigInteger[] { 0, 0, 0, 0 }, bank, 8,
                new[] { ValuationKnowledge.CertifiedLowerBound, ValuationKnowledge.KnownExact, ValuationKnowledge.KnownExact, ValuationKnowledge.KnownExact }), "success/partial"),
            ("width-overflow", HybridInteger.FromBinary(16, ValuationBank.First(1), 2), "reject"),
            ("claimed-magnitude-valid", HybridInteger.FromClaimedMagnitude(12, 1, 1, new BigInteger[] { 2, 1, 0, 0 }, bank, 8), "success"),
            ("claimed-magnitude-false", HybridInteger.FromClaimedMagnitude(13, 1, 1, new BigInteger[] { 2, 1, 0, 0 }, bank, 8), "reject"),
            ("undefined-knowledge", HybridInteger.FromStructured(1, 1, new BigInteger[] { 0, 0, 0, 0 }, bank, 8,
                new[] { (ValuationKnowledge)99, ValuationKnowledge.KnownExact, ValuationKnowledge.KnownExact, ValuationKnowledge.KnownExact }), "reject"),
        };
        var rows = cases.Select(item => new[]
        {
            item.Name,
            item.Expected,
            item.Result.Receipt.Succeeded ? "success" : "failure",
            item.Result.Receipt.Failure.ToString(),
            item.Result.Value?.Validity.ToString() ?? string.Empty,
            item.Result.Value?.Reconstruct().Value.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            item.Result.Receipt.Scope,
        }).ToList();

        var malformed = new[]
        {
            string.Empty,
            "{}",
            "{\"schema\":\"prime-axiom-hybrid-integer-v1\",\"schema\":\"prime-axiom-hybrid-integer-v1\"}",
            "{\"schema\":\"unknown\",\"bank\":[],\"exponentWidth\":8,\"sign\":0,\"cofactor\":\"0\",\"exponents\":[],\"knowledge\":[]}",
            "{\"schema\":\"prime-axiom-hybrid-integer-v1\",\"bank\":[4],\"exponentWidth\":8,\"sign\":1,\"cofactor\":\"1\",\"exponents\":[\"0\"],\"knowledge\":[\"KnownExact\"]}",
        };
        for (var index = 0; index < malformed.Length; index++)
        {
            var result = HybridInteger.Deserialize(malformed[index]);
            rows.Add(new[]
            {
                $"malformed-json-{index}", "reject", result.Receipt.Succeeded ? "success" : "failure",
                result.Receipt.Failure.ToString(), string.Empty, string.Empty, result.Receipt.Detail ?? result.Receipt.Scope,
            });
        }

        return rows;
    }

    private static List<PilotRow> BuildWorkloadMatrix()
    {
        var workloads = BuildSemanticWorkloads();
        var rows = new List<PilotRow>();
        foreach (var workload in workloads)
        {
            rows.Add(RunBinary(workload));
            rows.Add(RunSparseFull(workload));

            foreach (var size in BankSizes)
            {
                var first = ValuationBank.First(size);
                var selected = SelectedBank(workload.Family, size);
                rows.Add(RunBuild000(workload, first, "B000_DENSE", "FIRST_K", sparse: false));
                rows.Add(RunBuild000(workload, selected, "B000_DENSE", "SELECTED_K", sparse: false));
                rows.Add(RunBuild000(workload, first, "B000_SPARSE", "FIRST_K", sparse: true));
                rows.Add(RunBuild000(workload, selected, "B000_SPARSE", "SELECTED_K", sparse: true));
            }

            rows.Add(RunHybrid(workload, ValuationBank.Empty, "HYBRID_EAGER", "NONE", eager: true));
            foreach (var size in BankSizes)
            {
                rows.Add(RunHybrid(workload, ValuationBank.First(size), "HYBRID_EAGER", "FIRST_K", eager: true));
                rows.Add(RunHybrid(workload, SelectedBank(workload.Family, size), "HYBRID_EAGER", "SELECTED_K", eager: true));
                rows.Add(RunHybrid(workload, ValuationBank.First(size), "HYBRID_EAGER", "ADAPTIVE_LRU_K_NO_ELIGIBLE_MAG_REFERENCE", eager: true));
            }

            if (workload.Operation is PilotOperation.Add or PilotOperation.Mixed or PilotOperation.Adversarial)
            {
                rows.Add(RunHybrid(workload, ValuationBank.Empty, "HYBRID_LAZY", "NONE", eager: false));
                foreach (var size in BankSizes)
                {
                    rows.Add(RunHybrid(workload, ValuationBank.First(size), "HYBRID_LAZY", "FIRST_K", eager: false));
                    rows.Add(RunHybrid(workload, SelectedBank(workload.Family, size), "HYBRID_LAZY", "SELECTED_K", eager: false));
                    rows.Add(RunHybrid(workload, ValuationBank.First(size), "HYBRID_LAZY", "ADAPTIVE_LRU_K_NO_ELIGIBLE_MAG_REFERENCE", eager: false));
                }
            }
        }

        return rows;
    }

    private static PilotRow RunHybrid(
        PilotWorkload workload,
        ValuationBank bank,
        string implementation,
        string policy,
        bool eager)
    {
        var ledger = HybridCostLedger.Zero;
        long operations = 0;
        long failures = 0;
        long partialOutputs = 0;
        long payloadBits = 0;
        long payloadSamples = 0;
        foreach (var item in workload.Events)
        {
            var aResult = HybridInteger.FromBinary(item.A, bank, 16);
            var bResult = HybridInteger.FromBinary(item.B, bank, 16);
            var needsThirdOperand = workload.Operation is PilotOperation.Mixed or PilotOperation.Adversarial;
            var cResult = needsThirdOperand ? HybridInteger.FromBinary(item.C, bank, 16) : null;
            ledger += aResult.Receipt.Cost + bResult.Receipt.Cost;
            if (cResult is not null)
            {
                ledger += cResult.Receipt.Cost;
            }

            if (aResult.Value is null || bResult.Value is null || (needsThirdOperand && cResult?.Value is null))
            {
                failures++;
                continue;
            }

            var a = aResult.Value;
            var b = bResult.Value;
            var c = cResult?.Value;
            payloadBits += a.MeasurePayload().PerValuePayloadBits + b.MeasurePayload().PerValuePayloadBits;
            payloadSamples += 2;
            if (c is not null)
            {
                payloadBits += c.MeasurePayload().PerValuePayloadBits;
                payloadSamples++;
            }
            switch (workload.Operation)
            {
                case PilotOperation.ProductCancel:
                    {
                        var product = a.Multiply(b);
                        ledger += product.Receipt.Cost;
                        operations++;
                        if (product.Value is null)
                        {
                            failures++;
                            break;
                        }

                        var cancelled = product.Value.ExactDivide(b);
                        ledger += cancelled.Receipt.Cost;
                        operations++;
                        failures += VerifyMagnitude(cancelled.Value, item.A, ref ledger) ? 0 : 1;
                        break;
                    }

                case PilotOperation.Divides:
                    {
                        var query = a.Divides(b);
                        ledger += query.Receipt.Cost;
                        operations++;
                        if (!query.IsKnown || query.Value != (item.B % item.A == BigInteger.Zero))
                        {
                            failures++;
                        }

                        break;
                    }

                case PilotOperation.Add:
                    {
                        var sum = a.AddPreservingValuations(b);
                        ledger += sum.Receipt.Cost;
                        operations++;
                        var value = PrepareAddedResult(sum.Value, eager, ref ledger, ref partialOutputs, ref failures);
                        failures += VerifyMagnitude(value, item.A + item.B, ref ledger) ? 0 : 1;
                        break;
                    }

                case PilotOperation.Mixed:
                case PilotOperation.Adversarial:
                    {
                        var product = a.Multiply(b);
                        ledger += product.Receipt.Cost;
                        operations++;
                        if (product.Value is null)
                        {
                            failures++;
                            break;
                        }

                        var sum = product.Value.AddPreservingValuations(c!);
                        ledger += sum.Receipt.Cost;
                        operations++;
                        var value = PrepareAddedResult(sum.Value, eager, ref ledger, ref partialOutputs, ref failures);
                        failures += VerifyMagnitude(value, item.A * item.B + item.C, ref ledger) ? 0 : 1;
                        break;
                    }
            }
        }

        var total = ledger.Total;
        return new PilotRow(
            workload.Family,
            workload.Id,
            implementation,
            policy,
            bank.Count,
            workload.Events.Count,
            operations,
            failures == 0 ? "BOUNDED_PASS" : "FAILED",
            failures,
            partialOutputs,
            ledger.Ingress.TrialRemainders,
            ledger.Ingress.FactorDivisions,
            ledger.Native.BankGates.NandEvaluations,
            ledger.Native.ModeledBinaryNands,
            ledger.Native.CofactorAdditions,
            ledger.Native.CofactorMultiplications,
            ledger.Native.CofactorDivisions,
            ledger.Native.CofactorRemainders,
            ledger.Native.CofactorGcds,
            ledger.Maintenance.TrialRemainders,
            ledger.Maintenance.FactorDivisions,
            ledger.Maintenance.Migrations,
            ledger.Egress.ReconstructionMultiplications,
            total.LaneReads,
            total.LaneWrites,
            payloadSamples == 0 ? 0 : payloadBits / payloadSamples,
            bank.CatalogPayloadBits,
            workload.OutputObligation,
            policy.StartsWith("ADAPTIVE", StringComparison.Ordinal)
                ? "MAG ingress exposes no unbanked factor identity; registered LRU cannot adapt in this cell"
                : "Phase-separated exact hybrid receipt");
    }

    private static PilotRow RunBinary(PilotWorkload workload)
    {
        long operations = 0;
        long modeledNands = 0;
        long additions = 0;
        long multiplications = 0;
        long divisions = 0;
        long remainders = 0;
        long payload = 0;
        var inputCount = workload.Operation is PilotOperation.Mixed or PilotOperation.Adversarial ? 3L : 2L;
        foreach (var item in workload.Events)
        {
            payload += BitLength(item.A) + BitLength(item.B);
            if (inputCount == 3)
            {
                payload += BitLength(item.C);
            }
            switch (workload.Operation)
            {
                case PilotOperation.ProductCancel:
                    modeledNands += MultiplyNands(Math.Max(BitLength(item.A), BitLength(item.B)));
                    multiplications++;
                    divisions++;
                    operations += 2;
                    _benchmarkSink = item.A * item.B / item.B;
                    break;
                case PilotOperation.Divides:
                    remainders++;
                    operations++;
                    _benchmarkSink = item.B % item.A == 0;
                    break;
                case PilotOperation.Add:
                    modeledNands += AddNands(Math.Max(BitLength(item.A), BitLength(item.B)) + 1);
                    additions++;
                    operations++;
                    _benchmarkSink = item.A + item.B;
                    break;
                case PilotOperation.Mixed:
                case PilotOperation.Adversarial:
                    modeledNands += MultiplyNands(Math.Max(BitLength(item.A), BitLength(item.B)));
                    modeledNands += AddNands(Math.Max(BitLength(item.A * item.B), BitLength(item.C)) + 1);
                    multiplications++;
                    additions++;
                    operations += 2;
                    _benchmarkSink = item.A * item.B + item.C;
                    break;
            }
        }

        return new PilotRow(
            workload.Family,
            workload.Id,
            "BIN_EXACT",
            "NONE",
            0,
            workload.Events.Count,
            operations,
            "BOUNDED_PASS",
            0,
            0,
            0,
            0,
            0,
            modeledNands,
            additions,
            multiplications,
            divisions,
            remainders,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            payload / (workload.Events.Count * inputCount),
            0,
            workload.OutputObligation,
            "Optimized BigInteger is the executable baseline; NAND count is a transparent schoolbook/add proxy, not its implementation");
    }

    private static PilotRow RunSparseFull(PilotWorkload workload) =>
        PilotRow.Unsupported(
            workload,
            "SPARSE_FULL",
            "FULL_TRIAL_FACTOR_CONTROL",
            0,
            "Pilot has no independently executable full-factor-map operation/output replay; oracle arithmetic is not charged as a comparator");

    private static PilotRow RunBuild000(
        PilotWorkload workload,
        ValuationBank bank,
        string implementation,
        string policy,
        bool sparse)
    {
        if (sparse)
        {
            return PilotRow.Unsupported(
                workload,
                implementation,
                policy,
                bank.Count,
                "Build 000 sparse type has compose accounting but no exact sparse cancellation replay; dense gate costs are not relabeled as sparse");
        }

        if (workload.Operation != PilotOperation.ProductCancel)
        {
            return PilotRow.Unsupported(workload, implementation, policy, bank.Count,
                "Build 000 pure positive coordinate operations do not implement this signed/cofactor workload contract");
        }

        var basis = new PrimeBasis(bank.ToArray());
        long operations = 0;
        long failures = 0;
        long ingressChecks = 0;
        long ingressDivisions = 0;
        long gates = 0;
        long payload = 0;
        foreach (var item in workload.Events)
        {
            var a = PrimeCoordinates.Encode(item.A, basis, 16);
            var b = PrimeCoordinates.Encode(item.B, basis, 16);
            ingressChecks += a.Receipt.Cost.TrialRemainders + b.Receipt.Cost.TrialRemainders;
            ingressDivisions += a.Receipt.Cost.FactorDivisions + b.Receipt.Cost.FactorDivisions;
            if (a.Value is null || b.Value is null)
            {
                failures++;
                continue;
            }

            var product = a.Value.Compose(b.Value);
            var cancel = product.Value?.Cancel(b.Value);
            operations += 2;
            gates += product.Receipt.Cost.Gates.NandEvaluations + (cancel?.Receipt.Cost.Gates.NandEvaluations ?? 0);
            if (cancel?.Value?.Reconstruct().Value != item.A)
            {
                failures++;
            }

            payload += a.Value.DensePayloadBits + b.Value.DensePayloadBits;
        }

        return new PilotRow(
            workload.Family,
            workload.Id,
            implementation,
            policy,
            bank.Count,
            workload.Events.Count,
            operations,
            failures == 0 ? "BOUNDED_PASS" : "EXPECTED_BASIS_ESCAPE",
            failures,
            0,
            ingressChecks,
            ingressDivisions,
            gates,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            workload.Events.Count == 0 ? 0 : payload / (workload.Events.Count * 2L),
            bank.CatalogPayloadBits,
            workload.OutputObligation,
            "Historical Build 000 control; basis escapes remain visible and are not imputed");
    }

    private static List<BankRow> BuildBankStrategyMatrix()
    {
        var workloads = BuildSemanticWorkloads();
        var rows = new List<BankRow>();
        foreach (var workload in workloads)
        {
            foreach (var size in new[] { 0, 4, 8, 16, 32 })
            {
                foreach (var (policy, bank) in BanksForStrategy(workload.Family, size))
                {
                    long cofactorBits = 0;
                    long exponentBits = 0;
                    long metadataBits = 0;
                    long nonzeroLanes = 0;
                    long trialRemainders = 0;
                    long factorDivisions = 0;
                    long samples = 0;
                    foreach (var item in workload.Events)
                    {
                        var magnitudes = workload.Operation is PilotOperation.Mixed or PilotOperation.Adversarial
                            ? new[] { item.A, item.B, item.C }
                            : new[] { item.A, item.B };
                        foreach (var magnitude in magnitudes)
                        {
                            var encoded = HybridInteger.FromBinary(magnitude, bank, 16);
                            if (encoded.Value is null)
                            {
                                continue;
                            }

                            var metrics = encoded.Value.MeasurePayload();
                            cofactorBits += metrics.CofactorBits;
                            exponentBits += metrics.ExponentBits;
                            metadataBits += metrics.KnowledgeBits + metrics.ProvenanceBits + metrics.SignAndZeroBits;
                            nonzeroLanes += Enumerable.Range(0, bank.Count).Count(lane => encoded.Value.ExponentAt(lane) > 0);
                            trialRemainders += encoded.Receipt.Cost.Ingress.TrialRemainders;
                            factorDivisions += encoded.Receipt.Cost.Ingress.FactorDivisions;
                            samples++;
                        }
                    }

                    rows.Add(new BankRow(
                        workload.Family,
                        policy,
                        bank.Count,
                        samples,
                        samples == 0 ? 0 : cofactorBits / samples,
                        samples == 0 ? 0 : exponentBits / samples,
                        samples == 0 ? 0 : metadataBits / samples,
                        samples == 0 ? 0 : nonzeroLanes / (double)samples,
                        bank.CatalogPayloadBits,
                        trialRemainders,
                        factorDivisions,
                        bank.ValidationTrialDivisions,
                        bank.CanonicalId));
                }
            }
        }

        return rows;
    }

    private static List<FreshnessRow> BuildAdditionFreshness()
    {
        var random = new Random(Seed ^ 0xADD);
        var pairs = Enumerable.Range(0, 1_000)
            .Select(_ => (Left: new BigInteger(random.Next(-50_000, 50_001)), Right: new BigInteger(random.Next(-50_000, 50_001))))
            .ToArray();
        var rows = new List<FreshnessRow>();
        foreach (var size in new[] { 0, 4, 8, 16, 32 })
        {
            var bank = ValuationBank.First(size);
            long exactLanes = 0;
            long unknownLanes = 0;
            long partialValues = 0;
            long refreshChecks = 0;
            long refreshDivisions = 0;
            long nativeNands = 0;
            long cofactorAdds = 0;
            foreach (var (left, right) in pairs)
            {
                var a = HybridInteger.FromBinary(left, bank, 16).Value!;
                var b = HybridInteger.FromBinary(right, bank, 16).Value!;
                var added = a.AddPreservingValuations(b);
                var sum = added.Value!;
                nativeNands += added.Receipt.Cost.Native.BankGates.NandEvaluations;
                cofactorAdds++;
                if (sum.Validity == HybridValidity.Partial)
                {
                    partialValues++;
                }

                for (var lane = 0; lane < bank.Count; lane++)
                {
                    if (sum.KnowledgeAt(lane) == ValuationKnowledge.KnownExact)
                    {
                        exactLanes++;
                    }
                    else
                    {
                        unknownLanes++;
                    }
                }

                var normalized = sum.Normalize();
                refreshChecks += normalized.Receipt.Cost.Maintenance.TrialRemainders;
                refreshDivisions += normalized.Receipt.Cost.Maintenance.FactorDivisions;
            }

            rows.Add(new FreshnessRow(
                size,
                pairs.Length,
                exactLanes,
                unknownLanes,
                partialValues,
                refreshChecks,
                refreshDivisions,
                nativeNands,
                cofactorAdds,
                "Exact when operand valuations differ; otherwise a certified lower bound until refresh"));
        }

        return rows;
    }

    private static List<MigrationRow> BuildMigrationThrash()
    {
        var catalog = ValuationBank.First(96).ToArray();
        var rows = new List<MigrationRow>();
        foreach (var size in BankSizes)
        {
            var currentBank = ValuationBank.First(size);
            var values = Enumerable.Range(0, 16)
                .Select(index => HybridInteger.FromBinary(
                    new BigInteger(ResidentFactors[index % ResidentFactors.Length]) * RoughPrimes[index % RoughPrimes.Length],
                    currentBank,
                    16).Value!)
                .ToArray();
            var references = catalog.Skip(32).Take(size + 1).ToArray();
            var recency = new LinkedList<int>(currentBank);
            var ledger = HybridCostLedger.Zero;
            long migrations = 0;
            long exactnessFailures = 0;
            for (var cycle = 0; cycle < 4; cycle++)
            {
                foreach (var prime in references)
                {
                    if (currentBank.IndexOf(prime) >= 0)
                    {
                        recency.Remove(prime);
                        recency.AddLast(prime);
                        continue;
                    }

                    var evicted = recency.First!.Value;
                    recency.RemoveFirst();
                    recency.AddLast(prime);
                    var members = currentBank.Where(value => value != evicted).Append(prime).OrderBy(value => value).ToArray();
                    var nextBank = new ValuationBank(members, BankStrategy.Adaptive, $"lru-thrash-{size}");
                    for (var index = 0; index < values.Length; index++)
                    {
                        var before = values[index].Reconstruct().Value;
                        var migrated = values[index].MigrateBank(nextBank);
                        ledger += migrated.Receipt.Cost;
                        if (migrated.Value is null || migrated.Value.Reconstruct().Value != before)
                        {
                            exactnessFailures++;
                        }
                        else
                        {
                            values[index] = migrated.Value;
                        }
                    }

                    currentBank = nextBank;
                    migrations++;
                }
            }

            rows.Add(new MigrationRow(
                size,
                references.Length,
                migrations,
                values.Length,
                ledger.Maintenance.Migrations,
                ledger.Maintenance.TrialRemainders,
                ledger.Maintenance.FactorDivisions,
                ledger.Maintenance.CofactorMultiplications,
                ledger.Maintenance.LaneReads,
                ledger.Maintenance.LaneWrites,
                exactnessFailures,
                "Deterministic K+1 reference cycle; every bank change migrates all 16 live values"));
        }

        return rows;
    }

    private static List<CrossoverRow> BuildCrossovers()
    {
        var rows = new List<CrossoverRow>();
        foreach (var size in BankSizes)
        {
            var bank = ValuationBank.First(size);
            var left = HybridInteger.FromBinary(BigInteger.Pow(2, 7) * BigInteger.Pow(3, 5), bank, 16).Value!;
            var right = HybridInteger.FromBinary(BigInteger.Pow(5, 4) * BigInteger.Pow(7, 3), bank, 16).Value!;
            var ingress = HybridInteger.FromBinary(left.Reconstruct().Value, bank, 16).Receipt.Cost.Total +
                          HybridInteger.FromBinary(right.Reconstruct().Value, bank, 16).Receipt.Cost.Total;
            var binaryWidth = Math.Max(BitLength(left.Reconstruct().Value), BitLength(right.Reconstruct().Value));
            foreach (var reuse in ReuseCounts)
            {
                var hybrid = ingress;
                for (var iteration = 0; iteration < reuse; iteration++)
                {
                    hybrid += left.Multiply(right).Receipt.Cost.Total;
                }

                hybrid += left.Multiply(right).Value!.Reconstruct().Receipt.Cost.Total;
                var hybridWork = hybrid.BankGates.NandEvaluations + hybrid.ModeledBinaryNands;
                var binaryWork = checked(MultiplyNands(binaryWidth) * reuse);
                var hybridPayload = left.MeasurePayload().PerValuePayloadBits + right.MeasurePayload().PerValuePayloadBits;
                var binaryPayload = 2 * binaryWidth;
                rows.Add(new CrossoverRow(
                    size,
                    reuse,
                    binaryWork,
                    hybridWork,
                    binaryPayload,
                    hybridPayload,
                    hybridWork < binaryWork,
                    hybridPayload <= 2 * binaryPayload,
                    "Proxy combines exact bank NANDs and declared schoolbook cofactor NANDs; it is not host timing"));
            }
        }

        return rows;
    }

    private static object BuildVmTrace()
    {
        var bank = ValuationBank.First(4);
        var machine = new HybridMachine(bank, 4);
        var success = machine.Run(new HybridProgram(new HybridInstruction[]
        {
            new HybridLoadBinary(0, 12),
            new HybridLoadBinary(1, 20),
            new HybridAddPreserve(2, 0, 1),
            new HybridReadValuation(2, 0),
            new HybridRefreshLane(3, 2, 0),
            new HybridReconstruct(3),
        }));
        var overflowMachine = new HybridMachine(ValuationBank.First(1), 2);
        var overflow = overflowMachine.Run(new HybridProgram(new HybridInstruction[]
        {
            new HybridLoadBinary(0, 8),
            new HybridLoadBinary(1, 2),
            new HybridLoadBinary(2, 3),
            new HybridMultiply(2, 0, 1),
        }));
        return new
        {
            Schema = "prime-axiom-build001-vm-trace-v1",
            SuccessTrace = success,
            FailureTrace = overflow,
            FailedDestinationReadable = overflowMachine.ReadRegister(2) is not null,
            Claim = "FailedDestinationReadable must be false; earlier register 2 content is invalidated by overflow.",
        };
    }

    private static List<string[]> RunMicrobenchmarks()
    {
        var bank = ValuationBank.First(8);
        var smoothLeft = HybridInteger.FromBinary(BigInteger.Pow(2, 12) * BigInteger.Pow(3, 8), bank, 16).Value!;
        var smoothRight = HybridInteger.FromBinary(BigInteger.Pow(5, 9) * BigInteger.Pow(7, 5), bank, 16).Value!;
        var roughLeft = HybridInteger.FromBinary(BigInteger.Pow(2, 120) - 159, bank, 16).Value!;
        var roughRight = HybridInteger.FromBinary(BigInteger.Pow(3, 75) + 211, bank, 16).Value!;
        var addLeft = HybridInteger.FromBinary(123_456_789, bank, 16).Value!;
        var addRight = HybridInteger.FromBinary(987_654_321, bank, 16).Value!;
        var hostLeft = roughLeft.Reconstruct().Value;
        var hostRight = roughRight.Reconstruct().Value;
        return new List<string[]>
        {
            Measure("host_bigint_multiply_rough", 20_000, () => _benchmarkSink = hostLeft * hostRight),
            Measure("hybrid_multiply_smooth_bank8", 2_000, () => _benchmarkSink = smoothLeft.Multiply(smoothRight)),
            Measure("hybrid_multiply_rough_bank8", 2_000, () => _benchmarkSink = roughLeft.Multiply(roughRight)),
            Measure("hybrid_add_lazy_bank8", 2_000, () => _benchmarkSink = addLeft.AddPreservingValuations(addRight)),
            Measure("hybrid_add_eager_bank8", 1_000, () => _benchmarkSink = addLeft.AddPreservingValuations(addRight).Value!.Normalize()),
            Measure("hybrid_binary_ingress_bank8", 5_000, () => _benchmarkSink = HybridInteger.FromBinary(987_654_321, bank, 16)),
        };
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
            name,
            iterations.ToString(CultureInfo.InvariantCulture),
            trials.ToString(CultureInfo.InvariantCulture),
            samples[trials / 2].ToString("F3", CultureInfo.InvariantCulture),
            samples[0].ToString("F3", CultureInfo.InvariantCulture),
            samples[^1].ToString("F3", CultureInfo.InvariantCulture),
            "One managed implementation and host; not hardware latency or a population claim",
        };
    }

    private static void WriteCoverage(string path, IReadOnlyList<PilotRow> workloads)
    {
        WriteJson(path, new
        {
            Schema = "prime-axiom-build001-protocol-coverage-v1",
            FrozenPlanSha256,
            Status = "PILOT_SUBSET_COMPLETE_FULL_CONFIRMATION_NOT_RUN",
            Executed = new
            {
                WorkloadFamilies = workloads.Select(row => row.Family).Distinct().OrderBy(value => value).ToArray(),
                WorkloadIds = workloads.Select(row => row.Workload).Distinct().OrderBy(value => value).ToArray(),
                ConfigurationRows = workloads.Count,
                BankSizes = ExecutedBankSizes,
                SourceMode = "MAG",
                FunctionalExponentWidth = 16,
                PilotReplicates = 1,
                Output = "magnitude-final or predicate",
            },
            NotExecuted = UnexecutedProtocolCells,
            Consequence = "Pilot receipts may locate mechanisms and failures but cannot earn a terminal Build 001 decision under the unmet frozen stop rule.",
        });
    }

    private static void WriteManifest(
        string outputDirectory,
        string invocationOutputArgument,
        bool includedBenchmarks,
        CorrectnessReceipt correctness,
        IReadOnlyList<PilotRow> workloads)
    {
        var files = Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new
            {
                Path = Path.GetRelativePath(outputDirectory, path).Replace('\\', '/'),
                Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            })
            .ToArray();
        var coreAssembly = typeof(HybridInteger).Assembly.Location;
        var cliAssembly = typeof(Build001ExperimentRunner).Assembly.Location;
        var configuration = typeof(Build001ExperimentRunner).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "unknown";
        var actualInvocationArguments = BuildInvocationArguments(invocationOutputArgument, includedBenchmarks);
        WriteJson(Path.Combine(outputDirectory, "manifest.json"), new
        {
            Schema = "prime-axiom-build001-experiment-manifest-v1",
            BaselineCommit,
            FrozenPlanSha256,
            Build000ManifestSha256,
            CanonicalReproductionCommand =
                $"dotnet run --project src/PrimeAxiom.Cli --configuration {configuration} --no-build -- experiment-build001 --output results/build001" +
                (includedBenchmarks ? string.Empty : " --skip-benchmarks"),
            ActualInvocationArguments = actualInvocationArguments,
            DeterministicSeed = Seed,
            IncludedWallClockBenchmarks = includedBenchmarks,
            ClaimStatus = correctness.Failures.Count == 0 ? "PARTIAL" : "FAILED",
            CorrectnessStatus = correctness.Failures.Count == 0 ? "BOUNDED_PASS" : "FAILED",
            ProtocolCoverage = "PILOT_SUBSET_COMPLETE_FULL_CONFIRMATION_NOT_RUN",
            DecisionGateCeiling = "PARTIAL - PILOT_NEGATIVE; FINAL DECISION NOT EARNED. The frozen full-matrix stop condition is unmet.",
            WorkloadConfigurationRows = workloads.Count,
            Framework = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "not reported",
            LogicalProcessors = Environment.ProcessorCount,
            CoreAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(coreAssembly))),
            CliAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cliAssembly))),
            Files = files,
        });
    }

    private static void WritePhaseCostSvg(
        string path,
        IReadOnlyList<PilotRow> rows,
        string inputPath,
        string generatorCommand)
    {
        var selected = rows
            .Where(row => row.Workload == "M010_PILOT" &&
                          ((row.Implementation == "BIN_EXACT") ||
                           (row.Implementation == "HYBRID_EAGER" && row.Policy == "FIRST_K" && row.BankSize is 4 or 8 or 16 or 32)))
            .OrderBy(row => row.BankSize)
            .ToArray();
        var max = Math.Max(1L, selected.Max(row => row.NativeBankNands + row.ModeledBinaryNands + row.IngressRemainders + row.MaintenanceRemainders));
        var builder = new StringBuilder();
        builder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"980\" height=\"560\" viewBox=\"0 0 980 560\" role=\"img\" aria-labelledby=\"title desc\">");
        builder.AppendLine("<title id=\"title\">Build 001 factor-resident pilot phase counters</title>");
        builder.AppendLine("<desc id=\"desc\">Separate unweighted counters for ingress tests, bank NAND evaluations, modeled binary NAND evaluations, and maintenance tests.</desc>");
        builder.AppendLine(CultureInfo.InvariantCulture, $"<metadata id=\"provenance\"><generator-command>{SecurityElement.Escape(generatorCommand)}</generator-command><input path=\"workload_matrix.csv\" sha256=\"{FileSha256(inputPath)}\"/></metadata>");
        builder.AppendLine("<rect width=\"980\" height=\"560\" fill=\"#fbfaf7\"/><text x=\"70\" y=\"38\" font-family=\"Segoe UI, sans-serif\" font-size=\"22\" font-weight=\"600\">Factor-resident pilot: costs remain separate</text>");
        var colors = new[] { "#3366cc", "#dc3912", "#109618", "#990099" };
        var labels = new[] { "ingress remainder tests", "bank NAND evaluations", "modeled cofactor/binary NANDs", "maintenance remainder tests" };
        for (var legend = 0; legend < labels.Length; legend++)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"<rect x=\"650\" y=\"{20 + legend * 22}\" width=\"14\" height=\"14\" fill=\"{colors[legend]}\"/><text x=\"672\" y=\"{32 + legend * 22}\" font-family=\"Segoe UI, sans-serif\" font-size=\"12\">{labels[legend]}</text>");
        }

        for (var index = 0; index < selected.Length; index++)
        {
            var row = selected[index];
            var values = new[] { row.IngressRemainders, row.NativeBankNands, row.ModeledBinaryNands, row.MaintenanceRemainders };
            var x = 90 + index * 170;
            for (var series = 0; series < values.Length; series++)
            {
                var barHeight = 380d * values[series] / max;
                builder.AppendLine(CultureInfo.InvariantCulture,
                    $"<rect x=\"{x + series * 28}\" y=\"{470 - barHeight:F1}\" width=\"22\" height=\"{barHeight:F1}\" fill=\"{colors[series]}\"/>");
            }

            var label = row.Implementation == "BIN_EXACT" ? "binary" : $"hybrid K={row.BankSize}";
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"<text x=\"{x + 42}\" y=\"495\" text-anchor=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"12\">{label}</text>");
        }

        builder.AppendLine("<text x=\"490\" y=\"535\" text-anchor=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"12\">Bars are not additive universal cost or latency. Absolute maxima set one display scale.</text></svg>");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    private static void WriteBankTradeoffSvg(
        string path,
        IReadOnlyList<BankRow> rows,
        string inputPath,
        string generatorCommand)
    {
        var selected = rows.Where(row => row.Family == "M" && row.Policy == "FIRST_K").OrderBy(row => row.BankSize).ToArray();
        var maxX = Math.Max(1L, selected.Max(row => row.ExponentBits + row.MetadataBits));
        var maxY = Math.Max(1L, selected.Max(row => row.CofactorBits));
        var builder = new StringBuilder();
        builder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"900\" height=\"520\" viewBox=\"0 0 900 520\" role=\"img\" aria-labelledby=\"title desc\">");
        builder.AppendLine("<title id=\"title\">Bank metadata versus cofactor size</title><desc id=\"desc\">First-prime bank tradeoff for the multiplicative pilot corpus.</desc><rect width=\"900\" height=\"520\" fill=\"#fbfaf7\"/>");
        builder.AppendLine(CultureInfo.InvariantCulture, $"<metadata id=\"provenance\"><generator-command>{SecurityElement.Escape(generatorCommand)}</generator-command><input path=\"bank_strategy_matrix.csv\" sha256=\"{FileSha256(inputPath)}\"/></metadata>");
        builder.AppendLine("<text x=\"70\" y=\"35\" font-family=\"Segoe UI, sans-serif\" font-size=\"21\" font-weight=\"600\">Bank growth trades cofactor bits for lanes and metadata</text><line x1=\"80\" y1=\"450\" x2=\"850\" y2=\"450\" stroke=\"#4a5561\"/><line x1=\"80\" y1=\"70\" x2=\"80\" y2=\"450\" stroke=\"#4a5561\"/>");
        foreach (var row in selected)
        {
            var x = 80 + 720d * (row.ExponentBits + row.MetadataBits) / maxX;
            var y = 450 - 340d * row.CofactorBits / maxY;
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"<circle cx=\"{x:F1}\" cy=\"{y:F1}\" r=\"7\" fill=\"#276fbf\"/><text x=\"{x + 10:F1}\" y=\"{y - 8:F1}\" font-family=\"Segoe UI, sans-serif\" font-size=\"12\">K={row.BankSize}</text>");
        }

        builder.AppendLine("<text x=\"465\" y=\"495\" text-anchor=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"12\">average exponent + metadata bits per value</text><text x=\"20\" y=\"260\" transform=\"rotate(-90 20 260)\" text-anchor=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"12\">average exact cofactor bits</text></svg>");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    private static string[] BuildInvocationArguments(string outputArgument, bool includedBenchmarks)
    {
        var arguments = new List<string> { "experiment-build001", "--output", outputArgument };
        if (!includedBenchmarks)
        {
            arguments.Add("--skip-benchmarks");
        }

        return arguments.ToArray();
    }

    private static string BuildGeneratorCommand(string outputArgument, bool includedBenchmarks) =>
        "dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- " +
        string.Join(' ', BuildInvocationArguments(outputArgument, includedBenchmarks));

    private static string FileSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static PilotWorkload[] BuildSemanticWorkloads()
    {
        var random = new Random(Seed ^ 0xC0DE);
        var resident = Enumerable.Range(0, 128)
            .Select(_ => new PilotEvent(
                SmoothValue(random, ResidentFactors, 3),
                SmoothValue(random, ResidentFactors, 3),
                BigInteger.One))
            .ToArray();
        var divisibility = Enumerable.Range(0, 256)
            .Select(index =>
            {
                var divisor = SmoothValue(random, ResidentFactors, 2);
                var dividend = index % 2 == 0 ? divisor * random.Next(1, 30) : divisor * random.Next(1, 30) + 1;
                return new PilotEvent(divisor, dividend, BigInteger.One);
            })
            .ToArray();
        var addition = Enumerable.Range(0, 256)
            .Select(_ => new PilotEvent(random.Next(-250_000, 250_001), random.Next(-250_000, 250_001), BigInteger.Zero))
            .ToArray();
        var mixed = Enumerable.Range(0, 128)
            .Select(_ => new PilotEvent(
                random.Next(-5_000, 5_001),
                random.Next(-5_000, 5_001),
                random.Next(-100_000, 100_001)))
            .ToArray();
        var adversarial = Enumerable.Range(0, 128)
            .Select(index => new PilotEvent(
                RoughPrimes[index % RoughPrimes.Length],
                RoughPrimes[(index + 3) % RoughPrimes.Length],
                index % 2 == 0 ? 1 : -1))
            .ToArray();
        return new[]
        {
            new PilotWorkload("M", "M010_PILOT", PilotOperation.ProductCancel, resident, "magnitude-final after exact cancellation"),
            new PilotWorkload("D", "D010_PILOT", PilotOperation.Divides, divisibility, "predicate for every query"),
            new PilotWorkload("A", "A010_PILOT", PilotOperation.Add, addition, "magnitude-final for every sum"),
            new PilotWorkload("X", "X020_PILOT", PilotOperation.Mixed, mixed, "magnitude-final for multiply-add"),
            new PilotWorkload("T", "T010_PILOT", PilotOperation.Adversarial, adversarial, "magnitude-final for outside-bank multiply-add"),
        };
    }

    private static ValuationBank SelectedBank(string family, int size)
    {
        var candidates = family == "T"
            ? ValuationBank.First(64).Skip(32).Take(size)
            : ValuationBank.First(Math.Max(size, 32)).Take(size);
        return ValuationBank.WorkloadSelected(candidates, $"selected-{family.ToLowerInvariant()}-{size}");
    }

    private static IEnumerable<(string Policy, ValuationBank Bank)> BanksForStrategy(string family, int size)
    {
        if (size == 0)
        {
            yield return ("NONE", ValuationBank.Empty);
            yield break;
        }

        yield return ("FIRST_K", ValuationBank.First(size));
        yield return ("SELECTED_K", SelectedBank(family, size));
    }

    private static BigInteger SmoothValue(Random random, IReadOnlyList<int> primes, int maximumExponent)
    {
        var result = BigInteger.One;
        foreach (var prime in primes)
        {
            result *= BigInteger.Pow(prime, random.Next(maximumExponent + 1));
        }

        return result;
    }

    private static HybridInteger? PrepareAddedResult(
        HybridInteger? value,
        bool eager,
        ref HybridCostLedger ledger,
        ref long partialOutputs,
        ref long failures)
    {
        if (value is null)
        {
            failures++;
            return null;
        }

        if (value.Validity == HybridValidity.Partial)
        {
            partialOutputs++;
        }

        if (!eager)
        {
            return value;
        }

        var normalized = value.Normalize();
        ledger += normalized.Receipt.Cost;
        if (normalized.Value is null)
        {
            failures++;
        }

        return normalized.Value;
    }

    private static bool VerifyMagnitude(HybridInteger? value, BigInteger expected, ref HybridCostLedger ledger)
    {
        if (value is null)
        {
            return false;
        }

        var reconstructed = value.Reconstruct();
        ledger += reconstructed.Receipt.Cost;
        return reconstructed.Value == expected;
    }

    private static bool InvariantHolds(HybridInteger value)
    {
        if (value.IsZero)
        {
            return value.Sign == 0 && value.Cofactor.IsZero;
        }

        if (value.Sign is not (-1 or 1) || value.Cofactor <= 0)
        {
            return false;
        }

        for (var lane = 0; lane < value.LaneCount; lane++)
        {
            if (value.KnowledgeAt(lane) == ValuationKnowledge.KnownExact && value.Cofactor % value.Bank[lane] == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static long BitLength(BigInteger value)
    {
        value = BigInteger.Abs(value);
        return value.IsZero ? 1 : value.GetBitLength();
    }

    private static long MultiplyNands(long width) => checked(32L * width * width);

    private static long AddNands(long width) => checked(15L * width);

    private static int CeilingLog2(int value) => value <= 1 ? 1 : checked((int)Math.Ceiling(Math.Log2(value)));

    private static void Check(bool condition, string name, ref long checks, List<string> failures)
    {
        checks++;
        if (!condition)
        {
            failures.Add(name);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

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

    private static string S(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static readonly string[] ContractHeaders =
    [
        "case", "expected", "actual", "failure", "validity", "magnitude", "receipt_scope",
    ];

    private static readonly string[] BenchmarkHeaders =
    [
        "benchmark", "iterations_per_trial", "trials", "median_ns_per_operation", "min_ns_per_operation",
        "max_ns_per_operation", "claim_ceiling",
    ];

    private sealed record CorrectnessReceipt(
        string StatusCeiling,
        long CheckCount,
        IReadOnlyList<string> Failures,
        object Domain);

    private enum PilotOperation
    {
        ProductCancel,
        Divides,
        Add,
        Mixed,
        Adversarial,
    }

    private sealed record PilotEvent(BigInteger A, BigInteger B, BigInteger C);

    private sealed record PilotWorkload(
        string Family,
        string Id,
        PilotOperation Operation,
        IReadOnlyList<PilotEvent> Events,
        string OutputObligation);

    private sealed record PilotRow(
        string Family,
        string Workload,
        string Implementation,
        string Policy,
        int BankSize,
        long Events,
        long Operations,
        string Status,
        long Failures,
        long PartialOutputs,
        long IngressRemainders,
        long IngressDivisions,
        long NativeBankNands,
        long ModeledBinaryNands,
        long CofactorAdds,
        long CofactorMultiplies,
        long CofactorDivides,
        long CofactorRemainders,
        long CofactorGcds,
        long MaintenanceRemainders,
        long MaintenanceDivisions,
        long Migrations,
        long EgressReconstructionMultiplies,
        long LaneReads,
        long LaneWrites,
        long AveragePayloadBits,
        long CatalogBits,
        string OutputObligation,
        string Interpretation)
    {
        public static readonly string[] Headers =
        [
            "family", "workload", "implementation", "policy", "bank_size", "events", "operations", "status",
            "failures", "partial_outputs", "ingress_remainders", "ingress_divisions", "native_bank_nands",
            "modeled_binary_nands", "cofactor_adds", "cofactor_multiplies", "cofactor_divides",
            "cofactor_remainders", "cofactor_gcds", "maintenance_remainders", "maintenance_divisions",
            "migrations", "egress_reconstruction_multiplies", "lane_reads", "lane_writes",
            "average_payload_bits", "catalog_bits_unamortized", "output_obligation", "interpretation",
        ];

        public string[] ToCsv() =>
        [
            Family, Workload, Implementation, Policy, S(BankSize), S(Events), S(Operations), Status, S(Failures),
            S(PartialOutputs), S(IngressRemainders), S(IngressDivisions), S(NativeBankNands), S(ModeledBinaryNands),
            S(CofactorAdds), S(CofactorMultiplies), S(CofactorDivides), S(CofactorRemainders), S(CofactorGcds),
            S(MaintenanceRemainders), S(MaintenanceDivisions), S(Migrations), S(EgressReconstructionMultiplies),
            S(LaneReads), S(LaneWrites), S(AveragePayloadBits), S(CatalogBits), OutputObligation, Interpretation,
        ];

        public static PilotRow Unsupported(PilotWorkload workload, string implementation, string policy, int bankSize, string reason) =>
            new(
                workload.Family, workload.Id, implementation, policy, bankSize, workload.Events.Count, 0,
                "NOT_SUPPORTED", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                workload.OutputObligation, reason);
    }

    private sealed record BankRow(
        string Family,
        string Policy,
        int BankSize,
        long Samples,
        long CofactorBits,
        long ExponentBits,
        long MetadataBits,
        double AverageNonzeroLanes,
        long CatalogBits,
        long TrialRemainders,
        long FactorDivisions,
        long BankValidationTrialDivisions,
        string BankId)
    {
        public static readonly string[] Headers =
        [
            "family", "policy", "bank_size", "samples", "average_cofactor_bits", "exponent_bits_per_value",
            "metadata_bits_per_value", "average_nonzero_lanes", "catalog_bits_unamortized", "trial_remainders",
            "factor_divisions", "bank_validation_trial_divisions", "bank_id",
        ];

        public string[] ToCsv() =>
        [
            Family, Policy, S(BankSize), S(Samples), S(CofactorBits), S(ExponentBits), S(MetadataBits),
            AverageNonzeroLanes.ToString("F4", CultureInfo.InvariantCulture), S(CatalogBits), S(TrialRemainders),
            S(FactorDivisions), S(BankValidationTrialDivisions), BankId,
        ];
    }

    private sealed record FreshnessRow(
        int BankSize,
        long Pairs,
        long ExactLanes,
        long UnknownLanes,
        long PartialValues,
        long RefreshRemainders,
        long RefreshDivisions,
        long NativeNands,
        long CofactorAdds,
        string Rule)
    {
        public static readonly string[] Headers =
        [
            "bank_size", "pairs", "exact_lanes_after_add", "unknown_lanes_after_add", "partial_values",
            "eager_refresh_remainders", "eager_refresh_divisions", "native_bank_nands", "cofactor_adds", "rule",
        ];

        public string[] ToCsv() =>
        [
            S(BankSize), S(Pairs), S(ExactLanes), S(UnknownLanes), S(PartialValues), S(RefreshRemainders),
            S(RefreshDivisions), S(NativeNands), S(CofactorAdds), Rule,
        ];
    }

    private sealed record MigrationRow(
        int BankSize,
        long WorkingSetPrimes,
        long PolicyChanges,
        long LiveValues,
        long PerValueMigrations,
        long Remainders,
        long Divisions,
        long CofactorMultiplications,
        long LaneReads,
        long LaneWrites,
        long ExactnessFailures,
        string Scenario)
    {
        public static readonly string[] Headers =
        [
            "bank_size", "working_set_primes", "policy_changes", "live_values", "per_value_migrations",
            "remainder_tests", "factor_divisions", "cofactor_multiplications", "lane_reads", "lane_writes",
            "exactness_failures", "scenario",
        ];

        public string[] ToCsv() =>
        [
            S(BankSize), S(WorkingSetPrimes), S(PolicyChanges), S(LiveValues), S(PerValueMigrations), S(Remainders),
            S(Divisions), S(CofactorMultiplications), S(LaneReads), S(LaneWrites), S(ExactnessFailures), Scenario,
        ];
    }

    private sealed record CrossoverRow(
        int BankSize,
        int Reuse,
        long BinaryWorkProxy,
        long HybridWorkProxy,
        long BinaryPayloadBits,
        long HybridPayloadBits,
        bool HybridWorkLower,
        bool PayloadWithinTwoX,
        string Ceiling)
    {
        public static readonly string[] Headers =
        [
            "bank_size", "reuse_q", "binary_work_proxy_nands", "hybrid_work_proxy_nands", "binary_payload_bits",
            "hybrid_payload_bits", "hybrid_work_lower", "payload_within_2x", "claim_ceiling",
        ];

        public string[] ToCsv() =>
        [
            S(BankSize), S(Reuse), S(BinaryWorkProxy), S(HybridWorkProxy), S(BinaryPayloadBits), S(HybridPayloadBits),
            S(HybridWorkLower), S(PayloadWithinTwoX), Ceiling,
        ];
    }
}
