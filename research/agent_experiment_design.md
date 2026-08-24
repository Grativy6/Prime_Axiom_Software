# Build 000 adversarial experimental program

Status: design only. Nothing in this document is an experimental result.

## 1. Purpose and falsification posture

Build 000 should compare two lineages that diverge only after a common, explicit computational substrate:

1. finite two-state signals, combinational transition, clocked state, wiring, and storage;
2. conventional binary-magnitude representation and arithmetic;
3. factor/prime-coordinate representations and operations.

The motivating claim is not the null hypothesis. The working null is:

> Once identification, conversion, storage, closure, output, and precomputation costs are charged, prime-coordinate representation has no general computational advantage over binary magnitude.

The experiments should be capable of supporting any of these outcomes:

- it is only an inconvenient encoding;
- it has narrow factor-resident advantages;
- it supports useful specialized arithmetic units but not a general machine;
- a dual or lazy representation is more useful than either pure lineage;
- no useful alternative survives matched-cost comparison.

Expected local wins must be preregistered rather than rediscovered after measurement:

- Factor-resident composition, exact cancellation, divisibility, valuation, GCD, and LCM should be coordinate-local.
- Magnitude-native, one-shot, addition-heavy, increment-heavy, comparison-heavy, and large-prime workloads should usually favor binary magnitude.
- Dense coordinates should favor repeated work over a small fixed basis; sparse coordinates should favor low support; neither is expected to dominate across distributions.

An expected result is not an established result. A failed expected win is especially valuable evidence.

## 2. Claims this software can and cannot operationalize

### 2.1 Operationally testable

- Exact functional equivalence on bounded domains.
- Counts of abstract gates, state cells, logical depth, state transitions, memory bits, memory accesses, representation bits, primitive operations, and conversions.
- Runtime and allocation behavior of specified implementations on a recorded host.
- Whether costs move from an operation into input construction, catalog construction, normalization, factorization, reconstruction, output materialization, or caching.
- Whether an operation is local to a representation under a precise locality definition.
- Injectivity, canonicality, finite-domain code-space density, and preimage counts.
- Area/latency/throughput tradeoffs in an abstract synchronous netlist, and, if later synthesized, in a named technology/library.

### 2.2 Not operationally settled by this build

- What physical computation in general requires. A bit-and-gate simulator is already an abstraction running on conventional hardware.
- Actual energy, heat, reliability, metastability, manufacturability, or device physics. Net toggles are only a switching proxy.
- Whether history could really have taken a different path. Counterfactual plausibility is not an executable measurement.
- A general asymptotic advantage from finite benchmarks, especially where integer factorization complexity is involved.
- A new mathematical foundation. Taking opaque generators as primitive constructs a free commutative monoid; it does not by itself establish that those generators are number-theoretic primes.
- A general reversible-computing advantage. That requires reversible embeddings and accounting for ancilla and garbage, not suggestive notation.

### 2.3 Ill-posed prompt assumptions that must be split before testing

1. **Prime identity without integers.** A number-theoretic prime is defined relative to a multiplication/divisibility structure on natural numbers. A substrate can instead take opaque atoms and multiplicity as primitive, but calling the atoms primes is semantic labeling until a verified mapping to ordinary primes is supplied.
2. **Prime index as primitive.** An ordinal prime index assumes order, succession, and an index namespace. If the index is represented numerically, ordinary magnitude has not disappeared. If identity is encoded by wire position, the identity cost has moved into hardware lanes and catalog wiring.
3. **Representation size.** There is no meaningful unqualified statement that one unbounded representation uses fewer bits. Every comparison needs a bounded semantic set or an explicit input distribution and prefix code.
4. **Multiplication is simple.** It is coordinatewise exponent addition only when both operands are already canonically factorized and the result remains in range. Input factorization, exponent addition, overflow, identity metadata, and required output reconstruction still count.
5. **Addition destroys information.** Exact factor and magnitude encodings of a positive integer are bijective on a declared domain. Addition changes factorization, but any loss of operand information comes from discarding operands in a many-to-one binary operation, not from one representation uniquely losing an intrinsic object.
6. **Below software.** Build 000 can implement an auditable model of low-level components. It cannot escape the host language, compiler, operating system, or physical computer on which the model runs.

These are not reasons to stop. They define the honest experimental boundary.

## 3. Common substrate

Both lineages must compile to, or be traceable to, the same substrate. Representation-specific code may not invoke a hidden host arithmetic operation and still claim a substrate-level cost.

### 3.1 Synchronous abstract machine

The minimum shared substrate is:

