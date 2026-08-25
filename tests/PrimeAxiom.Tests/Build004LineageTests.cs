using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using PrimeAxiom.Core.Build004.Lineage;

namespace PrimeAxiom.Tests;

public sealed class Build004LineageTests
{
    [Fact]
    public void LineageIdentityRejectsIllFormedUtf16BeforeCanonicalHashing()
    {
        Assert.Throws<ArgumentException>(() => new AtomKey("\uD800", "epoch", "occurrence"));
        Assert.Throws<ArgumentException>(() => new AtomKey("namespace", "epoch", "\uD801"));

        var validSupplementaryScalar = new AtomKey("namespace", "epoch", "\U0001F642");
        Assert.Equal("\U0001F642", validSupplementaryScalar.OccurrenceId);
    }

    private static readonly string[] TwoOccurrenceIds = { "a", "b" };
    private static readonly string[] FourOccurrenceIds = { "a", "b", "c", "d" };
    private static readonly string[] ChildA = { "A" };
    private static readonly string[] ChildB = { "B" };

    [Fact]
    public void RegistryRejectsCoordinateCollisionsAndNamesItsEpoch()
    {
        var firstKey = new AtomKey("lab", "epoch-1", "a");
        var secondKey = new AtomKey("lab", "epoch-1", "b");

        Assert.Throws<ArgumentException>(() => new LineageRegistry(
            "lab",
            "epoch-1",
            new[]
            {
                new LineageAtomRegistration(firstKey, 2, 0),
                new LineageAtomRegistration(secondKey, 2, 1),
            }));
        Assert.Throws<ArgumentException>(() => new LineageRegistry(
            "lab",
            "epoch-1",
            new[]
            {
                new LineageAtomRegistration(firstKey, 2, 0),
                new LineageAtomRegistration(secondKey, 3, 0),
            }));
        Assert.Throws<ArgumentException>(() => new LineageRegistry(
            "lab",
            "epoch-1",
            new[] { new LineageAtomRegistration(firstKey, 9, 0) }));

        var first = LineageRegistry.CreateSequential("lab", "epoch-1", TwoOccurrenceIds);
        var second = LineageRegistry.CreateSequential("lab", "epoch-2", TwoOccurrenceIds);
        Assert.NotEqual(first.RegistryId, second.RegistryId);
        Assert.Throws<InvalidOperationException>(() =>
            SupportProjection.Create(first, new[] { first.Registrations[0].Key })
                .Union(SupportProjection.Create(second, new[] { second.Registrations[0].Key })));
    }

    [Fact]
    public void ProjectionIdentityPreservesCompletenessAndPayloadReplayabilityAsIndependentAxes()
    {
        var registry = LineageRegistry.CreateSequential("projection-axes", "epoch-1", TwoOccurrenceIds);
        var atoms = registry.Registrations.Select(registration => registration.Key).ToArray();
        var replayable = SupportProjection.Create(
            registry,
            atoms,
            LineageCompleteness.Exact,
            PayloadReplayability.ReplayableExact);
        var digestOnly = SupportProjection.Create(
            registry,
            atoms,
            LineageCompleteness.Exact,
            PayloadReplayability.DigestOnly);

        Assert.Equal(replayable.Atoms, digestOnly.Atoms);
        Assert.Equal(replayable.Completeness, digestOnly.Completeness);
        Assert.NotEqual(replayable, digestOnly);

        var raw = RawPrimeProductRepresentation.Encode(replayable, registry);
        var sparse = SparseExponentRepresentation.EncodeSupport(replayable, registry);
        var dense = DenseBitSetRepresentation.Encode(replayable, registry);
        Assert.Equal(PayloadReplayability.ReplayableExact, raw.PayloadReplayability);
        Assert.Equal(PayloadReplayability.ReplayableExact, sparse.PayloadReplayability);
        Assert.Equal(PayloadReplayability.ReplayableExact, dense.PayloadReplayability);
        Assert.Equal(PayloadReplayability.ReplayableExact, sparse.ToProjection(registry).PayloadReplayability);
        Assert.Equal(PayloadReplayability.ReplayableExact, dense.Decode(registry).PayloadReplayability);

        Assert.Equal(
            PayloadReplayability.DigestOnly,
            replayable.Union(digestOnly).PayloadReplayability);
        Assert.Equal(
            PayloadReplayability.DigestOnly,
            DenseBitSetRepresentation.Encode(replayable, registry)
                .Union(DenseBitSetRepresentation.Encode(digestOnly, registry))
                .PayloadReplayability);

        var engine = new LineageFusionEngine(registry);
        var firstLikelihood = Likelihood(2, 3);
        var secondLikelihood = Likelihood(3, 4);
        var firstEvidence = new FusionAtomEvidence(
            new AtomDescriptor(registry.Registrations[0].Key, "sensor-0", Digest("payload-0")),
            firstLikelihood);
        var secondEvidence = FusionAtomEvidence.DigestOnly(
            new AtomDescriptor(registry.Registrations[1].Key, "sensor-1", Digest("payload-1")));
        var state = engine.CreateState(
            new[] { firstEvidence, secondEvidence },
            retainedAggregatePayload: firstLikelihood * secondLikelihood);
        Assert.Equal(state.LineageCompleteness, state.Support.Completeness);
        Assert.Equal(state.LineageCompleteness, state.Multiplicity.Completeness);
        Assert.Equal(state.PayloadReplayability, state.Support.PayloadReplayability);
        Assert.Equal(state.PayloadReplayability, state.Multiplicity.PayloadReplayability);
        Assert.Equal(PayloadReplayability.DigestOnly, state.PayloadReplayability);
    }

