using System.Collections.ObjectModel;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hardware;

public enum NandNodeKind
{
    Input,
    Constant,
    State,
    Nand2,
}

public enum CombinationalLoopStatus
{
    Acyclic,
}

public enum SettledTransitionKind
{
    Input,
    State,
    NandOutput,
}

public sealed record NandNodeSpec(
    int Id,
    string Name,
    NandNodeKind Kind,
    string Region,
    int? LeftId = null,
    int? RightId = null,
    BitState? InitialValue = null);

public sealed record NandOutputSpec(string Name, int NodeId, string Region);

public sealed record DffBoundarySpec(
    int Id,
    string Name,
    int DataNodeId,
    int StateNodeId,
    string Region);

public readonly record struct NandSignal(int Id, string Name);

public sealed record SettledTransition(
    int NodeId,
    string NodeName,
    string Region,
    SettledTransitionKind Kind,
    BitState Before,
    BitState After);

public sealed record NandGateOutput(
    int NodeId,
    string NodeName,
    string Region,
    BitState State);

public sealed record NandStaticMetrics(
    int Nand2Static,
    int DffStatic,
    int StateBits,
    int InputBits,
    int OutputBits,
    int PortBits,
    int WireBits,
    int NandInputPinConnections,
    int TotalNetSinks,
    int ConnectionsStatic,
    int MaximumFanout,
    int UnitNandCriticalDepth,
    int CrossRegionConnections,
    int CrossLaneConnections,
    CombinationalLoopStatus CombinationalLoopStatus);

public sealed class NandEvaluation
{
    private readonly NandNetlist _owner;

    internal NandEvaluation(
        NandNetlist owner,
        IReadOnlyList<BitState> nodeValues,
        IReadOnlyDictionary<string, BitState> outputs,
        IReadOnlyDictionary<string, BitState> dffNextStates,
        IReadOnlyList<SettledTransition> settledTransitions)
    {
        _owner = owner;
        NodeValues = nodeValues;
        Outputs = outputs;
        DffNextStates = dffNextStates;
        SettledTransitions = settledTransitions;
        GateOutputs = Array.AsReadOnly(owner.TopologicalNodeIds
            .Select(nodeId => owner.Nodes[nodeId])
            .Where(node => node.Kind == NandNodeKind.Nand2)
            .Select(node => new NandGateOutput(
                node.Id,
                node.Name,
                node.Region,
                nodeValues[node.Id]))
            .ToArray());
        NandEvaluations = owner.Metrics.Nand2Static;
        NandOutputTransitions = settledTransitions.Count(
            transition => transition.Kind == SettledTransitionKind.NandOutput);
        InputTransitions = settledTransitions.Count(
            transition => transition.Kind == SettledTransitionKind.Input);
        StateBitTransitions = settledTransitions.Count(
            transition => transition.Kind == SettledTransitionKind.State);
    }

    public IReadOnlyList<BitState> NodeValues { get; }

    public IReadOnlyDictionary<string, BitState> Outputs { get; }

    public IReadOnlyDictionary<string, BitState> DffNextStates { get; }

    public IReadOnlyList<SettledTransition> SettledTransitions { get; }

    public IReadOnlyList<NandGateOutput> GateOutputs { get; }

    public int NandEvaluations { get; }

    public int NandOutputTransitions { get; }

    public int InputTransitions { get; }

    public int StateBitTransitions { get; }

    internal bool BelongsTo(NandNetlist netlist) => ReferenceEquals(_owner, netlist);
}

/// <summary>
/// A stable-identity acyclic graph whose only combinational cells are NAND2.
/// Inputs, constants, and state-boundary outputs are explicit driven nets.
/// DFF metadata breaks sequential feedback and is not silently converted into
/// a NAND-equivalent count.
/// </summary>
public sealed class NandNetlist
{
    private readonly ReadOnlyCollection<NandNodeSpec> _nodes;
    private readonly ReadOnlyCollection<NandOutputSpec> _outputs;
    private readonly ReadOnlyCollection<DffBoundarySpec> _dffBoundaries;
    private readonly ReadOnlyCollection<int> _topologicalOrder;
    private readonly ReadOnlyDictionary<string, NandNodeSpec> _inputNodes;
    private readonly ReadOnlyDictionary<string, NandNodeSpec> _stateNodes;

