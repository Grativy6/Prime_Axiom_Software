# Prime Axiom Hardware — Build 005 frozen experiment plan

Protocol ID: `PAH-BUILD005-DEMAND-VALUATION-0001`

Status: **FROZEN BEFORE CANDIDATE IMPLEMENTATION**

Baseline commit: `1fff29e2f1e454921aa51cb4a91bd5b41821ebcc`

## 1. Why this build exists

Build 002 earned `NO_HARDWARE_ADVANTAGE` for its fixed `S4`, W4/W6/W8,
strict-Pareto matrix. It also retained a real local result: already-resident
valuation structure made bounded scale/cancel work smaller and shallower while
using more state and ports. Build 003 exposed exact prime receipts but found
that cold factor discovery dominates one-shot arithmetic. Build 004 moved to
provenance-bearing workloads and recommended lineage-under-loss as its default
next step.

This build is a later steward-directed reopening of a different deferred
question from Build 002. It does not revise those results:

> Can bounded small-prime search leave an exact, resumable certificate that
> avoids more later work than lookup, search, storage, invalidation, transfer,
> and output obligations cost?

The hypothesis is not that prime identity is primitive at the NAND floor.
Binary magnitude remains authoritative. Prime identity is supplied by one
frozen, validated catalogue and paid for as configuration, addressing, and
state.

## 2. Claim boundary

Build 005 may establish bounded software semantics, deterministic workload
results, explicit NAND/DFF graph costs, and declared switching traces. It may
not claim fabricated hardware, FPGA/ASIC place-and-route, physical area,
frequency, energy, thermal behavior, or universal arithmetic advantage.

The search is not novel in itself. Factor-form arithmetic, selected-prime
stripping, factor caches, `ctz`, invariant/constant division, small-prime FPGA
trial division, and batch smooth-part methods are prior art. The bounded gap
being tested is the combination of exact resumable valuation frontiers,
value-version discipline, mutation/propagation rules, and end-to-end repayment
accounting. Failure to locate the same integration is not a novelty finding.

## 3. Shared floor and architectural fork

Both lineages share:

```text
ideal two-state signal
-> NAND2-derived Boolean logic
-> explicit DFF state boundary
-> authoritative unsigned binary magnitude
-> identical exact DIVMOD and radix-2 mechanisms
```

They fork only at retained evidence:

```text
direct binary path
    query -> ctz or DIVMOD -> answer -> discard search state

experimental path
    query/speculation -> ctz or DIVMOD -> exact frontier
    -> tagged K-entry cache -> resume, reuse, or legal propagation
```

The bounded hardware artifact is one radix-aware valuation service, not a CPU.
It contains a `p=2` path, one time-multiplexed odd-divisor path, and a cache
front end with `K in {0,1,2,4}`.

## 4. Frozen domains

- Semantic exhaustive width: `W=8`.
- Decision widths: `W in {16,32}`.
- Optional semantic-only width: `W=64`, always nonterminal unless the same
  structural evidence is produced.
- Value slots: four.
- Generation field: eight bits. Wrap requires a full cache flush.
- Prime catalogue, in order:

```text
2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31
```

- Size-matched non-prime controls:

```text
4, 6, 9, 10, 15, 21, 25, 27, 33, 35
```

Catalogue validation is charged once as configuration. Arbitrary caller
supplied prime labels are rejected. The implementation may use ordinary
arithmetic only at semantic/oracle boundaries; declared hardware paths must be
constructed from the shared NAND/DFF substrate.

## 5. Exact frontier contract

For a nonzero authoritative magnitude `n` and catalogue prime `p`, a line is:

```text
(valid, slot, generation, prime-index, L, residual R, terminal, infinite)
```

It must satisfy:

```text
n = p^L * R
```

- `terminal=false`: `L` is a certified lower bound and later work resumes from
  `R`; no earlier exact division may be repeated.
- `terminal=true`: `p` does not divide `R`, so `L = v_p(n)` exactly.
- `infinite=true`: the source is zero; finite `L` and `R` are not interpreted
  as an ordinary valuation.
- One produces terminal exponent zero and residual one.
- A first failed odd-prime test produces a cacheable terminal exponent zero.
  Negative results may not be omitted to favor factor-rich traces.
- A threshold query may stop once `L >= k`; exact valuation and strip requests
  continue until terminal.
- An unresolved residual is never called prime.

