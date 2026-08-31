using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using PrimeAxiom.Core.Build005.Hardware;
using PrimeAxiom.Core.Build005.Valuation;

namespace PrimeAxiom.Cli;

internal sealed record Build005PolicyConfiguration(
    string Name,
    ValuationCachePolicy Policy,
    int CacheCapacity,
    int SpeculationBudget)
{
    public bool IsSpeculative => SpeculationBudget > 0;
}

internal sealed record Build005ControlMetrics(
    long QueryRequests,
    long SpeculationQueries,
    long SpeculationDivmodSteps,
    long PrefetchKeysLaterRequested,
    long PrefetchKeysNeverRequested,
    long PrefetchToLaterRequestDistanceTotal,
    long PrefetchToLaterRequestDistanceMaximum,
    long GcdCalls,
    long GcdModuloSteps,
    long DeclaredGroupedScreenRemainderProxy,
    long CompositeDivmodCalls,
    long MagnitudeOutputs,
    long CorrectnessChecks,
    long CorrectnessFailures);

internal sealed record Build005ModeledDynamicCost(
    long SharedMagnitudeCycles,
    long PropagationArithmeticCycles,
    long QueryIssueCycles,
    long OddDivmodCycles,
    long CacheMaintenanceCycles,
    long TotalModeledCycles,
    long CatalogueNandEvaluations,
    long CtzNandEvaluations,
    long OddDivmodNandEvaluations,
    long CacheNandEvaluations,
    long TotalModeledServiceNandEvaluations,
    string EvidenceClass,
    string ExcludedSharedCost);

internal sealed record Build005EvidenceGate(
    string Gate,
    bool Satisfied,
    string Evidence);

internal sealed record Build005WorkloadRow(
    string ProtocolId,
    int Width,
    string Family,
    string TraceId,
    string TraceSha256,
    string SourceRegime,
    string OutputObligation,
    bool Hostile,
    bool PrimeAttributionEligible,
    bool SearchRepaymentEligible,
    string Policy,
    int CacheCapacity,
    int SpeculationBudget,
    ValuationMetrics SemanticMetrics,
    Build005ControlMetrics ControlMetrics,
    Build005ModeledDynamicCost ModeledCost,
    IReadOnlyList<Build005PrefixCost> PrefixCosts,
    IReadOnlyList<ValueSlotSnapshot> FinalSlots,
    string Status);

internal sealed record Build005PrefixCost(
    int EventIndex,
    long TotalModeledCycles,
    long TotalModeledServiceNandEvaluations);

internal sealed record Build005BreakEvenRow(
    int Width,
    string Family,
    string TraceId,
    string Candidate,
    string Baseline,
    int CacheCapacity,
    int SpeculationBudget,
    int StableCyclePrefix,
    int StableServiceNandPrefix,
    long FinalCycleDelta,
    long FinalServiceNandDelta,
    bool FinalNoWorseBoth,
    bool StrictlyBetterAtLeastOne,
    bool EligibleForFrozenDecision,
    string Attribution);

internal sealed record Build005StaticCostRow(
    int Width,
    int CacheCapacity,
    string Component,
    int Nand2Static,
    int DffStatic,
    int StateBits,
    int InputBits,
    int OutputBits,
    int PortBits,
    int WireBits,
    int ConnectionsStatic,
    int MaximumFanout,
    int UnitNandCriticalDepth,
    int CrossRegionConnections,
    int CacheLineBits,
    string CombinationalLoopStatus,
    string EvidenceClass,
    bool IntegratedNetlist);

internal sealed record Build005FamilyReceipt(
    string Family,
    int ExpectedRows,
    int Rows,
    long Checks,
    long Failures,
    string Status);

internal sealed record Build005IndependentCorrectness(
    long Checks,
    long Failures,
    IReadOnlyDictionary<string, long> CheckGroups,
    IReadOnlyList<string> FailureDetails);

internal sealed record Build005Decision(
    string GeneratedStatus,
    string CandidateTerminalLabel,
    bool DecisionAxesEarned,
    string SearchPolicy,
    string Attribution,
    string EvidenceBoundary,
    string ExploratoryObservedPattern,
    string ExploratorySearchObservation,
    bool DemandPrimeCandidate,
    bool SpeculativePrimeCandidate,
    bool GenericCacheAdvantage,
    bool ProducerOnlyAdvantage,
    bool RadixTwoOnly,
    IReadOnlyList<string> QualifyingDemandFamilies,
    IReadOnlyList<string> QualifyingSpeculativeFamilies,
    IReadOnlyList<string> UnmetGates,
    string ClaimCeiling);

internal sealed record Build005CampaignResult(
    Build005IndependentCorrectness Correctness,
    IReadOnlyList<Build005WorkloadRow> WorkloadRows,
    IReadOnlyList<Build005BreakEvenRow> BreakEvenRows,
    IReadOnlyList<Build005StaticCostRow> StaticCosts,
    IReadOnlyList<Build005FamilyReceipt> Families,
    IReadOnlyList<Build005EvidenceGate> EvidenceGates,
    Build005Decision Decision,
    bool ImplementedTraceCoverageComplete,
    bool CompleteFrozenCoverage,
    long Checks,
    long Failures);

internal static class Build005Campaign
{
    internal const string CompleteStatus = "IMPLEMENTED_TRACE_PASS";
    internal const string SemanticEvidence = "EXACT_SEMANTIC_CONTROLLER";
    internal const string CompositionalNandEvidence = "STRUCTURAL_DECLARED_COMPOSITIONAL_SCHEDULE";
    internal const string SharedMagnitudeExclusion =
        "AUTHORITATIVE_W_BIT_LOAD_ADD_MUL_OUTPUT_DATAPATH_NOT_REBUILT_AT_W16_W32; EXACT_OPERATION_COUNTS_AND_IDENTICAL_MAGNITUDES_RETAINED; GCD_GROUPED_SCREEN_AND_MAGNITUDE_OUTPUT_COSTS_EXCLUDED_FROM_MODELED_TOTAL";

    internal static readonly IReadOnlyList<Build005PolicyConfiguration> Policies =
        Array.AsReadOnly(CreatePolicies());

