using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Cli;

internal sealed record Build002ExperimentReceipt(
    string OutputDirectory,
    long CheckCount,
    long FailureCount,
    string Classification);

/// <summary>
/// Deterministic non-HDL evidence generator for the frozen Build 002 protocol.
/// Missing integrated circuits remain explicit NOT_MEASURED rows; negative
/// sentinels are never interpreted as zero-cost measurements.
/// </summary>
internal static class Build002ExperimentRunner
{
    private const long NotMeasured = -1;
    private const string PartialClassification = "PARTIAL — FINAL DECISION NOT EARNED";
    private static readonly string[] GeneratedRelativePaths =
    [
        "correctness.json",
        "static_costs.csv",
        "dynamic_operations.csv",
        "workload_matrix.csv",
        "ingress_egress.csv",
        "representation_search.csv",
        "addition_adversary.csv",
        "hostile_support.csv",
        "protocol_coverage.json",
        "figures/static_gate_counts.svg",
        "figures/representation_bits.svg",
        "README.md",
    ];

    public static Build002ExperimentReceipt Run(
        string repositoryRoot,
        string outputDirectory,
        string invocationOutputArgument,
        IReadOnlyList<string>? sanitizedHdlSummaryPaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationOutputArgument);
        Build002Protocol.VerifyFrozenPlan(repositoryRoot);

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(Path.Combine(outputDirectory, "figures"));
        var correctness = new CorrectnessAccumulator();
        var staticRows = BuildStaticCosts();
        var dynamic = BuildDynamicOperations(correctness);
        RunSemanticCorrectness(correctness);
        var workloadRows = BuildWorkloads(dynamic, correctness);
        var ingressEgressRows = BuildIngressEgress();
        var representationRows = BuildRepresentationSearch();
        var additionRows = BuildAdditionAdversary();
        var hostileRows = BuildHostileSupport();
        var hdlSummaries = LoadSanitizedHdlSummaries(sanitizedHdlSummaryPaths);

        WriteCsv(
            Path.Combine(outputDirectory, "static_costs.csv"),
            Build002StaticCostRow.Headers,
            staticRows.Select(row => row.ToCsv()));
        WriteCsv(
            Path.Combine(outputDirectory, "dynamic_operations.csv"),
            Build002DynamicOperationRow.Headers,
            dynamic.Rows.Select(row => row.ToCsv()));
        WriteCsv(
            Path.Combine(outputDirectory, "workload_matrix.csv"),
            Build002WorkloadRow.Headers,
            workloadRows.Select(row => row.ToCsv()));
        WriteCsv(
            Path.Combine(outputDirectory, "ingress_egress.csv"),
            Build002IngressEgressRow.Headers,
            ingressEgressRows.Select(row => row.ToCsv()));
        WriteCsv(
            Path.Combine(outputDirectory, "representation_search.csv"),
            Build002RepresentationRow.Headers,
            representationRows.Select(row => row.ToCsv()));
        WriteCsv(
            Path.Combine(outputDirectory, "addition_adversary.csv"),
            Build002AdditionRow.Headers,
            additionRows.Select(row => row.ToCsv()));
        WriteCsv(
            Path.Combine(outputDirectory, "hostile_support.csv"),
            Build002HostileRow.Headers,
            hostileRows.Select(row => row.ToCsv()));

        var correctnessReceipt = correctness.ToReceipt();
        Build002Protocol.WriteJson(
            Path.Combine(outputDirectory, "correctness.json"),
            correctnessReceipt);
        Build002Protocol.WriteJson(
            Path.Combine(outputDirectory, "protocol_coverage.json"),
            BuildCoverage(hdlSummaries));
        WriteStaticGateFigure(
            Path.Combine(outputDirectory, "figures", "static_gate_counts.svg"),
            staticRows);
        WriteRepresentationFigure(
            Path.Combine(outputDirectory, "figures", "representation_bits.svg"),
            representationRows);
        WriteReadme(
            Path.Combine(outputDirectory, "README.md"),
            invocationOutputArgument,
            correctnessReceipt,
            hdlSummaries);
        WriteManifest(
            repositoryRoot,
            outputDirectory,
            invocationOutputArgument,
            hdlSummaries,
            correctnessReceipt);

