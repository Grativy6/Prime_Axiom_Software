using System.Globalization;
using PrimeAxiom.Core.Hardware;

namespace PrimeAxiom.Cli;

internal static class Build002Evidence
{
    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    public static string Boolean(bool value) => value ? "true" : "false";
}

internal sealed record Build002StaticCostRow(
    string Implementation,
    int Width,
    string Module,
    string Operation,
    string Architecture,
    string EvidenceClass,
    string SupportStatus,
    NandStaticMetrics Metrics,
    string Notes)
{
    public static IReadOnlyList<string> Headers { get; } =
    [
        "protocol_id", "implementation", "width", "module", "operation", "architecture",
        "evidence_class", "support_status", "nand2_static", "dff_static", "state_bits",
        "input_bits", "output_bits", "port_bits", "wire_bits", "connections_static",
        "max_fanout", "cross_region_connections", "cross_lane_connections",
        "unit_nand_critical_depth", "combinational_loop_status", "notes",
    ];

    public IReadOnlyList<string> ToCsv() =>
    [
        Build002Protocol.Id,
        Implementation,
        Build002Evidence.Number(Width),
        Module,
        Operation,
        Architecture,
        EvidenceClass,
        SupportStatus,
        Build002Evidence.Number(Metrics.Nand2Static),
        Build002Evidence.Number(Metrics.DffStatic),
        Build002Evidence.Number(Metrics.StateBits),
        Build002Evidence.Number(Metrics.InputBits),
        Build002Evidence.Number(Metrics.OutputBits),
        Build002Evidence.Number(Metrics.PortBits),
        Build002Evidence.Number(Metrics.WireBits),
        Build002Evidence.Number(Metrics.ConnectionsStatic),
        Build002Evidence.Number(Metrics.MaximumFanout),
        Build002Evidence.Number(Metrics.CrossRegionConnections),
        Build002Evidence.Number(Metrics.CrossLaneConnections),
        Build002Evidence.Number(Metrics.UnitNandCriticalDepth),
        Metrics.CombinationalLoopStatus.ToString().ToUpperInvariant(),
        Notes,
    ];
}

internal sealed record Build002DynamicOperationRow(
    string Implementation,
    int Width,
    string Operation,
    string Regime,
    string OutputObligation,
    string EvidenceClass,
    string SupportStatus,
    long Cases,
    long Instructions,
    long Cycles,
    long NandEvaluations,
    long NandOutputTransitions,
    long StateBitTransitions,
    long InputBitTransitions,
    long InitialNandTransitions,
    long Rejections,
    long Encodes,
    long Reconstructs,
    long Refreshes,
    string Notes,
    string OperationClass = "UNCLASSIFIED")
{
    public static IReadOnlyList<string> Headers { get; } =
    [
        "protocol_id", "implementation", "width", "operation", "operation_class", "regime",
        "output_obligation", "evidence_class", "support_status", "cases", "instructions",
        "cycles", "nand_evaluations", "nand_output_transitions", "state_bit_transitions",
        "input_bit_transitions", "initial_nand_transitions", "rejections", "encodes",
        "reconstructs", "refreshes", "notes",
    ];

    public IReadOnlyList<string> ToCsv() =>
    [
        Build002Protocol.Id,
        Implementation,
        Build002Evidence.Number(Width),
        Operation,
        OperationClass,
        Regime,
        OutputObligation,
        EvidenceClass,
        SupportStatus,
        Build002Evidence.Number(Cases),
        Build002Evidence.Number(Instructions),
        Build002Evidence.Number(Cycles),
        Build002Evidence.Number(NandEvaluations),
        Build002Evidence.Number(NandOutputTransitions),
        Build002Evidence.Number(StateBitTransitions),
        Build002Evidence.Number(InputBitTransitions),
        Build002Evidence.Number(InitialNandTransitions),
        Build002Evidence.Number(Rejections),
        Build002Evidence.Number(Encodes),
        Build002Evidence.Number(Reconstructs),
        Build002Evidence.Number(Refreshes),
        Notes,
    ];
}