    public static Build005CampaignResult Run()
    {
        var hardware = new Dictionary<int, DeclaredRadixAwareValuationFamily>();
        foreach (var width in new[] { 8, 16, 32 })
        {
            hardware.Add(width, RadixAwareValuationHardware.BuildFamily(width));
        }

        var rows = new List<Build005WorkloadRow>();
        foreach (var width in new[] { 8, 16, 32 })
        {
            foreach (var trace in Build005Workloads.Create(width))
            {
                foreach (var policy in Policies)
                {
                    rows.Add(RunTrace(trace, policy, hardware[width]));
                }
            }
        }

        var correctness = RunIndependentCorrectness();
        var breakEven = BuildBreakEven(rows);
        var staticCosts = BuildStaticCosts(hardware);
        var families = BuildFamilies(rows);
        var implementedTraceCoverageComplete = correctness.Failures == 0 &&
            families.All(family => family.Status == CompleteStatus) &&
            rows.All(row => row.Status == CompleteStatus) &&
            staticCosts.Count == 3 * 4 * 4;
        var evidenceGates = BuildEvidenceGates(implementedTraceCoverageComplete);
        var complete = implementedTraceCoverageComplete && evidenceGates.All(gate => gate.Satisfied);
        var decision = Decide(rows, breakEven, evidenceGates, complete);
        var workloadChecks = rows.Sum(row => row.ControlMetrics.CorrectnessChecks);
        var workloadFailures = rows.Sum(row => row.ControlMetrics.CorrectnessFailures);
        return new Build005CampaignResult(
            correctness,
            new ReadOnlyCollection<Build005WorkloadRow>(rows),
            new ReadOnlyCollection<Build005BreakEvenRow>(breakEven),
            new ReadOnlyCollection<Build005StaticCostRow>(staticCosts),
            new ReadOnlyCollection<Build005FamilyReceipt>(families),
            new ReadOnlyCollection<Build005EvidenceGate>(evidenceGates),
            decision,
            implementedTraceCoverageComplete,
            complete,
            correctness.Checks + workloadChecks,
            correctness.Failures + workloadFailures);
    }

    private static Build005WorkloadRow RunTrace(
        Build005Trace trace,
        Build005PolicyConfiguration policy,
        DeclaredRadixAwareValuationFamily hardware)
    {
        var compositeControl = trace.Family == "COMPOSITE_CONTROL";
        var service = compositeControl
            ? DemandValuationService.CreateCompositeControl(
                trace.Width,
                policy.Policy,
                policy.CacheCapacity,
                Build005Protocol.CompositeControls)
            : new DemandValuationService(trace.Width, policy.Policy, policy.CacheCapacity);
        var oracle = new ulong?[DemandValuationService.SlotCount];
        var control = new MutableControlMetrics();
        var prefetched = new Dictionary<SpeculationKey, int>();
        var laterRequestedPrefetches = new HashSet<SpeculationKey>();
        var prefixes = new List<Build005PrefixCost>(trace.Events.Count);

        for (var eventIndex = 0; eventIndex < trace.Events.Count; eventIndex++)
        {
            var item = trace.Events[eventIndex];
            var producedSlot = ExecuteEvent(
                service,
                oracle,
                trace,
                item,
                control,
                prefetched,
                laterRequestedPrefetches,
                eventIndex);
            if (producedSlot >= 0 && policy.IsSpeculative && !compositeControl)
            {
                Speculate(
                    service,
                    oracle,
                    producedSlot,
                    policy.SpeculationBudget,
                    control,
                    prefetched,
                    eventIndex);
            }

            ValidateCurrentSlots(service, oracle, control, $"{trace.Family}:{eventIndex}");
            prefixes.Add(BuildModeledCost(
                trace.Width,
                policy,
                hardware.Services[policy.CacheCapacity],
                service.Metrics,
                control.Snapshot(laterRequestedPrefetches, prefetched)).ToPrefix(eventIndex));
        }

        for (var slot = 0; slot < oracle.Length; slot++)
        {
            if (!oracle[slot].HasValue)
            {
                continue;
            }

            control.MagnitudeOutputs++;
            Check(
                control,
                service.SnapshotSlot(slot).Magnitude == oracle[slot]!.Value,
                $"{trace.Family}:final-slot:{slot}");
        }

        var controlSnapshot = control.Snapshot(laterRequestedPrefetches, prefetched);
        var modeled = BuildModeledCost(
            trace.Width,
            policy,
            hardware.Services[policy.CacheCapacity],
            service.Metrics,
            controlSnapshot);
        var traceBytes = JsonSerializer.SerializeToUtf8Bytes(trace, Build005Protocol.JsonOptions);
        return new Build005WorkloadRow(
            Build005Protocol.ProtocolId,
            trace.Width,
            trace.Family,
            trace.TraceId,
            Build005Protocol.BytesSha256(traceBytes),
            trace.SourceRegime,
            trace.OutputObligation,
            trace.Hostile,
            trace.PrimeAttributionEligible,
            trace.SearchRepaymentEligible,
            policy.Name,
            policy.CacheCapacity,
            policy.SpeculationBudget,
            service.Metrics,
            controlSnapshot,
            modeled,
            new ReadOnlyCollection<Build005PrefixCost>(prefixes),
            Array.AsReadOnly(Enumerable.Range(0, DemandValuationService.SlotCount)
                .Select(service.SnapshotSlot)
                .ToArray()),
            control.CorrectnessFailures == 0 ? CompleteStatus : "FAILED_CORRECTNESS");
    }

