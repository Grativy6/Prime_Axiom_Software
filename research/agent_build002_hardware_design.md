# Build 002 independent hardware-design memo

Status: **DESIGN PROPOSAL — NO BUILD 002 RESULT**

Controlling question:

> Given these physical primitives, what mathematics does the machine find naturally?

This memo is intentionally written before any Build 002 result exists. It does not amend Build 000 or Build 001, and it does not convert the incomplete Build 001 confirmation gate into a terminal decision. Build 001 remains `PILOT_SUBSET_COMPLETE_FULL_CONFIRMATION_NOT_RUN` and `PARTIAL — PILOT_NEGATIVE; FINAL DECISION NOT EARNED`.

## 1. Earned starting point

The repository has already earned four constraints that should determine the hardware experiment.

1. Binary readable state remains the floor. Prime identity is configuration/semantics above stable state, not a physical primitive.
2. The architectural fork is at the word/functional-unit layer. Both lineages may use the same NAND gates, registers, counters, control, and binary exponent arithmetic.
3. A dense prime bank is not a plausible universal integer store. It can be a deliberately bounded structural datapath, a lower-bound experiment, or a sidecar.
4. An exact cofactor makes a finite bank total over ordinary integers, but it also retains ordinary multiplication, division, GCD, comparison, reconstruction, and addition costs. Hardware must not count only exponent lanes and call the complete operation local.

The existing source reaches an abstract `BitState`, a unit-delay `GateNetwork.Nand`, NAND-derived gates, a logical feedback latch/register model, ripple add/subtract/compare, a transparent shift-and-add multiplier, and dense exponent operations. It does **not** yet realize a complete structural processor: no static netlist identity, wire/fanout accounting, clocked DFF model, divider, GCD controller, constant-factor extractor, cofactor datapath, ingress/egress datapath, HDL implementation, or synthesis receipt exists. Existing `BigInteger` cofactor work is a semantic/host boundary and may not be relabeled as gate-level work.

## 2. Core recommendation

Build one common registered NAND substrate and fork it into:

```text
registered binary state
├── BIN-FU: conventional magnitude word + conventional arithmetic units
└── SVU: fixed small valuation structure + exact residual path
          └── optional threshold/cache variants
```

The primary experimental object should be a **small structural valuation unit**, working name `SVU-S4`, with the fixed hard-wired bank

```text
S4 = {2, 3, 5, 7}.
```

For a nonzero value its exact canonical state is

```text
sign * residual * 2^e2 * 3^e3 * 5^e5 * 7^e7,
```

where `residual` is an exact unsigned binary word coprime to 210. Zero is a separate tag. Sign is a separate multiplicative unit bit. Exponents are ordinary bounded binary counters. Bank labels live in hard-wired configuration and their catalogue/configuration cost is still reported.

This is not proposed as a replacement CPU word. It is the smallest total representation that can answer the prompt without either pretending a finite basis is universal or hiding an unfactored residual. It also creates a clean ablation ladder:

1. `SVU-PURE`: residual is fixed to one; positive `S4`-smooth domain only.
2. `SVU-S4`: exact residual plus four exact exponent lanes.
3. `SVU-LOWER`: exact residual plus one exact/lower-bound bit per lane for addition.
4. `VTC-S4`: a certified threshold sidecar storing `min(v_p(n), T)` beside an authoritative magnitude; predicate-oriented, not reconstructive.
5. `TAGGED-CACHE-K`: optional two- or four-entry tagged valuation cache, only if fixed-lane results justify paying CAM/tag/routing costs.

The pure unit is a lower-bound/mechanism probe. `SVU-S4` is the fair total candidate. `VTC-S4` tests the Build 001 lesson that valuations may be more useful as certified metadata than as the authoritative number representation. A dynamic tagged cache should not be the first implementation: fixed wiring must first establish whether there is any signal worth making sparse.

## 3. One charged substrate

### 3.1 Logical primitive

Use one two-input NAND with unit logical delay as the only combinational primitive. NOT, AND, OR, XOR, XNOR, muxes, decoders, comparators, adders, subtractors, shifters, priority encoders, dividers, and controllers must elaborate to NAND.

The current transparent contracts remain useful anchors:

| Circuit | Current transparent NAND count | Current modeled depth |
|---|---:|---:|
| NOT | 1 | 1 |
| AND | 2 | 2 |
| OR | 3 | 2 |
| XOR | 4 | 3 |
| 1-bit full adder | 15 | 7 |
| `w`-bit ripple add | `15w` | implementation-derived |
| `w`-bit subtract | `19w` | implementation-derived |
| `w`-bit compare | `23w` | implementation-derived |
| current `w`-bit shift/add multiply | `32w^2` | `14w-1` in checked Build 000 rows |

