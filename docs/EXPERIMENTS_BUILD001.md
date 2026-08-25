# Build 001 experiments — frozen protocol

Protocol status: **FROZEN FULL PROTOCOL — PILOT SUBSET COMPLETE; FULL CONFIRMATION NOT RUN**

Frozen source: `research/build001_experiment_plan.md`

Frozen source SHA-256: `4A79873ADCBE477944FFBE1D90AD0969AD99560AA0C014F3CB1DD8639FF9DDEF`

This document promotes the complete preregistered Build 001 protocol without changing its rules. Execution coverage and status are appended after the frozen body; they do not amend it.

---
# Build 001 adversarial experiment preregistration

Status: **PREREGISTERED DESIGN — NO BUILD 001 RESULTS**

Scope: bounded valuation bank plus exact cofactor on the settled Build 000 binary floor

Controlling request: Prime Axiom Software — Build 001

Evidence boundary: this document does not amend `BUILD_000_REPORT.md`, `docs/OBSERVATIONS.md`, or `results/build000/`

## 1. Purpose and freeze rule

Build 000 found that prime structure did not belong below stable binary state and software-readable representation. It found a narrower systems question worth testing: whether already available multiplicative structure can remain useful in a bounded valuation sidecar rather than being repeatedly destroyed and rediscovered.

This plan freezes the Build 001 confirmation matrix before implementation results are inspected. Its working null is:

> After source conversion, exactness, bank/catalog state, payload and metadata, bank maintenance, invalidation or refresh, and demanded output are charged, a bounded valuation bank plus exact cofactor offers no reproducible Pareto improvement over competent binary and factor-coordinate controls.

Expected local wins are hypotheses, not results:

- resident multiplication, exact cancellation, valuation, divisibility, GCD, and LCM may benefit from known bank valuations;
- one-shot magnitude-native, addition-heavy, outside-bank, large-cofactor, and bank-thrashing work may favor binary magnitude;
- workload-selected or adaptive banks may help only when factor demand persists long enough to repay selection and migration;
- a lazy freshness state may save eager refresh work, or it may merely defer it to a required output.

The confirmation manifest, trace hashes, implementation identifiers, and this file's hash must be frozen before any confirmation aggregate is viewed. Parameter tuning may use only the separately seeded training/discovery partition. Any change after viewing confirmation output requires an append-only amendment containing the old and new plan/manifest hashes, the reason, affected cells, and a new confirmation seed namespace. Amended results cannot be pooled with the original confirmation run.

No Build 001 result may be copied into Build 000 evidence. Reuse of Build 000 code is allowed, but any changed cost model receives a new Build 001 implementation ID and new receipts.

## 2. Claim ceiling and research questions

The experiments can establish bounded functional equivalence, representation invariants, exact event counts in declared implementations, and host/runtime or abstract-circuit measurements on declared configurations. They cannot establish a universal physical, asymptotic, energy, or hardware advantage.

The primary questions are:

1. Does an eager canonical hybrid amortize its ingress and metadata on any named workload?
2. Which fixed bank size, if any, is useful: 4, 8, 16, or 32 lanes?
3. Does offline workload selection beat the first-prime policy after selection cost is charged?
4. Does an online adaptive bank beat a fixed bank after every install, eviction, migration, and policy bit is charged?
5. How quickly do addition and subtraction invalidate or refresh useful valuations?
6. Does an explicit unknown/stale state earn its own complexity when all final observables are forced?
7. Are any apparent wins still present against Build 000 dense/sparse controls and a complete sparse-factor baseline on the subdomains where those controls are exact?
8. Where is work removed, and where is it merely moved among setup, ingress, native execution, maintenance, and egress?

All conclusions must name the workload, source mode, output obligation, width, bank policy, execution level, and resource profile. There is no unqualified “hybrid is faster” claim.

## 3. Common semantic machine

### 3.1 Value domain

Functional cells use exact signed integers with magnitude cap

```text
-(2^L - 1) <= x <= 2^L - 1
```

for `L in {16, 32, 64, 128, 256}`. Structural cells use the positive subset at `L in {8, 16, 32, 64}` unless a signed structural implementation is explicitly realized.

`0`, `+1`, `-1`, sign, and invalidity are distinct. Operations are checked, not modular. If an exact result is outside the cell's magnitude domain, the semantic status is `RANGE_OVERFLOW`, no result value exists, and the destination register is not written. A representation-internal exponent overflow may not replace `RANGE_OVERFLOW` when the mathematical result itself is out of range.

The primary exponent width is fixed by the semantic range:

```text
e_bits(L) = ceil(log2(L))
```

This represents every possible `v_2(x)` for a nonzero in-range value. Deliberately undersized lanes and one-step overflow are robustness cells only and are not pooled into performance conclusions.

### 3.2 Operation statuses

Every operation returns exactly one status from:

```text
OK
ZERO_UNDEFINED
NON_EXACT_DIVISION
RANGE_OVERFLOW
EXPONENT_OVERFLOW
BASIS_ESCAPE
UNKNOWN_REQUIRED
STALE_REQUIRED
MALFORMED_INPUT
INVALID_INPUT
NOT_SUPPORTED
RESOURCE_LIMIT
FAILED_INFRASTRUCTURE
```

Only `OK` carries a result. Predicate and valuation results carry their typed scalar only on `OK`. A failed VM-style operation cannot expose a prior successful result, and a failed destination write is recorded as `destination_written=false`.

`BASIS_ESCAPE` is an expected representational limitation for a pure fixed-basis coordinate value, not permission to drop that input. `RESOURCE_LIMIT` and `FAILED_INFRASTRUCTURE` are censored outcomes, not slow measured times and not arithmetic failures.

### 3.3 Semantic benchmark IR

The shared trace language contains semantic operations, not representation-favoring opcodes:

```text
LOAD_MAGNITUDE
LOAD_FACTORED
MULTIPLY
EXACT_DIVIDE
POWER
ADD
SUBTRACT
GCD
LCM
DIVIDES
VALUATION(p)
DIVIDES_BY_PRIME_POWER(p, e)
FACTOR_CONTAINS(p, e)
COMPARE
FORCE_MAGNITUDE
FORCE_CANONICAL_SERIALIZATION
```

`LOAD_FACTORED` supplies a canonical signed full factor list whose prime claims and exact product are independently verifiable. It does not also supply an uncharged binary magnitude. A binary implementation must reconstruct; a hybrid filters the factors into its bank and cofactor; a complete sparse implementation may retain them. `LOAD_MAGNITUDE` supplies only the binary magnitude. Oracle factor data used to construct that corpus is inaccessible to the implementation under test.

The IR is a benchmark interface, not an earned instruction set. No Build 001 ISA claim follows from operation names.

### 3.4 Register traces and output obligations

Random traces use 32 logical registers. Each trace has 32 charged initial loads followed by 4,096 semantic instructions. At instruction positions 1,024, 2,048, and 3,072, all 32 registers are explicitly reloaded; those 96 renewal loads are part of ingress rather than hidden dataset setup. Checked failures leave their destination unchanged.

Unless a workload says otherwise, the runner demands:

- every predicate or valuation result when issued;
- one exact magnitude at instructions 256, 512, ..., 4,096, using register `(instruction_index / 256 - 1) mod 32`;
- exact magnitudes and canonical serializations for all 32 registers at trace end.

These forcing points prevent lazy debt from remaining unobserved. Separate crossover sweeps vary forcing frequency; the main matrix does not.

The exact semantic trace, expected status, expected result hash, source provenance, and forcing points are generated once by the independent oracle and replayed unchanged by every compatible representation.

