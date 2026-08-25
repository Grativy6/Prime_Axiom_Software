# Build 004 frozen experiment plan

Protocol: `PAS-BUILD004-EXACT-LINEAGE-0001`  
Baseline: merged Build 003 commit `31dd150540bac79de3ee5925b44afdb7abaf327a`  
Frozen before implementation: yes

## Research question

Can computation preserve multiplicative provenance after evaluation, and is a two-speed receipt useful in practice: an exact active-source projection for overlap and deduplication plus a persistent derivation circuit for replay, alternatives, retraction, and explanation?

The motivating prime-product encoding is a candidate representation, not the target conclusion. Build 004 must compare it with explicit sets, sparse coordinates, dense bitsets, and a content-addressed derivation DAG on the same declared workloads.

## Scope and claim ceiling

Build 004 may establish only bounded software and abstract-structure results under this protocol. It may not claim:

- that numeric primes are computational, physical, PAL, or ontological primitives;
- that a PEV, prime product, digest, or DAG authenticates a source;
- that exact lineage establishes data quality, independence, causality, consent, authority, or scientific truth;
- privacy, encryption, zero knowledge, accumulator security, or tamper-proof history;
- placed/routed area, energy, frequency, or hardware advantage;
- PAL conformance for multi-parent account semantics; or
- a universal performance winner.

The positive terminal label, if every required gate passes, is `BOUNDED_EXACT_LINEAGE_TOOLKIT_VALIDATED`. A failed or incomplete gate remains `PARTIAL — FINAL DECISION NOT EARNED` with the failing family named. Hardware remains `NOT_MEASURED`.

## Architecture under test

The authoritative object is a typed, immutable derivation DAG. It has independent projections and an independent evidence envelope:

```text
immutable derivation DAG
|- source-support projection       overlap and deduplication
|- source-multiplicity projection  repeated joint use
|- numeric-factor projection       exact multiplicative arithmetic
|- unit-dimension projection       ratio-scale dimensional checking
`- evidence envelope               source, calibration, validity, uncertainty,
                                    residual, authentication state
