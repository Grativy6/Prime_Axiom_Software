using PrimeAxiom.Core.Hardware;

namespace PrimeAxiom.Cli;

internal enum Build002StaticScreenStatus
{
    StaticallyDisqualified,
    DynamicEvaluationRequired,
    IncompleteEvidence,
}

internal sealed record Build002StaticDecisionVector(
    int Nand2Static,
    int DffStatic,
    int StateBits,
    int InputBits,
    int OutputBits,
    int PortBits,
    int WireBits,
    int ConnectionsStatic,
    int MaximumFanout,
    int CrossLaneConnections,
    int UnitNandCriticalDepth,
    string CombinationalLoopStatus);

internal sealed record Build002StaticComparisonScreen(
    string ComparisonId,
    string Context,
    int Width,
    Build002StaticScreenStatus Status,
    string Basis,
    string CandidateRowId,
    string BaselineRowId,
    Build002StaticDecisionVector? CandidateVector,
    Build002StaticDecisionVector? BaselineVector,
    IReadOnlyList<string> CandidateWorseDimensions,
    IReadOnlyList<string> EvidenceFailures);

internal sealed record Build002DecisionRuleScreen(
    string RuleId,
    Build002StaticScreenStatus Status,
    string Basis,
    string Reason,
    IReadOnlyList<Build002StaticComparisonScreen> Comparisons);

internal sealed record Build002CandidateInventoryScreen(
    bool Complete,
    IReadOnlyList<string> ExpectedRowIds,
    IReadOnlyList<string> MissingRowIds,
    IReadOnlyList<string> UnexpectedRowIds,
    IReadOnlyList<string> DuplicateRowIds);

internal sealed record Build002DecisionScreenResult(
    Build002DecisionRuleScreen AlternativeArithmeticUnit,
    Build002DecisionRuleScreen PrimeStructuralCoprocessor,
    Build002DecisionRuleScreen WarmStateSpecialized,
    Build002CandidateInventoryScreen CandidateInventory)
{
    public IReadOnlyList<Build002DecisionRuleScreen> Rules =>
    [
        AlternativeArithmeticUnit,
        PrimeStructuralCoprocessor,
        WarmStateSpecialized,
    ];

    public bool EarnsNoHardwareAdvantage =>
        CandidateInventory.Complete &&
        Rules.All(rule => rule.Status == Build002StaticScreenStatus.StaticallyDisqualified);
}

internal static class Build002DecisionScreen
{
    private const string StaticFailureBasis = "STATIC_NECESSARY_CONDITION_FAILURE";
    private const string DynamicRequiredBasis = "STATIC_SCREEN_PASSED_DYNAMIC_EVALUATION_REQUIRED";
    private const string IncompleteBasis = "INCOMPLETE_STATIC_DECISION_EVIDENCE";
    private static readonly int[] DecisionWidths = [6, 8];

    private static readonly RowContract Sidecar = new(
        "BIN+VSC-S4",
        "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD",
        "BINARY_MAGNITUDE_PLUS_EXACT_THRESHOLD_SIDECAR",
        "STRUCTURAL_DECLARED_INTEGRATED",
        "FULL_BINARY_MAGNITUDE_WITH_SOUND_S4_METADATA");

    private static readonly RowContract MixedBinary = new(
        "BIN-MIXED-WARM",
        "INTEGRATED_LOAD_SCALE_CANCEL_ADD",
        "BINARY_POSITIONAL",
        "STRUCTURAL_DECLARED",
        "IMPLEMENTED");

    private static readonly RowContract BinaryDivide = new(
        "BIN-DIV",
        "UNSIGNED_RESTORING_DIVIDE",
        "BINARY_POSITIONAL",
        "STRUCTURAL_DECLARED",
        "IMPLEMENTED");

    private static readonly RowContract WarmStructural = new(
        "VFU-BINEXP-S4-WARM",
        "INTEGRATED_SCALE_CANCEL",
        "BINARY_EXPONENT",
        "STRUCTURAL_DECLARED_INTEGRATED",
        "FULLY_INTEGRATED_EXACT_S4_STATE");