        return new Build002ExperimentReceipt(
            outputDirectory,
            correctnessReceipt.CheckCount,
            correctnessReceipt.FailureCount,
            PartialClassification);
    }

    private static List<Build002StaticCostRow> BuildStaticCosts()
    {
        var rows = new List<Build002StaticCostRow>();
        foreach (var width in Build002Protocol.Widths)
        {
            AddBaseline("BIN-FU", width, "ADD", BaselineHardware.BuildRippleAdder(width));
            AddBaseline("BIN-FU", width, "SUBTRACT", BaselineHardware.BuildRippleSubtractor(width));
            AddBaseline("BIN-FU", width, "COMPARE", BaselineHardware.BuildComparator(width));
            AddBaseline("BIN-FU", width, "MULTIPLY", BaselineHardware.BuildShiftAddMultiplier(width));
            AddBaseline("BIN-FU", width, "FUNCTIONAL_UNIT", BaselineHardware.BuildFunctionalUnit(width));
            AddBaseline(
                "BIN-FU-INTEGRATED",
                width,
                "REGISTERED_FUNCTIONAL_UNIT",
                BaselineHardware.BuildRegisteredFunctionalUnit(width));
            AddBaseline(
                "BIN-DIV",
                width,
                "UNSIGNED_RESTORING_DIVIDE",
                BaselineAlgorithmHardware.BuildUnsignedRestoringDivider(width));
            AddBaseline(
                "BIN-GCD",
                width,
                "SUBTRACTIVE_GCD_STEP",
                BaselineAlgorithmHardware.BuildSubtractiveGcdMachine(width));

            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentCompose(width));
            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentCancel(width));
            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentMeet(width));
            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentJoin(width));
            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentDivides(width));
            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentFunctionalUnit(width));
            AddExperimental("VFU-THERM-S4", ExperimentalHardware.BuildThermometerCompose(width));
            AddExperimental("VFU-THERM-S4", ExperimentalHardware.BuildThermometerMeet(width));
            AddExperimental("VFU-THERM-S4", ExperimentalHardware.BuildThermometerJoin(width));
            AddExperimental("VFU-THERM-S4", ExperimentalHardware.BuildThermometerDivides(width));
            AddExperimental("VFU-THERM-S4", ExperimentalHardware.BuildThermometerCanonicalValidator(width));
            AddExperimental(
                "VFU-THERM-S4",
                ExperimentalHardware.BuildThermometerThresholdQuery(width, 2, 1));
        }

        return rows;

        void AddBaseline(string implementation, int width, string operation, NandNetlist netlist)
        {
            rows.Add(new Build002StaticCostRow(
                implementation,
                width,
                netlist.Name,
                operation,
                "BINARY_POSITIONAL",
                "STRUCTURAL_DECLARED",
                "IMPLEMENTED",
                netlist.Metrics,
                "Explicit NAND graph; DFFs, where present, are separately charged boundary cells."));
        }

        void AddExperimental(string implementation, DeclaredExperimentalCircuit circuit)
        {
            rows.Add(new Build002StaticCostRow(
                implementation,
                circuit.Ports.Width,
                circuit.Netlist.Name,
                circuit.Ports.Operation.ToString().ToUpperInvariant(),
                circuit.Ports.Encoding.ToString().ToUpperInvariant(),
                circuit.EvidenceClass,
                "IMPLEMENTED_OPERATION_ONLY",
                circuit.Netlist.Metrics,
                "No operand/result DFF boundary or binary acquisition/reconstruction adapter is included."));
        }
    }

    private static DynamicEvidence BuildDynamicOperations(CorrectnessAccumulator correctness)
    {
        var rows = new List<Build002DynamicOperationRow>();
        var measurements = new Dictionary<string, OperationMeasurement>(StringComparer.Ordinal);
        foreach (var width in Build002Protocol.Widths)
        {
            var magnitudeLimit = 1 << width;
            var multiplier = BaselineHardware.BuildShiftAddMultiplier(width);
            var multiply = MeasureCircuit(
                multiplier,
                BinaryPairInputs(width),
                (index, evaluation) =>
                {
                    var left = (int)(index / magnitudeLimit);
                    var right = (int)(index % magnitudeLimit);
                    correctness.Check(
                        ReadWord(evaluation.Outputs, "product", width * 2) == left * right,
                        $"A/BIN-MULTIPLY/W{width}/{left}/{right}");
                });
            AddMeasured(
                $"A/BIN-MULTIPLY/W{width}",
                "BIN-FU",
                width,
                "MULTIPLY",
                "COLD_MAG",
                "MAGNITUDE_FINAL",
                "STRUCTURAL_DECLARED_OPERATION_ONLY",
                "FULL_2W_PRODUCT",
                multiply,
                "Operand loading and result registers are not included in this operation-specific row.");

            var divider = BaselineAlgorithmHardware.BuildUnsignedRestoringDivider(width);
            var divide = MeasureCircuit(
                divider,
                DividerPairInputs(width),
                (index, evaluation) =>
                {
                    var dividend = (int)(index / magnitudeLimit);
                    var divisor = (int)(index % magnitudeLimit);
                    var quotient = ReadWord(evaluation.Outputs, "quotient", width);
                    var remainder = ReadWord(evaluation.Outputs, "remainder", width);
                    var correct = divisor == 0
                        ? quotient == 0 && remainder == dividend &&
                          IsOn(evaluation.Outputs["divide_by_zero"]) &&
                          !IsOn(evaluation.Outputs["exact"])
                        : quotient == dividend / divisor && remainder == dividend % divisor &&
                          !IsOn(evaluation.Outputs["divide_by_zero"]) &&
                          IsOn(evaluation.Outputs["exact"]) == (dividend % divisor == 0);
                    correctness.Check(correct, $"C/BIN-DIVIDE/W{width}/{dividend}/{divisor}");
                });
            AddMeasured(
                $"C/BIN-DIVIDE/W{width}",
                "BIN-DIV",
                width,
                "DIVIDE_AND_EXACT",
                "COLD_MAG",
                "PREDICATE_ONLY",
                "STRUCTURAL_DECLARED",
                "FULL_BINARY",
                divide,
                "Full quotient/remainder/status circuit is charged even though only exactness is required.");

            var structuralStates = SmoothStates(width);
            AddStructuralOperation(
                "A",
                "VFU-BINEXP-S4",
                ExperimentalHardware.BuildBinaryExponentCompose(width),
                structuralStates,
                "STRUCTURAL_FINAL",
                correctness);
            AddStructuralOperation(
                "A",
                "VFU-THERM-S4",
                ExperimentalHardware.BuildThermometerCompose(width),
                structuralStates,
                "STRUCTURAL_FINAL",
                correctness);
            AddStructuralOperation(
                "C",
                "VFU-BINEXP-S4",
                ExperimentalHardware.BuildBinaryExponentMeet(width),
                structuralStates,
                "STRUCTURAL_FINAL",
                correctness);
            AddStructuralOperation(
                "C",
                "VFU-BINEXP-S4",
                ExperimentalHardware.BuildBinaryExponentJoin(width),
                structuralStates,
                "STRUCTURAL_FINAL",
                correctness);
            AddStructuralOperation(
                "C",
                "VFU-BINEXP-S4",
                ExperimentalHardware.BuildBinaryExponentDivides(width),
                structuralStates,
                "PREDICATE_ONLY",
                correctness);

            var gcdNetlist = BaselineAlgorithmHardware.BuildSubtractiveGcdMachine(width);
            long gcdCases = 0;
            long gcdCycles = 0;
            for (var left = 0; left < magnitudeLimit; left++)
            {
                for (var right = 0; right < magnitudeLimit; right++)
                {
                    var receipt = BaselineAlgorithmHardware.SimulateSubtractiveGcd(width, left, right);
                    correctness.Check(
                        receipt.Result == OracleGcd(left, right),
                        $"C/BIN-GCD/W{width}/{left}/{right}");
                    gcdCases++;
                    gcdCycles += receipt.Cycles;
                }
            }

            var gcd = new OperationMeasurement(
                gcdCases,
                gcdCycles,
                checked(gcdCycles * gcdNetlist.Metrics.Nand2Static),
                NotMeasured,
                NotMeasured,
                NotMeasured,
                NotMeasured);
            AddMeasured(
                $"C/BIN-GCD/W{width}",
                "BIN-GCD",
                width,
                "GCD",
                "COLD_MAG",
                "MAGNITUDE_FINAL",
                "SEMANTIC_STEP_PLUS_STRUCTURAL_DECLARED",
                "FULL_BINARY_TRANSITIONS_NOT_MEASURED",
                gcd,
                "Cycles execute the exhaustively checked NAND transition; settled transition totals were not replayed end-to-end.");

            rows.Add(NotMeasuredDynamicRow(
                "BIN+VSC-S4",
                width,
                "ENCODE_AND_QUERY",
                "COLD_MAG",
                "PREDICATE_ONLY",
                magnitudeLimit,
                "Exact sidecar semantics exist, but no integrated sidecar acquisition/query NAND circuit is implemented."));
            rows.Add(NotMeasuredDynamicRow(
                "VFU-BINEXP-S4",
                width,
                "ENCODE_COMPOSE_RECONSTRUCT",
                "COLD_MAG",
                "MAGNITUDE_FINAL",
                structuralStates.Count * structuralStates.Count,
                "Cold acquisition and reconstruction NAND adapters are not implemented; native compose is reported separately."));
        }

        return new DynamicEvidence(rows, measurements);

        void AddMeasured(
            string key,
            string implementation,
            int width,
            string operation,
            string regime,
            string obligation,
            string evidenceClass,
            string support,
            OperationMeasurement measurement,
            string notes)
        {
            measurements[key] = measurement;
            rows.Add(new Build002DynamicOperationRow(
                implementation,
                width,
                operation,
                regime,
                obligation,
                evidenceClass,
                support,
                measurement.Cases,
                measurement.Cases,
                measurement.Cycles,
                measurement.NandEvaluations,
                measurement.NandOutputTransitions,
                measurement.StateBitTransitions,
                measurement.InputBitTransitions,
                measurement.InitialNandTransitions,
                0,
                0,
                0,
                0,
                notes));
        }

        void AddStructuralOperation(
            string experiment,
            string implementation,
            DeclaredExperimentalCircuit circuit,
            IReadOnlyList<ValuationHardwareState> states,
            string obligation,
            CorrectnessAccumulator accumulator)
        {
            var measurement = MeasureCircuit(
                circuit.Netlist,
                StructuralPairInputs(circuit, states),
                (index, evaluation) =>
                {
                    var left = states[(int)(index / states.Count)];
                    var right = states[(int)(index % states.Count)];
                    CheckExperimentalEvaluation(circuit, evaluation, left, right, accumulator);
                });
            var key = $"{experiment}/{implementation}/{circuit.Ports.Operation}/W{circuit.Ports.Width}";
            AddMeasured(
                key,
                implementation,
                circuit.Ports.Width,
                circuit.Ports.Operation.ToString().ToUpperInvariant(),
                "WARM_RESIDENT",
                obligation,
                circuit.EvidenceClass,
                "S4_SMOOTH_ONLY",
                measurement,
                "Native operation only; common-domain smooth states, no acquisition/reconstruction or state DFFs.");
        }
    }

    private static void RunSemanticCorrectness(CorrectnessAccumulator correctness)
    {
        foreach (var width in Build002Protocol.Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            for (var magnitude = 0; magnitude <= domain.MaximumMagnitude; magnitude++)
            {
                var encoded = BinaryValuationSidecar.Encode(width, magnitude);
                correctness.Check(
                    encoded.Succeeded && encoded.Value!.Magnitude == magnitude && encoded.Value.Valid,
                    $"SIDECAR/ENCODE/W{width}/{magnitude}");
                if (!encoded.Succeeded)
                {
                    continue;
                }

                foreach (var prime in ValuationHardwareDomain.S4)
                {
                    var expected = magnitude == 0 ? 0 : Valuation(magnitude, prime);
                    var actual = encoded.Value!.Valuation(prime);
                    correctness.Check(
                        actual.Succeeded && actual.Value!.LowerBound == expected && actual.Value.IsExact &&
                        actual.Value.IsPositiveInfinity == (magnitude == 0),
                        $"SIDECAR/VALUATION/W{width}/{magnitude}/P{prime}");
                }
            }

            foreach (var state in SmoothStates(width))
            {
                var reconstructed = state.Reconstruct();
                correctness.Check(
                    reconstructed.Succeeded && reconstructed.Value!.Value >= BigInteger.Zero &&
                    reconstructed.Value.Value <= domain.MaximumMagnitude,
                    $"STRUCTURAL/RECONSTRUCT/W{width}/{FormatState(state)}");
            }

            var malformedZero = ValuationHardwareState.Create(width, true, [1, 0, 0, 0]);
            correctness.Check(
                !malformedZero.Succeeded && malformedZero.Failure == ValuationStateFailure.NonCanonicalEncoding,
                $"STRUCTURAL/MALFORMED_ZERO/W{width}");
            var one = BinaryValuationSidecar.Encode(width, 1).Value!;
            var rejected = one.CancelKnownFactor(2);
            correctness.Check(
                !rejected.Succeeded && rejected.Failure == ValuationStateFailure.NotDivisible && one.Magnitude == 1,
                $"SIDECAR/ATOMIC_CANCEL/W{width}");
        }
    }

    private static List<Build002WorkloadRow> BuildWorkloads(
        DynamicEvidence dynamic,
        CorrectnessAccumulator correctness)
    {
        var rows = new List<Build002WorkloadRow>();
        foreach (var width in Build002Protocol.Widths)
        {
            AddMeasurement(
                "A",
                $"A-W{width}-BIN-MULTIPLY-ALL",
                "BIN-FU",
                width,
                "COLD_MAG",
                "MAGNITUDE_FINAL",
                "EXECUTE",
                "STRUCTURAL_DECLARED_OPERATION_ONLY",
                "FULL_2W_PRODUCT",
                dynamic.Measurements[$"A/BIN-MULTIPLY/W{width}"],
                "all ordered W-bit magnitude pairs",
                "Operation-only result; registered integrated cost is reported separately in static_costs.csv.");
            AddMeasurement(
                "A",
                $"A-W{width}-BINEXP-COMPOSE-SMOOTH",
                "VFU-BINEXP-S4",
                width,
                "WARM_RESIDENT",
                "STRUCTURAL_FINAL",
                "EXECUTE",
                "STRUCTURAL_DECLARED",
                "S4_SMOOTH_ONLY",
                dynamic.Measurements[$"A/VFU-BINEXP-S4/Compose/W{width}"],
                "all ordered common-domain S4-smooth states",
                "No magnitude requested; this row is not directly compared with MAGNITUDE_FINAL.");
            AddMeasurement(
                "A",
                $"A-W{width}-THERM-COMPOSE-SMOOTH",
                "VFU-THERM-S4",
                width,
                "WARM_RESIDENT",
                "STRUCTURAL_FINAL",
                "EXECUTE",
                "STRUCTURAL_DECLARED",
                "S4_SMOOTH_ONLY",
                dynamic.Measurements[$"A/VFU-THERM-S4/Compose/W{width}"],
                "all ordered common-domain S4-smooth states",
                "Direct threshold convolution; no binary acquisition/reconstruction or DFF boundary.");

            foreach (var trace in Build002Workloads.RepeatedScaleCancel(width))
            {
                var execution = RunStructuralTrace(trace, correctness);
                AddStructuralTraceRows(rows, trace, execution);
            }

            rows.Add(NotMeasuredWorkload(
                "B",
                $"B-W{width}-BIN-INTEGRATED",
                "BIN-FU",
                width,
                "WARM_GENERATED",
                "MAGNITUDE_FINAL",
                "EXECUTE",
                32,
                "FULL_BINARY",
                "Per-operation multiplier/divider circuits exist, but the atomic W-bit overflow/rejection controller is not integrated."));

            AddMeasurement(
                "C",
                $"C-W{width}-BIN-GCD-ALL",
                "BIN-GCD",
                width,
                "COLD_MAG",
                "MAGNITUDE_FINAL",
                "EXECUTE",
                "SEMANTIC_STEP_PLUS_STRUCTURAL_DECLARED",
                "FULL_BINARY_TRANSITIONS_NOT_MEASURED",
                dynamic.Measurements[$"C/BIN-GCD/W{width}"],
                "all ordered W-bit magnitude pairs",
                "Cycle/NAND-evaluation totals are exact for the checked transition; settled transition replay is NOT_MEASURED.");
            AddMeasurement(
                "C",
                $"C-W{width}-BINEXP-MEET-SMOOTH",
                "VFU-BINEXP-S4",
                width,
                "WARM_RESIDENT",
                "STRUCTURAL_FINAL",
                "EXECUTE",
                "STRUCTURAL_DECLARED",
                "S4_SMOOTH_ONLY",
                dynamic.Measurements[$"C/VFU-BINEXP-S4/Meet/W{width}"],
                "S4-smooth GCD as componentwise meet",
                "This is not a full binary GCD for unsupported cofactors.");
            AddMeasurement(
                "C",
                $"C-W{width}-BINEXP-JOIN-SMOOTH",
                "VFU-BINEXP-S4",
                width,
                "WARM_RESIDENT",
                "STRUCTURAL_FINAL",
                "EXECUTE",
                "STRUCTURAL_DECLARED",
                "S4_SMOOTH_ONLY",
                dynamic.Measurements[$"C/VFU-BINEXP-S4/Join/W{width}"],
                "S4-smooth LCM as componentwise join",
                "This is not a full binary LCM for unsupported cofactors.");
            AddMeasurement(
                "C",
                $"C-W{width}-BINEXP-DIVIDES-SMOOTH",
                "VFU-BINEXP-S4",
                width,
                "WARM_RESIDENT",
                "PREDICATE_ONLY",
                "EXECUTE",
                "STRUCTURAL_DECLARED",
                "S4_SMOOTH_ONLY",
                dynamic.Measurements[$"C/VFU-BINEXP-S4/Divides/W{width}"],
                "S4-smooth divisibility predicate",
                "Same predicate-only obligation as the charged binary divider row.");

            foreach (var rational in Build002Workloads.RationalReduction(width))
            {
                rows.Add(RunBinaryRational(rational, correctness));
                rows.Add(NotMeasuredWorkload(
                    "D",
                    rational.Id + "-STRUCTURAL",
                    "VFU-BINEXP-S4",
                    width,
                    "WARM_RESIDENT",
                    "STRUCTURAL_FINAL",
                    "EXECUTE",
                    1,
                    "CATALOG_PROJECTION_ONLY",
                    "Catalog reduction semantics are available, but an integrated rational register/controller was not built."));
            }

            foreach (var trace in Build002Workloads.MixedAddition(width))
            {
                rows.Add(RunSidecarMixedTrace(trace, correctness));
            }

            rows.Add(NotMeasuredWorkload(
                "F",
                $"F-W{width}-HOSTILE-TRACES",
                "BIN+VSC-S4",
                width,
                "WARM_GENERATED",
                "MAGNITUDE_EVERY_OP",
                "EXECUTE",
                Build002Workloads.HostileValues(width).Count,
                "AUTHORITATIVE_MAGNITUDE_SEMANTICS_ONLY",
                "Hostile values and state overhead are recorded, but frequent-addition/reconstruction/support-thrash hardware traces are NOT_MEASURED."));
            rows.Add(NotMeasuredWorkload(
                "R",
                $"R-W{width}-CONVERTERS",
                "REPRESENTATION-SEARCH",
                width,
                "COLD_MAG",
                "STRUCTURAL_FINAL",
                "INGRESS",
                1,
                "PARTIAL_REPRESENTATION_SET",
                "State-bit geometry and native circuits are measured; representation converters and presence-only hardware are NOT_MEASURED."));
        }

        return rows;

        void AddMeasurement(
            string experiment,
            string traceId,
            string implementation,
            int width,
            string regime,
            string obligation,
            string phase,
            string evidenceClass,
            string support,
            OperationMeasurement measurement,
            string feature,
            string notes)
        {
            rows.Add(new Build002WorkloadRow(
                experiment,
                traceId,
                implementation,
                width,
                regime,
                obligation,
                phase,
                evidenceClass,
                support,
                checked((int)measurement.Cases),
                measurement.Cycles,
                measurement.NandEvaluations,
                measurement.NandOutputTransitions,
                measurement.StateBitTransitions,
                0,
                0,
                0,
                0,
                string.Empty,
                feature,
                notes));
        }
    }

    private static void AddStructuralTraceRows(
        List<Build002WorkloadRow> rows,
        Build002Trace trace,
        StructuralTraceExecution execution)
    {
        foreach (var obligation in new[] { "STRUCTURAL_FINAL", "MAGNITUDE_FINAL", "MAGNITUDE_EVERY_OP" })
        {
            rows.Add(new Build002WorkloadRow(
                "B",
                trace.Id,
                "VFU-BINEXP-S4",
                trace.Width,
                "WARM_GENERATED",
                obligation,
                "EXECUTE",
                "STRUCTURAL_DECLARED_OPERATION_ONLY",
                "S4_GENERATED_STATE_PERSISTENT_DFF_NOT_CHARGED",
                trace.Steps.Count,
                execution.Measurement.Cycles,
                execution.Measurement.NandEvaluations,
                execution.Measurement.NandOutputTransitions,
                execution.Measurement.StateBitTransitions,
                execution.Rejections,
                0,
                0,
                0,
                FormatState(execution.FinalState),
                trace.Feature,
                "Shared VFU graph measured; persistent DFF and atomic hold mux are not integrated."));

            if (obligation == "STRUCTURAL_FINAL")
            {
                continue;
            }

            var reconstructs = obligation == "MAGNITUDE_FINAL" ? 1 : trace.Steps.Count;
            rows.Add(new Build002WorkloadRow(
                "B",
                trace.Id,
                "VFU-BINEXP-S4",
                trace.Width,
                "WARM_GENERATED",
                obligation,
                "EGRESS",
                "NOT_MEASURED",
                "RECONSTRUCTION_CIRCUIT_MISSING",
                0,
                NotMeasured,
                NotMeasured,
                NotMeasured,
                NotMeasured,
                0,
                0,
                reconstructs,
                0,
                string.Empty,
                trace.Feature,
                "-1 cost fields mean NOT_MEASURED, not zero cost."));
        }
    }

    private static StructuralTraceExecution RunStructuralTrace(
        Build002Trace trace,
        CorrectnessAccumulator correctness)
    {
        var circuit = ExperimentalHardware.BuildBinaryExponentFunctionalUnit(trace.Width);
        var current = ValuationHardwareState.Identity(trace.Width);
        NandEvaluation? previous = null;
        long nandEvaluations = 0;
        long nandTransitions = 0;
        long initialTransitions = 0;
        long inputTransitions = 0;
        long stateTransitions = 0;
        var rejections = 0;
        for (var index = 0; index < trace.Steps.Count; index++)
        {
            var step = trace.Steps[index];
            var factor = ValuationHardwareState.Power(trace.Width, step.Operand, 1).Value!;
            var opcode = step.Operation switch
            {
                Build002TraceOperation.ScaleKnownFactor => BinaryExponentFuOperation.Compose,
                Build002TraceOperation.CancelKnownFactor => BinaryExponentFuOperation.Cancel,
                _ => throw new InvalidOperationException("Experiment B contains only scale/cancel steps."),
            };
            var inputs = ExperimentalInputs(circuit, current, factor, (int)opcode);
            var evaluated = circuit.Netlist.Evaluate(
                inputs,
                previous: previous,
                compareWithAllOff: previous is null);
            var semantic = step.Operation == Build002TraceOperation.ScaleKnownFactor
                ? current.Compose(factor)
                : current.Cancel(factor);
            var rejected = !semantic.Succeeded;
            correctness.Check(
                IsOn(evaluated.Outputs[circuit.Ports.RejectOutput!]) == rejected,
                $"B/FU/STATUS/{trace.Id}/{index}");
            if (rejected)
            {
                rejections++;
                CheckRejectedOutputs(circuit, evaluated, correctness, $"B/FU/ATOMIC/{trace.Id}/{index}");
            }
            else
            {
                var next = semantic.Value!;
                CheckExperimentalResult(circuit, evaluated, next, correctness, $"B/FU/RESULT/{trace.Id}/{index}");
                stateTransitions += CountStateTransitions(current, next, circuit.Ports);
                current = next;
            }

            nandEvaluations += evaluated.NandEvaluations;
            inputTransitions += evaluated.InputTransitions;
            if (previous is null)
            {
                initialTransitions += evaluated.NandOutputTransitions;
            }
            else
            {
                nandTransitions += evaluated.NandOutputTransitions;
            }

            previous = evaluated;
        }

        correctness.Check(
            rejections == trace.ExpectedRejectedCancellations,
            $"B/REJECTION_COUNT/{trace.Id}");
        return new StructuralTraceExecution(
            current,
            rejections,
            new OperationMeasurement(
                trace.Steps.Count,
                trace.Steps.Count,
                nandEvaluations,
                nandTransitions,
                stateTransitions,
                inputTransitions,
                initialTransitions));
    }

    private static Build002WorkloadRow RunBinaryRational(
        Build002RationalCase rational,
        CorrectnessAccumulator correctness)
    {
        if (rational.Denominator == 0)
        {
            return new Build002WorkloadRow(
                "D", rational.Id, "BIN-GCD+DIV", rational.Width, "COLD_MAG", "MAGNITUDE_FINAL",
                "EXECUTE", "STRUCTURAL_DECLARED", "REJECTED_DENOMINATOR_ZERO", 1, 1, 0,
                NotMeasured, NotMeasured, 1, 0, 0, 0, string.Empty, rational.Feature,
                "Denominator-zero rejection is atomic; no divide circuit is executed.");
        }

        var gcdReceipt = BaselineAlgorithmHardware.SimulateSubtractiveGcd(
            rational.Width,
            rational.Numerator,
            rational.Denominator);
        var oracle = OracleGcd(rational.Numerator, rational.Denominator);
        correctness.Check(gcdReceipt.Result == oracle, $"D/GCD/{rational.Id}");
        var numerator = rational.Numerator / oracle;
        var denominator = rational.Denominator / oracle;
        correctness.Check(
            OracleGcd(numerator, denominator) == 1,
            $"D/REDUCED/{rational.Id}");
        var gcdNands = BaselineAlgorithmHardware.BuildSubtractiveGcdMachine(rational.Width)
            .Metrics.Nand2Static;
        var dividerNands = BaselineAlgorithmHardware.BuildUnsignedRestoringDivider(rational.Width)
            .Metrics.Nand2Static;
        var cycles = gcdReceipt.Cycles + 2;
        var evaluations = checked((gcdReceipt.Cycles * (long)gcdNands) + (2L * dividerNands));
        return new Build002WorkloadRow(
            "D",
            rational.Id,
            "BIN-GCD+DIV",
            rational.Width,
            "COLD_MAG",
            "MAGNITUDE_FINAL",
            "EXECUTE",
            "SEMANTIC_STEP_PLUS_STRUCTURAL_DECLARED",
            "FULLY_REDUCED_BINARY_TRANSITIONS_NOT_MEASURED",
            3,
            cycles,
            evaluations,
            NotMeasured,
            NotMeasured,
            0,
            0,
            0,
            0,
            $"{numerator}/{denominator}",
            rational.Feature,
            "GCD cycles and NAND evaluations are exact; end-to-end settled transitions are NOT_MEASURED.");
    }

    private static Build002WorkloadRow RunSidecarMixedTrace(
        Build002Trace trace,
        CorrectnessAccumulator correctness)
    {
        var encoded = BinaryValuationSidecar.Encode(trace.Width, trace.InitialMagnitude);
        var current = encoded.Value!;
        var rejections = 0;
        foreach (var step in trace.Steps)
        {
            ValuationStateResult<BinaryValuationSidecar> result;
            switch (step.Operation)
            {
                case Build002TraceOperation.ScaleKnownFactor:
                    result = current.ScaleKnownFactor(step.Operand);
                    break;
                case Build002TraceOperation.CancelKnownFactor:
                    result = current.CancelKnownFactor(step.Operand);
                    break;
                case Build002TraceOperation.AddMagnitude:
                    result = current.Add(BinaryValuationSidecar.Encode(trace.Width, step.Operand).Value!);
                    break;
                default:
                    throw new InvalidOperationException("Undefined mixed-trace instruction.");
            }

            if (!result.Succeeded)
            {
                rejections++;
                continue;
            }

            current = result.Value!;
        }

        correctness.Check(current.Magnitude >= 0, $"E/SIDECAR/EXACT_MAGNITUDE/{trace.Id}");
        return new Build002WorkloadRow(
            "E",
            trace.Id,
            "BIN+VSC-S4",
            trace.Width,
            "WARM_GENERATED",
            "MAGNITUDE_EVERY_OP",
                "EXECUTE",
                "SEMANTIC_ONLY",
                current.Valid
                    ? "AUTHORITATIVE_MAGNITUDE_SIDECAR_VALID_HARDWARE_COST_NOT_MEASURED"
                    : "AUTHORITATIVE_MAGNITUDE_SIDECAR_INVALID_HARDWARE_COST_NOT_MEASURED",
            trace.Steps.Count,
            NotMeasured,
            NotMeasured,
            NotMeasured,
            NotMeasured,
            rejections,
            1,
            0,
            0,
            current.Magnitude.ToString(CultureInfo.InvariantCulture),
            trace.Feature,
            "Magnitude/status semantics executed; integrated magnitude+sidecar NAND datapath is NOT_MEASURED.");
    }

    private static List<Build002IngressEgressRow> BuildIngressEgress()
    {
        var rows = new List<Build002IngressEgressRow>();
        foreach (var width in Build002Protocol.Widths)
        {
            var cases = 1 << width;
            var smoothCases = SmoothStates(width).Count;
            rows.Add(new Build002IngressEgressRow(
                "BIN-FU",
                width,
                "INGRESS",
                "DIRECT_W_BIT_WORD",
                "STRUCTURAL_DECLARED_BOUNDARY",
                cases,
                cases,
                0,
                0,
                "FULL_BINARY",
                "Direct nets use no combinational gates; equal registered boundaries remain charged in integrated static rows."));
            rows.Add(new Build002IngressEgressRow(
                "VFU-BINEXP-S4",
                width,
                "INGRESS",
                "BINARY_MAGNITUDE_TO_EXACT_S4",
                "NOT_MEASURED",
                cases,
                NotMeasured,
                NotMeasured,
                NotMeasured,
                "ACQUISITION_CIRCUIT_MISSING",
                "Semantic factoring is an oracle only; -1 fields are not zero-cost adapters."));
            rows.Add(new Build002IngressEgressRow(
                "VFU-BINEXP-S4",
                width,
                "EGRESS",
                "S4_STATE_TO_BINARY_MAGNITUDE",
                "NOT_MEASURED",
                smoothCases,
                NotMeasured,
                NotMeasured,
                NotMeasured,
                "RECONSTRUCTION_CIRCUIT_MISSING",
                "Semantic reconstruction is checked but no NAND reconstruction unit is implemented."));
            rows.Add(new Build002IngressEgressRow(
                "BIN+VSC-S4",
                width,
                "INGRESS",
                "EXACT_THRESHOLD_SIDECAR_ENCODE",
                "NOT_MEASURED",
                cases,
                NotMeasured,
                NotMeasured,
                NotMeasured,
                "SEMANTIC_ENCODER_ONLY",
                "All magnitude encodings are checked; acquisition hardware and its cycles remain unmeasured."));
        }

        return rows;
    }

    private static List<Build002RepresentationRow> BuildRepresentationSearch()
    {
        var rows = new List<Build002RepresentationRow>();
        foreach (var width in Build002Protocol.Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var exponentBits = domain.Caps.Sum(cap => BitsRequired(cap));
            var thresholdBits = domain.Caps.Sum();
            var payloadStates = domain.Caps.Aggregate(1, (product, cap) => checked(product * (cap + 1)));
            rows.Add(new Build002RepresentationRow(
                width,
                "BINARY_POSITIONAL",
                "ANALYTIC_EXACT_GEOMETRY",
                width,
                0,
                0,
                width,
                1 << width,
                "ADD;SUBTRACT;COMPARE;SHIFT",
                "MULTIPLICATIVE_STRUCTURE_NOT_LOCAL",
                "Authoritative magnitude baseline."));
            rows.Add(new Build002RepresentationRow(
                width,
                "BINARY_EXPONENT_S4",
                "ANALYTIC_EXACT_GEOMETRY",
                0,
                exponentBits,
                5,
                exponentBits + 5,
                payloadStates,
                "COMPOSE;CANCEL;MEET;JOIN;DIVIDES",
                "CARRIES_WITHIN_LANES_AND_ADAPTERS",
                "Tag bits are zero plus four per-lane saturation flags."));
            rows.Add(new Build002RepresentationRow(
                width,
                "THERMOMETER_S4",
                "ANALYTIC_EXACT_GEOMETRY",
                0,
                thresholdBits,
                5,
                thresholdBits + 5,
                payloadStates,
                "MEET;JOIN;DIVIDES;THRESHOLD_QUERY",
                "COMPOSE_CONVOLUTION_AND_STATE_BITS",
                "Canonical states form products of finite chains; malformed threshold strings are excluded."));
            rows.Add(new Build002RepresentationRow(
                width,
                "PRESENCE_ONLY_S4",
                "ANALYTIC_LOSSY_CONTROL",
                0,
                4,
                1,
                5,
                16,
                "PRIME_PRESENCE_QUERY",
                "LOSES_MULTIPLICITY",
                "Not equivalent to valuation state; circuit implementation is NOT_MEASURED."));
            rows.Add(new Build002RepresentationRow(
                width,
                "BINARY_PLUS_EXACT_THRESHOLD_SIDECAR_S4",
                "ANALYTIC_EXACT_GEOMETRY",
                width,
                thresholdBits,
                1,
                width + thresholdBits + 1,
                1 << width,
                "WARM_DIVISIBILITY;VALUATION_LOWER_BOUNDS",
                "STATIC_OVERHEAD_AND_REFRESH",
                "Magnitude is authoritative; valid is one explicit bit and zero is encoded in magnitude."));
        }

        return rows;
    }

    private static List<Build002AdditionRow> BuildAdditionAdversary()
    {
        var rows = new List<Build002AdditionRow>();
        foreach (var width in Build002Protocol.Widths)
        {
            var maximum = (1 << width) - 1;
            foreach (var trace in Build002Workloads.MixedAddition(width))
            {
                var left = trace.InitialMagnitude;
                var addend = 0;
                foreach (var step in trace.Steps)
                {
                    if (step.Operation == Build002TraceOperation.AddMagnitude)
                    {
                        addend = step.Operand;
                        break;
                    }

                    left = step.Operation switch
                    {
                        Build002TraceOperation.ScaleKnownFactor => left * step.Operand,
                        Build002TraceOperation.CancelKnownFactor => left / step.Operand,
                        _ => left,
                    };
                }

                var sum = left + addend;
                var overflow = sum > maximum;
                BinaryValuationSidecar? sidecar = null;
                if (!overflow)
                {
                    sidecar = BinaryValuationSidecar.Encode(width, left).Value!
                        .Add(BinaryValuationSidecar.Encode(width, addend).Value!).Value;
                }

                foreach (var prime in ValuationHardwareDomain.S4)
                {
                    var leftValuation = Valuation(left, prime);
                    var rightValuation = Valuation(addend, prime);
                    var exactValuation = Valuation(sum, prime);
                    var lowerBound = Math.Min(leftValuation, rightValuation);
                    var exactWithoutRefresh = left == 0 || addend == 0 || leftValuation != rightValuation;
                    rows.Add(new Build002AdditionRow(
                        width,
                        left,
                        addend,
                        sum,
                        prime.ToString(CultureInfo.InvariantCulture),
                        leftValuation,
                        rightValuation,
                        lowerBound,
                        exactValuation,
                        exactWithoutRefresh,
                        overflow
                            ? "OVERFLOW_REJECTED_ATOMIC"
                            : sidecar!.Valid
                                ? "VALID_EXACT_ALL_S4"
                                : "VALID_FALSE_LOWER_BOUNDS_ONLY",
                        trace.Id + ": " + trace.Feature +
                        (left == 0 || addend == 0 || sum == 0
                            ? "; zero is controlled by the explicit tag and valuation column 0 is only a tabular sentinel"
                            : string.Empty)));
                }
            }
        }

        return rows;
    }

    private static List<Build002HostileRow> BuildHostileSupport()
    {
        var rows = new List<Build002HostileRow>();
        foreach (var width in Build002Protocol.Widths)
        {
            var thresholdBits = ValuationHardwareDomain.ForWidth(width).Caps.Sum();
            foreach (var item in Build002Workloads.HostileValues(width))
            {
                Add(item.Magnitude, item.Kind);
            }

            var maximum = (1 << width) - 1;
            for (var magnitude = 0; magnitude <= Math.Min(15, maximum); magnitude++)
            {
                Add(magnitude, "SMALL_0_THROUGH_15");
            }

            void Add(int magnitude, string kind)
            {
                var smooth = TryEncodeSmooth(width, magnitude, out _);
                var thresholdsSet = magnitude == 0
                    ? 0
                    : ValuationHardwareDomain.S4.Sum(prime => Valuation(magnitude, prime));
                rows.Add(new Build002HostileRow(
                    width,
                    magnitude,
                    kind,
                    smooth ? "FULL_S4_STRUCTURAL" : "UNSUPPORTED_COFACTOR",
                    "FULL_EXACT_AUTHORITATIVE_MAGNITUDE",
                    thresholdsSet,
                    width + thresholdBits + 1,
                    smooth
                        ? "Pure structural state is exact on this value."
                        : "Pure S4 state cannot retain the unsupported cofactor; sidecar magnitude remains exact."));
            }
        }

        return rows;
    }

    private static object BuildCoverage(IReadOnlyList<HdlSummaryReceipt> hdlSummaries) =>
        new
        {
            ProtocolId = Build002Protocol.Id,
            Classification = PartialClassification,
            Widths = Build002Protocol.Widths,
            Coverage = new[]
            {
                new { Experiment = "A", Status = "PARTIAL_OPERATION_ONLY", Missing = "integrated cold encode/reconstruct and equal DFF boundaries" },
                new { Experiment = "B", Status = "PARTIAL_OPERATION_ONLY", Missing = "persistent VFU DFFs, atomic hold mux, binary integrated trace" },
                new { Experiment = "C", Status = "PARTIAL_CONTEXT_CONTROL", Missing = "sidecar query hardware and GCD settled transition replay" },
                new { Experiment = "D", Status = "PARTIAL_CONTEXT_CONTROL", Missing = "integrated rational controller and structural reducer" },
                new { Experiment = "E", Status = "PARTIAL_SEMANTIC_ONLY", Missing = "magnitude plus sidecar NAND datapath and refresh hardware" },
                new { Experiment = "F", Status = "PARTIAL_STATIC_AND_SEMANTIC", Missing = "frequent-addition, reconstruction, and support-thrash circuit traces" },
                new { Experiment = "R", Status = "PARTIAL_REPRESENTATION_SET", Missing = "converters, presence-only circuit, sparse/CAM candidate" },
            },
            NonHdlEvidence = new
            {
                StaticGraphs = "STRUCTURAL_DECLARED",
                DynamicCombinationalSequences = "SETTLED_TRANSITIONS",
                GcdCycles = "SEMANTIC_STEP_PLUS_EXHAUSTIVE_TRANSITION_TEST",
                MissingCostsUse = "-1 with NOT_MEASURED status; never zero",
            },
            HdlSummaries = hdlSummaries,
            DecisionEarned = false,
            Notes = "No Pareto or terminal classification is computed from this partial matrix.",
        };

    private static List<HdlSummaryReceipt> LoadSanitizedHdlSummaries(
        IReadOnlyList<string>? paths)
    {
        if (paths is null || paths.Count == 0)
        {
            return [];
        }

        var receipts = new List<HdlSummaryReceipt>();
        foreach (var path in paths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var name = Path.GetFileName(path);
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                receipts.Add(new HdlSummaryReceipt(
                    name,
                    Build002Protocol.HashFile(path),
                    ReadJsonString(root, "status") ?? "SUPPLIED",
                    ReadJsonString(root, "top") ?? string.Empty,
                    ReadJsonInt(root, "width"),
                    ReadJsonString(root, "architecture") ?? string.Empty,
                    "Only allowlisted scalar fields were imported; source paths and arbitrary JSON fields were omitted."));
            }
            catch (JsonException exception)
            {
                receipts.Add(new HdlSummaryReceipt(
                    name,
                    Build002Protocol.HashFile(path),
                    "INVALID_JSON",
                    string.Empty,
                    null,
                    string.Empty,
                    exception.GetType().Name));
            }
        }

        return receipts
            .OrderBy(receipt => receipt.Name, StringComparer.Ordinal)
            .ThenBy(receipt => receipt.Sha256, StringComparer.Ordinal)
            .ToList();
    }

    private static void WriteReadme(
        string path,
        string invocationOutputArgument,
        CorrectnessReceipt correctness,
        IReadOnlyList<HdlSummaryReceipt> hdlSummaries)
    {
        var command = $"dotnet run --project src/PrimeAxiom.Cli --configuration Release -- build002 --output {invocationOutputArgument}";
        var content = $$"""
            # Build 002 non-HDL evidence

            Protocol: `{{Build002Protocol.Id}}`

            Classification: `{{PartialClassification}}`

            This directory is generated evidence, not a completed hardware verdict. It preserves cold/warm source regimes, output obligations, phase costs, support restrictions, and missing integrated adapters as separate rows. A numeric cost of `-1` always means `NOT_MEASURED`; it never means zero cost and is excluded from comparison.

            ## Reproduce

            ```powershell
            {{command}}
            ```

            Master seed: `0x{{Build002Protocol.MasterSeed:X16}}`

            Runtime: `{{RuntimeInformation.FrameworkDescription}}`

            ## Evidence boundary

            - `correctness.json` records bounded executed checks and failures.
            - `static_costs.csv` contains declared NAND graphs only; it does not infer silicon area or frequency.
            - `dynamic_operations.csv` contains deterministic settled-vector sequences. Initial all-off transitions remain separate.
            - `workload_matrix.csv` keeps `INGRESS`, `EXECUTE`, `ADDITION_RECOVERY`, and `EGRESS` phases separate.
            - `ingress_egress.csv` makes missing acquisition/reconstruction circuits explicit.
            - `representation_search.csv` reports exact state-bit geometry as analytic evidence, not a synthesized circuit claim.
            - `addition_adversary.csv` and `hostile_support.csv` are semantic/support evidence; they do not manufacture sidecar hardware costs.
            - HDL summaries supplied: {{hdlSummaries.Count}}. They are allowlist-sanitized metadata only and do not alter non-HDL measurements.

            Correctness checks: {{correctness.CheckCount}}

            Correctness failures: {{correctness.FailureCount}}

            ## Why the decision is not earned

            Integrated sidecar hardware, structural acquisition/reconstruction, persistent VFU state/atomic rejection muxes, full mixed-addition circuits, complete adversarial traces, and the required HDL/formal matrix are not all measured. No universal scalar score or post-hoc weighted ranking is emitted.
            """;
        Build002Protocol.WriteLfText(path, content + "\n");
    }

    private static void WriteManifest(
        string repositoryRoot,
        string outputDirectory,
        string invocationOutputArgument,
        IReadOnlyList<HdlSummaryReceipt> hdlSummaries,
        CorrectnessReceipt correctness)
    {
        var files = GeneratedRelativePaths
            .Select(relative => new ManifestFile(
                relative,
                Build002Protocol.HashFile(Path.Combine(outputDirectory, relative.Replace('/', Path.DirectorySeparatorChar)))))
            .ToArray();
        var planPath = Path.Combine(repositoryRoot, "research", "build002_experiment_plan.md");
        var manifest = new
        {
            ProtocolId = Build002Protocol.Id,
            ProtocolBaselineCommit = Build002Protocol.BaselineCommit,
            FrozenPlanSha256 = Build002Protocol.HashFile(planPath),
            MasterSeed = $"0x{Build002Protocol.MasterSeed:X16}",
            GeneratorCommand = $"dotnet run --project src/PrimeAxiom.Cli --configuration Release -- build002 --output {invocationOutputArgument}",
            Runtime = RuntimeInformation.FrameworkDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Classification = PartialClassification,
            CorrectnessChecks = correctness.CheckCount,
            CorrectnessFailures = correctness.FailureCount,
            HdlSummaryInputs = hdlSummaries.Select(summary => new { summary.Name, summary.Sha256 }).ToArray(),
            Files = files,
            Notes = "manifest.json intentionally does not hash itself; paths are output-relative and slash-normalized.",
        };
        Build002Protocol.WriteJson(Path.Combine(outputDirectory, "manifest.json"), manifest);
    }

    private static void WriteStaticGateFigure(
        string path,
        IReadOnlyList<Build002StaticCostRow> rows)
    {
        var selected = rows
            .Where(row => row.Operation is "MULTIPLY" or "COMPOSE")
            .OrderBy(row => row.Width)
            .ThenBy(row => row.Implementation, StringComparer.Ordinal)
            .ToArray();
        var maximum = selected.Max(row => row.Metrics.Nand2Static);
        const int left = 80;
        const int chartHeight = 350;
        const int baselineY = 410;
        var barWidth = 70;
        var gap = 25;
        var builder = new StringBuilder();
        builder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1000\" height=\"520\" viewBox=\"0 0 1000 520\">");
        builder.AppendLine("<rect width=\"1000\" height=\"520\" fill=\"#fbfaf7\"/>");
        builder.AppendLine("<text x=\"50\" y=\"35\" font-family=\"sans-serif\" font-size=\"22\" fill=\"#20252b\">Declared operation-local NAND2 counts</text>");
        builder.AppendLine("<text x=\"50\" y=\"58\" font-family=\"sans-serif\" font-size=\"13\" fill=\"#59636e\">Different output contracts are shown, not ranked; adapters and equal state boundaries are excluded.</text>");
        for (var index = 0; index < selected.Length; index++)
        {
            var row = selected[index];
            var barHeight = Math.Max(1, row.Metrics.Nand2Static * chartHeight / maximum);
            var x = left + (index * (barWidth + gap));
            var y = baselineY - barHeight;
            var color = row.Implementation switch
            {
                "BIN-FU" => "#315f8c",
                "VFU-BINEXP-S4" => "#b45f35",
                _ => "#6b7f3a",
            };
            builder.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x}\" y=\"{y}\" width=\"{barWidth}\" height=\"{barHeight}\" fill=\"{color}\"/>\n");
            builder.Append(CultureInfo.InvariantCulture, $"<text x=\"{x + (barWidth / 2)}\" y=\"{y - 6}\" text-anchor=\"middle\" font-family=\"sans-serif\" font-size=\"11\">{row.Metrics.Nand2Static}</text>\n");
            builder.Append(CultureInfo.InvariantCulture, $"<text x=\"{x + (barWidth / 2)}\" y=\"430\" text-anchor=\"middle\" font-family=\"sans-serif\" font-size=\"10\">W{row.Width}</text>\n");
            builder.Append(CultureInfo.InvariantCulture, $"<text x=\"{x + (barWidth / 2)}\" y=\"445\" text-anchor=\"middle\" font-family=\"sans-serif\" font-size=\"9\">{EscapeXml(ShortImplementation(row.Implementation))}</text>\n");
        }

        builder.AppendLine("<line x1=\"60\" y1=\"410\" x2=\"970\" y2=\"410\" stroke=\"#20252b\"/>");
        builder.AppendLine("<text x=\"50\" y=\"495\" font-family=\"sans-serif\" font-size=\"11\" fill=\"#59636e\">Source: static_costs.csv; STRUCTURAL_DECLARED logical graph, not synthesis or silicon.</text>");
        builder.AppendLine("</svg>");
        Build002Protocol.WriteLfText(path, builder.ToString());
    }

    private static void WriteRepresentationFigure(
        string path,
        IReadOnlyList<Build002RepresentationRow> rows)
    {
        var selected = rows.OrderBy(row => row.Width).ThenBy(row => row.Representation, StringComparer.Ordinal).ToArray();
        var maximum = selected.Max(row => row.TotalStateBits);
        const int barWidth = 48;
        const int gap = 20;
        const int left = 45;
        const int chartHeight = 300;
        const int baselineY = 365;
        var builder = new StringBuilder();
        builder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1100\" height=\"500\" viewBox=\"0 0 1100 500\">");
        builder.AppendLine("<rect width=\"1100\" height=\"500\" fill=\"#fbfaf7\"/>");
        builder.AppendLine("<text x=\"40\" y=\"34\" font-family=\"sans-serif\" font-size=\"22\" fill=\"#20252b\">Representation state-bit geometry</text>");
        builder.AppendLine("<text x=\"40\" y=\"57\" font-family=\"sans-serif\" font-size=\"13\" fill=\"#59636e\">Analytic exact geometry; this is not a dynamic cost or general arithmetic score.</text>");
        for (var index = 0; index < selected.Length; index++)
        {
            var row = selected[index];
            var barHeight = Math.Max(1, row.TotalStateBits * chartHeight / maximum);
            var x = left + (index * (barWidth + gap));
            var y = baselineY - barHeight;
            builder.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x}\" y=\"{y}\" width=\"{barWidth}\" height=\"{barHeight}\" fill=\"#76558f\"/>\n");
            builder.Append(CultureInfo.InvariantCulture, $"<text x=\"{x + 24}\" y=\"{y - 5}\" text-anchor=\"middle\" font-family=\"sans-serif\" font-size=\"10\">{row.TotalStateBits}</text>\n");
            builder.Append(CultureInfo.InvariantCulture, $"<text transform=\"translate({x + 22},385) rotate(55)\" font-family=\"sans-serif\" font-size=\"8\">W{row.Width} {EscapeXml(ShortRepresentation(row.Representation))}</text>\n");
        }

        builder.AppendLine("<line x1=\"35\" y1=\"365\" x2=\"1075\" y2=\"365\" stroke=\"#20252b\"/>");
        builder.AppendLine("<text x=\"40\" y=\"480\" font-family=\"sans-serif\" font-size=\"11\" fill=\"#59636e\">Source: representation_search.csv. Payload coverage and lossiness remain separate columns.</text>");
        builder.AppendLine("</svg>");
        Build002Protocol.WriteLfText(path, builder.ToString());
    }

    private static void WriteCsv(
        string path,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows) =>
        Build002Protocol.WriteCsv(path, new[] { headers }.Concat(rows));

    private static OperationMeasurement MeasureCircuit(
        NandNetlist netlist,
        IEnumerable<IReadOnlyDictionary<string, BitState>> cases,
        Action<long, NandEvaluation> validate)
    {
        NandEvaluation? previous = null;
        long count = 0;
        long nandEvaluations = 0;
        long nandTransitions = 0;
        long initialTransitions = 0;
        long inputTransitions = 0;
        long stateTransitions = 0;
        foreach (var inputs in cases)
        {
            var evaluated = netlist.Evaluate(
                inputs,
                previous: previous,
                compareWithAllOff: previous is null);
            validate(count, evaluated);
            nandEvaluations += evaluated.NandEvaluations;
            inputTransitions += evaluated.InputTransitions;
            stateTransitions += evaluated.StateBitTransitions;
            if (previous is null)
            {
                initialTransitions += evaluated.NandOutputTransitions;
            }
            else
            {
                nandTransitions += evaluated.NandOutputTransitions;
            }

            previous = evaluated;
            count++;
        }

        return new OperationMeasurement(
            count,
            count,
            nandEvaluations,
            nandTransitions,
            stateTransitions,
            inputTransitions,
            initialTransitions);
    }

    private static IEnumerable<IReadOnlyDictionary<string, BitState>> BinaryPairInputs(int width)
    {
        var limit = 1 << width;
        for (var left = 0; left < limit; left++)
        {
            for (var right = 0; right < limit; right++)
            {
                yield return BinaryInputs(width, "a", left, "b", right);
            }
        }
    }

    private static IEnumerable<IReadOnlyDictionary<string, BitState>> DividerPairInputs(int width)
    {
        var limit = 1 << width;
        for (var dividend = 0; dividend < limit; dividend++)
        {
            for (var divisor = 0; divisor < limit; divisor++)
            {
                yield return BinaryInputs(width, "dividend", dividend, "divisor", divisor);
            }
        }
    }

    private static IEnumerable<IReadOnlyDictionary<string, BitState>> StructuralPairInputs(
        DeclaredExperimentalCircuit circuit,
        IReadOnlyList<ValuationHardwareState> states)
    {
        foreach (var left in states)
        {
            foreach (var right in states)
            {
                yield return ExperimentalInputs(circuit, left, right, opcode: null);
            }
        }
    }

    private static Dictionary<string, BitState> BinaryInputs(
        int width,
        string leftName,
        int left,
        string rightName,
        int right)
    {
        var inputs = new Dictionary<string, BitState>(StringComparer.Ordinal);
        for (var bit = 0; bit < width; bit++)
        {
            inputs[$"{leftName}[{bit}]"] = State((left & (1 << bit)) != 0);
            inputs[$"{rightName}[{bit}]"] = State((right & (1 << bit)) != 0);
        }

        return inputs;
    }

    private static Dictionary<string, BitState> ExperimentalInputs(
        DeclaredExperimentalCircuit circuit,
        ValuationHardwareState left,
        ValuationHardwareState? right,
        int? opcode)
    {
        var inputs = circuit.Netlist.Nodes
            .Where(node => node.Kind == NandNodeKind.Input)
            .ToDictionary(node => node.Name, _ => BitState.Off, StringComparer.Ordinal);
        SetOperand(inputs, circuit.Ports, left, isLeft: true);
        if (circuit.Ports.RightZeroInput is not null)
        {
            ArgumentNullException.ThrowIfNull(right);
            SetOperand(inputs, circuit.Ports, right, isLeft: false);
        }

        if (opcode.HasValue)
        {
            for (var bit = 0; bit < circuit.Ports.OpcodeInputs.Count; bit++)
            {
                inputs[circuit.Ports.OpcodeInputs[bit]] = State((opcode.Value & (1 << bit)) != 0);
            }
        }

        return inputs;
    }

    private static void SetOperand(
        IDictionary<string, BitState> inputs,
        ExperimentalPortLayout ports,
        ValuationHardwareState state,
        bool isLeft)
    {
        inputs[isLeft ? ports.LeftZeroInput : ports.RightZeroInput!] = State(state.IsZero);
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var layout = ports.Lanes[lane];
            var payload = isLeft ? layout.LeftPayloadInputs : layout.RightPayloadInputs;
            for (var bit = 0; bit < payload.Count; bit++)
            {
                var set = ports.Encoding == ExperimentalValuationEncoding.BinaryExponent
                    ? (state.ExponentAt(lane) & (1 << bit)) != 0
                    : state.ExponentAt(lane) >= bit + 1;
                inputs[payload[bit]] = State(set);
            }

            inputs[isLeft ? layout.LeftSaturationInput : layout.RightSaturationInput!] =
                State(state.IsLaneSaturated(lane));
        }
    }

    private static void CheckExperimentalEvaluation(
        DeclaredExperimentalCircuit circuit,
        NandEvaluation evaluation,
        ValuationHardwareState left,
        ValuationHardwareState right,
        CorrectnessAccumulator correctness)
    {
        var context = $"{circuit.Netlist.Name}/{FormatState(left)}/{FormatState(right)}";
        switch (circuit.Ports.Operation)
        {
            case ExperimentalValuationOperation.Compose:
                CheckResult(left.Compose(right));
                break;
            case ExperimentalValuationOperation.Meet:
                CheckResult(left.Meet(right));
                break;
            case ExperimentalValuationOperation.Join:
                CheckResult(left.Join(right));
                break;
            case ExperimentalValuationOperation.Divides:
                {
                    var expected = left.Divides(right);
                    correctness.Check(
                        expected.Succeeded &&
                        IsOn(evaluation.Outputs[circuit.Ports.PredicateOutput!]) == expected.Value!.Value,
                        context + "/predicate");
                    correctness.Check(
                        !IsOn(evaluation.Outputs[circuit.Ports.RejectOutput!]) &&
                        IsOn(evaluation.Outputs[circuit.Ports.AcceptedOutput!]),
                        context + "/status");
                    break;
                }
            default:
                throw new InvalidOperationException("Unexpected structural dynamic operation.");
        }

        void CheckResult(ValuationStateResult<ValuationHardwareState> expected)
        {
            correctness.Check(expected.Succeeded, context + "/semantic_status");
            if (expected.Succeeded)
            {
                CheckExperimentalResult(circuit, evaluation, expected.Value!, correctness, context);
            }
        }
    }

    private static void CheckExperimentalResult(
        DeclaredExperimentalCircuit circuit,
        NandEvaluation evaluation,
        ValuationHardwareState expected,
        CorrectnessAccumulator correctness,
        string context)
    {
        correctness.Check(
            !IsOn(evaluation.Outputs[circuit.Ports.RejectOutput!]) &&
            IsOn(evaluation.Outputs[circuit.Ports.AcceptedOutput!]),
            context + "/accepted");
        correctness.Check(
            IsOn(evaluation.Outputs[circuit.Ports.ResultZeroOutput!]) == expected.IsZero,
            context + "/zero");
        for (var lane = 0; lane < circuit.Ports.Lanes.Count; lane++)
        {
            var layout = circuit.Ports.Lanes[lane];
            var exponent = circuit.Ports.Encoding == ExperimentalValuationEncoding.BinaryExponent
                ? ReadUnsigned(evaluation.Outputs, layout.ResultPayloadOutputs)
                : layout.ResultPayloadOutputs.Count(name => IsOn(evaluation.Outputs[name]));
            correctness.Check(exponent == expected.ExponentAt(lane), context + $"/lane{lane}/payload");
            correctness.Check(
                IsOn(evaluation.Outputs[layout.ResultSaturationOutput!]) == expected.IsLaneSaturated(lane),
                context + $"/lane{lane}/saturation");
        }
    }

    private static void CheckRejectedOutputs(
        DeclaredExperimentalCircuit circuit,
        NandEvaluation evaluation,
        CorrectnessAccumulator correctness,
        string context)
    {
        correctness.Check(IsOn(evaluation.Outputs[circuit.Ports.RejectOutput!]), context + "/reject");
        correctness.Check(!IsOn(evaluation.Outputs[circuit.Ports.AcceptedOutput!]), context + "/accepted_clear");
        if (circuit.Ports.PredicateOutput is not null)
        {
            correctness.Check(!IsOn(evaluation.Outputs[circuit.Ports.PredicateOutput]), context + "/predicate_clear");
        }

        if (circuit.Ports.ResultZeroOutput is null)
        {
            return;
        }

        correctness.Check(!IsOn(evaluation.Outputs[circuit.Ports.ResultZeroOutput]), context + "/zero_clear");
        foreach (var lane in circuit.Ports.Lanes)
        {
            correctness.Check(
                lane.ResultPayloadOutputs.All(name => !IsOn(evaluation.Outputs[name])),
                context + $"/lane{lane.Lane}/payload_clear");
            correctness.Check(
                !IsOn(evaluation.Outputs[lane.ResultSaturationOutput!]),
                context + $"/lane{lane.Lane}/saturation_clear");
        }
    }

    private static int CountStateTransitions(
        ValuationHardwareState before,
        ValuationHardwareState after,
        ExperimentalPortLayout ports)
    {
        var beforeBits = StructuralBits(before, ports);
        var afterBits = StructuralBits(after, ports);
        return beforeBits.Zip(afterBits, (left, right) => left != right).Count(changed => changed);
    }

    private static bool[] StructuralBits(
        ValuationHardwareState state,
        ExperimentalPortLayout ports)
    {
        var bits = new List<bool> { state.IsZero };
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var payloadWidth = ports.Lanes[lane].PayloadWidth;
            for (var bit = 0; bit < payloadWidth; bit++)
            {
                bits.Add(ports.Encoding == ExperimentalValuationEncoding.BinaryExponent
                    ? (state.ExponentAt(lane) & (1 << bit)) != 0
                    : state.ExponentAt(lane) >= bit + 1);
            }

            bits.Add(state.IsLaneSaturated(lane));
        }

        return [.. bits];
    }

    private static List<ValuationHardwareState> SmoothStates(int width)
    {
        var states = new List<ValuationHardwareState>();
        var maximum = (1 << width) - 1;
        for (var magnitude = 0; magnitude <= maximum; magnitude++)
        {
            if (TryEncodeSmooth(width, magnitude, out var state))
            {
                states.Add(state!);
            }
        }

        return states;
    }

    private static bool TryEncodeSmooth(
        int width,
        int magnitude,
        out ValuationHardwareState? state)
    {
        if (magnitude == 0)
        {
            state = ValuationHardwareState.Zero(width);
            return true;
        }

        var residual = magnitude;
        var exponents = new int[ValuationHardwareDomain.S4.Count];
        for (var lane = 0; lane < ValuationHardwareDomain.S4.Count; lane++)
        {
            var prime = ValuationHardwareDomain.S4[lane];
            while (residual % prime == 0)
            {
                residual /= prime;
                exponents[lane]++;
            }
        }

        if (residual != 1)
        {
            state = null;
            return false;
        }

        state = ValuationHardwareState.Create(width, false, exponents).Value!;
        return true;
    }

    private static int ReadWord(
        IReadOnlyDictionary<string, BitState> outputs,
        string name,
        int width)
    {
        var result = 0;
        for (var bit = 0; bit < width; bit++)
        {
            if (IsOn(outputs[$"{name}[{bit}]"]))
            {
                result |= 1 << bit;
            }
        }

        return result;
    }

    private static int ReadUnsigned(
        IReadOnlyDictionary<string, BitState> outputs,
        IReadOnlyList<string> names)
    {
        var result = 0;
        for (var bit = 0; bit < names.Count; bit++)
        {
            if (IsOn(outputs[names[bit]]))
            {
                result |= 1 << bit;
            }
        }

        return result;
    }

    private static Build002DynamicOperationRow NotMeasuredDynamicRow(
        string implementation,
        int width,
        string operation,
        string regime,
        string obligation,
        long cases,
        string notes) =>
        new(
            implementation,
            width,
            operation,
            regime,
            obligation,
            "NOT_MEASURED",
            "MISSING_INTEGRATED_HARDWARE",
            cases,
            NotMeasured,
            NotMeasured,
            NotMeasured,
            NotMeasured,
            NotMeasured,
            NotMeasured,
            NotMeasured,
            0,
            0,
            0,
            0,
            notes + " All -1 fields mean NOT_MEASURED.");

    private static Build002WorkloadRow NotMeasuredWorkload(
        string experiment,
        string traceId,
        string implementation,
        int width,
        string regime,
        string obligation,
        string phase,
        int operations,
        string support,
        string notes) =>
        new(
            experiment,
            traceId,
            implementation,
            width,
            regime,
            obligation,
            phase,
            "NOT_MEASURED",
            support,
            operations,
            NotMeasured,
            NotMeasured,
            NotMeasured,
            NotMeasured,
            0,
            0,
            0,
            0,
            string.Empty,
            "registered coverage cell",
            notes + " All -1 cost fields mean NOT_MEASURED.");

    private static int OracleGcd(int left, int right)
    {
        while (right != 0)
        {
            var remainder = left % right;
            left = right;
            right = remainder;
        }

        return left;
    }

    private static int Valuation(int magnitude, int prime)
    {
        if (magnitude == 0)
        {
            return 0;
        }

        var exponent = 0;
        while (magnitude % prime == 0)
        {
            magnitude /= prime;
            exponent++;
        }

        return exponent;
    }

    private static int BitsRequired(int maximum)
    {
        var bits = 0;
        do
        {
            bits++;
            maximum >>= 1;
        }
        while (maximum != 0);

        return bits;
    }

    private static string FormatState(ValuationHardwareState state) =>
        state.IsZero
            ? "zero"
            : $"[{string.Join(',', state.Exponents)}];sat=[{string.Join(',', state.SaturatedLanes.Select(Build002Evidence.Boolean))}]";

    private static string? ReadJsonString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? ReadJsonInt(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var property) &&
        property.TryGetInt32(out var value)
            ? value
            : null;

    private static string EscapeXml(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private static string ShortImplementation(string implementation) => implementation switch
    {
        "VFU-BINEXP-S4" => "BINEXP",
        "VFU-THERM-S4" => "THERM",
        _ => implementation,
    };

    private static string ShortRepresentation(string representation) => representation switch
    {
        "BINARY_POSITIONAL" => "BIN",
        "BINARY_EXPONENT_S4" => "BEXP",
        "THERMOMETER_S4" => "THERM",
        "PRESENCE_ONLY_S4" => "PRES",
        "BINARY_PLUS_EXACT_THRESHOLD_SIDECAR_S4" => "SIDECAR",
        _ => representation,
    };

    private static bool IsOn(BitState state) => state == BitState.On;

    private static BitState State(bool value) => value ? BitState.On : BitState.Off;

    private sealed record OperationMeasurement(
        long Cases,
        long Cycles,
        long NandEvaluations,
        long NandOutputTransitions,
        long StateBitTransitions,
        long InputBitTransitions,
        long InitialNandTransitions);

    private sealed record DynamicEvidence(
        IReadOnlyList<Build002DynamicOperationRow> Rows,
        IReadOnlyDictionary<string, OperationMeasurement> Measurements);

    private sealed record StructuralTraceExecution(
        ValuationHardwareState FinalState,
        int Rejections,
        OperationMeasurement Measurement);

    private sealed record HdlSummaryReceipt(
        string Name,
        string Sha256,
        string Status,
        string Top,
        int? Width,
        string Architecture,
        string Notes);

    private sealed record ManifestFile(string Path, string Sha256);

    private sealed record CorrectnessReceipt(
        string Status,
        long CheckCount,
        long FailureCount,
        IReadOnlyList<string> Failures,
        object Domains,
        string Scope);

    private sealed class CorrectnessAccumulator
    {
        private readonly List<string> _failures = [];

        public long CheckCount { get; private set; }

        public long FailureCount { get; private set; }

        public void Check(bool condition, string context)
        {
            CheckCount++;
            if (condition)
            {
                return;
            }

            FailureCount++;
            if (_failures.Count < 100)
            {
                _failures.Add(context);
            }
        }

        public CorrectnessReceipt ToReceipt() =>
            new(
                FailureCount == 0 ? "BOUNDED_PASS" : "BOUNDED_FAILURE",
                CheckCount,
                FailureCount,
                _failures.AsReadOnly(),
                new
                {
                    Divider = "all ordered magnitude pairs at W=4,6,8",
                    Gcd = "all ordered magnitude pairs through deterministic subtractive semantic steps",
                    Multiplier = "all ordered magnitude pairs at W=4,6,8",
                    StructuralNative = "all ordered common-domain S4-smooth magnitude-derived state pairs",
                    SidecarEncode = "every W-bit magnitude and every S4 valuation query",
                    Workloads = "all frozen B, D, and E traces executed at the implemented evidence layer",
                },
                "Bounded executed checks only. HDL/formal, integrated sidecar, converters, and unmeasured workload phases are outside this receipt.");
    }
}