    private static int ExecuteEvent(
        DemandValuationService service,
        ulong?[] oracle,
        Build005Trace trace,
        Build005TraceEvent item,
        MutableControlMetrics control,
        IReadOnlyDictionary<SpeculationKey, int> prefetched,
        HashSet<SpeculationKey> laterRequestedPrefetches,
        int eventIndex)
    {
        switch (item.Kind)
        {
            case Build005EventKind.Load:
            case Build005EventKind.Overwrite:
                {
                    var result = service.Load(item.Destination, item.Magnitude);
                    Check(control, result.Succeeded, $"{trace.Family}:{eventIndex}:load");
                    if (result.Succeeded)
                    {
                        oracle[item.Destination] = item.Magnitude;
                        if (trace.Family == "SMOOTH_STRIP")
                        {
                            control.DeclaredGroupedScreenRemainderProxy += 3;
                        }

                        return item.Destination;
                    }

                    return -1;
                }
            case Build005EventKind.TestPower:
                {
                    control.QueryRequests++;
                    MarkLaterRequest(
                        service,
                        item.Left,
                        item.Divisor,
                        prefetched,
                        laterRequestedPrefetches,
                        eventIndex,
                        control);
                    var result = service.TestPower(item.Left, item.Divisor, item.Threshold);
                    var expected = OracleValuation(RequireOracle(oracle, item.Left), item.Divisor);
                    Check(control, result.Succeeded, $"{trace.Family}:{eventIndex}:test-success");
                    if (result.Value is not null)
                    {
                        Check(
                            control,
                            result.Value.IsAtLeastThreshold == (expected.Infinite || expected.Exponent >= item.Threshold),
                            $"{trace.Family}:{eventIndex}:test-answer");
                        CheckFrontierAnswer(control, RequireOracle(oracle, item.Left), item.Divisor, result.Value);
                    }

                    return -1;
                }
            case Build005EventKind.Valuation:
            case Build005EventKind.StripAll:
                {
                    control.QueryRequests++;
                    MarkLaterRequest(
                        service,
                        item.Left,
                        item.Divisor,
                        prefetched,
                        laterRequestedPrefetches,
                        eventIndex,
                        control);
                    var result = item.Kind == Build005EventKind.Valuation
                        ? service.Valuation(item.Left, item.Divisor)
                        : service.StripAll(item.Left, item.Divisor);
                    var expected = OracleValuation(RequireOracle(oracle, item.Left), item.Divisor);
                    Check(control, result.Succeeded, $"{trace.Family}:{eventIndex}:valuation-success");
                    if (result.Value is not null)
                    {
                        Check(control, result.Value.Exponent == expected.Exponent, $"{trace.Family}:{eventIndex}:valuation-exponent");
                        Check(control, result.Value.Residual == expected.Residual, $"{trace.Family}:{eventIndex}:valuation-residual");
                        Check(control, result.Value.Infinite == expected.Infinite, $"{trace.Family}:{eventIndex}:valuation-infinite");
                    }

                    return -1;
                }
            case Build005EventKind.Multiply:
                {
                    var left = RequireOracle(oracle, item.Left);
                    var right = RequireOracle(oracle, item.Right);
                    var previous = oracle[item.Destination];
                    var overflow = right != 0 && left > service.MaximumMagnitude / right;
                    var result = service.Multiply(item.Destination, item.Left, item.Right);
                    Check(control, result.Succeeded == !overflow, $"{trace.Family}:{eventIndex}:multiply-status");
                    if (overflow)
                    {
                        Check(control, SlotMatches(service, item.Destination, previous), $"{trace.Family}:{eventIndex}:multiply-atomic");
                        return -1;
                    }

                    oracle[item.Destination] = left * right;
                    return item.Destination;
                }
            case Build005EventKind.MultiplyByPrime:
            case Build005EventKind.ProducerPrimeFact:
                {
                    var source = RequireOracle(oracle, item.Left);
                    var previous = oracle[item.Destination];
                    var overflow = source != 0 && source > service.MaximumMagnitude / (ulong)item.Divisor;
                    var result = service.MultiplyByPrime(item.Destination, item.Left, item.Divisor);
                    Check(control, result.Succeeded == !overflow, $"{trace.Family}:{eventIndex}:multiply-prime-status");
                    if (overflow)
                    {
                        Check(control, SlotMatches(service, item.Destination, previous), $"{trace.Family}:{eventIndex}:multiply-prime-atomic");
                        return -1;
                    }

                    oracle[item.Destination] = source * (ulong)item.Divisor;
                    return item.Destination;
                }
            case Build005EventKind.Add:
                {
                    var left = RequireOracle(oracle, item.Left);
                    var right = RequireOracle(oracle, item.Right);
                    var previous = oracle[item.Destination];
                    var sum = left + right;
                    var overflow = sum > service.MaximumMagnitude;
                    var result = service.Add(item.Destination, item.Left, item.Right);
                    Check(control, result.Succeeded == !overflow, $"{trace.Family}:{eventIndex}:add-status");
                    if (overflow)
                    {
                        Check(control, SlotMatches(service, item.Destination, previous), $"{trace.Family}:{eventIndex}:add-atomic");
                        return -1;
                    }

                    oracle[item.Destination] = sum;
                    return item.Destination;
                }
            case Build005EventKind.RationalReduce:
                {
                    var left = RequireOracle(oracle, item.Left);
                    var right = RequireOracle(oracle, item.Right);
                    control.GcdCalls++;
                    var gcd = OracleGcd(left, right, out var steps);
                    control.GcdModuloSteps += steps;
                    Check(control, left % gcd == 0 && right % gcd == 0, $"{trace.Family}:{eventIndex}:gcd");
                    return -1;
                }
            case Build005EventKind.CompositeValuationControl:
                {
                    var value = RequireOracle(oracle, item.Left);
                    control.QueryRequests++;
                    var result = service.Valuation(item.Left, item.Divisor);
                    var expected = OracleValuation(value, item.Divisor);
                    Check(control, result.Succeeded, $"{trace.Family}:{eventIndex}:composite-success");
                    if (result.Value is not null)
                    {
                        Check(control, result.Value.Exponent == expected.Exponent, $"{trace.Family}:{eventIndex}:composite-exponent");
                        Check(control, result.Value.Residual == expected.Residual, $"{trace.Family}:{eventIndex}:composite-residual");
                        Check(control, result.Value.Infinite == expected.Infinite, $"{trace.Family}:{eventIndex}:composite-infinite");
                    }

                    control.CompositeDivmodCalls += result.MetricsDelta.DivModCalls;
                    return -1;
                }
            default:
                throw new InvalidOperationException($"Unhandled Build 005 event {item.Kind}.");
        }
    }

    private static bool SlotMatches(DemandValuationService service, int slot, ulong? expected)
    {
        var actual = service.SnapshotSlot(slot);
        return actual.Initialized == expected.HasValue &&
            (!expected.HasValue || actual.Magnitude == expected.Value);
    }

    private static void Speculate(
        DemandValuationService service,
        ulong?[] oracle,
        int slot,
        int budget,
        MutableControlMetrics control,
        Dictionary<SpeculationKey, int> prefetched,
        int eventIndex)
    {
        var spent = 0L;
        foreach (var prime in Build005Protocol.PrimeCatalog.Where(prime => prime != 2))
        {
            if (spent >= budget)
            {
                break;
            }

            var before = service.Metrics;
            var result = service.TestPower(slot, prime, 1);
            control.SpeculationQueries++;
            Check(control, result.Succeeded, $"speculation:{eventIndex}:{slot}:{prime}");
            var delta = result.MetricsDelta.DivModCalls;
            spent += delta;
            control.SpeculationDivmodSteps += delta;
            var snapshot = service.SnapshotSlot(slot);
            prefetched.TryAdd(new SpeculationKey(slot, snapshot.Generation, prime), eventIndex);
            Check(control, service.Metrics.DivModCalls >= before.DivModCalls, $"speculation-monotone:{eventIndex}:{prime}");
        }

        _ = oracle;
    }