    [Fact]
    public void AllUniverseEightSubsetPairsAgreeAcrossLossOrderedRepresentations()
    {
        var registry = LineageRegistry.CreateSequential(
            "subset",
            "epoch-1",
            Enumerable.Range(0, 8).Select(index => $"a{index}"));
        var keys = registry.Registrations.Select(registration => registration.Key).ToArray();

        for (var leftMask = 0; leftMask < 256; leftMask++)
        {
            var left = SupportProjection.Create(registry, Select(keys, leftMask));
            var leftProduct = RawPrimeProductRepresentation.Encode(left, registry);
            var leftSparse = SparseExponentRepresentation.EncodeSupport(left, registry);
            var leftBits = DenseBitSetRepresentation.Encode(left, registry);

            for (var rightMask = 0; rightMask < 256; rightMask++)
            {
                var right = SupportProjection.Create(registry, Select(keys, rightMask));
                var expectedUnion = left.Union(right);
                var expectedIntersection = left.Intersect(right);

                var productUnion = leftProduct
                    .Union(RawPrimeProductRepresentation.Encode(right, registry), registry)
                    .Support;
                var productIntersection = leftProduct
                    .Intersect(RawPrimeProductRepresentation.Encode(right, registry), registry)
                    .Support;
                Assert.Equal(expectedUnion, productUnion);
                Assert.Equal(expectedIntersection, productIntersection);

                var rightSparse = SparseExponentRepresentation.EncodeSupport(right, registry);
                Assert.Equal(
                    expectedUnion.Atoms,
                    leftSparse.Max(rightSparse, registry).ToProjection(registry).Entries.Select(entry => entry.Atom));
                Assert.Equal(
                    expectedIntersection.Atoms,
                    leftSparse.Min(rightSparse, registry).ToProjection(registry).Entries.Select(entry => entry.Atom));

                var rightBits = DenseBitSetRepresentation.Encode(right, registry);
                Assert.Equal(expectedUnion, leftBits.Union(rightBits).Decode(registry));
                Assert.Equal(expectedIntersection, leftBits.Intersect(rightBits).Decode(registry));
            }
        }
    }

    [Fact]
    public void AllFourLaneTernaryMultiplicityPairsAgreeForAddMinAndMax()
    {
        var registry = LineageRegistry.CreateSequential(
            "multiplicity",
            "epoch-1",
            Enumerable.Range(0, 4).Select(index => $"a{index}"));
        var keys = registry.Registrations.Select(registration => registration.Key).ToArray();

        for (var leftCode = 0; leftCode < 81; leftCode++)
        {
            var leftCounts = DecodeTernary(leftCode, 4);
            var left = SparseExponentRepresentation.Encode(MultiplicityProjection.Create(
                registry,
                Entries(keys, leftCounts)));
            for (var rightCode = 0; rightCode < 81; rightCode++)
            {
                var rightCounts = DecodeTernary(rightCode, 4);
                var right = SparseExponentRepresentation.Encode(MultiplicityProjection.Create(
                    registry,
                    Entries(keys, rightCounts)));
                var add = left.Add(right, registry).ToProjection(registry);
                var min = left.Min(right, registry).ToProjection(registry);
                var max = left.Max(right, registry).ToProjection(registry);
                for (var index = 0; index < keys.Length; index++)
                {
                    Assert.Equal(leftCounts[index] + rightCounts[index], add.GetCount(keys[index]));
                    Assert.Equal(Math.Min(leftCounts[index], rightCounts[index]), min.GetCount(keys[index]));
                    Assert.Equal(Math.Max(leftCounts[index], rightCounts[index]), max.GetCount(keys[index]));
                }
            }
        }
    }

    [Fact]
    public void DagCanonicalizesCommutativeAssociationButPreservesAlternativesAndMultiplicity()
    {
        var fixture = CreateDagFixture();
        var dag = fixture.Dag;
        var a = dag.AddAtom(fixture.Descriptors[0]);
        var b = dag.AddAtom(fixture.Descriptors[1]);
        var c = dag.AddAtom(fixture.Descriptors[2]);
        var d = dag.AddAtom(fixture.Descriptors[3]);

        Assert.Equal(dag.AddJoint(a, b), dag.AddJoint(b, a));
        Assert.Equal(dag.AddJoint(a, b, c), dag.AddJoint(dag.AddJoint(a, b), c));
        Assert.Equal(dag.AddAlternative(a, b, c), dag.AddAlternative(dag.AddAlternative(c, b), a));

        var first = dag.AddAlternative(dag.AddJoint(a, b), dag.AddJoint(c, d));
        var second = dag.AddAlternative(dag.AddJoint(a, c), dag.AddJoint(b, d));
        Assert.NotEqual(first, second);
        Assert.Equal(
            dag.ProjectSupport(first, fixture.Registry),
            dag.ProjectSupport(second, fixture.Registry));
        Assert.Equal(
            dag.ProjectMultiplicity(first, fixture.Registry),
            dag.ProjectMultiplicity(second, fixture.Registry));

        var alternativeDuplicate = dag.AddAlternative(a, a);
        var jointDuplicate = dag.AddJoint(a, a);
        Assert.NotEqual(a, alternativeDuplicate);
        Assert.NotEqual(a, jointDuplicate);
        Assert.NotEqual(alternativeDuplicate, jointDuplicate);
        Assert.Equal(2, dag.ProjectMultiplicity(alternativeDuplicate, fixture.Registry).GetCount(fixture.Descriptors[0].Key));
        Assert.Equal(2, dag.ProjectMultiplicity(jointDuplicate, fixture.Registry).GetCount(fixture.Descriptors[0].Key));

        var factored = dag.AddJoint(a, dag.AddAlternative(b, c));
        var expanded = dag.AddAlternative(dag.AddJoint(a, b), dag.AddJoint(a, c));
        Assert.NotEqual(factored, expanded);
        Assert.Equal(
            dag.ProjectSupport(factored, fixture.Registry),
            dag.ProjectSupport(expanded, fixture.Registry));

        var values = new Dictionary<AtomKey, BigInteger>
        {
            [fixture.Descriptors[0].Key] = 2,
            [fixture.Descriptors[1].Key] = 3,
            [fixture.Descriptors[2].Key] = 5,
            [fixture.Descriptors[3].Key] = 7,
        };
        Assert.Equal(dag.EvaluatePositive(factored, values), dag.EvaluatePositive(expanded, values));

        var sameSupportAlternative = dag.AddAlternative(a, b);
        var sameSupportJoint = dag.AddJoint(a, b);
        Assert.Equal(
            dag.ProjectSupport(sameSupportAlternative, fixture.Registry),
            dag.ProjectSupport(sameSupportJoint, fixture.Registry));
        Assert.NotEqual(
            dag.EvaluatePositive(sameSupportAlternative, values),
            dag.EvaluatePositive(sameSupportJoint, values));
    }

