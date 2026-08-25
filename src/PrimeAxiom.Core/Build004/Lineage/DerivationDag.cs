using System.Numerics;

namespace PrimeAxiom.Core.Build004.Lineage;

public enum DerivationNodeKind
{
    Zero,
    One,
    Atom,
    Joint,
    Alternative,
    Transform,
}

public sealed record DerivationNodeSnapshot(
    string NodeId,
    DerivationNodeKind Kind,
    AtomDescriptor? Atom,
    string? TransformContractId,
    IReadOnlyList<string> ChildNodeIds);

public enum DagVerificationStatus
{
    Valid,
    MissingRoot,
    DuplicateNodeId,
    DanglingReference,
    CycleDetected,
    InvalidNodeShape,
    HashMismatch,
    RegistryMismatch,
    ConflictingAtomDescriptor,
    CachedSupportMismatch,
    CachedMultiplicityMismatch,
}

public sealed record DagVerificationResult(DagVerificationStatus Status, string Detail)
{
    public bool IsValid => Status == DagVerificationStatus.Valid;
}

public enum DagRetractionStatus
{
    Success,
    AtomNotPresent,
    NonInvertibleTransform,
}

public sealed record DagRetractionResult(
    DagRetractionStatus Status,
    string? RootNodeId,
    string Detail)
{
    public bool IsSuccess => Status == DagRetractionStatus.Success;
}

public sealed record DerivationDagMetrics(
    int NodeCount,
    int EdgeCount,
    int MaximumDepth,
    int HashConsReuseCount);

public sealed record DerivationDagDiagnostics(
    long MapReads,
    long MapWrites,
    long SemanticHashComputations,
    long DagNodeVisits,
    long ProjectionQueries,
    long CacheVerificationRequests,
    long CacheVerificationPasses)
{
    public const string CountingContract =
        "INSTANCE_COUNTERS__MAP_ACCESS_AT_INSTRUMENTED_DAG_DICTIONARY_BOUNDARIES__NODE_VISIT_AFTER_MEMO_MISS__ONE_HASH_PER_SEMANTIC_HASH_CALL";
}

public sealed class DerivationDag
{
    public const string Schema = "prime-axiom-derivation-node-v1";

    private readonly Dictionary<string, DerivationNodeSnapshot> nodes = new(StringComparer.Ordinal);
    private int hashConsReuseCount;
    private long mapReads;
    private long mapWrites;
    private long semanticHashComputations;
    private long dagNodeVisits;
    private long projectionQueries;
    private long cacheVerificationRequests;
    private long cacheVerificationPasses;

    public DerivationDag()
    {
        ZeroNodeId = AddCanonical(DerivationNodeKind.Zero, null, null, Array.Empty<string>());
        OneNodeId = AddCanonical(DerivationNodeKind.One, null, null, Array.Empty<string>());
    }

    public string ZeroNodeId { get; }
    public string OneNodeId { get; }
    public int NodeCount => nodes.Count;

    public DerivationDagDiagnostics Diagnostics => new(
        mapReads,
        mapWrites,
        semanticHashComputations,
        dagNodeVisits,
        projectionQueries,
        cacheVerificationRequests,
        cacheVerificationPasses);

    public void ResetDiagnostics()
    {
        mapReads = 0;
        mapWrites = 0;
        semanticHashComputations = 0;
        dagNodeVisits = 0;
        projectionQueries = 0;
        cacheVerificationRequests = 0;
        cacheVerificationPasses = 0;
    }

    public IReadOnlyList<DerivationNodeSnapshot> Snapshots =>
        Array.AsReadOnly(nodes.Values
            .OrderBy(snapshot => snapshot.NodeId, StringComparer.Ordinal)
            .Select(CloneSnapshot)
            .ToArray());

    public string AddAtom(AtomDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return AddCanonical(DerivationNodeKind.Atom, descriptor, null, Array.Empty<string>());
    }

    public string AddJoint(params string[] childNodeIds) => AddJoint((IEnumerable<string>)childNodeIds);

