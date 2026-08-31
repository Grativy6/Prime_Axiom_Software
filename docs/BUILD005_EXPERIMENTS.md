# Build 005 experiments: demand-driven valuation frontiers

## Result first

The current Build 005 corpus establishes an exact bounded semantic
implementation of a demand-driven valuation service and an exploratory
additive NAND/DFF component inventory. It does **not** establish that looking
for primes is an optimization.

The generated status and candidate terminal label are both:

```text
PARTIAL — FINAL DECISION NOT EARNED
```

All `1,134` exploratory break-even comparisons have
`eligible_for_frozen_decision=false`. One W8 row shows the mechanism that the
build was looking for, but that row is quarantined as an exploratory signal:
it is outside the W16/W32 decision widths, does not beat every required
baseline, and cannot bypass the unmet evidence gates.

## Frozen protocol

The controlling plan is
[`PAH-BUILD005-DEMAND-VALUATION-0001`](../research/build005_experiment_plan.md).
It was frozen before candidate implementation.

| Field | Frozen value |
|---|---|
| Baseline commit | `1fff29e2f1e454921aa51cb4a91bd5b41821ebcc` |
| Freeze commit | `3ffb86b` |
| Plan SHA-256 | `8B76649A4D4E7E60B756BCFB5FDA7954385A10A9E6DDD520C97123B845CE9031` |
| Master seed | `0x5041485742303035` |
| Semantic exhaustive width | W8 |
| Decision widths | W16 and W32 |
| Value slots | 4 |
| Cache capacities | K=0, 1, 2, 4 |
| Speculation budgets | B=1 and B=4 odd DIVMOD steps |
| Prime catalogue | `2,3,5,7,11,13,17,19,23,29,31` |
| Composite controls | `4,6,9,10,15,21,25,27,33,35` |

Binary magnitude remains authoritative. The experiment asks only whether an
exact resumable frontier

```text
n = p^L * R
```

can retain enough earned work to repay lookup, search, storage, invalidation,
transfer, propagation, and output costs. Prime identity is supplied by the
validated frozen catalogue; it is not discovered by NAND gates.

## What was implemented

### Semantic service

[`DemandValuationService.cs`](../src/PrimeAxiom.Core/Build005/Valuation/DemandValuationService.cs)
implements four versioned value slots and exact `LOAD`, `TEST_POWER`,
`VALUATION`, `STRIP_ALL`, `ADD`, `MUL`, and `MUL_BY_PRIME` transactions. A
cache line carries slot, generation, catalogue index, lower bound, residual,
terminal status, and the zero/infinite state. The implementation provides:

- exact frontier replay against authoritative magnitude;
- terminal exponent-zero receipts after a failed divisibility probe;
- partial-frontier resume without repeating already completed divisions;
- LRU replacement for K=1, 2, or 4;
- destination invalidation and atomic rejection on failed arithmetic;
- full cache flush before an eight-bit generation tag can wrap into stale
  identity;
- legal terminal-receipt propagation across multiplication only for the
  validated prime catalogue; and
- a separate composite-control constructor that uses the same valuation and
  cache machinery while disabling prime-only propagation.

The public contracts and exact counter ledger are in
[`ValuationContracts.cs`](../src/PrimeAxiom.Core/Build005/Valuation/ValuationContracts.cs).

### Declared logical components

The Build 005 hardware directory constructs separate NAND/DFF graphs for:

- frozen catalogue selection;
- W8/W16/W32 count-trailing-zero paths;
- one sequential odd-divisor DIVMOD path; and
- K=0/1/2/4 slot-and-generation frontier-cache front ends.

The `48` static rows are four components times four cache capacities times
three widths. The following are additive component sums, not integrated
netlists:

| Width | K | NAND2 | DFF/state bits | Port bits | Wire bits |
|---:|---:|---:|---:|---:|---:|
| 8 | 0 | 1,847 | 40 | 134 | 2,124 |
| 8 | 4 | 3,635 | 156 | 134 | 4,064 |
| 16 | 0 | 3,433 | 73 | 201 | 3,866 |
| 16 | 4 | 5,653 | 225 | 201 | 6,274 |
| 32 | 0 | 6,811 | 138 | 332 | 7,552 |
| 32 | 4 | 9,847 | 358 | 332 | 10,844 |

These exact graph inventories do not include the policy-matched W-bit content
cache, an integrated exponent/residual propagation combiner, the shared W-bit
load/add/multiply/output datapath at W16/W32, routing, synthesis, placement,
clocking, or physical behavior. Dynamic service NAND evaluations therefore
remain an exploratory compositional proxy and cannot decide a hardware result.

## Policy matrix

Every width/family pair has the same `16` policy rows:

| Rows | Policy | K | B | Question isolated |
|---:|---|---|---:|---|
| 1 | `BIN_DIRECT_BEST` | 0 | 0 | Recompute and discard |
| 3 | `BIN_CONTENT_ANSWER_LRU_K` | 1,2,4 | 0 | Generic immutable-answer memoization |
| 3 | `BIN_FRONTIER_NOPROP_K` | 1,2,4 | 0 | Generic resumable checkpointing |
| 3 | `BIN_PRIME_FRONTIER_PROP_K` | 1,2,4 | 0 | Demand-only legal prime-certificate transfer |
| 3 | `BIN_PRIME_FRONTIER_SPEC_B1_K` | 1,2,4 | 1 | One-step ordered blind scouting |
| 3 | `BIN_PRIME_FRONTIER_SPEC_B4_K` | 1,2,4 | 4 | Four-step ordered blind scouting |

The matrix covers `18` required families at W8, W16, and W32: `54` concrete
trace definitions and `864` workload rows. Each family contributes `48/48`
rows and currently reports `IMPLEMENTED_TRACE_PASS`.

## Exact current receipt inventory

The committed/generated corpus under [`results/build005/`](../results/build005/)
contains eight manifest-addressed payloads plus the self-excluding manifest.

| Receipt | Exact current content |
|---|---|
| `correctness.json` | 1,066,724 checks; 0 failures |
| `trace_inventory.json` | 54 concrete traces; 18 families at 3 widths |
| `workload_matrix.csv` | 864 rows; 903,364 workload checks; 0 failures |
| `break_even.csv` | 1,134 exploratory candidate/baseline comparisons |
| `static_costs.csv` | 48 declared component rows |
| `protocol_coverage.json` | 10 explicit evidence gates: 1 satisfied, 9 unmet |
| `attribution.json` | nondecision axes, exploratory pattern, and claim ceiling |
| `README.md` | generated human-readable summary |
| `manifest.json` | v2 self-excluding SHA-256 inventory of the eight payloads, with stable runtime/platform contracts and external-only environment provenance |

The `163,360` independent correctness checks inside the total comprise:

- `12,288` W8 valuation checks;
- `131,072` exhaustive ordered W8 arithmetic checks;
- `10,000` seeded W16 checks; and
- `10,000` seeded W32 checks.

The workload ledger additionally records `193` terminal and `160` lower-bound
certificates propagated, `53` propagation exponent additions, and `227`
propagation residual multiplications. Those counters show that the mechanism
executed; they do not establish net repayment or prime-specific causation.
The grouped-screen remainder field is only a declared proxy, not an executed
control, and GCD and final-magnitude-output counters are excluded from the
modeled cycle total. Those omissions are part of the unmet competent-control
and complete-cost boundary.

## Quantitative exploratory comparisons

No comparison below is decision-eligible.

### The quarantined prime-propagation signal

On W8 `MULTIPLICATIVE_DAG`, K=4 demand propagation produced one terminal
certificate and reduced odd DIVMOD calls from `8` to `6` relative to the
no-propagation frontier. Its modeled totals were:

| W8 policy | DIVMOD calls | Modeled cycles | Modeled service NAND evaluations |
|---|---:|---:|---:|
| `BIN_DIRECT_BEST` | 8 | 84 | 55,465 |
| `BIN_CONTENT_ANSWER_LRU_K`, K=4 | 8 | 89 | 73,345 |
| `BIN_FRONTIER_NOPROP_K`, K=4 | 8 | 95 | 84,073 |
| `BIN_PRIME_FRONTIER_PROP_K`, K=4 | 6 | 83 | 78,225 |

Thus propagation was `12` modeled cycles and `5,848` modeled service NAND
evaluations below the matched no-propagation frontier, with a stable prefix at
event 11. It remained `22,760` NAND evaluations above direct binary despite
being one modeled cycle lower. This is the only one of `45`
`PRIME_PROPAGATION_ELIGIBLE` comparisons that was no worse on both modeled
dynamic fields and strictly better on at least one.

The W8 trace does not complete the named balanced product graph. Its final
`60 * 210 = 12,600` multiply exceeds W8, is rejected atomically, and leaves the
final query aimed at the earlier intermediate value `60`. W16 and W32 execute
that root multiply. The isolated W8 saving is therefore an intermediate-receipt
reuse signal on an overflow-truncated trace, not successful end-to-end root-DAG
transport.

It does not reproduce as a repayment at the decision widths. On the same K=4
trace:

| Width | Policy | DIVMOD calls | Terminal propagated | Modeled cycles | Modeled NAND evaluations |
|---:|---|---:|---:|---:|---:|
| 16 | direct | 9 | 0 | 165 | 189,905 |
| 16 | no propagation | 9 | 0 | 177 | 227,645 |
| 16 | demand propagation | 9 | 1 | 186 | 243,185 |
| 32 | direct | 9 | 0 | 309 | 647,061 |
| 32 | no propagation | 9 | 0 | 321 | 698,673 |
| 32 | demand propagation | 9 | 1 | 330 | 719,925 |

The current trace therefore demonstrates semantic transfer at W16/W32 but not
saved division or modeled repayment there.

### Generic reuse is the visible pattern

Of the `1,134` exploratory comparisons, `298` ended no worse on both modeled
fields and `136` were also strictly better on at least one. Those `136` rows
break down as:

- `45` generic memoization comparisons;
- `36` generic checkpoint comparisons;
- `54` generic or otherwise unattributed comparisons; and
- `1` quarantined prime-propagation-eligible W8 comparison.

The largest modeled cycle reduction was generic checkpointing on the W32
`THRESHOLD_STAIRCASE`: K=2 `BIN_FRONTIER_NOPROP_K` versus direct ended `20,671`
cycles and `44,347,443` compositional NAND evaluations lower, with stable
prefix 2. Because the content-cache structural graph and integrated service
are absent, this is an exploratory software/model result, not a hardware
finding.

### Blind scouting spent work that demand did not need

Across all widths, traces, and cache capacities, the speculative ledgers
record:

| Policy | Speculation queries | Odd DIVMOD steps | Distinct prefetched keys later requested | Distinct prefetched keys never requested |
|---|---:|---:|---:|---:|
| B=1 | 7,153 | 6,360 | 1,464 | 5,680 |
| B=4 | 26,217 | 25,440 | 2,541 | 23,640 |

“Later requested” does not establish that the prefetched line was still
resident or that it avoided work. These two key-fate columns are intentionally
not called useful/wasted. The exact negative evidence is the charged additional
DIVMOD work and the absence of a strictly winning speculation row against
demand-only.

The largest modeled cycle penalty was W32
`HOSTILE_GENERATION_WRAP`, B=4, K=2 versus demand-only: `35,980` additional
cycles and `77,802,124` additional compositional NAND evaluations. The
generated exploratory observation is therefore
`BLIND_SPECULATION_INCURRED_WASTED_WORK`, not a negative universal theorem
about prefetching.

## Composite-control correction

An implementation audit caught an important control-boundary problem before
the current receipt set was accepted: composite-divisor events cannot be sent
through a service whose only accepted catalogue is prime-valued, and the
prime terminal-product inference must never be credited to a composite base.

The corrected implementation constructs an explicit service over the frozen
composite catalogue. It preserves the same query, exact frontier, LRU,
invalidation, and generation mechanisms, while forcing multiplicative
propagation off. The `2 * 3` counterexample for divisor `6` is tested: each
input has exponent zero for base 6, while the product has exponent one and
must be discovered by fresh division rather than inferred.

That paired multiplication is a unit-level semantic counterexample, not a
campaign trace. The generated `COMPOSITE_CONTROL` workload directly loads its
test magnitudes; it does not provide the frozen causal prime-versus-composite
pair required for attribution.

The regenerated current `COMPOSITE_CONTROL` receipts contain `48` rows and
`6,000` checks with zero failures. Every policy/capacity row records zero
terminal certificates propagated. Each W8 row performs `13` composite DIVMOD
calls; each W16 and W32 row performs `18`. This correction makes the semantic
control real, but it does not satisfy the frozen causal-attribution gate: the
plan still requires an independently frozen paired witness that links an
odd-prime terminal transfer to a saved DIVMOD while rejecting the composite
inference under a decision-eligible cost ledger.

## Evidence audit and nondecision boundary

The implemented trace matrix is the only satisfied frozen evidence gate.
`implementedTraceCoverageComplete=true` means the current 18-family semantic
matrix ran; it does not mean `completeFrozenCoverage=true`.

The generator deliberately reports:

```text
search_policy: NOT_EARNED
attribution: NOT_ESTABLISHED
evidence_boundary: SEMANTIC
decision_axes_earned: false
qualifying_demand_families: NONE
qualifying_speculative_families: NONE
```

Every break-even row is nondecision evidence. In particular, the W8
multiplicative signal cannot be promoted by selecting it after inspection,
and the generic wins cannot be relabeled as prime wins. An external replay can
validate deterministic bytes and repository protection, but it cannot fill
the other eight missing scientific/engineering obligations or override the
frozen rule.

### Exact nine unmet gates

1. `PRE_RESULT_TRACE_DIGEST_REGISTRY` — trace digests are emitted from the
   executed mutable factory; no independent pre-result digest registry exists.