    [Fact]
    public void DagVerifierRejectsMutationDanglingCyclesAndFalseCaches()
    {
        var fixture = CreateDagFixture();
        var a = fixture.Dag.AddAtom(fixture.Descriptors[0]);
        var b = fixture.Dag.AddAtom(fixture.Descriptors[1]);
        var root = fixture.Dag.AddJoint(a, b);
        var support = fixture.Dag.ProjectSupport(root, fixture.Registry);
        var multiplicity = fixture.Dag.ProjectMultiplicity(root, fixture.Registry);
        Assert.True(fixture.Dag.Verify(root, fixture.Registry, support, multiplicity).IsValid);
        var reachable = fixture.Dag.GetReachableSnapshots(root);
        Assert.Equal(3, reachable.Count);
        Assert.Equal(reachable.OrderBy(snapshot => snapshot.NodeId, StringComparer.Ordinal), reachable);
        Assert.DoesNotContain(reachable, snapshot => snapshot.NodeId == fixture.Dag.ZeroNodeId);
        Assert.Throws<KeyNotFoundException>(() => fixture.Dag.GetReachableSnapshots(new string('F', 64)));

        var snapshots = fixture.Dag.Snapshots.ToArray();
        var atomIndex = Array.FindIndex(snapshots, snapshot => snapshot.NodeId == a);
        snapshots[atomIndex] = snapshots[atomIndex] with
        {
            Atom = new AtomDescriptor(
                fixture.Descriptors[0].Key,
                fixture.Descriptors[0].SourceId,
                Digest("mutated")),
        };
        Assert.Equal(
            DagVerificationStatus.HashMismatch,
            DerivationDag.VerifySnapshots(snapshots, root, fixture.Registry).Status);

        snapshots = fixture.Dag.Snapshots.ToArray();
        var rootIndex = Array.FindIndex(snapshots, snapshot => snapshot.NodeId == root);
        snapshots[rootIndex] = snapshots[rootIndex] with
        {
            ChildNodeIds = new[] { a, new string('F', 64) },
        };
        Assert.Equal(
            DagVerificationStatus.DanglingReference,
            DerivationDag.VerifySnapshots(snapshots, root, fixture.Registry).Status);

        var cyclic = new[]
        {
            new DerivationNodeSnapshot("A", DerivationNodeKind.Transform, null, "cycle", ChildB),
            new DerivationNodeSnapshot("B", DerivationNodeKind.Transform, null, "cycle", ChildA),
        };
        Assert.Equal(
            DagVerificationStatus.CycleDetected,
            DerivationDag.VerifySnapshots(cyclic, "A", fixture.Registry).Status);

        var falseSupport = SupportProjection.Create(fixture.Registry, new[] { fixture.Descriptors[0].Key });
        Assert.Equal(
            DagVerificationStatus.CachedSupportMismatch,
            fixture.Dag.Verify(root, fixture.Registry, falseSupport, multiplicity).Status);
        var falseMultiplicity = MultiplicityProjection.Create(
            fixture.Registry,
            new[] { new AtomMultiplicity(fixture.Descriptors[0].Key, 2), new AtomMultiplicity(fixture.Descriptors[1].Key, 1) });
        Assert.Equal(
            DagVerificationStatus.CachedMultiplicityMismatch,
            fixture.Dag.Verify(root, fixture.Registry, support, falseMultiplicity).Status);

        var incompleteSupport = SupportProjection.Create(
            fixture.Registry,
            support.Atoms,
            LineageCompleteness.KnownLowerBound);
        Assert.Equal(
            DagVerificationStatus.CachedSupportMismatch,
            fixture.Dag.Verify(root, fixture.Registry, incompleteSupport, multiplicity).Status);
        var conflictingMultiplicity = MultiplicityProjection.Create(
            fixture.Registry,
            multiplicity.Entries,
            LineageCompleteness.Conflict);
        Assert.Equal(
            DagVerificationStatus.CachedMultiplicityMismatch,
            fixture.Dag.Verify(root, fixture.Registry, support, conflictingMultiplicity).Status);

        var replayableSupport = SupportProjection.Create(
            fixture.Registry,
            support.Atoms,
            LineageCompleteness.Exact,
            PayloadReplayability.ReplayableExact);
        Assert.Equal(
            DagVerificationStatus.CachedSupportMismatch,
            fixture.Dag.Verify(root, fixture.Registry, replayableSupport, multiplicity).Status);
        var replayableMultiplicity = MultiplicityProjection.Create(
            fixture.Registry,
            multiplicity.Entries,
            LineageCompleteness.Exact,
            PayloadReplayability.ReplayableExact);
        Assert.Equal(
            DagVerificationStatus.CachedMultiplicityMismatch,
            fixture.Dag.Verify(root, fixture.Registry, support, replayableMultiplicity).Status);

        var conflictingDescriptor = new AtomDescriptor(
            fixture.Descriptors[0].Key,
            "conflicting-source",
            Digest("conflicting-payload"));
        var conflictingAtom = fixture.Dag.AddAtom(conflictingDescriptor);
        var conflictingRoot = fixture.Dag.AddJoint(a, conflictingAtom);
        Assert.Equal(
            DagVerificationStatus.ConflictingAtomDescriptor,
            fixture.Dag.Verify(conflictingRoot, fixture.Registry).Status);
    }

