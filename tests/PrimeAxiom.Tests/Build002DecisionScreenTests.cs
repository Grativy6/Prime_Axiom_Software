using PrimeAxiom.Cli;
using PrimeAxiom.Core.Hardware;

namespace PrimeAxiom.Tests;

public sealed class Build002DecisionScreenTests
{
    private const string PartialClassification = "PARTIAL — FINAL DECISION NOT EARNED";
    private static readonly IReadOnlyList<Build002StaticCostRow> CurrentRows =
        Build002ExperimentRunner.BuildStaticCosts();

    [Fact]
    public void CurrentRegisteredInventoryEarnsOnlyTheFrozenNegative()
    {
        var screen = Build002DecisionScreen.Evaluate(CurrentRows);

        Assert.True(screen.CandidateInventory.Complete);
        Assert.True(screen.EarnsNoHardwareAdvantage);
        Assert.Equal(
            ["B_WARM_SCALE_CANCEL", "C_SIDECAR_QUERY"],
            screen.WarmStateSpecialized.Comparisons
                .Select(comparison => comparison.Context)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.All(
            screen.Rules,
            rule =>
            {
                Assert.Equal(Build002StaticScreenStatus.StaticallyDisqualified, rule.Status);
                Assert.Equal("STATIC_NECESSARY_CONDITION_FAILURE", rule.Basis);
                Assert.All(
                    rule.Comparisons,
                    comparison =>
                    {
                        Assert.Equal(Build002StaticScreenStatus.StaticallyDisqualified, comparison.Status);
                        Assert.NotEmpty(comparison.CandidateWorseDimensions);
                        Assert.Equal("ACYCLIC", comparison.CandidateVector!.CombinationalLoopStatus);
                        Assert.Equal("ACYCLIC", comparison.BaselineVector!.CombinationalLoopStatus);
                    });
            });
        Assert.Equal("NO_HARDWARE_ADVANTAGE", Build002ExperimentRunner.ClassifyStaticDecision(CurrentRows));
    }

    [Theory]
    [InlineData("BIN+VSC-S4", 6, "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD")]
    [InlineData("BIN-MIXED-WARM", 6, "INTEGRATED_LOAD_SCALE_CANCEL_ADD")]
    [InlineData("BIN-DIV", 6, "UNSIGNED_RESTORING_DIVIDE")]
    [InlineData("VFU-BINEXP-S4-WARM", 6, "INTEGRATED_SCALE_CANCEL")]
    [InlineData("BIN-SCALE-CANCEL-WARM", 6, "INTEGRATED_SCALE_CANCEL")]
    public void MissingRequiredComparisonRowCannotEarnTerminalNegative(
        string implementation,
        int width,
        string operation)
    {
        var rows = CurrentRows
            .Where(row => !Matches(row, implementation, width, operation))
            .ToArray();

        var screen = Build002DecisionScreen.Evaluate(rows);

        Assert.False(screen.EarnsNoHardwareAdvantage);
        Assert.Contains(
            screen.Rules.SelectMany(rule => rule.Comparisons),
            comparison => comparison.Status == Build002StaticScreenStatus.IncompleteEvidence);
        Assert.Equal(PartialClassification, Build002ExperimentRunner.ClassifyStaticDecision(rows));
    }

    [Fact]
    public void StaticTieRequiresFullDynamicEvaluation()
    {
        var baselines = CurrentRows
            .Where(row => row.Implementation == "BIN-MIXED-WARM" &&
                          row.Operation == "INTEGRATED_LOAD_SCALE_CANCEL_ADD")
            .ToDictionary(row => row.Width);
        var rows = CurrentRows.Select(row =>
            row.Implementation == "BIN+VSC-S4" &&
            row.Operation == "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD" &&
            baselines.TryGetValue(row.Width, out var baseline)
                ? row with { Metrics = baseline.Metrics }
                : row).ToArray();

        var screen = Build002DecisionScreen.Evaluate(rows);

        Assert.Equal(
            Build002StaticScreenStatus.DynamicEvaluationRequired,
            screen.AlternativeArithmeticUnit.Status);
        Assert.False(screen.EarnsNoHardwareAdvantage);
        Assert.Equal(PartialClassification, Build002ExperimentRunner.ClassifyStaticDecision(rows));
    }

    [Fact]
    public void StaticNoWorseVectorCannotEarnPositiveOrNegativeLabel()
    {
        var baselines = CurrentRows
            .Where(row => row.Implementation == "BIN-MIXED-WARM" &&
                          row.Operation == "INTEGRATED_LOAD_SCALE_CANCEL_ADD")
            .ToDictionary(row => row.Width);
        var rows = CurrentRows.Select(row =>
            row.Implementation == "BIN+VSC-S4" &&
            row.Operation == "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD" &&
            baselines.TryGetValue(row.Width, out var baseline)
                ? row with
                {
                    Metrics = baseline.Metrics with
                    {
                        Nand2Static = baseline.Metrics.Nand2Static - 1,
                    },
                }
                : row).ToArray();

        var comparison = Build002DecisionScreen.Evaluate(rows)
            .AlternativeArithmeticUnit.Comparisons.Single(value => value.Width == 6);

        Assert.Equal(Build002StaticScreenStatus.DynamicEvaluationRequired, comparison.Status);
        Assert.Empty(comparison.CandidateWorseDimensions);
        Assert.Equal(PartialClassification, Build002ExperimentRunner.ClassifyStaticDecision(rows));
    }

    [Fact]
    public void NegativeFrozenMetricIsIncompleteRatherThanAnImprovement()
    {
        var candidate = Find("BIN+VSC-S4", 6, "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD");
        var rows = Replace(
            candidate,
            row => row with { Metrics = row.Metrics with { Nand2Static = -1 } });

        var comparison = Build002DecisionScreen.Evaluate(rows)
            .AlternativeArithmeticUnit.Comparisons.Single(value => value.Width == 6);

        Assert.Equal(Build002StaticScreenStatus.IncompleteEvidence, comparison.Status);
        Assert.Contains("candidate.nand2_static=-1;expected=NONNEGATIVE", comparison.EvidenceFailures);
        Assert.Equal(PartialClassification, Build002ExperimentRunner.ClassifyStaticDecision(rows));
    }

    [Theory]
    [InlineData("architecture")]
    [InlineData("evidence_class")]
    [InlineData("support_status")]
    public void WrongCandidateContractIsIncompleteEvidence(string field)
    {
        var candidate = Find("BIN+VSC-S4", 6, "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD");
        var rows = Replace(
            candidate,
            row => field switch
            {
                "architecture" => row with { Architecture = "UNREGISTERED_ARCHITECTURE" },
                "evidence_class" => row with { EvidenceClass = "STRUCTURAL_DECLARED" },
                "support_status" => row with { SupportStatus = "RESTRICTED" },
                _ => throw new InvalidOperationException(field),
            });

        var comparison = Build002DecisionScreen.Evaluate(rows)
            .AlternativeArithmeticUnit.Comparisons.Single(value => value.Width == 6);

        Assert.Equal(Build002StaticScreenStatus.IncompleteEvidence, comparison.Status);
        Assert.Contains(comparison.EvidenceFailures, failure => failure.Contains(field, StringComparison.Ordinal));
        Assert.Equal(PartialClassification, Build002ExperimentRunner.ClassifyStaticDecision(rows));
    }

    [Fact]
    public void UnregisteredIntegratedCandidateRequiresNewRule()
    {
        var template = Find("BIN+VSC-S4", 6, "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD");
        var rows = CurrentRows.Append(template with
        {
            Implementation = "UNEXPECTED-INTEGRATED-CANDIDATE",
            Module = "UNEXPECTED.W6",
            Operation = "INTEGRATED_UNEXPECTED",
            Architecture = "UNEXPECTED_ARCHITECTURE",
        }).ToArray();

        var screen = Build002DecisionScreen.Evaluate(rows);

        Assert.False(screen.CandidateInventory.Complete);
        Assert.Contains(
            screen.CandidateInventory.UnexpectedRowIds,
            rowId => rowId.StartsWith("UNEXPECTED-INTEGRATED-CANDIDATE|W6|", StringComparison.Ordinal));
        Assert.Equal(PartialClassification, Build002ExperimentRunner.ClassifyStaticDecision(rows));
    }

    [Fact]
    public void BinaryPositionalLabelCannotHideUnregisteredIntegratedCandidate()
    {
        var template = Find("BIN+VSC-S4", 6, "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD");
        var rows = CurrentRows.Append(template with
        {
            Implementation = "UNEXPECTED-INTEGRATED-CANDIDATE",
            Module = "UNEXPECTED.BINARY.W6",
            Operation = "INTEGRATED_UNEXPECTED",
            Architecture = "BINARY_POSITIONAL",
        }).ToArray();

        var screen = Build002DecisionScreen.Evaluate(rows);

        Assert.False(screen.CandidateInventory.Complete);
        Assert.Contains(
            screen.CandidateInventory.UnexpectedRowIds,
            rowId => rowId.StartsWith("UNEXPECTED-INTEGRATED-CANDIDATE|W6|", StringComparison.Ordinal));
        Assert.Equal(PartialClassification, Build002ExperimentRunner.ClassifyStaticDecision(rows));
    }

    [Theory]
    [InlineData("input_bits")]
    [InlineData("output_bits")]
    public void FrozenBoundaryBitDimensionsParticipateInStaticDisqualification(string dimension)
    {
        var baseline = Find("BIN-MIXED-WARM", 6, "INTEGRATED_LOAD_SCALE_CANCEL_ADD");
        var candidateMetrics = dimension switch
        {
            "input_bits" => baseline.Metrics with
            {
                InputBits = baseline.Metrics.InputBits + 1,
                PortBits = baseline.Metrics.PortBits + 1,
            },
            "output_bits" => baseline.Metrics with
            {
                OutputBits = baseline.Metrics.OutputBits + 1,
                PortBits = baseline.Metrics.PortBits + 1,
            },
            _ => throw new InvalidOperationException(dimension),
        };
        var rows = Replace(
            Find("BIN+VSC-S4", 6, "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD"),
            row => row with { Metrics = candidateMetrics });

        var comparison = Build002DecisionScreen.Evaluate(rows)
            .AlternativeArithmeticUnit.Comparisons.Single(value => value.Width == 6);

        Assert.Equal(Build002StaticScreenStatus.StaticallyDisqualified, comparison.Status);
        Assert.Contains(dimension, comparison.CandidateWorseDimensions);
    }

    [Fact]
    public void NonAcyclicLoopStatusInvalidatesStaticDecisionEvidence()
    {
        var candidate = Find("BIN+VSC-S4", 6, "INTEGRATED_LOAD_REFRESH_QUERY_SCALE_CANCEL_ADD");
        var rows = Replace(
            candidate,
            row => row with
            {
                Metrics = row.Metrics with
                {
                    CombinationalLoopStatus = (CombinationalLoopStatus)int.MaxValue,
                },
            });

        var comparison = Build002DecisionScreen.Evaluate(rows)
            .AlternativeArithmeticUnit.Comparisons.Single(value => value.Width == 6);

        Assert.Equal(Build002StaticScreenStatus.IncompleteEvidence, comparison.Status);
        Assert.Contains(
            comparison.EvidenceFailures,
            failure => failure.StartsWith("candidate.combinational_loop_status=", StringComparison.Ordinal));
        Assert.Equal(PartialClassification, Build002ExperimentRunner.ClassifyStaticDecision(rows));
    }

    [Fact]
    public void DuplicateRequiredCandidateRowIsIncompleteEvidence()
    {
        var candidate = Find("VFU-BINEXP-S4-WARM", 8, "INTEGRATED_SCALE_CANCEL");
        var rows = CurrentRows.Append(candidate with { Module = candidate.Module + ".DUPLICATE" }).ToArray();

        var screen = Build002DecisionScreen.Evaluate(rows);

        Assert.False(screen.CandidateInventory.Complete);
        Assert.Contains(
            screen.WarmStateSpecialized.Comparisons,
            comparison => comparison.Width == 8 &&
                          comparison.Status == Build002StaticScreenStatus.IncompleteEvidence);
        Assert.Equal(PartialClassification, Build002ExperimentRunner.ClassifyStaticDecision(rows));
    }

    [Theory]
    [InlineData(true, "linux-x64", 260, 15, 150, 150, 0, true)]
    [InlineData(true, "windows-x64", 260, 15, 150, 150, 0, false)]
    [InlineData(true, "linux-x64", 259, 15, 150, 150, 0, false)]
    [InlineData(true, "linux-x64", 260, 14, 150, 150, 0, false)]
    [InlineData(true, "linux-x64", 260, 15, 149, 149, 0, false)]
    [InlineData(true, "linux-x64", 260, 15, 150, 0, 150, false)]
    [InlineData(true, "linux-x64", 260, 15, 150, 149, 1, false)]
    [InlineData(false, "linux-x64", 260, 15, 150, 150, 0, false)]
    public void HdlDecisionEvidenceRequiresCanonicalCompleteRegisteredMatrix(
        bool complete,
        string platform,
        int verificationCases,
        int formalCases,
        int synthesisRows,
        int warningCountsMeasured,
        int warningCountsNotMeasured,
        bool expected)
    {
        var hdl = new Build002HdlImportReceipt(
            complete,
            complete ? "COMPLETE_VERIFIED" : "INCOMPLETE",
            platform,
            verificationCases,
            formalCases,
            synthesisRows,
            warningCountsMeasured,
            warningCountsNotMeasured,
            "VERIFICATION",
            "SYNTHESIS",
            "TOOLCHAIN",
            "SYNTHESIS_OUTPUT",
            "FORMAL_OUTPUT",
            "TOOLCHAIN_OUTPUT");

        Assert.Equal(expected, Build002ExperimentRunner.HdlDecisionEvidenceComplete(hdl));
    }

    private static Build002StaticCostRow Find(string implementation, int width, string operation) =>
        CurrentRows.Single(row => Matches(row, implementation, width, operation));

    private static Build002StaticCostRow[] Replace(
        Build002StaticCostRow target,
        Func<Build002StaticCostRow, Build002StaticCostRow> replacement) =>
        CurrentRows.Select(row => ReferenceEquals(row, target) ? replacement(row) : row).ToArray();

    private static bool Matches(
        Build002StaticCostRow row,
        string implementation,
        int width,
        string operation) =>
        row.Implementation == implementation && row.Width == width && row.Operation == operation;
}
