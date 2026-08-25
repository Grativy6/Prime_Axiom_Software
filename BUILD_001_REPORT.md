# Prime Axiom Software — Build 001 report

## Result

Build 001 implemented an exact signed-integer representation with a finite valuation bank and an exact ordinary-binary cofactor. It also implemented deferred per-lane valuation knowledge, explicit refresh and bank migration, a traceable VM, phase-separated cost receipts, adversarial workloads, deterministic functional evidence generation, and explicitly variable timing receipts.

The representation is coherent and survived the executed correctness campaign. No architectural advantage was earned in the pilot. The favorable factor-resident proxy reduced modeled local multiplication work, but every tested bank used much more logical payload than the binary control, the timed hybrid multiplication probes were much slower than the host `BigInteger` multiplication control, addition rapidly accumulated deferred valuation knowledge, and adaptive migration thrashed under a `K+1` reference cycle.

**Status: `PARTIAL — PILOT_NEGATIVE; FINAL DECISION NOT EARNED`.**

The checked evidence is a completed exploratory pilot subset: `PILOT_SUBSET_COMPLETE_FULL_CONFIRMATION_NOT_RUN`. The frozen 1,800-trace confirmation matrix, registered exhaustive/random correctness scale, SplitMix64/FNV trace namespaces and raw schemas, all source modes and widths, independently executable full-factor/Build 000 sparse comparators, complete managed/structural Pareto profiles, third-party native baselines, and hardware synthesis were not executed. The pilot instead uses seeded .NET `Random` under the recorded runtime. Those cells remain `NOT_MEASURED`, so the frozen full-matrix stop condition is unmet. Missing positive evidence prevents a positive label, but does not itself earn the terminal negative label `NO_USEFUL_ADVANTAGE`. This report therefore answers the requested questions at pilot scope and leaves the Build 001 decision gate open.

## Evidence and reproduction

The Build 001 experiment is pinned to Build 000 commit `7792b8b2a83c95693a6db48a0ed4b153bb0808f4`. Build 000's report and `results/build000/` evidence remain byte-identical to that baseline.

```powershell
& .\scripts\verify-build001.ps1
```

The verification script restores locked dependencies, verifies formatting, builds Release, runs the complete test assembly, regenerates Build 001 evidence, verifies every manifest hash, and checks the Build 000 preservation receipts. The recorded run produced:

- 89/89 xUnit tests passed, with zero skipped;
- 39,621 bounded exhaustive, randomized, malformed-state, and differential assertions, with zero failures;
- 194 pilot workload/configuration rows across multiplicative, divisibility, addition, mixed, and adversarial families;
- deterministic seed `99536897`;
- a checked manifest and per-file SHA-256 hashes under [`results/build001/`](results/build001/manifest.json).

The frozen protocol is [`research/build001_experiment_plan.md`](research/build001_experiment_plan.md), SHA-256 `4A79873ADCBE477944FFBE1D90AD0969AD99560AA0C014F3CB1DD8639FF9DDEF`. High-level pilot coverage and omission categories are recorded in [`protocol_coverage.json`](results/build001/protocol_coverage.json); it is not the registered cell-by-cell failure matrix. Wall-clock results describe one .NET implementation on the manifest host; they are not hardware measurements or population estimates.

## 1. What is the final representation contract?

For an immutable ordered bank `S = (p_0, ..., p_(K-1))`, a nonzero value denotes exactly

```text
sign * cofactor * product(p_i ^ e_i)
```

where `sign` is `-1` or `+1`, `cofactor` is a positive arbitrary-precision binary integer, and every `e_i` is a nonnegative fixed-width binary exponent. Each lane is either `KnownExact` or `CertifiedLowerBound`.