    private static void MarkLaterRequest(
        DemandValuationService service,
        int slot,
        int prime,
        IReadOnlyDictionary<SpeculationKey, int> prefetched,
        HashSet<SpeculationKey> laterRequested,
        int eventIndex,
        MutableControlMetrics control)
    {
        var generation = service.SnapshotSlot(slot).Generation;
        var key = new SpeculationKey(slot, generation, prime);
        if (!prefetched.TryGetValue(key, out var prefetchedAt) || !laterRequested.Add(key))
        {
            return;
        }

        var distance = eventIndex - prefetchedAt;
        control.PrefetchToLaterRequestDistanceTotal += distance;
        control.PrefetchToLaterRequestDistanceMaximum = Math.Max(
            control.PrefetchToLaterRequestDistanceMaximum,
            distance);
    }

    private static void ValidateCurrentSlots(
        DemandValuationService service,
        IReadOnlyList<ulong?> oracle,
        MutableControlMetrics control,
        string context)
    {
        for (var slot = 0; slot < oracle.Count; slot++)
        {
            var actual = service.SnapshotSlot(slot);
            Check(control, actual.Initialized == oracle[slot].HasValue, $"{context}:slot-init:{slot}");
            if (oracle[slot].HasValue)
            {
                Check(control, actual.Magnitude == oracle[slot]!.Value, $"{context}:slot-value:{slot}");
            }
        }

        Check(
            control,
            service.ValidateCacheState(out _),
            $"{context}:cache-state");
    }

    private static void CheckFrontierAnswer(
        MutableControlMetrics control,
        ulong magnitude,
        int prime,
        PowerTestAnswer answer)
    {
        if (answer.Infinite)
        {
            Check(control, magnitude == 0, "power-answer-infinite");
            return;
        }

        var reconstructed = answer.Residual;
        for (var index = 0; index < answer.CertifiedLowerBound; index++)
        {
            reconstructed = checked(reconstructed * (ulong)prime);
        }

        Check(control, reconstructed == magnitude, "power-answer-equation");
        Check(control, !answer.Terminal || answer.Residual % (ulong)prime != 0, "power-answer-terminal");
    }

    private static Build005ModeledDynamicCost BuildModeledCost(
        int width,
        Build005PolicyConfiguration policy,
        DeclaredRadixAwareValuationService hardware,
        ValuationMetrics metrics,
        Build005ControlMetrics control)
    {
        var sharedMagnitude = metrics.Loads +
            metrics.Additions +
            metrics.Multiplications +
            metrics.MultiplyByPrimeOperations +
            metrics.RejectedOperations;
        var propagationArithmetic = checked(
            metrics.PropagationExponentAdds + metrics.PropagationResidualMultiplies);
        var oddCycles = checked(metrics.DivModCalls * (width + 1L));
        var mutationEvents = policy.Policy is ValuationCachePolicy.BinFrontierNoPropK or
            ValuationCachePolicy.BinPrimeFrontierPropK
            ? metrics.Loads + metrics.Additions + metrics.Multiplications + metrics.MultiplyByPrimeOperations
            : 0;
        var extraLookups = Math.Max(0, metrics.CacheLookups - control.QueryRequests - control.SpeculationQueries);
        var cacheMaintenance = policy.CacheCapacity == 0
            ? 0
            : extraLookups + metrics.CacheFills + metrics.CacheUpdates + mutationEvents;
        var totalCycles = checked(
            sharedMagnitude +
            propagationArithmetic +
            control.QueryRequests +
            control.SpeculationQueries +
            oddCycles +
            cacheMaintenance);
        var catalogueNand = checked(
            (control.QueryRequests + control.SpeculationQueries) *
            hardware.Catalogue.Metrics.Nand2Static);
        var ctzNand = checked(metrics.CtzCalls * hardware.Ctz.Metrics.Nand2Static);
        var divmodNand = checked(
            metrics.DivModCalls *
            hardware.OddDivmod.Metrics.Nand2Static *
            (width + 2L));
        var cacheCycles = policy.CacheCapacity == 0
            ? 0
            : metrics.CacheLookups + metrics.CacheFills + metrics.CacheUpdates + mutationEvents;
        var cacheNand = checked(cacheCycles * hardware.Cache.Metrics.Nand2Static);
        return new Build005ModeledDynamicCost(
            sharedMagnitude,
            propagationArithmetic,
            control.QueryRequests + control.SpeculationQueries,
            oddCycles,
            cacheMaintenance,
            totalCycles,
            catalogueNand,
            ctzNand,
            divmodNand,
            cacheNand,
            checked(catalogueNand + ctzNand + divmodNand + cacheNand),
            "EXPLORATORY_COMPONENT_PROXY__NOT_DECISION_ELIGIBLE",
            SharedMagnitudeExclusion +
            "; PROPAGATION_COMBINER_AND_POLICY_MATCHED_CONTENT_CACHE_LOGIC_NOT_IN_NAND_TOTAL");
    }

    private static List<Build005StaticCostRow> BuildStaticCosts(
        IReadOnlyDictionary<int, DeclaredRadixAwareValuationFamily> families)
    {
        var rows = new List<Build005StaticCostRow>();
        foreach (var family in families.Values.OrderBy(family => family.Width))
        {
            foreach (var service in family.Services.Values.OrderBy(service => service.CacheCapacity))
            {
                foreach (var component in service.Cost.Components)
                {
                    var metrics = component.Metrics;
                    rows.Add(new Build005StaticCostRow(
                        family.Width,
                        service.CacheCapacity,
                        component.Component,
                        metrics.Nand2Static,
                        metrics.DffStatic,
                        metrics.StateBits,
                        metrics.InputBits,
                        metrics.OutputBits,
                        metrics.PortBits,
                        metrics.WireBits,
                        metrics.ConnectionsStatic,
                        metrics.MaximumFanout,
                        metrics.UnitNandCriticalDepth,
                        metrics.CrossRegionConnections,
                        service.Cost.CacheLineBits,
                        metrics.CombinationalLoopStatus.ToString(),
                        CompositionalServiceCost.EvidenceClass,
                        CompositionalServiceCost.IsIntegratedNetlist));
                }
            }
        }

        return rows;
    }