Because a semantic trace is representation-independent, it does not encode `BASIS_ESCAPE`. Before replay, an independent applicability pass classifies each event/representation as `IN_DOMAIN`, `EXPECTED_BASIS_ESCAPE`, or `NOT_SUPPORTED`. A pure-coordinate escape is correct only when that applicability receipt predicts it; it is still excluded from supported cost/Pareto comparisons. An unexpected escape is a correctness failure.

## 4. Competing representations

| ID | Required form | Exact domain | Bank policies | Primary role |
|---|---|---|---|---|
| `BIN_EXACT` | Canonical exact sign and binary magnitude; fixed-capacity and variable-length sizes both reported | Entire semantic domain | `NONE` | Competent conventional baseline |
| `B000_DENSE` | Build 000 tagged dense exponent lanes with fixed basis and fixed exponent width | Zero/sign only through explicit tag; nonzero magnitude must be fully basis-smooth | `FIRST_K`, `SELECTED_K` | Dense pure-coordinate control |
| `B000_SPARSE` | Build 000 canonical sorted nonzero `(basis_slot, exponent)` entries | Same fixed-basis domain as `B000_DENSE` | `FIRST_K`, `SELECTED_K` | Sparse pure-coordinate control |
| `SPARSE_FULL` | Canonical complete prime-value/exponent map plus sign/zero tag | Entire domain only when full factorization or a verified full factor source is available | no bounded bank | Full-factor control; all factorization/certificate work charged |
| `HYBRID_EAGER` | Sign/zero, selected-prime exponents, exact positive binary cofactor, validity/provenance/version state | Entire semantic domain | `NONE`, `FIRST_K`, `SELECTED_K`, `ADAPTIVE_LRU_K` | Primary Build 001 candidate |
| `HYBRID_LAZY` | Exact reconstruction plus explicitly known/unknown/stale lane state | Entire domain if implemented contract remains exact | same as eager | Preregistered ablation; never pooled with eager |

The mandatory functional replay therefore has 31 configuration points per common semantic trace: one `BIN_EXACT`, eight `B000_DENSE` (four `FIRST_K`, four `SELECTED_K`), eight `B000_SPARSE`, one `SPARSE_FULL`, and thirteen `HYBRID_EAGER` (one `NONE` plus four each of `FIRST_K`, `SELECTED_K`, and `ADAPTIVE_LRU_K`). `HYBRID_LAZY` adds thirteen separate ablation points if its contract is implemented. Unsupported pure-coordinate events remain rows rather than shrinking the matrix.

`SPARSE_FULL` is a necessary control in addition to the literal Build 000 sparse type. It uses self-delimiting prime-value keys so it can remain exact outside a finite catalog, and it pays for those keys, primality/factor certificates, sorting, allocation, and full factorization on magnitude-native ingress.

The minimum eager hybrid invariant for every nonzero valid value is:

```text
abs(x) = cofactor * product(p_i ^ e_i)
cofactor >= 1
gcd(cofactor, product(bank)) = 1
0 <= e_i < 2^e_bits
bank identity and version match the value
```

Sign is stored separately. Zero has no factor/cofactor interpretation and has one canonical encoding. `+1` has positive sign, all-zero valuations, and cofactor 1. A composite cofactor remains an opaque exact integer; it is never relabeled as a prime atom.

The lazy ablation must publish a separate contract before execution. At minimum it must distinguish an exact known valuation, an unknown valuation, and invalid state; unknown is never decoded as zero. It must retain enough exact state to reconstruct the integer. If those rules cannot be met, `HYBRID_LAZY` is `NOT_SUPPORTED`, not silently replaced by eager refresh.

### 4.1 Size accounting

For every live value, report both:

- logical canonical serialization bits, split into payload and metadata;
- actual managed resident bytes, capacity bytes, and allocated bytes.

The logical split has the following required categories:

```text
kind/sign/zero bits
binary magnitude or cofactor bits
exponent bits
sparse key bits
sparse length/delimiter bits
lane freshness/validity bits
provenance bits
bank version bits
certificate bits
alignment/capacity bits
```

Catalog and policy state are separate shared resources, never divided away silently:

```text
prime-value bits
slot/index bits
membership bits
replacement-policy bits
catalog version bits
catalog validation/certificate bits
```

Reports show catalog cost unamortized and amortized over live-value counts `N in {1, 8, 32, 256, 4096}`. A theoretical minimum, canonical wire size, and actual managed layout are different columns.

## 5. Prime-bank policies

### 5.1 Candidate catalog

The bank candidate catalog `C256` is the first 256 mathematical primes in ascending order, generated by a deterministic sieve and stored with its SHA-256. The prime's ordinal in `C256` is an eight-bit logical slot only inside catalog-backed forms; prime values/catalog storage remain charged.

All fixed and adaptive bank comparisons use the same candidate catalog. Large-cofactor pathologies may use primes outside `C256`; they cannot be inserted into the registered adaptive bank and must remain in the exact cofactor.

### 5.2 Fixed first-prime banks

`FIRST_K` is exactly the first `K` entries of `C256`, for `K in {4, 8, 16, 32}`. Its membership is frozen across workloads. Catalog generation and validation are setup costs; membership is not a zero-cost convention.

### 5.3 Workload-selected banks

`SELECTED_K` is selected separately for each `(workload_family, L, source_mode)` from the training partition only. Here `workload_family` is exactly one of `M`, `D`, `A`, `X`, or `T`, determined by the workload-ID prefix. Every training event from every registered workload ID in that family contributes once; there is no post hoc per-kernel bank. Candidates are restricted to `C256`. For candidate prime `p`, the frozen score is:

```text
score(p) =
    number of training LOAD operand occurrences whose exact value has v_p(x) > 0
  + number of explicit training VALUATION / DIVIDES_BY_PRIME_POWER /
    FACTOR_CONTAINS queries naming p
```

Select the `K` highest scores; ties go to the smaller prime. No cost-model coefficient or confirmation observation enters the score.

For `LOAD_FACTORED`, factor occurrences are visible to the selector after certificate validation. For `LOAD_MAGNITUDE`, deriving the first term requires the registered factor/valuation scanning algorithm and is charged as offline selection setup. Oracle factors are forbidden. The selected membership, all candidate scores, training trace hashes, selection work, and selector implementation hash are frozen in `bank_selections.json` before confirmation traces run.

Primary reports include both steady-state cost and setup-amortized cost at the registered reuse counts. A selected bank cannot win solely by omitting training and selection.

### 5.4 Adaptive bank

`ADAPTIVE_LRU_K` has capacity `K in {4, 8, 16, 32}`, begins with `FIRST_K`, and uses deterministic least-recently-used replacement. A candidate becomes observable only through:

- a validated factor named by `LOAD_FACTORED`;
- an explicit prime argument to `VALUATION`, `DIVIDES_BY_PRIME_POWER`, `FACTOR_CONTAINS`, or prime scaling;
- structure produced by a prior operation that already carried that verified identity.

`LOAD_MAGNITUDE` does not reveal an unbanked prime. An oracle factorization, a result inspector, or the future trace may not drive replacement.

On an eligible out-of-bank reference, the prime is installed immediately before the semantic operation. Evict the least recently referenced member; ties use the lower current slot, then the smaller prime. Membership is kept in ascending-prime serialization order, but LRU age is separate metadata. Every membership change increments a 32-bit bank version.

A `LOAD_FACTORED` processes eligible `C256` factors in the source's canonical ascending-prime order. Every factor occurrence updates recency once regardless of exponent; outside-catalog factors do not enter the bank. This order is part of the trace contract, not a collection-iteration accident.

Migration is global to the trace's 32-register file and is charged in maintenance:

- eviction multiplies every nonzero resident exponent of the evicted prime into that value's cofactor;
- installation trial-divides every live cofactor by the installed prime until no factor remains;
- every value's version, lane data, and validity state are updated or the value becomes explicitly invalid;
- reads, writes, exponentiation/multiplication, division, allocation, and policy work are recorded.