- `KnownExact` means `e_i = v_(p_i)(value)` and the cofactor is not divisible by `p_i`.
- `CertifiedLowerBound` means the stored `e_i` is proved, but additional copies of `p_i` may remain in the exact cofactor.
- `Canonical` means every lane is exact. `Partial` means at least one lane is a certified lower bound.
- Partial valuation knowledge never makes the integer approximate: the sign, exponents, and cofactor still reconstruct the exact integer.
- Zero has one explicit canonical form: zero sign, zero cofactor, zero exponents, exact lane knowledge. One has positive sign, cofactor one, and zero exponents.
- Bank mismatch, width mismatch, overflow, malformed state, unsupported exact query, and failed claimed-magnitude verification return typed failures. They do not create executable sentinel values.

The strict versioned serializer records numeric state and bank identity, rejects duplicate or extra fields and malformed enum/state combinations, and omits provenance from numeric equality. Structured ingress verifies its declared components; claimed-magnitude ingress additionally reconstructs and compares them to the external exact magnitude.

## 2. What exactly is the valuation bank?

The bank is immutable configuration: an ordered, strictly increasing set of distinct ordinary primes. It is not a supply of prime atoms and not part of each value's ordinary payload. The implementation permits `K = 0` as the binary-cofactor control and caps banks at 4,096 lanes. Exponent width is separately declared and bounded.

Build 001 supports no bank, first-`K`, workload-selected, configured, and experimentally adaptive policy identities. Bank construction validates every label and reports that setup work separately. A bank determines lane addresses and therefore belongs to the representation contract; equal exponent arrays under different banks do not denote the same structure.

## 3. What exactly is the cofactor?

The cofactor is the exact positive ordinary-binary residual after the represented bank powers are removed. It need not be prime, fully factored, small, or free of primes outside the bank. In canonical state it is coprime to every bank prime. In partial state it can contain more copies of lanes whose stored exponents are only lower bounds.

The cofactor is what makes the representation total over arbitrary signed integers and prevents finite-basis escape. It is also where much of the apparently removed complexity returns: multiplication, addition, division, remainder, GCD, comparison, storage, and reconstruction still require ordinary magnitude work on it.

## 4. Which invariants are mandatory?

Mandatory invariants are:

1. bank labels are bounded, prime, unique, and strictly increasing;
2. the exponent vector, knowledge vector, provenance vector, and bank have identical lane counts;
3. every exponent fits the declared width and every knowledge value is a defined executable state;
4. nonzero sign is exactly `-1` or `+1`, with a strictly positive cofactor;
5. zero and one use their unique forms;
6. an exact lane's prime does not divide the cofactor;
7. a lower-bound lane never claims more factors than the represented exact value contains;
8. operations requiring exact valuations either receive canonical inputs, refresh them, or fail explicitly;
9. bank migration folds evicted powers into the cofactor and extracts admitted primes exactly;
10. overflow and VM failure are transactional: no truncated result or stale destination survives.

The implementation enforces these at public construction, decoding, migration, and instruction boundaries. It is not resource-hardened for adversarial but contract-valid values: reconstruction, residual materialization during addition or bank eviction, cofactor powers, refresh loops, and serialization can allocate or run beyond practical limits before a typed receipt is returned. Callers must impose external magnitude/work limits.

## 5. Which operations remain native?

The genuinely bank-local pieces are bounded exponent addition/subtraction/comparison, lane valuation queries, lane-wise minimum/maximum, and knowledge-state transitions. On canonical compatible values, multiplication adds exponents, powers scale them, and the bank portions of exact division, divisibility, GCD, LCM, and rational cancellation use coordinate operations.

No nontrivial whole-integer operation is purely bank-local in the general representation: multiplication still multiplies cofactors; powers still exponentiate the cofactor; GCD/LCM and cancellation still inspect cofactors; and signs, zero, overflow, and status reductions remain explicit. “Native” therefore means a local structural component, not a free complete instruction.

## 6. Which operations cross back into ordinary magnitude arithmetic?

Binary ingress, claimed-magnitude validation, cofactor arithmetic, addition/subtraction, outside-bank divisibility, general exact division, cofactor GCD/LCM, numeric equality/order, formatting, and reconstruction cross into ordinary magnitude work. Addition uses an exact residual/cofactor sum even when common bank valuations are preserved. Egress reconstructs prime powers and combines them with the cofactor.