    private static List<Build005BreakEvenRow> BuildBreakEven(
        IReadOnlyList<Build005WorkloadRow> rows)
    {
        var result = new List<Build005BreakEvenRow>();
        foreach (var candidate in rows.Where(row =>
                     row.Policy == "BIN_CONTENT_ANSWER_LRU_K" ||
                     row.Policy == "BIN_FRONTIER_NOPROP_K" ||
                     row.Policy == "BIN_PRIME_FRONTIER_PROP_K" ||
                     row.Policy.StartsWith("BIN_PRIME_FRONTIER_SPEC_B", StringComparison.Ordinal)))
        {
            var baselines = new List<Build005WorkloadRow>();
            var direct = FindRow(rows, candidate, "BIN_DIRECT_BEST", 0, 0);
            if (candidate.Policy is "BIN_CONTENT_ANSWER_LRU_K" or "BIN_FRONTIER_NOPROP_K")
            {
                baselines.Add(direct);
            }
            else if (candidate.Policy == "BIN_PRIME_FRONTIER_PROP_K")
            {
                baselines.Add(direct);
                baselines.Add(FindRow(rows, candidate, "BIN_CONTENT_ANSWER_LRU_K", candidate.CacheCapacity, 0));
                baselines.Add(FindRow(rows, candidate, "BIN_FRONTIER_NOPROP_K", candidate.CacheCapacity, 0));
            }
            else
            {
                baselines.Add(FindRow(rows, candidate, "BIN_PRIME_FRONTIER_PROP_K", candidate.CacheCapacity, 0));
            }

            foreach (var baseline in baselines)
            {
                var cyclePrefix = StablePrefix(candidate.PrefixCosts, baseline.PrefixCosts, useNand: false);
                var nandPrefix = StablePrefix(candidate.PrefixCosts, baseline.PrefixCosts, useNand: true);
                var cycleDelta = candidate.ModeledCost.TotalModeledCycles - baseline.ModeledCost.TotalModeledCycles;
                var nandDelta = candidate.ModeledCost.TotalModeledServiceNandEvaluations -
                    baseline.ModeledCost.TotalModeledServiceNandEvaluations;
                result.Add(new Build005BreakEvenRow(
                    candidate.Width,
                    candidate.Family,
                    candidate.TraceId,
                    candidate.Policy,
                    baseline.Policy,
                    candidate.CacheCapacity,
                    candidate.SpeculationBudget,
                    cyclePrefix,
                    nandPrefix,
                    cycleDelta,
                    nandDelta,
                    cycleDelta <= 0 && nandDelta <= 0,
                    cycleDelta < 0 || nandDelta < 0,
                    false,
                    candidate.Policy switch
                    {
                        "BIN_CONTENT_ANSWER_LRU_K" => "GENERIC_MEMOIZATION",
                        "BIN_FRONTIER_NOPROP_K" => "GENERIC_CHECKPOINT",
                        _ when candidate.PrimeAttributionEligible => "PRIME_PROPAGATION_ELIGIBLE",
                        _ => "GENERIC_OR_UNATTRIBUTED",
                    }));
            }
        }

        return result;
    }

    private static Build005WorkloadRow FindRow(
        IReadOnlyList<Build005WorkloadRow> rows,
        Build005WorkloadRow key,
        string policy,
        int cacheCapacity,
        int speculationBudget) =>
        rows.Single(row =>
            row.Width == key.Width &&
            row.Family == key.Family &&
            row.TraceId == key.TraceId &&
            row.Policy == policy &&
            row.CacheCapacity == cacheCapacity &&
            row.SpeculationBudget == speculationBudget);

    private static int StablePrefix(
        IReadOnlyList<Build005PrefixCost> candidate,
        IReadOnlyList<Build005PrefixCost> baseline,
        bool useNand)
    {
        if (candidate.Count != baseline.Count)
        {
            return -1;
        }

        for (var start = 0; start < candidate.Count; start++)
        {
            var stable = true;
            for (var index = start; index < candidate.Count; index++)
            {
                var candidateValue = useNand
                    ? candidate[index].TotalModeledServiceNandEvaluations
                    : candidate[index].TotalModeledCycles;
                var baselineValue = useNand
                    ? baseline[index].TotalModeledServiceNandEvaluations
                    : baseline[index].TotalModeledCycles;
                if (candidateValue > baselineValue)
                {
                    stable = false;
                    break;
                }
            }

            if (stable)
            {
                return start;
            }
        }

        return -1;
    }

    private static List<Build005FamilyReceipt> BuildFamilies(
        IReadOnlyList<Build005WorkloadRow> rows)
    {
        var expected = 3 * Policies.Count;
        return rows
            .GroupBy(row => row.Family, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new Build005FamilyReceipt(
                group.Key,
                expected,
                group.Count(),
                group.Sum(row => row.ControlMetrics.CorrectnessChecks),
                group.Sum(row => row.ControlMetrics.CorrectnessFailures),
                group.Count() == expected && group.All(row => row.Status == CompleteStatus)
                    ? CompleteStatus
                    : "INCOMPLETE_OR_FAILED"))
            .ToList();
    }

    private static List<Build005EvidenceGate> BuildEvidenceGates(
        bool implementedTraceCoverageComplete) =>
        [
            new(
                "IMPLEMENTED_SEMANTIC_TRACE_MATRIX",
                implementedTraceCoverageComplete,
                "The implemented 18-family, three-width, sixteen-policy matrix is exact and zero-failure when true."),
            new(
                "PRE_RESULT_TRACE_DIGEST_REGISTRY",
                false,
                "Trace digests are emitted from the executed mutable factory; no independent pre-result digest registry exists."),
            new(
                "ALL_OUTPUT_OBLIGATIONS",
                false,
                "The current corpus uses MAGNITUDE_FINAL only; predicate, exact-exponent, residual, and every-event obligations are not separately costed."),
            new(
                "PHASE_AND_TRANSITION_LEDGER",
                false,
                "Raw per-event prefix series, INGRESS/SEARCH/EXECUTE/MAINTENANCE/EGRESS rows, and settled NAND/input/state transition series are not emitted."),
            new(
                "INTEGRATED_PROPAGATION_HARDWARE",
                false,
                "Semantic propagation arithmetic is counted, but no integrated exponent/residual combiner netlist or switching trace is present."),
            new(
                "POLICY_MATCHED_CONTENT_CACHE_HARDWARE",
                false,
                "The declared cache graph is slot/generation keyed; the W-bit content-plus-divisor cache has no matching structural graph."),
            new(
                "COMPETENT_CONVENTIONAL_CONTROLS",
                false,
                "Grouped screening, full rational reduction, cumulative smooth stripping, producer-known sparse factor form, and a strict radix comparator are incomplete."),
            new(
                "CAUSAL_PRIME_ATTRIBUTION",
                false,
                "No frozen paired witness binds an odd-prime terminal transfer to saved DIVMOD while rejecting the composite-product inference."),
            new(
                "FULL_INDEPENDENT_CORRECTNESS_MATRIX",
                false,
                "The independent W8/W16/W32 checks do not yet cover every cache capacity, arithmetic policy, transaction boundary, and K+1 hostile identity."),
            new(
                "EXTERNAL_DETERMINISTIC_REPLAY",
                false,
                "Only the external verifier may establish two-run replay, zero skipped tests, inherited-evidence protection, and manifest integrity."),
        ];