No adaptive Build 000 dense/sparse cell exists. Once an evicted factor is retained in a cofactor to preserve exactness, the representation is hybrid rather than the Build 000 pure coordinate form.

### 5.5 No-bank ablation

`HYBRID_EAGER/NONE` has zero exponent lanes and an exact binary cofactor. It retains all mandatory tags/provenance machinery. This exposes metadata and abstraction overhead relative to `BIN_EXACT`; it is not expected to win.

## 6. Deterministic corpus generation

### 6.1 PRNG and seed namespaces

All pseudorandom streams use SplitMix64 with unsigned arithmetic modulo `2^64`:

```text
state += 0x9E3779B97F4A7C15
z = state
z = (z xor (z >> 30)) * 0xBF58476D1CE4E5B9
z = (z xor (z >> 27)) * 0x94D049BB133111EB
return z xor (z >> 31)
```

Bounded sampling uses rejection, not `%` bias: for bound `b`, set `threshold = (-b mod 2^64) mod b`, reject draws below `threshold`, and return `draw mod b`.

Seed namespaces are:

| Purpose | 64-bit master seed |
|---|---:|
| correctness/property generation | `0x434F525230303031` (`CORR0001`) |
| selector training/discovery | `0x4255494C44303031` (`BUILD001`) |
| frozen confirmation | `0x434F4E4630303031` (`CONF0001`) |
| timing order | `0x54494D4530303031` (`TIME0001`) |

For every stream, compute FNV-1a-64 over the UTF-8 canonical cell ID using offset `14695981039346656037` and prime `1099511628211`; initialize SplitMix64 with `master_seed xor fnv1a64(cell_id)`. The semantic-trace ID is:

```text
<partition>/<workload_id>/L=<L>/source=<source>/replicate=<0..7>/variant=<variant>
```

`variant=COMMON` for every policy-independent workload. It is `K=<K>` only for the explicitly capacity-relative `T040`, the bank-specific certified-ingress cells, and the occupancy diagnostic. Representation ID, bank policy, and K never enter a common-trace seed. Thus all ordinary cross-policy and cross-K comparisons replay byte-identical semantic traces.

Every derived seed is emitted in hexadecimal and decimal. No platform `Random`, process hash, current time, thread scheduling, or unordered collection may influence data.

Training uses replicates `0..3`. Confirmation uses eight disjoint replicates `0..7` under the confirmation master. Timing order uses its own namespace and never changes semantic inputs.

### 6.2 Source modes

Every compatible main workload is generated in both source modes:

| Source ID | External input | Allowed implementation knowledge | Required charge |
|---|---|---|---|
| `MAG` | Canonical sign and exact binary magnitude only | No hidden factors | Binary parse; hybrid selected-prime extraction; complete sparse factorization; dense/sparse basis encoding or escape |
| `FACT` | Canonical sign and complete sorted prime/exponent list from a structured producer | Named factors only after structural/certificate validation | Validation; binary reconstruction; hybrid bank filtering/cofactor construction; sparse storage |
| `BANK_CERT` | Bank/version-specific sign, valuation lanes, exact cofactor, and trusted upstream provenance receipt | Exactly the supplied bank valuations and cofactor; no cofactor factorization | Tag/version/range/coprimality/provenance checks; binary reconstruction if demanded; representation storage |

`BANK_CERT` is measured only in the cold-path crossover suite because its source representation changes with bank membership. It is compared only within the same bank/version and is not used for cross-policy headlines. The trusted receipt makes the supplied components the source semantics; checking it never grants hidden factors of the cofactor.

Validation work may be reused by the loading implementation and is charged once. For example, if `FACT` product validation constructs a magnitude, `BIN_EXACT` may retain that product rather than reconstructing it twice; the receipt still attributes the work to ingress.

### 6.3 Input families

The corpus generator records both semantic values and an oracle-only construction witness. Implementations receive only the declared source mode.

| Family | Deterministic construction | Purpose |
|---|---|---|
| `SMOOTH_FIRST32` | Sample support from the first 32 primes and exponents `0..min(7, L-1)`, rejecting out-of-range products | Favor first-prime banks |
| `SMOOTH_SPARSE` | Support sizes cycle through `0, 1, 2, 4, 8, 16, 32`, capped only by range; factors are sampled without replacement from `C256` | Dense/sparse crossover on a policy-independent corpus |
| `SELECTED_SKEW` | Integer rank weight `floor(2^32/r)` over a fixed permutation of `C256` derived only from the training namespace | Favor non-prefix selection without confirmation tuning or floating-point drift |
| `OUTSIDE_CATALOG` | Small smooth part times a fixed prime outside `C256` | Exact cofactor and basis escape |
| `LARGE_COFACTOR` | Small first-four-prime smooth part times `q_L` below | Cofactor-dominated values |
| `BALANCED_PRIME_SQUARE` | `m_L^2` below | Full-factor ingress stress with a known exact factor witness |
| `CONSECUTIVE` | Deterministic `n, n+1` pairs within range | Additive disruption and coprimality |
| `SHARED_GCD` | `g*u, g*v` with controlled bank-smooth `g` and coprime residuals | Common-valuation preservation |
| `PRIME_POWER` | `p^e` at `e in {0, 1, max-1, max}` where range permits | Lane boundaries and repeated scaling |
| `SIGNED_ZERO` | Cycle `0, 1, -1`, positive/negative smooth and rough values | Sign/zero contract |

The `SELECTED_SKEW` permutation seed uses the canonical ID `TRAINING/SELECTED_SKEW_PERM/L=<L>/source=<source>/replicate=0/variant=COMMON`; that frozen permutation is then shared by training and confirmation generation at the same `(L, source)`.

Fixed large primes are:

| `L` | `q_L` | Verification requirement | `m_L` for square control |
|---:|---|---|---|
| 16 | `2^13 - 1` | Lucas-Lehmer certificate | `2^7 - 1` |
| 32 | `2^31 - 1` | Lucas-Lehmer certificate | `2^13 - 1` |
| 64 | `2^61 - 1` | Lucas-Lehmer certificate | `2^31 - 1` |
| 128 | `2^127 - 1` | Lucas-Lehmer certificate | `2^61 - 1` |
| 256 | `2^255 - 19` | checked deterministic certificate stored with corpus | `2^127 - 1` |

The certificate verifier and certificate bytes are hashed and charged where an implementation consumes them. Knowledge used by the oracle to build a case is not an ingress discount.

### 6.4 Mandatory edge corpus

Every representation and policy must also receive:

- zero, `+1`, `-1`, and the all-zero exponent vector;
- largest in-range magnitude and both signs;
- maximum legal exponent and one-step exponent overflow;
- largest bank prime, first prime outside the bank, largest `C256` prime, and first prime outside `C256`;
- cofactor 1, a prime cofactor, a composite cofactor, and a cofactor improperly divisible by a bank prime;
- sparse support `0`, `1`, `K/2`, and `K`;
- duplicate, descending, zero-exponent, truncated, wrong-version, stale, and invalid encodings;
- exact and non-exact division;
- products that fit exponent lanes but exceed the magnitude range;
- smooth operands whose sum is outside the bank or prime;
- a one-shot magnitude-native multiply and a 4,096-query resident case.

Malformed inputs are correctness-only and never timed as successful operations.

## 7. Frozen workload matrix

All main workload rows run at every functional width, both source modes, all eight confirmation replicates, and every compatible representation/policy, except that the explicitly magnitude-ingress `T020` is `MAG` only and `T040` has four K-relative semantic variants. This freezes 1,800 main confirmation trace instances before representation replay: 18 common workload IDs x 5 widths x 2 sources x 8 replicates, plus `T020` x 5 x 1 x 8, plus four `T040` variants x 5 x 2 x 8. The operation mix is based on an unbiased draw in `[0, 99]` unless a deterministic schedule is stated. Source registers are chosen uniformly; destination registers are chosen independently. Operand generation and checked failure statuses are fixed in the semantic trace before replay.

