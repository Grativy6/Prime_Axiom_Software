namespace PrimeAxiom.Core.Build004.Lineage;

public sealed record FusionAtomEvidence
{
    public FusionAtomEvidence(
        AtomDescriptor descriptor,
        TwoStateLikelihood likelihood)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Likelihood = likelihood ?? throw new ArgumentNullException(nameof(likelihood));
    }

    private FusionAtomEvidence(AtomDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Likelihood = null;
    }

    public static FusionAtomEvidence DigestOnly(AtomDescriptor descriptor) => new(descriptor);

    public AtomDescriptor Descriptor { get; }
    public TwoStateLikelihood? Likelihood { get; }
    public bool PayloadReplayable => Likelihood is not null;

    internal string Canonical => LineageText.Fields(
        Descriptor.Canonical,
        Likelihood?.Canonical ?? "LIKELIHOOD_NOT_RETAINED",
        PayloadReplayable ? "PAYLOAD_REPLAYABLE" : "PAYLOAD_NOT_REPLAYABLE");
}

public enum ExactLineageFusionStatus
{
    Success,
    RegistryEpochMismatch,
    PartialLineage,
    ConflictingAtomPayload,
    OverlapPayloadUnavailable,
    AtomNotPresent,
    MissingRetractionWitness,
    InternalOracleMismatch,
    AuthenticationNotProvided,
    ExternalAuthenticationUnverified,
}

public sealed record ExactLineageFusionResult(
    ExactLineageFusionStatus Status,
    ExactLineageFusionState? State,
    SupportProjection? ExactOverlap,
    string Detail)
{
    public bool IsSuccess => Status == ExactLineageFusionStatus.Success;
}

public sealed class ExactLineageFusionState
{
    private readonly IReadOnlyList<FusionAtomEvidence> atoms;

    internal ExactLineageFusionState(
        LineageFusionEngine owner,
        string receiptId,
        string expressionRootId,
        SupportProjection support,
        MultiplicityProjection multiplicity,
        TwoStateLikelihood payload,
        IEnumerable<FusionAtomEvidence> atoms,
        LineageCompleteness lineageCompleteness,
        PayloadReplayability payloadReplayability,
        IssuerAuthenticity issuerAuthenticity,
        IEnumerable<string> parentReceiptIds)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(support);
        ArgumentNullException.ThrowIfNull(multiplicity);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(atoms);
        ArgumentNullException.ThrowIfNull(parentReceiptIds);
        if (support.Completeness != lineageCompleteness ||
            multiplicity.Completeness != lineageCompleteness)
        {
            throw new ArgumentException("Projection completeness must match the enclosing fusion-state axis.");
        }

        if (support.PayloadReplayability != payloadReplayability ||
            multiplicity.PayloadReplayability != payloadReplayability)
        {
            throw new ArgumentException("Projection replayability must match the enclosing fusion-state axis.");
        }

        Owner = owner;
        ReceiptId = receiptId;
        ExpressionRootId = expressionRootId;
        Support = support;
        Multiplicity = multiplicity;
        Payload = payload;
        this.atoms = Array.AsReadOnly(atoms
            .OrderBy(atom => atom.Descriptor.Key, AtomKeyOrdering.Comparer)
            .ToArray());
        LineageCompleteness = lineageCompleteness;
        PayloadReplayability = payloadReplayability;
        IssuerAuthenticity = issuerAuthenticity;
        ParentReceiptIds = Array.AsReadOnly(parentReceiptIds.ToArray());
    }

    public string ReceiptId { get; }
    public string ExpressionRootId { get; }
    public SupportProjection Support { get; }
    public MultiplicityProjection Multiplicity { get; }
    public TwoStateLikelihood Payload { get; }
    public IReadOnlyList<FusionAtomEvidence> Atoms => atoms;
    public LineageCompleteness LineageCompleteness { get; }
    public PayloadReplayability PayloadReplayability { get; }
    public IssuerAuthenticity IssuerAuthenticity { get; }
    public IReadOnlyList<string> ParentReceiptIds { get; }

    internal LineageFusionEngine Owner { get; }
}

public sealed class LineageFusionEngine
{
    public const string ProtocolId = "PAS-BUILD004-EXACT-LINEAGE-0001";
    public const string CryptographicStatus = "NOT_CRYPTOGRAPHIC";
    public const string PrivacyStatus = "NO_PRIVACY";
    public const string SecurityBoundary =
        "Semantic SHA-256 IDs provide replayable integrity evidence only. No signature, issuer authentication, privacy, encryption, zero knowledge, or accumulator security is provided.";

