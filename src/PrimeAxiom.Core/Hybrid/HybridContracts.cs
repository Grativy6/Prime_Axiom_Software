using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hybrid;

public enum ValuationKnowledge
{
    KnownExact,
    CertifiedLowerBound,
}

public enum LaneProvenance
{
    Zero,
    BinaryIngress,
    StructuredIngress,
    Multiplication,
    ExactDivision,
    Power,
    UnequalValuationAddition,
    CommonLowerBoundAddition,
    Refresh,
    BankMigration,
    RationalCancellation,
}

public enum HybridValidity
{
    Canonical,
    Partial,
}

public enum HybridFailure
{
    None,
    BankMismatch,
    ExponentWidthMismatch,
    ExponentOverflow,
    NotDivisible,
    DivisionByZero,
    RequiresCanonical,
    NegativePower,
    InvalidStructuredIngress,
    UnverifiedStructuredIngress,
    InvalidSerialization,
    ClaimedMagnitudeMismatch,
    InvalidLane,
    InvalidRegister,
    InvalidInstruction,
    DestinationInvalidated,
}

public enum HybridDomain
{
    None,
    BankNative,
    CofactorArithmetic,
    Boundary,
    Maintenance,
    Mixed,
}

public enum CostPhase
{
    Ingress,
    Native,
    Maintenance,
    Egress,
}

/// <summary>
/// Counts are intentionally heterogeneous and remain separately named. They
/// must not be collapsed into a fictitious universal unit.
/// </summary>
public readonly record struct HybridCostVector(
    GateCost BankGates,
    long TrialRemainders = 0,
    long FactorDivisions = 0,
    long CofactorAdditions = 0,
    long CofactorMultiplications = 0,
    long CofactorDivisions = 0,
    long CofactorRemainders = 0,
    long CofactorGcds = 0,
    long CofactorComparisons = 0,
    long ReconstructionMultiplications = 0,
    long ModeledBinaryNands = 0,
    long BinaryOperandBits = 0,
    long LaneReads = 0,
    long LaneWrites = 0,
    long KnowledgeTransitions = 0,
    long MetadataReads = 0,
    long MetadataWrites = 0,
    long SerializedBytes = 0,
    long Migrations = 0)
{
    public static HybridCostVector Zero => new(GateCost.Zero);

    public static HybridCostVector operator +(HybridCostVector left, HybridCostVector right) =>
        new(
            Serial(left.BankGates, right.BankGates),
            checked(left.TrialRemainders + right.TrialRemainders),
            checked(left.FactorDivisions + right.FactorDivisions),
            checked(left.CofactorAdditions + right.CofactorAdditions),
            checked(left.CofactorMultiplications + right.CofactorMultiplications),
            checked(left.CofactorDivisions + right.CofactorDivisions),
            checked(left.CofactorRemainders + right.CofactorRemainders),
            checked(left.CofactorGcds + right.CofactorGcds),
            checked(left.CofactorComparisons + right.CofactorComparisons),
            checked(left.ReconstructionMultiplications + right.ReconstructionMultiplications),
            checked(left.ModeledBinaryNands + right.ModeledBinaryNands),
            checked(left.BinaryOperandBits + right.BinaryOperandBits),
            checked(left.LaneReads + right.LaneReads),
            checked(left.LaneWrites + right.LaneWrites),
            checked(left.KnowledgeTransitions + right.KnowledgeTransitions),
            checked(left.MetadataReads + right.MetadataReads),
            checked(left.MetadataWrites + right.MetadataWrites),
            checked(left.SerializedBytes + right.SerializedBytes),
            checked(left.Migrations + right.Migrations));

    private static GateCost Serial(GateCost left, GateCost right) =>
        new(
            checked(left.NandEvaluations + right.NandEvaluations),
            checked(left.CriticalPathDepth + right.CriticalPathDepth));
}

public readonly record struct HybridCostLedger(
    HybridCostVector Ingress,
    HybridCostVector Native,
    HybridCostVector Maintenance,
    HybridCostVector Egress)
{
    public static HybridCostLedger Zero => new(
        HybridCostVector.Zero,
        HybridCostVector.Zero,
        HybridCostVector.Zero,
        HybridCostVector.Zero);

    public HybridCostVector Total => Ingress + Native + Maintenance + Egress;

    public HybridCostLedger Add(CostPhase phase, HybridCostVector cost) => phase switch
    {
        CostPhase.Ingress => this with { Ingress = Ingress + cost },
        CostPhase.Native => this with { Native = Native + cost },
        CostPhase.Maintenance => this with { Maintenance = Maintenance + cost },
        CostPhase.Egress => this with { Egress = Egress + cost },
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    public static HybridCostLedger operator +(HybridCostLedger left, HybridCostLedger right) =>
        new(
            left.Ingress + right.Ingress,
            left.Native + right.Native,
            left.Maintenance + right.Maintenance,
            left.Egress + right.Egress);
}

public sealed record HybridReceipt(
    string Operation,
    bool Succeeded,
    HybridFailure Failure,
    HybridDomain Domain,
    HybridCostLedger Cost,
    HybridValidity? ValidityBefore,
    HybridValidity? ValidityAfter,
    string Scope,
    string? Detail = null);

public sealed record HybridResult<T>(T? Value, HybridReceipt Receipt) where T : class;

public sealed record HybridQueryResult<T>(T? Value, bool IsKnown, HybridReceipt Receipt);

public enum ValuationResultKind
{
    Finite,
    PositiveInfinity,
}

public sealed record ValuationAnswer(
    System.Numerics.BigInteger LowerBound,
    bool IsExact,
    ValuationResultKind Kind = ValuationResultKind.Finite);

public sealed record HybridPayloadMetrics(
    long SignAndZeroBits,
    long ExponentBits,
    long CofactorBits,
    long KnowledgeBits,
    long ProvenanceBits,
    long BankCatalogBits)
{
    public long PerValuePayloadBits => SignAndZeroBits + ExponentBits + CofactorBits + KnowledgeBits + ProvenanceBits;
}
