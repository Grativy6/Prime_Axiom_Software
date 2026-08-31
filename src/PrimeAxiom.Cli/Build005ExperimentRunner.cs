using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PrimeAxiom.Cli;

internal sealed record Build005RunReceipt(
    string OutputDirectory,
    long CheckCount,
    long FailureCount,
    string GeneratedStatus,
    string CandidateTerminalLabel,
    string SearchPolicy,
    string Attribution,
    string EvidenceBoundary);

internal static class Build005ExperimentRunner
{
    private static readonly int[] DecisionWidths = { 8, 16, 32 };

    private static readonly string[] WorkloadMatrixHeader =
    {
        "protocol_id", "width", "family", "trace_id", "trace_sha256", "source_regime",
        "output_obligation", "hostile", "prime_attribution_eligible", "search_repayment_eligible",
        "policy", "cache_capacity", "speculation_budget", "requested_instructions", "ctz_calls",
        "ctz_bit_inspections", "divmod_calls", "exact_divisions", "failed_divisibility_probes",
        "frontier_refinements", "cache_lookups", "cache_tag_comparisons", "cache_hits",
        "positive_cache_hits", "negative_cache_hits", "cache_misses", "cache_fills", "cache_updates",
        "cache_evictions", "cache_invalidations", "cache_transfers", "stale_hit_rejections",
        "generation_wrap_flushes", "terminal_certificates_earned", "lower_bound_certificates_earned",
        "terminal_certificates_propagated", "lower_bound_certificates_propagated",
        "propagation_exponent_adds", "propagation_residual_multiplies", "query_requests",
        "speculation_queries", "speculation_divmod_steps", "prefetch_keys_later_requested", "prefetch_keys_never_requested",
        "prefetch_to_later_request_distance_total", "prefetch_to_later_request_distance_maximum", "gcd_calls",
        "gcd_modulo_steps", "declared_grouped_screen_remainder_proxy", "composite_divmod_calls", "magnitude_outputs",
        "shared_magnitude_cycles", "propagation_arithmetic_cycles", "query_issue_cycles", "odd_divmod_cycles", "cache_maintenance_cycles",
        "total_modeled_cycles", "catalogue_nand_evaluations", "ctz_nand_evaluations",
        "odd_divmod_nand_evaluations", "cache_nand_evaluations", "total_modeled_service_nand_evaluations",
        "modeled_evidence_class", "excluded_shared_cost", "correctness_checks", "correctness_failures", "status",
    };

    private static readonly string[] BreakEvenHeader =
    {
        "width", "family", "trace_id", "candidate", "baseline", "cache_capacity",
        "speculation_budget", "stable_cycle_prefix", "stable_service_nand_prefix",
        "final_cycle_delta", "final_service_nand_delta", "final_no_worse_both",
        "strictly_better_at_least_one", "eligible_for_frozen_decision", "attribution",
    };

    private static readonly string[] StaticCostHeader =
    {
        "width", "cache_capacity", "component", "nand2_static", "dff_static", "state_bits",
        "input_bits", "output_bits", "port_bits", "wire_bits", "connections_static",
        "maximum_fanout", "unit_nand_critical_depth", "cross_region_connections",
        "cache_line_bits", "combinational_loop_status", "evidence_class", "integrated_netlist",
    };

    internal static readonly IReadOnlyList<string> ExpectedFiles = Array.AsReadOnly(new[]
    {
        "README.md",
        "correctness.json",
        "trace_inventory.json",
        "workload_matrix.csv",
        "break_even.csv",
        "static_costs.csv",
        "attribution.json",
        "protocol_coverage.json",
        "manifest.json",
    });

