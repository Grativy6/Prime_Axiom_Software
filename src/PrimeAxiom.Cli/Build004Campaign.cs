using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using PrimeAxiom.Core.Build004.Combinatorics;
using PrimeAxiom.Core.Build004.Lineage;
using PrimeAxiom.Core.Build004.Probes;

namespace PrimeAxiom.Cli;

internal sealed record Build004FamilyReceipt(
    string Family,
    long ExpectedCases,
    long ExpectedChecks,
    long Cases,
    long Checks,
    long FailureCount,
    IReadOnlyList<string> FailureDetails,
    string Status);

internal sealed record Build004StructuralCostRow(
    string Ledger,
    string Domain,
    string CaseId,
    string Metric,
    string Value,
    string Unit,
    string SoftwareMeaning,
    string HardwareImplication);

internal sealed record Build004CampaignResult(
    object Combinatorics,
    object Lineage,
    object Fusion,
    object BoundaryProbes,
    IReadOnlyList<Build004StructuralCostRow> StructuralCosts,
    byte[] AudioBytes,
    IReadOnlyList<Build004FamilyReceipt> Families,
    long Checks,
    long Failures,
    bool CompleteFrozenCoverage);

internal static class Build004Campaign
{
    internal const string AbstractStructureLedger = "ABSTRACT_STRUCTURE";
    internal const string HostSoftwareLedger = "HOST_SOFTWARE_DIAGNOSTIC";
    internal const string PhysicalHardwareLedger = "PHYSICAL_HARDWARE_IMPLICATION";

    private static readonly string[] LineageOccurrenceIds = ["a", "b", "c", "d", "e", "f", "g", "h"];
    private static readonly string[] AccumulatorLeftIds = ["sensor-a", "sensor-c"];
    private static readonly string[] AccumulatorRightIds = ["sensor-b", "sensor-c"];
    private static readonly string[] AccumulatorIntersectionIds = ["sensor-c"];
    private static readonly string[] SharedBomComponentIds = ["shared-fastener"];

    private const string FactorialFamily = "FACTORIAL_0_512";
    private const string BinomialFamily = "BINOMIAL_ALL_0_256";
    private const string HyperPointFamily = "HYPERGEOMETRIC_POINTS_0_24";
    private const string HyperNormalizationFamily = "HYPERGEOMETRIC_NORMALIZATION_0_32";
    private const string SeededPointFamily = "SEEDED_POINTS_N_LE_4096";
    private const string AdjacentFamily = "ADJACENT_STREAMS_0_48_PLUS_N2000";
    private const string NamedCombinatorialFamily = "NAMED_COMBINATORIAL_CONTROLS";
    private const string SupportProjectionFamily = "SUPPORT_PROJECTION_U8";
    private const string MultiplicityProjectionFamily = "MULTIPLICITY_PROJECTION_U4_E0_2";
    private const string DagFamily = "DERIVATION_DAG_AND_MUTATIONS";
    private const string FusionCycleFamily = "EXACT_FUSION_CYCLES";
    private const string FusionScheduleFamily = "SEEDED_ASYNC_FUSION_SCHEDULES";
    private const string FusionBoundaryFamily = "FUSION_FAILURE_AND_RETRACTION_BOUNDARIES";
    private const string ProbeFamily = "CALIBRATION_AUDIO_ACCUMULATOR_BOM_PROBES";

