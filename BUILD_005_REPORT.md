# Prime Axiom Hardware — Build 005 report

> **`PARTIAL — FINAL DECISION NOT EARNED`**

Build 005 does **not** establish that looking for primes is an efficient compute
structure. It does establish a narrower and useful result: an exact,
version-bound valuation frontier can be built above ordinary binary magnitude,
can survive hostile mutation and cache traces without arithmetic error, and can
reuse or legally transport multiplicative evidence. In the implemented model,
however, the clearest savings are generic memoization and checkpointing. Blind
small-prime scouting adds work, and the one prime-specific propagation saving
does not reproduce at both decision widths or beat all required controls.

The best current direction is therefore **preserve multiplicative provenance
when a producer or demanded query has already earned it; do not continuously
scan for primes merely because the machine is idle**. That is a research lead,
not an earned optimization claim.

## Status and claim boundary

- Protocol: `PAH-BUILD005-DEMAND-VALUATION-0001`
- Frozen plan SHA-256:
  `8B76649A4D4E7E60B756BCFB5FDA7954385A10A9E6DDD520C97123B845CE9031`
- Baseline commit: `1fff29e2f1e454921aa51cb4a91bd5b41821ebcc`
- Freeze commit: `3ffb86b`
- Generated status: `PARTIAL — FINAL DECISION NOT EARNED`
- Decision axes: `search_policy=NOT_EARNED`,
  `attribution=NOT_ESTABLISHED`, `evidence_boundary=SEMANTIC`

The evidence is bounded exact semantic, host-software, and declared additive
NAND/DFF component evidence only. Build 005 makes no universal arithmetic,
novelty, FPGA/ASIC place-and-route, physical-area, timing, energy,
fabricated-hardware, cryptographic, or PAL-conformance claim. The frozen Build
002 result remains unchanged.

## What was actually built

Binary magnitude remains authoritative. Prime identity enters through a frozen
validated catalogue, not through NAND gates discovering primality:

```text
2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31
```

For a nonzero value `n` and selected prime `p`, the experimental service retains
an exact frontier:

```text
(valid, slot, generation, prime-index, L, residual R, terminal, infinite)
n = p^L * R
```

An unterminated line is a resumable lower-bound receipt. A terminal line is an
exact valuation receipt because the final failed probe establishes that `p`
does not divide `R`. Zero, one, overflow, rejection, mutation, stale tags, and
generation wrap have explicit behavior.

The executable artifacts are:

| Layer | Files | Implemented role |
|---|---|---|
| Semantic contract | `src/PrimeAxiom.Core/Build005/Valuation/ValuationContracts.cs`, `DemandValuationService.cs` | Exact W8/W16/W32 load, threshold test, valuation, stripping, add, multiply, multiply-by-prime, cache, transfer, invalidation, and wrap behavior |
| Declared logical components | `src/PrimeAxiom.Core/Build005/Hardware/Build005HardwareDomain.cs`, `RadixAndOddDivmodHardware.cs`, `FrontierCacheHardware.cs` | NAND/DFF prime-catalogue selector, radix-2 CTZ, sequential odd DIVMOD, and K=0/1/2/4 slot-generation cache components |
| Protocol and implemented traces | `src/PrimeAxiom.Cli/Build005Protocol.cs`, `Build005Workloads.cs` | Frozen protocol constants and the implemented, independently unregistered 18-family trace corpus |
| Campaign and evidence | `src/PrimeAxiom.Cli/Build005Campaign.cs`, `Build005ExperimentRunner.cs`, `CommandLine.cs` | Five policy classes, sixteen policy/capacity/budget configurations, oracle comparison, modeled cost ledger, break-even rows, receipts, and safe publication |
| Tests | `tests/PrimeAxiom.Tests/Build005ProtocolTests.cs`, `Build005ValuationTests.cs`, `Build005HardwareTests.cs`, `Build005ExperimentTests.cs` | Frozen-hash, exact arithmetic, component-graph, hostile-boundary, campaign, manifest, and deterministic-output tests |
| Verification | `scripts/verify-build005.ps1` | External zero-skip, inherited-evidence, two-generation, byte-replay, manifest, and frozen-count gate |

The five compared policies were direct binary evaluation, a content-plus-divisor
answer cache, a resumable frontier without arithmetic propagation, a frontier
with legal prime propagation, and that same frontier with one- or four-step
speculation. Cache capacities were `K in {0,1,2,4}`. The catalogue also
included size-matched composite-control rows; these were not frozen paired
counterfactuals for every prime trace.

## Implemented experiment result

The current generated set contains:

| Receipt | Result |
|---|---:|
| Workload families | 18 |
| Widths | W8, W16, W32 |
| Policy configurations | 16 |
| Workload rows | 864 |
| Break-even comparisons | 1,134 |
| Declared static component rows | 48 |
| Workload correctness checks | 903,364 |
| Independent correctness checks | 163,360 |
| Total checks | **1,066,724** |
| Failures | **0** |

The independent matrix includes 12,288 exhaustive W8 valuation checks, 131,072
exhaustive W8 arithmetic checks, and 10,000 seeded checks at each of W16 and
W32. All 18 implemented trace families emitted their expected 48 rows and
reported zero semantic failures. This is exact for the implemented domains; it
does not satisfy the larger frozen coverage contract described below.