    [Fact]
    public void DagVerifierRejectsRehashedNodesOutsideConstructorCanonicalNormalForm()
    {
        var fixture = CreateDagFixture();
        var a = fixture.Dag.AddAtom(fixture.Descriptors[0]);
        var b = fixture.Dag.AddAtom(fixture.Descriptors[1]);
        var c = fixture.Dag.AddAtom(fixture.Descriptors[2]);
        var joint = fixture.Dag.AddJoint(a, b);
        var alternative = fixture.Dag.AddAlternative(a, b);
        var validSnapshots = fixture.Dag.Snapshots;

        var sortedJointChildren = new[] { joint, c }.Order(StringComparer.Ordinal).ToArray();
        var sortedAlternativeChildren = new[] { alternative, c }.Order(StringComparer.Ordinal).ToArray();
        var sortedJointIdentityChildren = new[] { a, fixture.Dag.OneNodeId }.Order(StringComparer.Ordinal).ToArray();
        var sortedAlternativeIdentityChildren = new[] { a, fixture.Dag.ZeroNodeId }.Order(StringComparer.Ordinal).ToArray();
        var sortedJointAnnihilatorChildren = new[] { a, fixture.Dag.ZeroNodeId }.Order(StringComparer.Ordinal).ToArray();
        var unsortedChildren = new[] { a, b }.OrderDescending(StringComparer.Ordinal).ToArray();
        var noncanonicalRoots = new[]
        {
            RehashedNode(DerivationNodeKind.Joint, sortedJointChildren),
            RehashedNode(DerivationNodeKind.Alternative, sortedAlternativeChildren),
            RehashedNode(DerivationNodeKind.Joint, sortedJointIdentityChildren),
            RehashedNode(DerivationNodeKind.Alternative, sortedAlternativeIdentityChildren),
            RehashedNode(DerivationNodeKind.Joint, sortedJointAnnihilatorChildren),
            RehashedNode(DerivationNodeKind.Joint, unsortedChildren),
            RehashedNode(DerivationNodeKind.Joint, a),
            RehashedNode(DerivationNodeKind.Alternative, a),
            RehashedNode(DerivationNodeKind.Joint),
            RehashedNode(DerivationNodeKind.Alternative),
        };

        foreach (var noncanonicalRoot in noncanonicalRoots)
        {
            Assert.Equal(ComputeNodeId(noncanonicalRoot), noncanonicalRoot.NodeId);
            var result = DerivationDag.VerifySnapshots(
                validSnapshots.Append(noncanonicalRoot),
                noncanonicalRoot.NodeId,
                fixture.Registry);
            Assert.Equal(DagVerificationStatus.InvalidNodeShape, result.Status);
        }
    }

    [Fact]
    public void RetractionUsesPositiveSpecializationRatherThanNaiveSupportSubtraction()
    {
        var fixture = CreateDagFixture();
        var nodes = fixture.Descriptors.Select(fixture.Dag.AddAtom).ToArray();
        var expression = fixture.Dag.AddAlternative(
            fixture.Dag.AddJoint(nodes[0], nodes[1]),
            fixture.Dag.AddJoint(nodes[2], nodes[3]));
        var result = fixture.Dag.SpecializeAtomToZero(expression, fixture.Descriptors[0].Key);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.RootNodeId);
        var replayed = fixture.Dag.ProjectSupport(result.RootNodeId, fixture.Registry);
        Assert.Equal(
            new[] { fixture.Descriptors[2].Key, fixture.Descriptors[3].Key },
            replayed.Atoms);

        var naive = fixture.Dag.ProjectSupport(expression, fixture.Registry).ExceptExact(
            SupportProjection.Create(fixture.Registry, new[] { fixture.Descriptors[0].Key }));
        Assert.Contains(fixture.Descriptors[1].Key, naive.Atoms);
        Assert.DoesNotContain(fixture.Descriptors[1].Key, replayed.Atoms);