Common parameter draws are also frozen. A query prime is selected 50% uniformly from `C256`, 25% from the oracle factors of the queried value, and 25% from `{first prime after C256, q_L}`; a missing factor branch falls back to the first category. Successful exact-divide cases choose a positive divisor from the source's oracle factors. Failed cases make at most 64 seeded divisor draws, then deterministically use the smallest prime not dividing the source. These oracle choices define semantic inputs but reveal no hidden factor witness to a `MAG` implementation. Power exponents are uniform in `0..4`. A generated mathematical overflow or non-exact result is retained with its checked status rather than resampled.

### 7.1 Multiplicative/factor-resident

| ID | Inputs and schedule | Operation mix | Required outputs |
|---|---|---|---|
| `M010_RESIDENT` | `SMOOTH_FIRST32`, `SMOOTH_SPARSE`, and `SELECTED_SKEW` cycle by renewal epoch | 35% multiply, 15% small-prime scale via multiply, 15% prepared successful exact divide, 10% prepared non-divisor, 10% power with exponent `0..4`, 5% GCD, 5% LCM, 5% divides | Standard forcing; every divide status |
| `M020_CANCEL` | Construct numerator/denominator products from the same factor pool; cancel in a seeded permutation | 45% multiply, 35% successful exact divide, 10% failed exact divide, 10% GCD | Reduced numerator/denominator magnitudes every 256 operations and at end |
| `M030_FACTORIAL` | For each `L`, all `n!` prefixes through the largest in-range `n`; repeat as left fold and balanced product tree | Deterministic multiply sequence | Every final factorial magnitude and canonical serialization |
| `M040_BINOMIAL` | Exact product ratios for all `C(n,k)` whose result is in range, in seeded `(n,k)` order | Numerator multiplication followed by GCD-guided exact cancellation | Exact coefficient and final denominator `1` |

`M030` and `M040` have variable deterministic event counts recorded in the trace manifest rather than padded with no-ops.

### 7.2 Divisibility-heavy

| ID | Inputs | Operation mix | Required outputs |
|---|---|---|---|
| `D010_QUERY` | Equal thirds smooth, outside-catalog, and shared-GCD values | 30% divides, 25% valuation, 15% factor containment, 10% divides-by-prime-power, 10% GCD, 10% LCM | Every predicate, valuation, GCD, and LCM result |
| `D020_RATIONAL` | Numerator/denominator pairs with controlled shared factors and opaque cofactors | 40% GCD, 30% exact cancellation, 20% divides, 10% valuation | Canonical reduced pair every 128 operations |
| `D030_REUSE` | Fixed 32-value corpus; no renewal after initial load | Deterministic 4,096-query seeded permutation | Every query result; exact magnitudes only at end |

`D030_REUSE` is the deliberately favorable long-residency case. It may not be presented without `D010` and the cold crossover suite.

### 7.3 Addition-heavy

| ID | Inputs and schedule | Operation mix | Required outputs |
|---|---|---|---|
| `A010_ACCUMULATE` | One accumulator plus seeded signed operands; accumulator reloads only at standard renewal points | 85% add-to-accumulator, 10% subtract, 5% increment by one | Accumulator magnitude every 64 operations; all final serializations |
| `A020_RANDOM_SUM` | Equal thirds smooth, consecutive, and shared-GCD pairs | 70% add, 20% subtract, 10% compare | Every comparison; standard forcing |
| `A030_ALTERNATE` | Smooth factors arranged so multiplication usually fits after renewal | Exactly alternating add and multiply | Every result magnitude every 32 operations |
| `A040_COMMON_FACTOR` | `a=g*u`, `b=g*v` with `g` selected-bank smooth and controlled `u,v` | 60% add, 20% subtract, 10% GCD, 10% valuation | Preserved common-factor claims and exact sums |

For every add/subtract, record lanes provably preserved without division, lanes invalidated, lanes refreshed, remainder tests performed, and whether a later consumer forced unknown state. Common-factor preservation is not credited as a complete factorization of the sum.

### 7.4 Mixed persistence

| ID | Multiplicative share | Additive share | Multiplicative submix | Additive submix |
|---|---:|---:|---|---|
| `X010_M90_A10` | 90% | 10% | 45% multiply, 15% exact divide, 15% divides, 10% GCD, 10% LCM, 5% valuation | 70% add, 20% subtract, 10% increment |
| `X020_M50_A50` | 50% | 50% | same | same |
| `X030_M10_A90` | 10% | 90% | same | same |

Each mixed trace uses fan-out: 50% of source choices come from the eight most recently written registers and 50% from all 32 registers. This makes persistence and reuse explicit without looking at representation state.

### 7.5 Adversarial and bank-thrashing

| ID | Deterministic attack | Comparison rule |
|---|---|---|
| `T010_OUTSIDE` | Values are `LARGE_COFACTOR` or `OUTSIDE_CATALOG`; 40% multiply, 30% divisibility/GCD queries, 30% add/subtract | Run all policies; outside primes remain cofactors |
| `T020_INGRESS` | 1,024 one-shot `MAG` loads drawn equally from large primes, balanced prime squares, large-cofactor composites, and consecutive rough values; exactly one demanded operation/output per load | No resident-cost-only report; full sparse timeouts preserved |
| `T030_THRASH33` | Cycle explicit references through `C256` slots 33 through 65: valuation, scale, exact cancel, then next prime | Same semantic stream for all `K`; compares capacity response |
| `T040_THRASH_K1` | For each `K`, cycle through a fixed set of `K+1` primes beginning at slot 97 | Compare policies only within the same `K`; never compare different `K` traces as if identical |
| `T050_ADD_INVALIDATE` | Alternate smooth additions chosen to make rough/consecutive sums with valuation queries immediately after each addition | Measures eager refresh versus explicit unknown/stale debt |
| `T060_PHASE_SHIFT` | Four 1,024-operation phases favor `C256` slots `1..32`, `33..64`, `65..96`, and `97..128`, respectively | Measures selected-bank staleness and adaptive migration |

No adaptive policy is told phase boundaries or future prime windows.

## 8. Crossover and ablation suites

### 8.1 Reuse/forcing sweep

For each `L`, source mode, `K`, and bank policy, run the same operand pair or resident corpus for

```text
Q in {1, 2, 4, 8, 16, 32, 64, 128, 256, 1024, 4096}
```

on:

- repeated multiply/scale;
- exact cancellation;
- divisibility and valuation queries;
- GCD/LCM service;
- add followed by valuation demand;
- mixed 90:10 and 50:50 traces.

Output modes are separate cells:

```text
PREDICATE_ONLY
STRUCTURAL_FINAL
MAGNITUDE_FINAL
MAGNITUDE_EVERY_OPERATION
CANONICAL_SERIALIZATION_FINAL
```

No factor result is compared to a demanded magnitude result. `STRUCTURAL_FINAL` is comparable only where the consumer explicitly accepts that form.

### 8.2 Bank-size and occupancy sweep

For `K in {4,8,16,32}`, sweep observed support occupancy `0`, `1`, `K/4`, `K/2`, `3K/4`, and `K` where integral, plus one outside-bank prime. Report lane utilization, payload, metadata, catalog share, hit rate, migration, and cofactor growth. The sparse support sweep uses the same semantic values as the dense/hybrid sweep.

### 8.3 Freshness ablation

If `HYBRID_LAZY` exists, replay `A010`, `A040`, `X020`, `T050`, and the forcing sweep with:

```text
EAGER_CANONICAL
LAZY_PER_LANE
LAZY_WHOLE_BANK
NO_BANK
```

