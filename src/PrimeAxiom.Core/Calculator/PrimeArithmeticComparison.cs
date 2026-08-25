using System.Globalization;
using System.Numerics;

namespace PrimeAxiom.Core.Calculator;

public sealed record DecimalColumnStep(
    int ColumnFromRight,
    int LeftDigit,
    int RightDigit,
    int CarryIn,
    int RawTotal,
    int ResultDigit,
    int CarryOut);

public sealed record MagnitudeMultiplicationStep(
    int Step,
    string AccumulatorBeforeDecimal,
    string OperandDecimal,
    string AccumulatorAfterDecimal);

public sealed record OrdinaryArithmeticWork(
    long DecimalColumns,
    long DigitAdditions,
    long CarryEvents,
    long SequentialMagnitudeMultiplications);

public sealed record OrdinaryArithmeticTrace(
    string Strategy,
    string Algorithm,
    string ResultDecimal,
    IReadOnlyList<DecimalColumnStep> ColumnSteps,
    IReadOnlyList<MagnitudeMultiplicationStep> MultiplicationSteps,
    OrdinaryArithmeticWork Work,
    bool OracleVerified,
    string ClaimCeiling);

public sealed record PrimePathEvent(
    int Sequence,
    string Phase,
    string OperationClass,
    string Operation,
    string Detail,
    string? ReceiptId);

public sealed record PrimePathWork(
    long FactorizationCalls,
    long RadixExtractions,
    long OddCandidatesExamined,
    long RemainderChecks,
    long ExactFactorDivisions,
    long ExponentReads,
    long ExponentWrites,
    long ExponentMerges,
    long MagnitudeAdditions,
    long ReceiptConstructionReconstructions,
    long IntegrityReplayReconstructions,
    long EgressReconstructions,
    long Reconstructions);

public sealed record PrimeArithmeticPath(
    string Strategy,
    string ResultDecimal,
    IReadOnlyList<PrimeReceipt> InputReceipts,
    PrimeReceipt OutputReceipt,
    IReadOnlyList<PrimePowerReceipt> CommonCertifiedPrimePowers,
    IReadOnlyList<PrimePathEvent> Events,
    PrimePathWork Work,
    bool ExactStructureCompleted,
    bool OracleVerified,
    string LocalityConclusion,
    string ClaimCeiling);

public sealed record ArithmeticComparisonReceipt(
    string Schema,
    string ProtocolId,
    string Id,
    string Operation,
    string Expression,
    IReadOnlyList<string> OperandsDecimal,
    string OutputObligation,
    OrdinaryArithmeticTrace OrdinaryPath,
    PrimeArithmeticPath PrimePath,
    bool PathsAgree,
    string AiComparisonBoundary);

/// <summary>
/// Produces public, replayable arithmetic-path traces. These records compare
/// algorithms and exposed receipts; they are not access to a model's private reasoning.
/// </summary>
public static class PrimeArithmeticComparison
{
    public const string Schema = "prime-axiom-arithmetic-comparison-v1";
    private const string AiBoundary =
        "This compares public deterministic arithmetic strategies, not hidden chain of thought, model understanding, or general LLM accuracy.";