Formulae not already asserted by a checked receipt must be regenerated from the Build 002 elaborator before appearing as results.

### 3.2 State

For logical simulation, count state as `DFF`/state bits separately from NAND2. Do not silently choose one NAND-equivalent for a DFF. If a NAND-only latch/DFF implementation is added, report both the decomposed NAND implementation and the abstract DFF view.

For HDL/synthesis, both top-level machines must use the same:

- clock/reset convention;
- registered input and output boundary;
- `start/ready/valid/status` protocol;
- synthesis tool, version, target library/device, constraints, and optimization settings;
- reset and enable style;
- treatment of memories, ROMs, and constant tables.

Constant prime labels may be hard-wired, but constant wires, ROM bits, decoders, and routing are not free configuration.

### 3.3 Two structural budget points

Implement two explicitly separate resource budgets where practical:

- `SEQUENTIAL`: reuse one arithmetic slice; report static area/state, cycles, and dynamic evaluations/transitions.
- `PARALLEL`: replicate lanes or partial-product logic for throughput; report static area/state, critical path, and initiation interval.

A parallel four-lane SVU may not be compared only against a single reused binary ALU. Conversely, a combinational binary multiplier may not be compared only against a serialized one-lane exponent engine.

## 4. Conventional baseline

The conventional baseline should be competent rather than merely transparent.

### 4.1 `BIN-FU-w`

At `w in {4, 6, 8}`, build and exhaustively verify:

- NAND-derived NOT/AND/OR/XOR/XNOR/mux;
- ripple adder with carry;
- subtractor with borrow;
- total-order comparator;
- logical shifts and trailing-zero detector;
- register/counter and status register;
- full `w x w -> 2w` unsigned multiplier;
- restoring or non-restoring unsigned divider with quotient/remainder and divide-by-zero status;
- sequential binary GCD (Stein) or another explicitly documented magnitude-domain GCD;
- LCM as a checked composite operation, not a free opcode;
- exact rational reduction using the baseline GCD and divider.

The existing shift/add multiplier is the transparent baseline. Also synthesize a behavioral RTL `*` baseline under the same tool so that an experimental circuit cannot win only by being compared with an avoidably weak multiplier. Advantage claims use the best correct supported nonexperimental baseline in the relevant resource profile.

For fixed-prime valuation/divisibility queries, include a competent constant-divisor or trailing-zero baseline. Comparing a hard-wired prime lane only with a generic divider would be a straw comparison.

### 4.2 Conventional algebra

Retaining carry and full products realizes bounded exact natural-number arithmetic. A checked fixed-width state machine is a partial semiring because out-of-range results return overflow. If wraparound is separately tested, it realizes arithmetic in `Z/(2^w)Z`; wraparound results must never be mixed with checked-integer results. The comparator supplies ordinary total magnitude order. The binary GCD unit computes the meet in the divisibility order, but by an iterative magnitude algorithm rather than by exposing valuation coordinates.

## 5. Experimental representation and units

### 5.1 Exact state contract

For fixed `S4`, a nonzero canonical state is:

```text
kind        : NONZERO
sign        : POSITIVE | NEGATIVE
e[4]        : fixed-width unsigned binary exponents
residual    : positive exact binary word
exact[4]    : all one in canonical state
status      : VALID
```

and obeys:

```text
gcd(residual, 210) = 1
value = sign * residual * product(S4[i] ^ e[i]).
```

Zero has one form: `kind=ZERO`, positive/neutral sign convention, zero residual, zero exponents, exact lanes. Invalid, overflow, non-exact cancellation, malformed certificate, and resource-limit states are statuses, not numeric sentinels.

For addition, `SVU-LOWER` may mark a lane `LOWER_BOUND`. The represented integer remains exact because additional copies may remain in the residual. A lower-bound zero is not an exact zero valuation. Exact-only consumers refresh with charged constant-prime stripping or return `UNKNOWN_REQUIRED`.

No silently saturating exponent is allowed in the exact SVU. A separate threshold cache may saturate only because its contract says that the top code means `>= T`, not an exact exponent.

### 5.2 Native structural operations

Derive the first instruction vocabulary from the datapath:

| Working opcode | Circuit action | Exact domain |
|---|---|---|
| `COMPOSE` | componentwise exponent add; sign XOR; residual multiply | general canonical SVU; pure lane-local only when residuals are one |
| `CANCEL` | checked componentwise subtract; sign XOR; exact residual divide | canonical exact division; pure lane-local when divisor residual is one |
| `MEET` | componentwise exponent min plus residual GCD | canonical SVU; pure lane-local when residuals are one/coprime by contract |
| `JOIN` | componentwise exponent max plus checked residual LCM | same limitation |
| `ORDER_DIVIDES` | reject if any lane underflows; otherwise exact residual divisibility | full result needs residual path |
| `PROJECT(p)` | lane read or certified lower bound | configured bank only |
| `SCALE(p,k)` | checked lane increment by known prime/exponent | configured prime and range |
| `REFRESH(p)` | strip configured prime from residual and advance exponent | lower-bound lane |
| `RECONSTRUCT` | multiply residual by configured prime powers | explicit magnitude egress |
| `INGRESS_MAG` | strip each configured prime from binary magnitude | explicit cold boundary |
| `ADD_COMMON` | preserve lane minima and compute exact residual sum | mixed/boundary operation, not fully local |

The hardware must not expose a friendly `DECOMPOSE` that means unbounded factorization.

For the existing binary-lane construction, the expected transparent `COMPOSE` network for `K` exponent lanes of width `e` is:

```text
15*K*e + 3*(K-1) NAND2
```

where the final term is the balanced OR of overflow flags. For `K=4,e=4`, that is 249 NAND2 before registers/control. This is a design calculation to be checked by the new static elaborator and synthesis; it is not yet a Build 002 result.

### 5.3 Addition as the destructive test

For canonical positive inputs, per lane compute `m_i=min(e_ai,e_bi)`. Then:

```text
a = g*A
b = g*B
a+b = g*(A+B),    g = product(p_i^m_i).
```

The minimum is an exact output valuation for prime `p_i` when the two exact input valuations differ. Equal valuations yield only a certified lower bound because `A+B` may contain additional copies of `p_i`.

The circuit must charge:

- four min circuits and lane comparisons;
- reconstruction of exponent differences into both residual terms;
- residual binary addition/subtraction and sign handling;
- residual-width growth/overflow;
- exact/lower-bound metadata writes;
- every later refresh forced by a query or canonical output.

Three addition paths should be compared:

1. `RECONSTRUCT_ADD_REINGRESS`: full magnitude crossing and canonical re-encoding.
2. `ADD_COMMON_LAZY`: preserve certified minima and exact residual; defer equal-lane refresh.
3. `ADD_COMMON_EAGER`: same result followed by immediate refresh of all uncertain lanes.

The lazy path gets no credit if its debt is never forced.

### 5.4 Hardware-representation variants

The first synthesis sweep should compare:

| Variant | State | Likely cheap math | Required caution |
|---|---|---|---|
| `BINEXP` | binary exponent per fixed lane | composition, scaling, compact state | carry path remains |
| `THERMO` | thermometer/unary exponent per lane | min/max via bitwise AND/OR, threshold predicates | state and compose routing grow linearly with max exponent |
| `PRESENCE` | exact factor-present bit per lane | support union/intersection and prime divisibility | loses multiplicity; cannot reconstruct alone |
| `THRESHOLD-T` | `min(v_p,T)` with top meaning `>=T` | bounded divisibility filters; saturated compose | certificate sidecar only; cancellation can become unknown |
| `TAGGED-CACHE-K` | valid + prime tag + exponent/certificate | sparse demanded support | CAM compare, allocation, replacement, and crossbar cost |

`PRESENCE` and `THRESHOLD-T` are not weaker encodings to be judged by exact reconstruction. They are predicate coprocessors with different output contracts. Their best result could be that the useful hardware object is a certified property cache rather than an alternative number word.

### 5.5 Binary exponents versus thermometer/threshold lanes

This is the most informative representation fork inside the experimental lineage.

Let one threshold lane encode

```text
t[j] = 1 iff v_p(n) >= j,    j=1..T.
```

The legal words are thermometer words (`111...100...0`). In the **threshold sidecar** contract, all-zero means exact valuation zero, a highest set threshold below `T` gives the exact valuation, and all `T` bits set means `v_p(n) >= T`, not exact equality. This distinction is essential.

The representation makes the divisibility lattice literally monotone wiring:

| Operation on one lane | Thermometer circuit | Transparent NAND cost/depth before control |
|---|---|---|
| `min(v_a,v_b)` | bitwise AND | `2T`, depth 2 |
| `max(v_a,v_b)` | bitwise OR | `3T`, depth 2 |
| `p^j divides n` | read `t[j]` | wire for fixed `j`; mux cost for variable `j` |
| prime presence | read `t[1]` | wire |
| saturated compose | Boolean threshold convolution | quadratic naive gates/wires in `T` |

For saturated composition, each output threshold is

```text
c[j] = OR over r=0..j of (a[r] AND b[j-r]),
a[0] = b[0] = 1.
```

This is a Boolean convolution. A direct binary-tree construction, without cross-output sharing, uses the design estimate

```text
(5*T^2 + T) / 2 NAND2 per lane
```

and depth bounded by one AND level plus a logarithmic OR tree. The exact mapped count must come from elaboration/synthesis. For `T=3`, the naive estimate is 24 NAND2 per lane, so four lanes are approximately 96 NAND2 before shared status/control. A four-lane exact binary-exponent compose with two-bit lanes (`0..3`) is 129 NAND2 including its overflow reduction. These numbers are **not equivalent semantics**: the thermometer top code is a certified `>=3` value, while the two-bit exact lane must report overflow when the true sum exceeds three.

An exact bounded thermometer lane can add a separate `sum >= T+1` overflow convolution and reject overflow. That restores exactness but also adds logic. Exact cancellation is less natural: deciding `a-b >= j` requires cross-position comparison/correlation, and a saturated top operand makes the result a lower bound or unknown. Consequently:

- thermometer/threshold lanes are strongest for `MEET`, `JOIN`, support, and fixed prime-power predicates;
- binary exponent lanes are stronger for exact `COMPOSE`, `CANCEL`, reconstruction, and larger exponent ranges;
- `T=1` collapses to a factor-presence bitmap and exposes a Boolean support lattice;
- small `T in {2,3}` may be the best property-cache design;
- increasing `T` makes thermometer state and convolution/crossbar routing grow linearly/quadratically while binary state grows logarithmically.

The strongest “let the hardware reveal the mathematics” experiment is therefore **not** to pick one in advance. Synthesize both under the same integrated shell and sweep the operation mix. If the workload is meet/join/divisibility-heavy, a thermometer threshold sidecar may reveal the product-of-chains/divisibility lattice more honestly than an exact alternate number representation. If compose/cancel dominates or exact magnitude must be reconstructed, binary exponent counters should take over. That crossover is a Build 002 result to measure, not a design assumption.

Threshold cancellation rules must be typed:

- codes below `T` are exact and can support ordinary checked subtraction;
- a saturated dividend minus an exact divisor yields only a certified lower bound;
- a saturated divisor generally makes exact divisibility/cancellation unknown;
- outside-bank factors always require the magnitude/residual fallback.

No threshold lane may manufacture an exact exponent from its saturated top state.

### 5.6 Baseline-only and integrated-resource accounting

Synthesize and report four separate top-level configurations:

```text
B0             = conventional magnitude core only
B0+VBIN-S4     = the identical B0 plus four exact binary-exponent certificate lanes
B0+VTHERM-S4-T = the identical B0 plus four threshold/thermometer lanes
SVU-S4         = structural authoritative word + exact residual/boundary datapath
```

For each sidecar, emit two views:

1. **incremental sidecar delta**: additional NAND2/cells, DFFs, connections, transitions, ports, and critical-path effect relative to the exact same `B0` netlist;
2. **integrated total**: the whole processor/service top, including magnitude core, sidecar state, extraction/validation, coherency/exactness logic, update path, clock/reset load, output/fallback muxes, and controller.

The incremental view explains mechanism. Only the integrated total can earn a machine/coprocessor advantage classification.

When a magnitude multiply and sidecar update execute concurrently, latency is the maximum of the two commit paths, not their sum, but static area and switching include both. If sidecar validity/status lies on the architectural commit path, its delay counts. On a sidecar-hit predicate the magnitude core may be clock-gated and dynamic work may fall, but its static resources remain. `SVU-S4` may avoid a full magnitude datapath during warm structural work, but every on-chip residual unit and ingress/egress converter included in the claimed machine remains in static totals. A block-only comparison may be reported as a lower bound and cannot support the final classification.

## 6. Mathematics realized by the circuits

These abstractions are established mathematics. Build 002 can show that a circuit realizes them cheaply under a declared cost model; it cannot claim to originate them.