internal sealed record Build002WorkloadRow(
    string Experiment,
    string TraceId,
    string Implementation,
    int Width,
    string Regime,
    string OutputObligation,
    string Phase,
    string EvidenceClass,
    string SupportStatus,
    int Operations,
    long Cycles,
    long NandEvaluations,
    long NandOutputTransitions,
    long StateBitTransitions,
    long Rejections,
    long Encodes,
    long Reconstructs,
    long Refreshes,
    string FinalValue,
    string Feature,
    string Notes,
    string OperationClass = "UNCLASSIFIED",
    long InputBitTransitions = -1,
    long InitialNandTransitions = -1)
{
    public static IReadOnlyList<string> Headers { get; } =
    [
        "protocol_id", "experiment", "trace_id", "implementation", "width", "regime",
        "output_obligation", "phase", "operation_class", "evidence_class", "support_status", "operations",
        "cycles", "nand_evaluations", "nand_output_transitions", "state_bit_transitions",
        "input_bit_transitions", "initial_nand_transitions", "rejections", "encodes",
        "reconstructs", "refreshes", "final_value", "feature", "notes",
    ];

    public IReadOnlyList<string> ToCsv() =>
    [
        Build002Protocol.Id,
        Experiment,
        TraceId,
        Implementation,
        Build002Evidence.Number(Width),
        Regime,
        OutputObligation,
        Phase,
        OperationClass,
        EvidenceClass,
        SupportStatus,
        Build002Evidence.Number(Operations),
        Build002Evidence.Number(Cycles),
        Build002Evidence.Number(NandEvaluations),
        Build002Evidence.Number(NandOutputTransitions),
        Build002Evidence.Number(StateBitTransitions),
        Build002Evidence.Number(InputBitTransitions),
        Build002Evidence.Number(InitialNandTransitions),
        Build002Evidence.Number(Rejections),
        Build002Evidence.Number(Encodes),
        Build002Evidence.Number(Reconstructs),
        Build002Evidence.Number(Refreshes),
        FinalValue,
        Feature,
        Notes,
    ];
}

internal sealed record Build002RepresentationRow(
    int Width,
    string Representation,
    string EvidenceClass,
    int MagnitudeBits,
    int ExponentOrThresholdBits,
    int TagBits,
    int TotalStateBits,
    int RepresentablePayloadStates,
    string NativeCheapOperations,
    string DominantCost,
    string Notes)
{
    public static IReadOnlyList<string> Headers { get; } =
    [
        "protocol_id", "width", "representation", "evidence_class", "magnitude_bits",
        "exponent_or_threshold_bits", "tag_bits", "total_state_bits",
        "representable_payload_states", "native_cheap_operations", "dominant_cost", "notes",
    ];

    public IReadOnlyList<string> ToCsv() =>
    [
        Build002Protocol.Id,
        Build002Evidence.Number(Width),
        Representation,
        EvidenceClass,
        Build002Evidence.Number(MagnitudeBits),
        Build002Evidence.Number(ExponentOrThresholdBits),
        Build002Evidence.Number(TagBits),
        Build002Evidence.Number(TotalStateBits),
        Build002Evidence.Number(RepresentablePayloadStates),
        NativeCheapOperations,
        DominantCost,
        Notes,
    ];
}

internal sealed record Build002IngressEgressRow(
    string Implementation,
    int Width,
    string Direction,
    string Method,
    string EvidenceClass,
    int Cases,
    long Cycles,
    long PrimitiveOperations,
    long NandEvaluations,
    string SupportStatus,
    string Notes)
{
    public static IReadOnlyList<string> Headers { get; } =
    [
        "protocol_id", "implementation", "width", "direction", "method", "evidence_class",
        "cases", "cycles", "primitive_operations", "nand_evaluations", "support_status", "notes",
    ];

    public IReadOnlyList<string> ToCsv() =>
    [
        Build002Protocol.Id,
        Implementation,
        Build002Evidence.Number(Width),
        Direction,
        Method,
        EvidenceClass,
        Build002Evidence.Number(Cases),
        Build002Evidence.Number(Cycles),
        Build002Evidence.Number(PrimitiveOperations),
        Build002Evidence.Number(NandEvaluations),
        SupportStatus,
        Notes,
    ];
}