    public static Build005RunReceipt Run(string repositoryRoot, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        outputDirectory = Path.GetFullPath(outputDirectory);
        Build005Protocol.VerifyFrozenPlan(repositoryRoot);
        ValidateOutputLocation(repositoryRoot, outputDirectory);
        ValidateExistingOutput(outputDirectory);

        var parent = Directory.GetParent(outputDirectory)?.FullName ??
            throw new InvalidOperationException("Build 005 output must have a parent directory.");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".build005-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        Build005CampaignResult campaign;
        try
        {
            campaign = Build005Campaign.Run();
            WriteCampaign(staging, campaign);
            ValidateGeneratedInventory(staging);
            CommitOutput(outputDirectory, staging);
        }
        catch (Exception exception)
        {
            if (Directory.Exists(staging))
            {
                Build005Protocol.WriteLfText(
                    Path.Combine(staging, "FAILED_GENERATION.txt"),
                    $"Build 005 generation failed before publication.\n{exception.GetType().Name}: {exception.Message}\n");
            }

            throw;
        }

        return new Build005RunReceipt(
            outputDirectory,
            campaign.Checks,
            campaign.Failures,
            campaign.Decision.GeneratedStatus,
            campaign.Decision.CandidateTerminalLabel,
            campaign.Decision.SearchPolicy,
            campaign.Decision.Attribution,
            campaign.Decision.EvidenceBoundary);
    }

    private static void WriteCampaign(string outputDirectory, Build005CampaignResult campaign)
    {
        WriteCorrectness(Path.Combine(outputDirectory, "correctness.json"), campaign);
        WriteTraceInventory(Path.Combine(outputDirectory, "trace_inventory.json"), campaign);
        WriteWorkloadMatrix(Path.Combine(outputDirectory, "workload_matrix.csv"), campaign.WorkloadRows);
        WriteBreakEven(Path.Combine(outputDirectory, "break_even.csv"), campaign.BreakEvenRows);
        WriteStaticCosts(Path.Combine(outputDirectory, "static_costs.csv"), campaign.StaticCosts);
        Build005Protocol.WriteJson(Path.Combine(outputDirectory, "attribution.json"), campaign.Decision);
        WriteCoverage(Path.Combine(outputDirectory, "protocol_coverage.json"), campaign);
        WriteReadme(Path.Combine(outputDirectory, "README.md"), campaign);
        WriteManifest(outputDirectory, campaign);
    }

    private static void WriteCorrectness(string path, Build005CampaignResult campaign) =>
        Build005Protocol.WriteJson(path, new
        {
            Schema = "prime-axiom-build005-correctness-v1",
            Build005Protocol.ProtocolId,
            MasterSeed = Build005Protocol.MasterSeed.ToString("X16", CultureInfo.InvariantCulture),
            ImplementedStatus = campaign.Failures == 0 && campaign.ImplementedTraceCoverageComplete
                ? Build005Campaign.CompleteStatus
                : Build005Protocol.PartialStatus,
            FrozenDecisionStatus = Build005Protocol.PartialStatus,
            campaign.Checks,
            campaign.Failures,
            Independent = campaign.Correctness,
            WorkloadChecks = campaign.WorkloadRows.Sum(row => row.ControlMetrics.CorrectnessChecks),
            WorkloadFailures = campaign.WorkloadRows.Sum(row => row.ControlMetrics.CorrectnessFailures),
            CampaignHasNoSkipMechanism = true,
            TestAssemblySkippedCount = "ESTABLISHED_ONLY_BY_EXTERNAL_TRX_VERIFIER",
            Build005Protocol.ClaimCeiling,
        });