    public string AddJoint(IEnumerable<string> childNodeIds)
    {
        ArgumentNullException.ThrowIfNull(childNodeIds);
        var flattened = Flatten(DerivationNodeKind.Joint, childNodeIds, ZeroNodeId, OneNodeId);
        if (flattened.Annihilated)
        {
            return ZeroNodeId;
        }

        return flattened.Children.Count switch
        {
            0 => OneNodeId,
            1 => flattened.Children[0],
            _ => AddCanonical(DerivationNodeKind.Joint, null, null, flattened.Children),
        };
    }

    public string AddAlternative(params string[] childNodeIds) => AddAlternative((IEnumerable<string>)childNodeIds);

    public string AddAlternative(IEnumerable<string> childNodeIds)
    {
        ArgumentNullException.ThrowIfNull(childNodeIds);
        var flattened = Flatten(DerivationNodeKind.Alternative, childNodeIds, null, ZeroNodeId);
        return flattened.Children.Count switch
        {
            0 => ZeroNodeId,
            1 => flattened.Children[0],
            _ => AddCanonical(DerivationNodeKind.Alternative, null, null, flattened.Children),
        };
    }

    public string AddTransform(string transformContractId, params string[] childNodeIds)
    {
        transformContractId = LineageText.RequireToken(transformContractId, nameof(transformContractId));
        ArgumentNullException.ThrowIfNull(childNodeIds);
        var children = childNodeIds.ToArray();
        if (children.Length == 0)
        {
            throw new ArgumentException("A transform must cite at least one input node.", nameof(childNodeIds));
        }

        EnsureChildrenExist(children);
        return AddCanonical(
            DerivationNodeKind.Transform,
            null,
            transformContractId,
            children);
    }

    public SupportProjection ProjectSupport(
        string rootNodeId,
        LineageRegistry registry,
        LineageCompleteness completeness = LineageCompleteness.Exact,
        PayloadReplayability payloadReplayability = PayloadReplayability.DigestOnly)
    {
        ArgumentNullException.ThrowIfNull(registry);
        projectionQueries++;
        var atoms = ProjectSupportCore(rootNodeId, nodes, this);
        return SupportProjection.Create(registry, atoms, completeness, payloadReplayability);
    }

    public MultiplicityProjection ProjectMultiplicity(
        string rootNodeId,
        LineageRegistry registry,
        LineageCompleteness completeness = LineageCompleteness.Exact,
        PayloadReplayability payloadReplayability = PayloadReplayability.DigestOnly)
    {
        ArgumentNullException.ThrowIfNull(registry);
        projectionQueries++;
        var counts = ProjectMultiplicityCore(rootNodeId, nodes, this);
        return MultiplicityProjection.Create(
            registry,
            counts.Select(pair => new AtomMultiplicity(pair.Key, pair.Value)),
            completeness,
            payloadReplayability);
    }

    public DagRetractionResult SpecializeAtomToZero(string rootNodeId, AtomKey atom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootNodeId);
        ArgumentNullException.ThrowIfNull(atom);
        EnsureChildrenExist(new[] { rootNodeId });
        if (!ContainsAtom(rootNodeId, atom, new Dictionary<string, bool>(StringComparer.Ordinal)))
        {
            return new DagRetractionResult(
                DagRetractionStatus.AtomNotPresent,
                rootNodeId,
                "The requested atom is not present in this derivation.");
        }

