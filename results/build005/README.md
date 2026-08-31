# Build 005 generated evidence

> **Generated status: `PARTIAL — FINAL DECISION NOT EARNED`**
> Frozen decision: `PARTIAL — FINAL DECISION NOT EARNED`

This result set tests whether a four-line-or-smaller exact valuation-frontier cache repays bounded small-prime search above authoritative binary magnitude. Prime propagation, generic memoization, generic checkpointing, radix-2 locality, and speculative search remain separate attribution axes.

## Implemented trace coverage

- `ADDITION_MUTATION`: 48/48 rows; 49362 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `COMPOSITE_CONTROL`: 48/48 rows; 6000 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `DIVISIBILITY_FILTER_PERSISTENT`: 48/48 rows; 161928 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `DIVISIBILITY_FILTER_STREAM`: 48/48 rows; 116016 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `HOSTILE_BOUNDARY_FAILURE`: 48/48 rows; 5604 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `HOSTILE_GENERATION_WRAP`: 48/48 rows; 110490 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `HOSTILE_MUTATE_AFTER_FILL`: 48/48 rows; 36714 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `HOSTILE_PRIME_THRASH`: 48/48 rows; 19674 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `HOSTILE_SLOT_THRASH`: 48/48 rows; 42120 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `HOSTILE_SPECULATION_POISON`: 48/48 rows; 58122 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `MULTIPLICATIVE_DAG`: 48/48 rows; 7016 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `PHASE_SHIFT`: 48/48 rows; 89178 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `PRODUCER_FACTORED`: 48/48 rows; 5384 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `RADIX_V2`: 48/48 rows; 58572 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `RATIONAL_CANCEL`: 48/48 rows; 9828 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `SMOOTH_STRIP`: 48/48 rows; 89982 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `STATIC_REUSE`: 48/48 rows; 18180 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`
- `THRESHOLD_STAIRCASE`: 48/48 rows; 19194 checks; 0 failures; `IMPLEMENTED_TRACE_PASS`

Total checks: 1066724; failures: 0.
Workload rows: 864; break-even comparisons: 1134; static component rows: 48.

## Frozen decision

- search policy: `NOT_EARNED`
- attribution: `NOT_ESTABLISHED`
- evidence boundary: `SEMANTIC`
- decision axes earned: `false`
- qualifying demand families: `NONE`
- qualifying speculative families: `NONE`

Exploratory pattern: `EXPLORATORY_GENERIC_REUSE_PATTERN`.
Exploratory search observation: `BLIND_SPECULATION_INCURRED_WASTED_WORK`.

No exploratory break-even row is eligible for the frozen decision. Prime-specific optimization is not established.

### Unmet frozen gates

- `PRE_RESULT_TRACE_DIGEST_REGISTRY`: Trace digests are emitted from the executed mutable factory; no independent pre-result digest registry exists.
- `ALL_OUTPUT_OBLIGATIONS`: The current corpus uses MAGNITUDE_FINAL only; predicate, exact-exponent, residual, and every-event obligations are not separately costed.
- `PHASE_AND_TRANSITION_LEDGER`: INGRESS/SEARCH/EXECUTE/MAINTENANCE/EGRESS and settled NAND/input/state transition series are not recorded per trace prefix.
- `INTEGRATED_PROPAGATION_HARDWARE`: Semantic propagation arithmetic is counted, but no integrated exponent/residual combiner netlist or switching trace is present.
- `POLICY_MATCHED_CONTENT_CACHE_HARDWARE`: The declared cache graph is slot/generation keyed; the W-bit content-plus-divisor cache has no matching structural graph.
- `COMPETENT_CONVENTIONAL_CONTROLS`: Grouped screening, full rational reduction, cumulative smooth stripping, producer-known sparse factor form, and a strict radix comparator are incomplete.
- `CAUSAL_PRIME_ATTRIBUTION`: No frozen paired witness binds an odd-prime terminal transfer to saved DIVMOD while rejecting the composite-product inference.
- `FULL_INDEPENDENT_CORRECTNESS_MATRIX`: The independent W8/W16/W32 checks do not yet cover every cache capacity, arithmetic policy, transaction boundary, and K+1 hostile identity.
- `EXTERNAL_DETERMINISTIC_REPLAY`: Only the external verifier may establish two-run replay, zero skipped tests, inherited-evidence protection, and manifest integrity.

The NAND/DFF material is an exact additive inventory over separately built catalogue, CTZ, divider, and slot/generation-cache graphs. It is not the policy-matched content cache, propagation combiner, integrated service, phase/transition ledger, synthesis result, or physical measurement. Its totals are exploratory and do not drive the decision.

Regenerate with:

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build005 --output results/build005
```

Bounded exact semantic, host-software, and declared NAND/DFF evidence under PAH-BUILD005-DEMAND-VALUATION-0001 only; no universal arithmetic, novelty, FPGA/ASIC PPA, physical-energy, fabricated-hardware, or PAL-conformance claim.