internal sealed record Build002AdditionRow(
    int Width,
    int Left,
    int Right,
    int Sum,
    string Prime,
    int LeftValuation,
    int RightValuation,
    int PreservedLowerBound,
    int ExactValuation,
    bool ExactWithoutRefresh,
    string SidecarStatus,
    string Notes)
{
    public static IReadOnlyList<string> Headers { get; } =
    [
        "protocol_id", "width", "left", "right", "sum", "prime", "left_valuation",
        "right_valuation", "preserved_lower_bound", "exact_sum_valuation",
        "exact_without_refresh", "sidecar_status", "notes",
    ];

    public IReadOnlyList<string> ToCsv() =>
    [
        Build002Protocol.Id,
        Build002Evidence.Number(Width),
        Build002Evidence.Number(Left),
        Build002Evidence.Number(Right),
        Build002Evidence.Number(Sum),
        Prime,
        Build002Evidence.Number(LeftValuation),
        Build002Evidence.Number(RightValuation),
        Build002Evidence.Number(PreservedLowerBound),
        Build002Evidence.Number(ExactValuation),
        Build002Evidence.Boolean(ExactWithoutRefresh),
        SidecarStatus,
        Notes,
    ];
}

internal sealed record Build002HostileRow(
    int Width,
    int Magnitude,
    string Kind,
    string PureVfuSupport,
    string SidecarSupport,
    int CatalogValuationBitsSet,
    int SidecarStateBits,
    string Notes)
{
    public static IReadOnlyList<string> Headers { get; } =
    [
        "protocol_id", "width", "magnitude", "kind", "pure_vfu_support", "sidecar_support",
        "catalog_valuation_bits_set", "sidecar_state_bits", "notes",
    ];

    public IReadOnlyList<string> ToCsv() =>
    [
        Build002Protocol.Id,
        Build002Evidence.Number(Width),
        Build002Evidence.Number(Magnitude),
        Kind,
        PureVfuSupport,
        SidecarSupport,
        Build002Evidence.Number(CatalogValuationBitsSet),
        Build002Evidence.Number(SidecarStateBits),
        Notes,
    ];
}

internal sealed record Build002SynthesisRow(
    string Platform,
    string ToolVersion,
    string Top,
    int Width,
    string Architecture,
    string Status,
    int Nand2Static,
    int DffStatic,
    int PortBits,
    int WireBits,
    int ConnectionsStatic,
    int MaximumFanout,
    int UnitNandCriticalDepth,
    string NetlistSha256,
    string Warnings,
    string Notes)
{
    public static IReadOnlyList<string> Headers { get; } =
    [
        "protocol_id", "platform", "tool_version", "top", "width", "architecture", "status",
        "nand2_static", "dff_static", "port_bits", "wire_bits", "connections_static",
        "max_fanout", "unit_nand_critical_depth", "netlist_sha256", "warnings", "notes",
    ];

    public IReadOnlyList<string> ToCsv() =>
    [
        Build002Protocol.Id,
        Platform,
        ToolVersion,
        Top,
        Build002Evidence.Number(Width),
        Architecture,
        Status,
        Build002Evidence.Number(Nand2Static),
        Build002Evidence.Number(DffStatic),
        Build002Evidence.Number(PortBits),
        Build002Evidence.Number(WireBits),
        Build002Evidence.Number(ConnectionsStatic),
        Build002Evidence.Number(MaximumFanout),
        Build002Evidence.Number(UnitNandCriticalDepth),
        NetlistSha256,
        Warnings,
        Notes,
    ];
}