- Signal: exactly one of two defined values, 0 or 1.
- Wire: directed connection; fan-out is legal and counted.
- NAND2: the sole primitive combinational gate.
- DFF: a one-bit, edge-triggered state cell with documented reset behavior.
- Clock step: combinational logic settles in topological order, then all DFFs update simultaneously.
- Port: an ordered sequence of signals; ordering is representational metadata and must be declared.

NAND2 is chosen for functional completeness, not because it is physically universal or optimal. DFF is admitted as an explicit state primitive. A NAND-latch exercise may expose how feedback creates memory, but analog timing, hazards, and metastability remain out of model.

Legal netlist invariants:

- every input has exactly one driver;
- there are no combinational cycles;
- every feedback cycle crosses at least one DFF;
- widths match at every port;
- reset reaches a documented state;
- evaluation is deterministic for every legal input and state;
- no signal silently takes an unknown or high-impedance value;
- overflow and invalid-domain status are explicit output signals, never host exceptions hidden from the trace.

### 3.2 Two execution levels

Use two related execution levels and never mix their claims:

1. **Structural level:** NAND2/DFF netlists for small widths. This is the primary source for gate count, DFF count, depth, fan-out, and toggle traces.
2. **Functional level:** bit-vector and explicit data-structure implementations for larger widths and workload experiments. This is the primary source for algorithm steps, allocation, memory traffic, and host runtime.

Every functional primitive should have at least one small structural realization or be marked NOT_STRUCTURALLY_REALIZED. A host big integer is permitted only as a differential-test oracle or as an explicitly labeled optimized conventional baseline, never as the hidden implementation of a claimed gate-level primitive.

### 3.3 Shared cost record

Each run emits a machine-readable record with at least:

- experiment ID and implementation ID;
- source and output domain;
- parameter manifest and deterministic seed;
- git commit and dirty-worktree indicator;
- toolchain, dependency, operating-system, CPU, and memory identifiers;
- correctness status and first counterexample if any;
- static NAND2 count, DFF count, connection count, maximum fan-out, and register-to-register logic depth;
- cycles, NAND evaluations, net transitions, register writes, abstract memory bit reads/writes, and peak live bits;
- logical payload bits, metadata bits, catalog bits, allocated bytes, and peak resident bytes;
- conversion, normalization, factorization, reconstruction, and cache-hit counts;
- setup/precompute time and bytes, separately from steady-state work;
- wall-clock samples, warmup policy, trial order, median, dispersion, and timeout/censor status.

Do not collapse this vector to one “cost.” If an area-time product is useful, sweep multiple DFF-to-NAND area weights rather than selecting one convenient constant. Report Pareto fronts for area, latency, throughput, memory, and transitions.

Net transitions are not joules. Abstract gate count is not silicon area. Host runtime is not hardware latency.

## 4. Reconstructing the conventional path

This phase shows exactly where quantity and ordinary successor enter the model.

### E000 — Distinguishable-state lower bound

For state-cell counts 0 through 12, enumerate all reachable bit patterns and verify that k independent two-state cells distinguish at most 2^k states. For requested identity counts M, verify that a payload needs at least ceil(log2 M) bits unless some identity is encoded in location, timing, or external wiring; charge that alternative resource explicitly.

Invariant: two semantic identities may not share one complete machine state if later behavior is required to distinguish them.

Purpose: prevent a “primitive prime symbol” from being treated as a zero-cost identity.

### E001 — Gate basis

Exhaust all four NAND2 input rows. Construct NOT, AND, OR, XOR, XNOR, MUX, half-adder, and full-adder netlists from NAND2 and exhaust their truth tables.

Invariants:

- derived truth tables equal their independent Boolean specifications;
- netlist evaluation order does not alter outputs;
- equivalent circuits may differ in cost but not semantics.

Record structural costs for every realization. If multiple standard constructions are tried, retain their Pareto frontier rather than choosing after seeing lineage results.

### E002 — State and memory

Exercise DFF reset and all input sequences through length 12. If enable exists, exhaust enable/input/reset combinations. Construct a small register and a finite state machine.

Invariants:

- q at step t+1 equals the documented transition from q and inputs at t;
- simultaneous state updates do not observe partially updated peers;
- reset/enable priority is explicit;
- all reachable states are enumerated for small machines.

Optional NAND SR-latch work must label forbidden inputs and cannot make analog stability claims.

### E003 — Quantity is an interpretation of state

Build modulo-M counters for M from 2 through 32 in at least:

- one-hot/ring encoding;
- ordinary binary encoding;
- Gray encoding where a legal cyclic code exists.

Measure state cells, gates, depth, transitions per successor, invalid states, reset cost, and decode cost. Exhaust every state and at least two full cycles.

Invariants:

- exactly M intended states form the intended cycle;
- the displayed labels 0 through M-1 are an external decoding map;
- behavior from invalid encodings is specified as trap, self-repair, or unconstrained and tested accordingly.