        var memo = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var root = Specialize(rootNodeId, atom, memo);
            return new DagRetractionResult(
                DagRetractionStatus.Success,
                root,
                "Retraction was evaluated as positive-provenance specialization of the atom to ZERO.");
        }
        catch (NonInvertibleTransformException exception)
        {
            return new DagRetractionResult(
                DagRetractionStatus.NonInvertibleTransform,
                null,
                exception.Message);
        }
    }

    public BigInteger EvaluatePositive(
        string rootNodeId,
        IReadOnlyDictionary<AtomKey, BigInteger> atomValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootNodeId);
        ArgumentNullException.ThrowIfNull(atomValues);
        EnsureChildrenExist(new[] { rootNodeId });
        if (atomValues.Values.Any(value => value < BigInteger.Zero))
        {
            throw new ArgumentException("Positive provenance evaluation requires nonnegative atom values.", nameof(atomValues));
        }

        var memo = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
        BigInteger Visit(string nodeId)
        {
            if (memo.TryGetValue(nodeId, out var known))
            {
                return known;
            }

            var node = nodes[nodeId];
            var result = node.Kind switch
            {
                DerivationNodeKind.Zero => BigInteger.Zero,
                DerivationNodeKind.One => BigInteger.One,
                DerivationNodeKind.Atom when node.Atom is not null =>
                    atomValues.TryGetValue(node.Atom.Key, out var value)
                        ? value
                        : throw new KeyNotFoundException($"No evaluation value was supplied for {node.Atom.Key}."),
                DerivationNodeKind.Joint => node.ChildNodeIds.Aggregate(
                    BigInteger.One,
                    (product, child) => product * Visit(child)),
                DerivationNodeKind.Alternative => node.ChildNodeIds.Aggregate(
                    BigInteger.Zero,
                    (sum, child) => sum + Visit(child)),
                DerivationNodeKind.Transform => throw new InvalidOperationException(
                    $"Transform {node.TransformContractId} has no positive-semiring evaluator."),
                _ => throw new InvalidOperationException("Unknown derivation node kind."),
            };
            memo[nodeId] = result;
            return result;
        }

        return Visit(rootNodeId);
    }

    public DagVerificationResult Verify(
        string rootNodeId,
        LineageRegistry registry,
        SupportProjection? cachedSupport = null,
        MultiplicityProjection? cachedMultiplicity = null)
    {
        projectionQueries = checked(projectionQueries + 2);
        var requestedCaches = (cachedSupport is null ? 0 : 1) + (cachedMultiplicity is null ? 0 : 1);
        cacheVerificationRequests = checked(cacheVerificationRequests + requestedCaches);
        var result = VerifySnapshotsCore(
            Snapshots,
            rootNodeId,
            registry,
            cachedSupport,
            cachedMultiplicity,
            this);
        if (result.IsValid)
        {
            cacheVerificationPasses = checked(cacheVerificationPasses + requestedCaches);
        }

        return result;
    }

    public DerivationDagMetrics GetMetrics(string rootNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootNodeId);
        EnsureChildrenExist(new[] { rootNodeId });
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        var edgeCount = 0;
        int Visit(string nodeId)
        {
            if (depths.TryGetValue(nodeId, out var known))
            {
                return known;
            }

            reachable.Add(nodeId);
            var node = nodes[nodeId];
            edgeCount = checked(edgeCount + node.ChildNodeIds.Count);
            var depth = node.ChildNodeIds.Count == 0
                ? 1
                : checked(1 + node.ChildNodeIds.Max(Visit));
            depths[nodeId] = depth;
            return depth;
        }

        var maximumDepth = Visit(rootNodeId);
        return new DerivationDagMetrics(reachable.Count, edgeCount, maximumDepth, hashConsReuseCount);
    }

    public IReadOnlyList<DerivationNodeSnapshot> GetReachableSnapshots(string rootNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootNodeId);
        if (!nodes.ContainsKey(rootNodeId))
        {
            throw new KeyNotFoundException($"The declared DAG root {rootNodeId} does not exist.");
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(rootNodeId);
        while (pending.Count > 0)
        {
            var nodeId = pending.Pop();
            if (!reachable.Add(nodeId))
            {
                continue;
            }

            if (!nodes.TryGetValue(nodeId, out var node))
            {
                throw new KeyNotFoundException($"The DAG contains a dangling reference to {nodeId}.");
            }

            foreach (var child in node.ChildNodeIds)
            {
                pending.Push(child);
            }
        }

        return Array.AsReadOnly(reachable
            .Order(StringComparer.Ordinal)
            .Select(nodeId => CloneSnapshot(nodes[nodeId]))
            .ToArray());
    }

    public static DagVerificationResult VerifySnapshots(
        IEnumerable<DerivationNodeSnapshot> snapshots,
        string rootNodeId,
        LineageRegistry registry,
        SupportProjection? cachedSupport = null,
        MultiplicityProjection? cachedMultiplicity = null) =>
        VerifySnapshotsCore(snapshots, rootNodeId, registry, cachedSupport, cachedMultiplicity, null);

    private static DagVerificationResult VerifySnapshotsCore(
        IEnumerable<DerivationNodeSnapshot> snapshots,
        string rootNodeId,
        LineageRegistry registry,
        SupportProjection? cachedSupport,
        MultiplicityProjection? cachedMultiplicity,
        DerivationDag? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootNodeId);
        ArgumentNullException.ThrowIfNull(registry);
        var frozen = snapshots.Select(CloneSnapshot).ToArray();
        if (frozen.GroupBy(snapshot => snapshot.NodeId, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            return new DagVerificationResult(DagVerificationStatus.DuplicateNodeId, "A node ID occurs more than once.");
        }

        var lookup = frozen.ToDictionary(snapshot => snapshot.NodeId, StringComparer.Ordinal);
        if (diagnostics is not null)
        {
            diagnostics.mapWrites = checked(diagnostics.mapWrites + frozen.LongLength);
            diagnostics.mapReads++;
        }
        if (!lookup.ContainsKey(rootNodeId))
        {
            return new DagVerificationResult(DagVerificationStatus.MissingRoot, "The declared root is absent.");
        }

        var colors = new Dictionary<string, int>(StringComparer.Ordinal);
        DagVerificationResult? Walk(string nodeId)
        {
            if (diagnostics is not null)
            {
                diagnostics.mapReads = checked(diagnostics.mapReads + 2);
            }
            if (!lookup.TryGetValue(nodeId, out var node))
            {
                return new DagVerificationResult(DagVerificationStatus.DanglingReference, $"Missing child {nodeId}.");
            }

            if (colors.TryGetValue(nodeId, out var color))
            {
                return color == 1
                    ? new DagVerificationResult(DagVerificationStatus.CycleDetected, $"Dependency cycle reaches {nodeId}.")
                    : null;
            }

            if (diagnostics is not null)
            {
                diagnostics.dagNodeVisits++;
                diagnostics.mapWrites++;
            }
            colors[nodeId] = 1;
            foreach (var child in node.ChildNodeIds)
            {
                var result = Walk(child);
                if (result is not null)
                {
                    return result;
                }
            }

            if (diagnostics is not null)
            {
                diagnostics.mapWrites++;
            }
            colors[nodeId] = 2;
            return null;
        }

        var graphResult = Walk(rootNodeId);
        if (graphResult is not null)
        {
            return graphResult;
        }

        var reachableNodes = new List<DerivationNodeSnapshot>(colors.Count);
        foreach (var nodeId in colors.Keys)
        {
            if (diagnostics is not null)
            {
                diagnostics.mapReads++;
            }

            reachableNodes.Add(lookup[nodeId]);
        }

        foreach (var node in reachableNodes)
        {
            if (!ShapeIsValid(node))
            {
                return new DagVerificationResult(DagVerificationStatus.InvalidNodeShape, $"Node {node.NodeId} violates its kind contract.");
            }
        }

        foreach (var node in reachableNodes)
        {
            if (diagnostics is not null)
            {
                diagnostics.semanticHashComputations++;
            }
            if (!string.Equals(node.NodeId, ComputeNodeId(node), StringComparison.Ordinal))
            {
                return new DagVerificationResult(DagVerificationStatus.HashMismatch, $"Node {node.NodeId} does not match its semantic hash.");
            }
        }

        foreach (var node in reachableNodes)
        {
            if (!CanonicalCompositeShapeIsValid(node, lookup, diagnostics))
            {
                return new DagVerificationResult(
                    DagVerificationStatus.InvalidNodeShape,
                    $"Node {node.NodeId} is not in the canonical normal form produced by live DAG construction.");
            }
        }

        var descriptorByKey = new Dictionary<AtomKey, AtomDescriptor>();
        foreach (var node in reachableNodes.Where(candidate => candidate.Kind == DerivationNodeKind.Atom))
        {
            var descriptor = node.Atom!;
            if (diagnostics is not null)
            {
                diagnostics.mapReads++;
            }

            if (descriptorByKey.TryGetValue(descriptor.Key, out var existingDescriptor))
            {
                if (existingDescriptor != descriptor)
                {
                    return new DagVerificationResult(
                        DagVerificationStatus.ConflictingAtomDescriptor,
                        $"Reachable atom key {descriptor.Key} names conflicting source or payload-digest content.");
                }
            }
            else
            {
                if (diagnostics is not null)
                {
                    diagnostics.mapWrites++;
                }

                descriptorByKey.Add(descriptor.Key, descriptor);
            }
        }

        IReadOnlyList<AtomKey> support;
        Dictionary<AtomKey, int> multiplicity;
        try
        {
            support = ProjectSupportCore(rootNodeId, lookup, diagnostics);
            multiplicity = ProjectMultiplicityCore(rootNodeId, lookup, diagnostics);
            if (support.Any(atom => !registry.Contains(atom)))
            {
                return new DagVerificationResult(DagVerificationStatus.RegistryMismatch, "The DAG contains an atom outside the declared registry.");
            }
        }
        catch (KeyNotFoundException exception)
        {
            return new DagVerificationResult(DagVerificationStatus.DanglingReference, exception.Message);
        }

        if (cachedSupport is not null)
        {
            if (!string.Equals(cachedSupport.RegistryId, registry.RegistryId, StringComparison.Ordinal) ||
                cachedSupport.Completeness != LineageCompleteness.Exact ||
                cachedSupport.PayloadReplayability != PayloadReplayability.DigestOnly ||
                !cachedSupport.Atoms.SequenceEqual(support.OrderBy(atom => atom, AtomKeyOrdering.Comparer)))
            {
                return new DagVerificationResult(DagVerificationStatus.CachedSupportMismatch, "The cached support differs from DAG replay.");
            }
        }

        if (cachedMultiplicity is not null)
        {
            var expected = multiplicity
                .OrderBy(pair => pair.Key, AtomKeyOrdering.Comparer)
                .Select(pair => new AtomMultiplicity(pair.Key, pair.Value));
            if (!string.Equals(cachedMultiplicity.RegistryId, registry.RegistryId, StringComparison.Ordinal) ||
                cachedMultiplicity.Completeness != LineageCompleteness.Exact ||
                cachedMultiplicity.PayloadReplayability != PayloadReplayability.DigestOnly ||
                !cachedMultiplicity.Entries.SequenceEqual(expected))
            {
                return new DagVerificationResult(DagVerificationStatus.CachedMultiplicityMismatch, "The cached multiplicity differs from DAG replay.");
            }
        }

        return new DagVerificationResult(
            DagVerificationStatus.Valid,
            "All reachable node shapes, canonical normal forms, references, hashes, and requested DigestOnly structural projection caches replayed exactly. This does not establish payload availability or issuer authentication.");
    }

    internal static string ComputeNodeId(DerivationNodeSnapshot node) =>
        LineageHash.Sha256(LineageText.Fields(
            Schema,
            node.Kind.ToString(),
            node.Atom?.Canonical ?? string.Empty,
            node.TransformContractId ?? string.Empty,
            string.Concat(node.ChildNodeIds.Select(child => LineageText.Fields(child)))));

    private static DerivationNodeSnapshot CloneSnapshot(DerivationNodeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new DerivationNodeSnapshot(
            snapshot.NodeId,
            snapshot.Kind,
            snapshot.Atom,
            snapshot.TransformContractId,
            Array.AsReadOnly(snapshot.ChildNodeIds.ToArray()));
    }

    private static bool ShapeIsValid(DerivationNodeSnapshot node)
    {
        if (string.IsNullOrWhiteSpace(node.NodeId) || node.ChildNodeIds is null)
        {
            return false;
        }

        return node.Kind switch
        {
            DerivationNodeKind.Zero or DerivationNodeKind.One =>
                node.Atom is null && node.TransformContractId is null && node.ChildNodeIds.Count == 0,
            DerivationNodeKind.Atom =>
                node.Atom is not null && node.TransformContractId is null && node.ChildNodeIds.Count == 0,
            DerivationNodeKind.Joint or DerivationNodeKind.Alternative =>
                node.Atom is null && node.TransformContractId is null && node.ChildNodeIds.Count >= 2 &&
                node.ChildNodeIds.SequenceEqual(node.ChildNodeIds.OrderBy(child => child, StringComparer.Ordinal)),
            DerivationNodeKind.Transform =>
                node.Atom is null && !string.IsNullOrWhiteSpace(node.TransformContractId) && node.ChildNodeIds.Count > 0,
            _ => false,
        };
    }

    private static bool CanonicalCompositeShapeIsValid(
        DerivationNodeSnapshot node,
        IReadOnlyDictionary<string, DerivationNodeSnapshot> lookup,
        DerivationDag? diagnostics)
    {
        if (node.Kind is not (DerivationNodeKind.Joint or DerivationNodeKind.Alternative))
        {
            return true;
        }

        foreach (var childNodeId in node.ChildNodeIds)
        {
            if (diagnostics is not null)
            {
                diagnostics.mapReads++;
            }

            var childKind = lookup[childNodeId].Kind;
            if (childKind == node.Kind ||
                (node.Kind == DerivationNodeKind.Joint &&
                    childKind is DerivationNodeKind.Zero or DerivationNodeKind.One) ||
                (node.Kind == DerivationNodeKind.Alternative && childKind == DerivationNodeKind.Zero))
            {
                return false;
            }
        }

        return true;
    }

    private static AtomKey[] ProjectSupportCore(
        string rootNodeId,
        Dictionary<string, DerivationNodeSnapshot> lookup,
        DerivationDag? diagnostics = null)
    {
        var memo = new Dictionary<string, HashSet<AtomKey>>(StringComparer.Ordinal);
        HashSet<AtomKey> Visit(string nodeId)
        {
            if (diagnostics is not null)
            {
                diagnostics.mapReads++;
            }
            if (memo.TryGetValue(nodeId, out var known))
            {
                return known;
            }

            if (diagnostics is not null)
            {
                diagnostics.mapReads++;
                diagnostics.dagNodeVisits++;
            }
            var node = lookup.TryGetValue(nodeId, out var found)
                ? found
                : throw new KeyNotFoundException($"Missing child {nodeId}.");
            var atoms = new HashSet<AtomKey>();
            if (node.Kind == DerivationNodeKind.Atom && node.Atom is not null)
            {
                atoms.Add(node.Atom.Key);
            }

            foreach (var child in node.ChildNodeIds)
            {
                atoms.UnionWith(Visit(child));
            }

            if (diagnostics is not null)
            {
                diagnostics.mapWrites++;
            }
            memo[nodeId] = atoms;
            return atoms;
        }

        return Visit(rootNodeId).OrderBy(atom => atom, AtomKeyOrdering.Comparer).ToArray();
    }

    private static Dictionary<AtomKey, int> ProjectMultiplicityCore(
        string rootNodeId,
        Dictionary<string, DerivationNodeSnapshot> lookup,
        DerivationDag? diagnostics = null)
    {
        var memo = new Dictionary<string, Dictionary<AtomKey, int>>(StringComparer.Ordinal);
        Dictionary<AtomKey, int> Visit(string nodeId)
        {
            if (diagnostics is not null)
            {
                diagnostics.mapReads++;
            }
            if (memo.TryGetValue(nodeId, out var known))
            {
                return known;
            }

            if (diagnostics is not null)
            {
                diagnostics.mapReads++;
                diagnostics.dagNodeVisits++;
            }
            var node = lookup.TryGetValue(nodeId, out var found)
                ? found
                : throw new KeyNotFoundException($"Missing child {nodeId}.");
            var counts = new Dictionary<AtomKey, int>();
            if (node.Kind == DerivationNodeKind.Atom && node.Atom is not null)
            {
                if (diagnostics is not null)
                {
                    diagnostics.mapWrites++;
                }
                counts[node.Atom.Key] = 1;
            }

            foreach (var child in node.ChildNodeIds)
            {
                foreach (var pair in Visit(child))
                {
                    if (diagnostics is not null)
                    {
                        diagnostics.mapReads++;
                        diagnostics.mapWrites++;
                    }
                    counts.TryGetValue(pair.Key, out var previous);
                    counts[pair.Key] = checked(previous + pair.Value);
                }
            }

            if (diagnostics is not null)
            {
                diagnostics.mapWrites++;
            }
            memo[nodeId] = counts;
            return counts;
        }

        return Visit(rootNodeId);
    }

    private string Specialize(
        string nodeId,
        AtomKey atom,
        Dictionary<string, string> memo)
    {
        if (memo.TryGetValue(nodeId, out var known))
        {
            return known;
        }

        var node = nodes[nodeId];
        string result;
        switch (node.Kind)
        {
            case DerivationNodeKind.Zero:
            case DerivationNodeKind.One:
                result = nodeId;
                break;
            case DerivationNodeKind.Atom:
                result = node.Atom?.Key == atom ? ZeroNodeId : nodeId;
                break;
            case DerivationNodeKind.Joint:
                result = AddJoint(node.ChildNodeIds.Select(child => Specialize(child, atom, memo)));
                break;
            case DerivationNodeKind.Alternative:
                result = AddAlternative(node.ChildNodeIds.Select(child => Specialize(child, atom, memo)));
                break;
            case DerivationNodeKind.Transform:
                if (ContainsAtom(nodeId, atom, new Dictionary<string, bool>(StringComparer.Ordinal)))
                {
                    throw new NonInvertibleTransformException(
                        $"Transform {node.TransformContractId} has no registered specialization/replay contract.");
                }

                result = nodeId;
                break;
            default:
                throw new InvalidOperationException("Unknown derivation node kind.");
        }

        memo[nodeId] = result;
        return result;
    }

    private bool ContainsAtom(
        string nodeId,
        AtomKey atom,
        Dictionary<string, bool> memo)
    {
        if (memo.TryGetValue(nodeId, out var known))
        {
            return known;
        }

        var node = nodes[nodeId];
        var contains = (node.Kind == DerivationNodeKind.Atom && node.Atom?.Key == atom) ||
            node.ChildNodeIds.Any(child => ContainsAtom(child, atom, memo));
        memo[nodeId] = contains;
        return contains;
    }

    private (bool Annihilated, IReadOnlyList<string> Children) Flatten(
        DerivationNodeKind kind,
        IEnumerable<string> childNodeIds,
        string? annihilator,
        string identity)
    {
        var pending = childNodeIds.ToArray();
        EnsureChildrenExist(pending);
        var flattened = new List<string>();
        foreach (var childId in pending)
        {
            if (annihilator is not null && string.Equals(childId, annihilator, StringComparison.Ordinal))
            {
                return (true, Array.Empty<string>());
            }

            if (string.Equals(childId, identity, StringComparison.Ordinal))
            {
                continue;
            }

            mapReads++;
            var child = nodes[childId];
            if (child.Kind == kind)
            {
                flattened.AddRange(child.ChildNodeIds);
            }
            else
            {
                flattened.Add(childId);
            }
        }

        flattened.Sort(StringComparer.Ordinal);
        return (false, Array.AsReadOnly(flattened.ToArray()));
    }

    private string AddCanonical(
        DerivationNodeKind kind,
        AtomDescriptor? atom,
        string? transformContractId,
        IReadOnlyList<string> childNodeIds)
    {
        var frozenChildren = Array.AsReadOnly(childNodeIds.ToArray());
        var provisional = new DerivationNodeSnapshot(string.Empty, kind, atom, transformContractId, frozenChildren);
        semanticHashComputations++;
        var nodeId = ComputeNodeId(provisional);
        var node = provisional with { NodeId = nodeId };
        mapReads++;
        if (nodes.TryGetValue(nodeId, out var existing))
        {
            semanticHashComputations++;
            if (!string.Equals(ComputeNodeId(existing), nodeId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The stored derivation node no longer matches its semantic hash.");
            }

            if (!CanonicalSemanticsEqual(existing, node))
            {
                throw new InvalidOperationException(
                    "A semantic hash collision between different canonical derivation nodes was detected.");
            }

            hashConsReuseCount++;
            return nodeId;
        }

        mapWrites++;
        nodes.Add(nodeId, node);
        return nodeId;
    }

    private static bool CanonicalSemanticsEqual(
        DerivationNodeSnapshot left,
        DerivationNodeSnapshot right) =>
        left.Kind == right.Kind &&
        Equals(left.Atom, right.Atom) &&
        string.Equals(left.TransformContractId, right.TransformContractId, StringComparison.Ordinal) &&
        left.ChildNodeIds.SequenceEqual(right.ChildNodeIds, StringComparer.Ordinal);

    private void EnsureChildrenExist(IEnumerable<string> childNodeIds)
    {
        foreach (var child in childNodeIds)
        {
            mapReads++;
            if (string.IsNullOrWhiteSpace(child) || !nodes.ContainsKey(child))
            {
                throw new ArgumentException($"Unknown derivation child {child}.", nameof(childNodeIds));
            }
        }
    }

    private sealed class NonInvertibleTransformException : Exception
    {
        public NonInvertibleTransformException(string message)
            : base(message)
        {
        }
    }
}
