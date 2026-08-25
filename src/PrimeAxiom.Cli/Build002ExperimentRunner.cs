using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
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
    private static readonly string[] RequiredExperiments = ["A", "B", "C", "D", "E", "F", "R"];
    private static readonly string[] RequiredBObligations =
        ["STRUCTURAL_FINAL", "MAGNITUDE_FINAL", "MAGNITUDE_EVERY_OP"];
    private static readonly string[] MixedExperiments = ["E", "F"];
    private static readonly string[] RequiredAPhases = ["INGRESS", "EXECUTE", "EGRESS"];
    private static readonly int[] DecisionWidths = [6, 8];
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
        "synthesis_metrics.csv",
        "formal_receipts.json",
        "toolchain.json",
        "protocol_coverage.json",
        "figures/static_gate_counts.svg",
        "figures/representation_bits.svg",
        "README.md",
    ];

    public static Build002ExperimentReceipt Run(
        string repositoryRoot,
        string outputDirectory,
        string invocationOutputArgument,
        string? hdlVerificationSummaryPath = null,
        string? hdlSynthesisMetricsPath = null,
        string? hdlToolchainBootstrapPath = null)
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
        var hdlImport = Build002HdlEvidenceImporter.Import(
            outputDirectory,
            hdlVerificationSummaryPath,
            hdlSynthesisMetricsPath,
            hdlToolchainBootstrapPath);

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
            BuildCoverage(repositoryRoot, hdlImport, staticRows, workloadRows, correctnessReceipt));
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
            repositoryRoot,
            hdlImport,
            staticRows,
            workloadRows);
        WriteManifest(
            repositoryRoot,
            outputDirectory,
            invocationOutputArgument,
            hdlImport,
            correctnessReceipt,
            staticRows,
            workloadRows);

        return new Build002ExperimentReceipt(
            outputDirectory,
            correctnessReceipt.CheckCount,
            correctnessReceipt.FailureCount,
            Classify(repositoryRoot, hdlImport, staticRows, workloadRows, correctnessReceipt));
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
            AddBaseline(
                "BIN-SCALE-CANCEL-WARM",
                width,
                "INTEGRATED_SCALE_CANCEL",
                WarmStructuralHardware.BuildBinaryScaleCancelMachine(width).Netlist);
            AddBaseline(
                "BIN-MIXED-WARM",
                width,
                "INTEGRATED_LOAD_SCALE_CANCEL_ADD",
                BinaryMagnitudeDatapathHardware.Build(width).Netlist);
            var sidecar = SidecarDatapathHardware.Build(width);
            rows.Add(new Build002StaticCostRow(
                "BIN+VSC-S4",
                width,
                sidecar.Netlist.Name,
                "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD",
                "BINARY_MAGNITUDE_PLUS_EXACT_THRESHOLD_SIDECAR",
                sidecar.EvidenceClass,
                "FULL_BINARY_MAGNITUDE_WITH_SOUND_S4_METADATA",
                sidecar.Netlist.Metrics,
                "Authoritative magnitude, sidecar thresholds, validity, control/status, atomic hold, and persistent DFF state are all charged."));

            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentCompose(width));
            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentCancel(width));
            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentMeet(width));
            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentJoin(width));
            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentDivides(width));
            AddExperimental("VFU-BINEXP-S4", ExperimentalHardware.BuildBinaryExponentFunctionalUnit(width));
            var warmStructural = WarmStructuralHardware.BuildScaleCancelMachine(width);
            rows.Add(new Build002StaticCostRow(
                "VFU-BINEXP-S4-WARM",
                width,
                warmStructural.Netlist.Name,
                "INTEGRATED_SCALE_CANCEL",
                "BINARY_EXPONENT",
                warmStructural.EvidenceClass,
                "FULLY_INTEGRATED_EXACT_S4_STATE",
                warmStructural.Netlist.Metrics,
                "Persistent DFF state, control/status, cap checks, malformed-state rejection, and atomic hold are charged."));
            AddAdapter(
                "VFU-BINEXP-S4",
                width,
                "COLD_ENCODE",
                "BINARY_MAGNITUDE_TO_BINARY_EXPONENT",
                RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(width).Netlist,
                "Exact S4 valuations are emitted; support status distinguishes zero/S4-smooth values from unsupported cofactors.");
            AddAdapter(
                "VFU-BINEXP-S4",
                width,
                "RECONSTRUCT",
                "BINARY_EXPONENT_TO_BINARY_MAGNITUDE",
                RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(width).Netlist,
                "Malformed, saturated, and W-bit-overflow states are rejected explicitly.");
            AddAdapter(
                "PRESENCE-S4",
                width,
                "PROJECT_PRESENCE",
                "BINARY_EXPONENT_TO_PRESENCE",
                RepresentationAdapterHardware.BuildBinaryExponentPresence(width).Netlist,
                "Lossy presence projection; multiplicity is intentionally not represented.");
            AddAdapter(
                "PRESENCE-S4",
                width,
                "PROJECT_PRESENCE",
                "THERMOMETER_TO_PRESENCE",
                RepresentationAdapterHardware.BuildThermometerPresence(width).Netlist,
                "Lossy presence projection from canonical thermometer thresholds.");
            AddAdapter(
                "VFU-ENCODING-ADAPTER-S4",
                width,
                "BINEXP_TO_THERMOMETER",
                "SAME_CAP_BINARY_EXPONENT_TO_THERMOMETER",
                ValuationEncodingAdapterHardware.BuildBinaryExponentToThermometer(width).Netlist,
                "Exact/saturated cap semantics are preserved; malformed and zero-with-payload sources reject.");
            AddAdapter(
                "VFU-ENCODING-ADAPTER-S4",
                width,
                "THERMOMETER_TO_BINEXP",
                "SAME_CAP_THERMOMETER_TO_BINARY_EXPONENT",
                ValuationEncodingAdapterHardware.BuildThermometerToBinaryExponent(width).Netlist,
                "Canonical thresholds are converted to minimal binary lanes; malformed monotonicity and saturation reject.");
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

        void AddAdapter(
            string implementation,
            int width,
            string operation,
            string architecture,
            NandNetlist netlist,
            string notes)
        {
            rows.Add(new Build002StaticCostRow(
                implementation,
                width,
                netlist.Name,
                operation,
                architecture,
                "STRUCTURAL_DECLARED",
                "IMPLEMENTED_ADAPTER",
                netlist.Metrics,
                notes));
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

            var coldEncoder = RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(width);
            var encode = MeasureCircuit(
                coldEncoder.Netlist,
                MagnitudeEncoderInputs(coldEncoder),
                (index, evaluation) =>
                {
                    var magnitude = (int)index;
                    correctness.Check(
                        IsOn(evaluation.Outputs[coldEncoder.Ports.ZeroOutput]) == (magnitude == 0),
                        $"A/COLD_ENCODER/ZERO/W{width}/{magnitude}");
                    foreach (var lane in coldEncoder.Ports.Lanes)
                    {
                        correctness.Check(
                            ReadUnsigned(evaluation.Outputs, lane.ExponentOutputs) ==
                            (magnitude == 0 ? 0 : Valuation(magnitude, lane.Prime)),
                            $"A/COLD_ENCODER/LANE/W{width}/{magnitude}/P{lane.Prime}");
                    }

                    var smooth = TryEncodeSmooth(width, magnitude, out _);
                    correctness.Check(
                        IsOn(evaluation.Outputs[coldEncoder.Ports.SupportedOutput]) == smooth,
                        $"A/COLD_ENCODER/SUPPORT/W{width}/{magnitude}");
                });
            AddMeasured(
                $"A/VFU-BINEXP-S4/ENCODE/W{width}",
                "VFU-BINEXP-S4",
                width,
                "ENCODE",
                "COLD_MAG",
                "STRUCTURAL_FINAL",
                coldEncoder.EvidenceClass,
                "EXACT_CATALOG_VALUATIONS_SUPPORT_TAGGED",
                encode,
                "Runtime is an emitted NAND truth-table adapter; construction-time arithmetic only selects fixed minterms.");

            var reconstruction = RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(width);
            var reconstruct = MeasureCircuit(
                reconstruction.Netlist,
                DecoderInputs(reconstruction, structuralStates),
                (index, evaluation) =>
                {
                    var state = structuralStates[(int)index];
                    var expected = state.Reconstruct();
                    correctness.Check(
                        expected.Succeeded && IsOn(evaluation.Outputs[reconstruction.Ports.AcceptedOutput]) &&
                        ReadUnsigned(evaluation.Outputs, reconstruction.Ports.MagnitudeOutputs) ==
                        (int)expected.Value!.Value,
                        $"A/RECONSTRUCT/W{width}/{index}");
                });
            AddMeasured(
                $"A/VFU-BINEXP-S4/RECONSTRUCT/W{width}",
                "VFU-BINEXP-S4",
                width,
                "RECONSTRUCT",
                "WARM_RESIDENT",
                "MAGNITUDE_FINAL",
                reconstruction.EvidenceClass,
                "FULL_EXACT_COMMON_W_BIT_DOMAIN",
                reconstruct,
                "Every common-domain exact state is reconstructed; malformed/saturated/overflow rejection is tested separately.");

            var gcdNetlist = BaselineAlgorithmHardware.BuildSubtractiveGcdMachine(width);
            long gcdCases = 0;
            long gcdCycles = 0;
            long lcmCycles = 0;
            long lcmEvaluations = 0;
            var dividerNands = divider.Metrics.Nand2Static;
            var multiplierNands = multiplier.Metrics.Nand2Static;
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
                    var expectedLcm = left == 0 || right == 0
                        ? 0L
                        : ((long)left / receipt.Result) * right;
                    correctness.Check(
                        expectedLcm == OracleLcm(left, right),
                        $"C/BIN-LCM/W{width}/{left}/{right}");
                    var postGcdCycles = expectedLcm == 0 ? 0 : 2;
                    lcmCycles += receipt.Cycles + postGcdCycles;
                    lcmEvaluations += checked(
                        (receipt.Cycles * (long)gcdNetlist.Metrics.Nand2Static) +
                        (expectedLcm == 0 ? 0 : dividerNands + multiplierNands));
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
            var lcm = new OperationMeasurement(
                gcdCases,
                lcmCycles,
                lcmEvaluations,
                NotMeasured,
                NotMeasured,
                NotMeasured,
                NotMeasured);
            AddMeasured(
                $"C/BIN-LCM/W{width}",
                "BIN-GCD+DIV+MUL",
                width,
                "LCM",
                "COLD_MAG",
                "MAGNITUDE_FINAL",
                "COMPOSITE_STRUCTURAL_DECLARED",
                "FULL_BINARY_2W_RESULT_TRANSITIONS_NOT_MEASURED",
                lcm,
                "LCM uses checked GCD, exact division, and full 2W multiplication; composite controller transitions are not claimed.");

            var sidecar = SidecarDatapathHardware.Build(width);
            var rawSidecarState = SidecarDatapathHardware.EncodeRawState(
                sidecar,
                magnitude: 0,
                valid: false,
                new int[sidecar.Ports.Lanes.Count]);
            var loadCases = Enumerable.Range(0, magnitudeLimit)
                .Select(magnitude => new StatefulCircuitCase(
                    SidecarDatapathHardware.EncodeInputs(
                        sidecar,
                        SidecarDatapathOperation.Load,
                        operand: magnitude),
                    rawSidecarState))
                .ToArray();
            var loadMeasurement = MeasureStatefulCircuit(
                sidecar.Netlist,
                loadCases,
                (index, evaluation) =>
                {
                    var magnitude = (int)index;
                    var actual = SidecarDatapathHardware.DecodeNextState(sidecar, evaluation);
                    var expected = SidecarDatapathHardware.CreateExactState(width, magnitude);
                    correctness.Check(
                        IsOn(evaluation.Outputs[sidecar.Ports.AcceptedOutput]) &&
                        actual.Magnitude == expected.Magnitude && actual.Valid == expected.Valid &&
                        actual.LowerBounds.SequenceEqual(expected.LowerBounds),
                        $"C/SIDECAR/LOAD/W{width}/{magnitude}");
                });
            AddMeasured(
                $"C/BIN+VSC-S4/LOAD/W{width}",
                "BIN+VSC-S4",
                width,
                "ENCODE_LOAD",
                "COLD_MAG",
                "STRUCTURAL_FINAL",
                sidecar.EvidenceClass,
                "FULL_BINARY_TO_EXACT_S4",
                loadMeasurement,
                "The integrated load truth table acquires authoritative magnitude and exact S4 thresholds in one cycle.");

            var queryCases = new List<StatefulCircuitCase>();
            var queryExpected = new List<bool>();
            for (var magnitude = 0; magnitude < magnitudeLimit; magnitude++)
            {
                var exact = SidecarDatapathHardware.CreateExactState(width, magnitude);
                var exactState = SidecarDatapathHardware.EncodeState(sidecar, exact);
                foreach (var lane in sidecar.Ports.Lanes)
                {
                    for (var threshold = 1; threshold <= lane.Cap; threshold++)
                    {
                        queryCases.Add(new StatefulCircuitCase(
                            SidecarDatapathHardware.EncodeInputs(
                                sidecar,
                                SidecarDatapathOperation.Query,
                                lane.Prime,
                                threshold),
                            exactState));
                        queryExpected.Add(magnitude == 0 || Valuation(magnitude, lane.Prime) >= threshold);
                    }
                }
            }

            var queryMeasurement = MeasureStatefulCircuit(
                sidecar.Netlist,
                queryCases,
                (index, evaluation) =>
                {
                    var expected = queryExpected[(int)index];
                    correctness.Check(
                        IsOn(evaluation.Outputs[sidecar.Ports.AcceptedOutput]) &&
                        IsOn(evaluation.Outputs[sidecar.Ports.QueryKnownOutput]) &&
                        IsOn(evaluation.Outputs[sidecar.Ports.QueryExactOutput]) &&
                        IsOn(evaluation.Outputs[sidecar.Ports.QueryPredicateOutput]) == expected,
                        $"C/SIDECAR/QUERY/W{width}/{index}");
                });
            AddMeasured(
                $"C/BIN+VSC-S4/QUERY/W{width}",
                "BIN+VSC-S4",
                width,
                "VALUATION_THRESHOLD_QUERY",
                "WARM_RESIDENT",
                "PREDICATE_ONLY",
                sidecar.EvidenceClass,
                "FULL_BINARY_MAGNITUDE_EXACT_S4_SIDECAR",
                queryMeasurement,
                "Every supported S4 threshold query is executed from every exact W-bit resident state; state is held.");
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
                notes,
                OperationClassFor(implementation, operation, regime)));
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
            AddAIntegratedCohortRows(rows, width);

            foreach (var trace in Build002Workloads.RepeatedScaleCancel(width))
            {
                var execution = RunStructuralTrace(trace, correctness);
                AddStructuralTraceRows(rows, trace, execution);
                foreach (var obligation in new[] { "STRUCTURAL_FINAL", "MAGNITUDE_FINAL", "MAGNITUDE_EVERY_OP" })
                {
                    rows.Add(RunIntegratedWarmStructuralTrace(trace, obligation, correctness));
                    rows.Add(RunIntegratedWarmBinaryTrace(trace, obligation, correctness));
                    if (obligation != "STRUCTURAL_FINAL")
                    {
                        var reconstructCount = obligation == "MAGNITUDE_FINAL" ? 1 : trace.Steps.Count;
                        rows.Add(BuildTraceAdapterRow(
                            "B",
                            trace,
                            "VFU-BINEXP-S4-WARM",
                            obligation,
                            "EGRESS",
                            reconstructCount,
                            RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(width).Netlist,
                            encodes: 0,
                            reconstructs: reconstructCount,
                            "CROSS_REPRESENTATION",
                            "Exact structural state is reconstructed through the implemented NAND adapter."));
                    }

                    if (obligation == "STRUCTURAL_FINAL")
                    {
                        rows.Add(BuildTraceAdapterRow(
                            "B",
                            trace,
                            "BIN-SCALE-CANCEL-WARM",
                            obligation,
                            "EGRESS",
                            1,
                            RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(width).Netlist,
                            encodes: 1,
                            reconstructs: 0,
                            "REQUIRES_FACTOR_DISCOVERY",
                            "The binary result is converted to the exact requested S4 structural contract; support is known exact for this generated trace."));
                    }
                }
            }

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
                $"C-W{width}-BIN-DIVIDES-ALL",
                "BIN-DIV",
                width,
                "COLD_MAG",
                "PREDICATE_ONLY",
                "EXECUTE",
                "STRUCTURAL_DECLARED",
                "FULL_BINARY",
                dynamic.Measurements[$"C/BIN-DIVIDE/W{width}"],
                "all ordered W-bit magnitude pairs",
                "The full restoring divider is charged for the conventional exact-divisibility predicate.");
            AddMeasurement(
                "C",
                $"C-W{width}-BIN-LCM-ALL",
                "BIN-GCD+DIV+MUL",
                width,
                "COLD_MAG",
                "MAGNITUDE_FINAL",
                "EXECUTE",
                "COMPOSITE_STRUCTURAL_DECLARED",
                "FULL_BINARY_2W_RESULT_TRANSITIONS_NOT_MEASURED",
                dynamic.Measurements[$"C/BIN-LCM/W{width}"],
                "all ordered W-bit magnitude pairs",
                "Exact 2W LCM semantics; GCD/divider/multiplier costs are charged, while composite settled transitions remain explicit NOT_MEASURED.");
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
            AddMeasurement(
                "C",
                $"C-W{width}-SIDECAR-LOAD-ALL",
                "BIN+VSC-S4",
                width,
                "COLD_MAG",
                "PREDICATE_ONLY",
                "INGRESS",
                "STRUCTURAL_DECLARED_INTEGRATED",
                "FULL_BINARY_TO_EXACT_S4",
                dynamic.Measurements[$"C/BIN+VSC-S4/LOAD/W{width}"],
                "one exact sidecar acquisition per W-bit magnitude",
                "Cold predicate use pays integrated factor-discovery/load cost before queries.");
            AddMeasurement(
                "C",
                $"C-W{width}-SIDECAR-QUERY-ALL",
                "BIN+VSC-S4",
                width,
                "WARM_RESIDENT",
                "PREDICATE_ONLY",
                "EXECUTE",
                "STRUCTURAL_DECLARED_INTEGRATED",
                "FULL_BINARY_MAGNITUDE_EXACT_S4_SIDECAR",
                dynamic.Measurements[$"C/BIN+VSC-S4/QUERY/W{width}"],
                "every S4 threshold query from every exact state",
                "Warm query retains authoritative magnitude; the whole integrated sidecar graph is charged each cycle.");

            foreach (var rational in Build002Workloads.RationalReduction(width))
            {
                rows.Add(RunBinaryRational(rational, correctness));
                rows.Add(RunStructuralRational(rational, correctness));
            }

            foreach (var trace in Build002Workloads.MixedAddition(width))
            {
                rows.AddRange(RunIntegratedBinaryMixedTrace("E", trace, correctness));
                rows.AddRange(RunIntegratedSidecarTrace("E", trace, refreshAfterInvalidAddition: false, correctness));
                rows.AddRange(RunIntegratedSidecarTrace("E", trace, refreshAfterInvalidAddition: true, correctness));
            }

            foreach (var trace in Build002Workloads.HostileTraces(width))
            {
                rows.AddRange(RunIntegratedBinaryMixedTrace("F", trace, correctness));
                rows.AddRange(RunIntegratedSidecarTrace("F", trace, refreshAfterInvalidAddition: false, correctness));
                rows.AddRange(RunIntegratedSidecarTrace("F", trace, refreshAfterInvalidAddition: true, correctness));
            }
            var domain = ValuationHardwareDomain.ForWidth(width);
            var encoder = RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(width);
            var decoder = RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(width);
            var binaryPresence = RepresentationAdapterHardware.BuildBinaryExponentPresence(width);
            var thermometerPresence = RepresentationAdapterHardware.BuildThermometerPresence(width);
            var binaryToThermometer = ValuationEncodingAdapterHardware.BuildBinaryExponentToThermometer(width);
            var thermometerToBinary = ValuationEncodingAdapterHardware.BuildThermometerToBinaryExponent(width);
            var magnitudeCases = 1 << width;
            var binaryPayloadCases = 1 << domain.Caps.Sum(BitsRequired);
            var thermometerPayloadCases = 1 << domain.Caps.Sum();
            var binaryRawEncodingCases = 1 << (domain.Caps.Sum(BitsRequired) + domain.LaneCount + 1);
            var thermometerEncodingTestCases =
                (1 << (domain.Caps.Sum() + 1)) +
                checked((domain.Caps.Aggregate(1, (product, cap) => product * (cap + 1)) + 1) *
                        (1 << domain.LaneCount));
            rows.Add(AdapterWorkload(
                $"R-W{width}-MAG-TO-BINEXP",
                "VFU-BINEXP-S4",
                width,
                "COLD_MAG",
                "STRUCTURAL_FINAL",
                "INGRESS",
                magnitudeCases,
                encoder.Netlist,
                "EXACT_CATALOG_VALUATIONS_SUPPORT_TAGGED",
                "Cold magnitude encoder; unsupported cofactor support is explicit."));
            rows.Add(AdapterWorkload(
                $"R-W{width}-BINEXP-TO-MAG",
                "VFU-BINEXP-S4",
                width,
                "WARM_RESIDENT",
                "MAGNITUDE_FINAL",
                "EGRESS",
                SmoothStates(width).Count,
                decoder.Netlist,
                "FULL_EXACT_COMMON_W_BIT_DOMAIN",
                "Structural decoder rejects malformed, saturated, and overflow states."));
            rows.Add(AdapterWorkload(
                $"R-W{width}-BINEXP-PRESENCE",
                "PRESENCE-S4",
                width,
                "WARM_RESIDENT",
                "STRUCTURAL_FINAL",
                "EXECUTE",
                binaryPayloadCases,
                binaryPresence.Netlist,
                "LOSSY_PRESENCE_PROJECTION",
                "Multiplicity is intentionally discarded and cannot support cancellation counts."));
            rows.Add(AdapterWorkload(
                $"R-W{width}-THERM-PRESENCE",
                "PRESENCE-S4",
                width,
                "WARM_RESIDENT",
                "STRUCTURAL_FINAL",
                "EXECUTE",
                thermometerPayloadCases,
                thermometerPresence.Netlist,
                "LOSSY_PRESENCE_PROJECTION",
                "Raw thermometer payloads are validated before presence projection."));
            rows.Add(AdapterWorkload(
                $"R-W{width}-BINEXP-TO-THERM",
                "VFU-ENCODING-ADAPTER-S4",
                width,
                "WARM_RESIDENT",
                "STRUCTURAL_FINAL",
                "EXECUTE",
                binaryRawEncodingCases,
                binaryToThermometer.Netlist,
                "EXHAUSTIVE_RAW_DOMAIN_CANONICAL_OR_REJECTED",
                "Same-cap exact and saturation-preserving conversion; no magnitude reconstruction."));
            rows.Add(AdapterWorkload(
                $"R-W{width}-THERM-TO-BINEXP",
                "VFU-ENCODING-ADAPTER-S4",
                width,
                "WARM_RESIDENT",
                "STRUCTURAL_FINAL",
                "EXECUTE",
                thermometerEncodingTestCases,
                thermometerToBinary.Netlist,
                "LAYERED_EXHAUSTIVE_RAW_PAYLOAD_PLUS_LEGAL_SATURATION_MASKS",
                "Every raw threshold payload/zero pair is tested without saturation; every saturation mask is tested over every legal vector and zero. Non-monotone and malformed cases reject."));
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
                notes,
                OperationClassFor(implementation, phase, regime),
                measurement.InputBitTransitions,
                measurement.InitialNandTransitions));
        }

        static Build002WorkloadRow AdapterWorkload(
            string traceId,
            string implementation,
            int width,
            string regime,
            string obligation,
            string phase,
            int cases,
            NandNetlist netlist,
            string support,
            string notes) =>
            new(
                "R",
                traceId,
                implementation,
                width,
                regime,
                obligation,
                phase,
                "STRUCTURAL_DECLARED_EXHAUSTIVE",
                support,
                cases,
                cases,
                checked((long)cases * netlist.Metrics.Nand2Static),
                NotMeasured,
                0,
                0,
                phase == "INGRESS" ? cases : 0,
                phase == "EGRESS" ? cases : 0,
                0,
                string.Empty,
                "representation ablation",
                notes + " Settled-transition aggregate is not pooled across invalid raw encodings.",
                phase == "INGRESS" ? "REQUIRES_FACTOR_DISCOVERY" :
                    phase == "EGRESS" ? "CROSS_REPRESENTATION" : "REPRESENTATION_LOCAL",
                NotMeasured,
                NotMeasured);
    }

    private static void AddAIntegratedCohortRows(
        List<Build002WorkloadRow> rows,
        int width)
    {
        var maximum = (1 << width) - 1;
        var states = SmoothStates(width);
        var cohorts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["FIT_W_EXACT"] = 0,
            ["FIT_2W_STRUCTURAL_UNSATURATED_OUTSIDE_W"] = 0,
            ["FIT_2W_WITH_SATURATED_LANE"] = 0,
        };
        foreach (var left in states)
        {
            var leftMagnitude = checked((int)left.Reconstruct().Value!.Value);
            foreach (var right in states)
            {
                var rightMagnitude = checked((int)right.Reconstruct().Value!.Value);
                var product = leftMagnitude * rightMagnitude;
                var composed = left.Compose(right);
                var cohort = product <= maximum && composed.Succeeded && composed.Value!.IsExact
                    ? "FIT_W_EXACT"
                    : composed.Succeeded && composed.Value!.IsExact
                        ? "FIT_2W_STRUCTURAL_UNSATURATED_OUTSIDE_W"
                        : "FIT_2W_WITH_SATURATED_LANE";
                cohorts[cohort]++;
            }
        }

        var encoder = RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(width).Netlist;
        var compose = ExperimentalHardware.BuildBinaryExponentCompose(width).Netlist;
        var decoder = RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(width).Netlist;
        var multiplier = BaselineHardware.BuildShiftAddMultiplier(width);
        foreach (var pair in cohorts)
        {
            var cases = pair.Value;
            var magnitudeContract = pair.Key == "FIT_W_EXACT";
            var obligation = magnitudeContract ? "MAGNITUDE_FINAL" : "STRUCTURAL_FINAL";
            var traceId = $"A-W{width}-{pair.Key}";
            rows.Add(new Build002WorkloadRow(
                "A",
                traceId,
                "VFU-BINEXP-S4-COLD-INTEGRATED",
                width,
                "COLD_MAG",
                obligation,
                "INGRESS",
                "STRUCTURAL_DECLARED_ADAPTER",
                "TWO_EXACT_CATALOG_ENCODERS_SUPPORT_TAGGED",
                checked(cases * 2),
                checked(cases * 2L),
                checked(cases * 2L * encoder.Metrics.Nand2Static),
                NotMeasured,
                0,
                0,
                checked(cases * 2),
                0,
                0,
                string.Empty,
                pair.Key,
                "Both W-bit operands pay the implemented cold valuation acquisition path.",
                "REQUIRES_FACTOR_DISCOVERY",
                NotMeasured,
                NotMeasured));
            rows.Add(new Build002WorkloadRow(
                "A",
                traceId,
                "VFU-BINEXP-S4-COLD-INTEGRATED",
                width,
                "COLD_MAG",
                obligation,
                "EXECUTE",
                "STRUCTURAL_DECLARED_OPERATION_ONLY",
                pair.Key,
                cases,
                cases,
                checked((long)cases * compose.Metrics.Nand2Static),
                NotMeasured,
                0,
                0,
                0,
                0,
                0,
                string.Empty,
                pair.Key,
                "Native compose is charged after both cold encodes; persistent boundaries remain visible in static_costs.csv.",
                "REPRESENTATION_LOCAL",
                NotMeasured,
                NotMeasured));
            rows.Add(new Build002WorkloadRow(
                "A",
                traceId,
                "VFU-BINEXP-S4-COLD-INTEGRATED",
                width,
                "COLD_MAG",
                obligation,
                "EGRESS",
                magnitudeContract ? "STRUCTURAL_DECLARED_ADAPTER" : "CONTRACT_LIMIT",
                magnitudeContract ? "EXACT_W_BIT_RECONSTRUCTION" : "NO_W_BIT_MAGNITUDE_FOR_2W_PRODUCT",
                magnitudeContract ? cases : 0,
                magnitudeContract ? cases : 0,
                magnitudeContract ? checked((long)cases * decoder.Metrics.Nand2Static) : NotMeasured,
                NotMeasured,
                0,
                0,
                0,
                magnitudeContract ? cases : 0,
                0,
                string.Empty,
                pair.Key,
                magnitudeContract
                    ? "Every exact W-bit structural product pays the implemented reconstruction path."
                    : "The frozen decoder has a W-bit magnitude port; a full 2W magnitude contract is unsupported and is not assigned zero cost.",
                "CROSS_REPRESENTATION",
                NotMeasured,
                NotMeasured));
            rows.Add(new Build002WorkloadRow(
                "A",
                traceId,
                "BIN-FU",
                width,
                "COLD_MAG",
                "MAGNITUDE_FINAL",
                "EXECUTE",
                "STRUCTURAL_DECLARED_EXHAUSTIVE_CASE_COUNT",
                "FULL_2W_PRODUCT",
                cases,
                cases,
                checked((long)cases * multiplier.Metrics.Nand2Static),
                NotMeasured,
                0,
                0,
                0,
                0,
                0,
                string.Empty,
                pair.Key,
                "The conventional multiplier satisfies the full 2W product contract for the identical smooth-input cohort.",
                "BINARY_MAGNITUDE_LOCAL",
                NotMeasured,
                NotMeasured));
        }
    }

    private static void AddStructuralTraceRows(
        List<Build002WorkloadRow> rows,
        Build002Trace trace,
        StructuralTraceExecution execution)
    {
        rows.Add(new Build002WorkloadRow(
            "B",
            trace.Id,
            "VFU-BINEXP-S4",
            trace.Width,
            "WARM_GENERATED",
            "STRUCTURAL_FINAL",
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
            "Shared VFU graph measured as an operation-only control; the integrated comparison uses VFU-BINEXP-S4-WARM.",
            "REPRESENTATION_LOCAL",
            execution.Measurement.InputBitTransitions,
            execution.Measurement.InitialNandTransitions));
    }

    private static Build002WorkloadRow BuildTraceAdapterRow(
        string experiment,
        Build002Trace trace,
        string implementation,
        string obligation,
        string phase,
        int uses,
        NandNetlist adapter,
        int encodes,
        int reconstructs,
        string operationClass,
        string notes) =>
        new(
            experiment,
            trace.Id,
            implementation,
            trace.Width,
            "WARM_GENERATED",
            obligation,
            phase,
            "STRUCTURAL_DECLARED_ADAPTER",
            "IMPLEMENTED_EXACT_ADAPTER",
            uses,
            uses,
            checked((long)uses * adapter.Metrics.Nand2Static),
            NotMeasured,
            0,
            0,
            encodes,
            reconstructs,
            0,
            string.Empty,
            trace.Feature,
            notes + " Settled transitions are not aggregated; -1 denotes NOT_MEASURED.",
            operationClass,
            NotMeasured,
            NotMeasured);

    private static Build002WorkloadRow RunIntegratedWarmStructuralTrace(
        Build002Trace trace,
        string obligation,
        CorrectnessAccumulator correctness)
    {
        var machine = WarmStructuralHardware.BuildScaleCancelMachine(trace.Width);
        var semantic = ValuationHardwareState.Identity(trace.Width);
        var state = WarmStructuralHardware.EncodeExactState(machine, semantic);
        NandEvaluation? previous = null;
        long nandEvaluations = 0;
        long nandTransitions = 0;
        long stateTransitions = 0;
        long inputTransitions = 0;
        long initialTransitions = 0;
        var rejections = 0;
        for (var index = 0; index < trace.Steps.Count; index++)
        {
            var step = trace.Steps[index];
            var operation = step.Operation switch
            {
                Build002TraceOperation.ScaleKnownFactor => WarmStructuralOperation.Scale,
                Build002TraceOperation.CancelKnownFactor => WarmStructuralOperation.Cancel,
                _ => throw new InvalidOperationException("Experiment B contains only scale/cancel steps."),
            };
            var beforeState = state;
            var evaluated = machine.Netlist.Evaluate(
                WarmStructuralHardware.EncodeControl(machine, step.Operand, operation),
                state,
                previous,
                compareWithAllOff: previous is null);
            var factor = ValuationHardwareState.Power(trace.Width, step.Operand, 1).Value!;
            var expected = operation == WarmStructuralOperation.Scale
                ? semantic.Compose(factor)
                : semantic.Cancel(factor);
            var rejected = !expected.Succeeded || !expected.Value!.IsExact;
            correctness.Check(
                IsOn(evaluated.Outputs[machine.Ports.RejectOutput]) == rejected,
                $"B/WARM_STRUCTURAL/STATUS/{trace.Id}/{index}");
            if (rejected)
            {
                rejections++;
            }
            else
            {
                semantic = expected.Value!;
            }

            state = WarmStructuralHardware.AdvanceState(evaluated);
            var decoded = WarmStructuralHardware.DecodeNextExactState(machine, evaluated);
            correctness.Check(
                decoded.Succeeded && SameValuationState(decoded.Value!, semantic),
                $"B/WARM_STRUCTURAL/NEXT_STATE/{trace.Id}/{index}");
            nandEvaluations += evaluated.NandEvaluations;
            stateTransitions += CountBitChanges(beforeState, evaluated.DffNextStates);
            inputTransitions += evaluated.InputTransitions;
            if (previous is null)
            {
                initialTransitions = evaluated.NandOutputTransitions;
            }
            else
            {
                nandTransitions += evaluated.NandOutputTransitions;
            }

            previous = evaluated;
        }

        correctness.Check(
            rejections == trace.ExpectedRejectedCancellations,
            $"B/WARM_STRUCTURAL/REJECTION_COUNT/{trace.Id}");
        return new Build002WorkloadRow(
            "B",
            trace.Id,
            "VFU-BINEXP-S4-WARM",
            trace.Width,
            "WARM_GENERATED",
            obligation,
            "EXECUTE",
            "STRUCTURAL_DECLARED_INTEGRATED_EXHAUSTIVE",
            "FULL_EXACT_S4_GENERATED",
            trace.Steps.Count,
            trace.Steps.Count,
            nandEvaluations,
            nandTransitions,
            stateTransitions,
            rejections,
            0,
            0,
            0,
            FormatState(semantic),
            trace.Feature,
            "Persistent DFFs and atomic hold are charged; required output conversion, if any, is a separate phase row.",
            "REPRESENTATION_LOCAL",
            inputTransitions,
            initialTransitions);
    }

    private static Build002WorkloadRow RunIntegratedWarmBinaryTrace(
        Build002Trace trace,
        string obligation,
        CorrectnessAccumulator correctness)
    {
        var machine = WarmStructuralHardware.BuildBinaryScaleCancelMachine(trace.Width);
        var magnitude = trace.InitialMagnitude;
        var maximum = (1 << trace.Width) - 1;
        var state = WarmStructuralHardware.EncodeMagnitudeState(machine, magnitude);
        NandEvaluation? previous = null;
        long nandEvaluations = 0;
        long nandTransitions = 0;
        long stateTransitions = 0;
        long inputTransitions = 0;
        long initialTransitions = 0;
        var rejections = 0;
        for (var index = 0; index < trace.Steps.Count; index++)
        {
            var step = trace.Steps[index];
            var operation = step.Operation switch
            {
                Build002TraceOperation.ScaleKnownFactor => WarmStructuralOperation.Scale,
                Build002TraceOperation.CancelKnownFactor => WarmStructuralOperation.Cancel,
                _ => throw new InvalidOperationException("Experiment B contains only scale/cancel steps."),
            };
            var beforeState = state;
            var evaluated = machine.Netlist.Evaluate(
                WarmStructuralHardware.EncodeControl(machine, step.Operand, operation),
                state,
                previous,
                compareWithAllOff: previous is null);
            var expectedMagnitude = magnitude;
            var expectedSucceeded = TryApplyMagnitudeStep(ref expectedMagnitude, maximum, step);
            correctness.Check(
                IsOn(evaluated.Outputs[machine.Ports.RejectOutput]) == !expectedSucceeded,
                $"B/WARM_BINARY/STATUS/{trace.Id}/{index}");
            if (expectedSucceeded)
            {
                magnitude = expectedMagnitude;
            }
            else
            {
                rejections++;
            }

            state = WarmStructuralHardware.AdvanceState(evaluated);
            correctness.Check(
                WarmStructuralHardware.DecodeNextMagnitude(machine, evaluated) == magnitude,
                $"B/WARM_BINARY/NEXT_STATE/{trace.Id}/{index}");
            nandEvaluations += evaluated.NandEvaluations;
            stateTransitions += CountBitChanges(beforeState, evaluated.DffNextStates);
            inputTransitions += evaluated.InputTransitions;
            if (previous is null)
            {
                initialTransitions = evaluated.NandOutputTransitions;
            }
            else
            {
                nandTransitions += evaluated.NandOutputTransitions;
            }

            previous = evaluated;
        }

        correctness.Check(
            rejections == trace.ExpectedRejectedCancellations,
            $"B/WARM_BINARY/REJECTION_COUNT/{trace.Id}");
        return new Build002WorkloadRow(
            "B",
            trace.Id,
            "BIN-SCALE-CANCEL-WARM",
            trace.Width,
            "WARM_GENERATED",
            obligation,
            "EXECUTE",
            "STRUCTURAL_DECLARED_INTEGRATED_EXHAUSTIVE",
            "FULL_BINARY",
            trace.Steps.Count,
            trace.Steps.Count,
            nandEvaluations,
            nandTransitions,
            stateTransitions,
            rejections,
            0,
            0,
            0,
            magnitude.ToString(CultureInfo.InvariantCulture),
            trace.Feature,
            "Persistent DFFs and atomic hold are charged; required output conversion, if any, is a separate phase row.",
            "BINARY_MAGNITUDE_LOCAL",
            inputTransitions,
            initialTransitions);
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
                "Denominator-zero rejection is atomic; no divide circuit is executed.",
                "BINARY_MAGNITUDE_LOCAL");
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
            "GCD cycles and NAND evaluations are exact; end-to-end settled transitions are NOT_MEASURED.",
            "BINARY_MAGNITUDE_LOCAL");
    }

    private static Build002WorkloadRow RunStructuralRational(
        Build002RationalCase rational,
        CorrectnessAccumulator correctness)
    {
        if (rational.Denominator == 0)
        {
            return new Build002WorkloadRow(
                "D",
                rational.Id + "-STRUCTURAL",
                "VFU-BINEXP-S4-RATIONAL",
                rational.Width,
                "WARM_RESIDENT",
                "STRUCTURAL_FINAL",
                "EXECUTE",
                "SEMANTIC_REJECTION",
                "REJECTED_DENOMINATOR_ZERO",
                1,
                1,
                0,
                0,
                0,
                1,
                0,
                0,
                0,
                string.Empty,
                rational.Feature,
                "Denominator-zero rejection occurs before a structural datapath operation; controller static cost is reported as NOT_MEASURED.",
                "REPRESENTATION_LOCAL");
        }

        var numerator = ValuationProjection(rational.Width, rational.Numerator);
        var denominator = ValuationProjection(rational.Width, rational.Denominator);
        var meet = ExperimentalHardware.BuildBinaryExponentMeet(rational.Width);
        var cancel = ExperimentalHardware.BuildBinaryExponentCancel(rational.Width);
        var meetEval = meet.Netlist.Evaluate(
            ExperimentalInputs(meet, numerator.State, denominator.State, opcode: null),
            compareWithAllOff: true);
        var common = numerator.State.Meet(denominator.State);
        correctness.Check(common.Succeeded, $"D/STRUCTURAL/MEET/{rational.Id}");
        CheckExperimentalResult(
            meet,
            meetEval,
            common.Value!,
            correctness,
            $"D/STRUCTURAL/MEET_RESULT/{rational.Id}");

        var numeratorEval = cancel.Netlist.Evaluate(
            ExperimentalInputs(cancel, numerator.State, common.Value!, opcode: null),
            compareWithAllOff: true);
        var numeratorReduced = numerator.State.Cancel(common.Value!);
        correctness.Check(numeratorReduced.Succeeded, $"D/STRUCTURAL/CANCEL_NUM/{rational.Id}");
        CheckExperimentalResult(
            cancel,
            numeratorEval,
            numeratorReduced.Value!,
            correctness,
            $"D/STRUCTURAL/CANCEL_NUM_RESULT/{rational.Id}");

        var denominatorEval = cancel.Netlist.Evaluate(
            ExperimentalInputs(cancel, denominator.State, common.Value!, opcode: null),
            compareWithAllOff: true);
        var denominatorReduced = denominator.State.Cancel(common.Value!);
        correctness.Check(denominatorReduced.Succeeded, $"D/STRUCTURAL/CANCEL_DEN/{rational.Id}");
        CheckExperimentalResult(
            cancel,
            denominatorEval,
            denominatorReduced.Value!,
            correctness,
            $"D/STRUCTURAL/CANCEL_DEN_RESULT/{rational.Id}");

        var fullySupported = numerator.FullySupported && denominator.FullySupported;
        if (fullySupported)
        {
            var binaryGcd = OracleGcd(rational.Numerator, rational.Denominator);
            correctness.Check(
                numeratorReduced.Value!.Reconstruct().Value!.Value == rational.Numerator / binaryGcd &&
                denominatorReduced.Value!.Reconstruct().Value!.Value == rational.Denominator / binaryGcd,
                $"D/STRUCTURAL/FULL_REDUCTION/{rational.Id}");
        }

        var nandEvaluations = (long)meetEval.NandEvaluations +
                              numeratorEval.NandEvaluations +
                              denominatorEval.NandEvaluations;
        var transitions = (long)meetEval.NandOutputTransitions +
                          numeratorEval.NandOutputTransitions +
                          denominatorEval.NandOutputTransitions;
        return new Build002WorkloadRow(
            "D",
            rational.Id + "-STRUCTURAL",
            "VFU-BINEXP-S4-RATIONAL",
            rational.Width,
            "WARM_RESIDENT",
            "STRUCTURAL_FINAL",
            "EXECUTE",
            "COMPOSITE_STRUCTURAL_DECLARED",
            fullySupported ? "FULLY_REDUCED_S4_STRUCTURAL" : "CATALOG_PROJECTION_ONLY",
            3,
            3,
            nandEvaluations,
            transitions,
            0,
            0,
            0,
            0,
            0,
            $"{FormatState(numeratorReduced.Value!)}/{FormatState(denominatorReduced.Value!)}",
            rational.Feature,
            (fullySupported
                ? "MEET plus two checked CANCEL operations fully reduce this S4-supported rational; integrated rational DFF/control is not included."
                : "MEET plus two checked CANCEL operations remove only catalog factors; an unsupported shared cofactor can remain, so this is not a fully reduced rational."),
            "REPRESENTATION_LOCAL",
            NotMeasured,
            NotMeasured);
    }

    private static List<Build002WorkloadRow> RunIntegratedBinaryMixedTrace(
        string experiment,
        Build002Trace trace,
        CorrectnessAccumulator correctness)
    {
        var machine = BinaryMagnitudeDatapathHardware.Build(trace.Width);
        var magnitude = trace.InitialMagnitude;
        var maximum = (1 << trace.Width) - 1;
        var state = BinaryMagnitudeDatapathHardware.EncodeState(machine, 0);
        NandEvaluation? previous = null;
        var load = machine.Netlist.Evaluate(
            BinaryMagnitudeDatapathHardware.EncodeInputs(
                machine,
                BinaryMagnitudeDatapathOperation.Load,
                operand: trace.InitialMagnitude),
            state,
            compareWithAllOff: true);
        correctness.Check(
            IsOn(load.Outputs[machine.Ports.AcceptedOutput]) &&
            BinaryMagnitudeDatapathHardware.DecodeNextMagnitude(machine, load) == magnitude,
            $"{experiment}/BINARY_MIXED/{trace.Id}/LOAD");
        var ingressStateTransitions = CountBitChanges(state, load.DffNextStates);
        state = BinaryMagnitudeDatapathHardware.AdvanceState(load);
        previous = load;
        long nands = 0;
        long transitions = 0;
        long inputTransitions = 0;
        long stateTransitions = 0;
        var rejections = 0;
        for (var index = 0; index < trace.Steps.Count; index++)
        {
            var step = trace.Steps[index];
            var operation = step.Operation switch
            {
                Build002TraceOperation.ScaleKnownFactor => BinaryMagnitudeDatapathOperation.Scale,
                Build002TraceOperation.CancelKnownFactor => BinaryMagnitudeDatapathOperation.Cancel,
                Build002TraceOperation.AddMagnitude => BinaryMagnitudeDatapathOperation.AddMagnitude,
                _ => throw new InvalidOperationException("Undefined binary mixed instruction."),
            };
            var expected = magnitude;
            var succeeded = TryApplyMagnitudeStep(ref expected, maximum, step);
            var before = state;
            var evaluated = machine.Netlist.Evaluate(
                BinaryMagnitudeDatapathHardware.EncodeInputs(
                    machine,
                    operation,
                    prime: step.Operation == Build002TraceOperation.AddMagnitude ? 2 : step.Operand,
                    operand: step.Operation == Build002TraceOperation.AddMagnitude ? step.Operand : 0),
                state,
                previous);
            correctness.Check(
                IsOn(evaluated.Outputs[machine.Ports.AcceptedOutput]) == succeeded &&
                IsOn(evaluated.Outputs[machine.Ports.RejectOutput]) == !succeeded,
                $"{experiment}/BINARY_MIXED/{trace.Id}/STEP/{index}/STATUS");
            if (succeeded)
            {
                magnitude = expected;
            }
            else
            {
                rejections++;
                correctness.Check(
                    CountBitChanges(before, evaluated.DffNextStates) == 0,
                    $"{experiment}/BINARY_MIXED/{trace.Id}/STEP/{index}/ATOMIC_HOLD");
            }

            correctness.Check(
                BinaryMagnitudeDatapathHardware.DecodeNextMagnitude(machine, evaluated) == magnitude,
                $"{experiment}/BINARY_MIXED/{trace.Id}/STEP/{index}/MAGNITUDE");
            nands += evaluated.NandEvaluations;
            transitions += evaluated.NandOutputTransitions;
            inputTransitions += evaluated.InputTransitions;
            stateTransitions += CountBitChanges(before, evaluated.DffNextStates);
            state = BinaryMagnitudeDatapathHardware.AdvanceState(evaluated);
            previous = evaluated;
        }

        return
        [
            new Build002WorkloadRow(
                experiment,
                trace.Id + "-BASELINE",
                "BIN-MIXED-WARM",
                trace.Width,
                "WARM_GENERATED",
                "MAGNITUDE_EVERY_OP",
                "INGRESS",
                machine.EvidenceClass,
                "DIRECT_BINARY_LOAD",
                1,
                1,
                load.NandEvaluations,
                0,
                ingressStateTransitions,
                0,
                1,
                0,
                0,
                trace.InitialMagnitude.ToString(CultureInfo.InvariantCulture),
                trace.Feature,
                "Matched integrated binary LOAD; no valuation acquisition is performed.",
                "BINARY_MAGNITUDE_LOCAL",
                0,
                load.NandOutputTransitions),
            new Build002WorkloadRow(
                experiment,
                trace.Id + "-BASELINE",
                "BIN-MIXED-WARM",
                trace.Width,
                "WARM_GENERATED",
                "MAGNITUDE_EVERY_OP",
                "EXECUTE",
                machine.EvidenceClass,
                "FULL_BINARY_MAGNITUDE",
                trace.Steps.Count,
                trace.Steps.Count,
                nands,
                transitions,
                stateTransitions,
                rejections,
                0,
                0,
                0,
                magnitude.ToString(CultureInfo.InvariantCulture),
                trace.Feature,
                "Matched integrated binary SCALE/CANCEL/ADD baseline with the same overlapping arithmetic, control, status, and atomic-hold contract.",
                "BINARY_MAGNITUDE_LOCAL",
                inputTransitions,
                0),
        ];
    }

    private static List<Build002WorkloadRow> RunIntegratedSidecarTrace(
        string experiment,
        Build002Trace trace,
        bool refreshAfterInvalidAddition,
        CorrectnessAccumulator correctness)
    {
        var machine = SidecarDatapathHardware.Build(trace.Width);
        var semantic = BinaryValuationSidecar.Encode(trace.Width, trace.InitialMagnitude).Value!;
        var scalar = trace.InitialMagnitude;
        var maximum = (1 << trace.Width) - 1;
        var rawBounds = new int[machine.Ports.Lanes.Count];
        var state = SidecarDatapathHardware.EncodeRawState(
            machine,
            magnitude: 0,
            valid: false,
            rawBounds);
        NandEvaluation? previous = null;
        long ingressNands = 0;
        long ingressInitialTransitions = 0;
        long ingressStateTransitions = 0;
        long executeCycles = 0;
        long executeNands = 0;
        long executeTransitions = 0;
        long executeInputTransitions = 0;
        long executeStateTransitions = 0;
        long recoveryCycles = 0;
        long recoveryNands = 0;
        long recoveryTransitions = 0;
        long recoveryInputTransitions = 0;
        long recoveryStateTransitions = 0;
        var rejections = 0;
        var refreshes = 0;
        var variant = refreshAfterInvalidAddition ? "EAGER-REFRESH" : "DELAYED-REFRESH";
        var context = $"{experiment}/SIDECAR/{variant}/{trace.Id}";

        var load = Evaluate(SidecarDatapathOperation.Load, operand: trace.InitialMagnitude);
        correctness.Check(
            IsOn(load.Outputs[machine.Ports.AcceptedOutput]) &&
            !IsOn(load.Outputs[machine.Ports.RejectOutput]),
            context + "/LOAD/STATUS");
        ingressNands = load.NandEvaluations;
        ingressInitialTransitions = load.NandOutputTransitions;
        ingressStateTransitions = CountBitChanges(state, load.DffNextStates);
        state = SidecarDatapathHardware.AdvanceState(load);
        previous = load;
        CheckSnapshot(SidecarDatapathHardware.DecodeNextState(machine, load), semantic, context + "/LOAD");

        for (var index = 0; index < trace.Steps.Count; index++)
        {
            var step = trace.Steps[index];
            var result = step.Operation switch
            {
                Build002TraceOperation.ScaleKnownFactor => semantic.ScaleKnownFactor(step.Operand),
                Build002TraceOperation.CancelKnownFactor => semantic.CancelKnownFactor(step.Operand),
                Build002TraceOperation.AddMagnitude => semantic.Add(
                    BinaryValuationSidecar.Encode(trace.Width, step.Operand).Value!),
                _ => throw new InvalidOperationException("Undefined sidecar trace instruction."),
            };
            var expectedMagnitude = scalar;
            var expectedSucceeded = TryApplyMagnitudeStep(ref expectedMagnitude, maximum, step);
            correctness.Check(result.Succeeded == expectedSucceeded, $"{context}/STEP/{index}/ORACLE_STATUS");
            var operation = step.Operation switch
            {
                Build002TraceOperation.ScaleKnownFactor => SidecarDatapathOperation.Scale,
                Build002TraceOperation.CancelKnownFactor => SidecarDatapathOperation.Cancel,
                Build002TraceOperation.AddMagnitude => SidecarDatapathOperation.AddMagnitude,
                _ => throw new InvalidOperationException("Undefined sidecar trace instruction."),
            };
            var before = state;
            var evaluated = Evaluate(
                operation,
                prime: step.Operation == Build002TraceOperation.AddMagnitude ? 2 : step.Operand,
                operand: step.Operation == Build002TraceOperation.AddMagnitude ? step.Operand : 0);
            var accepted = IsOn(evaluated.Outputs[machine.Ports.AcceptedOutput]);
            correctness.Check(accepted == expectedSucceeded, $"{context}/STEP/{index}/HARDWARE_STATUS");
            correctness.Check(
                IsOn(evaluated.Outputs[machine.Ports.RejectOutput]) == !expectedSucceeded,
                $"{context}/STEP/{index}/REJECT");
            executeCycles++;
            executeNands += evaluated.NandEvaluations;
            executeTransitions += evaluated.NandOutputTransitions;
            executeInputTransitions += evaluated.InputTransitions;
            executeStateTransitions += CountBitChanges(before, evaluated.DffNextStates);
            state = SidecarDatapathHardware.AdvanceState(evaluated);
            previous = evaluated;
            if (!expectedSucceeded)
            {
                rejections++;
                correctness.Check(
                    CountBitChanges(before, evaluated.DffNextStates) == 0,
                    $"{context}/STEP/{index}/ATOMIC_HOLD");
                continue;
            }

            scalar = expectedMagnitude;
            semantic = result.Value!;
            CheckSnapshot(
                SidecarDatapathHardware.DecodeNextState(machine, evaluated),
                semantic,
                $"{context}/STEP/{index}");

            if (step.Operation != Build002TraceOperation.AddMagnitude ||
                semantic.Valid ||
                !refreshAfterInvalidAddition)
            {
                continue;
            }

            before = state;
            var refreshed = Evaluate(SidecarDatapathOperation.Refresh);
            correctness.Check(
                IsOn(refreshed.Outputs[machine.Ports.AcceptedOutput]),
                $"{context}/STEP/{index}/REFRESH_STATUS");
            semantic = semantic.Refresh().Value!;
            recoveryCycles++;
            recoveryNands += refreshed.NandEvaluations;
            recoveryTransitions += refreshed.NandOutputTransitions;
            recoveryInputTransitions += refreshed.InputTransitions;
            recoveryStateTransitions += CountBitChanges(before, refreshed.DffNextStates);
            refreshes++;
            state = SidecarDatapathHardware.AdvanceState(refreshed);
            previous = refreshed;
            CheckSnapshot(
                SidecarDatapathHardware.DecodeNextState(machine, refreshed),
                semantic,
                $"{context}/STEP/{index}/REFRESH");
        }

        correctness.Check(semantic.Magnitude == scalar, context + "/FINAL_MAGNITUDE");
        var traceId = trace.Id + "-" + variant;
        var support = semantic.Valid
            ? "AUTHORITATIVE_MAGNITUDE_EXACT_S4_SIDECAR"
            : "AUTHORITATIVE_MAGNITUDE_SOUND_LOWER_BOUNDS_VALID_FALSE";
        var rows = new List<Build002WorkloadRow>
        {
            new(
                experiment,
                traceId,
                "BIN+VSC-S4",
                trace.Width,
                "WARM_GENERATED",
                "MAGNITUDE_EVERY_OP",
                "INGRESS",
                machine.EvidenceClass,
                "FULL_BINARY_TO_EXACT_S4_LOAD",
                1,
                1,
                ingressNands,
                0,
                ingressStateTransitions,
                0,
                1,
                0,
                0,
                trace.InitialMagnitude.ToString(CultureInfo.InvariantCulture),
                trace.Feature,
                "One integrated LOAD acquires exact S4 thresholds from the authoritative binary magnitude.",
                "REQUIRES_FACTOR_DISCOVERY",
                0,
                ingressInitialTransitions),
            new(
                experiment,
                traceId,
                "BIN+VSC-S4",
                trace.Width,
                "WARM_GENERATED",
                "MAGNITUDE_EVERY_OP",
                "EXECUTE",
                machine.EvidenceClass,
                support,
                trace.Steps.Count,
                executeCycles,
                executeNands,
                executeTransitions,
                executeStateTransitions,
                rejections,
                0,
                0,
                0,
                scalar.ToString(CultureInfo.InvariantCulture),
                trace.Feature,
                "Integrated SCALE/CANCEL/ADD hardware preserves authoritative magnitude; addition retains only proven lower bounds unless the unequal-valuation theorem makes them exact.",
                "REPRESENTATION_LOCAL",
                executeInputTransitions,
                0),
        };
        if (recoveryCycles > 0)
        {
            rows.Add(new Build002WorkloadRow(
                experiment,
                traceId,
                "BIN+VSC-S4",
                trace.Width,
                "WARM_GENERATED",
                "MAGNITUDE_EVERY_OP",
                "ADDITION_RECOVERY",
                machine.EvidenceClass,
                "EXACT_S4_REFRESH_FROM_AUTHORITATIVE_MAGNITUDE",
                refreshes,
                recoveryCycles,
                recoveryNands,
                recoveryTransitions,
                recoveryStateTransitions,
                0,
                0,
                0,
                refreshes,
                scalar.ToString(CultureInfo.InvariantCulture),
                trace.Feature,
                "Each refresh re-discovers exact S4 thresholds after an addition invalidated exact metadata.",
                "CROSS_REPRESENTATION",
                recoveryInputTransitions,
                0));
        }

        return rows;

        NandEvaluation Evaluate(
            SidecarDatapathOperation operation,
            int prime = 2,
            int operand = 0) =>
            machine.Netlist.Evaluate(
                SidecarDatapathHardware.EncodeInputs(machine, operation, prime, operand: operand),
                state,
                previous,
                compareWithAllOff: previous is null);

        void CheckSnapshot(
            SidecarDatapathStateSnapshot actual,
            BinaryValuationSidecar expected,
            string snapshotContext)
        {
            correctness.Check(actual.Magnitude == expected.Magnitude, snapshotContext + "/MAGNITUDE");
            correctness.Check(actual.Valid == expected.Valid, snapshotContext + "/VALID");
            for (var lane = 0; lane < machine.Ports.Lanes.Count; lane++)
            {
                var layout = machine.Ports.Lanes[lane];
                var expectedBound = Enumerable.Range(1, layout.Cap)
                    .Count(threshold => expected.ThresholdAt(layout.Prime, threshold));
                correctness.Check(
                    actual.LowerBounds[lane] == expectedBound,
                    snapshotContext + $"/LANE/{layout.Prime}");
            }
        }
    }

    private static List<Build002IngressEgressRow> BuildIngressEgress()
    {
        var rows = new List<Build002IngressEgressRow>();
        foreach (var width in Build002Protocol.Widths)
        {
            var cases = 1 << width;
            var smoothCases = SmoothStates(width).Count;
            var coldEncoder = RepresentationAdapterHardware.BuildMagnitudeToBinaryExponent(width);
            var reconstruction = RepresentationAdapterHardware.BuildBinaryExponentToMagnitude(width);
            var sidecar = SidecarDatapathHardware.Build(width);
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
                coldEncoder.EvidenceClass,
                cases,
                cases,
                cases,
                checked((long)cases * coldEncoder.Netlist.Metrics.Nand2Static),
                "EXACT_CATALOG_VALUATIONS_SUPPORT_TAGGED",
                "The adapter reports whether the magnitude is fully S4-supported; unsupported cofactors are not silently encoded."));
            rows.Add(new Build002IngressEgressRow(
                "VFU-BINEXP-S4",
                width,
                "EGRESS",
                "S4_STATE_TO_BINARY_MAGNITUDE",
                reconstruction.EvidenceClass,
                smoothCases,
                smoothCases,
                smoothCases,
                checked((long)smoothCases * reconstruction.Netlist.Metrics.Nand2Static),
                "FULL_EXACT_COMMON_W_BIT_DOMAIN",
                "Malformed, saturated, noncanonical-zero, and out-of-range products are explicit rejection statuses."));
            rows.Add(new Build002IngressEgressRow(
                "BIN+VSC-S4",
                width,
                "INGRESS",
                "EXACT_THRESHOLD_SIDECAR_ENCODE",
                sidecar.EvidenceClass,
                cases,
                cases,
                cases,
                checked((long)cases * sidecar.Netlist.Metrics.Nand2Static),
                "FULL_BINARY_TO_EXACT_S4_INTEGRATED_LOAD",
                "Every W-bit magnitude is acquired by the integrated NAND LOAD path; authoritative magnitude and exact sidecar state update together."));
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
                "STRUCTURAL_DECLARED_LOSSY_CONTROL",
                0,
                4,
                1,
                5,
                16,
                "PRIME_PRESENCE_QUERY",
                "LOSES_MULTIPLICITY",
                "Implemented binary-exponent and thermometer projections are reported in static_costs.csv; neither is equivalent to valuation state."));
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

                    _ = TryApplyMagnitudeStep(ref left, maximum, step);
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

    private static bool TryApplyMagnitudeStep(
        ref int magnitude,
        int maximum,
        Build002TraceStep step)
    {
        switch (step.Operation)
        {
            case Build002TraceOperation.ScaleKnownFactor:
                if (magnitude > maximum / step.Operand)
                {
                    return false;
                }

                magnitude *= step.Operand;
                return true;
            case Build002TraceOperation.CancelKnownFactor:
                if (magnitude % step.Operand != 0)
                {
                    return false;
                }

                magnitude /= step.Operand;
                return true;
            case Build002TraceOperation.AddMagnitude:
                if (magnitude > maximum - step.Operand)
                {
                    return false;
                }

                magnitude += step.Operand;
                return true;
            default:
                throw new InvalidOperationException("Undefined Build 002 trace operation.");
        }
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
                    ? thresholdBits
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

    private static object BuildCoverage(
        string repositoryRoot,
        Build002HdlImportReceipt hdl,
        IReadOnlyList<Build002StaticCostRow> staticRows,
        IReadOnlyList<Build002WorkloadRow> workloadRows,
        CorrectnessReceipt correctness)
    {
        var inherited = CheckInheritedEvidence(repositoryRoot);
        var classification = Classify(repositoryRoot, hdl, staticRows, workloadRows, correctness);
        var coverage = RequiredExperiments
            .Select(experiment =>
            {
                var rows = workloadRows.Where(row => row.Experiment == experiment).ToArray();
                var widths = rows.Select(row => row.Width).Distinct().Order().ToArray();
                var completeWidths = Build002Protocol.Widths.All(width => widths.Contains(width));
                return new
                {
                    Experiment = experiment,
                    Status = completeWidths ? "COMPLETE_BOUNDED" : "INCOMPLETE",
                    Widths = widths,
                    RowCount = rows.Length,
                    IntegratedRows = rows.Count(row => row.EvidenceClass.Contains("INTEGRATED", StringComparison.Ordinal)),
                    NotMeasuredCostCells = rows.Sum(CountNotMeasuredCostCells),
                    Notes = CoverageNote(experiment),
                };
            })
            .ToArray();
        var operationClassesComplete = workloadRows.All(row => row.OperationClass != "UNCLASSIFIED");
        var decisionEarned = classification != PartialClassification;
        return new
        {
            ProtocolId = Build002Protocol.Id,
            Classification = classification,
            Widths = Build002Protocol.Widths,
            Coverage = coverage,
            InheritedEvidence = inherited,
            Correctness = new
            {
                correctness.Status,
                correctness.CheckCount,
                correctness.FailureCount,
                ZeroSkippedRequired = true,
                Notes = "The generated arithmetic receipt has no skip mechanism; repository test and CI receipts independently require zero skipped tests.",
            },
            OperationClassesComplete = operationClassesComplete,
            Hdl = new
            {
                hdl.Status,
                hdl.Complete,
                hdl.VerificationCaseCount,
                hdl.FormalCaseCount,
                hdl.SynthesisRowCount,
                hdl.WarningCountsMeasured,
                hdl.WarningCountsNotMeasured,
                SourceHashes = new
                {
                    Verification = hdl.VerificationSummarySourceSha256,
                    Synthesis = hdl.SynthesisMetricsSourceSha256,
                    Toolchain = hdl.ToolchainBootstrapSourceSha256,
                },
            },
            DecisionEarned = decisionEarned,
            DecisionRules = BuildDecisionRules(staticRows),
            Limits = new[]
            {
                "Logical NAND2/DFF evidence is not a placed, routed, timed, or power-characterized chip.",
                "S4 is a fixed bounded catalog; catalog projections are never relabeled as general factorization.",
                "Some composite GCD/LCM/rational settled-transition totals remain NOT_MEASURED and are not used for an advantage claim.",
                "W8 sidecar addition uses the frozen 20,000 gate-level cases plus exhaustive semantic differential checking.",
            },
        };

        static int CountNotMeasuredCostCells(Build002WorkloadRow row)
        {
            var count = 0;
            count += row.Cycles < 0 ? 1 : 0;
            count += row.NandEvaluations < 0 ? 1 : 0;
            count += row.NandOutputTransitions < 0 ? 1 : 0;
            count += row.StateBitTransitions < 0 ? 1 : 0;
            count += row.InputBitTransitions < 0 ? 1 : 0;
            count += row.InitialNandTransitions < 0 ? 1 : 0;
            return count;
        }

        static string CoverageNote(string experiment) => experiment switch
        {
            "A" => "Binary multiply, binary-exponent compose, thermometer compose, and explicit adapters are separated by phase and contract.",
            "B" => "Eight frozen 32-step traces per width use integrated persistent binary and structural machines under all three output obligations.",
            "C" => "Full binary GCD/LCM semantics, S4 meet/join/divides, adapters, and exact-sidecar query hardware are bounded separately.",
            "D" => "Eight rational cases plus denominator-zero per width distinguish fully reduced binary results from bounded catalog projections.",
            "E" => "Eight mixed-addition traces per width run integrated sidecar hardware with delayed and eager refresh policies.",
            "F" => "All three frozen hostile trace families per width run integrated hardware; unsupported cofactors remain in authoritative magnitude.",
            "R" => "Binary exponent, thermometer, presence, and exact-sidecar representations and implemented adapters retain distinct evidence classes.",
            _ => string.Empty,
        };
    }

    private static string Classify(
        string repositoryRoot,
        Build002HdlImportReceipt hdl,
        IReadOnlyList<Build002StaticCostRow> staticRows,
        IReadOnlyList<Build002WorkloadRow> workloadRows,
        CorrectnessReceipt correctness)
    {
        var inherited = CheckInheritedEvidence(repositoryRoot);
        var experimentsComplete = RequiredExperiments
            .All(experiment => Build002Protocol.Widths.All(width =>
                workloadRows.Any(row => row.Experiment == experiment && row.Width == width)));
        var bContractsComplete = Build002Protocol.Widths.All(width =>
            RequiredBObligations.All(obligation =>
                workloadRows.Any(row => row.Experiment == "B" && row.Width == width &&
                    row.Implementation == "VFU-BINEXP-S4-WARM" && row.OutputObligation == obligation) &&
                workloadRows.Any(row => row.Experiment == "B" && row.Width == width &&
                    row.Implementation == "BIN-SCALE-CANCEL-WARM" && row.OutputObligation == obligation)));
        var mixedBaselinesComplete = Build002Protocol.Widths.All(width =>
            MixedExperiments.All(experiment =>
                workloadRows.Any(row => row.Experiment == experiment && row.Width == width &&
                    row.Implementation == "BIN+VSC-S4" && row.EvidenceClass.Contains("INTEGRATED", StringComparison.Ordinal)) &&
                workloadRows.Any(row => row.Experiment == experiment && row.Width == width &&
                    row.Implementation == "BIN-MIXED-WARM" && row.EvidenceClass.Contains("INTEGRATED", StringComparison.Ordinal))));
        var aPhasesComplete = Build002Protocol.Widths.All(width =>
            RequiredAPhases.All(phase =>
                workloadRows.Any(row => row.Experiment == "A" && row.Width == width && row.Phase == phase)));
        var sidecarQueriesComplete = Build002Protocol.Widths.All(width =>
            workloadRows.Any(row => row.Experiment == "C" && row.Width == width &&
                row.Implementation == "BIN+VSC-S4" && row.OutputObligation == "PREDICATE_ONLY"));
        var representationAdaptersComplete = Build002Protocol.Widths.All(width =>
            staticRows.Any(row => row.Width == width && row.Operation == "BINEXP_TO_THERMOMETER") &&
            staticRows.Any(row => row.Width == width && row.Operation == "THERMOMETER_TO_BINEXP"));
        var complete = inherited.Complete &&
                       hdl.Complete &&
                       correctness.FailureCount == 0 &&
                       experimentsComplete &&
                       bContractsComplete &&
                       mixedBaselinesComplete &&
                       aPhasesComplete &&
                       sidecarQueriesComplete &&
                       representationAdaptersComplete &&
                       workloadRows.All(row => row.OperationClass != "UNCLASSIFIED");
        if (!complete)
        {
            return PartialClassification;
        }

        var rules = BuildDecisionRuleValues(staticRows);
        if (rules.AlternativeArithmeticUnit)
        {
            return "ALTERNATIVE_ARITHMETIC_UNIT_CANDIDATE";
        }

        if (rules.PrimeStructuralCoprocessor)
        {
            return "PRIME_STRUCTURAL_COPROCESSOR_CANDIDATE";
        }

        if (rules.WarmStateSpecialized)
        {
            return "WARM_STATE_SPECIALIZED_ADVANTAGE";
        }

        return "NO_HARDWARE_ADVANTAGE";
    }

    private static object BuildDecisionRules(IReadOnlyList<Build002StaticCostRow> staticRows)
    {
        var values = BuildDecisionRuleValues(staticRows);
        return new
        {
            AlternativeArithmeticUnitCandidate = new
            {
                Satisfied = values.AlternativeArithmeticUnit,
                Reason = "The only integrated mixed-operation experimental machine is the exact sidecar. It is statically larger than the matched binary machine at W6 and W8 and therefore cannot Pareto-dominate E in both regimes.",
            },
            PrimeStructuralCoprocessorCandidate = new
            {
                Satisfied = values.PrimeStructuralCoprocessor,
                Reason = "The exact sidecar is larger than the full binary divide/query context at W6 and W8 before any 32-operation dynamic cost is charged; its static overhead cannot reach frozen Pareto break-even.",
            },
            WarmStateSpecializedAdvantage = new
            {
                Satisfied = values.WarmStateSpecialized,
                Reason = "The structural warm machine reduces NAND count, depth, wiring, and transitions, but uses more DFF/state/port bits at both W6 and W8. That tradeoff is not Pareto dominance under the frozen vector rule.",
            },
            UnexpectedArchitecture = new
            {
                Satisfied = false,
                Reason = "No architecture outside the preregistered structural, thermometer, presence, or exact-sidecar families survived the integrated adversarial tests.",
            },
            Fallback = "NO_HARDWARE_ADVANTAGE",
        };
    }

    private static DecisionRuleValues BuildDecisionRuleValues(
        IReadOnlyList<Build002StaticCostRow> staticRows)
    {
        var alternative = DecisionWidths.All(width =>
            ParetoDominates(
                FindStatic(staticRows, "BIN+VSC-S4", width, "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD"),
                FindStatic(staticRows, "BIN-MIXED-WARM", width, "INTEGRATED_LOAD_SCALE_CANCEL_ADD")));
        var coprocessor = DecisionWidths.All(width =>
            ParetoDominates(
                FindStatic(staticRows, "BIN+VSC-S4", width, "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD"),
                FindStatic(staticRows, "BIN-DIV", width, "UNSIGNED_RESTORING_DIVIDE")));
        var warm = DecisionWidths.All(width =>
            ParetoDominates(
                FindStatic(staticRows, "VFU-BINEXP-S4-WARM", width, "INTEGRATED_SCALE_CANCEL"),
                FindStatic(staticRows, "BIN-SCALE-CANCEL-WARM", width, "INTEGRATED_SCALE_CANCEL")));
        return new DecisionRuleValues(alternative, coprocessor, warm);
    }

    private static Build002StaticCostRow? FindStatic(
        IReadOnlyList<Build002StaticCostRow> rows,
        string implementation,
        int width,
        string operation) =>
        rows.SingleOrDefault(row => row.Implementation == implementation &&
            row.Width == width && row.Operation == operation);

    private static bool ParetoDominates(Build002StaticCostRow? candidate, Build002StaticCostRow? baseline)
    {
        if (candidate is null || baseline is null)
        {
            return false;
        }

        var candidateVector = StaticVector(candidate.Metrics);
        var baselineVector = StaticVector(baseline.Metrics);
        return candidateVector.Zip(baselineVector, (left, right) => left <= right).All(value => value) &&
               candidateVector.Zip(baselineVector, (left, right) => left < right).Any(value => value);
    }

    private static long[] StaticVector(NandStaticMetrics metrics) =>
    [
        metrics.Nand2Static,
        metrics.DffStatic,
        metrics.StateBits,
        metrics.PortBits,
        metrics.WireBits,
        metrics.ConnectionsStatic,
        metrics.MaximumFanout,
        metrics.CrossLaneConnections,
        metrics.UnitNandCriticalDepth,
    ];

    private static InheritedEvidenceReceipt CheckInheritedEvidence(string repositoryRoot)
    {
        const string build000ManifestExpected = "2F9ECD3DA3C2887EAA3E836D543FCCD7C0FF2139DD737FFA68475A9A5BE0935D";
        const string build001ManifestExpected = "E7A1FCF41C9E34253D398C250FDBB10D340755BF5CF07D8CDE79696C3CC48E14";
        const string build001ReportExpected = "806EF56F13025D2837BF2A1D915D692A06DDB0DCC7195AB4F7CF5887B23473F6";
        var build000 = HashIfPresent(Path.Combine(repositoryRoot, "results", "build000", "manifest.json"));
        var build001 = HashIfPresent(Path.Combine(repositoryRoot, "results", "build001", "manifest.json"));
        var report = HashIfPresent(Path.Combine(repositoryRoot, "BUILD_001_REPORT.md"));
        return new InheritedEvidenceReceipt(
            build000 == build000ManifestExpected &&
            build001 == build001ManifestExpected &&
            report == build001ReportExpected,
            build000,
            build001,
            report);

        static string HashIfPresent(string path) =>
            File.Exists(path) ? Build002Protocol.HashFile(path) : "MISSING";
    }

    private static void WriteReadme(
        string path,
        string invocationOutputArgument,
        CorrectnessReceipt correctness,
        string repositoryRoot,
        Build002HdlImportReceipt hdl,
        IReadOnlyList<Build002StaticCostRow> staticRows,
        IReadOnlyList<Build002WorkloadRow> workloadRows)
    {
        var command = BuildGeneratorCommand(invocationOutputArgument, hdl.Complete);
        var classification = Classify(repositoryRoot, hdl, staticRows, workloadRows, correctness);
        var content = $$"""
            # Build 002 generated evidence

            Protocol: `{{Build002Protocol.Id}}`

            Classification: `{{classification}}`

            This directory preserves cold/warm source regimes, output obligations, phase costs, support restrictions, and evidence classes as separate rows. A numeric cost of `-1` always means `NOT_MEASURED`; it never means zero cost and is excluded from comparison.

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
            - `ingress_egress.csv` charges implemented acquisition/reconstruction circuits explicitly.
            - `representation_search.csv` reports exact state-bit geometry as analytic evidence, not a synthesized circuit claim.
            - `addition_adversary.csv` and `hostile_support.csv` preserve semantic/support facts independently from integrated workload costs.
            - `synthesis_metrics.csv`, `formal_receipts.json`, and `toolchain.json` are validated, path-sanitized imports from the pinned common HDL flow.

            Correctness checks: {{correctness.CheckCount}}

            Correctness failures: {{correctness.FailureCount}}

            HDL evidence: `{{hdl.Status}}` ({{hdl.VerificationCaseCount}} checks; {{hdl.FormalCaseCount}} formal; {{hdl.SynthesisRowCount}} synthesis rows)

            ## Decision boundary

            The terminal negative is not a claim that valuation operations lack local advantages. The warm structural unit uses fewer NANDs, less depth, and fewer transitions for known-factor composition/cancellation, but it uses more state/port bits; the exact sidecar is larger than the matched binary datapath at W6 and W8; and cold adapters dominate the operation savings. Under the frozen Pareto rule none of those tradeoffs is a whole-machine hardware advantage. No universal scalar score or post-hoc weighted ranking is emitted.
            """;
        Build002Protocol.WriteLfText(path, content + "\n");
    }

    private static void WriteManifest(
        string repositoryRoot,
        string outputDirectory,
        string invocationOutputArgument,
        Build002HdlImportReceipt hdl,
        CorrectnessReceipt correctness,
        IReadOnlyList<Build002StaticCostRow> staticRows,
        IReadOnlyList<Build002WorkloadRow> workloadRows)
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
            GeneratorCommand = BuildGeneratorCommand(invocationOutputArgument, hdl.Complete),
            Runtime = RuntimeInformation.FrameworkDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Classification = Classify(repositoryRoot, hdl, staticRows, workloadRows, correctness),
            CorrectnessChecks = correctness.CheckCount,
            CorrectnessFailures = correctness.FailureCount,
            HdlImport = new
            {
                hdl.Status,
                hdl.Complete,
                VerificationSummarySha256 = hdl.VerificationSummarySourceSha256,
                SynthesisMetricsSha256 = hdl.SynthesisMetricsSourceSha256,
                ToolchainBootstrapSha256 = hdl.ToolchainBootstrapSourceSha256,
            },
            Files = files,
            Notes = "manifest.json intentionally does not hash itself; paths are output-relative and slash-normalized.",
        };
        Build002Protocol.WriteJson(Path.Combine(outputDirectory, "manifest.json"), manifest);
    }

    private static string BuildGeneratorCommand(string outputArgument, bool includeHdl)
    {
        var command = $"dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build002 --output {outputArgument}";
        return includeHdl
            ? command + " --hdl-verification-summary .artifacts/build002-hdl-full-zero-repair/verification-summary.json" +
              " --hdl-synthesis-metrics .artifacts/build002-hdl-full-zero-repair/synthesis-metrics.csv" +
              " --hdl-toolchain .artifacts/build002-hdl-full-zero-repair/toolchain-bootstrap.json"
            : command;
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

    private static OperationMeasurement MeasureStatefulCircuit(
        NandNetlist netlist,
        IReadOnlyList<StatefulCircuitCase> cases,
        Action<long, NandEvaluation> validate)
    {
        NandEvaluation? previous = null;
        long nandEvaluations = 0;
        long nandTransitions = 0;
        long initialTransitions = 0;
        long inputTransitions = 0;
        long stateTransitions = 0;
        for (var index = 0; index < cases.Count; index++)
        {
            var item = cases[index];
            var evaluated = netlist.Evaluate(
                item.Inputs,
                item.State,
                previous,
                compareWithAllOff: previous is null);
            validate(index, evaluated);
            nandEvaluations += evaluated.NandEvaluations;
            inputTransitions += evaluated.InputTransitions;
            stateTransitions += evaluated.StateBitTransitions;
            if (previous is null)
            {
                initialTransitions = evaluated.NandOutputTransitions;
            }
            else
            {
                nandTransitions += evaluated.NandOutputTransitions;
            }

            previous = evaluated;
        }

        return new OperationMeasurement(
            cases.Count,
            cases.Count,
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

    private static IEnumerable<IReadOnlyDictionary<string, BitState>> MagnitudeEncoderInputs(
        DeclaredMagnitudeEncoderCircuit circuit)
    {
        var limit = 1 << circuit.Ports.Width;
        for (var magnitude = 0; magnitude < limit; magnitude++)
        {
            yield return circuit.Ports.MagnitudeInputs
                .Select((name, bit) => new KeyValuePair<string, BitState>(
                    name,
                    State((magnitude & (1 << bit)) != 0)))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }
    }

    private static IEnumerable<IReadOnlyDictionary<string, BitState>> DecoderInputs(
        DeclaredMagnitudeDecoderCircuit circuit,
        IEnumerable<ValuationHardwareState> states)
    {
        foreach (var state in states)
        {
            var inputs = new Dictionary<string, BitState>(StringComparer.Ordinal)
            {
                [circuit.Ports.ZeroInput] = State(state.IsZero),
            };
            foreach (var lane in circuit.Ports.Lanes)
            {
                for (var bit = 0; bit < lane.ExponentInputs.Count; bit++)
                {
                    inputs[lane.ExponentInputs[bit]] =
                        State((state.ExponentAt(lane.Lane) & (1 << bit)) != 0);
                }

                inputs[lane.SaturationInput] = State(state.IsLaneSaturated(lane.Lane));
            }

            yield return inputs;
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

    private static ValuationProjectionReceipt ValuationProjection(int width, int magnitude)
    {
        if (magnitude == 0)
        {
            return new ValuationProjectionReceipt(
                ValuationHardwareState.Zero(width),
                FullySupported: true);
        }

        var exponents = ValuationHardwareDomain.S4
            .Select(prime => Valuation(magnitude, prime))
            .ToArray();
        var state = ValuationHardwareState.Create(width, false, exponents);
        if (!state.Succeeded)
        {
            throw new InvalidOperationException(
                $"A W-bit magnitude produced an invalid catalog valuation projection: {magnitude}.");
        }

        return new ValuationProjectionReceipt(
            state.Value!,
            TryEncodeSmooth(width, magnitude, out _));
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

    private static int CountBitChanges(
        Dictionary<string, BitState> before,
        IReadOnlyDictionary<string, BitState> after) =>
        after.Count(pair => before.TryGetValue(pair.Key, out var prior) && prior != pair.Value);

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
            notes + " All -1 fields mean NOT_MEASURED.",
            OperationClassFor(implementation, operation, regime));

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
            notes + " All -1 cost fields mean NOT_MEASURED.",
            OperationClassFor(implementation, phase, regime),
            NotMeasured,
            NotMeasured);

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

    private static long OracleLcm(int left, int right) =>
        left == 0 || right == 0
            ? 0
            : ((long)left / OracleGcd(left, right)) * right;

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

    private static bool SameValuationState(
        ValuationHardwareState left,
        ValuationHardwareState right) =>
        left.Width == right.Width &&
        left.IsZero == right.IsZero &&
        left.IsExact == right.IsExact &&
        left.Exponents.SequenceEqual(right.Exponents) &&
        left.SaturatedLanes.SequenceEqual(right.SaturatedLanes);

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

    private static string OperationClassFor(
        string implementation,
        string operation,
        string regime)
    {
        if (operation.Contains("RECONSTRUCT", StringComparison.Ordinal) ||
            operation.Contains("CONVERT", StringComparison.Ordinal) ||
            operation.Contains("REFRESH", StringComparison.Ordinal) ||
            operation == "EGRESS" || operation == "ADDITION_RECOVERY")
        {
            return "CROSS_REPRESENTATION";
        }

        if (operation.Contains("ENCODE", StringComparison.Ordinal) ||
            operation == "INGRESS" ||
            regime == "COLD_MAG" && implementation.Contains("VFU", StringComparison.Ordinal))
        {
            return "REQUIRES_FACTOR_DISCOVERY";
        }

        if (implementation.StartsWith("VFU-", StringComparison.Ordinal) ||
            implementation.StartsWith("PRESENCE-", StringComparison.Ordinal))
        {
            return "REPRESENTATION_LOCAL";
        }

        if (implementation.StartsWith("BIN+VSC", StringComparison.Ordinal))
        {
            return "REPRESENTATION_LOCAL";
        }

        return "BINARY_MAGNITUDE_LOCAL";
    }

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

    private sealed record StatefulCircuitCase(
        Dictionary<string, BitState> Inputs,
        Dictionary<string, BitState> State);

    private sealed record ValuationProjectionReceipt(
        ValuationHardwareState State,
        bool FullySupported);

    private sealed record DecisionRuleValues(
        bool AlternativeArithmeticUnit,
        bool PrimeStructuralCoprocessor,
        bool WarmStateSpecialized);

    private sealed record InheritedEvidenceReceipt(
        bool Complete,
        string Build000ManifestSha256,
        string Build001ManifestSha256,
        string Build001ReportSha256);

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
                    SidecarDatapath = "every W-bit integrated LOAD and every supported S4 threshold query, plus every frozen E/F trace",
                    MatchedBinaryDatapath = "every frozen E/F trace on the matched integrated binary baseline",
                    Workloads = "all frozen A through F/R rows at their declared semantic or integrated hardware evidence layer",
                },
                "Bounded generated-run checks only. HDL simulation/formal/synthesis are separate imported receipts; repository tests independently cover malformed states and encoding adapters. NOT_MEASURED composite transition fields are excluded from claims.");
    }
}