    public static ArithmeticComparisonReceipt CompareAddition(
        string id,
        BigInteger left,
        BigInteger right,
        PrimeReceiptPolicy? policy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (left.Sign != 0 && right.Sign != 0 && left.Sign != right.Sign)
        {
            throw new ArgumentException(
                "The public decimal comparison trace currently supports same-sign addition only; mixed-sign subtraction is not yet implemented.");
        }

        policy ??= new PrimeReceiptPolicy();
        var ordinary = BuildAdditionTrace(left, right);
        var inputReceipts = new[]
        {
            PrimeReceiptCalculator.Analyze(left, policy),
            PrimeReceiptCalculator.Analyze(right, policy),
        };
        var sum = left + right;
        var outputReceipt = PrimeReceiptCalculator.AnalyzeAfterMagnitudeAddition(
            sum,
            policy,
            inputReceipts);
        var aggregateWork = inputReceipts
            .Append(outputReceipt)
            .Aggregate(PrimeReceiptWork.Zero, (current, receipt) => current + receipt.Work);
        var events = new List<PrimePathEvent>
        {
            new(1, "INGRESS", "REQUIRES_FACTOR_DISCOVERY", "FACTOR_LEFT", "Acquire an auditable receipt from the first ordinary magnitude.", inputReceipts[0].ReceiptId),
            new(2, "INGRESS", "REQUIRES_FACTOR_DISCOVERY", "FACTOR_RIGHT", "Acquire an auditable receipt from the second ordinary magnitude.", inputReceipts[1].ReceiptId),
            new(3, "EXECUTE", "BINARY_MAGNITUDE_LOCAL", "ADD_MAGNITUDES", "General exact addition remains an ordinary magnitude operation.", null),
            new(4, "ADDITION_RECOVERY", "REQUIRES_FACTOR_DISCOVERY", "FACTOR_SUM", "The output prime structure is freshly discovered; it is not obtained by exponent merge.", outputReceipt.ReceiptId),
            new(5, "VERIFY", "CROSS_REPRESENTATION", "VERIFY_RECEIPTS", "Reconstruct every receipt and compare the final exact magnitude.", outputReceipt.ReceiptId),
        };
        var allReceipts = inputReceipts.Append(outputReceipt).ToArray();
        var commonCertifiedPrimePowers = CommonCertifiedFactors(inputReceipts[0], inputReceipts[1]);
        var exactStructureCompleted = allReceipts.All(IsExact);
        var reconstructed = PrimeReceiptCalculator.Reconstruct(outputReceipt);
        var receiptConstructionReconstructions = allReceipts.Sum(ConstructionReconstructionCount);
        const long integrityReplayReconstructions = 2;
        const long egressReconstructions = 1;
        var primePath = new PrimeArithmeticPath(
            "PRIME_RECEIPT_TOOL_PATH",
            sum.ToString(CultureInfo.InvariantCulture),
            inputReceipts,
            outputReceipt,
            commonCertifiedPrimePowers,
            events,
            new PrimePathWork(
                FactorizationCalls: 3,
                aggregateWork.RadixExtractions,
                aggregateWork.OddCandidatesExamined,
                aggregateWork.RemainderChecks,
                aggregateWork.ExactFactorDivisions,
                ExponentReads: inputReceipts.Sum(receipt => (long)receipt.PrimePowers.Count),
                ExponentWrites: commonCertifiedPrimePowers.Length,
                ExponentMerges: 0,
                MagnitudeAdditions: 1,
                receiptConstructionReconstructions,
                integrityReplayReconstructions,
                egressReconstructions,
                Reconstructions: checked(
                    receiptConstructionReconstructions +
                    integrityReplayReconstructions +
                    egressReconstructions)),
            exactStructureCompleted,
            reconstructed == sum,
            "MAGNITUDE_ADD_THEN_FRESH_FACTOR_DISCOVERY",
            "The receipts expose exact structure when complete, but they do not make general addition representation-local or remove acquisition work.");

        return new ArithmeticComparisonReceipt(
            Schema,
            PrimeReceiptCalculator.ProtocolId,
            id,
            "ADD",
            $"{left.ToString(CultureInfo.InvariantCulture)} + {right.ToString(CultureInfo.InvariantCulture)}",
            new[] { left.ToString(CultureInfo.InvariantCulture), right.ToString(CultureInfo.InvariantCulture) },
            "MAGNITUDE_AND_RECEIPT",
            ordinary,
            primePath,
            ordinary.ResultDecimal == primePath.ResultDecimal && ordinary.OracleVerified && primePath.OracleVerified,
            AiBoundary);
    }