    private static void WriteTraceInventory(string path, Build005CampaignResult campaign) =>
        Build005Protocol.WriteJson(path, new
        {
            Schema = "prime-axiom-build005-trace-inventory-v1",
            Build005Protocol.ProtocolId,
            Traces = campaign.WorkloadRows
                .GroupBy(
                    row => new
                    {
                        row.Width,
                        row.Family,
                        row.TraceId,
                        row.TraceSha256,
                        row.SourceRegime,
                        row.OutputObligation,
                        row.Hostile,
                        row.PrimeAttributionEligible,
                        row.SearchRepaymentEligible,
                    })
                .Select(group => new
                {
                    group.Key.Width,
                    group.Key.Family,
                    group.Key.TraceId,
                    group.Key.TraceSha256,
                    group.Key.SourceRegime,
                    group.Key.OutputObligation,
                    group.Key.Hostile,
                    group.Key.PrimeAttributionEligible,
                    group.Key.SearchRepaymentEligible,
                    EventCount = group.First().PrefixCosts.Count,
                    PolicyRows = group.Count(),
                })
                .OrderBy(row => row.Width)
                .ThenBy(row => row.Family, StringComparer.Ordinal)
                .ToArray(),
        });

    private static void WriteCoverage(string path, Build005CampaignResult campaign) =>
        Build005Protocol.WriteJson(path, new
        {
            Schema = "prime-axiom-build005-protocol-coverage-v1",
            Build005Protocol.ProtocolId,
            Build005Protocol.FrozenPlanSha256,
            Build005Protocol.BaselineCommit,
            Build005Protocol.FreezeCommit,
            GeneratedStatus = campaign.Decision.GeneratedStatus,
            campaign.Decision.CandidateTerminalLabel,
            ExternalVerificationRequired = true,
            campaign.ImplementedTraceCoverageComplete,
            campaign.CompleteFrozenCoverage,
            campaign.EvidenceGates,
            UnmetGates = campaign.Decision.UnmetGates,
            Widths = DecisionWidths,
            Build005Protocol.CacheSizes,
            Build005Protocol.SpeculationBudgets,
            Build005Protocol.PrimeCatalog,
            Build005Protocol.CompositeControls,
            Policies = Build005Campaign.Policies,
            Families = campaign.Families,
            WorkloadRows = campaign.WorkloadRows.Count,
            BreakEvenRows = campaign.BreakEvenRows.Count,
            StaticRows = campaign.StaticCosts.Count,
            RequiredTraceFamilies = Build005Workloads.Create(8).Select(trace => trace.Family).ToArray(),
            Evidence = new
            {
                Semantic = "EXACT_BOUNDED",
                DeclaredLogical = "EXPLORATORY_COMPONENT_INVENTORY_NOT_DECISION_ELIGIBLE",
                IntegratedNetlist = false,
                ExactLruHardware = "NOT_INTEGRATED",
                GenerationWrapDetectionHardware = "NOT_INTEGRATED",
                HdlSynthesis = "NOT_MEASURED",
                FpgaPlaceAndRoute = "NOT_MEASURED",
                OpenCellMapping = "NOT_MEASURED",
                PhysicalMeasurement = "NOT_MEASURED",
                HostElapsedTime = "NOT_MEASURED",
                HostAllocation = "NOT_MEASURED",
                SpeculationKeyFate = "LATER_REQUESTED_VS_NEVER_REQUESTED_KEYS_ONLY__RESIDENCY_AND_AVOIDED_WORK_NOT_ESTABLISHED",
                GroupedScreenControl = "DECLARED_REMAINDER_PROXY_ONLY__NOT_EXECUTED_OR_COSTED",
                GcdAndMagnitudeOutputCosts = "COUNTERS_PRESENT__EXCLUDED_FROM_MODELED_TOTAL",
            },
            DeterministicReplay = "ESTABLISHED_ONLY_BY_EXTERNAL_TWO_RUN_VERIFIER",
            InheritedEvidence = "PROTECTED_ONLY_BY_EXTERNAL_VERIFIER",
            Build005Protocol.ClaimCeiling,
        });