This experiment should demonstrate, rather than merely assert, that sequential quantity can be represented in multiple ways. It also prevents low toggle count, low state count, and easy decoding from being conflated.

### E004 — Conventional arithmetic baseline

Build, from the shared substrate:

- fixed-width register;
- ripple-carry ADD with carry/overflow;
- SUB/COMPARE;
- shift;
- at least one transparent multiplier, preferably sequential shift-add;
- optional array multiplier as a different area/latency point;
- exact division or GCD only if implemented without host arithmetic shortcuts.

For fairness, retain two conventional baselines:

- **C-transparent:** simple circuits and algorithms whose primitive costs are auditable.
- **C-optimized:** host/compiler or synthesized implementations used only for practical runtime comparison.

Never compare an aggressively parallel prime unit only to a single reused binary ALU and call the difference architectural. Generate both area-reused and throughput-oriented designs where feasible.

## 5. The earliest coherent fork

The fork should occur after finite state, transition, storage, and identity are available, but before a particular quantity encoding and arithmetic unit are chosen.

The alternative branch starts with a finite set of generator identities and natural multiplicities. Algebraically this is a bounded representation of a free commutative monoid. It becomes a representation of positive integers only after a catalog maps each generator bijectively to a verified ordinary prime and decode is defined by multiplication.

This distinction must remain visible in types and results:

- Generator coordinate: opaque generator IDs and exponents.
- Prime coordinate: generator IDs plus a verified catalog mapping to ordinary primes.
- Magnitude: conventional nonnegative binary value.

An experiment on generator coordinates can establish structural properties of composition. It cannot by itself establish a result about integer factorization or prime-number hardware.

## 6. Competing representations

Initially restrict semantic arithmetic to positive integers. Encode 1 as an empty/all-zero coordinate vector. Zero has no finite prime factorization and must either be excluded or carried by an explicit tag. Signed values need a sign tag. Report tag costs. Do not silently map zero to the empty vector.

At minimum compare:

### B-Fixed — fixed-width binary magnitude

- n payload bits, fixed range, explicit overflow policy.
- Also include a fair variable-length binary representation when variable-length prime forms are compared.

### U-Tally — diagnostic unary control

- Quantity is represented by a count of marks or asserted lanes.
- Concatenation can make addition look structurally trivial while storage, canonicalization, comparison, and fixed-capacity overflow become expensive.
- Keep this control to small bounds; it is not proposed as a scalable lineage.

U-Tally is an important adversarial control. If a prime-coordinate “multiplication advantage” has exactly the same character as unary addition applied independently to exponent lanes, report it as a representation expansion/locality trade rather than a new prime-specific primitive.

### P-Lane — dense fixed-catalog exponent lanes

- One lane per catalog prime.
- Lane identity resides in position/wiring.
- Each exponent has a declared maximum and a fixed binary width.
- Charge catalog ROM/wiring once, lane hardware always, and payload bits per resident value.
- A variant with unary/thermometer exponents may be tested, but its unused lane capacity and normalization circuitry count.

For primes p_i and exponent caps E_i:

- raw payload bits = sum over i of ceil(log2(E_i + 1));
- coordinate-box cardinality = product over i of (E_i + 1);
- maximum decoded magnitude = product over i of p_i raised to E_i;
- values above a declared magnitude bound are invalid even when their coordinates fit.

Binary exponent lanes still rely on ordinary bounded integer addition. Unary exponent lanes move that arithmetic into marks and capacity. Report which choice is used; neither permits the claim that ordinary arithmetic disappeared.

### P-Sparse — canonical sorted coordinate pairs

- Only nonzero exponents are stored.
- Key, exponent, length, delimiter/capacity, and allocation costs all count.
- Pairs are strictly key-sorted, keys are unique, exponents are positive, and zero exponents are omitted.

Test three identity schemes separately:

1. prime value as key;
2. ordinal prime rank as key, with prime table/ranking/unranking cost charged;
3. bounded catalog slot as key, with catalog state charged.

Ordinal rank is not a free primitive. Prime-value keys are not “factor only” metadata-free. Slot keys trade payload bits for fixed catalog/hardware resources.

### P-Bag — optional token multiset or lazy composition tree

This candidate is included to test whether constant-time append merely defers canonicalization:

- token-bag stores one token per factor occurrence;
- lazy tree stores COMPOSE nodes and factor leaves;
- composition may be O(1) structurally, but equality, cancellation, comparison, serialization, canonical output, and memory retention must force or otherwise account for normalization.

P-Bag is a dead end if its only advantage disappears when a required observable is materialized.

### H-Dual — conditional hybrid