The VM makes these crossings visible through `Boundary`, `CofactorArithmetic`, `Mixed`, and `Maintenance` domains. It deliberately has no generic `DECOMPOSE` instruction that would disguise factorization as primitive.

## 7. How expensive is ingress?

Magnitude ingress performs one remainder probe per configured prime plus one exact division for every extracted occurrence, then allocates and initializes every lane and its metadata. This is bounded selected-prime stripping, not complete factorization, but it grows with bank size and multiplicity. Bank-primality validation and catalog bits are separately reported setup costs.

Bank-8 binary ingress was a microsecond-scale managed operation on the recorded host; exact median/spread values remain in [`microbenchmarks.csv`](results/build001/microbenchmarks.csv) because canonical reruns replace timing receipts. This is an implementation diagnostic only. `FACT` and `BANK_CERT` source replay were not run, so the experiment does not claim a universal ingress crossover.

## 8. How expensive is maintenance?

Maintenance is potentially global. Refreshing one uncertain lane probes and divides the cofactor. Full normalization repeats this for every uncertain lane. A bank change must migrate every live value: evicted powers are multiplied into each cofactor, admitted primes are stripped, lane arrays and knowledge metadata are rewritten, and overflow remains possible.

The deterministic `K+1` LRU attack used 16 live values and four cycles. It caused 320, 576, 1,088, and 2,112 per-value migrations at `K = 4, 8, 16, 32`, respectively. Lane reads and writes grew from 1,280 each at `K=4` to 67,584 each at `K=32`. All values remained exact, but the policy supplied no free cache behavior.

## 9. How expensive is egress?

Exact egress computes the represented prime powers, multiplies them together and by the cofactor, applies sign, and allocates an ordinary magnitude. Its cost depends on output size, exponent pattern, and multiplication algorithm; it is not captured by counting only bank lanes. Exact magnitude comparison and claimed-magnitude verification also cross this boundary.

The pilot forced magnitude or predicate outputs, so favorable resident work was not credited without an observable result. It did not execute the full forcing-frequency sweep, so no general egress-amortization crossover is claimed.

## 10. What happens under addition?

For each bank prime, addition preserves the minimum input exponent. If both input valuations are exact and unequal, the minimum is proved to be the exact output valuation. If they are equal, or either input is already partial, the minimum is only a certified lower bound because cancellation can introduce more copies of that prime. The exact residuals are then added in ordinary magnitude arithmetic.

In 1,000 seeded additions:

| Bank size | Exact lanes after add | Lower-bound lanes | Partial results | Eager refresh remainder probes |
|---:|---:|---:|---:|---:|
| 4 | 1,814 / 4,000 | 2,186 | 975 / 1,000 | 3,513 |
| 8 | 2,325 / 8,000 | 5,675 | 1,000 / 1,000 | 7,251 |
| 16 | 2,747 / 16,000 | 13,253 | 1,000 / 1,000 | 15,035 |
| 32 | 3,100 / 32,000 | 28,900 | 1,000 / 1,000 | 30,868 |

Wider banks mostly added exact-zero/equal-valuation lanes that became uncertain. In the checked timing receipt, lazy bank-8 addition was faster than eager normalization, but it left explicit debt for later exact consumers; exact timing values remain in [`microbenchmarks.csv`](results/build001/microbenchmarks.csv).

## 11. Can stale or unknown valuation state be useful?

Yes, in a narrow sense. Build 001 does not execute a vague `STALE` state. It uses a proved lower bound tied to an immutable exact value. That lower bound preserves known common divisibility, supports exact reconstruction, and can pass through multiplication without pretending to be an exact valuation. A consumer needing only the certified minimum can avoid refresh.

It is not useful as a substitute for exact knowledge. Exact division, general divisibility, GCD, LCM, and exact valuation queries must refresh or reject partial inputs. The addition results show that deferred knowledge becomes nearly universal and increasingly metadata-heavy as `K` grows.