    private static Build005Decision Decide(
        IReadOnlyList<Build005WorkloadRow> rows,
        IReadOnlyList<Build005BreakEvenRow> breakEven,
        IReadOnlyList<Build005EvidenceGate> evidenceGates,
        bool complete)
    {
        var exploratoryDemandFamilies = QualifyingFamilies(
            rows,
            breakEven,
            "BIN_PRIME_FRONTIER_PROP_K",
            speculationBudget: 0,
            requirePrimeAttribution: true,
            requireDecisionEligibility: false);
        var exploratorySpeculativeFamilies = QualifyingFamilies(
            rows,
            breakEven,
            policyPrefix: "BIN_PRIME_FRONTIER_SPEC_B",
            speculationBudget: null,
            requirePrimeAttribution: true,
            requireDecisionEligibility: false);
        exploratorySpeculativeFamilies.RemoveAll(
            family => !exploratoryDemandFamilies.Contains(family, StringComparer.Ordinal));
        var exploratoryGenericAdvantage =
            HasCrossWidthAdvantage(rows, breakEven, "BIN_CONTENT_ANSWER_LRU_K", requireDecisionEligibility: false) ||
            HasCrossWidthAdvantage(rows, breakEven, "BIN_FRONTIER_NOPROP_K", requireDecisionEligibility: false);
        var terminalDecisionCoverage = complete && breakEven.Any(row => row.EligibleForFrozenDecision);
        var demandFamilies = terminalDecisionCoverage
            ? QualifyingFamilies(
                rows,
                breakEven,
                "BIN_PRIME_FRONTIER_PROP_K",
                speculationBudget: 0,
                requirePrimeAttribution: true,
                requireDecisionEligibility: true)
            : [];
        var speculativeFamilies = terminalDecisionCoverage
            ? QualifyingFamilies(
                rows,
                breakEven,
                policyPrefix: "BIN_PRIME_FRONTIER_SPEC_B",
                speculationBudget: null,
                requirePrimeAttribution: true,
                requireDecisionEligibility: true)
            : [];
        speculativeFamilies.RemoveAll(family => !demandFamilies.Contains(family, StringComparer.Ordinal));
        var genericAdvantage = terminalDecisionCoverage &&
            (HasCrossWidthAdvantage(rows, breakEven, "BIN_CONTENT_ANSWER_LRU_K", requireDecisionEligibility: true) ||
             HasCrossWidthAdvantage(rows, breakEven, "BIN_FRONTIER_NOPROP_K", requireDecisionEligibility: true));
        // These axes need competent baselines that are not yet implemented.
        var producerOnly = false;
        var radixOnly = false;

        var exploratoryPattern = exploratorySpeculativeFamilies.Count > 0
            ? "EXPLORATORY_SPECULATIVE_PRIME_SIGNAL"
            : exploratoryDemandFamilies.Count > 0
                ? "EXPLORATORY_DEMAND_PRIME_SIGNAL"
                : exploratoryGenericAdvantage
                    ? "EXPLORATORY_GENERIC_REUSE_PATTERN"
                    : "NO_EXPLORATORY_REPAYMENT_PATTERN";
        var exploratorySearch = rows
            .Where(row => row.Policy.StartsWith("BIN_PRIME_FRONTIER_SPEC_B", StringComparison.Ordinal))
            .Sum(row => row.ControlMetrics.PrefetchKeysNeverRequested) > 0
            ? "BLIND_SPECULATION_INCURRED_WASTED_WORK"
            : "NO_SPECULATION_OBSERVATION";

        string label;
        string search;
        string attribution;
        if (!terminalDecisionCoverage)
        {
            label = Build005Protocol.PartialStatus;
            search = "NOT_EARNED";
            attribution = "NOT_ESTABLISHED";
        }
        else if (speculativeFamilies.Count > 0)
        {
            label = "BOUNDED_SPECULATIVE_ODD_PRIME_SCOUT_CANDIDATE";
            search = "SPECULATIVE_REPAID";
            attribution = "PRIME_PROPAGATION";
        }
        else if (demandFamilies.Count > 0)
        {
            label = "BOUNDED_DEMAND_DRIVEN_PRIME_PROPAGATION_CANDIDATE";
            search = "DEMAND_ONLY_REPAID";
            attribution = "PRIME_PROPAGATION";
        }
        else if (genericAdvantage)
        {
            label = "GENERIC_CACHE_ADVANTAGE_ONLY";
            search = "DEMAND_ONLY_REPAID";
            attribution = "GENERIC_CHECKPOINT_OR_MEMOIZATION";
        }
        else if (producerOnly)
        {
            label = "PRODUCER_PROVENANCE_ADVANTAGE_ONLY";
            search = "PRODUCER_ONLY";
            attribution = "PRIME_PROPAGATION";
        }
        else if (radixOnly)
        {
            label = "RADIX_V2_ONLY";
            search = "NOT_REPAID";
            attribution = "RADIX_V2_ONLY";
        }
        else
        {
            label = "SEARCH_DOES_NOT_REPAY";
            search = "NOT_REPAID";
            attribution = "NOT_ESTABLISHED";
        }

        return new Build005Decision(
            Build005Protocol.PartialStatus,
            label,
            terminalDecisionCoverage,
            search,
            attribution,
            "SEMANTIC",
            exploratoryPattern,
            exploratorySearch,
            terminalDecisionCoverage && demandFamilies.Count > 0,
            terminalDecisionCoverage && speculativeFamilies.Count > 0,
            terminalDecisionCoverage && genericAdvantage,
            terminalDecisionCoverage && producerOnly,
            terminalDecisionCoverage && radixOnly,
            terminalDecisionCoverage ? Array.AsReadOnly(demandFamilies.ToArray()) : Array.Empty<string>(),
            terminalDecisionCoverage ? Array.AsReadOnly(speculativeFamilies.ToArray()) : Array.Empty<string>(),
            Array.AsReadOnly(evidenceGates.Where(gate => !gate.Satisfied).Select(gate => gate.Gate).ToArray()),
            Build005Protocol.ClaimCeiling);
    }

