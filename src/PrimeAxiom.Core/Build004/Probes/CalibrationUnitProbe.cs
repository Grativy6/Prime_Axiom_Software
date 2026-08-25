using System.Collections.ObjectModel;

namespace PrimeAxiom.Core.Build004.Probes;

public enum ProbeMeasurementTransformKind
{
    ExactRatioScale,
    Affine,
    Logarithmic,
    Nonlinear,
}

public enum ProbeBoundaryDisposition
{
    ExactRepresentationLocal,
    ExplicitTransformCrossing,
    Unresolved,
}

public enum ProbeUncertaintyKind
{
    NoneDeclared,
    Independent,
    Correlated,
    NotEvaluated,
}

public enum ProbeCoefficientStatus
{
    ExactDefined,
    ExactCalibratedRatio,
    Rounded,
    NotApplicable,
}

public enum ProbeEvidenceAuthentication
{
    Unauthenticated,
    IntegrityOnly,
    AuthenticatedExternally,
}

/// <summary>
/// Evidence is deliberately not encoded into the numeric prime coordinates or
/// unit dimensions. Authentication is declared, never inferred from a digest.
/// </summary>
public sealed record ProbeCalibrationEvidenceEnvelope
{
    public ProbeCalibrationEvidenceEnvelope(
        string evidenceId,
        string sourceReference,
        DateTimeOffset validFrom,
        DateTimeOffset validThrough,
        ProbeCoefficientStatus coefficientStatus,
        ProbeUncertaintyKind uncertaintyKind,
        string uncertaintyStatement,
        ProbeEvidenceAuthentication authentication,
        string residual)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            throw new ArgumentException("Evidence requires an identity.", nameof(evidenceId));
        }

        if (string.IsNullOrWhiteSpace(sourceReference))
        {
            throw new ArgumentException("Evidence requires a source reference.", nameof(sourceReference));
        }

        if (validThrough < validFrom)
        {
            throw new ArgumentOutOfRangeException(nameof(validThrough), "Validity cannot end before it starts.");
        }

        if (!Enum.IsDefined(coefficientStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(coefficientStatus));
        }

        if (!Enum.IsDefined(uncertaintyKind))
        {
            throw new ArgumentOutOfRangeException(nameof(uncertaintyKind));
        }

        if (!Enum.IsDefined(authentication))
        {
            throw new ArgumentOutOfRangeException(nameof(authentication));
        }

        EvidenceId = evidenceId;
        SourceReference = sourceReference;
        ValidFrom = validFrom;
        ValidThrough = validThrough;
        CoefficientStatus = coefficientStatus;
        UncertaintyKind = uncertaintyKind;
        UncertaintyStatement = uncertaintyStatement ?? string.Empty;
        Authentication = authentication;
        Residual = residual ?? string.Empty;
    }

    public string EvidenceId { get; }

    public string SourceReference { get; }

    public DateTimeOffset ValidFrom { get; }

    public DateTimeOffset ValidThrough { get; }

    public ProbeCoefficientStatus CoefficientStatus { get; }

    public ProbeUncertaintyKind UncertaintyKind { get; }

    public string UncertaintyStatement { get; }

    public ProbeEvidenceAuthentication Authentication { get; }

    public string Residual { get; }

    public bool IsValidAt(DateTimeOffset instant) => instant >= ValidFrom && instant <= ValidThrough;
}

/// <summary>
/// A bounded measurement-transform receipt with four independent axes:
/// numeric factors, physical dimensions, derivation labels, and evidence.
/// Affine/logarithmic/nonlinear transforms never masquerade as exponent merges.
/// </summary>
public sealed class ProbeMeasurementTransformReceipt
{
    public const string ClaimCeiling =
        "BOUNDED_SOFTWARE_RECEIPT_ONLY__NO_EMPIRICAL_TRUTH_AUTHORITY_OR_PHYSICAL_CALIBRATION_CLAIM";

    private readonly ReadOnlyCollection<string> _parentDerivationReceiptIds;
    private readonly ReadOnlyCollection<ProbeCalibrationEvidenceEnvelope> _evidence;

    private ProbeMeasurementTransformReceipt(
        string receiptId,
        ProbeMeasurementTransformKind transformKind,
        ProbeBoundaryDisposition disposition,
        ProbeExactRatio? nominalCoefficient,
        ProbeSignedPrimeCoordinates? numericFactors,
        ProbeUnitDimensionVector dimension,
        string derivationReceiptId,
        IReadOnlyList<string> parentDerivationReceiptIds,
        IReadOnlyList<ProbeCalibrationEvidenceEnvelope> evidence,
        DateTimeOffset evaluatedAt,
        string crossingReason)
    {
        if (string.IsNullOrWhiteSpace(receiptId))
        {
            throw new ArgumentException("Receipt identity is required.", nameof(receiptId));
        }

        if (string.IsNullOrWhiteSpace(derivationReceiptId))
        {
            throw new ArgumentException("A separate derivation identity is required.", nameof(derivationReceiptId));
        }

        ReceiptId = receiptId;
        TransformKind = transformKind;
        Disposition = disposition;
        NominalCoefficient = nominalCoefficient;
        NumericFactors = numericFactors;
        Dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
        DerivationReceiptId = derivationReceiptId;
        _parentDerivationReceiptIds = Array.AsReadOnly(parentDerivationReceiptIds.ToArray());
        _evidence = Array.AsReadOnly(evidence.ToArray());
        EvaluatedAt = evaluatedAt;
        CrossingReason = crossingReason ?? string.Empty;
    }