## 12. Which bank strategies work best?

No strategy won generally.

- No bank is the honest control and avoids lane/maintenance costs on rough, addition-heavy, or one-shot magnitude-native work.
- A small fixed prefix is predictable and captures first-prime-smooth workloads without policy migration.
- Workload selection can reduce a cofactor only when a disjoint training source actually exposes stable prime frequency; it is not free discovery.
- Under `MAG` ingress, the pilot `ADAPTIVE_LRU_K_NO_ELIGIBLE_MAG_REFERENCE` rows intentionally reused the fixed first-`K` bank; no adaptive selection was executed because hidden unbanked factors were not observable without a charged reference mechanism.
- True adaptive replacement was exact but failed the `K+1` thrashing attack through global migration traffic.

First-`K` and selected-`K` converged on the same banks for the non-adversarial pilot families. The adversarial `T` family selected different higher primes, but both policies had zero average occupied lanes and identical per-value cofactor/exponent/metadata outcomes there; the selected bank also paid larger unamortized catalog and validation setup. The pilot therefore demonstrated no selector advantage and cannot rank sophisticated selectors. The practical result is to prefer the smallest stable bank justified by an external workload contract, not autonomous global bank churn.

## 13. Where are the crossover points?

There is no measured crossover eligible for the frozen decision gate because no complete managed or structural profile and confirmation set was executed.

In a favorable resident-product diagnostic, hybrid local work was lower already at `Q=1` and remained lower through `Q=4,096`: at bank 4 it used 1,001 versus 10,368 modeled NAND evaluations at `Q=1`. The two-operand logical payload was 174 bits versus 36 binary bits, already 4.83x; bank 8 used 342 bits, and larger banks were worse. The work proxy and logical input payload belong to different incomplete profiles, so this ratio is informative but cannot be substituted for the frozen complete-profile 2x rule.

The host timing receipt also found both bank-8 hybrid multiplication probes substantially slower than the rough `BigInteger` multiplication control. Exact medians and spread are in [`microbenchmarks.csv`](results/build001/microbenchmarks.csv). These are unmatched implementation timings, not a hardware comparison, but they establish no managed crossover here.

## 14. What workloads benefit?

Mechanism-level benefits appear when values are born with verified factor structure, stay under one small bank, undergo many multiplication/power/selected-divisibility/cancellation operations, and delay or avoid magnitude outputs. Smooth factor-resident products are the clearest example. Direct valuation queries and cancellation of bank-known factors can also reuse evidence instead of rediscovering it.

No workload family earned `SPECIALIZED_ADVANTAGE`: the complete Pareto, width, and replicate requirements were not executed. “Benefits” here means an isolated structural simplification, not an end-to-end win.

## 15. What workloads lose badly?

One-shot magnitude ingress, rough or outside-bank values, addition-heavy accumulation, frequent exact magnitude comparison/output, large cofactors, and shifting prime locality lose. Large banks pay dense zero-lane and metadata costs. Adaptive banks lose when changing membership forces migration of a live working set. General arithmetic also keeps the cofactor operations while adding bank work, so it can pay both lineages' costs.

## 16. Does the architecture survive adversarial testing?

It survived the executed bounded campaign. Tests cover exhaustive signed ingress, every ordered pair in a small signed arithmetic domain, 5,000 seeded randomized trials, zero/sign/identity, malformed structured and JSON states, undefined enums, exponent overflow, cofactor invariants, partial knowledge, migration, claimed magnitudes, deterministic serialization, VM aliasing, stale-output prevention, destination invalidation, and source preservation. Independent code review found defects in enum validation, zero valuation, receipt status, allocation bounds, addition accounting, power overflow/accounting, equality accounting, lexical negative zero, enumerable bounds, and malformed VM programs; each material defect was fixed and regression-tested.