    private static readonly RowContract WarmBinary = new(
        "BIN-SCALE-CANCEL-WARM",
        "INTEGRATED_SCALE_CANCEL",
        "BINARY_POSITIONAL",
        "STRUCTURAL_DECLARED",
        "IMPLEMENTED");

    private static readonly RuleDefinition[] Rules =
    [
        new(
            "ALTERNATIVE_ARITHMETIC_UNIT_CANDIDATE",
            [new("E_MIXED_MAGNITUDE", Sidecar, MixedBinary)],
            "The registered integrated E-capable sidecar has a mandatory static-vector loss at both decision widths, so it cannot satisfy the frozen whole-vector Pareto rule."),
        new(
            "PRIME_STRUCTURAL_COPROCESSOR_CANDIDATE",
            [new("C_SIDECAR_QUERY", Sidecar, BinaryDivide)],
            "The registered integrated sidecar has a mandatory static-vector loss against the binary query context at both decision widths; cumulative dynamic savings cannot remove a separately charged static loss."),
        new(
            "WARM_STATE_SPECIALIZED_ADVANTAGE",
            [
                new("B_WARM_SCALE_CANCEL", WarmStructural, WarmBinary),
                new("C_SIDECAR_QUERY", Sidecar, BinaryDivide),
            ],
            "Every registered integrated B/C candidate has a mandatory static-vector loss against its matched binary context at both decision widths; D has no integrated candidate."),
    ];

    internal static Build002DecisionScreenResult Evaluate(IReadOnlyList<Build002StaticCostRow> rows)
    {
        var evaluatedRules = Rules.Select(rule => EvaluateRule(rows, rule)).ToArray();
        return new Build002DecisionScreenResult(
            evaluatedRules[0],
            evaluatedRules[1],
            evaluatedRules[2],
            EvaluateCandidateInventory(rows));
    }

    private static Build002DecisionRuleScreen EvaluateRule(
        IReadOnlyList<Build002StaticCostRow> rows,
        RuleDefinition definition)
    {
        var comparisons = definition.Comparisons
            .SelectMany(comparison => DecisionWidths.Select(width =>
                EvaluateComparison(rows, comparison, width)))
            .ToArray();
        var status = comparisons.Any(comparison => comparison.Status == Build002StaticScreenStatus.IncompleteEvidence)
            ? Build002StaticScreenStatus.IncompleteEvidence
            : comparisons
                .GroupBy(comparison => comparison.Context, StringComparer.Ordinal)
                .All(context => context.Any(comparison =>
                    comparison.Status == Build002StaticScreenStatus.StaticallyDisqualified))
                ? Build002StaticScreenStatus.StaticallyDisqualified
                : Build002StaticScreenStatus.DynamicEvaluationRequired;
        var basis = status switch
        {
            Build002StaticScreenStatus.StaticallyDisqualified => StaticFailureBasis,
            Build002StaticScreenStatus.DynamicEvaluationRequired => DynamicRequiredBasis,
            _ => IncompleteBasis,
        };
        var reason = status switch
        {
            Build002StaticScreenStatus.StaticallyDisqualified => definition.StaticFailureReason,
            Build002StaticScreenStatus.DynamicEvaluationRequired =>
                "The candidate is not statically disqualified at every required width. Static evidence alone cannot earn either a positive label or the terminal negative; the full frozen dynamic predicate remains required.",
            _ =>
                "One or more required static comparison rows are missing, ambiguous, invalid, or outside the frozen contract. The terminal decision is not earned.",
        };

        return new Build002DecisionRuleScreen(definition.RuleId, status, basis, reason, comparisons);
    }