    public string ReceiptId { get; }

    public ProbeMeasurementTransformKind TransformKind { get; }

    public ProbeBoundaryDisposition Disposition { get; }

    public ProbeExactRatio? NominalCoefficient { get; }

    public ProbeSignedPrimeCoordinates? NumericFactors { get; }

    public ProbeUnitDimensionVector Dimension { get; }

    public string DerivationReceiptId { get; }

    public IReadOnlyList<string> ParentDerivationReceiptIds => _parentDerivationReceiptIds;

    public IReadOnlyList<ProbeCalibrationEvidenceEnvelope> Evidence => _evidence;

    public DateTimeOffset EvaluatedAt { get; }

    public string CrossingReason { get; }

    public static ProbeMeasurementTransformReceipt ExactRatioScale(
        string receiptId,
        ProbeExactRatio coefficient,
        ProbeUnitDimensionVector dimension,
        string derivationReceiptId,
        IEnumerable<ProbeCalibrationEvidenceEnvelope> evidence,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(coefficient);
        ArgumentNullException.ThrowIfNull(dimension);
        if (coefficient.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coefficient), "A ratio-scale conversion must be strictly positive.");
        }

        if (dimension.Completeness != Lineage.LineageCompleteness.Exact)
        {
            throw new ArgumentException(
                "An exact ratio-scale receipt requires an Exact unit-dimension projection.",
                nameof(dimension));
        }

        var evidenceCopy = CopyEvidence(evidence);
        var (disposition, reason) = AssessRatioEvidence(evidenceCopy, evaluatedAt);
        var projectionIsUsable = disposition != ProbeBoundaryDisposition.Unresolved;
        var retainedDimension = projectionIsUsable
            ? dimension
            : ProbeUnitDimensionVector.CreateDeclared(
                dimension.BasisId,
                Lineage.LineageCompleteness.Conflict,
                dimension.PayloadReplayability);
        return new ProbeMeasurementTransformReceipt(
            receiptId,
            ProbeMeasurementTransformKind.ExactRatioScale,
            disposition,
            coefficient,
            projectionIsUsable ? ProbeSignedPrimeCoordinates.FromRatio(coefficient) : null,
            retainedDimension,
            derivationReceiptId,
            Array.Empty<string>(),
            evidenceCopy,
            evaluatedAt,
            reason);
    }

    public static ProbeMeasurementTransformReceipt ExplicitCrossing(
        string receiptId,
        ProbeMeasurementTransformKind transformKind,
        ProbeUnitDimensionVector dimension,
        string derivationReceiptId,
        IEnumerable<ProbeCalibrationEvidenceEnvelope> evidence,
        DateTimeOffset evaluatedAt,
        string reason)
    {
        if (!Enum.IsDefined(transformKind))
        {
            throw new ArgumentOutOfRangeException(nameof(transformKind));
        }

        if (transformKind == ProbeMeasurementTransformKind.ExactRatioScale)
        {
            throw new ArgumentException("Use ExactRatioScale for multiplicative transforms.", nameof(transformKind));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A crossing requires an explicit reason.", nameof(reason));
        }

        var evidenceCopy = CopyEvidence(evidence);
        var expired = evidenceCopy.Any(item => !item.IsValidAt(evaluatedAt));
        return new ProbeMeasurementTransformReceipt(
            receiptId,
            transformKind,
            expired ? ProbeBoundaryDisposition.Unresolved : ProbeBoundaryDisposition.ExplicitTransformCrossing,
            nominalCoefficient: null,
            numericFactors: null,
            dimension,
            derivationReceiptId,
            Array.Empty<string>(),
            evidenceCopy,
            evaluatedAt,
            expired ? $"EXPIRED_VALIDITY__{reason}" : reason);
    }

    public static ProbeMeasurementTransformReceipt ComposeExact(
        string receiptId,
        string derivationReceiptId,
        ProbeMeasurementTransformReceipt left,
        ProbeMeasurementTransformReceipt right,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.TransformKind != ProbeMeasurementTransformKind.ExactRatioScale ||
            right.TransformKind != ProbeMeasurementTransformKind.ExactRatioScale ||
            left.Disposition != ProbeBoundaryDisposition.ExactRepresentationLocal ||
            right.Disposition != ProbeBoundaryDisposition.ExactRepresentationLocal ||
            left.NominalCoefficient is null ||
            right.NominalCoefficient is null ||
            left.NumericFactors is null ||
            right.NumericFactors is null ||
            left.Dimension.Completeness != Lineage.LineageCompleteness.Exact ||
            right.Dimension.Completeness != Lineage.LineageCompleteness.Exact)
        {
            throw new InvalidOperationException(
                "Only currently valid exact ratio-scale receipts compose through exponent merging.");
        }

        var evidence = left._evidence.Concat(right._evidence).ToArray();
        var (disposition, reason) = AssessRatioEvidence(evidence, evaluatedAt);
        var coefficient = left.NominalCoefficient.Multiply(right.NominalCoefficient);

        if (!string.Equals(
                left.NumericFactors.BasisId,
                right.NumericFactors.BasisId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Numeric-factor projections from different prime-basis identities cannot compose.");
        }

        if (!string.Equals(
                left.Dimension.BasisId,
                right.Dimension.BasisId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Unit-dimension projections from different basis identities cannot compose.");
        }

        if (disposition != ProbeBoundaryDisposition.ExactRepresentationLocal)
        {
            var unresolvedDimension = ProbeUnitDimensionVector.CreateDeclared(
                left.Dimension.BasisId,
                Lineage.LineageCompleteness.Conflict,
                ProbeProjectionKnowledge.CombineReplayability(
                    left.Dimension.PayloadReplayability,
                    right.Dimension.PayloadReplayability));
            return new ProbeMeasurementTransformReceipt(
                receiptId,
                ProbeMeasurementTransformKind.ExactRatioScale,
                disposition,
                coefficient,
                numericFactors: null,
                unresolvedDimension,
                derivationReceiptId,
                new[] { left.DerivationReceiptId, right.DerivationReceiptId },
                evidence,
                evaluatedAt,
                reason);
        }

        var factors = left.NumericFactors.Compose(right.NumericFactors);
        if (!factors.ToRatio().Equals(coefficient))
        {
            throw new InvalidOperationException("Numeric-factor composition disagreed with exact-ratio composition.");
        }

        return new ProbeMeasurementTransformReceipt(
            receiptId,
            ProbeMeasurementTransformKind.ExactRatioScale,
            disposition,
            coefficient,
            factors,
            left.Dimension.Multiply(right.Dimension),
            derivationReceiptId,
            new[] { left.DerivationReceiptId, right.DerivationReceiptId },
            evidence,
            evaluatedAt,
            reason);
    }

    public ProbeExactRatio ApplyExact(ProbeExactRatio input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (Disposition != ProbeBoundaryDisposition.ExactRepresentationLocal ||
            TransformKind != ProbeMeasurementTransformKind.ExactRatioScale ||
            NominalCoefficient is null)
        {
            throw new InvalidOperationException(
                "This transform crosses the exact ratio-scale boundary and cannot be applied as a local exponent merge.");
        }

        return input.Multiply(NominalCoefficient);
    }

    private static ProbeCalibrationEvidenceEnvelope[] CopyEvidence(
        IEnumerable<ProbeCalibrationEvidenceEnvelope> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var copy = evidence.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("At least one evidence envelope is required.", nameof(evidence));
        }

        if (copy.Any(item => item is null))
        {
            throw new ArgumentException("Evidence envelopes cannot contain null.", nameof(evidence));
        }

        return copy;
    }

    private static (ProbeBoundaryDisposition Disposition, string Reason) AssessRatioEvidence(
        IReadOnlyList<ProbeCalibrationEvidenceEnvelope> evidence,
        DateTimeOffset evaluatedAt)
    {
        if (evidence.Any(item => !item.IsValidAt(evaluatedAt)))
        {
            return (ProbeBoundaryDisposition.Unresolved, "EXPIRED_VALIDITY");
        }

        if (evidence.Any(item => item.CoefficientStatus == ProbeCoefficientStatus.Rounded))
        {
            return (
                ProbeBoundaryDisposition.ExplicitTransformCrossing,
                "ROUNDED_COEFFICIENT__NOMINAL_RATIO_IS_NOT_AN_EXACT_CALIBRATION_CLAIM");
        }

        if (evidence.Any(item => item.UncertaintyKind == ProbeUncertaintyKind.Correlated))
        {
            return (
                ProbeBoundaryDisposition.ExplicitTransformCrossing,
                "CORRELATED_UNCERTAINTY__COVARIANCE_PROPAGATION_REQUIRED");
        }

        if (evidence.Any(item => item.UncertaintyKind == ProbeUncertaintyKind.NotEvaluated))
        {
            return (
                ProbeBoundaryDisposition.Unresolved,
                "UNCERTAINTY_NOT_EVALUATED");
        }

        return (ProbeBoundaryDisposition.ExactRepresentationLocal, string.Empty);
    }
}
