# Build 001 cost model

## Status and claim ceiling

This document describes the cost accounting implemented by `HybridCostVector`, `HybridCostLedger`, `ValuationBank`, and `Build001ExperimentRunner`. It is a contract for interpreting Build 001 receipts, not a claim that unlike resources are interchangeable.

The model has five causal scopes:

```text
shared bank setup
per-value ingress
resident native operation
maintenance
egress demanded by a consumer
```

The core ledger contains the last four scopes. Shared bank setup is reported beside the ledger because copying a one-time catalog or policy cost into every value would be misleading. There is no universal scalar `total cost`.

Results may establish exact event counts in this implementation, logical NAND counts for the circuits actually simulated, declared analytic proxies, logical storage estimates, or timing on one recorded managed host. They do not establish universal speed, hardware latency, area, energy, or asymptotic advantage.

## Accounting object

`HybridCostLedger` is

```text
Ingress      : HybridCostVector
Native       : HybridCostVector
Maintenance  : HybridCostVector
Egress       : HybridCostVector
```

`ledger.Total` adds matching fields componentwise. It does not convert remainder tests, divisions, NAND evaluations, bytes, metadata accesses, or migrations into a common unit. Code and reports must not rank implementations by summing heterogeneous fields.

Every `HybridReceipt` carries the operation, success/failure status, failure kind, execution domain, cost ledger, validity before and after, scope, and optional detail. A failed operation reports work performed before failure and returns no value. Work that would have occurred after the detected failure is not invented.

All additive counters use checked `Int64` arithmetic. Counter overflow is therefore a runtime failure, not silent wraparound.

## Implemented cost-vector fields

The following meanings are implementation-local. “Exact count” means the instrumented code records each declared event on the executed path; it does not mean that every event has equal physical or temporal cost.

| Field | Implemented meaning | Interpretation limit |
|---|---|---|
| `BankGates.NandEvaluations` | Number of NAND evaluations performed by the explicit `GateNetwork` used for bank-lane arithmetic and reductions | Exact for that logical network only; not a static gate count or hardware energy measure |
| `BankGates.CriticalPathDepth` | Unit-NAND logical depth of the recorded network composition | No wire delay, fan-out, loading, hazards, clocking, or placement |
| `TrialRemainders` | Calls to the explicit selected-prime remainder test in ingress, refresh, normalization, or migration loops | A remainder on a 20-bit and a 2,000-bit cofactor both count as one |
| `FactorDivisions` | Successful exact divisions that remove one copy of a selected factor | Variable-width division cost is not derived from the count |
| `CofactorAdditions` | Exact host `BigInteger` additions/subtractions used on cofactors or residuals | Not a limb-operation or NAND count |
| `CofactorMultiplications` | Exact host multiplication calls, including counted multiplication steps in exponentiation or migration | Operand sizes vary; the counter alone is not work |
| `CofactorDivisions` | Exact host divisions on cofactors | No analytic NAND proxy is currently assigned |
| `CofactorRemainders` | Exact host remainder tests on cofactors outside the selected-prime extraction counters | No analytic NAND proxy is currently assigned |
| `CofactorGcds` | Host gcd calls | Internal gcd iterations and operand evolution are not measured |
| `CofactorComparisons` | Host comparisons of exact cofactor or reconstructed values | Comparison width and memory traffic are not recovered from this count |
| `ReconstructionMultiplications` | Multiplications executed by the explicit reconstruction/powering route | Their variable-width cost is not modeled as NANDs in this field |
| `ModeledBinaryNands` | A declared analytic proxy for selected binary multiplication or power work | Not evaluated gates, not the algorithm used by `BigInteger`, and has no modeled critical depth |
| `BinaryOperandBits` | Sum of operand/result bit lengths that the calling operation explicitly records | Not logical bit reads, bytes moved, or a sufficient complexity measure |
| `LaneReads` / `LaneWrites` | Logical accesses to bank exponent lanes | Not managed-memory loads/stores or cache transactions |
| `KnowledgeTransitions` | Explicit lane knowledge-state changes counted by the operation | Does not include every enum copy or object write |
| `MetadataReads` / `MetadataWrites` | Logical accesses to sign, validity, provenance, and related representation metadata | Not actual field traffic after JIT optimization |
| `SerializedBytes` | UTF-8 byte length accepted or emitted at the serialization boundary | Parsing, formatting, allocation, and validation time are not implied by byte count |
| `Migrations` | Per-value bank-migration operation count | A bank policy change affecting 16 live values records 16 migrations; policy selection itself is separate |