        var opaque = fixture.Dag.AddTransform("OPAQUE_UDF", nodes[0]);
        Assert.Equal(
            DagRetractionStatus.NonInvertibleTransform,
            fixture.Dag.SpecializeAtomToZero(opaque, fixture.Descriptors[0].Key).Status);
    }

    [Fact]
    public void ExactFusionCycleDeduplicatesAgainstCentralOracleAndStabilizesSemanticRoot()
    {
        var registry = LineageRegistry.CreateSequential("fusion", "epoch-1", FourOccurrenceIds);
        var engine = new LineageFusionEngine(registry);
        var atoms = registry.Registrations.Select((registration, index) => new FusionAtomEvidence(
            new AtomDescriptor(registration.Key, $"sensor-{index}", Digest($"payload-{index}")),
            Likelihood(index + 2, index + 3))).ToArray();
        var a = engine.CreateState(new[] { atoms[0] });
        var b = engine.CreateState(new[] { atoms[1] });
        var c = engine.CreateState(new[] { atoms[2] });
        var ab = RequireSuccess(engine.MergeUnique(a, b));
        var bc = RequireSuccess(engine.MergeUnique(b, c));
        var ac = RequireSuccess(engine.MergeUnique(a, c));

        var abcFromFirstPath = RequireSuccess(engine.MergeUnique(ab, bc));
        var abcFromSecondPath = RequireSuccess(engine.MergeUnique(ac, bc));
        Assert.Equal(abcFromFirstPath.ExpressionRootId, abcFromSecondPath.ExpressionRootId);
        Assert.Equal(abcFromFirstPath.Payload, abcFromSecondPath.Payload);
        Assert.Equal(
            LineageFusionEngine.ComputeCentralizedUniqueAtomOracle(atoms.Take(3)),
            abcFromFirstPath.Payload);

        var noNewInformation = RequireSuccess(engine.MergeUnique(abcFromFirstPath, bc));
        Assert.Equal(abcFromFirstPath.ExpressionRootId, noNewInformation.ExpressionRootId);
        Assert.Equal(abcFromFirstPath.Payload, noNewInformation.Payload);
        Assert.Equal(3, noNewInformation.Support.Count);
        Assert.Equal(IssuerAuthenticity.NotProvided, noNewInformation.IssuerAuthenticity);
        Assert.Equal("NOT_CRYPTOGRAPHIC", LineageFusionEngine.CryptographicStatus);
        Assert.Equal("NO_PRIVACY", LineageFusionEngine.PrivacyStatus);
        Assert.Contains("No signature", LineageFusionEngine.SecurityBoundary, StringComparison.Ordinal);
    }

    [Fact]
    public void FusionFailuresAreTypedAtomicAndEpochAware()
    {
        var registry = LineageRegistry.CreateSequential("fusion", "epoch-1", TwoOccurrenceIds);
        var engine = new LineageFusionEngine(registry);
        var descriptor = new AtomDescriptor(registry.Registrations[0].Key, "sensor", Digest("same"));
        var likelihood = Likelihood(2, 3);
        var replayable = engine.CreateState(new[] { new FusionAtomEvidence(descriptor, likelihood) });
        var unavailable = engine.CreateState(
            new[] { FusionAtomEvidence.DigestOnly(descriptor) },
            retainedAggregatePayload: likelihood);
        var originalReceipt = replayable.ReceiptId;

        Assert.Equal(
            ExactLineageFusionStatus.AuthenticationNotProvided,
            engine.RequireVerifiedIssuerAuthentication(replayable).Status);
        var unverifiedExternal = engine.CreateState(
            new[] { new FusionAtomEvidence(descriptor, likelihood) },
            authenticity: IssuerAuthenticity.ExternalClaimNotVerified);
        Assert.Equal(
            ExactLineageFusionStatus.ExternalAuthenticationUnverified,
            engine.RequireVerifiedIssuerAuthentication(unverifiedExternal).Status);

        var missing = engine.MergeUnique(unavailable, unavailable);
        Assert.Equal(ExactLineageFusionStatus.OverlapPayloadUnavailable, missing.Status);
        Assert.Null(missing.State);
        Assert.Equal(originalReceipt, replayable.ReceiptId);

        var conflictDescriptor = new AtomDescriptor(descriptor.Key, "sensor", Digest("conflict"));
        var conflict = engine.CreateState(new[] { new FusionAtomEvidence(conflictDescriptor, likelihood) });
        Assert.Equal(
            ExactLineageFusionStatus.ConflictingAtomPayload,
            engine.MergeUnique(replayable, conflict).Status);

        var partial = engine.CreateState(
            new[] { new FusionAtomEvidence(descriptor, likelihood) },
            LineageCompleteness.KnownLowerBound);
        Assert.Equal(ExactLineageFusionStatus.PartialLineage, engine.MergeUnique(replayable, partial).Status);

        var nextRegistry = LineageRegistry.CreateSequential("fusion", "epoch-2", TwoOccurrenceIds);
        var nextEngine = new LineageFusionEngine(nextRegistry);
        var nextDescriptor = new AtomDescriptor(nextRegistry.Registrations[0].Key, "sensor", Digest("same"));
        var delayed = nextEngine.CreateState(new[] { new FusionAtomEvidence(nextDescriptor, likelihood) });
        Assert.Equal(
            ExactLineageFusionStatus.RegistryEpochMismatch,
            engine.MergeUnique(replayable, delayed).Status);
    }

    [Fact]
    public void ExactFusionAllowsDigestOnlyUniqueLeafWhenOverlapWitnessAndExternalOracleExist()
    {
        var registry = LineageRegistry.CreateSequential("fusion-opaque-unique", "epoch-1", TwoOccurrenceIds);
        var engine = new LineageFusionEngine(registry);
        var first = new FusionAtomEvidence(
            new AtomDescriptor(registry.Registrations[0].Key, "sensor-0", Digest("payload-0")),
            Likelihood(2, 3));
        var secondTruth = new FusionAtomEvidence(
            new AtomDescriptor(registry.Registrations[1].Key, "sensor-1", Digest("payload-1")),
            Likelihood(3, 5));
        var externalOracle = LineageFusionEngine.ComputeCentralizedUniqueAtomOracle(new[] { first, secondTruth });
        var left = engine.CreateState(new[] { first });
        var right = engine.CreateState(
            new[] { first, FusionAtomEvidence.DigestOnly(secondTruth.Descriptor) },
            retainedAggregatePayload: externalOracle);

        var merged = RequireSuccess(engine.MergeUnique(left, right));
        Assert.Equal(externalOracle, merged.Payload);
        Assert.Equal(PayloadReplayability.DigestOnly, merged.PayloadReplayability);
        Assert.Null(merged.Atoms.Single(atom => atom.Descriptor.Key == secondTruth.Descriptor.Key).Likelihood);
        Assert.Throws<ArgumentException>(() => engine.CreateState(
            new[] { FusionAtomEvidence.DigestOnly(secondTruth.Descriptor) }));
        Assert.Throws<ArgumentException>(() => engine.CreateState(
            new[] { first },
            retainedAggregatePayload: TwoStateLikelihood.One));
    }

    [Fact]
    public void FusionReceiptBindsPerAtomLikelihoodAndReplayabilityRatherThanOnlyAggregatePayload()
    {
        var registry = LineageRegistry.CreateSequential("fusion-receipt", "epoch-1", TwoOccurrenceIds);
        var engine = new LineageFusionEngine(registry);
        var firstDescriptor = new AtomDescriptor(registry.Registrations[0].Key, "sensor-0", Digest("payload-0"));
        var secondDescriptor = new AtomDescriptor(registry.Registrations[1].Key, "sensor-1", Digest("payload-1"));
        var twoThree = new TwoStateLikelihood(new ExactPositiveRational(2, 1), new ExactPositiveRational(3, 1));
        var fiveSeven = new TwoStateLikelihood(new ExactPositiveRational(5, 1), new ExactPositiveRational(7, 1));
        var tenTwentyOne = new TwoStateLikelihood(new ExactPositiveRational(10, 1), new ExactPositiveRational(21, 1));
        var oneOne = TwoStateLikelihood.One;

        var factoredOneWay = engine.CreateState(new[]
        {
            new FusionAtomEvidence(firstDescriptor, twoThree),
            new FusionAtomEvidence(secondDescriptor, fiveSeven),
        });
        var factoredAnotherWay = engine.CreateState(new[]
        {
            new FusionAtomEvidence(firstDescriptor, tenTwentyOne),
            new FusionAtomEvidence(secondDescriptor, oneOne),
        });
        Assert.Equal(factoredOneWay.ExpressionRootId, factoredAnotherWay.ExpressionRootId);
        Assert.Equal(factoredOneWay.Payload, factoredAnotherWay.Payload);
        Assert.NotEqual(factoredOneWay.ReceiptId, factoredAnotherWay.ReceiptId);

        var firstUnavailable = engine.CreateState(
            new[]
            {
                FusionAtomEvidence.DigestOnly(firstDescriptor),
                new FusionAtomEvidence(secondDescriptor, fiveSeven),
            },
            retainedAggregatePayload: factoredOneWay.Payload);
        var secondUnavailable = engine.CreateState(
            new[]
            {
                new FusionAtomEvidence(firstDescriptor, twoThree),
                FusionAtomEvidence.DigestOnly(secondDescriptor),
            },
            retainedAggregatePayload: factoredOneWay.Payload);
        Assert.Equal(firstUnavailable.PayloadReplayability, secondUnavailable.PayloadReplayability);
        Assert.Equal(firstUnavailable.Payload, secondUnavailable.Payload);
        Assert.NotEqual(firstUnavailable.ReceiptId, secondUnavailable.ReceiptId);
        Assert.NotEqual(factoredOneWay.ReceiptId, firstUnavailable.ReceiptId);
    }

    [Fact]
    public void ExactProductRetractionRequiresReplayableWitness()
    {
        var registry = LineageRegistry.CreateSequential("retract", "epoch-1", TwoOccurrenceIds);
        var engine = new LineageFusionEngine(registry);
        var first = new FusionAtomEvidence(
            new AtomDescriptor(registry.Registrations[0].Key, "s0", Digest("p0")),
            Likelihood(2, 5));
        var second = new FusionAtomEvidence(
            new AtomDescriptor(registry.Registrations[1].Key, "s1", Digest("p1")),
            Likelihood(3, 7));
        var combined = engine.CreateState(new[] { first, second });
        var retracted = RequireSuccess(engine.RetractExactProduct(combined, first.Descriptor.Key));
        Assert.Equal(second.Likelihood!, retracted.Payload);
        Assert.Equal(new[] { second.Descriptor.Key }, retracted.Support.Atoms);

        var unavailable = engine.CreateState(
            new[]
            {
                first,
                FusionAtomEvidence.DigestOnly(second.Descriptor),
            },
            retainedAggregatePayload: combined.Payload);
        Assert.Equal(
            ExactLineageFusionStatus.MissingRetractionWitness,
            engine.RetractExactProduct(unavailable, second.Descriptor.Key).Status);
    }

    [Fact]
    public void EvidenceEnvelopeBindsDagRegistryIndependentAxesAndTypedReferences()
    {
        var fixture = CreateDagFixture();
        var first = fixture.Dag.AddAtom(fixture.Descriptors[0]);
        var second = fixture.Dag.AddAtom(fixture.Descriptors[1]);
        var root = fixture.Dag.AddJoint(first, second);
        var callerOwned = new[]
        {
            new LineageEvidenceReference(
                LineageEvidenceReferenceKind.Residual,
                "residual:unresolved-temperature-drift",
                Digest("residual")),
            new LineageEvidenceReference(
                LineageEvidenceReferenceKind.Uncertainty,
                "uncertainty:covariance-record-7",
                Digest("uncertainty")),
            new LineageEvidenceReference(
                LineageEvidenceReferenceKind.CalibrationValidity,
                "calibration:certificate-42",
                Digest("calibration")),
            new LineageEvidenceReference(
                LineageEvidenceReferenceKind.Source,
                "source:sensor-a-occurrence",
                Digest("source")),
        };
        var envelope = LineageEvidenceEnvelope.Create(
            fixture.Dag,
            root,
            fixture.Registry,
            LineageCompleteness.Exact,
            PayloadReplayability.ReplayableExact,
            IssuerAuthenticity.NotProvided,
            callerOwned);

        callerOwned[0] = new LineageEvidenceReference(
            LineageEvidenceReferenceKind.Residual,
            "residual:caller-mutated",
            Digest("caller-mutated"));
        Assert.Equal(
            new[]
            {
                LineageEvidenceReferenceKind.Source,
                LineageEvidenceReferenceKind.CalibrationValidity,
                LineageEvidenceReferenceKind.Uncertainty,
                LineageEvidenceReferenceKind.Residual,
            },
            envelope.EvidenceReferences.Select(reference => reference.Kind));
        Assert.DoesNotContain(
            envelope.EvidenceReferences,
            reference => reference.ReferenceId == "residual:caller-mutated");

        var snapshot = envelope.ToSnapshot();
        var verification = LineageEvidenceEnvelope.VerifySnapshot(
            snapshot,
            fixture.Dag.GetReachableSnapshots(root),
            fixture.Registry);
        Assert.True(verification.IsValid);
        Assert.Equal(
            LineageEvidenceEnvelopeVerificationStatus.ValidIntegrityOnly,
            verification.Status);
        Assert.True(verification.EnvelopeHashMatches);
        Assert.True(verification.RegistryBindingMatches);
        Assert.True(verification.DagReplay?.IsValid);
        Assert.False(LineageEvidenceEnvelopeVerificationResult.EstablishesIssuerAuthentication);
        Assert.Contains("no issuer authentication", verification.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NO_SIGNATURE", LineageEvidenceEnvelope.SecurityBoundary, StringComparison.Ordinal);

        var differentCompleteness = LineageEvidenceEnvelope.Create(
            fixture.Dag,
            root,
            fixture.Registry,
            LineageCompleteness.KnownLowerBound,
            PayloadReplayability.ReplayableExact,
            IssuerAuthenticity.NotProvided,
            envelope.EvidenceReferences);
        var differentReplayability = LineageEvidenceEnvelope.Create(
            fixture.Dag,
            root,
            fixture.Registry,
            LineageCompleteness.Exact,
            PayloadReplayability.DigestOnly,
            IssuerAuthenticity.NotProvided,
            envelope.EvidenceReferences);
        var differentAuthenticity = LineageEvidenceEnvelope.Create(
            fixture.Dag,
            root,
            fixture.Registry,
            LineageCompleteness.Exact,
            PayloadReplayability.ReplayableExact,
            IssuerAuthenticity.ExternalClaimNotVerified,
            envelope.EvidenceReferences);
        Assert.NotEqual(envelope.EnvelopeId, differentCompleteness.EnvelopeId);
        Assert.NotEqual(envelope.EnvelopeId, differentReplayability.EnvelopeId);
        Assert.NotEqual(envelope.EnvelopeId, differentAuthenticity.EnvelopeId);
    }

    [Fact]
    public void EvidenceEnvelopeVerificationDetectsTamperedHashEvidenceAndAxes()
    {
        var fixture = CreateDagFixture();
        var root = fixture.Dag.AddAtom(fixture.Descriptors[0]);
        var envelope = LineageEvidenceEnvelope.Create(
            fixture.Dag,
            root,
            fixture.Registry,
            LineageCompleteness.Exact,
            PayloadReplayability.ReplayableExact,
            IssuerAuthenticity.NotProvided,
            new[]
            {
                new LineageEvidenceReference(
                    LineageEvidenceReferenceKind.Source,
                    "source:fixture",
                    Digest("source-fixture")),
                new LineageEvidenceReference(
                    LineageEvidenceReferenceKind.Residual,
                    "residual:open",
                    Digest("residual-open")),
            });
        var snapshot = envelope.ToSnapshot();
        var dagSnapshots = fixture.Dag.GetReachableSnapshots(root);

        var tamperedHash = snapshot with { EnvelopeId = Digest("wrong-envelope-id") };
        var hashResult = LineageEvidenceEnvelope.VerifySnapshot(
            tamperedHash,
            dagSnapshots,
            fixture.Registry);
        Assert.Equal(LineageEvidenceEnvelopeVerificationStatus.EnvelopeHashMismatch, hashResult.Status);
        Assert.False(hashResult.EnvelopeHashMatches);
        Assert.True(hashResult.DagReplay?.IsValid);

        var changedEvidence = snapshot.EvidenceReferences.ToArray();
        changedEvidence[0] = changedEvidence[0] with { ContentSha256 = Digest("changed-evidence") };
        var evidenceResult = LineageEvidenceEnvelope.VerifySnapshot(
            snapshot with { EvidenceReferences = Array.AsReadOnly(changedEvidence) },
            dagSnapshots,
            fixture.Registry);
        Assert.Equal(LineageEvidenceEnvelopeVerificationStatus.EnvelopeHashMismatch, evidenceResult.Status);
        Assert.False(evidenceResult.EnvelopeHashMatches);
        Assert.True(evidenceResult.DagReplay?.IsValid);

        var axisMutations = new[]
        {
            snapshot with { LineageCompleteness = LineageCompleteness.KnownLowerBound },
            snapshot with { PayloadReplayability = PayloadReplayability.MissingDependency },
            snapshot with { IssuerAuthenticity = IssuerAuthenticity.ExternalClaimNotVerified },
        };
        foreach (var axisMutation in axisMutations)
        {
            var result = LineageEvidenceEnvelope.VerifySnapshot(
                axisMutation,
                dagSnapshots,
                fixture.Registry);
            Assert.Equal(LineageEvidenceEnvelopeVerificationStatus.EnvelopeHashMismatch, result.Status);
            Assert.False(result.EnvelopeHashMatches);
            Assert.True(result.DagReplay?.IsValid);
        }
    }

    [Fact]
    public void EvidenceEnvelopeVerificationDetectsTamperedRootAndRegistryBinding()
    {
        var fixture = CreateDagFixture();
        var root = fixture.Dag.AddAtom(fixture.Descriptors[0]);
        var envelope = LineageEvidenceEnvelope.Create(
            fixture.Dag,
            root,
            fixture.Registry,
            LineageCompleteness.Exact,
            PayloadReplayability.DigestOnly,
            IssuerAuthenticity.NotProvided,
            Array.Empty<LineageEvidenceReference>());
        var snapshot = envelope.ToSnapshot();
        var dagSnapshots = fixture.Dag.GetReachableSnapshots(root);

        var rootResult = LineageEvidenceEnvelope.VerifySnapshot(
            snapshot with { RootNodeId = Digest("missing-root") },
            dagSnapshots,
            fixture.Registry);
        Assert.Equal(LineageEvidenceEnvelopeVerificationStatus.DagReplayFailed, rootResult.Status);
        Assert.False(rootResult.EnvelopeHashMatches);
        Assert.Equal(DagVerificationStatus.MissingRoot, rootResult.DagReplay?.Status);

        var changedRegistryId = LineageEvidenceEnvelope.VerifySnapshot(
            snapshot with { RegistryId = Digest("wrong-registry") },
            dagSnapshots,
            fixture.Registry);
        Assert.Equal(
            LineageEvidenceEnvelopeVerificationStatus.RegistryBindingMismatch,
            changedRegistryId.Status);
        Assert.False(changedRegistryId.RegistryBindingMatches);

        var otherRegistry = LineageRegistry.CreateSequential("dag", "epoch-2", FourOccurrenceIds);
        var otherRegistryResult = LineageEvidenceEnvelope.VerifySnapshot(
            snapshot,
            dagSnapshots,
            otherRegistry);
        Assert.Equal(
            LineageEvidenceEnvelopeVerificationStatus.RegistryBindingMismatch,
            otherRegistryResult.Status);
        Assert.False(otherRegistryResult.RegistryBindingMatches);
        Assert.Equal(DagVerificationStatus.RegistryMismatch, otherRegistryResult.DagReplay?.Status);
    }

    [Fact]
    public void EvidenceEnvelopeRejectsNoncanonicalDigestsDuplicateReferencesAndOversizeLists()
    {
        var fixture = CreateDagFixture();
        var root = fixture.Dag.AddAtom(fixture.Descriptors[0]);
        var reference = new LineageEvidenceReference(
            LineageEvidenceReferenceKind.Source,
            "source:fixture",
            Digest("source-fixture"));
        Assert.Throws<ArgumentException>(() => LineageEvidenceEnvelope.Create(
            fixture.Dag,
            root,
            fixture.Registry,
            LineageCompleteness.Exact,
            PayloadReplayability.DigestOnly,
            IssuerAuthenticity.NotProvided,
            new[] { reference, reference }));

        var tooMany = Enumerable.Range(0, LineageEvidenceEnvelope.MaximumEvidenceReferences + 1)
            .Select(index => new LineageEvidenceReference(
                LineageEvidenceReferenceKind.Residual,
                $"residual:{index}",
                Digest($"residual-{index}")))
            .ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => LineageEvidenceEnvelope.Create(
            fixture.Dag,
            root,
            fixture.Registry,
            LineageCompleteness.Exact,
            PayloadReplayability.DigestOnly,
            IssuerAuthenticity.NotProvided,
            tooMany));

        var envelope = LineageEvidenceEnvelope.Create(
            fixture.Dag,
            root,
            fixture.Registry,
            LineageCompleteness.Exact,
            PayloadReplayability.DigestOnly,
            IssuerAuthenticity.NotProvided,
            new[] { reference });
        var snapshot = envelope.ToSnapshot();
        var lowercaseHash = snapshot with { EnvelopeId = snapshot.EnvelopeId.ToLowerInvariant() };
        Assert.Equal(
            LineageEvidenceEnvelopeVerificationStatus.InvalidSnapshot,
            LineageEvidenceEnvelope.VerifySnapshot(
                lowercaseHash,
                fixture.Dag.GetReachableSnapshots(root),
                fixture.Registry).Status);
    }

    [Fact]
    public void ExactPositiveRationalCrossReductionPreservesExactProbability()
    {
        var left = new ExactPositiveRational(new BigInteger(6), new BigInteger(35));
        var right = new ExactPositiveRational(new BigInteger(14), new BigInteger(15));
        Assert.Equal(new ExactPositiveRational(4, 25), left * right);

        var likelihood = new TwoStateLikelihood(
            new ExactPositiveRational(1, 3),
            new ExactPositiveRational(2, 3));
        Assert.Equal(new ExactPositiveRational(2, 3), likelihood.ProbabilityOfOne);
    }

    private static ExactLineageFusionState RequireSuccess(ExactLineageFusionResult result)
    {
        Assert.Equal(ExactLineageFusionStatus.Success, result.Status);
        Assert.NotNull(result.State);
        return result.State;
    }

    private static (LineageRegistry Registry, DerivationDag Dag, AtomDescriptor[] Descriptors) CreateDagFixture()
    {
        var registry = LineageRegistry.CreateSequential("dag", "epoch-1", FourOccurrenceIds);
        var descriptors = registry.Registrations.Select((registration, index) => new AtomDescriptor(
            registration.Key,
            $"source-{index}",
            Digest($"payload-{index}"))).ToArray();
        return (registry, new DerivationDag(), descriptors);
    }

    private static DerivationNodeSnapshot RehashedNode(
        DerivationNodeKind kind,
        params string[] childNodeIds)
    {
        var provisional = new DerivationNodeSnapshot(
            string.Empty,
            kind,
            null,
            null,
            Array.AsReadOnly(childNodeIds.ToArray()));
        return provisional with { NodeId = ComputeNodeId(provisional) };
    }

    private static string ComputeNodeId(DerivationNodeSnapshot node)
    {
        var method = typeof(DerivationDag).GetMethod(
            "ComputeNodeId",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, new object[] { node }));
    }

    private static TwoStateLikelihood Likelihood(int zeroNumerator, int oneNumerator) => new(
        new ExactPositiveRational(zeroNumerator, zeroNumerator + oneNumerator),
        new ExactPositiveRational(oneNumerator, zeroNumerator + oneNumerator));

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static IEnumerable<AtomKey> Select(IReadOnlyList<AtomKey> keys, int mask)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            if ((mask & (1 << index)) != 0)
            {
                yield return keys[index];
            }
        }
    }

    private static int[] DecodeTernary(int code, int width)
    {
        var result = new int[width];
        for (var index = 0; index < width; index++)
        {
            result[index] = code % 3;
            code /= 3;
        }

        return result;
    }

    private static IEnumerable<AtomMultiplicity> Entries(
        IReadOnlyList<AtomKey> keys,
        IReadOnlyList<int> counts)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            if (counts[index] > 0)
            {
                yield return new AtomMultiplicity(keys[index], counts[index]);
            }
        }
    }
}