After pure baselines are established, test a record that may hold magnitude, factors, or both, each with a validity bit:

- multiplication may update factors and invalidate magnitude;
- addition may update magnitude and invalidate factors;
- conversion is explicit and cached only while storage is charged;
- eviction and cache invalidation are deterministic.

This is a candidate experiment, not the assumed architecture. It is valuable precisely because it can reveal that the useful fork is a domain-aware cache rather than a replacement number system.

### G-Opaque — nonnumeric generator ablation

Run the same coordinate structures with generator labels that have no mapping to ordinary primes. Composition, cancellation, support, componentwise minimum, and componentwise maximum remain defined in the free commutative monoid.

If a cost advantage survives unchanged under this ablation, it is an advantage of factorized symbolic/monomial representation, not evidence for a specifically prime-native machine. Prime-specific content begins where verified prime catalogs, magnitude reconstruction, divisibility semantics, or number-theoretic algorithms enter.

## 7. Canonicality and arithmetic invariants

### 7.1 Encoding invariants

For every declared bounded domain:

- decode(encode(n)) = n for every supported n;
- encode(decode(c)) = canonicalize(c) for every valid coordinate c;
- canonical encodings are injective;
- malformed, duplicate-key, unsorted, zero-exponent, out-of-catalog, and overflowed encodings are rejected or explicitly normalized;
- 1 has exactly the documented representation;
- zero and sign tags, if supported, do not collide with positive values;
- catalog version is part of the representation identity.

### 7.2 Operation invariants

Where results remain in domain:

- decode(compose(a,b)) = decode(a) × decode(b);
- compose is commutative and associative and has the encoding of 1 as identity;
- exact-cancel(a,b) succeeds iff decode(b) divides decode(a), and its decoded result is the quotient;
- coordinate GCD uses componentwise minimum;
- coordinate LCM uses componentwise maximum;
- valuation returns the selected exponent;
- divisibility is componentwise exponent comparison;
- factor equality agrees with magnitude equality;
- numeric comparison agrees with decoded magnitude comparison; componentwise order is not accepted as numeric order;
- ADD agrees with magnitude addition regardless of the algorithm used;
- overflow, out-of-basis, timeout, and invalid-input states are distinct.

Test distributivity only as a semantic arithmetic property after decoding. Do not infer that ADD is coordinate-local from the existence of a correct conversion path.

### 7.3 Locality definition

An operation is representation-local only if it:

- reads and writes only the representation and fixed declared metadata;
- does not call factorization, reconstruction, an uncharged oracle, or a magnitude cache;
- has a cost trace expressed entirely in the common primitives or declared functional primitives;
- produces the required output form, not an unevaluated promise unless the workload explicitly permits lazy output.

“Fast because not yet forced” is deferred work, not a local result.

## 8. Conversion and addition experiments

### E100 — Catalog identity costs

For catalog sizes K and prime ranges up to at least 8, 12, 16, and 24 bits, compare value-key, ordinal-key, and lane-key identity.

Measure:

- bits per identity and total catalog bits;
- prime generation or table construction;
- ranking/unranking cost;
- lane decoder/multiplexer cost;
- lookup locality and memory traffic;
- behavior for a prime not in the catalog.

Adversarial case: a single large prime. It should expose whether the design assumes an enormous dense catalog, an expensive ordinal mapping, or a key almost as wide as the magnitude.

### E101 — Factor and reconstruct boundaries

Implement at least one transparent bounded factorizer and reconstructor. If an optimized library algorithm is added, keep it as a separate implementation.

Test four source/output modes:

- M→M: magnitude input, magnitude output;
- M→F: magnitude input, factor output;
- F→M: factor input, magnitude output;
- F→F: factor input, factor output.

For each operation report:

- cold end-to-end cost, including input conversion and demanded output materialization;
- warm resident cost, excluding a prior conversion but charging resident storage;
- cached cost over reuse count Q, including cache construction, capacity, and eviction;
- break-even Q, if one exists.

Precomputed smallest-prime-factor tables, prime catalogs, or memoized factorizations have separate construction time and bytes. Show both cold and amortized results. Unlimited uncharged cache is forbidden.

### E102 — Addition strategies

Prime-form addition must be attacked with at least these strategies:

1. reconstruct both operands, binary ADD, refactor if factor output is demanded;
2. extract the componentwise common factor, reconstruct/add the coprime residuals, then refactor only the residual sum if factor output is demanded;
3. optional lazy magnitude or dual-cache strategy.

Input families:

- unrelated smooth operands;
- operands sharing a very large common factor;
- coprime operands;
- consecutive integers;
- pairs whose sum is prime;
- pairs whose sum has a large prime factor;
- repeated increment by one;
- balanced additions in a large reduction tree.