Zero in one of these fields means the implementation recorded zero events of that kind. It does not mean the operation was physically free. In particular, a zero `ModeledBinaryNands` value does not certify that an unmodeled `BigInteger` addition, division, remainder, gcd, comparison, or reconstruction had zero cost.

### Gate-depth aggregation

Inside one bank operation, independent lane circuits are combined with `GateCost.Parallel`: NAND evaluations sum and depth is the maximum lane depth. Explicit carry/borrow/overflow reduction is then added to that network.

When cost vectors or receipts are composed, `HybridCostVector.operator +` treats their gate costs as serial: NAND evaluations and depths both add. The resulting depth is a serial trace estimate, not the maximum critical path of a static multi-operation hardware design. The pilot workload CSV exports native NAND evaluations but not critical depth; full receipts such as the VM trace retain both fields.

## Phase attribution

### Shared bank setup

Bank setup occurs before a value is ingested and is not a `HybridCostLedger` phase. The current implementation exposes:

- `ValuationBank.ValidationTrialDivisions`: odd-divisor trial tests used to validate each stored prime label;
- `ValuationBank.CatalogPayloadBits`: the sum of the unsigned bit lengths of all prime labels;
- bank identity, strategy, size, and policy-change counts as separate descriptors.

`bank_strategy_matrix.csv` reports validation trial divisions and catalog bits explicitly. Catalog bits are labeled `unamortized`; the runner does not silently divide them by the number of values.

The following setup work is not currently measured and must not be read as zero:

- candidate search while constructing `FIRST_K`;
- a certificate or independent proof that catalog labels are prime;
- training, scoring, or source scanning for `SELECTED_K`;
- generation, hashing, persistence, and loading of a shared catalog;
- allocation, sorting, duplicate checks, names, strategy/version fields, and bank lookup structures;
- LRU bookkeeping and the decision logic that selects an adaptive victim;
- fixed precomputation for constant divisors;
- JIT and warmup except where separately described by the timing harness.

The pilot `SELECTED_K` membership is deterministically chosen by the runner; it is not a measured execution of the full preregistered training selector. The main magnitude-source adaptive rows are deliberately labeled `ADAPTIVE_LRU_K_NO_ELIGIBLE_MAG_REFERENCE`: magnitude ingress does not reveal an unbanked prime identity, so no adaptation is credited. The separate thrashing experiment measures per-value migration consequences, not the full policy implementation cost.

### Ingress

Ingress is work caused by converting or validating an external value into resident representation. Examples include:

- selected-prime remainder tests and factor divisions in `FromBinary`;
- sign, zero, exponent-width, lane-count, knowledge-state, and exact-cofactor validation;
- claimed-magnitude reconstruction and comparison when externally supplied components must be verified;
- JSON decoding and executable-contract validation;
- logical lane and metadata writes caused by the load.

The pilot workload matrix uses source mode `MAG`: each hybrid event ingests `A`, `B`, and, where required, `C` from exact binary magnitudes and charges those receipts. An ingress failure remains a row and retains work done before failure.

### Native

Native cost is work required to perform the requested operation on resident operands. It includes explicit exponent-lane circuits, cofactor arithmetic invoked by the operation, knowledge transitions caused directly by the result, and logical accesses.

Examples include exponent addition for multiplication, coordinate minima and residual addition for `ADD_PRESERVE`, lane subtraction plus exact cofactor division, gcd/lcm cofactor work, and bank-local comparisons. A domain crossing required to implement the operation belongs here; it is not reclassified as egress merely because it uses ordinary magnitude arithmetic.

### Maintenance

Maintenance is work needed to restore or change legal resident state after or between native operations. The implementation assigns these costs to maintenance:

- refreshing a certified lower-bound lane;
- normalizing every deferred lane;
- stripping newly admitted bank primes;
- folding evicted prime powers back into the exact cofactor;
- copying lane/knowledge metadata during bank migration;
- recording a per-value migration.

Lazy addition can return an exact partial value without immediate maintenance. The runner increments `partial_outputs`; eager mode calls `Normalize` and charges the resulting remainder tests and factor divisions. If a later consumer only asks for exact magnitude, reconstruction remains possible from the lower bound plus exact cofactor and is charged as egress. Lazy debt is therefore visible, but it is not automatically forced in every pilot operation beyond the declared output obligation.

### Egress

Egress is work caused only by the consumer's requested boundary form. Implemented examples are:

- exact signed-magnitude reconstruction;
- deterministic JSON serialization;
- cross-bank or partial-value equality that reconstructs both exact magnitudes for comparison.

`RunHybrid` calls `VerifyMagnitude` for magnitude-final workloads and adds the reconstruction receipt to egress. Predicate workloads instead demand every predicate result and do not add an irrelevant magnitude conversion.

## Exact circuits and analytic proxies

### Explicit NAND circuits

`BankGates` comes from the actual NAND-only logical circuit simulator used by `BinaryCircuit`. It covers the implemented fixed-width lane arithmetic and explicit result reductions. For a declared operand and exponent width, the NAND evaluation count and logical depth are reproducible properties of this source implementation.

This does not mean bank arithmetic has been elaborated into a complete processor. Registers, DFFs, routing, address decoding, control, ports, memory, clock cycles, initiation interval, area, and physical timing are not structurally realized by this counter.

### Schoolbook multiplication proxy

Both the hybrid core and binary pilot use the declared proxy

```text
modeled_multiply_nands(w) = 32 * w^2
```

where `w` is the selected maximum operand bit width. This is an intentionally simple schoolbook-style proxy. The host still executes `BigInteger` multiplication; the proxy neither observes nor predicts the actual .NET multiplication algorithm.

The binary pilot also uses

```text
modeled_add_nands(w) = 15 * w
```

for its ordinary-addition control. Hybrid cofactor addition is currently recorded as a `CofactorAdditions` event and operand-bit detail, not as `ModeledBinaryNands`. The two columns must therefore remain separate.

### Square-and-multiply power proxy

Hybrid cofactor powers use an explicit square-and-multiply accounting walk. For each mathematical multiply or square, the model recomputes the current operand widths and adds `32 * max(width_left, width_right)^2`. It also records the number of cofactor multiplications and summed operand bits.

The calculation is a proxy over mathematically evolving values. It is not a simulated NAND circuit, a model of `BigInteger.Pow`, or a constant-width hardware power unit.

### What may and may not be combined

The raw cost model never adds an exact remainder test, a host division, a logical lane access, a byte, or a migration to a NAND count.

`crossovers.csv` contains a deliberately narrower diagnostic:

```text
hybrid_work_proxy = exact_bank_nand_evaluations + modeled_binary_nands
binary_work_proxy = modeled_schoolbook_multiply_nands * reuse
```

Those two terms are both expressed in nominal NAND evaluations, but one is simulated and the other analytic. More importantly, this crossover proxy omits ingress remainder/division work, metadata and lane traffic, maintenance, catalog/setup work, and the unmodeled cost of reconstruction. Its `hybrid_work_lower` boolean answers only the declared proxy inequality. It is not a total-work result and cannot assert an advantage.

## Storage and catalog accounting

`HybridInteger.MeasurePayload()` reports a logical per-value model:

```text
sign_and_zero_bits = 2
exponent_bits      = bank_size * exponent_width
cofactor_bits      = 0 for zero, otherwise bit_length(cofactor)
knowledge_bits     = bank_size
provenance_bits    = bank_size * ceil(log2(number_of_provenance_states))
```

With the current 11 provenance states, the model assigns four provenance bits per lane. `PerValuePayloadBits` sums those five categories. `BankCatalogBits` is returned separately and is never included in the per-value total.

This is a logical resident-payload estimate, not an actual managed layout or canonical wire length. In particular:

- the deterministic JSON representation omits provenance by design and is measured separately in UTF-8 bytes;
- object headers, references, array lengths/capacities, alignment, enum storage, `BigInteger` limb capacity, and allocator overhead are absent;
- bank references, strategy/name/version fields, lookup structures, and policy state are absent;
- the catalog estimate counts only prime-value bits, not delimiters, slots, certificates, membership bits, or validation records;
- cache locality, peak live memory, copying, and allocated bytes are not measured.

The pilot workload matrix reports the integer-truncated average input-value payload and the catalog bits in separate columns. It does not sample every intermediate or output value. The bank-strategy matrix gives separate average cofactor, exponent, and metadata bits. `crossovers.csv` compares two resident input payloads and excludes catalog and managed overhead. These are bounded logical comparisons only.

No catalog amortization is currently emitted. A later analysis may show `catalog_bits / N` only when it names the live-value count `N`, retains the unamortized catalog column, and also accounts for policy/setup state. It may not erase catalog cost by choosing a favorable unreported population.

## Workload-comparison discipline

The pilot runner improves comparability in several concrete ways:

- one deterministic event corpus is reused across compatible implementations for each workload family;
- rows preserve implementation, bank policy, bank size, event count, operation count, failure count, partial-output count, and output obligation;
- hybrid magnitude-source ingress is charged on every event rather than treated as free setup;
- magnitude-final hybrid workloads charge exact reconstruction;
- eager and lazy maintenance policies remain separate rows;
- unsupported Build 000 coordinate workloads remain `NOT_SUPPORTED`, and basis escapes remain visible;
- the requested complete sparse-factor and Build 000 sparse-operation controls remain explicit `NOT_SUPPORTED` rows because the pilot lacks independent executable operation/output replays; oracle or dense costs are not relabeled;
- favorable product/cancel, divisibility, addition, mixed, and outside-bank adversarial families are all present.

The following limits prevent an advantage claim:

1. The current run is a pilot subset: source mode is only `MAG`, exponent width is 16, confirmation replicates are one, and the frozen 1,800-trace confirmation/Pareto matrix was not executed.
2. `BIN_EXACT` starts in its native `BigInteger` form, which is appropriate for `MAG`; the pilot does not execute the reciprocal `FACT` or `BANK_CERT` conversion cases.
3. Binary arithmetic is executed by optimized `BigInteger`, while its NAND columns are analytic proxies. Hybrid cofactor arithmetic uses the same host integer class, with only selected operations receiving NAND proxies.
4. Binary division, remainder, hybrid cofactor division/remainder/gcd/comparison, and reconstruction remain separate event counters without a common work model.
5. `SPARSE_FULL` is not executed: the pilot does not implement an independent full-factor-map operation/output replay, and no oracle arithmetic is credited as a comparator. PARI, FLINT, FriCAS, GMP, and other production baselines are also absent.
6. The Build 000 dense control supports only the positive fixed-basis product/cancel subdomain. The sparse control is `NOT_SUPPORTED` because the existing sparse type lacks exact cancellation replay; dense gate costs are never relabeled as sparse. Unsupported rows cannot enter a Pareto comparison.
7. The main adaptive magnitude rows cannot observe new prime identities and therefore do not execute real replacement. The migration-thrash table is a separate deterministic stress test.
8. Average logical payloads exclude actual managed memory and shared-policy state, and use integer division when averaged.
9. Reference-value generation and oracle arithmetic used to form expected results are outside costed operations.
10. The exploratory corpus is reproducibly seeded for the recorded .NET runtime but does not implement the frozen SplitMix64/FNV trace namespaces; it is not a confirmation-trace substitute.

A comparison is admissible only if it names the implementation, workload ID, source mode, output obligation, bank policy and size, exponent width, eager/lazy policy, failures, and which cost-vector fields are being compared. “Hybrid is cheaper” or “binary is faster” without those qualifiers is unsupported.

## Timing measurements

Wall-clock microbenchmarks are optional and separate from deterministic logical receipts. The current harness:

- builds fixed operands;
- performs up to 1,000 untimed warmup iterations;
- executes seven measured trials;
- reports median, minimum, and maximum nanoseconds per operation;
- assigns each result to a static sink to reduce dead-code elimination risk;
- records whether benchmarks were included plus framework, OS, architecture, processor string, logical-processor count, and assembly hashes in the manifest.

The timing ceiling is deliberately low. Measurements describe one managed implementation, process, runtime, and host at one time. They are not hardware latency, physical switching cost, a population estimate, or an asymptotic result.

The current microbenchmark harness does not record raw per-trial rows, allocation bytes, GC counts, process affinity, CPU frequency/power state, concurrent load, confidence intervals, or a timing-order randomization. Different benchmark cases also use different iteration counts, and receipt/object allocation remains inside hybrid timed actions. JIT tiering, GC, OS scheduling, thermal state, and background work may influence samples. A timing difference cannot support an advantage claim without the unexecuted preregistered confirmation protocol and competent external baselines.

## Known unmeasured or partially measured costs

The following are outside the current model or recorded only by a coarse event counter:

- actual algorithms, limb operations, constant factors, and memory traffic inside `BigInteger` arithmetic;
- variable-width costs for addition, division, remainder, gcd, comparison, and reconstruction;
- managed allocations, object headers, copying, garbage collection, and peak live memory;
- cache behavior, locality, branch prediction, vectorization, and parallel-runtime overhead;
- bank lookup/address decoding, prime-index routing, and constant-divisor precomputation;
- full selected-bank training, factor discovery used by selection, adaptive-policy bookkeeping, and catalog certification;
- sorting, hashing, dictionary/list operations, policy versions, and concurrency control;
- JSON parsing/formatting CPU work, decimal conversion, and allocation beyond serialized byte length;
- VM instruction decode, register-map access, dispatch, trace creation, and failed-destination invalidation overhead;
- static NAND2/DFF resources, wiring, fan-out, ports, cycles, throughput, and initiation interval;
- transistor/relay behavior, energy, clocking, voltage margins, hazards, and physical timing;
- compiler/runtime alternatives and third-party arithmetic/CAS implementations;
- setup amortization over declared live-value populations;
- magnitude/output work ceilings, cancellation, and allocation guards for contract-valid but computationally infeasible exponents or cofactors;
- the full frozen Build 001 confirmation and hardware/Pareto cells.

These omissions are not defects hidden behind a scalar. They define the evidence ceiling. A later build may add measured fields, but it must preserve old raw counters, version the model, and avoid retrospectively converting `NOT_MEASURED` into zero.

## Reading the generated artifacts

The main cost-facing artifacts produced by the runner are:

| Artifact | Cost evidence | Ceiling |
|---|---|---|
| `workload_matrix.csv` | Selected projection of four-phase ledgers, logical payload, catalog, status, and output obligation | Pilot workload/configuration rows; not the complete vector |
| `bank_strategy_matrix.csv` | Payload split, support occupancy, ingress extraction, unamortized catalog, and measured prime-label validation trials | Selection and policy setup remain incomplete |
| `addition_freshness.csv` | Exact/lower-bound lanes and eager refresh work | One deterministic sample of 1,000 pairs per bank size and fixed widths |
| `migration_thrash.csv` | Per-value migration, extraction, cofactor multiplication, and lane traffic | Policy-decision and managed-memory costs omitted |
| `crossovers.csv` | Narrow NAND-denominated reuse proxy and logical input payload | Diagnostic inequality only; not total cost |
| `vm_trace.json` | Full machine receipts including all ledger fields and failure invalidation | One success and one overflow trace |
| `microbenchmarks.csv` | Median/min/max managed host timing when enabled | Secondary single-host evidence only |
| `manifest.json` | hashes, environment, seed, benchmark inclusion, protocol status, and decision ceiling | Does not broaden protocol coverage |

The strongest legitimate conclusion from a cost row is the literal bounded statement encoded by its counters. An apparent local reduction in one field must be accompanied by all increased fields, conversion phases, storage, setup, failure status, output obligation, and known omissions. No Build 001 cost artifact by itself asserts an advantage.