    public NandNetlist(
        string name,
        IEnumerable<NandNodeSpec> nodes,
        IEnumerable<NandOutputSpec> outputs,
        IEnumerable<DffBoundarySpec>? dffBoundaries = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(outputs);

        Name = name;
        var nodeArray = nodes.OrderBy(node => node.Id).ToArray();
        var outputArray = outputs.ToArray();
        var dffArray = dffBoundaries?.OrderBy(dff => dff.Id).ToArray() ?? [];

        ValidateNodeIdentities(nodeArray);
        ValidateNodeContracts(nodeArray);
        ValidateOutputContracts(nodeArray, outputArray);
        ValidateDffContracts(nodeArray, dffArray);

        var topologicalOrder = BuildTopologicalOrder(nodeArray);
        _nodes = Array.AsReadOnly(nodeArray);
        _outputs = Array.AsReadOnly(outputArray);
        _dffBoundaries = Array.AsReadOnly(dffArray);
        _topologicalOrder = Array.AsReadOnly(topologicalOrder);
        _inputNodes = new ReadOnlyDictionary<string, NandNodeSpec>(
            nodeArray
                .Where(node => node.Kind == NandNodeKind.Input)
                .ToDictionary(node => node.Name, StringComparer.Ordinal));
        _stateNodes = new ReadOnlyDictionary<string, NandNodeSpec>(
            nodeArray
                .Where(node => node.Kind == NandNodeKind.State)
                .ToDictionary(node => node.Name, StringComparer.Ordinal));
        Metrics = Measure(nodeArray, outputArray, dffArray, topologicalOrder);
    }

    public string Name { get; }

    public IReadOnlyList<NandNodeSpec> Nodes => _nodes;

    public IReadOnlyList<NandOutputSpec> NamedOutputs => _outputs;

    public IReadOnlyList<DffBoundarySpec> DffBoundaries => _dffBoundaries;

    public IReadOnlyList<int> TopologicalNodeIds => _topologicalOrder;

    public NandStaticMetrics Metrics { get; }

    public NandEvaluation Evaluate(
        IReadOnlyDictionary<string, BitState> inputs,
        IReadOnlyDictionary<string, BitState>? state = null,
        NandEvaluation? previous = null,
        bool compareWithAllOff = false)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (previous is not null && !previous.BelongsTo(this))
        {
            throw new ArgumentException(
                "A settled-transition reference must come from the same netlist instance.",
                nameof(previous));
        }

        if (previous is not null && compareWithAllOff)
        {
            throw new ArgumentException(
                "Choose either a previous settled evaluation or the all-off origin, not both.",
                nameof(compareWithAllOff));
        }

        ValidateProvidedValues(inputs, _inputNodes, nameof(inputs));
        var stateValues = state ?? EmptyStates;
        ValidateProvidedStateValues(stateValues);

        var values = new BitState[_nodes.Count];
        foreach (var nodeId in _topologicalOrder)
        {
            var node = _nodes[nodeId];
            values[nodeId] = node.Kind switch
            {
                NandNodeKind.Input => inputs[node.Name],
                NandNodeKind.Constant => node.InitialValue!.Value,
                NandNodeKind.State => stateValues.TryGetValue(node.Name, out var current)
                    ? current
                    : node.InitialValue!.Value,
                NandNodeKind.Nand2 => BitStateExtensions.FromBoolean(
                    !(values[node.LeftId!.Value].ToBoolean() &&
                      values[node.RightId!.Value].ToBoolean())),
                _ => throw new InvalidOperationException($"Undefined node kind {node.Kind}."),
            };
        }

        var namedOutputs = new Dictionary<string, BitState>(StringComparer.Ordinal);
        foreach (var output in _outputs)
        {
            namedOutputs.Add(output.Name, values[output.NodeId]);
        }

        var nextStates = new Dictionary<string, BitState>(StringComparer.Ordinal);
        foreach (var dff in _dffBoundaries)
        {
            nextStates.Add(_nodes[dff.StateNodeId].Name, values[dff.DataNodeId]);
        }