Every line is bound to slot and generation. A stale tag is a miss, never a
hint. Malformed, out-of-catalog, overflowed, failed, or wrapped state produces a
typed rejection or flush without a stale output.

## 6. Operations

The service implements only:

- `LOAD(slot,n)`: atomically replace the binary magnitude, advance generation,
  and invalidate prior lines for the destination.
- `TEST_POWER(slot,p,k)`: decide whether `v_p(n) >= k`, refining only as far as
  required.
- `VALUATION(slot,p)`: return exact finite exponent or positive infinity.
- `STRIP_ALL(slot,p)`: return exact exponent and terminal residual.
- `MUL(destination,left,right)`: exact checked W-bit multiplication. Eligible
  same-prime frontiers may be combined; overflow rejects atomically.
- `MUL_BY_PRIME(destination,source,p)`: exact checked multiplication by a
  catalogue prime. Existing exact certificates may transfer under the stated
  valuation law.
- `ADD(destination,left,right)`: exact checked W-bit addition and destination
  invalidation. No additive valuation propagation is credited in the primary
  experiment.
- `OVERWRITE` and failed operations: invalidate or hold exactly as the frozen
  transactional contract specifies.

For prime `p`, terminal receipts propagate across multiplication because

```text
p does not divide R1 and p does not divide R2
=> p does not divide R1*R2.
```

Thus exponents add and residuals multiply. This inference is deliberately
tested against composite bases, where it is false in general (`6` divides
`2*3` although it divides neither factor). Generic checkpointing and caching
receive no prime-specific credit.

## 7. Compared policies

All applicable policies use the same radix and odd-DIVMOD mechanisms.

1. `BIN_DIRECT_BEST`
   - `ctz`/shift semantics for `p=2`;
   - exact constant-divisor predicate/extraction semantics for odd catalogue
     values;
   - no retained certificate.

2. `BIN_CONTENT_ANSWER_LRU_K`
   - immutable content-plus-divisor key;
   - caches completed answers and terminal residuals;
   - no partial frontier and no arithmetic propagation.

3. `BIN_FRONTIER_NOPROP_K`
   - same `(L,R,terminal)` frontier, lookup, replacement, and line budget as the
     candidate;
   - resumes partial searches;
   - invalidates destination evidence after every magnitude mutation.

4. `BIN_PRIME_FRONTIER_PROP_K`
   - the candidate;
   - may propagate only consequences justified by exact prime certificates and
     the committed arithmetic event.

5. `BIN_PRIME_FRONTIER_SPEC_B_K`
   - the candidate plus ordered speculative scouting;
   - after a value-producing event, performs at most `B in {1,4}` odd DIVMOD
     steps in catalogue order;
   - speculative work, stalls, transitions, and unused lines remain charged.

The content cache is a necessary control: a repeated identical answer can be a
generic memoization win. The no-propagation frontier is also necessary: saved
repeated division can be generic checkpointing. Only a strict difference
against both controls may be attributed to prime-certificate propagation.

## 8. Competent workload controls

Where the workload asks a different question, the corresponding conventional
control is required:

- Boolean divisibility: direct constant-divisor predicate, plus grouped
  product/primorial screening when several small divisors are queried.
- Exact exponent and residual: `mpz_remove`-equivalent repeated exact stripping.
- Rational reduction: ordinary binary Euclidean GCD and exact division.
- Many values over one factor base: grouped/batched small-factor screening;
  absence of a Bernstein product-tree implementation limits any wider claim.
- Producer-known structure: established sparse factor-form propagation, with no
  forced rediscovery.

Prime and similarly sized composite-divisor rows remain paired. A saving shared
with a composite divisor is attributed to constant division or memoization,
not prime identity.

## 9. Source regimes and output obligations

Source regimes are never pooled:

- `COLD_MAG`: empty cache; only binary magnitudes are supplied.
- `WARM_TRACE_PREFIX`: all warmth was earned by a measured earlier prefix.
- `PRODUCER_GENERATED`: a measured product or known-prime constructor supplied
  transferable structure.

Output obligations are never pooled:

- `PREDICATE_ONLY`;
- `EXACT_EXPONENT`;
- `EXPONENT_AND_RESIDUAL`;
- `MAGNITUDE_FINAL`;
- `MAGNITUDE_EVERY_EVENT`.