    public static ArithmeticComparisonReceipt CompareMultiplication(
        string id,
        IReadOnlyList<BigInteger> operands,
        PrimeReceiptPolicy? policy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(operands);
        var frozenOperands = operands.ToArray();
        if (frozenOperands.Length < 2)
        {
            throw new ArgumentException("At least two operands are required.", nameof(operands));
        }

        policy ??= new PrimeReceiptPolicy();
        var ordinary = BuildMultiplicationTrace(frozenOperands);
        var inputReceipts = frozenOperands
            .Select(operand => PrimeReceiptCalculator.Analyze(operand, policy))
            .ToArray();
        if (inputReceipts.Any(receipt => !IsExact(receipt)))
        {
            throw new InvalidOperationException(
                "The exact multiplication comparison requires complete input receipts; increase the odd-candidate budget.");
        }

        var outputReceipt = PrimeReceiptCalculator.ComposeExact(inputReceipts);
        var aggregateWork = inputReceipts
            .Aggregate(PrimeReceiptWork.Zero, (current, receipt) => current + receipt.Work);
        var zeroShortCircuit = inputReceipts.Any(receipt => receipt.Status == PrimeReceiptStatus.ExactZero);
        var totalEntries = inputReceipts.Sum(receipt => (long)receipt.PrimePowers.Count);
        var outputEntries = outputReceipt.PrimePowers.Count;
        var events = new List<PrimePathEvent>();
        for (var index = 0; index < inputReceipts.Length; index++)
        {
            events.Add(new PrimePathEvent(
                index + 1,
                "INGRESS",
                "REQUIRES_FACTOR_DISCOVERY",
                "FACTOR_OPERAND",
                $"Acquire operand receipt {index.ToString(CultureInfo.InvariantCulture)} from ordinary magnitude.",
                inputReceipts[index].ReceiptId));
        }

        events.Add(zeroShortCircuit
            ? new PrimePathEvent(
                events.Count + 1,
                "EXECUTE",
                "REPRESENTATION_LOCAL",
                "ZERO_TAG_SHORT_CIRCUIT",
                "An exact zero tag determines the product without reading or merging exponent entries.",
                outputReceipt.ReceiptId)
            : new PrimePathEvent(
                events.Count + 1,
                "EXECUTE",
                "REPRESENTATION_LOCAL",
                "MERGE_EXPONENTS",
                "Merge identical prime identities and add their exponents; the product is not refactored.",
                outputReceipt.ReceiptId));
        events.Add(new PrimePathEvent(
            events.Count + 1,
            "EGRESS",
            "CROSS_REPRESENTATION",
            "RECONSTRUCT_MAGNITUDE",
            "One exact ordinary magnitude is required by the output contract.",
            outputReceipt.ReceiptId));
        events.Add(new PrimePathEvent(
            events.Count + 1,
            "VERIFY",
            "BINARY_MAGNITUDE_LOCAL",
            "DIFFERENTIAL_CHECK",
            "Compare the reconstructed structural product with the independent ordinary product.",
            outputReceipt.ReceiptId));

        var reconstructed = PrimeReceiptCalculator.Reconstruct(outputReceipt);
        var receiptConstructionReconstructions = inputReceipts.Sum(ConstructionReconstructionCount) +
            ConstructionReconstructionCount(outputReceipt);
        var integrityReplayReconstructions = (long)inputReceipts.Length;
        const long egressReconstructions = 1;
        var primePath = new PrimeArithmeticPath(
            "PRIME_RECEIPT_TOOL_PATH",
            reconstructed.ToString(CultureInfo.InvariantCulture),
            inputReceipts,
            outputReceipt,
            Array.Empty<PrimePowerReceipt>(),
            events,
            new PrimePathWork(
                FactorizationCalls: inputReceipts.Length,
                aggregateWork.RadixExtractions,
                aggregateWork.OddCandidatesExamined,
                aggregateWork.RemainderChecks,
                aggregateWork.ExactFactorDivisions,
                ExponentReads: zeroShortCircuit ? 0 : totalEntries,
                ExponentWrites: zeroShortCircuit ? 0 : outputEntries,
                ExponentMerges: zeroShortCircuit ? 0 : checked(totalEntries - outputEntries),
                MagnitudeAdditions: 0,
                receiptConstructionReconstructions,
                integrityReplayReconstructions,
                egressReconstructions,
                Reconstructions: checked(
                    receiptConstructionReconstructions +
                    integrityReplayReconstructions +
                    egressReconstructions)),
            ExactStructureCompleted: true,
            OracleVerified: reconstructed.ToString(CultureInfo.InvariantCulture) == ordinary.ResultDecimal,
            zeroShortCircuit
                ? "ZERO_TAG_SHORT_CIRCUIT_AFTER_EXPLICIT_ACQUISITION"
                : "LOCAL_EXPONENT_MERGE_AFTER_EXPLICIT_ACQUISITION",
            zeroShortCircuit
                ? "The zero tag short-circuits structural multiplication only after input receipt acquisition; the output magnitude contract and verification remain explicit."
                : "Exponent merge is local only after exact receipts exist; cold factor discovery and the required final magnitude reconstruction remain charged.");

        return new ArithmeticComparisonReceipt(
            Schema,
            PrimeReceiptCalculator.ProtocolId,
            id,
            "MULTIPLY",
            string.Join(" * ", frozenOperands.Select(operand => operand.ToString(CultureInfo.InvariantCulture))),
            frozenOperands.Select(operand => operand.ToString(CultureInfo.InvariantCulture)).ToArray(),
            "MAGNITUDE_AND_RECEIPT",
            ordinary,
            primePath,
            ordinary.ResultDecimal == primePath.ResultDecimal && ordinary.OracleVerified && primePath.OracleVerified,
            AiBoundary);
    }