        var transitions = CaptureTransitions(values, previous, compareWithAllOff);
        return new NandEvaluation(
            this,
            Array.AsReadOnly(values),
            new ReadOnlyDictionary<string, BitState>(namedOutputs),
            new ReadOnlyDictionary<string, BitState>(nextStates),
            Array.AsReadOnly(transitions));
    }

    private static IReadOnlyDictionary<string, BitState> EmptyStates { get; } =
        new ReadOnlyDictionary<string, BitState>(
            new Dictionary<string, BitState>(StringComparer.Ordinal));

    private SettledTransition[] CaptureTransitions(
        IReadOnlyList<BitState> current,
        NandEvaluation? previous,
        bool compareWithAllOff)
    {
        if (previous is null && !compareWithAllOff)
        {
            return [];
        }

        var transitions = new List<SettledTransition>();
        foreach (var nodeId in _topologicalOrder)
        {
            var node = _nodes[nodeId];
            var before = previous is null ? BitState.Off : previous.NodeValues[node.Id];
            var after = current[node.Id];
            if (before == after || node.Kind == NandNodeKind.Constant)
            {
                continue;
            }

            var kind = node.Kind switch
            {
                NandNodeKind.Input => SettledTransitionKind.Input,
                NandNodeKind.State => SettledTransitionKind.State,
                NandNodeKind.Nand2 => SettledTransitionKind.NandOutput,
                _ => throw new InvalidOperationException($"Undefined transition node kind {node.Kind}."),
            };
            transitions.Add(new SettledTransition(
                node.Id,
                node.Name,
                node.Region,
                kind,
                before,
                after));
        }

        return [.. transitions];
    }

    private void ValidateProvidedStateValues(IReadOnlyDictionary<string, BitState> state)
    {
        foreach (var pair in state)
        {
            if (!_stateNodes.ContainsKey(pair.Key))
            {
                throw new ArgumentException($"Unknown state net '{pair.Key}'.", nameof(state));
            }

            ValidateBitState(pair.Value, pair.Key, nameof(state));
        }
    }

    private static void ValidateProvidedValues(
        IReadOnlyDictionary<string, BitState> provided,
        ReadOnlyDictionary<string, NandNodeSpec> required,
        string parameterName)
    {
        foreach (var pair in provided)
        {
            if (!required.ContainsKey(pair.Key))
            {
                throw new ArgumentException($"Unknown input net '{pair.Key}'.", parameterName);
            }

            ValidateBitState(pair.Value, pair.Key, parameterName);
        }

        foreach (var requiredName in required.Keys)
        {
            if (!provided.ContainsKey(requiredName))
            {
                throw new ArgumentException($"Missing input net '{requiredName}'.", parameterName);
            }
        }
    }

    private static void ValidateBitState(BitState value, string name, string parameterName)
    {
        if (value is not BitState.Off and not BitState.On)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Net '{name}' has undefined two-state value {(byte)value}.");
        }
    }

    private static void ValidateNodeIdentities(IReadOnlyList<NandNodeSpec> nodes)
    {
        if (nodes.Count == 0)
        {
            throw new ArgumentException("A netlist must contain at least one driven net.", nameof(nodes));
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node.Id != index)
            {
                throw new ArgumentException(
                    "Stable node IDs must be unique and contiguous from zero.",
                    nameof(nodes));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(node.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(node.Region);
            if (!names.Add(node.Name))
            {
                throw new ArgumentException(
                    $"Net '{node.Name}' has more than one driver.",
                    nameof(nodes));
            }

            if (!Enum.IsDefined(node.Kind))
            {
                throw new ArgumentException($"Node {node.Id} has an undefined kind.", nameof(nodes));
            }
        }
    }

    private static void ValidateNodeContracts(IReadOnlyList<NandNodeSpec> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node.Kind)
            {
                case NandNodeKind.Input:
                    RequireNoSourcesOrValue(node, requireValue: false, nodes);
                    break;
                case NandNodeKind.Constant:
                    RequireNoSourcesOrValue(node, requireValue: true, nodes);
                    break;
                case NandNodeKind.State:
                    RequireNoSourcesOrValue(node, requireValue: true, nodes);
                    break;
                case NandNodeKind.Nand2:
                    if (node.LeftId is null || node.RightId is null || node.InitialValue is not null)
                    {
                        throw new ArgumentException(
                            $"NAND node '{node.Name}' must have exactly two drivers and no initial value.",
                            nameof(nodes));
                    }

                    ValidateSource(node.LeftId.Value, node.Name, nodes);
                    ValidateSource(node.RightId.Value, node.Name, nodes);
                    break;
                default:
                    throw new ArgumentException($"Node '{node.Name}' has an undefined kind.", nameof(nodes));
            }
        }
    }

    private static void RequireNoSourcesOrValue(
        NandNodeSpec node,
        bool requireValue,
        IReadOnlyList<NandNodeSpec> nodes)
    {
        if (node.LeftId is not null || node.RightId is not null ||
            requireValue != node.InitialValue.HasValue)
        {
            throw new ArgumentException(
                $"Node '{node.Name}' has fields inconsistent with kind {node.Kind}.",
                nameof(nodes));
        }

        if (node.InitialValue.HasValue)
        {
            ValidateBitState(node.InitialValue.Value, node.Name, nameof(nodes));
        }
    }

    private static void ValidateSource(
        int sourceId,
        string sinkName,
        IReadOnlyList<NandNodeSpec> nodes)
    {
        if (sourceId < 0 || sourceId >= nodes.Count)
        {
            throw new ArgumentException(
                $"Node '{sinkName}' references undriven net ID {sourceId}.",
                nameof(nodes));
        }
    }

    private static void ValidateOutputContracts(
        IReadOnlyList<NandNodeSpec> nodes,
        IReadOnlyList<NandOutputSpec> outputs)
    {
        if (outputs.Count == 0)
        {
            throw new ArgumentException("A netlist must expose at least one named output.", nameof(outputs));
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var output in outputs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(output.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(output.Region);
            if (!names.Add(output.Name))
            {
                throw new ArgumentException($"Output '{output.Name}' is duplicated.", nameof(outputs));
            }

            ValidateSource(output.NodeId, output.Name, nodes);
        }
    }

    private static void ValidateDffContracts(
        IReadOnlyList<NandNodeSpec> nodes,
        IReadOnlyList<DffBoundarySpec> dffBoundaries)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var stateDrivers = new HashSet<int>();
        for (var index = 0; index < dffBoundaries.Count; index++)
        {
            var dff = dffBoundaries[index];
            if (dff.Id != index)
            {
                throw new ArgumentException(
                    "DFF IDs must be unique and contiguous from zero.",
                    nameof(dffBoundaries));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(dff.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(dff.Region);
            if (!names.Add(dff.Name))
            {
                throw new ArgumentException($"DFF '{dff.Name}' is duplicated.", nameof(dffBoundaries));
            }

            ValidateSource(dff.DataNodeId, dff.Name, nodes);
            ValidateSource(dff.StateNodeId, dff.Name, nodes);
            if (nodes[dff.StateNodeId].Kind != NandNodeKind.State)
            {
                throw new ArgumentException(
                    $"DFF '{dff.Name}' Q net must be a state-boundary net.",
                    nameof(dffBoundaries));
            }

            if (!stateDrivers.Add(dff.StateNodeId))
            {
                throw new ArgumentException(
                    $"State net '{nodes[dff.StateNodeId].Name}' has duplicate DFF drivers.",
                    nameof(dffBoundaries));
            }
        }
    }

    private static int[] BuildTopologicalOrder(IReadOnlyList<NandNodeSpec> nodes)
    {
        var visit = new byte[nodes.Count];
        var order = new List<int>(nodes.Count);

        void Visit(int nodeId)
        {
            if (visit[nodeId] == 2)
            {
                return;
            }

            if (visit[nodeId] == 1)
            {
                throw new ArgumentException(
                    $"Combinational cycle detected at net '{nodes[nodeId].Name}'.",
                    nameof(nodes));
            }

            visit[nodeId] = 1;
            var node = nodes[nodeId];
            if (node.Kind == NandNodeKind.Nand2)
            {
                Visit(node.LeftId!.Value);
                Visit(node.RightId!.Value);
            }

            visit[nodeId] = 2;
            order.Add(nodeId);
        }

        for (var nodeId = 0; nodeId < nodes.Count; nodeId++)
        {
            Visit(nodeId);
        }

        return [.. order];
    }

    private static NandStaticMetrics Measure(
        IReadOnlyList<NandNodeSpec> nodes,
        IReadOnlyList<NandOutputSpec> outputs,
        IReadOnlyList<DffBoundarySpec> dffs,
        IReadOnlyList<int> topologicalOrder)
    {
        var fanout = new int[nodes.Count];
        var depth = new int[nodes.Count];
        var nandInputPins = 0;
        var crossRegion = 0;
        var crossLane = 0;

        void AddSink(int sourceId, string sinkRegion)
        {
            fanout[sourceId]++;
            if (!string.Equals(nodes[sourceId].Region, sinkRegion, StringComparison.Ordinal))
            {
                crossRegion++;
            }

            var sourceLane = ExtractLane(nodes[sourceId].Region);
            var sinkLane = ExtractLane(sinkRegion);
            if (sourceLane is not null && sinkLane is not null &&
                !string.Equals(sourceLane, sinkLane, StringComparison.Ordinal))
            {
                crossLane++;
            }
        }

        foreach (var nodeId in topologicalOrder)
        {
            var node = nodes[nodeId];
            if (node.Kind != NandNodeKind.Nand2)
            {
                continue;
            }

            var left = node.LeftId!.Value;
            var right = node.RightId!.Value;
            AddSink(left, node.Region);
            AddSink(right, node.Region);
            nandInputPins += 2;
            depth[nodeId] = Math.Max(depth[left], depth[right]) + 1;
        }

        foreach (var output in outputs)
        {
            AddSink(output.NodeId, output.Region);
        }

        foreach (var dff in dffs)
        {
            AddSink(dff.DataNodeId, dff.Region);
        }

        var nandCount = nodes.Count(node => node.Kind == NandNodeKind.Nand2);
        var inputCount = nodes.Count(node => node.Kind == NandNodeKind.Input);
        var stateCount = nodes.Count(node => node.Kind == NandNodeKind.State);
        var totalSinks = fanout.Sum();
        return new NandStaticMetrics(
            Nand2Static: nandCount,
            DffStatic: dffs.Count,
            StateBits: stateCount,
            InputBits: inputCount,
            OutputBits: outputs.Count,
            PortBits: checked(inputCount + outputs.Count),
            WireBits: nodes.Count,
            NandInputPinConnections: nandInputPins,
            TotalNetSinks: totalSinks,
            ConnectionsStatic: totalSinks,
            MaximumFanout: fanout.Max(),
            UnitNandCriticalDepth: depth.Max(),
            CrossRegionConnections: crossRegion,
            CrossLaneConnections: crossLane,
            CombinationalLoopStatus: CombinationalLoopStatus.Acyclic);
    }

    private static string? ExtractLane(string region)
    {
        foreach (var component in region.Split('/'))
        {
            if (component.StartsWith("lane:", StringComparison.Ordinal) ||
                component.StartsWith("lane[", StringComparison.Ordinal))
            {
                return component;
            }
        }

        return null;
    }
}

public sealed class NandNetlistBuilder
{
    private readonly List<NandNodeSpec> _nodes = [];
    private readonly List<NandOutputSpec> _outputs = [];
    private readonly List<DffBoundarySpec> _dffs = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly HashSet<string> _outputNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dffNames = new(StringComparer.Ordinal);

    public NandNetlistBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }

    public NandSignal Input(string name, string region = "ports/input") =>
        AddSource(name, NandNodeKind.Input, region, null);

    public NandSignal Constant(
        string name,
        BitState value,
        string region = "configuration/constants") =>
        AddSource(name, NandNodeKind.Constant, region, value);

    public NandSignal State(
        string name,
        BitState initialValue = BitState.Off,
        string region = "state") =>
        AddSource(name, NandNodeKind.State, region, initialValue);

    public NandSignal Nand(
        string name,
        NandSignal left,
        NandSignal right,
        string region)
    {
        EnsureNewName(name);
        EnsureSignal(left);
        EnsureSignal(right);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        var id = _nodes.Count;
        _nodes.Add(new NandNodeSpec(
            id,
            name,
            NandNodeKind.Nand2,
            region,
            left.Id,
            right.Id));
        return new NandSignal(id, name);
    }

    public void Output(string name, NandSignal signal, string region = "ports/output")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        EnsureSignal(signal);
        if (!_outputNames.Add(name))
        {
            throw new ArgumentException($"Output '{name}' is duplicated.", nameof(name));
        }

        _outputs.Add(new NandOutputSpec(name, signal.Id, region));
    }

    public void Dff(
        string name,
        NandSignal data,
        NandSignal state,
        string region = "state")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        EnsureSignal(data);
        EnsureSignal(state);
        if (_nodes[state.Id].Kind != NandNodeKind.State)
        {
            throw new ArgumentException("A DFF Q net must be a state-boundary signal.", nameof(state));
        }

        if (!_dffNames.Add(name))
        {
            throw new ArgumentException($"DFF '{name}' is duplicated.", nameof(name));
        }

        _dffs.Add(new DffBoundarySpec(_dffs.Count, name, data.Id, state.Id, region));
    }

    public NandNetlist Build() => new(Name, _nodes, _outputs, _dffs);

    private NandSignal AddSource(
        string name,
        NandNodeKind kind,
        string region,
        BitState? initialValue)
    {
        EnsureNewName(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        if (initialValue.HasValue && initialValue.Value is not BitState.Off and not BitState.On)
        {
            throw new ArgumentOutOfRangeException(nameof(initialValue));
        }

        var id = _nodes.Count;
        _nodes.Add(new NandNodeSpec(id, name, kind, region, InitialValue: initialValue));
        return new NandSignal(id, name);
    }

    private void EnsureNewName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_names.Add(name))
        {
            throw new ArgumentException($"Net '{name}' already has a driver.", nameof(name));
        }
    }

    private void EnsureSignal(NandSignal signal)
    {
        if (signal.Id < 0 || signal.Id >= _nodes.Count ||
            !string.Equals(_nodes[signal.Id].Name, signal.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException("Signal does not belong to this builder.", nameof(signal));
        }
    }
}