    private static Build002StaticComparisonScreen EvaluateComparison(
        IReadOnlyList<Build002StaticCostRow> rows,
        ComparisonDefinition definition,
        int width)
    {
        var candidateRows = FindRows(rows, definition.Candidate, width);
        var baselineRows = FindRows(rows, definition.Baseline, width);
        var candidateExpectedId = ExpectedRowId(definition.Candidate, width);
        var baselineExpectedId = ExpectedRowId(definition.Baseline, width);
        var evidenceFailures = new List<string>();

        if (candidateRows.Length != 1)
        {
            evidenceFailures.Add($"candidate_row_count={candidateRows.Length};expected=1");
        }

        if (baselineRows.Length != 1)
        {
            evidenceFailures.Add($"baseline_row_count={baselineRows.Length};expected=1");
        }

        var candidate = candidateRows.Length == 1 ? candidateRows[0] : null;
        var baseline = baselineRows.Length == 1 ? baselineRows[0] : null;
        if (candidate is not null)
        {
            AddContractFailures(evidenceFailures, "candidate", candidate, definition.Candidate);
        }

        if (baseline is not null)
        {
            AddContractFailures(evidenceFailures, "baseline", baseline, definition.Baseline);
        }

        if (evidenceFailures.Count != 0 || candidate is null || baseline is null)
        {
            return new Build002StaticComparisonScreen(
                $"{definition.Context}|{candidateExpectedId}__vs__{baselineExpectedId}",
                definition.Context,
                width,
                Build002StaticScreenStatus.IncompleteEvidence,
                IncompleteBasis,
                candidate is null ? candidateExpectedId : RowId(candidate),
                baseline is null ? baselineExpectedId : RowId(baseline),
                candidate is null ? null : StaticVector(candidate.Metrics),
                baseline is null ? null : StaticVector(baseline.Metrics),
                [],
                evidenceFailures);
        }

        var worseDimensions = FrozenNumericFields(candidate.Metrics, baseline.Metrics)
            .Where(field => field.Candidate > field.Baseline)
            .Select(field => field.Name)
            .ToArray();
        var status = worseDimensions.Length == 0
            ? Build002StaticScreenStatus.DynamicEvaluationRequired
            : Build002StaticScreenStatus.StaticallyDisqualified;

        return new Build002StaticComparisonScreen(
            $"{definition.Context}|{RowId(candidate)}__vs__{RowId(baseline)}",
            definition.Context,
            width,
            status,
            status == Build002StaticScreenStatus.StaticallyDisqualified ? StaticFailureBasis : DynamicRequiredBasis,
            RowId(candidate),
            RowId(baseline),
            StaticVector(candidate.Metrics),
            StaticVector(baseline.Metrics),
            worseDimensions,
            []);
    }