A positive search-repayment claim must begin in `COLD_MAG`. A producer-only
result is provenance preservation, not evidence that search repaid itself.

## 10. Frozen workload families

The master seed is `0x5041485742303035`. Derived streams hash protocol ID,
width, family, and replicate before SplitMix64 sampling. Trace definitions and
their generated SHA-256 digests are receipts; they may not change after a
candidate result is observed.

Required families:

1. `STATIC_REUSE`: repeated exact and threshold queries on an unchanged value,
   including high powers and exponent-zero answers.
2. `THRESHOLD_STAIRCASE`: increasing, decreasing, and shuffled thresholds for
   one `(value,p)` pair; this targets resumable frontiers.
3. `RATIONAL_CANCEL`: repeated selected-prime cancellation plus full exact
   reduction against binary GCD.
4. `DIVISIBILITY_FILTER_PERSISTENT`: repeated queries on a bounded persistent
   working set.
5. `DIVISIBILITY_FILTER_STREAM`: one query per newly loaded value; caches are
   expected to lose.
6. `SMOOTH_STRIP`: exact factor-base stripping and residual-one classification
   on smooth, near-smooth, prime, and semiprime values.
7. `MULTIPLICATIVE_DAG`: leaf certificates are earned cold, then balanced
   checked products test exact frontier propagation before forced magnitude
   output.
8. `PRODUCER_FACTORED`: facts are emitted by measured constructors and kept
   separate from search claims.
9. `ADDITION_MUTATION`: queries alternate with addition or overwrite so stale
   certificates cannot survive.
10. `PHASE_SHIFT`: high locality switches halfway to no locality.
11. `COMPOSITE_CONTROL`: size-matched composite divisors exercise the same
    checkpoint/cache paths and the invalid terminal-product inference.
12. `RADIX_V2`: `p=2` queries are isolated from every odd-prime conclusion.

Required hostile traces:

- `K+1` slot thrash and `K+1` prime-index rotation;
- mutation immediately after each fill;
- speculation poison that never requests prefetched primes;
- odd rough numbers, primes, semiprimes, and high prime powers;
- zero, one, maximum value, overflow, rejection, source/destination aliasing,
  equal magnitudes in distinct slots, and generation wrap.

At W8, all magnitudes and every catalogue query are exhaustively checked for
direct/frontier equivalence. Checked multiplication/addition exhausts every
ordered operand pair, including overflow. W16/W32 use the frozen boundary set
plus at least 10,000 derived cases per width. No skip mechanism is permitted.

## 11. Cost ledger

No weighted scalar combines unlike costs.

### Static logical vector

- NAND2 cells, DFF/state bits, input/output/port bits;
- wire bits, sinks, fanout, cross-region/lane connections;
- unit-NAND critical depth and combinational-loop status;
- cache line, tag, residual, exponent, replacement, validity, and generation
  bits;
- catalogue configuration/selection bits.

### Dynamic logical and semantic vector

- requested instructions and cycles;
- NAND evaluations, settled NAND-output transitions, state-bit transitions,
  and input transitions where the declared graph is replayed;
- CTZ calls/bit inspections, DIVMOD calls, exact divisions, failed divisibility
  probes, candidate divisors, and frontier refinements;
- lookups, tag comparisons, positive/negative hits, misses, fills, evictions,
  invalidations, transfers, and rejected stale-hit attempts;
- terminal and lower-bound certificates earned or propagated;
- speculation steps used, wasted, and search-to-first-use distance;
- reconstruction, output, GCD, grouped-screen, and arithmetic events.

### Host-software vector

Elapsed time, allocation, and runtime identity are reported separately and are
not promoted to hardware evidence or a stable architectural claim.

Every row retains `INGRESS`, `SEARCH`, `EXECUTE`, `MAINTENANCE`, and `EGRESS`
phases where applicable. `NOT_MEASURED` never means zero.

For each comparable dynamic field, report the earliest trace prefix after
which the candidate remains no worse through the trace end. Static cache cost
never amortizes away; any positive result is explicitly a dynamic repayment
with a disclosed static tradeoff unless strict full-vector Pareto dominance is
independently earned.

## 12. Structural evidence

Build one declared NAND/DFF implementation of the radix-aware valuation step
and cache front end at W8/W16/W32. The K=0 and cached variants share the same
odd-DIVMOD datapath. Tests must establish:

- acyclic NAND-only combinational structure with explicit DFF boundaries;
- exhaustive W8 `ctz`, selected-divisor DIVMOD, lookup, update, zero, invalid,
  and controller transitions;
- frozen boundary and seeded equivalence at W16/W32;
- atomic failure, stale-tag rejection, and wrap flush;
- declared graph metrics and selected switching traces.

HDL synthesis, FPGA mapping/place-and-route, ASIC/open-cell mapping, and
physical measurements are optional later evidence classes. Their absence
prevents the corresponding physical classification but not an honest semantic
or declared-logical result.

## 13. Decision rule

Record three orthogonal axes:

```text
search_policy:
  SPECULATIVE_REPAID
  DEMAND_ONLY_REPAID
  PRODUCER_ONLY
  NOT_REPAID

attribution:
  PRIME_PROPAGATION
  GENERIC_CHECKPOINT_OR_MEMOIZATION
  RADIX_V2_ONLY
  NOT_ESTABLISHED

evidence_boundary:
  SEMANTIC
  NAND_LOGICAL
  FPGA_PNR
  OPEN_CELL
  NOT_MEASURED
```

A bounded demand-driven prime-propagation candidate requires:

1. all correctness, deterministic replay, and coverage gates pass;
2. an empty-cache `COLD_MAG` start and identical exact output obligation;
3. stable dynamic no-worse repayment against `BIN_DIRECT_BEST`,
   `BIN_CONTENT_ANSWER_LRU_K`, and `BIN_FRONTIER_NOPROP_K` for every frozen
   trace in at least one non-producer required family;
4. both W16 and W32 and some `K <= 4` satisfy rule 3;
5. at least one strict saving is caused by legal odd-prime multiplicative
   certificate propagation; and
6. all static tradeoffs, hostile-family losses, and unsupported physical fields
   remain visible.

A speculative candidate additionally must beat demand-only on every trace in
the qualifying family after all speculative work is charged. Idle placement
may hide latency but never erases evaluations, transitions, or contention.

Single terminal labels, selected in order after the axes are computed:

1. `BOUNDED_SPECULATIVE_ODD_PRIME_SCOUT_CANDIDATE`
2. `BOUNDED_DEMAND_DRIVEN_PRIME_PROPAGATION_CANDIDATE`
3. `GENERIC_CACHE_ADVANTAGE_ONLY`
4. `PRODUCER_PROVENANCE_ADVANTAGE_ONLY`
5. `RADIX_V2_ONLY`
6. `SEARCH_DOES_NOT_REPAY`
7. `PARTIAL — FINAL DECISION NOT EARNED`

Missing required evidence selects `PARTIAL`; it is not converted into a
negative. No positive physical-hardware label is available without its own
named synthesis/place-and-route evidence.

## 14. Stop rule

If no K<=4 odd-prime candidate repays its complete dynamic costs at W16 and W32
on any frozen non-producer family, stop this hardware-search branch. Do not
widen the cache, catalogue, or trace horizon after observing the result.

Narrower outcomes control the recommendation:

- demand succeeds and speculation fails: preserve requested work; stop blind
  scouting;
- generic frontier matches propagation: cache repeated division, but do not
  attribute the result to prime structure;
- producer-only succeeds: pursue provenance-carrying arithmetic, not search;
- only `p=2` succeeds: retain conventional radix-aware logic;
- no candidate succeeds: report `SEARCH_DOES_NOT_REPAY`.

## 15. Evidence and replay gate

The generated result set must self-report
`PARTIAL — FINAL DECISION NOT EARNED`. A separate verifier may promote the
checkout to a candidate or negative terminal label only after it:

- verifies this frozen plan hash and baseline ancestry;
- protects inherited Build 000–004 reports and result inventories;
- restores locked dependencies, formats, builds, and runs the complete test
  assembly with zero skipped tests;
- generates twice into isolated directories and compares exact inventories and
  bytes;
- validates a self-excluding SHA-256 manifest and LF/UTF-8 text policy;
- checks every required width, policy, cache size, family, hostile trace,
  output obligation, metric sentinel, and decision predicate;
- preserves any failed raw run rather than rewriting it as a pass.

The final report must distinguish measured evidence, interpretation, prior
art, conjecture, dead ends, and open questions. Framework analogies, if any,
are retrospective and cannot validate the arithmetic or change PAL canon.