All final observables are forced. State bits, invalidations, deferred nodes/records, refreshes, and stale-read traps are charged. A lazy form that only avoids work because the output is never demanded does not pass the ablation.

### 8.4 Structural subset

At `L in {8,16,32,64}`, compare two declared budget points where implemented:

- `SEQUENTIAL`: reused exponent/cofactor arithmetic with cycles and DFFs charged;
- `PARALLEL`: lane-parallel exponent arithmetic and a throughput-oriented binary unit.

Reuse Build 000 NAND components only under their exact contracts. Record NAND2 count, DFF count, critical depth, cycles, initiation interval, NAND evaluations, transitions, port width, and peak live bits. If cofactor work or control is not structurally realized, the aggregate cell is `NOT_STRUCTURALLY_REALIZED`; host arithmetic counts cannot be added to NAND counts.

## 9. Correctness and independent oracles

### 9.1 Exhaustive small domains

Before performance runs:

1. For every signed `x` in `[-64,64]`, test encode, decode, canonical serialization, equality, and every bank policy whose membership is fixed for the case.
2. For every ordered pair in `[-64,64]^2`, test multiply, checked exact divide, add, subtract, GCD, LCM, divides, compare, and all valuation queries for the first eight primes.
3. Enumerate every four-lane exponent tuple with exponent `0..3`, both signs, zero, legal cofactors `1..64`, and malformed cofactor/bank combinations.
4. Enumerate all raw encodings through 16 bits for the smallest serialization profile and classify each as canonical, noncanonical, malformed, or truncated.
5. Exhaust every one-step magnitude/exponent overflow, basis escape, sign/zero, wrong-version, unknown/stale, and failed-destination case.

Pure coordinate forms are expected to return `BASIS_ESCAPE` outside their declared domain. The applicability oracle checks that representation-specific status; those cases are not removed.

### 9.2 Randomized differential sequences

For every important `(L, representation, policy, source)` correctness cell, execute at least 10,000 generated operation cases and 256 mixed sequences of length 64 using the correctness namespace. Check after every instruction, not only final output. Shrink and preserve every counterexample with the exact seed, trace prefix, serialized operands, expected/actual status, and implementation hash.

The independent semantic oracle is arbitrary-precision signed integer arithmetic plus an independently implemented slow valuation/factor specification. It may generate expected answers and traces but may not be called inside a costed implementation operation.

### 9.3 Representation checks

For every successful result:

- reconstruction equals the oracle integer;
- eager bank exponents equal independent `v_p(abs(x))` for every lane;
- eager cofactor is positive and coprime to every bank prime;
- sign/zero/identity are canonical;
- sparse keys are unique, ascending, positive, and complete for `SPARSE_FULL`;
- catalog identity/version matches;
- freshness and provenance transitions obey the published state machine;
- serialization round-trips byte-for-byte;
- source operands remain unchanged unless the IR explicitly writes them.

Malformed representations must never reach arithmetic as valid values.

### 9.4 Correctness gate

A representation/policy cannot enter confirmation timing until all applicable exhaustive and randomized checks pass. Arithmetic mismatch, invalid-state acceptance, silent truncation, stale-result reuse, or oracle access is `WRONG_RESULT` and invalidates every affected performance cell. Raw failures remain evidence.

## 10. Cost vector

No result is reduced to a universal scalar cost. Accounting contains five separate phases:

```text
setup
ingress
native
maintenance
egress
```

`setup` includes catalog construction/validation, selected-bank training and selection, JIT/warmup only where explicitly reported, and fixed precomputation. It is emitted once per setup scope and referenced by operation rows; it is never copied into every event. For additive counters, headline total cost is:

```text
total = setup_at_declared_amortization
      + ingress
      + native
      + maintenance
      + egress
```

Maximum fields aggregate by maximum, not addition. Static resources are reported once per implementation/configuration and are never repeated per operation or phase.

Phase attribution is fixed as follows:

- `setup`: shared catalog/policy construction and offline selection before an external value is loaded;
- `ingress`: parsing, validating, extracting, factoring, or reconstructing an external `LOAD` into resident state;
- `native`: work required to compute the requested semantic operation from resident exact operands, including an explicit binary-magnitude step when the operation crosses domains;
- `maintenance`: canonicalization, cofactor normalization, freshness changes, refresh, invalidation, bank replacement, and migration needed to leave legal resident state;
- `egress`: work performed only because the consumer demanded a predicate, magnitude, or serialization.

Each primitive event is assigned once to its causal phase. A reconstruction performed to execute addition is `native`; the same reconstruction performed only for `FORCE_MAGNITUDE` is `egress`. Post-add valuation extraction is `maintenance`.

### 10.1 Fixed `CostVector/v1`

Each phase uses every field below. Operation rows carry ingress/native/maintenance/egress vectors; a separate setup row carries the same vector. Integer counters are unsigned 64-bit decimal strings in JSON to avoid parser-width loss. Zero means measured zero. `null` is legal only when the associated measurement state is `NOT_MEASURED` or `NOT_STRUCTURALLY_REALIZED`.

| Group | Exact fields |
|---|---|
| Structural dynamic | `critical_depth_max`, `modeled_cycles`, `nand_evaluations`, `net_transitions`, `peak_live_bits` |
| Exponent/bank | `lane_reads`, `lane_writes`, `exponent_adds`, `exponent_subtracts`, `exponent_compares`, `exponent_minmax`, `overflow_reductions`, `bank_hits`, `bank_misses`, `bank_installs`, `bank_evictions`, `bank_migrations`, `bank_version_writes` |
| Binary arithmetic | `binary_adds`, `binary_subtracts`, `binary_multiplies`, `binary_divrem_calls`, `binary_gcd_calls`, `binary_compares`, `binary_shifts`, `operand_bits_sum`, `result_bits_sum` |
| Extraction/factor work | `prime_remainder_tests`, `factor_divisions`, `full_factorization_calls`, `factorization_iterations`, `primality_tests`, `modular_multiplies`, `modular_exponentiations`, `certificate_verifications`, `reconstruction_multiplies`, `normalization_calls`, `refresh_calls`, `invalidation_calls` |
| Sparse/control | `sparse_key_compares`, `sparse_entry_reads`, `sparse_entry_writes`, `branches`, `status_reductions`, `destination_writes` |
| Memory/serialization | `logical_bit_reads`, `logical_bit_writes`, `managed_bytes_read`, `managed_bytes_written`, `allocated_bytes`, `serialized_bits_read`, `serialized_bits_written` |

Bit-length-weighted operand detail is not inferred from `operand_bits_sum`. Every variable-width binary arithmetic call also emits an `ArithmeticEvent/v1` child row with operation, signedness, operand bit lengths, result bit length, algorithm ID, and phase. This permits later declared work proxies without editing raw receipts.

The additive fields sum within and across phases. `critical_depth_max` and `peak_live_bits` aggregate by maximum. Static structural resources use a separate `StructuralResource/v1` record with `nand2_static`, `dff_static`, `connections_static`, `max_fanout`, `port_bits`, `initiation_interval`, configuration hash, and elaboration status.

### 10.2 Storage vector

Every load, bank migration, 256-operation forcing point, and trace end emits:

```text
logical_payload_bits
logical_metadata_bits
logical_catalog_bits
logical_policy_bits
logical_certificate_bits
provisioned_value_bits
managed_live_bytes
managed_capacity_bytes
peak_managed_live_bytes
live_value_count
cofactor_bits_sum
nonzero_lane_count
unknown_lane_count
stale_lane_count
```

Payload and metadata may not be combined in raw data. Catalog bits are reported once per machine and under each registered amortization count.

### 10.3 Host timing protocol

Host time is a secondary software measurement, not a hardware claim. After the correctness gate:

- pinned Release build and dependency lock files are used;
- each cell has three untimed warmup repetitions and eleven measured repetitions;
- each repetition starts from identical serialized initial state and replays the same trace once;
- implementation order is a timing-seed permutation, fixed before measurements;
- assertions, receipt serialization, and logging are outside the timed region for every implementation;
- allocation counters and elapsed nanoseconds are recorded per repetition;
- GC mode, CPU/OS/runtime, process affinity and power policy when controllable, and concurrent-load note are recorded;
- no sample is trimmed. Median, MAD, minimum, maximum, and all raw samples are published.

If `MAD / median > 0.10`, the cell is marked `UNSTABLE_TIMING`; its exact logical receipts remain usable, but it cannot support a host-time advantage claim without a complete rerun.

Per-cell resource limits are 120 wall-clock seconds and 2 GiB additional managed allocation for a functional trace, and 30 seconds and 1 GiB for a structural elaboration/simulation. Censored cells remain in tables and plots.

## 11. Exact evidence schemas

All generated Build 001 evidence lives under `results/build001/`. Raw files are append-only within a run; summaries and figures are regenerated solely from raw files.

### 11.1 `preregistration.json`

Required fields:

```text
schema_version:string = "build001.preregistration.v1"
created_utc:string (ISO-8601 UTC)
plan_path:string
plan_sha256:string
controlling_brief_sha256:string
build000_manifest_sha256:string
seed_namespaces:object
frozen_matrix_sha256:string
amendments:array
```

### 11.2 `manifest.json`

Required fields:

```text
schema_version:string = "build001.manifest.v1"
run_id:string
run_kind: TRAINING | CONFIRMATION | EXPLORATORY | CORRECTNESS | TIMING
started_utc:string
finished_utc:string
git_commit:string
dirty_worktree:boolean
command_argv:string[]
sdk_runtime_os_cpu_memory:object
dependency_lock_sha256:object
source_assembly_sha256:object
implementation_ids:string[]
cost_model_version:string
catalog_sha256:string
matrix_sha256:string
trace_index_sha256:string
raw_file_sha256:object
result_status: COMPLETE | PARTIAL | FAILED_INFRASTRUCTURE
claim_ceiling:string
sanitization_status:string
```

Machine/user names, home paths, raw TRX identities, tokens, and environment secrets are prohibited from public receipts.

### 11.3 `traces/index.json` and `traces/<trace_id>.jsonl`

Index rows contain `trace_id`, partition, workload ID, `L`, source mode, replicate, derived seed, event count, output obligations, and SHA-256.

Each trace event is exactly:

```text
schema_version:string = "build001.trace_event.v1"
trace_id:string
event_index:uint64
opcode:enum from section 3.3
destination:int|null
sources:int[]
immediates:object
source_provenance: MAG | FACT | BANK_CERT | RESIDENT
semantic_expected_status:enum from section 3.2, excluding representation-only escape
expected_value_sha256:string|null
expected_scalar:string|null
output_obligation:NONE | PREDICATE | STRUCTURAL | MAGNITUDE | SERIALIZATION
```

Raw oracle values may be stored in the private generator artifact for debugging, but confirmation result rows use hashes and bounded non-sensitive corpus values only.

### 11.4 `raw/operation_receipts.jsonl`

Each row is:

```text
schema_version:string = "build001.operation_receipt.v1"
run_id:string
trace_id:string
event_index:uint64
implementation_id:string
representation_id:string
policy_id:string
K:uint32|null
bank_sha256:string|null
source_mode:MAG | FACT | BANK_CERT | RESIDENT
output_obligation:enum
opcode:enum
semantic_expected_status:enum
applicability:IN_DOMAIN | EXPECTED_BASIS_ESCAPE | NOT_SUPPORTED
expected_actual_status:enum
actual_status:enum
correct:boolean
destination_written:boolean
result_sha256:string|null
value_state_before:enum
value_state_after:enum
execution_path:BANK_LOCAL | COFACTOR_BINARY | MAGNITUDE_BINARY | PARTIAL_REFRESH | FULL_FACTORIZATION | MIXED | REJECTED
setup_receipt_id:string
ingress:CostVector/v1
native:CostVector/v1
maintenance:CostVector/v1
egress:CostVector/v1
measurement_state:MEASURED | NOT_MEASURED | NOT_STRUCTURALLY_REALIZED | RESOURCE_LIMIT | FAILED_INFRASTRUCTURE
first_error_id:string|null
```

### 11.5 Other raw receipts

- `raw/setup_receipts.jsonl`: setup receipt ID and scope, implementation/configuration IDs, catalog/policy/source context, amortization eligibility, one `CostVector/v1`, and measurement state.
- `raw/arithmetic_events.jsonl`: `run_id`, trace/event/implementation IDs, phase, operation, algorithm ID, signedness, operand bit-length array, result bit length, exact/inexact flag.
- `raw/structural_resources.jsonl`: implementation/configuration IDs, `L`, policy/K, budget point, configuration hash, `nand2_static`, `dff_static`, `connections_static`, `max_fanout`, `port_bits`, `initiation_interval`, and elaboration status.
- `raw/storage_snapshots.jsonl`: trace/event/implementation IDs plus every storage field in section 10.2.
- `raw/bank_events.jsonl`: event ID, old/new membership hashes and explicit members, referenced prime, hit/miss, victim, reason, live values scanned, lane/cofactor work, bank version before/after.
- `raw/correctness_cases.jsonl`: case ID, seed, serialized input hashes, expected/actual status and result hashes, representation invariants, pass flag, counterexample path.
- `raw/timing_samples.csv`: `run_id,trace_id,implementation_id,policy_id,K,repetition,order_index,elapsed_ns,allocated_bytes,gc0,gc1,gc2,measurement_status`.
- `bank_selections.json`: training manifest/trace hashes, `(family,L,source,K)`, all 256 scores, selected ordered primes, ties, selection cost vector, selector hash.

### 11.6 Derived receipts

- `summary/trace_totals.csv`: one row per trace/implementation with all phase totals, storage maxima, correctness, censoring, and timing summaries.
- `summary/crossovers.csv`: workload, width, source/output mode, baseline, candidate, policy/K, metric/profile, tested `Q`, first sustained crossover or explicit no-crossover status.
- `summary/pareto.csv`: resource profile, comparison set, point, every coordinate, dominance status, and dominators.
- `summary/failure_matrix.csv`: every planned cell including `OK`, `WRONG_RESULT`, `BASIS_ESCAPE`, `NOT_SUPPORTED`, `RESOURCE_LIMIT`, `UNSTABLE_TIMING`, and `FAILED_INFRASTRUCTURE`.

Every derived row includes the raw manifest and input-file hashes. Generation scripts must fail on duplicate primary keys, missing planned cells, unknown enums, nullable measured counters, hash mismatch, or a correctness-failed row entering a performance aggregate.

## 12. Analysis locked before results

### 12.1 Paired summaries

Comparisons are paired on the identical semantic trace. Publish all eight confirmation replicates, median, minimum, maximum, and paired ratios. The eight generated replicates are not eight real-world workloads, and timing repetitions are not independent workload samples. No p-value or population-generalization claim is planned.

### 12.2 Crossover rule

For every scalar metric within one resource profile, the empirical sustained crossover is the smallest registered `Q` for which the candidate is better than the baseline at that `Q` and every larger measured `Q`, with the paired direction holding in at least seven of eight confirmation traces. There is no interpolation between registered `Q` values.

If no such point exists through 4,096, emit `NO_CROSSOVER_WITHIN_BOUND`. If the candidate is already better at `Q=1`, emit `CROSSOVER_AT_OR_BELOW_1`. If a denominator is zero, publish absolute values and `RATIO_UNDEFINED`.

An analytic linear estimate

```text
Q* = (candidate_setup - baseline_setup) /
     (baseline_resident_per_op - candidate_resident_per_op)
```