    private static Build002CandidateInventoryScreen EvaluateCandidateInventory(
        IReadOnlyList<Build002StaticCostRow> rows)
    {
        var expected = DecisionWidths
            .SelectMany(width => new[] { ExpectedRowId(Sidecar, width), ExpectedRowId(WarmStructural, width) })
            .Order(StringComparer.Ordinal)
            .ToArray();
        var registeredBaselines = DecisionWidths
            .SelectMany(width => new[] { ExpectedRowId(MixedBinary, width), ExpectedRowId(WarmBinary, width) })
            .ToHashSet(StringComparer.Ordinal);
        var actualRows = rows
            .Where(row => DecisionWidths.Contains(row.Width) &&
                          (row.EvidenceClass.Contains("INTEGRATED", StringComparison.Ordinal) ||
                           row.Operation.StartsWith("INTEGRATED_", StringComparison.Ordinal)) &&
                          !registeredBaselines.Contains(RowIdWithoutModule(row)))
            .ToArray();
        var actual = actualRows
            .Select(RowIdWithoutModule)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var duplicates = actualRows
            .GroupBy(RowIdWithoutModule, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var missing = expected.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var unexpected = actual.Except(expected, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        return new Build002CandidateInventoryScreen(
            missing.Length == 0 && unexpected.Length == 0 && duplicates.Length == 0,
            expected,
            missing,
            unexpected,
            duplicates);
    }

    private static Build002StaticCostRow[] FindRows(
        IReadOnlyList<Build002StaticCostRow> rows,
        RowContract contract,
        int width) =>
        rows.Where(row => row.Implementation == contract.Implementation &&
                          row.Width == width &&
                          row.Operation == contract.Operation)
            .ToArray();

    private static void AddContractFailures(
        List<string> failures,
        string role,
        Build002StaticCostRow row,
        RowContract contract)
    {
        if (row.Architecture != contract.Architecture)
        {
            failures.Add($"{role}.architecture={row.Architecture};expected={contract.Architecture}");
        }

        if (row.EvidenceClass != contract.EvidenceClass)
        {
            failures.Add($"{role}.evidence_class={row.EvidenceClass};expected={contract.EvidenceClass}");
        }

        if (row.SupportStatus != contract.SupportStatus)
        {
            failures.Add($"{role}.support_status={row.SupportStatus};expected={contract.SupportStatus}");
        }

        if (row.Metrics.CombinationalLoopStatus != CombinationalLoopStatus.Acyclic)
        {
            failures.Add($"{role}.combinational_loop_status={row.Metrics.CombinationalLoopStatus};expected=ACYCLIC");
        }

        foreach (var field in FrozenNumericFields(row.Metrics, row.Metrics).Where(field => field.Candidate < 0))
        {
            failures.Add($"{role}.{field.Name}={field.Candidate};expected=NONNEGATIVE");
        }
    }

    private static IReadOnlyList<(string Name, int Candidate, int Baseline)> FrozenNumericFields(
        NandStaticMetrics candidate,
        NandStaticMetrics baseline) =>
    [
        ("nand2_static", candidate.Nand2Static, baseline.Nand2Static),
        ("dff_static", candidate.DffStatic, baseline.DffStatic),
        ("state_bits", candidate.StateBits, baseline.StateBits),
        ("input_bits", candidate.InputBits, baseline.InputBits),
        ("output_bits", candidate.OutputBits, baseline.OutputBits),
        ("port_bits", candidate.PortBits, baseline.PortBits),
        ("wire_bits", candidate.WireBits, baseline.WireBits),
        ("connections_static", candidate.ConnectionsStatic, baseline.ConnectionsStatic),
        ("max_fanout", candidate.MaximumFanout, baseline.MaximumFanout),
        ("cross_lane_connections", candidate.CrossLaneConnections, baseline.CrossLaneConnections),
        ("unit_nand_critical_depth", candidate.UnitNandCriticalDepth, baseline.UnitNandCriticalDepth),
    ];

    private static Build002StaticDecisionVector StaticVector(NandStaticMetrics metrics) => new(
        metrics.Nand2Static,
        metrics.DffStatic,
        metrics.StateBits,
        metrics.InputBits,
        metrics.OutputBits,
        metrics.PortBits,
        metrics.WireBits,
        metrics.ConnectionsStatic,
        metrics.MaximumFanout,
        metrics.CrossLaneConnections,
        metrics.UnitNandCriticalDepth,
        metrics.CombinationalLoopStatus.ToString().ToUpperInvariant());

    private static string RowId(Build002StaticCostRow row) =>
        $"{RowIdWithoutModule(row)}|{row.Module}";

    private static string RowIdWithoutModule(Build002StaticCostRow row) =>
        $"{row.Implementation}|W{row.Width}|{row.Operation}";

    private static string ExpectedRowId(RowContract contract, int width) =>
        $"{contract.Implementation}|W{width}|{contract.Operation}";

    private sealed record RowContract(
        string Implementation,
        string Operation,
        string Architecture,
        string EvidenceClass,
        string SupportStatus);

    private sealed record RuleDefinition(
        string RuleId,
        IReadOnlyList<ComparisonDefinition> Comparisons,
        string StaticFailureReason);

    private sealed record ComparisonDefinition(
        string Context,
        RowContract Candidate,
        RowContract Baseline);
}