| Hardware object | Mathematical abstraction | Cheap operations |
|---|---|---|
| NAND network | Boolean algebra expressed through the Sheffer stroke | arbitrary finite Boolean functions after composition |
| checked binary word | bounded natural arithmetic / partial semiring | ordinary add, compare; multiplier according to chosen topology |
| wrapping binary word | finite ring `Z/(2^w)Z` | modular add/multiply |
| `K` exact exponent lanes | free commutative monoid `N^K` with a finite overflow boundary | componentwise addition (`COMPOSE`) |
| signed exponent completion | free abelian group `Z^K` | composition and cancellation for positive rationals/nonzero signed units |
| exponent lanes with componentwise order | divisibility poset; product of chains | `DIVIDES`, coordinate projection |
| componentwise min/max | distributive lattice | GCD/LCM on the covered factor part |
| exact presence bitmap | Boolean lattice/power set of configured support | support union/intersection |
| threshold lanes `[0..T]` | product of finite chains with saturated addition | bounded divisibility evidence, min/max, saturated compose |
| canonical exponents plus `S`-free residual | direct product of the bank monoid and residual multiplicative monoid, isomorphic to ordinary positive multiplication | decomposition of work, not a new integer algebra |
| valuation behavior under addition | non-Archimedean filtration: `v_p(a+b)>=min(...)`, exact on unequal valuations | common-factor lower bounds |

The valuation image is “tropical-like” only with care. Equal-valuation cancellation prevents ordinary integer addition from being a homomorphism to min-plus arithmetic. The circuit exposes a lower-bound rule, not a complete tropical semiring for integers.

The likely answer to the goal question is layered:

```text
NAND naturally exposes Boolean composition.
Registers expose finite-state transition systems.
Binary words expose bounded positional arithmetic and total magnitude order.
Exponent lanes expose a product-of-chains ordered monoid and distributive lattice.
Addition exposes where that valuation shadow ceases to be closed.
```

Whether any layer is *useful* is a separate engineering question answered only by the experiments below.

## 7. Exact correctness and formal domains

### 7.1 Primitive/baseline exhaustion

For each `w in {4,6,8}`:

- exhaust every NAND/derived-gate truth table;
- exhaust every `a,b in [0,2^w-1]` and carry-in for addition;
- exhaust every ordered pair for subtraction, compare, `w x w -> 2w` multiply, quotient/remainder, divides, GCD, and LCM;
- exhaust every numerator/positive-denominator pair for rational reduction;
- exhaust every register state with enable/data and one complete counter cycle;
- check zero, one, maximum input, divide by zero, exact/non-exact division, carry, borrow, and overflow statuses.

At `w=8`, a pairwise operation has 65,536 ordered input pairs and remains deliberately tractable.

### 7.2 Structural-lane exhaustion

For exponent widths `e in {2,3,4}`:

- exhaust every one-lane exponent pair for add/subtract/compare/min/max and every overflow/underflow result;
- exhaust every carry/borrow/status vector for `K in {1,2,4,8}` through the reduction tree;
- exhaust every two-lane state pair at `K=2,e=2` for compose, cancel, meet, join, and divides;
- for `K=4`, exhaust every coordinate state produced by magnitudes in the paired `w<=8` magnitude domains, then every ordered pair of those input magnitudes;
- verify reconstruct-after-success against an independent arbitrary-precision oracle at the boundary only.

### 7.3 Exact hybrid magnitude domain

For each `w in {4,6,8}` and every signed magnitude whose absolute value is in `[0,2^w-1]`:

1. cold-encode through `S4` and verify the `S4`-free residual invariant;
2. reconstruct exactly;
3. serialize/deserialize any hardware transaction record if such a format exists;
4. for every ordered pair, verify compose/full product, add/common-factor result, exact and rejected cancel, divides, GCD, LCM, and rational reduction;
5. independently check every exact/lower-bound lane after addition;
6. include product/result ports wide enough for the mathematical result or return an explicit checked range status. Never compare a `2w` conventional product with a silently narrowed SVU result.

For raw malformed-state exhaustion, define a tiny profile no larger than 16 total encoded bits, enumerate all bit patterns, and classify each as canonical, partial-valid, malformed, or invalid. Larger configurations receive deterministic mutation/property tests.

### 7.4 Sequence correctness

Generate at least 256 deterministic mixed sequences of length 64 for each important width/configuration. Compare after every instruction, including status, destination commit/invalidation, structural certificate, and demanded magnitude—not only the final state. Preserve minimized counterexamples as immutable fixtures.

Formal/SMT or formal-HDL targets should include:

- NAND equivalence of every derived gate;
- arithmetic equivalence of add/subtract/compare/multiply at bounded widths;
- `reconstruct(COMPOSE(a,b)) = reconstruct(a)*reconstruct(b)` on successful states;
- cancellation success iff the represented divisor divides exactly;
- componentwise min/max reconstruct to the covered GCD/LCM part;
- zero and identity laws;
- exponent/range overflow and malformed-state rejection;
- no successful result from a failed producer.

## 8. Frozen experiment matrix to write before measuring

Every workload has three source regimes and four output obligations where meaningful.

### 8.1 Source regimes

| Source | What arrives | Experimental charge | Conventional charge |
|---|---|---|---|
| `COLD_MAG` | ordinary signed binary magnitude only | constant-prime strip/extraction, state writes, validation | ordinary register load |
| `COLD_CERT` | independently verifiable complete factor certificate | certificate validation, bank filtering, residual construction | certificate validation and magnitude reconstruction |
| `WARM_RESIDENT` | values produced by prior charged operations | no new ingress, but retained state/metadata count | no new ingress; retained magnitude state counts |

`WARM_RESIDENT` reports both resident-only cost and setup-amortized cost at declared reuse counts. It is never presented as a cold-input result.

### 8.2 Output obligations

- `PREDICATE_ONLY`: demanded Boolean/valuation result only.
- `STRUCTURAL_FINAL`: final exact structural certificate; comparable only when the consumer accepts it.
- `MAGNITUDE_FINAL`: one exact magnitude at trace end.
- `MAGNITUDE_EVERY_OP`: exact magnitude after every operation.

### 8.3 Workloads

Use deterministic common traces and reuse counts

```text
Q in {1,2,4,8,16,32,64,128,256,1024}.
```

| ID | Workload | Purpose |
|---|---|---|
| `H010_COMPOSE` | repeated multiply/compose of `S4`-smooth resident values | strongest pure structural case |
| `H020_SCALE_CANCEL` | alternating scale by configured prime and exact cancellation | tests reversible lane locality |
| `H030_DIVIDES` | repeated valuation, prime-power divisibility, and divides queries | tests property reuse |
| `H040_GCD_LCM` | controlled shared `S4` part plus coprime/shared residuals | separates lane lattice from residual work |
| `H050_RATIONAL` | numerator/denominator products with bank and outside-bank cancellation | tests practical structural cancellation |
| `H060_COMMON_ADD` | `a=gA,b=gB`, split equally between unequal and equal lane valuations | tests exact versus lower-bound preservation |
| `H070_MIXED` | multiply, multiply, divide, add, multiply; repeat | requested mixed sequence and debt forcing |
| `H080_ADD_RATE` | additive share `0,1/16,1/4,1/2,1` with fixed forcing | locates structural-survival boundary |

Adversarial families:

| ID | Attack |
|---|---|
| `T010_RANDOM` | uniform random magnitudes; no hidden factors available |
| `T020_OUTSIDE` | primes/semiprimes dominated by factors outside `{2,3,5,7}` |
| `T030_METADATA` | very small values and short traces where tags/lanes dominate |
| `T040_RECONSTRUCT` | magnitude forced every operation |
| `T050_EQUAL_ADD` | equal valuations chosen to acquire additional bank factors after addition |
| `T060_OVERFLOW` | exponent, residual, result-width, and divide-status boundaries |
| `T070_PHASE` | factor support shifts among disjoint prime windows |
| `T080_THRASH_K1` | if tagged cache exists, deterministic cycle through `K+1` prime identities |

All architectures replay the same semantic trace. Oracle factors used to generate expectations are inaccessible to `COLD_MAG` implementations.

### 8.4 Representation ablations

At minimum compare:

```text
BIN-FU
SVU-PURE-S4
SVU-S4-BINEXP
SVU-S4-LOWER-LAZY
SVU-S4-LOWER-EAGER
VTC-S4-T={1,2,3}
```

Run `THERMO` only for small exponent caps where its full exact/saturated contract fits. Run `TAGGED-CACHE-K` only after its static tag/CAM/crossbar and replacement logic are realized. Unsupported cells remain `NOT_SUPPORTED`; they are not omitted or assigned zero cost.

## 9. Hardware cost vector

Never reduce the following to one post-hoc weighted score.

### 9.1 Static logical resources

```text
nand2_static
dff_static
latch_static (if distinct)
constant_bits / catalogue_bits
net_count
connection_count
cross_lane_connections
mux_input_count
comparator_bits
controller_state_bits
port_bits
max_fanout
fanout_histogram
combinational_depth
```

### 9.2 Dynamic logical resources