```

Every projection must declare what it preserves, what it discards, its registry or basis identity, its completeness, and whether the underlying payload remains replayable. Lineage completeness, payload replayability, and issuer authenticity are independent axes.

An atom identifies an observation occurrence, not merely a sensor. Its key contains a namespace, assignment epoch, and occurrence ID. The same key with the same payload digest is a retransmission; the same key with a different digest is a conflict. Recycled coordinates require a new epoch.

The derivation vocabulary is `ZERO`, `ONE`, `ATOM`, `JOINT`, `ALTERNATIVE`, and typed `TRANSFORM`. `JOINT` is commutative and associative but preserves multiplicity. `ALTERNATIVE` is commutative and associative but remains distinct from joint use. General quotient is not part of positive provenance; division is allowed only inside an explicitly invertible numeric contract.

## Exact combinatorial experiment

Implement exact nonnegative rational arithmetic and signed prime-exponent receipts. Construct factorial coordinates using Legendre valuations and binomial coordinates by subtracting factorial vectors. Do not factor a completed factorial, binomial coefficient, or probability numerator/denominator afterward while calling the path prime-native.

Required operations:

- `n!`;
- `C(n,k)`;
- a hypergeometric point probability `C(K,x) C(N-K,n-x) / C(N,n)`;
- a hypergeometric event as an exact sum of point probabilities; and
- adjacent-point recurrence as an independently implemented ordinary exact control.

Required deterministic domains:

- factorials `0 <= n <= 512`;
- every binomial pair `0 <= n <= 256`, `0 <= k <= n` (33,153 cases);
- every in-support hypergeometric point for `0 <= N <= 24` (20,475 cases);
- normalization of every hypergeometric distribution for `0 <= N <= 32` (12,529 distributions);
- 10,000 seeded valid point cases with `N <= 4096`, seed `0x50415334`;
- named cases `(N,K,n,x) = (5000,1200,900,225)` and `(1000,413,271,117)`;
- `C(4096,2048)`, plus hostile `C(100000,1)`;
- exact zero/one boundaries, symmetry, ratio reduction, full-support events, and tails; and
- adjacent stream reuse for all distributions through `N=48` and named `N=2000` streams.

The ordinary controls use `BigInteger` cross-cancellation and adjacent exact recurrence; they must not materialize factorials as a straw baseline. Every claimed equality is differential, exact, and reproducible. A point may be multiplicative and prime-coordinate-local after construction. An event sum must create an additive derivation node or cross to exact magnitude; it must never be presented as exponent-vector addition.

## Lineage and fusion experiment

Implement these loss-ordered representations under one validated registry:

- raw prime product;
- sparse exponent map;
- dense bitset/PEV;
- explicit `HashSet<AtomKey>` control;
- persistent content-addressed derivation DAG; and
- DAG with cached support and multiplicity projections.

Required deterministic families:

- all 65,536 subset-pair combinations for universe size 8 across product, sparse, bitset, and set controls;
- all 6,561 vector-pair combinations for four exponents in `0..2` across exponent addition/min/max;
- canonical node IDs under child permutation and associative flattening;
- distinct roots for `a*b+c*d` and `a*c+b*d`, despite equal support and multiplicity projections;
- `a+a` versus `a`, `a*a` versus `a`, same value/different lineage, and same support/different value;
- semantic equality but structural distinction for `a*(b+c)` and `a*b+a*c`;
- DAG mutation, dangling reference, cycle, cached-summary mutation, payload mutation, registry collision, and epoch-recycle rejection; and
- retraction by specialization/replay, including a case where naive support subtraction is wrong.

Use an exact two-state likelihood payload with positive rational weights. Unique-source fusion is `left * right / exact-overlap` and is allowed only when the overlap payload is replayable. Compare every successful result with a centralized unique-atom oracle.

Required network cases:

- the three-node PRIMEX-style cycle with no new observations;
- cycles with newly assigned occurrence IDs;
- seeded asynchronous duplicate, delay, and reorder schedules;
- overlap identified but overlap payload unavailable;
- same atom ID with conflicting payload digests;
- registry collision and delayed prior-epoch message;
- partial lineage whose support is only a known lower bound;
- exact product retraction; and
- noninvertible or missing-witness retraction.

A no-new-information cycle must stabilize its semantic root. Failed merges must be atomic. Identifying overlap without its shared payload is not exact fusion.

## Boundary probes

These probes test where the architecture stops. They are not separate product claims.

### Database and ETL

Treat provenance as a semantic receipt carried beside scalar evaluation. Demonstrate that evaluation is many-to-one and that source support alone cannot reconstruct joint-versus-alternative structure. Bound the experiment to positive relational-style composition; negation, difference, arbitrary aggregates, recursion, and opaque UDFs remain typed crossings or open work.

### Calibration and units

Keep numeric coefficient coordinates, physical-dimension coordinates, derivation history, and evidence/calibration envelope separate. Compose exact ratio-scale conversions and calibration ratios. Required hostile controls are Celsius/affine conversion, dB/log conversion, nonlinear calibration, correlated uncertainty, rounded coefficients, and expired validity. Those require explicit transforms or an unresolved state, not exponent merging.

### Just intonation and audio

Represent a rational interval as signed prime coordinates, compose and invert it, derive nominal frequency from a declared base, and render a deterministic PCM WAV. Preserve the exact requested ratio separately from approximate samples and record sample rate, duration, phase, amplitude, envelope, rounding, and clipping policy. Test octave-folded collisions and equal ratio with different lineage. This is a boundary/readout experiment over established monzo-like prior art, not a new music theory.

### Structural accumulator

Implement only a transparent membership demonstration over exact support/product representations. It must emit `NOT_CRYPTOGRAPHIC` and `NO_PRIVACY`, expose membership leakage, and distinguish a structural token, integrity digest, authenticated commitment, membership proof, and zero-knowledge proof. The latter three remain `NOT_PROVIDED`. No novel cryptography is in scope.

### BOM demonstration

Keep this deliberately small: show that equal computed quantity can retain different supplier/component derivations, and that shared-part overlap can be queried. Do not build a manufacturing application in Build 004.

## Evidence gates

Correctness gates precede diagnostic measurement:

1. exact arithmetic invariants and frozen differential domains;
2. registry, identity, projection, DAG, mutation, and replay invariants;
3. exact centralized-oracle agreement for every successful fusion;
4. explicit typed failure for missing, partial, conflicting, recycled, nonlinear, affine, or unauthenticated cases;
5. deterministic output from two external generator invocations;
6. byte agreement between committed and replayed generated evidence;
7. zero-skip repository test pass; and
8. inherited Build 000-003 reports and evidence unchanged from the baseline.

Generated evidence will live in `results/build004/`; the generator may not claim its own deterministic replay. The verifier performs two fresh replays, validates exact file sets and hashes, and writes its PASS receipt outside the committed results directory.

## Cost ledgers

Do not sum heterogeneous quantities or choose post hoc weights.

### Abstract structural ledger

Record input/domain size, active atoms, multiplicities, raw-product bit length, sparse entries, bitset words, DAG nodes/edges/depth/reuse, projection queries, exact payload dependencies, additive nodes, transform nodes, and declared information loss.

### Host-software diagnostic ledger

Record canonical byte length, arbitrary-width operations, map operations, hash computations, DAG visits, exact-rational reductions, cache hits/misses, and optional elapsed/allocation diagnostics with runtime and platform identity. Host timings are nonterminal diagnostics for this implementation only.

### Physical/hardware implication ledger

Record only representation width, variable-width datapath need, conceptual parallel Boolean depth, sparse lookup/routing need, and DAG/hash memory traffic obligations. NAND/DFF/PPA/timing/energy remain `NOT_MEASURED` unless a separately frozen hardware experiment is actually built.

Abstract elegance, current-software performance, and physical implementation consequences are reported independently. Build 004 does not rank them with a single score.

## Prior-art and reference boundary

PRIMEX-PEV motivates the active-source projection and exact-overlap question. Database provenance semirings control the joint-versus-alternative comparison. Exact unit and uncertainty handling must defer to SI/UCUM/GUM-style domain standards; rational just intonation and monzos are established prior art; cryptographic accumulators require separate published security definitions and assumptions.

PAL v2.2, BLA, CLEF, BRT&AIC, and the A0 Software Boundary-Layer Kernel may be applied only after computational results are earned, as removable comparative lenses. In particular, PAL v2.2 leaves multi-parent account semantics open. This experiment neither fills that canon gap nor treats agreement with any framework as evidence.

## Build 005 recommendation rule

Recommend the next build only after separating:

- what the full DAG preserves that every projection loses;
- whether a PEV-like cache materially improves exact fusion queries;
- the structural and current-software price of retaining both;
- the first operation families that require transforms or replay; and
- which missing authenticity, uncertainty, distributed-systems, privacy, or hardware question is now the dominant blocker.

Negative results and representational equivalences are successful outcomes.