    public LineageFusionEngine(LineageRegistry registry)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Dag = new DerivationDag();
    }

    public LineageRegistry Registry { get; }
    public DerivationDag Dag { get; }

    public ExactLineageFusionState CreateState(
        IEnumerable<FusionAtomEvidence> atoms,
        LineageCompleteness completeness = LineageCompleteness.Exact,
        IssuerAuthenticity authenticity = IssuerAuthenticity.NotProvided,
        TwoStateLikelihood? retainedAggregatePayload = null)
    {
        ArgumentNullException.ThrowIfNull(atoms);
        var frozen = atoms.ToArray();
        if (frozen.Any(atom => atom is null))
        {
            throw new ArgumentException("A fusion state cannot contain null atom evidence.", nameof(atoms));
        }

        ValidateRegistered(frozen);
        var duplicate = frozen.GroupBy(atom => atom.Descriptor.Key).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"A state must contain each unique observation at most once: {duplicate.Key}.",
                nameof(atoms));
        }

        var atomNodeIds = frozen.Select(atom => Dag.AddAtom(atom.Descriptor)).ToArray();
        var root = Dag.AddJoint(atomNodeIds);
        var replayability = frozen.All(atom => atom.PayloadReplayable)
            ? PayloadReplayability.ReplayableExact
            : PayloadReplayability.DigestOnly;
        var support = Dag.ProjectSupport(root, Registry, completeness, replayability);
        var multiplicity = Dag.ProjectMultiplicity(root, Registry, completeness, replayability);
        TwoStateLikelihood payload;
        if (replayability == PayloadReplayability.ReplayableExact)
        {
            var replayed = ComputeCentralizedUniqueAtomOracle(frozen);
            if (retainedAggregatePayload is not null && retainedAggregatePayload != replayed)
            {
                throw new ArgumentException(
                    "The retained aggregate payload disagrees with replayable per-atom likelihoods.",
                    nameof(retainedAggregatePayload));
            }

            payload = retainedAggregatePayload ?? replayed;
        }
        else
        {
            payload = retainedAggregatePayload ?? throw new ArgumentException(
                "A digest-only atom requires an explicit retained aggregate payload.",
                nameof(retainedAggregatePayload));
        }
        return CreateStateCore(
            root,
            support,
            multiplicity,
            payload,
            frozen,
            completeness,
            replayability,
            authenticity,
            Array.Empty<string>());
    }

    public ExactLineageFusionResult MergeUnique(
        ExactLineageFusionState left,
        ExactLineageFusionState right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (!ReferenceEquals(left.Owner, this) || !ReferenceEquals(right.Owner, this) ||
            !string.Equals(left.Support.RegistryId, Registry.RegistryId, StringComparison.Ordinal) ||
            !string.Equals(right.Support.RegistryId, Registry.RegistryId, StringComparison.Ordinal))
        {
            return Failure(
                ExactLineageFusionStatus.RegistryEpochMismatch,
                "Fusion states from different registry namespaces, epochs, or engines require an explicit migration.");
        }

        if (left.LineageCompleteness != LineageCompleteness.Exact ||
            right.LineageCompleteness != LineageCompleteness.Exact)
        {
            return Failure(
                ExactLineageFusionStatus.PartialLineage,
                "Known support is only a lower bound; exact total overlap is not earned.");
        }

        var leftByKey = left.Atoms.ToDictionary(atom => atom.Descriptor.Key);
        var rightByKey = right.Atoms.ToDictionary(atom => atom.Descriptor.Key);
        var overlapKeys = leftByKey.Keys.Intersect(rightByKey.Keys).ToArray();
        var overlapEvidence = new List<FusionAtomEvidence>(overlapKeys.Length);
        foreach (var key in overlapKeys)
        {
            var leftAtom = leftByKey[key];
            var rightAtom = rightByKey[key];
            if (leftAtom.Descriptor != rightAtom.Descriptor ||
                (leftAtom.Likelihood is not null && rightAtom.Likelihood is not null &&
                 leftAtom.Likelihood != rightAtom.Likelihood))
            {
                var conflictingOverlap = SupportProjection.Create(
                    Registry,
                    overlapKeys,
                    LineageCompleteness.Exact,
                    PayloadReplayability.DigestOnly);
                return Failure(
                    ExactLineageFusionStatus.ConflictingAtomPayload,
                    $"Observation {key} was reused with conflicting source, digest, or likelihood content.",
                    conflictingOverlap);
            }

            var replayableAtom = leftAtom.PayloadReplayable
                ? leftAtom
                : rightAtom.PayloadReplayable
                    ? rightAtom
                    : null;
            if (replayableAtom is null)
            {
                var unavailableOverlap = SupportProjection.Create(
                    Registry,
                    overlapKeys,
                    LineageCompleteness.Exact,
                    PayloadReplayability.DigestOnly);
                return Failure(
                    ExactLineageFusionStatus.OverlapPayloadUnavailable,
                    $"Overlap {key} was identified, but neither state retains its exact shared likelihood.",
                    unavailableOverlap);
            }

            overlapEvidence.Add(replayableAtom);
        }

        var overlap = SupportProjection.Create(
            Registry,
            overlapKeys,
            LineageCompleteness.Exact,
            PayloadReplayability.ReplayableExact);
        var unionByKey = left.Atoms.ToDictionary(atom => atom.Descriptor.Key);
        foreach (var rightAtom in right.Atoms)
        {
            if (!unionByKey.TryGetValue(rightAtom.Descriptor.Key, out var leftAtom) ||
                (!leftAtom.PayloadReplayable && rightAtom.PayloadReplayable))
            {
                unionByKey[rightAtom.Descriptor.Key] = rightAtom;
            }
        }

        var union = unionByKey.Values.ToArray();
        var overlapPayload = ComputeCentralizedUniqueAtomOracle(overlapEvidence);
        var payload = (left.Payload * right.Payload) / overlapPayload;
        var replayableUnion = union.All(atom => atom.PayloadReplayable);
        if (replayableUnion && payload != ComputeCentralizedUniqueAtomOracle(union))
        {
            return Failure(
                ExactLineageFusionStatus.InternalOracleMismatch,
                "The pairwise deduplication result disagreed with the centralized unique-atom oracle.",
                overlap);
        }

        var root = Dag.AddJoint(union.Select(atom => Dag.AddAtom(atom.Descriptor)));
        var replayability = union.All(atom => atom.PayloadReplayable)
            ? PayloadReplayability.ReplayableExact
            : PayloadReplayability.DigestOnly;
        var support = Dag.ProjectSupport(root, Registry, LineageCompleteness.Exact, replayability);
        var multiplicity = Dag.ProjectMultiplicity(root, Registry, LineageCompleteness.Exact, replayability);
        var state = CreateStateCore(
            root,
            support,
            multiplicity,
            payload,
            union,
            LineageCompleteness.Exact,
            replayability,
            IssuerAuthenticity.NotProvided,
            new[] { left.ReceiptId, right.ReceiptId });
        return new ExactLineageFusionResult(
            ExactLineageFusionStatus.Success,
            state,
            overlap,
            replayableUnion
                ? "Exact unique-source fusion matched the centralized replay oracle. The lineage receipt remains unauthenticated."
                : "Exact unique-source algebra used retained aggregate states and replayable overlap witnesses; at least one unique leaf remains digest-only, so an external oracle is required to validate the aggregate. The lineage receipt remains unauthenticated.");
    }

    public ExactLineageFusionResult RequireVerifiedIssuerAuthentication(ExactLineageFusionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ReferenceEquals(state.Owner, this))
        {
            return Failure(
                ExactLineageFusionStatus.RegistryEpochMismatch,
                "Authentication requirements can be evaluated only by the state-owning registry engine.");
        }

        return state.IssuerAuthenticity switch
        {
            IssuerAuthenticity.NotProvided => Failure(
                ExactLineageFusionStatus.AuthenticationNotProvided,
                "Exact arithmetic and replay do not provide issuer authentication."),
            IssuerAuthenticity.ExternalClaimNotVerified => Failure(
                ExactLineageFusionStatus.ExternalAuthenticationUnverified,
                "An external authentication claim is present but has not been verified by this system."),
            _ => throw new InvalidOperationException("Unknown issuer-authenticity state."),
        };
    }

    public ExactLineageFusionResult RetractExactProduct(
        ExactLineageFusionState state,
        AtomKey atom)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(atom);
        if (!ReferenceEquals(state.Owner, this))
        {
            return Failure(
                ExactLineageFusionStatus.RegistryEpochMismatch,
                "The state belongs to another registry epoch or fusion engine.");
        }

        if (state.LineageCompleteness != LineageCompleteness.Exact)
        {
            return Failure(
                ExactLineageFusionStatus.PartialLineage,
                "An exact product retraction cannot be earned from lower-bound lineage.");
        }

        var removed = state.Atoms.FirstOrDefault(candidate => candidate.Descriptor.Key == atom);
        if (removed is null)
        {
            return Failure(
                ExactLineageFusionStatus.AtomNotPresent,
                "The requested observation is not present in the exact product.");
        }

        if (!removed.PayloadReplayable || state.Atoms.Any(candidate => !candidate.PayloadReplayable))
        {
            return Failure(
                ExactLineageFusionStatus.MissingRetractionWitness,
                "Exact retraction requires replayable likelihood witnesses for the removed atom and the centralized replay oracle.");
        }

        var remaining = state.Atoms.Where(candidate => candidate.Descriptor.Key != atom).ToArray();
        var payload = state.Payload / removed.Likelihood!;
        var oracle = ComputeCentralizedUniqueAtomOracle(remaining);
        if (payload != oracle)
        {
            return Failure(
                ExactLineageFusionStatus.InternalOracleMismatch,
                "Product division disagreed with specialization replay.");
        }

        var root = Dag.AddJoint(remaining.Select(candidate => Dag.AddAtom(candidate.Descriptor)));
        var replayability = remaining.All(candidate => candidate.PayloadReplayable)
            ? PayloadReplayability.ReplayableExact
            : PayloadReplayability.DigestOnly;
        var support = Dag.ProjectSupport(root, Registry, LineageCompleteness.Exact, replayability);
        var multiplicity = Dag.ProjectMultiplicity(root, Registry, LineageCompleteness.Exact, replayability);
        var result = CreateStateCore(
            root,
            support,
            multiplicity,
            payload,
            remaining,
            LineageCompleteness.Exact,
            replayability,
            IssuerAuthenticity.NotProvided,
            new[] { state.ReceiptId });
        return new ExactLineageFusionResult(
            ExactLineageFusionStatus.Success,
            result,
            SupportProjection.Create(
                Registry,
                new[] { atom },
                LineageCompleteness.Exact,
                removed.PayloadReplayable
                    ? PayloadReplayability.ReplayableExact
                    : PayloadReplayability.DigestOnly),
            "Exact product retraction divided by a replayable positive likelihood witness and matched full replay.");
    }

    public static TwoStateLikelihood ComputeCentralizedUniqueAtomOracle(
        IEnumerable<FusionAtomEvidence> atoms)
    {
        ArgumentNullException.ThrowIfNull(atoms);
        var result = TwoStateLikelihood.One;
        var seen = new Dictionary<AtomKey, FusionAtomEvidence>();
        foreach (var atom in atoms)
        {
            ArgumentNullException.ThrowIfNull(atom);
            if (seen.TryGetValue(atom.Descriptor.Key, out var previous))
            {
                if (previous.Descriptor != atom.Descriptor || previous.Likelihood != atom.Likelihood)
                {
                    throw new ArgumentException(
                        $"Conflicting content was supplied for observation {atom.Descriptor.Key}.",
                        nameof(atoms));
                }

                continue;
            }

            seen.Add(atom.Descriptor.Key, atom);
            if (atom.Likelihood is null)
            {
                throw new InvalidOperationException(
                    $"Observation {atom.Descriptor.Key} has no replayable likelihood witness.");
            }

            result *= atom.Likelihood;
        }

        return result;
    }

    private ExactLineageFusionState CreateStateCore(
        string root,
        SupportProjection support,
        MultiplicityProjection multiplicity,
        TwoStateLikelihood payload,
        IReadOnlyList<FusionAtomEvidence> atoms,
        LineageCompleteness completeness,
        PayloadReplayability replayability,
        IssuerAuthenticity authenticity,
        IReadOnlyList<string> parentReceiptIds)
    {
        var parents = parentReceiptIds.OrderBy(parent => parent, StringComparer.Ordinal).ToArray();
        var canonicalAtoms = atoms
            .OrderBy(atom => atom.Descriptor.Key, AtomKeyOrdering.Comparer)
            .ToArray();
        var receiptId = LineageHash.Sha256(LineageText.Fields(
            "prime-axiom-exact-lineage-fusion-receipt-v2",
            ProtocolId,
            Registry.RegistryId,
            root,
            support.Canonical,
            multiplicity.Canonical,
            payload.Canonical,
            string.Concat(canonicalAtoms.Select(atom => LineageText.Fields(atom.Canonical))),
            completeness.ToString(),
            replayability.ToString(),
            authenticity.ToString(),
            string.Concat(parents.Select(parent => LineageText.Fields(parent)))));
        return new ExactLineageFusionState(
            this,
            receiptId,
            root,
            support,
            multiplicity,
            payload,
            canonicalAtoms,
            completeness,
            replayability,
            authenticity,
            parents);
    }

    private void ValidateRegistered(IEnumerable<FusionAtomEvidence> atoms)
    {
        foreach (var atom in atoms)
        {
            if (!Registry.Contains(atom.Descriptor.Key))
            {
                throw new ArgumentException(
                    $"Observation {atom.Descriptor.Key} is outside registry {Registry.RegistryId}.",
                    nameof(atoms));
            }
        }
    }

    private static ExactLineageFusionResult Failure(
        ExactLineageFusionStatus status,
        string detail,
        SupportProjection? overlap = null) =>
        new(status, null, overlap, detail);
}