This is not an unbounded proof or a safe untrusted-input runtime. The magnitude-materialization and work-budget limitations noted above remain open. Cross-platform CI is configured, but this report claims only the local checked run until remote CI supplies its own receipt.

## 17. What prior art is closest?

The closest systems are:

1. PARI/GP `Z_smoothen` plus `famat`: selected-prime stripping, exponent records, exact cofactor, persistent factored products, and explicit expansion;
2. FLINT `fmpz_factor_t` and bounded/smooth factor routines: sign, factor/exponent vectors, exact final cofactor, completeness status, refinement, and reconstruction;
3. FriCAS `Factored(R)`: exact incompletely factored values with graded knowledge and explicit expansion;
4. the mathematical finite `S`-part / `S`-free decomposition;
5. OpenJDK `BigInteger`'s lazy `v_2` cache, GMP `mpz_remove` and rational cross-GCD, factor bases, SymPy's bounded factor cache, and invariant-divisor algorithms.

The sourced reconciliation and ranked register are in [`docs/PRIOR_ART_BUILD001.md`](docs/PRIOR_ART_BUILD001.md).

## 18. Is this merely a known optimization rediscovered independently?

Mostly yes. The mathematical decomposition, selected-prime extraction, exact residual, factored multiplication, partial factor knowledge, explicit reconstruction, bank/factor-base selection, valuation-aware cancellation, and lazy valuation caches all have close prior art. Build 001 must not present any of them as a new arithmetic foundation.

The implementation still had research value: it forced those mechanisms into one exact contract, charged displaced work, attacked addition and migration, and made failure/evidence semantics executable. That integration tested the motivating intuition more honestly than a notation-level comparison.

## 19. Is any genuinely novel engineering structure left?

The search did not locate one existing system combining fixed multi-prime lane addresses, an exact cofactor, exact/deferred per-lane evidence, explicit bank migration, transactionally invalidating VM semantics, phase-separated heterogeneous receipts, and adversarial bank-policy evaluation on a shared binary floor. That is a narrow integration and evaluation gap, not demonstrated novelty and not evidence of a new computational foundation.

The more useful architectural observation is that valuation information behaves like evidence-carrying cached metadata, not like a replacement number system. Exact magnitude/cofactor authority remains necessary; the bank is useful only while its certificates match a consumer's narrow questions.

## 20. What should Build 002 investigate?

If the project steward accepts closing the unexecuted Build 001 confirmation gate as `PARTIAL`, Build 002 should stop treating a dense fixed bank as the authoritative general representation. It should test a **sparse, demand-driven certified valuation cache beside an authoritative exact magnitude**:

1. cache only primes actually queried or supplied by a structured producer;
2. retain stable per-value certificates rather than migrating every live value when a global bank changes;
3. compare no-cache, lazy one-prime/multi-prime cache, PARI/FLINT/FriCAS-style persistent factored values, and optimized GMP/host magnitude arithmetic;
4. use real exact-rational cancellation, divisibility filtering, and factor-structured symbolic workloads, plus addition-heavy and phase-shifting attacks;
5. make all cache construction, validation, eviction, cofactor/GCD work, and demanded outputs visible;
6. require an optimized native baseline and a small preflight relevance gate before committing to a large confirmation matrix;
7. separately synthesize a tiny fixed-prime valuation/constant-divider accelerator only if software traces show sufficient query reuse.

The falsifiable question becomes: *can sparse certified valuation evidence avoid repeated work without imposing dense payload or global migration debt?* If that preflight also fails, the general Prime Axiom numeric-architecture line should close with the negative receipts intact.

## Final decision gate

```text
PARTIAL — PILOT_NEGATIVE
FINAL DECISION NOT EARNED
```

The bounded valuation bank is exact and locally intelligible, and the pilot found no reason to justify its complexity as a numeric representation. The registered confirmation/stop rule was not completed, so neither `NO_USEFUL_ADVANTAGE` nor a positive terminal label is earned. The honest next choice is either to execute the frozen confirmation or explicitly accept the partial closure and pursue the narrower cache/coprocessor experiment.