### Positive signals

1. **The representation is coherent.** Exact frontier receipts resume without
   repeating already-certified divisions, negative exponent-zero answers can be
   cached, and stale generation tags do not become arithmetic hints.
2. **Generic retained work is useful in the model.** At W16/K4, the no-
   propagation frontier finished `STATIC_REUSE` 2,953 modeled cycles and
   3,479,460 modeled service-NAND evaluations below direct recomputation. At
   W32/K4, the corresponding differences were 9,697 cycles and 20,727,180
   evaluations. On `THRESHOLD_STAIRCASE`, the W32/K4 differences were 20,671
   cycles and 44,223,432 evaluations. These are exploratory row-local modeled
   differences, not whole-machine or physical savings.
3. **Prime propagation produced one isolated strict local saving.** On the W8,
   K4 `MULTIPLICATIVE_DAG` row, the propagation policy ended 12 modeled cycles
   and 5,848 modeled service-NAND evaluations below the equal frontier without
   propagation. This is the only row that was both no-worse on those two fields
   and strictly better than the no-propagation control. The final
   `60 * 210 = 12,600` root multiply overflows W8 and is rejected, so the last
   query reuses the intermediate value `60`; this is an overflow-truncated
   intermediate-receipt signal, not completed root-DAG transport.
4. **The machine found a plausible placement for prime structure.** It is not
   the two-state floor and not a replacement for binary magnitude. It is a
   small evidence-carrying service immediately above a radix-aware binary
   datapath, where demanded or producer-known facts can be retained.

### Negative signals

1. **No demand-driven odd-prime family qualified.** The W8 multiplicative row
   did not beat the content cache on both modeled fields, and the same
   prime-specific saving was absent at W16 and W32. The frozen rule required a
   non-producer family to beat direct, content-cache, and no-propagation
   controls at both W16 and W32.
2. **Blind speculation lost.** Across its 162 rows, budget-one scouting recorded
   6,360 speculative DIVMOD steps; 1,464 distinct prefetched keys were later
   requested and 5,680 were never requested. Budget four recorded 25,440
   steps, with 2,541 keys later requested and 23,640 never requested. A later
   request does not prove that the prefetched line remained resident or saved
   work. Neither policy had one strictly winning break-even row against
   demand-only. Per-row modeled cycle penalties reached 8,928 for B1 and 35,980
   for B4; modeled service-NAND penalties reached 20,092,581 and 80,804,976.
3. **Prime propagation was usually indistinguishable from overhead.** Against
   the equal no-propagation frontier, 145 of 162 comparison rows were no worse
   on both recorded modeled fields because most rows were equal. Only the W8/K4
   multiplicative row was both no-worse and strictly better. A second row was
   cycle-better but service-NAND-worse and therefore did not repay.
4. **State cost remains.** The W32 component inventory grows from 6,811 NAND2
   cells and 138 DFF bits at K0 to 9,847 NAND2 cells and 358 DFF bits at K4.
   W16 grows from 3,433/73 to 5,653/225; W8 from 1,847/40 to 3,635/156. These
   are additive inventories of separately built components, not an integrated
   service netlist or a physical implementation.

## Why the earlier generic candidate was quarantined

An earlier internal aggregation could have read the cross-width reuse rows as
`GENERIC_CACHE_ADVANTAGE_ONLY`. The raw pattern is real enough to retain, but
the terminal interpretation was not justified. It treated the available
`MAGNITUDE_FINAL` traces and compositional cost model as if they also satisfied
the frozen output-obligation, phase, transition, policy-matched hardware,
competent-control, attribution, correctness, and replay gates.

The current evidence therefore preserves the observation only as
`EXPLORATORY_GENERIC_REUSE_PATTERN`. It sets `genericCacheAdvantage=false`,
sets all 1,134 break-even rows to `eligible_for_frozen_decision=false`, and
leaves the decision axes unearned. This quarantines the claim without erasing
or rewriting the raw modeled rows. It is especially important because generic
memoization and lazy checkpointing are established techniques; a promising
cache trace is not evidence for prime-native computation.

## Nine unmet frozen gates

Only `IMPLEMENTED_SEMANTIC_TRACE_MATRIX` is satisfied. These nine gates remain
open:

1. `PRE_RESULT_TRACE_DIGEST_REGISTRY` — trace digests come from the executed
   mutable factory; there is no independently frozen pre-result registry.
2. `ALL_OUTPUT_OBLIGATIONS` — every current trace uses `MAGNITUDE_FINAL`;
   predicate-only, exact-exponent, exponent-plus-residual, and every-event
   magnitude obligations are not separately costed.
3. `PHASE_AND_TRANSITION_LEDGER` — raw per-event prefix series are not emitted;
   ingress, search, execute, maintenance, and egress phase rows and settled
   NAND/input/state transition series are absent.
4. `INTEGRATED_PROPAGATION_HARDWARE` — propagation arithmetic is counted
   semantically, but no exponent/residual propagation combiner is integrated
   into a netlist or switching trace.