    private static OrdinaryArithmeticTrace BuildAdditionTrace(BigInteger left, BigInteger right)
    {
        var leftDigits = BigInteger.Abs(left).ToString(CultureInfo.InvariantCulture);
        var rightDigits = BigInteger.Abs(right).ToString(CultureInfo.InvariantCulture);
        var columns = Math.Max(leftDigits.Length, rightDigits.Length);
        var carry = 0;
        long carryEvents = 0;
        var resultDigits = new List<char>(columns + 1);
        var steps = new List<DecimalColumnStep>(columns);
        for (var column = 0; column < columns; column++)
        {
            var leftIndex = leftDigits.Length - 1 - column;
            var rightIndex = rightDigits.Length - 1 - column;
            var leftDigit = leftIndex >= 0 ? leftDigits[leftIndex] - '0' : 0;
            var rightDigit = rightIndex >= 0 ? rightDigits[rightIndex] - '0' : 0;
            var carryIn = carry;
            var rawTotal = leftDigit + rightDigit + carryIn;
            var resultDigit = rawTotal % 10;
            carry = rawTotal / 10;
            if (carry != 0)
            {
                carryEvents++;
            }

            steps.Add(new DecimalColumnStep(
                column,
                leftDigit,
                rightDigit,
                carryIn,
                rawTotal,
                resultDigit,
                carry));
            resultDigits.Add((char)('0' + resultDigit));
        }

        if (carry != 0)
        {
            resultDigits.Add((char)('0' + carry));
        }

        resultDigits.Reverse();
        var unsignedResult = new string(resultDigits.ToArray()).TrimStart('0');
        if (unsignedResult.Length == 0)
        {
            unsignedResult = "0";
        }

        var negative = left.Sign < 0 || right.Sign < 0;
        var tracedResult = negative && unsignedResult != "0" ? $"-{unsignedResult}" : unsignedResult;
        var oracleResult = (left + right).ToString(CultureInfo.InvariantCulture);
        return new OrdinaryArithmeticTrace(
            "EXPLICIT_DECIMAL_BASELINE_TRACE",
            "COLUMN_ADD_DECIMAL_V1",
            tracedResult,
            steps,
            Array.Empty<MagnitudeMultiplicationStep>(),
            new OrdinaryArithmeticWork(
                columns,
                checked(columns * 2L),
                carryEvents,
                0),
            tracedResult == oracleResult,
            "Replayable public decimal-column algorithm; counts are not tokens, latency, or private model reasoning.");
    }

    private static OrdinaryArithmeticTrace BuildMultiplicationTrace(IReadOnlyList<BigInteger> operands)
    {
        var accumulator = operands[0];
        var steps = new List<MagnitudeMultiplicationStep>(operands.Count - 1);
        for (var index = 1; index < operands.Count; index++)
        {
            var before = accumulator;
            accumulator *= operands[index];
            steps.Add(new MagnitudeMultiplicationStep(
                index - 1,
                before.ToString(CultureInfo.InvariantCulture),
                operands[index].ToString(CultureInfo.InvariantCulture),
                accumulator.ToString(CultureInfo.InvariantCulture)));
        }

        var oracle = operands.Aggregate(BigInteger.One, (current, operand) => current * operand);
        return new OrdinaryArithmeticTrace(
            "EXPLICIT_DECIMAL_BASELINE_TRACE",
            "SEQUENTIAL_MAGNITUDE_MULTIPLY_V1",
            accumulator.ToString(CultureInfo.InvariantCulture),
            Array.Empty<DecimalColumnStep>(),
            steps,
            new OrdinaryArithmeticWork(0, 0, 0, operands.Count - 1L),
            accumulator == oracle,
            "Replayable public left-fold multiplication; the BigInteger operations are explicit semantic baseline work, not a hardware or cognition metric.");
    }

    private static PrimePowerReceipt[] CommonCertifiedFactors(PrimeReceipt left, PrimeReceipt right)
    {
        var leftFactors = left.PrimePowers.ToDictionary(factor => factor.PrimeDecimal, StringComparer.Ordinal);
        var common = new List<PrimePowerReceipt>();
        foreach (var factor in right.PrimePowers)
        {
            if (!leftFactors.TryGetValue(factor.PrimeDecimal, out var leftFactor))
            {
                continue;
            }

            var exponent = Math.Min(leftFactor.Exponent, factor.Exponent);
            if (exponent > 0)
            {
                common.Add(new PrimePowerReceipt(
                    factor.PrimeDecimal,
                    exponent,
                    PrimeFactorProofKind.InheritedFromParentReceipts));
            }
        }

        return common
            .OrderBy(factor => BigInteger.Parse(factor.PrimeDecimal, CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static bool IsExact(PrimeReceipt receipt) =>
        receipt.Status != PrimeReceiptStatus.PartialBudget && receipt.ReconstructionVerified;

    private static long ConstructionReconstructionCount(PrimeReceipt receipt) =>
        receipt.Algorithm == PrimeReceiptCalculator.CompositionAlgorithm
            ? receipt.Status == PrimeReceiptStatus.ExactZero ? 0 : 1
            : receipt.Status is PrimeReceiptStatus.ExactFactorization or PrimeReceiptStatus.PartialBudget ? 1 : 0;
}