    internal static readonly IReadOnlyDictionary<string, long> ExpectedCases =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [FactorialFamily] = 513,
            [BinomialFamily] = 33_153,
            [HyperPointFamily] = 20_475,
            [HyperNormalizationFamily] = 12_529,
            [SeededPointFamily] = 10_000,
            [AdjacentFamily] = 40_426,
            [NamedCombinatorialFamily] = 8,
            [SupportProjectionFamily] = 65_536,
            [MultiplicityProjectionFamily] = 6_561,
            [DagFamily] = 12,
            [FusionCycleFamily] = 2,
            [FusionScheduleFamily] = 512,
            [FusionBoundaryFamily] = 9,
            [ProbeFamily] = 12,
        };

    internal static readonly IReadOnlyDictionary<string, long> ExpectedChecks =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [FactorialFamily] = 1_026,
            [BinomialFamily] = 66_306,
            [HyperPointFamily] = 40_950,
            [HyperNormalizationFamily] = 25_058,
            [SeededPointFamily] = 10_000,
            [AdjacentFamily] = 271_229,
            [NamedCombinatorialFamily] = 8,
            [SupportProjectionFamily] = 393_216,
            [MultiplicityProjectionFamily] = 78_732,
            [DagFamily] = 12,
            [FusionCycleFamily] = 2,
            [FusionScheduleFamily] = 512,
            [FusionBoundaryFamily] = 9,
            [ProbeFamily] = 12,
        };

    public static Build004CampaignResult Run()
    {
        var ledger = new CampaignLedger(ExpectedCases, ExpectedChecks);
        var costs = new List<Build004StructuralCostRow>();
        var combinatorics = RunCombinatorics(ledger, costs);
        var lineage = RunLineage(ledger, costs);
        var fusion = RunFusion(ledger, lineage.Context, costs);
        var probes = RunBoundaryProbes(ledger, costs);
        var families = ledger.Freeze();
        return new Build004CampaignResult(
            combinatorics,
            lineage.Evidence,
            fusion,
            probes.Evidence,
            Array.AsReadOnly(costs.ToArray()),
            probes.AudioBytes,
            families,
            families.Sum(family => family.Checks),
            families.Sum(family => family.FailureCount),
            families.All(family => family.Status == "BOUNDED_PASS"));
    }

    private static object RunCombinatorics(
        CampaignLedger ledger,
        List<Build004StructuralCostRow> costs)
    {
        var factorialContext = new PrimeCombinatorics(512);
        var factorialOracle = BigInteger.One;
        for (var n = 0; n <= 512; n++)
        {
            ledger.Case(FactorialFamily);
            if (n > 1)
            {
                factorialOracle *= n;
            }

            var receipt = factorialContext.Factorial(n);
            ledger.Check(FactorialFamily, receipt.Value == factorialOracle, $"factorial-value:{n}");
            ledger.Check(
                FactorialFamily,
                receipt.Boundary == CombinatorialBoundaryStatus.PrimeCoordinateLocal,
                $"factorial-boundary:{n}");
        }

        var binomialContext = new PrimeCombinatorics(256);
        for (var n = 0; n <= 256; n++)
        {
            for (var k = 0; k <= n; k++)
            {
                ledger.Case(BinomialFamily);
                var structural = binomialContext.Binomial(n, k);
                var ordinary = OrdinaryExactCombinatorics.Binomial(n, k);
                ledger.Check(BinomialFamily, structural.Value == ordinary.Value, $"binomial-value:{n}:{k}");
                ledger.Check(
                    BinomialFamily,
                    structural.Value == binomialContext.Binomial(n, n - k).Value,
                    $"binomial-symmetry:{n}:{k}");
            }
        }

        var pointContext = new PrimeCombinatorics(24);
        for (var population = 0; population <= 24; population++)
        {
            for (var successes = 0; successes <= population; successes++)
            {
                for (var draws = 0; draws <= population; draws++)
                {
                    var minimum = Math.Max(0, draws - (population - successes));
                    var maximum = Math.Min(draws, successes);
                    for (var observed = minimum; observed <= maximum; observed++)
                    {
                        ledger.Case(HyperPointFamily);
                        var structural = pointContext.HypergeometricPoint(
                            population,
                            successes,
                            draws,
                            observed);
                        var ordinary = OrdinaryExactCombinatorics.HypergeometricPoint(
                            population,
                            successes,
                            draws,
                            observed);
                        ledger.Check(
                            HyperPointFamily,
                            structural.Probability == ordinary.Probability,
                            $"hyper-point:{population}:{successes}:{draws}:{observed}");
                        ledger.Check(
                            HyperPointFamily,
                            structural.Probability > ExactRational.Zero && structural.Probability <= ExactRational.One,
                            $"hyper-range:{population}:{successes}:{draws}:{observed}");
                    }
                }
            }
        }

        var normalizationContext = new PrimeCombinatorics(32);
        for (var population = 0; population <= 32; population++)
        {
            for (var successes = 0; successes <= population; successes++)
            {
                for (var draws = 0; draws <= population; draws++)
                {
                    ledger.Case(HyperNormalizationFamily);
                    var minimum = Math.Max(0, draws - (population - successes));
                    var maximum = Math.Min(draws, successes);
                    var structural = normalizationContext.HypergeometricRange(
                        population,
                        successes,
                        draws,
                        minimum,
                        maximum);
                    var ordinary = OrdinaryExactCombinatorics.HypergeometricRange(
                        population,
                        successes,
                        draws,
                        minimum,
                        maximum);
                    ledger.Check(
                        HyperNormalizationFamily,
                        structural.Probability == ExactRational.One && ordinary.Probability == ExactRational.One,
                        $"hyper-normalization:{population}:{successes}:{draws}");
                    ledger.Check(
                        HyperNormalizationFamily,
                        structural.Boundary == CombinatorialBoundaryStatus.AdditiveMagnitudeRequired &&
                        !structural.PrimeCoordinatesAvailable,
                        $"hyper-additive-boundary:{population}:{successes}:{draws}");
                }
            }
        }

        var randomContext = new PrimeCombinatorics(4_096);
        var random = new SplitMix64(Build004Protocol.MasterSeed);
        for (var trial = 0; trial < 10_000; trial++)
        {
            ledger.Case(SeededPointFamily);
            var population = (int)(random.Next() % 4_097UL);
            var successes = population == 0 ? 0 : (int)(random.Next() % (ulong)(population + 1));
            var draws = population == 0 ? 0 : (int)(random.Next() % (ulong)(population + 1));
            var minimum = Math.Max(0, draws - (population - successes));
            var maximum = Math.Min(draws, successes);
            var observed = minimum + (int)(random.Next() % (ulong)(maximum - minimum + 1));
            var structural = randomContext.HypergeometricPoint(
                population,
                successes,
                draws,
                observed);
            var ordinary = OrdinaryExactCombinatorics.HypergeometricPoint(
                population,
                successes,
                draws,
                observed);
            ledger.Check(
                SeededPointFamily,
                structural.Probability == ordinary.Probability,
                $"seeded-point:{trial}:{population}:{successes}:{draws}:{observed}");
        }

        var adjacentContext = new PrimeCombinatorics(48);
        for (var population = 0; population <= 48; population++)
        {
            for (var successes = 0; successes <= population; successes++)
            {
                for (var draws = 0; draws <= population; draws++)
                {
                    ledger.Case(AdjacentFamily);
                    var stream = OrdinaryExactCombinatorics.HypergeometricStream(population, successes, draws);
                    for (var observed = stream.SupportMinimum; observed <= stream.SupportMaximum; observed++)
                    {
                        var structural = adjacentContext.HypergeometricPoint(
                            population,
                            successes,
                            draws,
                            observed);
                        ledger.Check(
                            AdjacentFamily,
                            stream.Probabilities[observed - stream.SupportMinimum] == structural.Probability,
                            $"adjacent:{population}:{successes}:{draws}:{observed}");
                    }
                }
            }
        }

        ledger.Case(AdjacentFamily);
        var largeStream = OrdinaryExactCombinatorics.HypergeometricStream(2_000, 731, 503);
        var largeStreamContext = new PrimeCombinatorics(2_000);
        for (var observed = largeStream.SupportMinimum; observed <= largeStream.SupportMaximum; observed++)
        {
            ledger.Check(
                AdjacentFamily,
                largeStream.Probabilities[observed - largeStream.SupportMinimum] ==
                largeStreamContext.HypergeometricPoint(2_000, 731, 503, observed).Probability,
                $"adjacent-large:{observed}");
        }

        var namedContext = new PrimeCombinatorics(5_000);
        var centralBinomial = randomContext.Binomial(4_096, 2_048);
        RecordNamed(
            centralBinomial.Value == OrdinaryExactCombinatorics.Binomial(4_096, 2_048).Value,
            "central-binomial");
        var hostile = new PrimeCombinatorics(100_000).Binomial(100_000, 1);
        RecordNamed(hostile.Value == 100_000, "hostile-k-one");
        var namedPointA = namedContext.HypergeometricPoint(5_000, 1_200, 900, 225);
        RecordNamed(
            namedPointA.Probability == OrdinaryExactCombinatorics.HypergeometricPoint(5_000, 1_200, 900, 225).Probability,
            "named-point-a");
        var namedPointB = namedContext.HypergeometricPoint(1_000, 413, 271, 117);
        RecordNamed(
            namedPointB.Probability == OrdinaryExactCombinatorics.HypergeometricPoint(1_000, 413, 271, 117).Probability,
            "named-point-b");
        var zeroPoint = namedContext.HypergeometricPoint(20, 3, 4, 4);
        RecordNamed(zeroPoint.Status == CombinatorialValueStatus.ExactZero, "out-of-support-zero");
        var emptyEvent = namedContext.HypergeometricEvent(20, 8, 5, Array.Empty<int>());
        RecordNamed(emptyEvent.Probability == ExactRational.Zero, "empty-event");
        var fullEvent = namedContext.HypergeometricRange(20, 8, 5, 0, 5);
        RecordNamed(fullEvent.Probability == ExactRational.One, "full-event");
        var tailEvent = namedContext.HypergeometricRange(1_000, 413, 271, 117, 271);
        var ordinaryTail = OrdinaryExactCombinatorics.HypergeometricRange(1_000, 413, 271, 117, 271);
        RecordNamed(
            tailEvent.Probability == ordinaryTail.Probability && !tailEvent.PrimeCoordinatesAvailable,
            "tail-additive-crossing");

        AddWorkRows(costs, "COMBINATORICS", "PRIME_BASIS_0_5000", namedContext.BasisWork);
        AddWorkRows(costs, "COMBINATORICS", "HYPER_POINT_5000_1200_900_225", namedPointA.Work);
        AddWorkRows(costs, "COMBINATORICS", "HYPER_TAIL_1000_413_271_GE117", tailEvent.Work);
        AddCanonicalByteRow(costs, "COMBINATORICS", "HYPER_POINT_5000_1200_900_225", namedPointA);
        AddCanonicalByteRow(costs, "COMBINATORICS", "HYPER_TAIL_1000_413_271_GE117", tailEvent);

        return new
        {
            Schema = "prime-axiom-build004-combinatorics-v1",
            Build004Protocol.ProtocolId,
            Build004Protocol.CanonicalJsonContract,
            Construction = "LEGENDRE_FACTORIAL_VALUATIONS_AND_SIGNED_FACTORIAL_RATIOS",
            ProjectionContract = SignedPrimeCoordinates.Contract,
            ProjectionInstance = new
            {
                SignedPrimeCoordinates.Contract.Name,
                namedPointA.Coordinates.BasisId,
                namedPointA.Coordinates.Completeness,
                namedPointA.Coordinates.PayloadReplayability,
            },
            OrdinaryControls = new[]
            {
                OrdinaryExactCombinatorics.BinomialAlgorithm,
                OrdinaryExactCombinatorics.HypergeometricPointAlgorithm,
                OrdinaryExactCombinatorics.AdjacentStreamAlgorithm,
            },
            Examples = new
            {
                CentralBinomial = centralBinomial,
                HostileKOne = hostile,
                NamedPointA = namedPointA,
                NamedPointB = namedPointB,
                TailEvent = tailEvent,
            },
            Conclusions = new
            {
                Point = "SIGNED_EXPONENT_COMPOSITION_WITHOUT_RESULT_FACTORIZATION",
                Event = "ADDITIVE_DAG_OR_EXACT_MAGNITUDE_SUM_REQUIRED",
                Comparison = "CORRECTNESS_AND_OPERATION_LEDGER_ONLY__NO_UNIVERSAL_WINNER",
            },
            Build004Protocol.ClaimCeiling,
        };

        void RecordNamed(bool condition, string id)
        {
            ledger.Case(NamedCombinatorialFamily);
            ledger.Check(NamedCombinatorialFamily, condition, id);
        }
    }

    private static (object Evidence, LineageContext Context) RunLineage(
        CampaignLedger ledger,
        List<Build004StructuralCostRow> costs)
    {
        var registry = LineageRegistry.CreateSequential(
            "build004-lineage",
            "epoch-1",
            LineageOccurrenceIds);
        var atoms = registry.Registrations.Select(registration => registration.Key).ToArray();
        var supports = new SupportProjection[256];
        var products = new RawPrimeProductRepresentation[256];
        var bitsets = new DenseBitSetRepresentation[256];
        var sparseSupports = new SparseExponentRepresentation[256];
        for (var mask = 0; mask < 256; mask++)
        {
            var active = atoms.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
            supports[mask] = SupportProjection.Create(registry, active);
            products[mask] = RawPrimeProductRepresentation.Encode(supports[mask], registry);
            bitsets[mask] = DenseBitSetRepresentation.Encode(supports[mask], registry);
            sparseSupports[mask] = SparseExponentRepresentation.EncodeSupport(supports[mask], registry);
        }

        for (var left = 0; left < 256; left++)
        {
            for (var right = 0; right < 256; right++)
            {
                ledger.Case(SupportProjectionFamily);
                var expectedUnion = supports[left].Union(supports[right]);
                var expectedIntersection = supports[left].Intersect(supports[right]);
                ledger.Check(
                    SupportProjectionFamily,
                    products[left].Union(products[right], registry).Support.Equals(expectedUnion),
                    $"product-union:{left}:{right}");
                ledger.Check(
                    SupportProjectionFamily,
                    products[left].Intersect(products[right], registry).Support.Equals(expectedIntersection),
                    $"product-intersection:{left}:{right}");
                ledger.Check(
                    SupportProjectionFamily,
                    bitsets[left].Union(bitsets[right]).Decode(registry).Equals(expectedUnion),
                    $"bitset-union:{left}:{right}");
                ledger.Check(
                    SupportProjectionFamily,
                    bitsets[left].Intersect(bitsets[right]).Decode(registry).Equals(expectedIntersection),
                    $"bitset-intersection:{left}:{right}");
                ledger.Check(
                    SupportProjectionFamily,
                    sparseSupports[left].Max(sparseSupports[right], registry).ToProjection(registry)
                        .Entries.Select(entry => entry.Atom).SequenceEqual(expectedUnion.Atoms),
                    $"sparse-union:{left}:{right}");
                ledger.Check(
                    SupportProjectionFamily,
                    sparseSupports[left].Min(sparseSupports[right], registry).ToProjection(registry)
                        .Entries.Select(entry => entry.Atom).SequenceEqual(expectedIntersection.Atoms),
                    $"sparse-intersection:{left}:{right}");
            }
        }

        var multiplicities = new MultiplicityProjection[81];
        var sparseMultiplicities = new SparseExponentRepresentation[81];
        for (var encoded = 0; encoded < 81; encoded++)
        {
            var remaining = encoded;
            var entries = new List<AtomMultiplicity>();
            for (var index = 0; index < 4; index++)
            {
                var count = remaining % 3;
                remaining /= 3;
                if (count > 0)
                {
                    entries.Add(new AtomMultiplicity(atoms[index], count));
                }
            }

            multiplicities[encoded] = MultiplicityProjection.Create(registry, entries);
            sparseMultiplicities[encoded] = SparseExponentRepresentation.Encode(multiplicities[encoded]);
        }

        for (var left = 0; left < 81; left++)
        {
            for (var right = 0; right < 81; right++)
            {
                ledger.Case(MultiplicityProjectionFamily);
                var added = sparseMultiplicities[left].Add(sparseMultiplicities[right], registry).ToProjection(registry);
                var minimum = sparseMultiplicities[left].Min(sparseMultiplicities[right], registry).ToProjection(registry);
                var maximum = sparseMultiplicities[left].Max(sparseMultiplicities[right], registry).ToProjection(registry);
                for (var index = 0; index < 4; index++)
                {
                    ledger.Check(
                        MultiplicityProjectionFamily,
                        added.GetCount(atoms[index]) ==
                        multiplicities[left].GetCount(atoms[index]) + multiplicities[right].GetCount(atoms[index]),
                        $"multiplicity-add:{left}:{right}:{index}");
                    ledger.Check(
                        MultiplicityProjectionFamily,
                        minimum.GetCount(atoms[index]) == Math.Min(
                            multiplicities[left].GetCount(atoms[index]),
                            multiplicities[right].GetCount(atoms[index])),
                        $"multiplicity-min:{left}:{right}:{index}");
                    ledger.Check(
                        MultiplicityProjectionFamily,
                        maximum.GetCount(atoms[index]) == Math.Max(
                            multiplicities[left].GetCount(atoms[index]),
                            multiplicities[right].GetCount(atoms[index])),
                        $"multiplicity-max:{left}:{right}:{index}");
                }
            }
        }

        var descriptors = atoms.Select((key, index) => new AtomDescriptor(
            key,
            $"source-{index.ToString(CultureInfo.InvariantCulture)}",
            HashText($"payload-{index.ToString(CultureInfo.InvariantCulture)}"))).ToArray();
        var dag = new DerivationDag();
        var node = descriptors.Select(dag.AddAtom).ToArray();
        var ab = dag.AddJoint(node[0], node[1]);
        var ac = dag.AddJoint(node[0], node[2]);
        var bd = dag.AddJoint(node[1], node[3]);
        var cd = dag.AddJoint(node[2], node[3]);
        var first = dag.AddAlternative(ab, cd);
        var second = dag.AddAlternative(ac, bd);
        var firstSupport = dag.ProjectSupport(first, registry);
        var firstMultiplicity = dag.ProjectMultiplicity(first, registry);
        var secondSupport = dag.ProjectSupport(second, registry);
        var secondMultiplicity = dag.ProjectMultiplicity(second, registry);

        DagCase(dag.AddJoint(node[0], node[1]) == dag.AddJoint(node[1], node[0]), "joint-child-order");
        DagCase(
            dag.AddJoint(node[0], dag.AddJoint(node[1], node[2])) ==
            dag.AddJoint(dag.AddJoint(node[0], node[1]), node[2]),
            "joint-associative-flattening");
        DagCase(first != second && firstSupport.Equals(secondSupport), "same-support-different-derivation");
        DagCase(firstMultiplicity.Equals(secondMultiplicity), "same-multiplicity-different-pairing");
        DagCase(dag.AddAlternative(node[0], node[0]) != node[0], "alternative-multiplicity");
        DagCase(dag.AddJoint(node[0], node[0]) != node[0], "joint-multiplicity");
        var distributedLeft = dag.AddJoint(node[0], dag.AddAlternative(node[1], node[2]));
        var distributedRight = dag.AddAlternative(dag.AddJoint(node[0], node[1]), dag.AddJoint(node[0], node[2]));
        DagCase(distributedLeft != distributedRight, "semantic-equality-structural-distinction");
        var retraction = dag.SpecializeAtomToZero(first, atoms[0]);
        DagCase(retraction.IsSuccess && retraction.RootNodeId == cd, "retraction-specialization");
        var transform = dag.AddTransform("OPAQUE-UDF-V1", node[0]);
        DagCase(
            dag.SpecializeAtomToZero(transform, atoms[0]).Status == DagRetractionStatus.NonInvertibleTransform,
            "opaque-transform-retraction");
        DagCase(dag.Verify(first, registry, firstSupport, firstMultiplicity).IsValid, "dag-valid-replay");

        var snapshots = dag.Snapshots;
        var atomSnapshot = snapshots.Single(snapshot => snapshot.NodeId == node[0]);
        var mutatedAtom = atomSnapshot with
        {
            Atom = new AtomDescriptor(atoms[0], descriptors[0].SourceId, HashText("mutated-payload")),
        };
        DagCase(
            DerivationDag.VerifySnapshots(
                snapshots.Select(snapshot => snapshot.NodeId == node[0] ? mutatedAtom : snapshot),
                first,
                registry).Status == DagVerificationStatus.HashMismatch,
            "dag-payload-mutation");
        var wrongCache = SupportProjection.Create(registry, new[] { atoms[0] });
        DagCase(
            dag.Verify(first, registry, wrongCache).Status == DagVerificationStatus.CachedSupportMismatch,
            "dag-cache-mutation");

        foreach (var universe in new[] { 8, 64, 256, 1_024 })
        {
            var costRegistry = LineageRegistry.CreateSequential(
                "cost-universe",
                $"u-{universe.ToString(CultureInfo.InvariantCulture)}",
                Enumerable.Range(0, universe).Select(index => $"atom-{index:D4}"));
            var support = SupportProjection.Create(
                costRegistry,
                costRegistry.Registrations.Select(registration => registration.Key));
            var product = RawPrimeProductRepresentation.Encode(support, costRegistry);
            var sparse = SparseExponentRepresentation.EncodeSupport(support, costRegistry);
            var dense = DenseBitSetRepresentation.Encode(support, costRegistry);
            costs.Add(new Build004StructuralCostRow(
                AbstractStructureLedger,
                "LINEAGE",
                $"FULL_SUPPORT_U{universe}",
                "raw_prime_product_width",
                product.BitLength.ToString(CultureInfo.InvariantCulture),
                "bits",
                "Arbitrary-width product materialization; abstract identity remains exact regardless of width.",
                "VARIABLE_WIDTH_DATAPATH_REQUIRED__NAND_DFF_PPA_NOT_MEASURED"));
            costs.Add(new Build004StructuralCostRow(
                AbstractStructureLedger,
                "LINEAGE",
                $"FULL_SUPPORT_U{universe}",
                "dense_pev_width",
                universe.ToString(CultureInfo.InvariantCulture),
                "bits",
                "Direct registered membership vector.",
                "CONCEPTUAL_PARALLEL_AND_OR_DEPTH_1__NAND_DFF_PPA_NOT_MEASURED"));
            costs.Add(new Build004StructuralCostRow(
                AbstractStructureLedger,
                "LINEAGE",
                $"FULL_SUPPORT_U{universe}",
                "sparse_entries",
                universe.ToString(CultureInfo.InvariantCulture),
                "entries",
                "Keyed support; useful only when active support is sparse.",
                "LOOKUP_AND_ROUTING_REQUIRED__NAND_DFF_PPA_NOT_MEASURED"));
            AddLineageCost(
                $"FULL_SUPPORT_U{universe}",
                "input_universe",
                universe,
                "registered_atoms",
                "Declared registry domain size.",
                "REGISTRY_STORAGE_AND_ROUTING__NAND_DFF_PPA_NOT_MEASURED");
            AddLineageCost(
                $"FULL_SUPPORT_U{universe}",
                "active_atoms",
                universe,
                "atoms",
                "Every registered atom is active in this full-support case.",
                "STATE_ACTIVITY_AND_SWITCHING_NOT_MEASURED");
            AddLineageCost(
                $"FULL_SUPPORT_U{universe}",
                "total_multiplicity",
                universe,
                "occurrences",
                "Each active atom occurs exactly once.",
                "EXPONENT_STORAGE_COST_NOT_MEASURED");
            AddLineageCost(
                $"FULL_SUPPORT_U{universe}",
                "maximum_multiplicity",
                1,
                "occurrences_per_atom",
                "Full-support width case is presence-only.",
                "EXPONENT_STORAGE_COST_NOT_MEASURED");
            AddLineageCost(
                $"FULL_SUPPORT_U{universe}",
                "dense_pev_words",
                (universe + 63) / 64,
                "uint64_words",
                "Concrete managed dense-bitset storage words, excluding object metadata.",
                "SOFTWARE_LAYOUT_ONLY__NAND_DFF_PPA_NOT_MEASURED");
            AddCanonicalByteRow(costs, "LINEAGE", $"FULL_SUPPORT_U{universe}", "explicit_set_canonical_utf8_bytes", support);
            AddCanonicalByteRow(costs, "LINEAGE", $"FULL_SUPPORT_U{universe}", "raw_prime_product_canonical_utf8_bytes", product);
            AddCanonicalByteRow(costs, "LINEAGE", $"FULL_SUPPORT_U{universe}", "sparse_exponent_canonical_utf8_bytes", sparse);
            AddCanonicalByteRow(costs, "LINEAGE", $"FULL_SUPPORT_U{universe}", "dense_pev_canonical_utf8_bytes", dense);
            AddPhysicalCost(
                $"FULL_SUPPORT_U{universe}",
                "materialized_raw_product_datapath_width",
                product.BitLength,
                "bits",
                "Conceptual datapath width copied from the exact abstract product width; no circuit was built.",
                "VARIABLE_WIDTH_DATAPATH_REQUIRED__NAND_DFF_PPA_NOT_MEASURED");
            AddPhysicalCost(
                $"FULL_SUPPORT_U{universe}",
                "dense_pev_state_width",
                universe,
                "bits",
                "Conceptual fixed-registry presence state width; no register bank was built.",
                "DENSE_STATE_WIDTH_ONLY__NAND_DFF_PPA_NOT_MEASURED");
            AddPhysicalCost(
                $"FULL_SUPPORT_U{universe}",
                "conceptual_boolean_union_intersection_depth",
                1,
                "boolean_levels",
                "One dependency level of bitwise AND or OR before gate mapping; no timing path was built.",
                "CONCEPTUAL_PARALLEL_BOOLEAN_DEPTH_1__NAND_DFF_PPA_NOT_MEASURED");
            AddPhysicalCost(
                $"FULL_SUPPORT_U{universe}",
                "sparse_lookup_routing_entries",
                universe,
                "entries",
                "Full-support lookup and routing obligation; no sparse hardware was built.",
                "SPARSE_LOOKUP_AND_ROUTING_REQUIRED__NAND_DFF_PPA_NOT_MEASURED");
        }

        var metrics = dag.GetMetrics(first);
        costs.Add(new Build004StructuralCostRow(
            AbstractStructureLedger,
            "LINEAGE",
            "A_B_PLUS_C_D",
            "reachable_dag_nodes",
            metrics.NodeCount.ToString(CultureInfo.InvariantCulture),
            "nodes",
            "Persistent derivation object retains pairing hidden by support/multiplicity projections.",
            "GRAPH_MEMORY_AND_HASH_TRAFFIC__NOT_MEASURED"));
        costs.Add(new Build004StructuralCostRow(
            AbstractStructureLedger,
            "LINEAGE",
            "A_B_PLUS_C_D",
            "reachable_dag_edges",
            metrics.EdgeCount.ToString(CultureInfo.InvariantCulture),
            "edges",
            "Replay and retraction traversal obligation.",
            "GRAPH_MEMORY_AND_HASH_TRAFFIC__NOT_MEASURED"));
        AddLineageCost(
            "A_B_PLUS_C_D",
            "maximum_depth",
            metrics.MaximumDepth,
            "nodes",
            "Maximum reachable derivation depth.",
            "GRAPH_MEMORY_AND_HASH_TRAFFIC__NOT_MEASURED");
        AddLineageCost(
            "A_B_PLUS_C_D",
            "campaign_hash_cons_reuse",
            metrics.HashConsReuseCount,
            "reuse_events",
            "Global reuse events in the named campaign DAG before the metric snapshot; not intrinsic to this root.",
            "HASH_TABLE_AND_MEMORY_COST_NOT_MEASURED");
        AddLineageCost(
            "A_B_PLUS_C_D",
            "active_atoms",
            firstSupport.Atoms.Count,
            "atoms",
            "Unique active source occurrences.",
            "STATE_ACTIVITY_AND_SWITCHING_NOT_MEASURED");
        AddLineageCost(
            "A_B_PLUS_C_D",
            "syntactic_occurrences",
            firstMultiplicity.Entries.Sum(entry => entry.Count),
            "occurrences",
            "Total source occurrences across the positive expression.",
            "EXPONENT_STORAGE_COST_NOT_MEASURED");
        AddLineageCost(
            "RETRACTION_BOUNDARY",
            "constructed_transform_nodes",
            1,
            "nodes",
            "One explicit opaque transform is constructed for the noninvertible retraction boundary.",
            "TRANSFORM_IMPLEMENTATION_NOT_MEASURED");
        AddCanonicalByteRow(
            costs,
            "LINEAGE",
            "A_B_PLUS_C_D",
            "reachable_dag_receipt_canonical_utf8_bytes",
            new
            {
                Schema = DerivationDag.Schema,
                RootNodeId = first,
                Nodes = dag.GetReachableSnapshots(first),
            });
        AddLoss("EXPLICIT_SET_SUPPORT", SupportProjection.Contract.Discards);
        AddLoss("MULTIPLICITY_PROJECTION", MultiplicityProjection.Contract.Discards);
        AddLoss("RAW_PRIME_PRODUCT_SUPPORT", RawPrimeProductRepresentation.Contract.Discards);
        AddLoss("SPARSE_ATOM_EXPONENTS", SparseExponentRepresentation.Contract.Discards);
        AddLoss("DENSE_BINARY_PEV_SUPPORT", DenseBitSetRepresentation.Contract.Discards);
        AddLoss(
            "PERSISTENT_TYPED_DAG",
            "Issuer authenticity, empirical validity, and external payload availability remain outside the graph receipt.");
        AddPhysicalCost(
            "A_B_PLUS_C_D",
            "dag_hash_memory_traffic_obligation",
            1,
            "declared_obligation",
            "The retained graph requires node/hash storage and traversal if physically materialized; traffic was not measured.",
            "DAG_AND_HASH_MEMORY_TRAFFIC_REQUIRED__NAND_DFF_PPA_NOT_MEASURED");

        var diagnosticDag = new DerivationDag();
        diagnosticDag.ResetDiagnostics();
        var diagnosticAtoms = descriptors.Take(4).Select(diagnosticDag.AddAtom).ToArray();
        var diagnosticRoot = diagnosticDag.AddAlternative(
            diagnosticDag.AddJoint(diagnosticAtoms[0], diagnosticAtoms[1]),
            diagnosticDag.AddJoint(diagnosticAtoms[2], diagnosticAtoms[3]));
        var diagnosticSupport = diagnosticDag.ProjectSupport(diagnosticRoot, registry);
        var diagnosticMultiplicity = diagnosticDag.ProjectMultiplicity(diagnosticRoot, registry);
        var diagnosticVerification = diagnosticDag.Verify(
            diagnosticRoot,
            registry,
            diagnosticSupport,
            diagnosticMultiplicity);
        if (!diagnosticVerification.IsValid)
        {
            throw new InvalidOperationException("The canonical DAG diagnostic path did not verify.");
        }

        var diagnostics = diagnosticDag.Diagnostics;
        var replayableProjection = SupportProjection.Create(
            registry,
            firstSupport.Atoms,
            LineageCompleteness.Exact,
            PayloadReplayability.ReplayableExact);
        var digestOnlyProjection = SupportProjection.Create(
            registry,
            firstSupport.Atoms,
            LineageCompleteness.Exact,
            PayloadReplayability.DigestOnly);
        var digestOnlyRawProduct = RawPrimeProductRepresentation.Encode(digestOnlyProjection, registry);
        var digestOnlySparse = SparseExponentRepresentation.EncodeSupport(digestOnlyProjection, registry);
        var digestOnlyDense = DenseBitSetRepresentation.Encode(digestOnlyProjection, registry);
        AddDiagnostic("map_reads", diagnostics.MapReads, "operations");
        AddDiagnostic("map_writes", diagnostics.MapWrites, "operations");
        AddDiagnostic("semantic_hash_computations", diagnostics.SemanticHashComputations, "operations");
        AddDiagnostic("dag_node_visits", diagnostics.DagNodeVisits, "visits_after_memo_miss");
        AddDiagnostic("projection_queries", diagnostics.ProjectionQueries, "queries");
        AddDiagnostic("cache_verification_requests", diagnostics.CacheVerificationRequests, "requests");
        AddDiagnostic("cache_verification_passes", diagnostics.CacheVerificationPasses, "passes");

        var evidenceEnvelope = LineageEvidenceEnvelope.Create(
            dag,
            first,
            registry,
            LineageCompleteness.Exact,
            PayloadReplayability.DigestOnly,
            IssuerAuthenticity.NotProvided,
            new[]
            {
                new LineageEvidenceReference(
                    LineageEvidenceReferenceKind.Source,
                    "fixture:build004/source-catalog-v1",
                    HashText("BUILD004_SYNTHETIC_SOURCE_CATALOG_V1")),
                new LineageEvidenceReference(
                    LineageEvidenceReferenceKind.CalibrationValidity,
                    "fixture:build004/calibration-validity-v1",
                    HashText("BUILD004_SYNTHETIC_CALIBRATION_VALIDITY_V1")),
                new LineageEvidenceReference(
                    LineageEvidenceReferenceKind.Uncertainty,
                    "fixture:build004/uncertainty-v1",
                    HashText("BUILD004_SYNTHETIC_UNCERTAINTY_V1")),
                new LineageEvidenceReference(
                    LineageEvidenceReferenceKind.Residual,
                    "fixture:build004/residual-v1",
                    HashText("BUILD004_SYNTHETIC_RESIDUAL_V1")),
            });
        var evidenceEnvelopeSnapshot = evidenceEnvelope.ToSnapshot();
        var evidenceEnvelopeVerification = LineageEvidenceEnvelope.VerifySnapshot(
            evidenceEnvelopeSnapshot,
            dag.GetReachableSnapshots(first),
            registry);
        if (!evidenceEnvelopeVerification.IsValid ||
            !evidenceEnvelopeVerification.EnvelopeHashMatches ||
            !evidenceEnvelopeVerification.RegistryBindingMatches ||
            evidenceEnvelopeVerification.DagReplay?.IsValid != true)
        {
            throw new InvalidOperationException("The bounded lineage evidence envelope did not replay.");
        }

        var context = new LineageContext(registry, atoms, descriptors);
        var evidence = new
        {
            Schema = "prime-axiom-build004-lineage-v1",
            Build004Protocol.ProtocolId,
            Build004Protocol.CanonicalJsonContract,
            Registry = new
            {
                registry.RegistryId,
                registry.NamespaceId,
                registry.AssignmentEpoch,
                registry.UniverseSize,
            },
            ProjectionContracts = new[]
            {
                SupportProjection.Contract,
                MultiplicityProjection.Contract,
                RawPrimeProductRepresentation.Contract,
                SparseExponentRepresentation.Contract,
                DenseBitSetRepresentation.Contract,
            },
            ProjectionInstances = new[]
            {
                new { Representation = SupportProjection.Contract.Name, digestOnlyProjection.RegistryId, digestOnlyProjection.Completeness, digestOnlyProjection.PayloadReplayability },
                new { Representation = MultiplicityProjection.Contract.Name, firstMultiplicity.RegistryId, firstMultiplicity.Completeness, firstMultiplicity.PayloadReplayability },
                new { Representation = RawPrimeProductRepresentation.Contract.Name, digestOnlyRawProduct.RegistryId, Completeness = digestOnlyRawProduct.Support.Completeness, digestOnlyRawProduct.PayloadReplayability },
                new { Representation = SparseExponentRepresentation.Contract.Name, digestOnlySparse.RegistryId, digestOnlySparse.Completeness, digestOnlySparse.PayloadReplayability },
                new { Representation = DenseBitSetRepresentation.Contract.Name, digestOnlyDense.RegistryId, digestOnlyDense.Completeness, digestOnlyDense.PayloadReplayability },
            },
            StructuralCounterexample = new
            {
                First = "a*b + c*d",
                FirstRoot = first,
                Second = "a*c + b*d",
                SecondRoot = second,
                SameSupport = firstSupport.Equals(secondSupport),
                SameMultiplicity = firstMultiplicity.Equals(secondMultiplicity),
                SameDerivation = first == second,
                Metrics = metrics,
            },
            ReplayabilityAxis = new
            {
                SameAtoms = replayableProjection.Atoms.SequenceEqual(digestOnlyProjection.Atoms),
                SameCompleteness = replayableProjection.Completeness == digestOnlyProjection.Completeness,
                Replayable = replayableProjection.PayloadReplayability,
                DigestOnly = digestOnlyProjection.PayloadReplayability,
                DistinctProjectionIdentity = !replayableProjection.Equals(digestOnlyProjection),
                Boundary = "DECLARATION_PRESERVED__NOT_PROOF_OF_PAYLOAD_AVAILABILITY",
            },
            CanonicalDiagnostic = new
            {
                Scope = "CONSTRUCT_A_B_PLUS_C_D__PROJECT_SUPPORT_AND_MULTIPLICITY__VERIFY_BOTH_CACHES",
                DerivationDagDiagnostics.CountingContract,
                Diagnostics = diagnostics,
            },
            EvidenceEnvelope = new
            {
                Snapshot = evidenceEnvelopeSnapshot,
                Verification = evidenceEnvelopeVerification,
                LineageEvidenceEnvelope.SecurityBoundary,
                EstablishesIssuerAuthentication =
                    LineageEvidenceEnvelopeVerificationResult.EstablishesIssuerAuthentication,
                Scope = "BINDS_DAG_ROOT_REGISTRY_AND_EXTERNAL_REFERENCE_DIGESTS__NUMERIC_FACTOR_AND_UNIT_DIMENSION_PROBES_REMAIN_SEPARATE",
            },
            DatabaseBoundary =
                "PROVENANCE_IS_IRREDUCIBLE_RELATIVE_TO_SCALAR_EVALUATION__POSITIVE_ALGEBRA_ONLY",
            Authentication = IssuerAuthenticity.NotProvided,
            Build004Protocol.ClaimCeiling,
        };
        return (evidence, context);

        void DagCase(bool condition, string id)
        {
            ledger.Case(DagFamily);
            ledger.Check(DagFamily, condition, id);
        }

        void AddLineageCost(
            string caseId,
            string metric,
            long value,
            string unit,
            string softwareMeaning,
            string hardwareImplication,
            string ledgerClass = AbstractStructureLedger) =>
            costs.Add(new Build004StructuralCostRow(
                ledgerClass,
                "LINEAGE",
                caseId,
                metric,
                value.ToString(CultureInfo.InvariantCulture),
                unit,
                softwareMeaning,
                hardwareImplication));

        void AddDiagnostic(string metric, long value, string unit) => AddLineageCost(
            "CANONICAL_DAG_DIAGNOSTIC",
            metric,
            value,
            unit,
            "Instrumented instance counter under the declared canonical diagnostic scope.",
            "HOST_IMPLEMENTATION_COUNTER__NAND_DFF_PPA_NOT_MEASURED",
            metric == "projection_queries" ? AbstractStructureLedger : HostSoftwareLedger);

        void AddLoss(string caseId, string declaredLoss) => costs.Add(new Build004StructuralCostRow(
            AbstractStructureLedger,
            "LINEAGE_LOSS",
            caseId,
            "declared_information_loss",
            declaredLoss,
            "loss_contract",
            "Semantic information deliberately absent from this projection or receipt.",
            "SEMANTIC_LOSS_ONLY__HARDWARE_NOT_MEASURED"));

        void AddPhysicalCost(
            string caseId,
            string metric,
            long value,
            string unit,
            string softwareMeaning,
            string hardwareImplication) => costs.Add(new Build004StructuralCostRow(
            PhysicalHardwareLedger,
            "PHYSICAL",
            caseId,
            metric,
            value.ToString(CultureInfo.InvariantCulture),
            unit,
            softwareMeaning,
            hardwareImplication));
    }

    private static object RunFusion(
        CampaignLedger ledger,
        LineageContext context,
        List<Build004StructuralCostRow> costs)
    {
        var engine = new LineageFusionEngine(context.Registry);
        var evidence = context.Descriptors.Select((descriptor, index) => new FusionAtomEvidence(
            descriptor,
            new TwoStateLikelihood(
                new ExactPositiveRational(index + 2, index + 1),
                new ExactPositiveRational((index + 1) * 2, index + 2)))).ToArray();
        var a = engine.CreateState(new[] { evidence[0] });
        var b = engine.CreateState(new[] { evidence[1] });
        var c = engine.CreateState(new[] { evidence[2] });
        var d = engine.CreateState(new[] { evidence[3] });
        var ab = engine.CreateState(new[] { evidence[0], evidence[1] });
        var bc = engine.CreateState(new[] { evidence[1], evidence[2] });
        var ac = engine.CreateState(new[] { evidence[0], evidence[2] });
        var abc = engine.CreateState(new[] { evidence[0], evidence[1], evidence[2] });
        var abWithBc = engine.MergeUnique(ab, bc);
        var stabilized = abWithBc.IsSuccess ? engine.MergeUnique(abWithBc.State!, ac) : null;
        FusionCycleCase(
            stabilized is not null && stabilized.IsSuccess &&
            stabilized.State!.ExpressionRootId == abc.ExpressionRootId &&
            stabilized.State.Payload == abc.Payload,
            "primex-cycle-no-new-stabilizes");
        AddFusionCost(
            "ABC_EXACT_CYCLE",
            "unique_active_atoms",
            abc.Support.Atoms.Count,
            "atoms",
            "Exact unique source occurrences retained by the centralized oracle state.");
        AddFusionCost(
            "ABC_EXACT_CYCLE",
            "exact_payload_dependencies",
            abc.Atoms.Count,
            "payloads",
            "Exact fusion remains replay-dependent on every unique atomic likelihood payload.");
        AddFusionCost(
            "AB_MERGE_BC",
            "exact_overlap_payload_dependencies",
            abWithBc.ExactOverlap?.Atoms.Count ?? 0,
            "payloads",
            "Shared ancestry identity and its exact likelihood payload are separate obligations.");
        AddCanonicalByteRow(
            costs,
            "FUSION",
            "ABC_EXACT_CYCLE",
            "canonical_state_receipt_utf8_bytes",
            abc);
        var withNew = stabilized is null || !stabilized.IsSuccess
            ? null
            : engine.MergeUnique(stabilized.State!, d);
        FusionCycleCase(
            withNew is not null && withNew.IsSuccess && withNew.State!.Support.Count == 4,
            "cycle-with-new-occurrence");

        var messages = new[] { a, b, c, ab, bc, ac, a, b, c, ab, bc, ac };
        var random = new SplitMix64(Build004Protocol.MasterSeed ^ 0xF0510AUL);
        for (var schedule = 0; schedule < 512; schedule++)
        {
            ledger.Case(FusionScheduleFamily);
            var shuffled = messages.ToArray();
            for (var index = shuffled.Length - 1; index > 0; index--)
            {
                var swap = (int)(random.Next() % (ulong)(index + 1));
                (shuffled[index], shuffled[swap]) = (shuffled[swap], shuffled[index]);
            }

            var state = engine.CreateState(Array.Empty<FusionAtomEvidence>());
            var succeeded = true;
            foreach (var message in shuffled)
            {
                var merged = engine.MergeUnique(state, message);
                if (!merged.IsSuccess)
                {
                    succeeded = false;
                    break;
                }

                state = merged.State!;
            }

            ledger.Check(
                FusionScheduleFamily,
                succeeded && state.ExpressionRootId == abc.ExpressionRootId && state.Payload == abc.Payload,
                $"async-schedule:{schedule}");
        }

        var nonReplayableA = FusionAtomEvidence.DigestOnly(evidence[0].Descriptor);
        var missingState = engine.CreateState(
            new[] { nonReplayableA },
            retainedAggregatePayload: evidence[0].Likelihood);
        var missingAb = engine.CreateState(
            new[] { nonReplayableA, evidence[1] },
            retainedAggregatePayload: ab.Payload);
        var missingMerge = engine.MergeUnique(missingState, missingAb);
        var digestOnlyB = FusionAtomEvidence.DigestOnly(evidence[1].Descriptor);
        var opaqueUniqueRight = engine.CreateState(
            new[] { evidence[0], digestOnlyB },
            retainedAggregatePayload: ab.Payload);
        var opaqueUniqueMerge = engine.MergeUnique(a, opaqueUniqueRight);
        var opaqueUniqueExternalOracleMatched = opaqueUniqueMerge.IsSuccess &&
            opaqueUniqueMerge.State?.Payload ==
            LineageFusionEngine.ComputeCentralizedUniqueAtomOracle(evidence.Take(2));
        FusionBoundaryCase(
            missingMerge.Status == ExactLineageFusionStatus.OverlapPayloadUnavailable &&
            missingMerge.ExactOverlap?.Count == 1,
            "overlap-identified-payload-unavailable");

        var conflictingDescriptor = new AtomDescriptor(
            context.Atoms[0],
            evidence[0].Descriptor.SourceId,
            HashText("conflicting-payload"));
        var conflictingState = engine.CreateState(new[]
        {
            new FusionAtomEvidence(conflictingDescriptor, evidence[0].Likelihood!),
        });
        FusionBoundaryCase(
            engine.MergeUnique(a, conflictingState).Status == ExactLineageFusionStatus.ConflictingAtomPayload,
            "atom-id-payload-conflict");

        var partial = engine.CreateState(new[] { evidence[0] }, LineageCompleteness.KnownLowerBound);
        FusionBoundaryCase(
            engine.MergeUnique(partial, b).Status == ExactLineageFusionStatus.PartialLineage,
            "partial-lineage-blocked");

        var authenticationRequirement = engine.RequireVerifiedIssuerAuthentication(a);
        FusionBoundaryCase(
            authenticationRequirement.Status == ExactLineageFusionStatus.AuthenticationNotProvided &&
            authenticationRequirement.State is null,
            "authentication-not-provided-typed-failure");

        var epochRegistry = LineageRegistry.CreateSequential(
            context.Registry.NamespaceId,
            "epoch-2",
            LineageOccurrenceIds);
        var epochEngine = new LineageFusionEngine(epochRegistry);
        var epochDescriptor = new AtomDescriptor(
            epochRegistry.Registrations[0].Key,
            "source-0",
            evidence[0].Descriptor.PayloadSha256);
        var epochState = epochEngine.CreateState(new[]
        {
            new FusionAtomEvidence(epochDescriptor, evidence[0].Likelihood!),
        });
        FusionBoundaryCase(
            engine.MergeUnique(a, epochState).Status == ExactLineageFusionStatus.RegistryEpochMismatch,
            "delayed-prior-epoch-blocked");

        var exactRetraction = engine.RetractExactProduct(ab, context.Atoms[0]);
        FusionBoundaryCase(
            exactRetraction.IsSuccess && exactRetraction.State!.ExpressionRootId == b.ExpressionRootId,
            "exact-product-retraction");
        FusionBoundaryCase(
            engine.RetractExactProduct(missingState, context.Atoms[0]).Status ==
            ExactLineageFusionStatus.MissingRetractionWitness,
            "missing-retraction-witness");
        FusionBoundaryCase(
            engine.RetractExactProduct(ab, context.Atoms[7]).Status == ExactLineageFusionStatus.AtomNotPresent,
            "absent-retraction");
        FusionBoundaryCase(
            LineageFusionEngine.ComputeCentralizedUniqueAtomOracle(evidence.Take(3)) == abc.Payload,
            "centralized-unique-atom-oracle");

        return new
        {
            Schema = "prime-axiom-build004-fusion-v1",
            Build004Protocol.ProtocolId,
            ExactCycle = new
            {
                AB = ab.ExpressionRootId,
                BC = bc.ExpressionRootId,
                AC = ac.ExpressionRootId,
                ABC = abc.ExpressionRootId,
                Stabilized = stabilized?.State?.ExpressionRootId,
                ProbabilityOfOne = abc.Payload.ProbabilityOfOne,
            },
            FailureReceipts = new[]
            {
                new { Case = "OVERLAP_IDENTIFIED_PAYLOAD_EVICTED_FROM_BOTH_STATES", Status = missingMerge.Status.ToString(), missingMerge.Detail },
                new { Case = "ATOM_ID_PAYLOAD_CONFLICT", Status = engine.MergeUnique(a, conflictingState).Status.ToString(), Detail = "Atomic failure; no destination state issued." },
                new { Case = "PARTIAL_LINEAGE", Status = engine.MergeUnique(partial, b).Status.ToString(), Detail = "Lower-bound support cannot earn exact overlap." },
                new { Case = "RECYCLED_EPOCH", Status = engine.MergeUnique(a, epochState).Status.ToString(), Detail = "Explicit migration required." },
                new { Case = "AUTHENTICATION_REQUIRED_NOT_PROVIDED", Status = authenticationRequirement.Status.ToString(), authenticationRequirement.Detail },
            },
            IndependentAxes = new
            {
                Lineage = abc.LineageCompleteness,
                Payload = abc.PayloadReplayability,
                Authenticity = abc.IssuerAuthenticity,
            },
            OpaqueUniqueLeaf = new
            {
                Status = opaqueUniqueMerge.Status,
                ExternalOracleMatched = opaqueUniqueExternalOracleMatched,
                PayloadReplayability = opaqueUniqueMerge.State?.PayloadReplayability,
                Boundary = "RETAINED_AGGREGATE_PLUS_REPLAYABLE_OVERLAP__EXTERNAL_ORACLE_REQUIRED_FOR_DIGEST_ONLY_UNIQUE_LEAF",
            },
            LineageFusionEngine.SecurityBoundary,
            Build004Protocol.ClaimCeiling,
        };

        void FusionCycleCase(bool condition, string id)
        {
            ledger.Case(FusionCycleFamily);
            ledger.Check(FusionCycleFamily, condition, id);
        }

        void FusionBoundaryCase(bool condition, string id)
        {
            ledger.Case(FusionBoundaryFamily);
            ledger.Check(FusionBoundaryFamily, condition, id);
        }

        void AddFusionCost(
            string caseId,
            string metric,
            long value,
            string unit,
            string softwareMeaning) =>
            costs.Add(new Build004StructuralCostRow(
                AbstractStructureLedger,
                "FUSION",
                caseId,
                metric,
                value.ToString(CultureInfo.InvariantCulture),
                unit,
                softwareMeaning,
                "DISTRIBUTED_STORAGE_TRANSFER_AND_HARDWARE_COST_NOT_MEASURED"));
    }

    private static (object Evidence, byte[] AudioBytes) RunBoundaryProbes(
        CampaignLedger ledger,
        List<Build004StructuralCostRow> costs)
    {
        var evaluatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var validEvidence = new ProbeCalibrationEvidenceEnvelope(
            "defined-ratio",
            "BUILD004_BOUNDED_FIXTURE",
            evaluatedAt.AddYears(-1),
            evaluatedAt.AddYears(1),
            ProbeCoefficientStatus.ExactDefined,
            ProbeUncertaintyKind.NoneDeclared,
            "No empirical uncertainty: exact defined fixture.",
            ProbeEvidenceAuthentication.Unauthenticated,
            string.Empty);
        var dimensionless = ProbeUnitDimensionVector.Dimensionless;
        var twoThirds = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "ratio-2-3",
            new ProbeExactRatio(2, 3),
            dimensionless,
            "derive-ratio-2-3",
            new[] { validEvidence },
            evaluatedAt);
        var fiveSevenths = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "ratio-5-7",
            new ProbeExactRatio(5, 7),
            dimensionless,
            "derive-ratio-5-7",
            new[] { validEvidence },
            evaluatedAt);
        var composed = ProbeMeasurementTransformReceipt.ComposeExact(
            "ratio-10-21",
            "derive-ratio-10-21",
            twoThirds,
            fiveSevenths,
            evaluatedAt);
        ProbeCase(
            composed.Disposition == ProbeBoundaryDisposition.ExactRepresentationLocal &&
            composed.NominalCoefficient == new ProbeExactRatio(10, 21),
            "ratio-scale-compose");

        var coulombDimension = ProbeUnitDimensionVector.Create(("I", 1), ("T", 1));
        var elementaryCharge = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "si-elementary-charge-definition",
            new ProbeExactRatio(1_602_176_634, BigInteger.Pow(10, 28)),
            coulombDimension,
            "bipm-si-defining-constant-e",
            new[]
            {
                new ProbeCalibrationEvidenceEnvelope(
                    "bipm-e",
                    "https://www.bipm.org/en/measurement-units/si-defining-constants",
                    validEvidence.ValidFrom,
                    validEvidence.ValidThrough,
                    validEvidence.CoefficientStatus,
                    validEvidence.UncertaintyKind,
                    validEvidence.UncertaintyStatement,
                    validEvidence.Authentication,
                    validEvidence.Residual),
            },
            evaluatedAt);
        ProbeCase(
            elementaryCharge.Disposition == ProbeBoundaryDisposition.ExactRepresentationLocal,
            "exact-si-defined-fixture");

        var affine = Crossing(ProbeMeasurementTransformKind.Affine, "CELSIUS_OFFSET_FUNCTION_REQUIRED");
        ProbeCase(affine.Disposition == ProbeBoundaryDisposition.ExplicitTransformCrossing, "affine-crossing");
        var logarithmic = Crossing(ProbeMeasurementTransformKind.Logarithmic, "DECIBEL_LOG_FUNCTION_REQUIRED");
        ProbeCase(logarithmic.Disposition == ProbeBoundaryDisposition.ExplicitTransformCrossing, "log-crossing");
        var nonlinear = Crossing(ProbeMeasurementTransformKind.Nonlinear, "POLYNOMIAL_OR_LOOKUP_REPLAY_REQUIRED");
        ProbeCase(nonlinear.Disposition == ProbeBoundaryDisposition.ExplicitTransformCrossing, "nonlinear-crossing");

        var roundedEvidence = new ProbeCalibrationEvidenceEnvelope(
            "rounded",
            validEvidence.SourceReference,
            validEvidence.ValidFrom,
            validEvidence.ValidThrough,
            ProbeCoefficientStatus.Rounded,
            validEvidence.UncertaintyKind,
            "Synthetic rounded nominal; exact pre-rounding value and rounding error are not supplied.",
            validEvidence.Authentication,
            validEvidence.Residual);
        var rounded = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "rounded-ratio",
            new ProbeExactRatio(1234, 1000),
            dimensionless,
            "rounded-regression",
            new[] { roundedEvidence },
            evaluatedAt);
        ProbeCase(rounded.Disposition == ProbeBoundaryDisposition.ExplicitTransformCrossing, "rounded-crossing");
        var correlated = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "correlated-ratio",
            ProbeExactRatio.One,
            dimensionless,
            "correlated-calibration",
            new[]
            {
                new ProbeCalibrationEvidenceEnvelope(
                    "correlated",
                    validEvidence.SourceReference,
                    validEvidence.ValidFrom,
                    validEvidence.ValidThrough,
                    validEvidence.CoefficientStatus,
                    ProbeUncertaintyKind.Correlated,
                    "Synthetic correlated uncertainty; covariance data are required and not supplied by this fixture.",
                    validEvidence.Authentication,
                    validEvidence.Residual),
            },
            evaluatedAt);
        ProbeCase(correlated.Disposition == ProbeBoundaryDisposition.ExplicitTransformCrossing, "correlated-crossing");
        var expired = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "expired-ratio",
            ProbeExactRatio.One,
            dimensionless,
            "expired-calibration",
            new[]
            {
                new ProbeCalibrationEvidenceEnvelope(
                    "expired",
                    validEvidence.SourceReference,
                    validEvidence.ValidFrom,
                    evaluatedAt.AddDays(-1),
                    validEvidence.CoefficientStatus,
                    validEvidence.UncertaintyKind,
                    validEvidence.UncertaintyStatement,
                    validEvidence.Authentication,
                    validEvidence.Residual),
            },
            evaluatedAt);
        ProbeCase(expired.Disposition == ProbeBoundaryDisposition.Unresolved, "expired-unresolved");

        var fifth = ProbeJustIntervalReceipt.FromRatio(
            "perfect-fifth",
            "music-source-3-2",
            new ProbeExactRatio(3, 2));
        var majorThird = ProbeJustIntervalReceipt.FromRatio(
            "major-third",
            "music-source-5-4",
            new ProbeExactRatio(5, 4));
        var majorSeventh = fifth.Compose(majorThird, "major-seventh", "music-compose-15-8");
        var policy = new ProbeAudioApproximationPolicy(
            sampleRate: 8_000,
            sampleCount: 8_000,
            phaseRadians: 0,
            peakAmplitude: 0.25,
            linearAttackSamples: 80,
            linearReleaseSamples: 80);
        var wave = ProbePcmWaveRenderer.RenderSine(
            "render-perfect-fifth",
            fifth,
            new ProbeExactRatio(220, 1),
            policy);
        ProbeCase(
            majorSeventh.Ratio == new ProbeExactRatio(15, 8) &&
            wave.WavByteLength == 16_044 && wave.ClippedSampleCount == 0,
            "exact-interval-to-pcm");
        var ratioThree = ProbeJustIntervalReceipt.FromRatio("three-one", "music-source-3-1", new ProbeExactRatio(3, 1));
        ProbeCase(
            ratioThree.ProjectToOctave().PitchClassRatio == fifth.ProjectToOctave().PitchClassRatio &&
            ratioThree.DerivationReceiptId != fifth.DerivationReceiptId,
            "octave-collision-preserves-lineage");

        var accumulatorRegistry = new ProbeStructuralPrimeRegistry(
            "transparent-demo",
            1,
            new Dictionary<string, BigInteger>(StringComparer.Ordinal)
            {
                ["sensor-a"] = 2,
                ["sensor-b"] = 3,
                ["sensor-c"] = 5,
                ["sensor-d"] = 7,
            });
        var accumulatorLeft = ProbeTransparentStructuralAccumulator.Create(
            accumulatorRegistry,
            AccumulatorLeftIds);
        var accumulatorRight = ProbeTransparentStructuralAccumulator.Create(
            accumulatorRegistry,
            AccumulatorRightIds);
        var accumulatorIntersection = accumulatorLeft.Intersect(accumulatorRight);
        ProbeCase(
            accumulatorIntersection.PubliclyDecodableSupport.SequenceEqual(AccumulatorIntersectionIds) &&
            accumulatorLeft.TestMembership("sensor-a").MembershipIsPubliclyLeaked &&
            ProbeTransparentStructuralAccumulator.SecurityBoundary.CryptographicClassification == Build004Protocol.SecurityStatus &&
            ProbeTransparentStructuralAccumulator.SecurityBoundary.PrivacyClassification == Build004Protocol.PrivacyStatus,
            "transparent-accumulator-leaks-membership");

        var bomA = ProbeBomQuantityReceipt.Create(
            "bom-a",
            new[]
            {
                new ProbeBomLine("supplier-a", "shared-fastener", "lot-1", 4, "receipt-a1"),
                new ProbeBomLine("supplier-a", "housing-a", "lot-2", 6, "receipt-a2"),
            });
        var bomB = ProbeBomQuantityReceipt.Create(
            "bom-b",
            new[]
            {
                new ProbeBomLine("supplier-b", "shared-fastener", "lot-9", 2, "receipt-b1"),
                new ProbeBomLine("supplier-b", "housing-b", "lot-8", 8, "receipt-b2"),
            });
        ProbeCase(
            bomA.HasSameComputedValueButDifferentLineage(bomB) &&
            bomA.SharedComponentKeys(bomB).SequenceEqual(SharedBomComponentIds),
            "bom-same-value-different-lineage");

        costs.Add(new Build004StructuralCostRow(
            HostSoftwareLedger,
            "AUDIO",
            "JUST_FIFTH_3_2_FROM_220_HZ",
            "pcm_payload",
            wave.WavByteLength.ToString(CultureInfo.InvariantCulture),
            "bytes",
            "Exact requested ratio retained beside approximate PCM16 readout.",
            "DAC_TRANSDUCER_ROOM_AND_PERCEPTION_NOT_MEASURED"));

        var evidence = new
        {
            Schema = "prime-axiom-build004-boundary-probes-v1",
            Build004Protocol.ProtocolId,
            ProjectionContracts = new[]
            {
                ProbeSignedPrimeCoordinates.Contract,
                ProbeUnitDimensionVector.Contract,
            },
            ProjectionInstances = new[]
            {
                new
                {
                    Projection = ProbeSignedPrimeCoordinates.Contract.Name,
                    BasisId = composed.NumericFactors!.BasisId,
                    composed.NumericFactors.Completeness,
                    composed.NumericFactors.PayloadReplayability,
                },
                new
                {
                    Projection = ProbeUnitDimensionVector.Contract.Name,
                    composed.Dimension.BasisId,
                    composed.Dimension.Completeness,
                    composed.Dimension.PayloadReplayability,
                },
            },
            Calibration = new
            {
                RatioScaleComposition = composed,
                ElementaryChargeDefinitionFixture = elementaryCharge,
                Affine = affine,
                Logarithmic = logarithmic,
                Nonlinear = nonlinear,
                Rounded = rounded,
                Correlated = correlated,
                Expired = expired,
                Note = "Exact arithmetic does not establish empirical calibration truth.",
            },
            Audio = new
            {
                Fifth = fifth,
                MajorThird = majorThird,
                MajorSeventh = majorSeventh,
                Wave = new
                {
                    wave.RenderReceiptId,
                    wave.RequestedRatio,
                    wave.BaseFrequencyHertz,
                    wave.NominalFrequencyHertz,
                    wave.RenderedFrequencyHertz,
                    wave.Policy,
                    wave.ClippedSampleCount,
                    wave.WavByteLength,
                    wave.WavSha256,
                    ProbePcmWaveReceipt.ApproximationDeclaration,
                },
            },
            StructuralAccumulator = new
            {
                Left = accumulatorLeft,
                Right = accumulatorRight,
                Intersection = accumulatorIntersection,
                ProbeTransparentStructuralAccumulator.LeakageStatement,
                Security = ProbeStructuralSecurityBoundary.TransparentOnly,
            },
            Bom = new
            {
                First = bomA,
                Second = bomB,
                SameValueDifferentLineage = bomA.HasSameComputedValueButDifferentLineage(bomB),
                SharedComponents = bomA.SharedComponentKeys(bomB),
                ProbeBomQuantityReceipt.IntegrationBoundary,
            },
            FrameworkComparison = new
            {
                Status = "AFTER_THE_FACT_REMOVABLE_LENSES_ONLY",
                PAL = "Multi-parent account semantics remain open in PAL v2.2; this DAG does not amend canon.",
                BLA = "Residual-triggered reopening lens; does not infer cause or authority.",
                CLEF = "Observable, aperture, noise, cost, uncertainty, and stopping-rule checklist; not validation evidence.",
                BrtAic = "Exact-symbolic to trace/readout boundary lens; not a physics or computation proof.",
            },
            Build004Protocol.ClaimCeiling,
        };
        return (evidence, wave.GetWavBytes());

        ProbeMeasurementTransformReceipt Crossing(
            ProbeMeasurementTransformKind kind,
            string reason) =>
            ProbeMeasurementTransformReceipt.ExplicitCrossing(
                $"{kind.ToString().ToLowerInvariant()}-crossing",
                kind,
                coulombDimension,
                $"derive-{kind.ToString().ToLowerInvariant()}",
                new[] { validEvidence },
                evaluatedAt,
                reason);

        void ProbeCase(bool condition, string id)
        {
            ledger.Case(ProbeFamily);
            ledger.Check(ProbeFamily, condition, id);
        }
    }

    private static void AddWorkRows(
        List<Build004StructuralCostRow> rows,
        string domain,
        string caseId,
        CombinatorialWork work)
    {
        Add("prime_basis_candidates", work.PrimeBasisCandidates, "operations");
        Add("prime_basis_composite_marks", work.PrimeBasisCompositeMarks, "operations");
        Add("prime_basis_primes", work.PrimeBasisPrimes, "primes");
        Add("factorial_cache_hits", work.FactorialCacheHits, "lookups");
        Add("factorial_cache_misses", work.FactorialCacheMisses, "lookups");
        Add("factorial_valuation_calls", work.FactorialValuationCalls, "calls");
        Add("legendre_quotient_steps", work.LegendreQuotientSteps, "operations");
        Add("coordinate_reads", work.CoordinateReads, "operations");
        Add("coordinate_writes", work.CoordinateWrites, "operations");
        Add(
            "coordinate_additions",
            work.CoordinateAdditions,
            "operations",
            "Nonzero existing-coordinate exponent merges; not every CLR integer addition.");
        Add("coordinate_zero_eliminations", work.CoordinateZeroEliminations, "operations");
        Add("big_integer_powers", work.BigIntegerPowers, "operations");
        Add("big_integer_multiplications", work.BigIntegerMultiplications, "operations");
        Add("big_integer_exact_divisions", work.BigIntegerExactDivisions, "operations");
        Add("exact_rational_additions", work.BigIntegerAdditions, "operations");
        Add("greatest_common_divisors", work.GreatestCommonDivisors, "operations");
        Add("reconstructions", work.Reconstructions, "operations");
        Add("additive_nodes", work.AdditiveNodes, "nodes");
        Add("additive_terms", work.AdditiveTerms, "terms");
        Add(
            "exact_rational_reductions",
            work.ExactRationalReductions,
            "operations",
            "Nontrivial residual cancellations after exact rational addition; GCD and exact-division counts are separate.");

        void Add(string metric, long value, string unit, string? softwareMeaning = null)
        {
            var ledgerClass = metric is "prime_basis_primes" or "additive_nodes" or "additive_terms"
                ? AbstractStructureLedger
                : HostSoftwareLedger;
            rows.Add(new Build004StructuralCostRow(
                ledgerClass,
                domain,
                caseId,
                metric,
                value.ToString(CultureInfo.InvariantCulture),
                unit,
                softwareMeaning ?? "Deterministic operation counter for this implementation.",
                "HARDWARE_NOT_MEASURED"));
        }
    }

    private static void AddCanonicalByteRow<T>(
        List<Build004StructuralCostRow> rows,
        string domain,
        string caseId,
        T value) => AddCanonicalByteRow(rows, domain, caseId, "canonical_receipt_utf8_bytes", value);

    private static void AddCanonicalByteRow<T>(
        List<Build004StructuralCostRow> rows,
        string domain,
        string caseId,
        string metric,
        T value) => rows.Add(new Build004StructuralCostRow(
            HostSoftwareLedger,
            domain,
            caseId,
            metric,
            Build004Protocol.CanonicalJsonUtf8ByteLength(value).ToString(CultureInfo.InvariantCulture),
            "bytes",
            $"Canonical host receipt under {Build004Protocol.CanonicalJsonContract}.",
            "HOST_SERIALIZATION_ONLY__HARDWARE_NOT_MEASURED"));

    private static string HashText(string value) =>
        Build004Protocol.BytesSha256(Encoding.UTF8.GetBytes(value));

    private sealed record LineageContext(
        LineageRegistry Registry,
        IReadOnlyList<AtomKey> Atoms,
        IReadOnlyList<AtomDescriptor> Descriptors);

    private sealed class CampaignLedger
    {
        private readonly IReadOnlyDictionary<string, long> expectedCases;
        private readonly IReadOnlyDictionary<string, long> expectedChecks;
        private readonly Dictionary<string, MutableFamily> families = new(StringComparer.Ordinal);

        public CampaignLedger(
            IReadOnlyDictionary<string, long> cases,
            IReadOnlyDictionary<string, long> checks)
        {
            if (!cases.Keys.Order(StringComparer.Ordinal).SequenceEqual(
                    checks.Keys.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal))
            {
                throw new ArgumentException("Expected case and check families must match exactly.");
            }

            expectedCases = cases;
            expectedChecks = checks;
            foreach (var family in cases.Keys)
            {
                families.Add(family, new MutableFamily());
            }
        }

        public void Case(string family) => Get(family).Cases++;

        public void Check(string family, bool condition, string failureId)
        {
            var target = Get(family);
            target.Checks++;
            if (condition)
            {
                return;
            }

            target.FailureCount++;
            if (target.FailureDetails.Count < 64)
            {
                target.FailureDetails.Add(failureId);
            }
        }

        public ReadOnlyCollection<Build004FamilyReceipt> Freeze() => Array.AsReadOnly(
            expectedCases.Select(pair =>
            {
                var family = Get(pair.Key);
                var expectedCheckCount = expectedChecks[pair.Key];
                var status = family.FailureCount == 0 &&
                    family.Cases == pair.Value &&
                    family.Checks == expectedCheckCount
                    ? "BOUNDED_PASS"
                    : "FAILED_OR_INCOMPLETE";
                return new Build004FamilyReceipt(
                    pair.Key,
                    pair.Value,
                    expectedCheckCount,
                    family.Cases,
                    family.Checks,
                    family.FailureCount,
                    Array.AsReadOnly(family.FailureDetails.ToArray()),
                    status);
            }).ToArray());

        private MutableFamily Get(string family) => families.TryGetValue(family, out var value)
            ? value
            : throw new InvalidOperationException($"Unregistered Build 004 family: {family}");

        private sealed class MutableFamily
        {
            public long Cases { get; set; }
            public long Checks { get; set; }
            public long FailureCount { get; set; }
            public List<string> FailureDetails { get; } = new();
        }
    }

    private sealed class SplitMix64
    {
        private ulong state;

        public SplitMix64(ulong seed)
        {
            state = seed;
        }

        public ulong Next()
        {
            state += 0x9E3779B97F4A7C15UL;
            var value = state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