5. `POLICY_MATCHED_CONTENT_CACHE_HARDWARE` — the declared cache graph is keyed
   by slot and generation; the W-bit content-plus-divisor control has no
   matching structural graph.
6. `COMPETENT_CONVENTIONAL_CONTROLS` — grouped screening, full rational
   reduction, cumulative smooth stripping, producer-known sparse factor form,
   and a strict radix comparator remain incomplete.
7. `CAUSAL_PRIME_ATTRIBUTION` — no pre-frozen paired witness yet binds one
   legal odd-prime terminal transfer to avoided DIVMOD work while demonstrating
   the invalidity of the analogous composite inference.
8. `FULL_INDEPENDENT_CORRECTNESS_MATRIX` — the independent checks do not span
   every capacity, policy, transaction boundary, and K+1 hostile identity.
9. `EXTERNAL_DETERMINISTIC_REPLAY` — a generator invocation cannot establish
   its own replay, zero-skip test status, inherited-evidence preservation, or
   manifest integrity.

These are material absences, not paperwork. The frozen decision rule requires
them, so missing evidence cannot be converted into either a positive candidate
or `SEARCH_DOES_NOT_REPAY`.

## Prior-art boundary

The ingredients are established. Relevant predecessors include GNU MP
`mpz_remove` and small-divisor guidance, SymPy `FactorCache`, Magma/PARI/FLINT
factor-form arithmetic, RISC-V `ctz`, invariant-divisor arithmetic, Southern et
al.'s FPGA small-prime trial-division pipeline, Bernstein's batch smooth-part
method, and exact lazy refinement systems. Euclid's lemma supplies the
prime-specific multiplication law; Build 005 did not discover it.

The bounded integration not located in the Build 005 review is the exact
combination of a tiny versioned frontier cache, negative terminal receipts,
threshold-limited resumption, transactional invalidation, prime-only transport,
and complete repayment accounting. That is an integration question, **not a
novelty claim**. The search was neither systematic nor a patent review.

## What this says about “just looking for primes”

The phrase currently hides three different strategies:

- **Look repeatedly at the same value:** useful here, but primarily generic
  memoization/checkpointing.
- **Look because a consumer asks:** potentially useful; the work can leave an
  exact receipt that later demands or multiplication may reuse.
- **Look speculatively in case a consumer asks later:** harmful on the current
  implemented traces after unused work and cache pollution are charged.

So the optimization candidate is not “search primes everywhere.” It is
“retain exact multiplicative provenance once legitimately obtained, and move
it only across operations that preserve it.” Build 005 locates that candidate
but does not yet prove its end-to-end value.

## Build 006 recommendation: causal provenance transport

Build 006 should be a **newly frozen experiment**, not a repaired label or
retroactive promotion of Build 005. Its single question should be:

> When an exact multiplicative receipt is already earned by a measured
> producer or demanded query, does carrying that receipt through a computation
> causally avoid later work after validation, transport, mutation, and output
> costs are charged?

A bounded design:

1. Freeze canonical trace JSON and SHA-256 digests before implementation.
2. Use paired traces with identical binary magnitudes and arithmetic events.
   One branch carries a validated receipt; the counterfactual branch discards
   it and must rediscover the requested valuation downstream.
3. Include a prime witness such as terminal `p=3` leaf receipts propagated
   through multiplication, and a size-matched composite witness such as base
   six where non-divisibility of the leaves cannot be transported to their
   product.
4. Compare direct evaluation, content memoization, equal-budget checkpointing,
   and receipt transport under the same downstream obligation. Charge producer
   emission, receipt validation, cache/tag state, transfer, invalidation,
   residual/exponent combination, magnitude output, and every failed or unused
   operation.
5. Run producer-generated and cold-demand regimes separately. A producer win
   is a provenance result; it must never be relabeled as search repayment.
6. Add an independent causal receipt stating the exact DIVMOD calls avoided by
   each legal transfer, then replay the same event graph with transfer disabled.
7. Keep the first pass semantic and software-measured. Build an integrated
   hardware block only if the paired causal experiment produces a stable saving
   at W16 and W32 against all generic controls.

This attacks the strongest signal left by Build 005: not that primes should be
searched blindly, but that already-earned multiplicative facts might be useful
computational provenance.

## Reproduction

Generate the committed-format receipts:

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build005 --output results/build005
```

Run the external verification path:

```powershell
& .\scripts\verify-build005.ps1
```

The verifier is the only path allowed to establish byte-identical two-run
replay, zero skipped tests, manifest integrity, and inherited Build 000–004
evidence preservation. Even a successful verifier cannot fill the other eight
unmet gates or retroactively turn this report into a terminal optimization
claim.

## Bottom line

Build 005 found no prime magic and did not earn a more efficient compute
structure. It did find a cleaner fork in the stack: binary carries magnitude;
a small exact side service may carry multiplicative provenance. Generic reuse
already explains most modeled savings, blind scouting is a clear negative
signal, and prime-specific propagation remains one isolated local mechanism.
Build 006 should test that mechanism causally, with provenance present versus
deliberately discarded, before any further hardware speculation.