    private static List<string> QualifyingFamilies(
        IReadOnlyList<Build005WorkloadRow> rows,
        IReadOnlyList<Build005BreakEvenRow> breakEven,
        string policyPrefix,
        int? speculationBudget,
        bool requirePrimeAttribution,
        bool requireDecisionEligibility)
    {
        var result = new List<string>();
        foreach (var family in rows.Select(row => row.Family).Distinct(StringComparer.Ordinal))
        {
            var budgets = speculationBudget.HasValue
                ? new[] { speculationBudget.Value }
                : new[] { 1, 4 };
            foreach (var budget in budgets)
            {
                var familyRows = rows.Where(row =>
                        row.Family == family &&
                        row.Policy.StartsWith(policyPrefix, StringComparison.Ordinal) &&
                        row.SpeculationBudget == budget &&
                        row.Width is 16 or 32 &&
                        row.SearchRepaymentEligible &&
                        (!requirePrimeAttribution || row.PrimeAttributionEligible))
                    .ToArray();
                foreach (var cacheSize in new[] { 1, 2, 4 })
                {
                    var candidates = familyRows.Where(row => row.CacheCapacity == cacheSize).ToArray();
                    if (candidates.Select(row => row.Width).Distinct().Count() != 2)
                    {
                        continue;
                    }

                    var comparisons = breakEven.Where(row =>
                            (!requireDecisionEligibility || row.EligibleForFrozenDecision) &&
                            row.Family == family &&
                            row.Candidate.StartsWith(policyPrefix, StringComparison.Ordinal) &&
                            row.CacheCapacity == cacheSize &&
                            row.SpeculationBudget == budget &&
                            row.Width is 16 or 32)
                        .ToArray();
                    var expectedBaselinesPerWidth = policyPrefix == "BIN_PRIME_FRONTIER_PROP_K" ? 3 : 1;
                    var stableNoWorse = comparisons.Length == 2 * expectedBaselinesPerWidth &&
                        comparisons.All(row =>
                            row.FinalNoWorseBoth &&
                            row.StableCyclePrefix >= 0 &&
                            row.StableServiceNandPrefix >= 0);
                    var attributableStrictSaving = policyPrefix == "BIN_PRIME_FRONTIER_PROP_K"
                        ? comparisons.Any(comparison =>
                            comparison.Baseline == "BIN_FRONTIER_NOPROP_K" &&
                            comparison.StrictlyBetterAtLeastOne &&
                            candidates.Any(candidate =>
                            {
                                if (candidate.Width != comparison.Width ||
                                    candidate.Policy != comparison.Candidate)
                                {
                                    return false;
                                }

                                var noPropagation = FindRow(
                                    rows,
                                    candidate,
                                    "BIN_FRONTIER_NOPROP_K",
                                    cacheSize,
                                    speculationBudget: 0);
                                return candidate.SemanticMetrics.TerminalCertificatesPropagated > 0 &&
                                    candidate.SemanticMetrics.DivModCalls < noPropagation.SemanticMetrics.DivModCalls;
                            }))
                        : comparisons.All(row => row.StrictlyBetterAtLeastOne);
                    if (stableNoWorse && attributableStrictSaving)
                    {
                        result.Add(family);
                        break;
                    }
                }

                if (result.Contains(family, StringComparer.Ordinal))
                {
                    break;
                }
            }
        }

        return result;
    }