```text
cycles_to_valid
initiation_interval
nand_evaluations
net_transitions_0_to_1
net_transitions_1_to_0
register_bit_transitions
register_reads / writes
lane_reads / writes
divider_iterations
strip_iterations
reconstruction_multiplications
refreshes / invalidations
status reductions
input_bits / output_bits transferred
```

Use identical input-transition initialization and clock/reset accounting when comparing switching. Report zero-input/reset transitions separately so a representation with more state does not receive free initialization.

### 9.3 Synthesis resources

Under the same target and constraints record:

```text
tool/version/target/library/constraints
elaboration and synthesis status
cell counts by type
combinational area proxy
sequential area proxy
total area proxy
critical path and endpoints
worst slack / achieved clock
register count
inferred memories/DSPs (must be zero or equally controlled)
high-fanout nets
synthesis warnings
```

RTL synthesis is evidence about that RTL/tool/target, not silicon performance. If place-and-route is not run, routing pressure remains a logical proxy. If power analysis lacks a common activity trace and characterized library, do not call transition counts power.

### 9.4 Causal phases

Dynamic receipts remain split into:

```text
setup/configuration
ingress
resident native operation
maintenance/refresh
egress
```

Static resources are reported once per implementation, never copied into each event. Catalog cost is shown unamortized and at declared live-value/reuse counts.

### 9.5 Complete comparison profiles

Evaluate Pareto dominance only within complete profiles:

| Profile | Coordinates (all lower is better) |
|---|---|
| `LOGICAL_STATIC` | NAND2, DFF, connections, max fanout, port bits |
| `LOGICAL_SEQUENTIAL` | NAND2, DFF, cycles, NAND evaluations, net/register transitions |
| `LOGICAL_PARALLEL` | NAND2, DFF, critical depth, initiation interval, connections |
| `SYNTHESIS` | total area proxy, critical path, register count, high-fanout count |
| `STATE` | payload bits, metadata bits, catalog bits, provisioned bits |

A missing coordinate prevents a dominance claim rather than becoming zero.

## 10. Fairness and invalid-comparison rules

The following comparisons are invalid unless split:

1. pre-factorized experimental operands versus magnitude-only binary operands without charging source conversion;
2. structural output versus exact magnitude output;
3. pure `S4` coordinates versus arbitrary integers outside their domain;
4. lane-local GCD/LCM versus full-integer GCD/LCM when residual work is omitted;
5. hard-wired prime division versus a deliberately generic divider without a constant-divisor baseline;
6. parallel lanes versus a single reused binary unit without separate area/throughput profiles;
7. current dynamic `GateNetwork` evaluation counts versus static synthesized cells;
8. host `BigInteger` cofactor work added to NAND counts or treated as zero;
9. behavioral RTL optimized by synthesis on one side versus hand-expanded NAND RTL on the other without also reporting common mapped netlists;
10. wrapping binary arithmetic versus checked exact structural arithmetic;
11. saturating threshold evidence versus exact exponent reconstruction;
12. a cache hit without the construction, validation, retained state, and eviction that made the hit possible;
13. raw gate count without DFF, ports, control, fanout, and wiring disclosures;
14. one favorable `S4`-smooth workload as a prime-specific or general arithmetic conclusion.

## 11. Falsification and decision rules

Freeze implementation IDs, tool versions, exact matrix, trace hashes, and decision rules before inspecting confirmation aggregates. Discovery runs use a separate seed namespace. Any architecture selected after discovery receives fresh confirmation traces.

### 11.1 Correctness gate

No performance claim survives any arithmetic mismatch, malformed-state acceptance, silent narrowing/wrap, invalid exactness certificate, hidden oracle factor access, stale destination/query output, or unmatched semantic trace. The affected circuit/configuration is `WRONG_RESULT`; failures remain evidence.

### 11.2 Minimum engineering relevance

A bounded hardware advantage in a named family requires all of:

1. zero correctness/invariant failures in cited cells;
2. complete matched source and output obligations;
3. a complete logical or synthesis Pareto profile;
4. at least 20% improvement in the profile's declared primary latency/work coordinate versus the best correct nonexperimental baseline;
5. no more than 2x regression in any other coordinate of that profile;
6. the direction holds for at least seven of eight frozen traces at two widths, including `w=8`;
7. the result survives `MAGNITUDE_FINAL` unless the claim is explicitly a predicate/structural-interface coprocessor claim;
8. the result is not solely the pure smooth lower bound, one-prime `v_2`, a malformed-input path, or an unsupported-baseline omission.

The 20% and 2x thresholds are engineering relevance filters, not universal constants.