2. `ALL_OUTPUT_OBLIGATIONS` — the current corpus uses `MAGNITUDE_FINAL` only;
   predicate, exact-exponent, residual, and every-event obligations are not
   separately costed.
3. `PHASE_AND_TRANSITION_LEDGER` — raw per-event prefix series are not emitted;
   neither are `INGRESS`, `SEARCH`, `EXECUTE`, `MAINTENANCE`, and `EGRESS`
   rows or settled NAND/input/state transition series.
4. `INTEGRATED_PROPAGATION_HARDWARE` — semantic propagation arithmetic is
   counted, but no integrated exponent/residual combiner netlist or switching
   trace is present.
5. `POLICY_MATCHED_CONTENT_CACHE_HARDWARE` — the declared cache graph is
   slot/generation keyed; the W-bit content-plus-divisor cache has no matching
   structural graph.
6. `COMPETENT_CONVENTIONAL_CONTROLS` — grouped screening, full rational
   reduction, cumulative smooth stripping, producer-known sparse factor form,
   and a strict radix comparator are incomplete.
7. `CAUSAL_PRIME_ATTRIBUTION` — no frozen paired witness binds an odd-prime
   terminal transfer to saved DIVMOD while rejecting the composite-product
   inference.
8. `FULL_INDEPENDENT_CORRECTNESS_MATRIX` — the independent W8/W16/W32 checks
   do not cover every cache capacity, arithmetic policy, transaction boundary,
   and K+1 hostile identity.
9. `EXTERNAL_DETERMINISTIC_REPLAY` — only the external verifier may establish
   two-run replay, zero skipped tests, inherited-evidence protection, and
   manifest integrity.

Until those obligations are satisfied under the frozen protocol, neither a
positive label nor `SEARCH_DOES_NOT_REPAY` is earned. Missing evidence remains
`PARTIAL`; it is not converted into a favorable or unfavorable result.

## Reproduction commands

Build and run the focused Build 005 test surface:

```powershell
dotnet build PrimeAxiom.sln --configuration Release
dotnet test tests/PrimeAxiom.Tests/PrimeAxiom.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Build005"
```

Regenerate the deterministic semantic receipts:

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build005 --output results/build005
```

Run the external verifier, which protects inherited Build 000–004 evidence,
validates the pinned SDK policy, requires a zero-skip full test pass, performs
two isolated generations, compares all nine inventories and bytes, and
validates the manifest. Installed runtime, OS, and architecture provenance is
recorded in the verifier receipt rather than the deterministic corpus. Each
generator invocation emits a verifier-owned environment sidecar; the verifier
requires exact A/B agreement and confirms the selected runtime appears exactly
once in the installed runtime inventory:

```powershell
& .\scripts\verify-build005.ps1
```

The generated corpus always retains the partial label. The verifier may
establish its own replay and protection receipts; it cannot make an
incomplete decision matrix complete.

## File map

- Frozen question and decision rule:
  [`research/build005_experiment_plan.md`](../research/build005_experiment_plan.md)
- Architecture and claim boundary:
  [`docs/BUILD005_ARCHITECTURE.md`](BUILD005_ARCHITECTURE.md)
- Prior-art boundary:
  [`docs/PRIOR_ART_BUILD005.md`](PRIOR_ART_BUILD005.md)
- Deterministic workload definitions:
  [`src/PrimeAxiom.Cli/Build005Workloads.cs`](../src/PrimeAxiom.Cli/Build005Workloads.cs)
- Campaign, cost ledger, audit gates, and classification:
  [`src/PrimeAxiom.Cli/Build005Campaign.cs`](../src/PrimeAxiom.Cli/Build005Campaign.cs)
- Receipt writer and manifest:
  [`src/PrimeAxiom.Cli/Build005ExperimentRunner.cs`](../src/PrimeAxiom.Cli/Build005ExperimentRunner.cs)
- Semantic service:
  [`src/PrimeAxiom.Core/Build005/Valuation/`](../src/PrimeAxiom.Core/Build005/Valuation/)
- Declared logical components:
  [`src/PrimeAxiom.Core/Build005/Hardware/`](../src/PrimeAxiom.Core/Build005/Hardware/)
- Unit, exhaustive, and generation tests:
  [`tests/PrimeAxiom.Tests/`](../tests/PrimeAxiom.Tests/)
- External verification:
  [`scripts/verify-build005.ps1`](../scripts/verify-build005.ps1)
- Generated evidence:
  [`results/build005/`](../results/build005/)

## Claim ceiling

Bounded exact semantic, host-software, and declared NAND/DFF evidence under
`PAH-BUILD005-DEMAND-VALUATION-0001` only; no universal arithmetic, novelty,
FPGA/ASIC PPA, physical-energy, fabricated-hardware, or PAL-conformance claim.