    private static bool HasCrossWidthAdvantage(
        IReadOnlyList<Build005WorkloadRow> rows,
        IReadOnlyList<Build005BreakEvenRow> breakEven,
        string policy,
        bool requireDecisionEligibility)
    {
        foreach (var family in rows.Select(row => row.Family).Distinct(StringComparer.Ordinal))
        {
            foreach (var cacheSize in new[] { 1, 2, 4 })
            {
                var candidateRows = rows.Where(row =>
                    row.Family == family &&
                    row.Policy == policy &&
                    row.CacheCapacity == cacheSize &&
                    row.SearchRepaymentEligible &&
                    row.Width is 16 or 32).ToArray();
                if (candidateRows.Length != 2)
                {
                    continue;
                }

                var comparisons = breakEven.Where(row =>
                    (!requireDecisionEligibility || row.EligibleForFrozenDecision) &&
                    row.Family == family &&
                    row.Candidate == policy &&
                    row.Baseline == "BIN_DIRECT_BEST" &&
                    row.CacheCapacity == cacheSize &&
                    row.Width is 16 or 32).ToArray();
                var wins = comparisons.Length == 2 &&
                    comparisons.All(row =>
                        row.FinalNoWorseBoth &&
                        row.StableCyclePrefix >= 0 &&
                        row.StableServiceNandPrefix >= 0) &&
                    comparisons.Any(row => row.StrictlyBetterAtLeastOne);
                if (wins)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Build005IndependentCorrectness RunIndependentCorrectness()
    {
        var groups = new Dictionary<string, long>(StringComparer.Ordinal);
        var failures = new List<string>();
        long checks = 0;
        long failureCount = 0;

        void CheckGroup(string group, bool condition, string detail)
        {
            checks++;
            groups[group] = groups.GetValueOrDefault(group) + 1;
            if (!condition)
            {
                failureCount++;
                if (failures.Count < 100)
                {
                    failures.Add($"{group}:{detail}");
                }
            }
        }

        foreach (var policy in new[]
                 {
                     (ValuationCachePolicy.BinDirectBest, 0),
                     (ValuationCachePolicy.BinContentAnswerLruK, 4),
                     (ValuationCachePolicy.BinFrontierNoPropK, 4),
                     (ValuationCachePolicy.BinPrimeFrontierPropK, 4),
                 })
        {
            var service = new DemandValuationService(8, policy.Item1, policy.Item2);
            for (ulong value = 0; value <= byte.MaxValue; value++)
            {
                var loaded = service.Load(0, value);
                CheckGroup("W8_VALUATION", loaded.Succeeded, $"load:{policy.Item1}:{value}");
                foreach (var prime in Build005Protocol.PrimeCatalog)
                {
                    var actual = service.Valuation(0, prime);
                    var expected = OracleValuation(value, prime);
                    CheckGroup(
                        "W8_VALUATION",
                        actual.Succeeded &&
                        actual.Value is not null &&
                        actual.Value.Exponent == expected.Exponent &&
                        actual.Value.Residual == expected.Residual &&
                        actual.Value.Infinite == expected.Infinite,
                        $"{policy.Item1}:{value}:{prime}");
                }
            }
        }

        var arithmetic = new DemandValuationService(8, ValuationCachePolicy.BinPrimeFrontierPropK, 4);
        for (ulong left = 0; left <= byte.MaxValue; left++)
        {
            for (ulong right = 0; right <= byte.MaxValue; right++)
            {
                arithmetic.Load(0, left);
                arithmetic.Load(1, right);
                var add = arithmetic.Add(2, 0, 1);
                var addFits = left + right <= byte.MaxValue;
                CheckGroup(
                    "W8_ARITHMETIC",
                    add.Succeeded == addFits &&
                    (!addFits || arithmetic.SnapshotSlot(2).Magnitude == left + right),
                    $"add:{left}:{right}");
                arithmetic.Load(2, 0);
                var multiply = arithmetic.Multiply(2, 0, 1);
                var multiplyFits = right == 0 || left <= byte.MaxValue / right;
                CheckGroup(
                    "W8_ARITHMETIC",
                    multiply.Succeeded == multiplyFits &&
                    (!multiplyFits || arithmetic.SnapshotSlot(2).Magnitude == left * right),
                    $"multiply:{left}:{right}");
            }
        }

        foreach (var width in new[] { 16, 32 })
        {
            var random = new Build005SplitMix64(Build005Protocol.DeriveSeed(width, "INDEPENDENT_CORRECTNESS"));
            var maximum = width == 32 ? uint.MaxValue : (1UL << width) - 1;
            var direct = new DemandValuationService(width, ValuationCachePolicy.BinDirectBest, 0);
            var cached = new DemandValuationService(width, ValuationCachePolicy.BinPrimeFrontierPropK, 4);
            for (var index = 0; index < 10_000; index++)
            {
                var value = random.NextUInt64() & maximum;
                var prime = Build005Protocol.PrimeCatalog[(int)random.NextBelow((ulong)Build005Protocol.PrimeCatalog.Length)];
                direct.Load(0, value);
                cached.Load(0, value);
                var expected = OracleValuation(value, prime);
                var first = direct.Valuation(0, prime);
                var second = cached.Valuation(0, prime);
                CheckGroup(
                    $"W{width}_SEEDED",
                    first.Value is not null && second.Value is not null &&
                    first.Value == second.Value &&
                    first.Value.Exponent == expected.Exponent &&
                    first.Value.Residual == expected.Residual &&
                    first.Value.Infinite == expected.Infinite,
                    $"valuation:{index}");
            }
        }

        return new Build005IndependentCorrectness(
            checks,
            failureCount,
            new ReadOnlyDictionary<string, long>(groups),
            new ReadOnlyCollection<string>(failures));
    }

    private static OracleValuationResult OracleValuation(ulong value, int divisor)
    {
        if (value == 0)
        {
            return new OracleValuationResult(0, 0, Infinite: true);
        }

        var residual = value;
        var exponent = 0;
        while (residual % (ulong)divisor == 0)
        {
            residual /= (ulong)divisor;
            exponent++;
        }

        return new OracleValuationResult(exponent, residual, Infinite: false);
    }

    private static ulong OracleGcd(ulong left, ulong right, out long moduloSteps)
    {
        moduloSteps = 0;
        while (right != 0)
        {
            moduloSteps++;
            (left, right) = (right, left % right);
        }

        return left;
    }

    private static ulong RequireOracle(IReadOnlyList<ulong?> oracle, int slot) =>
        oracle[slot] ?? throw new InvalidOperationException($"Trace referenced uninitialized slot {slot}.");

    private static void Check(MutableControlMetrics metrics, bool condition, string detail)
    {
        metrics.CorrectnessChecks++;
        if (!condition)
        {
            metrics.CorrectnessFailures++;
            metrics.FailureDetails.Add(detail);
        }
    }

    private static Build005PolicyConfiguration[] CreatePolicies()
    {
        var policies = new List<Build005PolicyConfiguration>
        {
            new("BIN_DIRECT_BEST", ValuationCachePolicy.BinDirectBest, 0, 0),
        };
        foreach (var capacity in new[] { 1, 2, 4 })
        {
            policies.Add(new("BIN_CONTENT_ANSWER_LRU_K", ValuationCachePolicy.BinContentAnswerLruK, capacity, 0));
            policies.Add(new("BIN_FRONTIER_NOPROP_K", ValuationCachePolicy.BinFrontierNoPropK, capacity, 0));
            policies.Add(new("BIN_PRIME_FRONTIER_PROP_K", ValuationCachePolicy.BinPrimeFrontierPropK, capacity, 0));
            policies.Add(new($"BIN_PRIME_FRONTIER_SPEC_B1_K", ValuationCachePolicy.BinPrimeFrontierPropK, capacity, 1));
            policies.Add(new($"BIN_PRIME_FRONTIER_SPEC_B4_K", ValuationCachePolicy.BinPrimeFrontierPropK, capacity, 4));
        }

        return policies.ToArray();
    }

    private readonly record struct OracleValuationResult(int Exponent, ulong Residual, bool Infinite);

    private readonly record struct SpeculationKey(int Slot, byte Generation, int Prime);

    private sealed class MutableControlMetrics
    {
        public long QueryRequests;
        public long SpeculationQueries;
        public long SpeculationDivmodSteps;
        public long PrefetchToLaterRequestDistanceTotal;
        public long PrefetchToLaterRequestDistanceMaximum;
        public long GcdCalls;
        public long GcdModuloSteps;
        public long DeclaredGroupedScreenRemainderProxy;
        public long CompositeDivmodCalls;
        public long MagnitudeOutputs;
        public long CorrectnessChecks;
        public long CorrectnessFailures;
        public List<string> FailureDetails { get; } = [];

        public Build005ControlMetrics Snapshot(
            IReadOnlySet<SpeculationKey> laterRequested,
            IReadOnlyDictionary<SpeculationKey, int> prefetched) =>
            new(
                QueryRequests,
                SpeculationQueries,
                SpeculationDivmodSteps,
                laterRequested.Count,
                prefetched.Count - laterRequested.Count,
                PrefetchToLaterRequestDistanceTotal,
                PrefetchToLaterRequestDistanceMaximum,
                GcdCalls,
                GcdModuloSteps,
                DeclaredGroupedScreenRemainderProxy,
                CompositeDivmodCalls,
                MagnitudeOutputs,
                CorrectnessChecks,
                CorrectnessFailures);
    }

    private static Build005PrefixCost ToPrefix(this Build005ModeledDynamicCost cost, int eventIndex) =>
        new(eventIndex, cost.TotalModeledCycles, cost.TotalModeledServiceNandEvaluations);
}