### 11.3 Classification rules

- `NO_HARDWARE_ADVANTAGE`: earned only after the complete frozen matrix/stop rule, when no experimental unit earns bounded advantage in any nontrivial family and every apparent local win is dominated after boundary/state/control cost.
- `WARM_STATE_SPECIALIZED_ADVANTAGE`: a candidate earns the criterion in at least two warm nontrivial families, including one of compose/scale/cancel and one of divisibility/GCD/rational reduction, with exact final magnitude forced; cold ingress may lose and must be shown.
- `PRIME_STRUCTURAL_COPROCESSOR_CANDIDATE`: in addition to the warm result, a fixed-prime exact or threshold unit earns a complete Pareto advantage for an externally meaningful predicate/cancellation service, crosses over after fully charged ingress at a registered finite `Q`, and retains a correct magnitude/fallback path for unsupported factors.
- `ALTERNATIVE_ARITHMETIC_UNIT_CANDIDATE`: requires supported exact behavior for arbitrary in-range values and positive results in cold magnitude, mixed arithmetic containing at least 25% arbitrary additions, outside-basis adversarial values, and magnitude-every-operation output. A smooth-only unit cannot earn this label.
- `UNEXPECTED_ARCHITECTURE`: requires an append-only amended preregistration, new implementation ID, and fresh confirmation namespace. Discovery evidence alone cannot earn it.

If synthesis/formal infrastructure or planned cells are missing, the honest status is `PARTIAL — FINAL DECISION NOT EARNED`; absence of a positive result does not earn `NO_HARDWARE_ADVANTAGE`.

### 11.4 Stop rule

Stop only when:

- baseline and candidate correctness gates pass;
- every frozen cell is measured or explicitly `NOT_SUPPORTED`, `RESOURCE_LIMIT`, or `FAILED_INFRASTRUCTURE`;
- cold/warm and structural/magnitude outputs remain separated;
- static, dynamic, state, control, and boundary receipts hash-check;
- synthesis claims use the same target assumptions;
- prior Build 000/001 evidence remains unchanged;
- the classification follows the frozen rule without strengthening it.

## 12. Recommended implementation order

1. Freeze `research/build002_experiment_plan.md` and the Build 000/001 preservation hashes.
2. Add a named static NAND netlist/elaboration layer; retain the current functional simulator as a differential oracle.
3. Complete and exhaust `BIN-FU-4`, then parameterize to 6 and 8 bits.
4. Implement pure fixed-lane `COMPOSE/CANCEL/MEET/JOIN/DIVIDES/PROJECT` with explicit registers/control.
5. Implement the exact residual path and cold `S4` ingress/egress; refuse to aggregate a cell until this path is structurally realized.
6. Implement lazy/eager common-factor addition and force its deferred debt.
7. Implement the threshold sidecar ablation. Attempt tagged sparse/CAM state only if fixed-lane evidence justifies it.
8. Emit equivalent HDL tops, prove/exhaust equivalence, and synthesize every compared top with one script/tool/target.
9. Run discovery, freeze the strongest candidate without deleting losers, then run fresh confirmation.
10. Derive `docs/HARDWARE_MATHEMATICS.md` from circuit receipts, keeping hardware fact, established abstraction, cost consequence, prior art, and open implication separate.

## 13. Expected ways the motivating hunch can fail

- Residual multiplication may dominate every total exact operation, leaving exponent lanes as redundant work.
- Cold odd-prime extraction may cost more than all later predicate savings.
- `v_2` may be the only economically useful fixed valuation because it maps directly to trailing-zero logic.
- Dense lane state/control/routing may erase shallow componentwise depth.
- Thermometer counters may make min/max cheap but lose badly on storage and compose.
- Addition may turn exact lanes into lower bounds so quickly that refresh dominates.
- A competent constant-divider, binary GCD, or RNS-style control may match the useful predicates without preserving prime exponents.
- The only win may require structural input and structural output; that is a valid specialized interface result, not an integer-ALU result.

## 14. Most informative likely endpoint

The most informative Build 002 outcome is not “prime hardware beats binary.” It is a measured boundary among three objects:

```text
exact binary magnitude
exact factor-resident structural word
certified bounded valuation/property sidecar
```

The circuits will likely make a product-of-finite-chains algebra—componentwise addition, order, min, max, and projection—visibly natural. The decisive question is whether that algebra deserves authoritative numeric state, a coprocessor/cache role, or only a pedagogical demonstration after ingress, residual work, wiring, and egress are charged.