The common-factor strategy is useful only if total end-to-end cost improves; reducing the apparent size of the refactored residue is not by itself a win.

### E103 — Exact comparison

Compare:

- binary lexicographic/bit comparison;
- full reconstruction then comparison;
- log-sum estimate with certified error bounds and exact fallback;
- any direct factor-domain exact algorithm discovered later.

Adversarial cases must include distinct, extremely close large values. Floating log estimates may choose a fast path only when interval bounds prove the result; otherwise they must fall back. A wrong comparison is a correctness failure, not an acceptable approximation.

### E104 — Prime-specificity ablation

Replace every catalog prime with an opaque stable symbol while preserving coordinate shapes and operation traces. Rerun compose, cancel, support queries, and componentwise min/max.

Classify each observed advantage as:

- generic to free commutative monoids or sparse monomials;
- dependent on a prime-to-magnitude catalog;
- dependent on integer unique factorization;
- dependent on the tested workload already supplying factors.

This ablation is required before describing a structural win as genuinely prime-specific.

## 9. Exhaustive, property, differential, and formal testing

### 9.1 Exhaustive structural tests

- Every derived gate: full truth table.
- DFF/register/state machine: all state/input combinations and bounded traces.
- Binary circuits: every operand pair for widths 1 through 8; multiplication uses a sufficiently wide output or explicit checked overflow.
- Coordinate units: every legal coordinate tuple for small catalogs and exponent caps, including every overflow boundary.
- Encode/decode and arithmetic: all positive magnitudes through at least 255, all pairs where practical, and all demanded output modes.
- Serialization/validation: all bitstrings through a small bounded encoded length, classified as one canonical value, invalid, or an explicitly documented noncanonical form.

Exhaustive tests compare against independent mathematical specifications, not another code path sharing the same bug.

### 9.2 Randomized and property tests

Use fixed, recorded seeds and at least 10,000 generated cases per important representation/width/distribution cell unless a manifest records a justified resource limit.

Widths should include 16, 32, 64, 128, and 256 bits at the functional level. Structural simulation may stop earlier and must report that boundary.

Properties include:

- round trips and canonicality;
- arithmetic identities and metamorphic relations;
- differential equality across B-Fixed, P-Lane, P-Sparse, and the independent oracle;
- operation sequences, not only isolated calls;
- cache validity after mixed operations;
- stable serialization across process runs and catalog versions;
- exact trap/overflow agreement;
- no mutation of preserved source operands unless specified.

Use shrinking to retain minimal counterexamples. Preserve failing seeds and serialized inputs as repository artifacts.

### 9.3 Differential oracles

An established arbitrary-precision integer implementation is acceptable as a top-level oracle. An independently implemented slow trial-division factorizer is acceptable for small differential domains. Neither may be called by the implementation under test during a costed operation.

Cross-check optimized factor libraries against the independent oracle on bounded inputs. Record probable-prime versus proven-prime status if probabilistic routines enter later work.

### 9.4 SAT/SMT and model checking

Where productive:

- prove or exhaustively discharge NAND netlist equivalence to bit-vector ADD, SUB, COMPARE, and small MUL specifications;
- check state-machine reachability and illegal-state handling;
- prove small coordinate compose/cancel circuits equivalent to their exponent-vector specification;
- obtain counterexamples for any failed equivalence.

Formal verification of an implementation does not prove that the representation is advantageous, and testing unique factorization does not re-prove the theorem.

### 9.5 Fuzzing

Fuzz variable-length coordinate parsers, catalogs, malformed sparse pairs, overflow edges, serialization, and the conditional VM decoder. Important classes:

- duplicate or descending keys;
- enormous length headers;
- zero or negative exponents if the format forbids them;
- catalog mismatch;
- exponent and allocation overflow;
- truncated streams;
- aliasing and cache-invalidity sequences.

Distinguish WRONG_RESULT, INVALID_ACCEPTED, CRASH, TIMEOUT, RESOURCE_LIMIT, and INFRASTRUCTURE_FAILURE.

## 10. Cost models and fair accounting

### 10.1 Representation cost

Report at least:

- semantic domain cardinality;
- logical payload bits;
- tag, length, key, alignment, pointer, and capacity overhead;
- fixed catalog/ROM/wiring state;
- physical allocated bytes;
- maximum and mean resident size under each input distribution;
- fraction of raw codewords that are canonical valid values;
- ideal information lower bound log2(domain cardinality);
- redundancy relative to that bound and relative to a fair binary encoding of the same semantic set.

For a noncontiguous smooth-number set, also report a hypothetical rank encoding lower bound. It may be computationally expensive, but it prevents dense coordinates from being credited for sparsity that any enumerative code could exploit.