may be shown only when per-operation cost is demonstrably constant over the fitted kernel. It is labeled `MODEL_DERIVED`, shown beside the empirical sweep, and never substituted for it.

### 12.3 Pareto profiles

Dominance is evaluated only within a complete profile whose coordinates are all lower-is-better:

| Profile | Coordinates |
|---|---|
| `MANAGED` | median total elapsed ns, allocated bytes, peak managed live bytes |
| `STRUCTURAL_SEQUENTIAL` | NAND2 static, DFF static, modeled cycles, NAND evaluations, peak live bits |
| `STRUCTURAL_PARALLEL` | NAND2 static, DFF static, critical depth, initiation interval, peak live bits |
| `LOGICAL_STORAGE` | logical payload bits, metadata bits, unamortized catalog/policy bits |

Point A dominates B only if A is no worse in every measured coordinate and strictly better in at least one. A missing or structurally unrealized coordinate prevents dominance, rather than being treated as zero. Exact operation-type counters are reported as a vector and are not added using post hoc weights.

### 12.4 Bank analysis

For every policy and K, report:

- occupancy and nonzero-lane distribution;
- cofactor size distribution;
- hit, miss, install, eviction, and migration counts;
- saved refresh/extraction work versus policy overhead;
- catalog plus policy bits;
- cumulative cost by trace index;
- fixed phase-shift recovery lag;
- training/setup amortization for selected banks.

`T040_THRASH_K1` is capacity-relative and cannot establish that one K is globally better than another. Cross-K conclusions use only identical traces such as `T030_THRASH33`.

### 12.5 Addition analysis

For every add/subtract, publish:

- exact common valuation lower bound `min(v_p(a),v_p(b))` for known lanes;
- count of lanes preserved, refreshed, marked unknown/stale, or invalid;
- cofactor/magnitude arithmetic and trial divisions;
- time until each retained fact is next consumed;
- cost forced at the next predicate, magnitude, or serialization demand;
- final exactness and canonicality.

No support-distance statistic is a hardness theorem. Preserving a common factor is not credited as knowing the full output factorization.

## 13. Null, success, and failure criteria

### 13.1 Family nulls

The null is retained separately for multiplicative, divisibility, addition, mixed, and adversarial families unless the hybrid meets the criterion below. No pooled average may let a favorable synthetic family erase a hostile one.

A **bounded specialized advantage** in a family requires all of:

1. zero correctness or invariant failures in every cited cell;
2. all source, setup, ingress, maintenance, demanded egress, payload, metadata, and catalog costs included;
3. a complete `MANAGED` or structural Pareto profile on the same semantic trace and output obligation;
4. at least 20% improvement over the best correct, supported nonhybrid baseline in the profile's primary work coordinate (elapsed time for `MANAGED`, NAND evaluations for sequential structural, critical depth for parallel structural);
5. no more than 2x regression in any other coordinate in that profile;
6. the direction holds for at least seven of eight confirmation replicates at two or more widths;
7. the result survives `MAGNITUDE_FINAL` or the workload's stronger registered output obligation;
8. the result is not solely `D030_REUSE`, `T040_THRASH_K1`, a malformed-input path, or an unsupported-baseline omission.

The 20% and 2x thresholds are preregistered engineering relevance filters, not statistical or physical constants. All smaller effects remain reported without being called a decision-gate advantage.

### 13.2 Build 001 decision labels

- `NO_USEFUL_ADVANTAGE`: no hybrid/new candidate satisfies the specialized criterion in two distinct nontrivial workload families after costs and outputs are charged, or representational complexity/correctness prevents a complete comparison.
- `SPECIALIZED_ADVANTAGE`: the criterion holds in at least two distinct nontrivial families, including at least one of multiplicative or divisibility and at least one `MAG` or fully charged `FACT` source cell.
- `GENERAL_REPRESENTATION_CANDIDATE`: in addition to specialized advantage, the criterion holds in multiplicative, divisibility, addition, mixed, and adversarial families; at least four functional widths; both source modes; and no bank policy is dominated by `BIN_EXACT` across all registered mixed/adversarial profiles. Host-only timing cannot earn this label without a complete abstract structural or equally transparent operation-level profile.
- `NEW_DIRECTION`: an exploratory architecture may receive this label only after an amended, newly seeded confirmation manifest applies the same correctness, cost, source/output, and two-family criteria. Discovery output alone cannot earn it.

The strongest available label is not the target. Negative results and explicit no-crossovers complete the experiment.

### 13.3 Invalid and censored evidence

The following invalidate an affected cost/performance claim:

- arithmetic mismatch, malformed-state acceptance, silent overflow/truncation, stale-result reuse, or noncanonical successful output;
- hidden access to oracle factors or future trace events;
- missing factorization, certificate, catalog, bank-migration, invalidation, refresh, or output cost;
- different semantic trace, source representation, output obligation, or range between compared points;
- mixing host primitive counts with NAND counts into one total;
- confirmation tuning or post hoc removal of unfavorable seeds;
- missing raw row, duplicate key, hash mismatch, or regenerated summary not traceable to raw evidence.

Timeout, memory limit, unsupported operation, and infrastructure failure remain visible and are never imputed. A full-factor implementation timing out on a 256-bit hostile input is evidence about that bounded implementation and budget, not a factorization complexity theorem.

## 14. Planned figures

Every figure is generated as SVG from hashed raw/summary inputs, embeds the generating command and input SHA-256 values in metadata, includes absolute values as well as ratios, uses accessible colors plus shape/line-style distinctions, and shows censored/unsupported cells explicitly.

1. `phase_costs.svg`: setup, ingress, native, maintenance, and egress as faceted absolute bars by workload/source/representation/K.
2. `pareto_managed.svg`: elapsed time versus peak resident bytes, point size for allocation, faceted by workload and width; raw replicate points behind medians.
3. `pareto_structural.svg`: area/depth or area/cycles fronts, split into sequential and parallel budget points.
4. `bank_policy_heatmap.svg`: K/policy by workload with occupancy, hit rate, cofactor bits, and total migration work; values printed in cells.
5. `cumulative_crossing.svg`: cumulative paired cost against operation index and registered Q, including ingress and forcing events; no smoothed interpolation.
6. `crossover_grid.svg`: empirical first sustained crossover or explicit no-crossover for every workload/source/output/K cell.
7. `bank_timeline.svg`: membership, hit/miss, install/evict, and migration work for `T030`, `T040`, and `T060`.
8. `addition_freshness.svg`: lanes preserved/unknown/refreshed and deferred cost versus time-to-next-use for `A010`, `A040`, `X020`, and `T050`.
9. `cofactor_growth.svg`: cofactor bit-length distributions by policy and phase, with exact magnitude cap marked.
10. `failure_matrix.svg`: every planned cell and its correctness/support/resource/timing status, including unfavorable and censored cases.
11. `storage_breakdown.svg`: payload, metadata, catalog/policy, certificate, capacity, and actual managed bytes at registered live-value amortizations.
12. `timing_dispersion.svg`: all eleven raw timing samples, median, and MAD; unstable cells visibly flagged.

Figures never establish claims absent from their source receipts.

## 15. Comparisons that are ill-posed unless split

The following must not appear as single headline comparisons:

1. **Pure dense/sparse coordinates versus arbitrary exact integers.** Build 000 fixed-basis forms cannot represent an outside-bank factor. Their `BASIS_ESCAPE` is reported. Adding a cofactor changes the candidate to the hybrid.
2. **Adaptive pure dense/sparse coordinates.** Eviction cannot preserve arbitrary exact values without another domain. A migrating exact cofactor is hybrid state.
3. **Magnitude-native versus factor-native ingress.** Factor data is not free for `MAG`; reconstruction is not free for `FACT`. Results are split by source.
4. **Full sparse factors with an uncharged certificate.** `SPARSE_FULL` must factor magnitude input or validate supplied factor input.
5. **Predicate output versus magnitude output.** A divisibility bit is not an exact integer result. Output obligation is a matrix dimension.
6. **Variable-size hybrid versus fixed-width binary storage.** Report both logical occupied bits and provisioned capacity for both.
7. **Gate model versus optimized host arithmetic.** Structural and managed profiles remain separate.
8. **Parallel lane bank versus one reused binary ALU.** Sequential and throughput-oriented budget points are separate.
9. **Operation-count categories as one unit.** A division, lane read, NAND, branch, and allocated byte have no preregistered universal exchange rate.
10. **Unamortized versus silently amortized catalog/selection.** Every amortization divisor is declared and both absolute/setup-separated values are shown.
11. **Workload-selected bank on its training corpus.** Only disjoint confirmation traces support performance conclusions.
12. **Adaptive discovery of hidden factors.** An unbanked factor in a magnitude is not observable without a charged computation.
13. **Cross-K comparison on `K+1` thrashing traces.** Those semantic streams differ. Only same-K policy comparisons are valid there.
14. **Lazy unknown versus canonical zero exponent.** Unknown/stale requires an explicit state and forced consumer; it cannot be treated as zero.
15. **Opaque composite cofactor as a factor atom.** That would falsely convert unknown internal structure into verified prime identity.
16. **Managed object bytes versus logical payload lower bounds.** Both are useful and neither substitutes for the other.
17. **A first-prime win as a prime-specific theorem.** It may be a workload skew or factorized-symbolic locality result. The selected/outside/no-bank controls remain necessary.

## 16. Reproduction workflow to implement

The eventual Build 001 tooling must expose these stable tasks, retaining the pinned .NET SDK unless a separately justified tool is added:

```text
verify-build000-unchanged
verify-build001-correctness
generate-build001-training
select-build001-banks
freeze-build001-manifest
generate-build001-confirmation-traces
run-build001-confirmation
run-build001-timing
summarize-build001
verify-build001-evidence
```

The verifier must, in this order:

1. hash-check all preserved `results/build000/` artifacts against their settled manifest;
2. restore locked dependencies and build Release with warnings as errors;
3. pass exhaustive, randomized, malformed, sequence, and serialization correctness gates;
4. regenerate training traces and selected banks from their seeds and match hashes;
5. refuse confirmation execution unless the preregistration/matrix/trace hashes match;
6. run confirmation and timing without editing raw files;
7. regenerate summaries and SVGs in a clean staging directory;
8. compare every derived hash and planned-cell count;
9. scan public receipts for absolute home paths, local account/machine names, tokens, and raw identity-bearing test artifacts;
10. emit a final manifest with exact checks, failures, censoring, commands, tool versions, and hashes.

Exact shell commands belong in the implementation after task names exist; inventing executable commands in this preimplementation plan would create a false reproduction receipt. The semantic workflow and schemas above are already frozen.

## 17. Stop rule

Stop Build 001 when the full frozen matrix is either measured or explicitly classified as unsupported, resource-limited, or infrastructure-failed; correctness and evidence audits pass; every demanded output is forced; and the decision label follows section 13 without strengthening claim ceilings.

If no bank size/policy earns a bounded specialized advantage, report `NO_USEFUL_ADVANTAGE` and preserve the negative receipts. If only resident factor workloads earn it, report `SPECIALIZED_ADVANTAGE` with the exact residency/source/forcing boundary. If a stranger architecture appears, keep its discovery evidence exploratory until it receives its own amended confirmation manifest.

Binary remains the floor throughout. The experiment begins at numeric representation and asks only what verified multiplicative structure is worth preserving above that floor.

---

## Execution coverage appended after the frozen body

This section is an execution receipt, not a retroactive change to the 916-line preregistration above. The embedded frozen source remains verbatim. Its source hash is still `4A79873ADCBE477944FFBE1D90AD0969AD99560AA0C014F3CB1DD8639FF9DDEF`.

### Status

```text
PILOT_SUBSET_COMPLETE_FULL_CONFIRMATION_NOT_RUN
```

The deterministic pilot executed 194 configuration rows across `A010_PILOT`, `D010_PILOT`, `M010_PILOT`, `T010_PILOT`, and `X020_PILOT`, using the no-bank control and bank sizes 4, 8, 16, and 32, `MAG` source, 16-bit functional exponents, one pilot replicate, and magnitude-final or predicate outputs. It also executed independent representation-contract, addition-freshness, `K+1` migration-thrash, factor-resident crossover-proxy, VM-trace, and one-host microbenchmark probes.

The following frozen confirmation work was not executed:

- the complete 1,800-trace confirmation matrix;
- `FACT` and `BANK_CERT` source replay;
- five functional widths and eight confirmation replicates;
- signed `[-64,64]` pairwise operation/valuation exhaustion, full four-lane tuple enumeration, 16-bit raw-encoding enumeration, and complete status/overflow exhaustion;
- at least 10,000 randomized cases per important cell and 256 mixed sequences of length 64;
- an independently executable `SPARSE_FULL` operation/output replay and true Build 000 sparse-operation cost replay;
- registered SplitMix64/FNV namespaced trace generation; the exploratory pilot uses seeded .NET `Random` under the manifest-recorded runtime;
- complete `MANAGED`, `STRUCTURAL_SEQUENTIAL`, `STRUCTURAL_PARALLEL`, and `LOGICAL_STORAGE` Pareto profiles;
- third-party PARI/FLINT/FriCAS/GMP performance baselines;
- hardware elaboration and synthesis.

These cells are `NOT_MEASURED`; they are not relabeled as unsupported or resource failures. The frozen section 17 full-matrix stop condition was therefore not met. The pilot cannot earn a positive label, but absent confirmation does not earn `NO_USEFUL_ADVANTAGE` either. The correct status is `PARTIAL — PILOT_NEGATIVE; FINAL DECISION NOT EARNED`. No claim is made that the unexecuted confirmation matrix could not reveal a better implementation or workload cell.

### Checked receipts

| Receipt | Checked result |
|---|---|
| [`test-summary.json`](../results/build001/test-summary.json) | 89/89 passed; 0 failed; 0 skipped |
| [`correctness.json`](../results/build001/correctness.json) | 39,621 checks; 0 failures; exact bounded domains recorded |
| [`workload_matrix.csv`](../results/build001/workload_matrix.csv) | 194 pilot configuration rows; status retained per row |
| [`addition_freshness.csv`](../results/build001/addition_freshness.csv) | 1,000 additions per `K`; exact/lower-bound lanes and refresh work retained |
| [`migration_thrash.csv`](../results/build001/migration_thrash.csv) | deterministic `K+1` cycle; all migration/lane traffic; 0 exactness failures |
| [`crossovers.csv`](../results/build001/crossovers.csv) | registered `Q=1..4096`; work proxy and logical payload kept separate |
| [`microbenchmarks.csv`](../results/build001/microbenchmarks.csv) | seven trials per managed kernel on the manifest host |
| [`vm_trace.json`](../results/build001/vm_trace.json) | successful domain trace plus atomic failed-destination trace |
| [`protocol_coverage.json`](../results/build001/protocol_coverage.json) | high-level pilot coverage and omission categories; not a per-cell frozen failure matrix |
| [`manifest.json`](../results/build001/manifest.json) | baseline, plan, environment, commands, assemblies, and all artifact hashes |

Canonical verification command:

```powershell
& .\scripts\verify-build001.ps1
```

The verifier preserves the frozen source, regenerates the pilot results, validates the evidence manifest, and checks Build 000 immutability. The partial classification and the open full-confirmation gate are in [`BUILD_001_REPORT.md`](../BUILD_001_REPORT.md).