    private static void WriteReadme(string path, Build005CampaignResult campaign)
    {
        var decision = campaign.Decision;
        var builder = new StringBuilder();
        builder.AppendLine("# Build 005 generated evidence");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"> **Generated status: `{decision.GeneratedStatus}`**");
        builder.AppendLine(CultureInfo.InvariantCulture, $"> Frozen decision: `{decision.CandidateTerminalLabel}`");
        builder.AppendLine();
        builder.AppendLine("This result set tests whether a four-line-or-smaller exact valuation-frontier cache repays bounded small-prime search above authoritative binary magnitude. Prime propagation, generic memoization, generic checkpointing, radix-2 locality, and speculative search remain separate attribution axes.");
        builder.AppendLine();
        builder.AppendLine("## Implemented trace coverage");
        builder.AppendLine();
        foreach (var family in campaign.Families)
        {
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"- `{family.Family}`: {family.Rows.ToString(CultureInfo.InvariantCulture)}/{family.ExpectedRows.ToString(CultureInfo.InvariantCulture)} rows; {family.Checks.ToString(CultureInfo.InvariantCulture)} checks; {family.Failures.ToString(CultureInfo.InvariantCulture)} failures; `{family.Status}`");
        }

        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Total checks: {campaign.Checks.ToString(CultureInfo.InvariantCulture)}; failures: {campaign.Failures.ToString(CultureInfo.InvariantCulture)}.");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Workload rows: {campaign.WorkloadRows.Count.ToString(CultureInfo.InvariantCulture)}; break-even comparisons: {campaign.BreakEvenRows.Count.ToString(CultureInfo.InvariantCulture)}; static component rows: {campaign.StaticCosts.Count.ToString(CultureInfo.InvariantCulture)}.");
        builder.AppendLine();
        builder.AppendLine("## Frozen decision");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"- search policy: `{decision.SearchPolicy}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- attribution: `{decision.Attribution}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- evidence boundary: `{decision.EvidenceBoundary}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- decision axes earned: `{B(decision.DecisionAxesEarned)}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- qualifying demand families: `{JoinOrNone(decision.QualifyingDemandFamilies)}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- qualifying speculative families: `{JoinOrNone(decision.QualifyingSpeculativeFamilies)}`");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Exploratory pattern: `{decision.ExploratoryObservedPattern}`.");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Exploratory search observation: `{decision.ExploratorySearchObservation}`.");
        builder.AppendLine();
        builder.AppendLine("No exploratory break-even row is eligible for the frozen decision. Prime-specific optimization is not established.");
        builder.AppendLine();
        builder.AppendLine("### Unmet frozen gates");
        builder.AppendLine();
        foreach (var gate in campaign.EvidenceGates.Where(gate => !gate.Satisfied))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"- `{gate.Gate}`: {gate.Evidence}");
        }

        builder.AppendLine();
        builder.AppendLine("The NAND/DFF material is an exact additive inventory over separately built catalogue, CTZ, divider, and slot/generation-cache graphs. It is not the policy-matched content cache, propagation combiner, integrated service, phase/transition ledger, synthesis result, or physical measurement. Its totals are exploratory and do not drive the decision.");
        builder.AppendLine();
        builder.AppendLine("Regenerate with:");
        builder.AppendLine();
        builder.AppendLine("```powershell");
        builder.AppendLine("dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build005 --output results/build005");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine(Build005Protocol.ClaimCeiling);
        Build005Protocol.WriteLfText(path, builder.ToString());
    }

    private static void WriteWorkloadMatrix(
        string path,
        IReadOnlyList<Build005WorkloadRow> rows)
    {
        var output = new List<IReadOnlyList<string>>
        {
            WorkloadMatrixHeader,
        };
        foreach (var row in rows)
        {
            var semantic = row.SemanticMetrics;
            var control = row.ControlMetrics;
            var modeled = row.ModeledCost;
            output.Add(new[]
            {
                row.ProtocolId,
                I(row.Width),
                row.Family,
                row.TraceId,
                row.TraceSha256,
                row.SourceRegime,
                row.OutputObligation,
                B(row.Hostile),
                B(row.PrimeAttributionEligible),
                B(row.SearchRepaymentEligible),
                row.Policy,
                I(row.CacheCapacity),
                I(row.SpeculationBudget),
                I(semantic.RequestedInstructions),
                I(semantic.CtzCalls),
                I(semantic.CtzBitInspections),
                I(semantic.DivModCalls),
                I(semantic.ExactDivisions),
                I(semantic.FailedDivisibilityProbes),
                I(semantic.FrontierRefinements),
                I(semantic.CacheLookups),
                I(semantic.CacheTagComparisons),
                I(semantic.CacheHits),
                I(semantic.PositiveCacheHits),
                I(semantic.NegativeCacheHits),
                I(semantic.CacheMisses),
                I(semantic.CacheFills),
                I(semantic.CacheUpdates),
                I(semantic.CacheEvictions),
                I(semantic.CacheInvalidations),
                I(semantic.CacheTransfers),
                I(semantic.RejectedStaleHitAttempts),
                I(semantic.GenerationWrapFlushes),
                I(semantic.TerminalCertificatesEarned),
                I(semantic.LowerBoundCertificatesEarned),
                I(semantic.TerminalCertificatesPropagated),
                I(semantic.LowerBoundCertificatesPropagated),
                I(semantic.PropagationExponentAdds),
                I(semantic.PropagationResidualMultiplies),
                I(control.QueryRequests),
                I(control.SpeculationQueries),
                I(control.SpeculationDivmodSteps),
                I(control.PrefetchKeysLaterRequested),
                I(control.PrefetchKeysNeverRequested),
                I(control.PrefetchToLaterRequestDistanceTotal),
                I(control.PrefetchToLaterRequestDistanceMaximum),
                I(control.GcdCalls),
                I(control.GcdModuloSteps),
                I(control.DeclaredGroupedScreenRemainderProxy),
                I(control.CompositeDivmodCalls),
                I(control.MagnitudeOutputs),
                I(modeled.SharedMagnitudeCycles),
                I(modeled.PropagationArithmeticCycles),
                I(modeled.QueryIssueCycles),
                I(modeled.OddDivmodCycles),
                I(modeled.CacheMaintenanceCycles),
                I(modeled.TotalModeledCycles),
                I(modeled.CatalogueNandEvaluations),
                I(modeled.CtzNandEvaluations),
                I(modeled.OddDivmodNandEvaluations),
                I(modeled.CacheNandEvaluations),
                I(modeled.TotalModeledServiceNandEvaluations),
                modeled.EvidenceClass,
                modeled.ExcludedSharedCost,
                I(control.CorrectnessChecks),
                I(control.CorrectnessFailures),
                row.Status,
            });
        }

        WriteCsv(path, output);
    }

    private static void WriteBreakEven(
        string path,
        IReadOnlyList<Build005BreakEvenRow> rows)
    {
        var output = new List<IReadOnlyList<string>>
        {
            BreakEvenHeader,
        };
        output.AddRange(rows.Select(row => (IReadOnlyList<string>)new[]
        {
            I(row.Width), row.Family, row.TraceId, row.Candidate, row.Baseline,
            I(row.CacheCapacity), I(row.SpeculationBudget), I(row.StableCyclePrefix),
            I(row.StableServiceNandPrefix), I(row.FinalCycleDelta), I(row.FinalServiceNandDelta),
            B(row.FinalNoWorseBoth), B(row.StrictlyBetterAtLeastOne),
            B(row.EligibleForFrozenDecision), row.Attribution,
        }));
        WriteCsv(path, output);
    }

    private static void WriteStaticCosts(
        string path,
        IReadOnlyList<Build005StaticCostRow> rows)
    {
        var output = new List<IReadOnlyList<string>>
        {
            StaticCostHeader,
        };
        output.AddRange(rows.Select(row => (IReadOnlyList<string>)new[]
        {
            I(row.Width), I(row.CacheCapacity), row.Component, I(row.Nand2Static), I(row.DffStatic),
            I(row.StateBits), I(row.InputBits), I(row.OutputBits), I(row.PortBits), I(row.WireBits),
            I(row.ConnectionsStatic), I(row.MaximumFanout), I(row.UnitNandCriticalDepth),
            I(row.CrossRegionConnections), I(row.CacheLineBits), row.CombinationalLoopStatus,
            row.EvidenceClass, B(row.IntegratedNetlist),
        }));
        WriteCsv(path, output);
    }

    private static void WriteManifest(string outputDirectory, Build005CampaignResult campaign)
    {
        var entries = ExpectedFiles
            .Where(file => file != "manifest.json")
            .OrderBy(file => file, StringComparer.Ordinal)
            .Select(file =>
            {
                var path = Path.Combine(outputDirectory, file);
                return new
                {
                    Path = file,
                    Bytes = new FileInfo(path).Length,
                    Sha256 = Build005Protocol.FileSha256(path),
                };
            })
            .ToArray();
        Build005Protocol.WriteJson(Path.Combine(outputDirectory, "manifest.json"), new
        {
            Schema = "prime-axiom-build005-manifest-v1",
            Build005Protocol.ProtocolId,
            Build005Protocol.FrozenPlanSha256,
            Build005Protocol.BaselineCommit,
            Build005Protocol.FreezeCommit,
            GeneratedStatus = campaign.Decision.GeneratedStatus,
            campaign.Decision.CandidateTerminalLabel,
            campaign.Decision.DecisionAxesEarned,
            campaign.ImplementedTraceCoverageComplete,
            campaign.CompleteFrozenCoverage,
            UnmetGates = campaign.Decision.UnmetGates,
            campaign.Checks,
            campaign.Failures,
            Runtime = Environment.Version.ToString(),
            Platform = Environment.OSVersion.Platform.ToString(),
            CanonicalRegenerationCommand = "dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build005 --output results/build005",
            GenerationContext = "OUTPUT_PATH_EXCLUDED_FOR_CROSS_DIRECTORY_BYTE_REPLAY",
            SelfExcluding = true,
            Entries = entries,
            Build005Protocol.ClaimCeiling,
        });
    }

    private static void ValidateOutputLocation(string repositoryRoot, string outputDirectory)
    {
        RejectReparsePointTraversal(outputDirectory);
        if (string.Equals(repositoryRoot, outputDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Build 005 output cannot be the repository root.");
        }

        var relative = Path.GetRelativePath(repositoryRoot, outputDirectory);
        var isOutsideRepository = string.Equals(relative, "..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
        if (!isOutsideRepository &&
            !string.Equals(relative, Path.Combine("results", "build005"), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Inside the repository, Build 005 owns only results/build005. Use an external temporary directory for replay.");
        }
    }

    private static void ValidateExistingOutput(string outputDirectory)
    {
        if (File.Exists(outputDirectory))
        {
            throw new InvalidOperationException("Build 005 output is a file, not a directory.");
        }

        if (!Directory.Exists(outputDirectory))
        {
            return;
        }

        RejectReparsePointTraversal(outputDirectory);
        var entries = Directory.EnumerateFileSystemEntries(outputDirectory).ToArray();
        if (entries.Length == 0)
        {
            return;
        }

        if (!IsOwnedBuild005Directory(outputDirectory))
        {
            throw new InvalidOperationException(
                "Refusing to replace a nonempty directory that is not an intact manifest-owned Build 005 result set.");
        }
    }

    private static bool IsOwnedBuild005Directory(string directory)
    {
        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        try
        {
            RejectReparsePointTraversal(directory);
            var actualInventory = Directory.EnumerateFileSystemEntries(directory)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var expectedInventory = ExpectedFiles
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (!actualInventory.SequenceEqual(expectedInventory, StringComparer.Ordinal))
            {
                return false;
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("schema", out var schema) ||
                schema.GetString() != "prime-axiom-build005-manifest-v1" ||
                !root.TryGetProperty("protocolId", out var protocol) ||
                protocol.GetString() != Build005Protocol.ProtocolId ||
                !root.TryGetProperty("frozenPlanSha256", out var frozenPlan) ||
                frozenPlan.GetString() != Build005Protocol.FrozenPlanSha256 ||
                !root.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var expectedPayloads = ExpectedFiles
                .Where(file => file != "manifest.json")
                .ToHashSet(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("path", out var pathProperty) ||
                    !entry.TryGetProperty("bytes", out var bytesProperty) ||
                    !entry.TryGetProperty("sha256", out var hashProperty))
                {
                    return false;
                }

                var relative = pathProperty.GetString();
                var expectedHash = hashProperty.GetString();
                if (relative is null || expectedHash is null ||
                    Path.GetFileName(relative) != relative ||
                    !expectedPayloads.Contains(relative) ||
                    !seen.Add(relative))
                {
                    return false;
                }

                var path = Path.Combine(directory, relative);
                var item = new FileInfo(path);
                if (!item.Exists ||
                    (item.Attributes & FileAttributes.ReparsePoint) != 0 ||
                    item.Length != bytesProperty.GetInt64() ||
                    Build005Protocol.FileSha256(path) != expectedHash)
                {
                    return false;
                }
            }

            return seen.SetEquals(expectedPayloads);
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void ValidateGeneratedInventory(string directory)
    {
        RejectReparsePoint(directory);
        if (Directory.EnumerateDirectories(directory).Any())
        {
            throw new InvalidOperationException("Generated Build 005 staging output contains a subdirectory.");
        }

        var actual = Directory.EnumerateFiles(directory)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var expected = ExpectedFiles.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Generated Build 005 staging inventory is incomplete or unexpected.");
        }
    }

    private static void CommitOutput(string outputDirectory, string staging)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.Move(staging, outputDirectory);
            return;
        }

        var backup = $"{outputDirectory}.previous-{Guid.NewGuid():N}";
        Directory.Move(outputDirectory, backup);
        try
        {
            Directory.Move(staging, outputDirectory);
        }
        catch
        {
            if (!Directory.Exists(outputDirectory) && Directory.Exists(backup))
            {
                Directory.Move(backup, outputDirectory);
            }

            throw;
        }

        ValidateDisposableOwnedDirectory(backup);
        Directory.Delete(backup, recursive: true);
    }

    private static void ValidateDisposableOwnedDirectory(string directory)
    {
        var full = Path.GetFullPath(directory);
        if (!Path.GetFileName(full).Contains(".previous-", StringComparison.Ordinal) ||
            !Directory.Exists(full))
        {
            throw new InvalidOperationException("Refusing to delete a directory that is not a validated Build 005 backup.");
        }

        RejectReparsePointTraversal(full);
        var entries = Directory.EnumerateFileSystemEntries(full).ToArray();
        if (entries.Length == 0)
        {
            return;
        }

        if (!IsOwnedBuild005Directory(full))
        {
            throw new InvalidOperationException(
                "Refusing to delete a backup that is not an intact manifest-owned Build 005 result set.");
        }
    }

    private static void RejectReparsePointTraversal(string path)
    {
        var current = Path.GetFullPath(path);
        while (!File.Exists(current) && !Directory.Exists(current))
        {
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }

        while (!string.IsNullOrEmpty(current) && (File.Exists(current) || Directory.Exists(current)))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    $"Build 005 output may not traverse a symbolic link or junction: {current}");
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException($"Build 005 refuses reparse-point output '{path}'.");
        }
    }

    private static void WriteCsv(string path, IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendJoin(',', row.Select(Csv));
            builder.Append('\n');
        }

        Build005Protocol.WriteLfText(path, builder.ToString());
    }

    private static string Csv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }

    private static string I(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string B(bool value) => value ? "true" : "false";

    private static string JoinOrNone(IReadOnlyList<string> values) =>
        values.Count == 0 ? "NONE" : string.Join(";", values);
}