### 10.2 Operation cost

Separate:

- representation-local work;
- conversion;
- normalization;
- setup/precompute;
- demanded output materialization;
- cache maintenance;
- error/overflow handling.

Primary metrics are exact primitive counts. Host time is secondary and requires:

- pinned build mode and dependency versions;
- isolated warmup;
- randomized benchmark order;
- multiple trials;
- no correctness assertions or logging inside only one timed implementation;
- recorded CPU affinity/power policy where controllable;
- confidence or dispersion without claiming independence that is not present.

### 10.3 Hardware-style tradeoffs

For each arithmetic unit produce at least two budget points if feasible:

- area-reused/sequential;
- throughput-oriented/parallel.

Compare Pareto fronts using:

- NAND2 and DFF counts;
- maximum combinational depth;
- cycles/operation and steady-state initiation interval;
- net transitions;
- I/O and register width;
- peak live state.

Coordinate lanes can be parallel but wide. Binary multiplication can be sequential but narrow. Neither latency nor area alone decides the comparison.

If HDL synthesis is later used, pin the synthesizer, script, target library, constraints, and seed. Keep abstract-netlist and synthesized results separate.

### 10.4 Algorithmic scaling

Fit or plot empirical scaling only over measured ranges. Include exact operation-count formulas where derivable, but do not infer a factorization complexity class from the fit.

Report censored cases at fixed time/memory budgets. Never drop semiprimes or large primes because they make a graph untidy.

## 11. Benchmark matrix

Every arithmetic workload must declare:

- source representation;
- demanded output representation or predicate;
- lifespan/reuse count of each value;
- allowed catalog/cache/precompute state;
- width/range and overflow semantics;
- input distribution;
- implementation budget point.

### W000 — Primitive counters and state encodings

Compare ring, binary, and Gray counters across M. This reconstructs successor before arithmetic and measures the price of state labeling.

### W010 — Single-operation cold path

One ADD, MUL, exact DIV, DIVIDES, GCD, LCM, VALUATION, EQUAL, and numeric COMPARE under all M/F source-output modes. Include U-Tally at tractable small bounds as a locality/expansion control. This is the strongest test for displaced conversion cost.

### W020 — Factor-resident composition chains

Repeated products of factor-native inputs for Q in 1, 2, 4, 8, 16, 64, 256, 4096. Demand factor output, magnitude output, and only a divisibility predicate in separate runs.

Track exponent growth and overflow. Include balanced trees and left-associated chains.

### W021 — Exact cancellation chains

Compose a product, then cancel known factors in randomized order. Include failed non-divisors. Preserve original operands so reversibility claims can be assessed separately.

### W022 — Divisibility, valuation, GCD, and LCM service

Many queries over a resident corpus, varying query count and cache capacity. This is a likely favorable niche and should be implemented well enough not to create a straw win.

### W030 — Addition and successor

- repeated increment from 1;
- random additions;
- running sum;
- balanced reductions;
- add then multiply;
- multiply then add;
- Euclidean-style update sequences if supported.

Demand both magnitude and factor outputs. Record how often factor state becomes invalid or must be rebuilt in H-Dual.

### W031 — Mixed arithmetic DAGs

Generate reproducible DAGs with multiplicative:additive operation ratios of 100:0, 90:10, 50:50, 10:90, and 0:100. Vary reuse/fan-out and output demands.

For lazy forms, count retained nodes and force every final observable. A DAG is not complete if expensive nodes remain unobserved.

### W040 — Numeric comparison and sorting

Pairwise comparisons, min/max reductions, and sorts over binary, smooth, prime, and semiprime distributions. Include close-value adversaries for log-based comparison.

### W050 — Memory and locality

Arrays of values with sequential scan, random access, update, composition, and query patterns. Sweep dense/sparse occupancy and cache sizes. Measure payload, allocation, pointer chasing, bit traffic, and catalog access.

### W060 — Conversion break-even

For each favorable factor-local kernel, sweep reuse count Q until:

total factor path = encode + Q × local work + demanded decode + storage/cache

is lower than the matched magnitude path, if ever. Report no crossover when none is observed within measured bounds.

### W070 — Optional hybrid/lazy challenge

Run the same mixed DAGs through B-Fixed, pure factor forms, H-Dual, and P-Bag. Force identical final outputs. Charge validity bits, both cached forms, invalidation, eviction, normalization, and retained DAG nodes.

The hybrid wins only if it reaches a Pareto point unavailable to both pure forms, not merely because it stores both without paying for both.

## 12. Required input families and pathologies

Use distributions that represent different source semantics:

- uniform n-bit magnitude;
- uniformly sampled values from a bounded interval;
- K-smooth values generated natively from coordinates;
- prime powers;
- square-free products and primorial prefixes;
- values with many repeated factors;
- n-bit primes;
- balanced semiprimes;
- one large prime times a small smooth part;
- consecutive and near-consecutive values;
- values sharing a controlled large GCD;
- highly composite values;
- factor-native expression trees.

Always include:

- 1 and the all-zero exponent vector;
- zero as excluded or explicitly tagged;
- sign handling as excluded or explicitly charged;
- the largest in-range exponent and one-step exponent overflow;
- the largest in-catalog prime and the first out-of-catalog prime;
- sparse support 0, 1, K/2, and K;
- duplicate/noncanonical sparse keys;
- products that fit coordinates but exceed the declared magnitude bound;
- smooth operands whose sum is rough or prime;
- balanced semiprimes that stress factorization;
- close unequal values that stress approximate comparison;
- a one-shot magnitude-native multiply, where factorization has no reuse;
- an extremely long factor-resident query sequence, where conversion can amortize.

Dataset construction must not contaminate timed operation cost, but source semantics must remain explicit. For example, generating semiprimes from known factors is valid for an F-native dataset and invalid as an uncharged shortcut for an M-native factorization workload.

## 13. Information, identity, and reversibility experiments

### I000 — Finite encoding census

For small bounds enumerate:

- semantic values;
- raw codewords;
- canonical valid codewords;
- invalid/noncanonical codewords;
- collisions under decode;
- representation lengths and empirical distributions.

Verify injectivity and compute code-space density and redundancy.

### I001 — Operation preimages

For bounded operand pairs, count preimages of ADD and MUL outputs. Repeat after coordinate encoding.

Expected interpretation to test: changing representation does not make a two-input/one-output arithmetic function reversible. Multiple operand pairs still map to one result when operands are discarded.

### I002 — Reversible embeddings

Compare reversible-style mappings that preserve enough input:

- binary modular add: (a,b) maps to (a, a+b mod 2^n);
- coordinate bounded add with explicit overflow/status and preserved operand;
- checked nonnegative coordinate addition without modular wrap;
- compose/cancel sequences with and without retained operands.

Account for input registers, status, ancilla, garbage, and uncomputation. Coordinate addition is not invertible from its sum alone. Binary modular addition is already bijective when one operand is retained. Any advantage must appear in a complete circuit cost, not a verbal claim.

### I003 — Exact conversion

Verify that factorization and reconstruction are mutually inverse on the supported positive domain. Label them lossless but potentially expensive. Zero, sign, catalog mismatch, overflow, and probabilistic factor status prevent an unqualified bijection claim.

### I004 — Identity location

Move the same generator identity among payload key, ordinal table, and physical lane. Show which bit/state/wiring cost moves. This tests the conservation-of-identification intuition without asserting a physical law.

## 14. Benchmark integrity and evidence handling

- Freeze manifests before viewing aggregate results.
- Use deterministic data-generation seeds and publish generators.
- Keep correctness runs distinct from performance runs but require correctness gates first.
- Preserve raw JSONL/CSV, manifests, tool versions, summaries, plots, and all counterexamples.
- Mark missing measurements as NOT_MEASURED, resource censoring as RESOURCE_LIMIT, and infrastructure faults as FAILED_INFRASTRUCTURE.
- Do not impute timeouts as measured runtimes.
- Plot every required distribution, including those unfavorable to the motivating hunch.
- Include absolute values as well as ratios; ratios with a near-zero denominator can mislead.
- Compare equivalent output obligations. A factor result is not equivalent to a magnitude result unless the consumer requested either form.
- Separate discovery runs from confirmation runs. If parameters are changed after viewing results, label the later run exploratory until reconfirmed on a frozen manifest.

## 15. Conditional instruction-set gate

No VM instruction set is earned at the design stage. The experiment runner may use operation names as a benchmark IR, but that is not evidence that they belong in a machine ISA.

A factor-aware VM extension may be built only if all of these hold:

1. At least two nontrivial factor-resident workload families show a reproducible Pareto improvement after catalog, storage, and required I/O are charged.
2. The advantage survives a matched area/throughput comparison and a competent conventional implementation.
3. A concrete reuse or source-semantics regime makes conversion amortization plausible.
4. The representation has explicit zero/sign/range/catalog behavior.
5. The proposed operations are closed or trap explicitly; none invokes an uncosted factor oracle.
6. The result is confirmed on a frozen manifest not used to choose the instruction set.

If that gate is passed, start with a **dual-bank extension**, not a claimed replacement general computer:

- factor registers hold one chosen canonical factor representation;
- magnitude registers hold ordinary bit vectors;
- ordinary load/store/branch/control comes from the already built common machine;
- MAG_TO_FACT and FACT_TO_MAG are explicit, costed boundary operations;
- FCOMPOSE is checked coordinate addition;
- FCANCEL is exact checked coordinate subtraction.

Add FMIN/FMAX for GCD/LCM or FVAL only if measured kernels justify dedicated instructions over loops or lane primitives. Do not add an instruction merely because it has an appealing name. DECOMPOSE must never mean “factor instantly”; it is either omitted or is a measured factorization routine behind MAG_TO_FACT.

The minimality test is empirical:

- remove each candidate opcode;
- lower its workloads to the remaining primitives;
- retain it only if its removal materially worsens a confirmed Pareto point or makes required semantics impossible.

If the gate fails, record “no VM earned.” A library or typed intermediate representation may still be the correct artifact.

## 16. Experiment sequence and decision gates

### Phase A — substrate

Run E000–E004. Gate: all primitive invariants pass; costs are traceable; ordinary successor is reconstructed in at least two encodings.

### Phase B — representations

Implement B-Fixed, P-Lane, and P-Sparse; P-Bag is optional. Gate: exhaustive canonicality and round-trip tests pass; catalog and metadata costs are recorded.

### Phase C — local arithmetic

Run compose, exact cancel, divisibility, valuation, GCD, and LCM at structural and functional levels. Gate: differential correctness passes and favorable results remain after matched parallelism/area sweeps.

### Phase D — boundaries and disruptive operations

Run E100–E104, especially cold M→M and F→F variants. Gate: no hidden conversion or lazy-output debt remains, and generic monoid behavior is not mislabeled prime-specific.

### Phase E — workload matrix

Run W000–W070 across frozen distributions, widths, reuse counts, and output demands. Produce Pareto fronts and crossover curves.

### Phase F — information and reversibility

Run I000–I004. Gate: all claims distinguish injectivity, information, operand discard, and reversible embeddings.

### Phase G — architecture decision

Apply the conditional ISA gate. Choose among:

- stop: no useful alternative;
- specialized factor representation/library;
- factor-aware coprocessor or ISA extension;
- dual/lazy representation;
- further foundational experiment prompted by an unexpected result.

## 17. Minimum evidence for Build 000 conclusions

A conclusion that prime-native composition is “simpler” must identify:

- representation and catalog;
- source/output mode;
- domain and overflow behavior;
- resident/precompute assumptions;
- static area and depth;
- dynamic cycles/transitions/memory;
- conversion and output costs;
- matched binary baseline;
- workloads where the claim fails.

A conclusion that addition is “harder” must distinguish:

- factor output demanded versus magnitude output demanded;
- conversion/reconstruction from factorization of the result;
- shared-factor cases from coprime/rough-sum cases;
- one operation from amortized mixed workloads.

A conclusion about a new architecture must survive:

- exhaustive small-domain correctness;
- randomized differential sequences;
- required pathologies;
- resource and output parity;
- a confirmation manifest;
- removal/ablation of each proposed primitive.

## 18. Planned reproducibility contract

The eventual implementation should expose stable tasks with these semantics, regardless of language or build tool:

- test-primitives: exhaustive gate/state/circuit tests;
- test-arithmetic: exhaustive, property, differential, and malformed-input tests;
- verify-netlists: SAT/SMT equivalence where available;
- generate-datasets: deterministic, manifested workload corpora;
- run-experiments: execute a frozen manifest and append raw records;
- summarize-results: derive tables/plots without editing raw evidence;
- reproduce-build000: run the confirmation subset from a clean checkout.

Exact shell commands cannot honestly be fixed before the implementation language and toolchain are chosen. Once chosen, pin them in the repository and make the tasks above one-command reproducible.

## 19. Recommended Build 000 stopping rule

Stop when:

- both lineages share a tested primitive substrate;
- at least one dense and one sparse factor representation have been compared to fixed and variable binary magnitude;
- conversion and output obligations have been crossed in both directions;
- addition-heavy, multiplication-heavy, factor-query, mixed, memory, and adversarial workloads are measured;
- all material correctness claims have exhaustive small-domain and randomized differential evidence;
- information/reversibility language has passed the finite-domain tests;
- an ISA has either passed the explicit gate or been rejected;
- the observed Pareto regions are stable enough to select one narrow Build 001 question.

Do not stop merely when coordinate multiplication works. Conversely, do not continue adding machinery after every plausible advantage is dominated and the remaining question requires a different physical platform or an unbounded complexity result.

The most informative likely output is not a universal replacement for binary magnitude. It is a map of where factor structure is already present, how long it remains resident, which observables are required, and exactly when the cost of crossing representation boundaries dominates. That map must be earned by the experiments above.
