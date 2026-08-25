# Hardware Mathematics

This is a post-circuit interpretation of Build 002. It describes mathematics that is visible in the committed C# semantic models, declared NAND graphs, and HDL; it did not specify those artifacts in advance. It reports neither synthesized optimality nor physical area, timing, or energy. No structure below is claimed as new mathematics or new computer architecture. The source boundary and closest precedents are recorded in [Hardware Prior Art](HARDWARE_PRIOR_ART.md), accessed 2026-08-24.

Notation is fixed to hardware widths `W in {4,6,8}`, prime catalog `S4 = (2,3,5,7)`, and lane cap

`T_p(W) = floor(log_p(2^W - 1))`.

An exact nonzero structural state has exponent vector `e = (e_p)`, with `0 <= e_p <= T_p(W)`. “Native” below means local to an already resident representation. It never means physically primitive, free to acquire, or sufficient for exact general integer arithmetic.

## 1. NAND switching algebra

### Hardware fact

[`NandNetlistBuilder.Nand`](../src/PrimeAxiom.Core/Hardware/NandNetlist.cs) is the sole combinational cell constructor in the declared C# graph. [`NandLogic`](../src/PrimeAxiom.Core/Hardware/BaselineHardware.cs) builds `NOT`, `AND`, `OR`, `XOR`, `XNOR`, muxes, adders, subtractors, and comparators from it; the declared constructions use respectively 1, 2, 3, 4, 5, and 4 NAND2 cells for `NOT`, `AND`, `OR`, `XOR`, `XNOR`, and a 2:1 mux. The corresponding HDL floor in [`pa_nand.sv`](../hdl/rtl/pa_nand.sv) likewise derives its Boolean primitives from `pa_nand2`. Constants and direct nets are represented explicitly but are not silently charged as NAND gates.

### Mathematical abstraction

For two-state inputs, the cell computes the Sheffer operation `a | b = not(a and b)`. The two-element Boolean algebra and functional completeness of this operation explain why both numerical lineages can share exactly the same combinational floor. This algebra determines extensional truth values; it does not describe voltage, time, hazards, metastability, or fabrication.

### Cost consequence

At this rung, an exact claim is limited to the constructed graph: NAND2 instances, modeled dependency depth, ports, nets, sinks, fanout, and region crossings. Different representations induce different Boolean graphs above the same complete basis, but a smaller declared graph is not automatically a smaller optimized netlist or physical implementation. FPGA LUT mapping, standard-cell mapping, routing, and timing remain later and target-specific evidence.

### Prior art

Single-connective presentations of Boolean algebra are established by Sheffer’s 1913 [“A Set of Five Independent Postulates for Boolean Algebras”](https://doi.org/10.1090/S0002-9947-1913-1500960-1). Shannon’s 1938 [“A Symbolic Analysis of Relay and Switching Circuits”](https://doi.org/10.1109/T-AIEE.1938.5057767) established the switching-algebra bridge used by digital circuit design. Build 002 applies this inherited floor; it does not rediscover it.

### Build 002 result and remaining implication

The experiment shows that distinct resident interpretations map to different declared and optimized Boolean cost vectors on one substrate. It cannot infer that either mathematics is physically fundamental merely because one NAND decomposition is smaller at these widths.

## 2. Registers and finite state

### Hardware fact

[`NandNetlist`](../src/PrimeAxiom.Core/Hardware/NandNetlist.cs) distinguishes combinational nodes from explicit `State` nodes and DFF boundary metadata, and reports DFFs separately from NAND2 cells. The conventional lineage includes registered operands and results. The current C# experimental functional unit is combinational; using it as persistent resident machinery requires the register, status, and control boundaries to be added and charged. The HDL modules similarly expose combinational transformations whose integration state is a separate architectural obligation.

### Mathematical abstraction

A clock-delimited machine with finitely many state bits is a finite-state transducer: current state and current inputs select next state and outputs. The same set of bit states can be interpreted as a counter, a positional word, a valuation vector, threshold knowledge, or control. Nothing in a DFF chooses that interpretation; the transition and decoding maps do.

### Cost consequence

Representation changes the cardinality and shape of architectural state before it changes arithmetic. Exponent bits, thermometer thresholds, zero tags, saturation flags, exactness bits, magnitude shadows, and operation status are all state when persisted. Operation-only combinational graphs therefore cannot support an integrated advantage claim until equal register boundaries and state-transition costs are included.

### Prior art

Mealy’s 1955 [“A Method for Synthesizing Sequential Circuits”](https://doi.org/10.1002/j.1538-7305.1955.tb03788.x) is primary prior art for treating sequential circuits through explicit state, input, transition, and output behavior. Readiness-driven or dataflow execution can expose independent valuation lanes, but Dennis and Misunas’s [data-flow processor](https://doi.org/10.1145/642089.642111) changes scheduling rather than the arithmetic representation itself.

### Build 002 result and remaining implication

Already resident valuation state did not repay acquisition, registers, validity control, and eventual egress across the frozen whole-machine comparisons, although it reduced local warm structural work. That is a result for these workloads and transparent machines, not a theorem about finite-state architectures; reuse thresholds remain a legitimate target for a narrower sequential design.

## 3. Binary positional magnitude and order

### Hardware fact

The conventional lineage in [`BaselineHardware.cs`](../src/PrimeAxiom.Core/Hardware/BaselineHardware.cs) uses unsigned LSB-first `W`-bit operands, ripple-carry addition, borrow-producing subtraction, unsigned comparison, a `W x W` shift-and-add multiplier with `2W`-bit product, and a native functional unit. Overflow, borrow, comparison predicates, and result widths are exposed rather than hidden. These circuits were constructed from the common NAND library, not delegated to host integer operations during evaluation.

### Mathematical abstraction

A word `b_(W-1)...b_0` denotes `sum_i b_i 2^i` in `[0,2^W-1]`. Positional place value makes magnitude order a total order and gives addition its familiar carry recurrence. Depending on the declared result port, a word operation realizes exact bounded arithmetic with a status bit, a wider exact result, or arithmetic modulo `2^W`; those are different contracts.

### Cost consequence

Binary magnitude directly supports ordinary comparison, addition, and bit shifts, while carries and partial products create cross-position dependencies. The Build 002 ripple and shift-add circuits are transparent conventional controls, not presumed optimal multipliers or adders. Any comparison against valuation lanes must preserve width, overflow, input preparation, and output obligation.

### Prior art

Conventional multiplier design extends far beyond shift-add: Booth’s [signed recoding](https://doi.org/10.1093/qjmam/4.2.236), Wallace’s [parallel multiplier](https://doi.org/10.1109/PGEC.1964.263830), Dadda’s [parallel schemes](https://ieeemilestones.ethw.org/File%3ASome_schemes_for_parallel_multipliers_%28reprint%29.pdf), and Baugh and Wooley’s [two’s-complement array](https://doi.org/10.1109/T-C.1973.223648) are established controls. A result against the transparent baseline must not be generalized to every conventional arithmetic architecture.

### Build 002 result and remaining implication

Build 002 located representation-dependent dependency patterns at tiny widths and preserved both lineages through one synthesis flow. It did not show that carry-like dependency is globally avoidable: the valuation lineage relocated work into conversion, status, comparison, and addition recovery. Optimized netlists at W4/W6/W8 are bounded mapping evidence, not an optimality result for either lineage.

## 4. Binary exponent lanes and bounded composition

### Hardware fact

The S4 binary-exponent representation stores each lane in `ceil(log2(T_p+1))` bits plus saturation status, with a separate zero tag. [`ValuationHardwareState`](../src/PrimeAxiom.Core/Hardware/ValuationHardwareState.cs), the declared circuits in [`ExperimentalHardware.cs`](../src/PrimeAxiom.Core/Hardware/ExperimentalHardware.cs), and [`pa_binexp.sv`](../hdl/rtl/pa_binexp.sv) implement lane-local compose, checked cancel, meet, join, divisibility, valuation/power constructors where exposed, and explicit saturation or rejection. Compose clamps a lane to its cap and marks loss of exactness when `e_p + f_p > T_p`; checked cancellation rejects atomically if a nonzero dividend would underflow or the divisor is zero. The full exponent box is legal even when `product p^(e_p)` exceeds the `W`-bit magnitude domain, so reconstruction performs a separate common-domain check.

### Mathematical abstraction

Without caps, finite-support prime exponents form the free commutative monoid on the primes, and an S4-smooth positive integer corresponds to a vector in `N^S4`; multiplication is vector addition. The exact bounded hardware states form the product of finite chains

`C_W = product_(p in S4) {0,...,T_p(W)}`.

`C_W` is not closed under ordinary vector addition. A saturated lane is therefore not an element carrying a larger exact exponent: it carries only the lower bound `e_p >= T_p` and makes later exact operations partial. The implementation’s clamp-plus-flag transition must not be renamed the unbounded free-monoid law.

### Cost consequence

Resident multiplication becomes four independent small additions, and exact division becomes four independent subtractions plus aggregate rejection. The apparent locality is purchased with bank identity, lane widths, saturation status, zero handling, acquisition, and reconstruction. It is strongest for warm structural workloads and weakest when operands arrive as magnitudes or every step demands an exact magnitude.

### Prior art

Prime-exponent coordinates and their laws are established in Mathlib’s [factorization definitions](https://leanprover-community.github.io/mathlib4_docs/Mathlib/Data/Nat/Factorization/Defs.html) and [factorization lemmas](https://leanprover-community.github.io/mathlib4_docs/Mathlib/Data/Nat/Factorization/Basic.html). Wells’s [*Elements of Combinatorial Computing*](https://shop.elsevier.com/books/elements-of-combinatorial-computing/wells/978-0-08-016091-7) treats prime-factor representation among computer representations. Factor-form software and bounded prime extraction documented in [Build 001 prior art](PRIOR_ART_BUILD001.md) further rule out a novelty claim for the representation or its local multiplication law.

### Build 002 result and remaining implication

The complete registered matrix answers the general version negatively under its strict-Pareto rule: status bits, persistent state, and crossings prevent a hardware-advantage classification. It answers the local version positively as a tradeoff: the bounded algebra is markedly smaller and shallower for warm structural SCALE/CANCEL traces. That earns a specialized instruction-domain candidate, not an alternative foundation and not a Pareto win.

## 5. Thermometer thresholds as chain ideals

### Hardware fact

For lane cap `T`, the thermometer form stores `t_k = 1` exactly when `e >= k`, for `1 <= k <= T`. Canonical vectors are true prefixes followed by false suffixes; the C# and HDL validators reject a later rise from `0` to `1`. [`ExperimentalHardware.cs`](../src/PrimeAxiom.Core/Hardware/ExperimentalHardware.cs) and [`pa_therm.sv`](../hdl/rtl/pa_therm.sv) construct meet with bitwise `AND`, join with bitwise `OR`, divisibility with threshold implication, fixed-threshold query by selection, and compose by direct threshold convolution. Compose sets output threshold `k` when some split `i + j = k` is witnessed by the two input prefixes, and separately reports `e+f > T` saturation.

### Mathematical abstraction

The canonical encoding of exponent `e` is the initial segment, or order ideal,

`I_e = {1,...,e}`

of the finite chain `{1,...,T}`. Hence `I_min(e,f) = I_e intersection I_f`, `I_max(e,f) = I_e union I_f`, and `e <= f` exactly when `I_e` is a subset of `I_f`. A product of these chain-ideal lattices is distributive. Convolution realizes bounded addition of ranks; it is not lattice join.

### Cost consequence

Thermometer storage spends `T_p` bits where binary exponents spend `ceil(log2(T_p+1))`. In return, meet, join, fixed-threshold predicates, and inclusion expose very shallow per-threshold Boolean structure. Direct compose requires cross-threshold terms and canonical validation, so “carry-free” does not mean connection-free or cheaper. Only measured whole-graph and later mapped results can locate a crossover.

### Prior art

Order-ideal representations and distributive lattices are classical; Birkhoff’s [“Rings of Sets”](https://doi.org/10.1215/S0012-7094-37-00334-X) is primary prior art for set representations of distributive lattices. Componentwise factorization order is already formalized by the Mathlib sources above. Build 002’s thermometer vector is a hardware encoding of those established structures.

### Build 002 result and remaining implication

For the frozen small caps, order ideals made threshold, meet, and join logic direct but spent more state and moved composition into convolution, validation, and adapters. That representation did not produce the registered unexpected-architecture or whole-machine Pareto result. Predicate-only sequential reuse remains the more precise follow-up question.

## 6. Divisibility order, meet, and join

### Hardware fact

For exact nonzero structural inputs, the binary-exponent circuit implements `DIVIDES` by four unsigned lane comparisons and conjunction; the thermometer circuit implements the same predicate by threshold implications. `MEET` and `JOIN` use lane minima/maxima in binary form and threshold intersection/union in thermometer form. The operation contracts include explicit zero cases and reject saturated or malformed inputs when an exact answer is unavailable.

### Mathematical abstraction

On positive S4-smooth integers, `a` divides `b` exactly when `v_p(a) <= v_p(b)` for every bank prime. Under this order, componentwise minimum is `gcd`, componentwise maximum is `lcm`, and the finite exponent box is a distributive lattice. This is divisibility order, not magnitude order: neither `e <= f` componentwise nor lexicographic comparison of exponent vectors determines ordinary numerical order for arbitrary pairs.

### Cost consequence

Already resident lanes can answer a predicate or catalog-supported meet/join without reconstruction. That does not make a full binary GCD or LCM free: unsupported factors may contribute, and a magnitude output still requires reconstruction or an authoritative magnitude shadow. Predicate-only, structural-result, and magnitude-result contracts must remain separate.

### Prior art

The factorization/divisibility/gcd/lcm correspondence is established in Mathlib’s [natural factorization library](https://leanprover-community.github.io/mathlib4_docs/Mathlib/Data/Nat/Factorization/Basic.html). Binary magnitude also has serious dedicated controls, including Brent and Kung’s [systolic GCD](https://maths-people.anu.edu.au/~brent/pub/pub082.html), Yun and Zhang’s [carry-free extended GCD](https://doi.org/10.1145/32439.32455), and Guyot’s [GCD coprocessor](https://doi.org/10.1109/ARITH.1991.145564). Lane-local `min` alone does not establish an end-to-end hardware advantage over them.

### Build 002 result and remaining implication

The frozen matrix found a real warm structural advantage but not strict Pareto dominance: the resident S4 machine saved NANDs and depth on its local workload while using more DFF, state, and port bits, and cold acquisition or magnitude reconstruction erased the advantage under frequent crossings. A still-narrower sequential predicate engine may remain useful when many questions target one fixed catalog, but that is now a Build 003 candidate rather than an unmeasured Build 002 claim.

## 7. Zero as a tagged extension

### Hardware fact

Pure exponent and thermometer states carry an explicit zero tag and require their canonical zero payload and saturation flags to be clear. The implemented contracts make zero absorbing for compose, use `meet(0,n)=n`, `join(0,n)=0`, define nonzero values as divisors of zero, define zero as dividing only zero, and reject cancellation by zero. A valuation query on semantic zero returns positive infinity rather than a finite lane value.

The exact C# BIN+VSC semantic model and corrected HDL cold encoder use the same query-oriented extension: magnitude/`zero` is authoritative, every finite threshold predicate is true, and `Valid=true`. An earlier superseded HDL run instead emitted `zero=1`, clear thresholds, and `valid=0`; the later cross-contract audit rejected that mismatch and preserved it as a failed design receipt. The final convention still requires the zero indication before finite payload bits are interpreted as an ordinary exponent.

### Mathematical abstraction

No vector in `N^S4` represents integer zero because every finite product of prime powers is positive. Extending valuations by `v_p(0)=+infinity` recovers `v_p(0*n)=+infinity` and the familiar divisibility conventions, but `+infinity` is not one of the bounded finite exponents. The tag is therefore a genuine sum-type extension, not a fifth ordinary exponent value.

### Cost consequence

Zero adds state, detection, muxing, and operation-specific control. Encoding it as clear structural payload is compact but makes payload bits meaningless without the tag; encoding every finite divisibility threshold as true is query-friendly but also requires the magnitude/tag to distinguish infinity from a large finite valuation. The pre-repair C#/HDL mismatch demonstrated why such conversions cannot remain implicit; the final integrated comparison uses the converged all-true-threshold contract.

### Prior art

Special zero handling is standard in valuation-related instructions: the ratified RISC-V Zbb [`ctz`](https://docs.riscv.org/reference/isa/unpriv/b-st-ext.html) returns the register width on an all-zero operand rather than mathematical infinity. Mathlib’s factorization and valuation APIs similarly treat zero separately from positive prime-factor coordinates. Build 002’s tag is an implementation obligation, not a new number-theoretic object.

### Build 002 result and remaining implication

Build 002 converged the semantic model, cold encoder, sidecar operations, self-checking simulation, and formal harnesses on one explicit zero/valid contract. Clear thresholds now distinguish only a finite failed threshold or unknown/invalid metadata according to the accompanying validity state; they are never used as an implicit zero encoding. Any future representation adapter must preserve that tagged distinction, especially if it compresses the all-true infinity payload.

## 8. Addition, valuation lower bounds, and nonclosure

### Hardware fact

The pure structural VFU exposes no ordinary integer addition instruction. [`BinaryValuationSidecar.Add`](../src/PrimeAxiom.Core/Hardware/ValuationHardwareState.cs) keeps the exact binary magnitude authoritative, rejects word overflow, and retains the per-prime common lower bound `min(v_p(a),v_p(b))`. Without refresh, its single global exactness bit remains true only when both inputs were exact and every S4 lane has unequal input valuations; otherwise retained true thresholds are lower-bound facts and clear thresholds are unknown. The HDL BIN+VSC control in [`pa_acquisition_sidecar.sv`](../hdl/rtl/pa_acquisition_sidecar.sv) represents an unmodelled structure-transforming operation by invalidating the exact sidecar rather than inventing post-addition exponents.

### Mathematical abstraction

For every prime valuation,

`v_p(ab) = v_p(a) + v_p(b)`

and, for a nonzero sum,

`v_p(a+b) >= min(v_p(a),v_p(b))`.

When the two valuations differ, equality holds. When they are equal, writing `a=p^r u` and `b=p^r v` shows that the answer also depends on `v_p(u+v)`; the exponent coordinates alone omit this unit/residue information. Thus ordinary addition is not closed as an exact operation on valuation vectors, even though it has a sound minimum lower bound.

### Cost consequence

The representation makes multiplication local by moving exact general addition toward an authoritative magnitude datapath, a refresh, or additional residue/unit state. The C# model makes this displaced cost visible through invalidation and refresh rather than returning a plausible but false exponent. An addition-heavy workload is therefore an adversarial test, not an inconvenient exception to omit.

### Prior art

The valuation laws are established in the Stacks Project, [Lemma 10.50.14](https://stacks.math.columbia.edu/tag/00IF). Logarithmic number systems provide the closest architectural control for the same cost displacement: Mitchell’s [binary-logarithm arithmetic](https://doi.org/10.1109/TEC.1962.5219391), Swartzlander and Alexopoulos’s [sign/logarithm system](https://doi.org/10.1109/T-C.1975.224172), and Lewis’s [LNS addition architecture](https://doi.org/10.1109/12.61042) all predate Build 002.

### Open implication

The next mathematical-hardware question is how much additional per-prime unit or residue information makes selected additions exact, and whether maintaining it costs less than refreshing from binary magnitude. Any such extension must still handle carries between knowledge states, zero, overflow, and unsupported factors.

## 9. Unsupported factors as an exact projection boundary

### Hardware fact

Pure S4 structural states represent only S4-smooth positive values plus tagged zero. The C# BIN+VSC model and HDL `pa_bin_vsc_s4` instead retain a `W`-bit magnitude as the exact authoritative value while S4 thresholds carry selected divisibility facts. Multiplication or exact cancellation by a known catalog prime updates the matching threshold lane and magnitude; primes outside S4 remain present in magnitude and are neither silently discarded nor automatically discovered. The HDL cold encoder computes only S4 thresholds, while published trial-division precedent also shows the alternative of emitting an explicit residual quotient.

### Mathematical abstraction

The map

`nu_S(n) = (v_p(n))_(p in S)`

is a projection and is not injective: values differing only by factors outside `S` share coordinates. An exact normalized decomposition restores the missing information as

`n = c * product_(p in S) p^(v_p(n))`, with `gcd(c, product S)=1`.

Keeping the entire magnitude instead produces a redundant exact representation: magnitude identifies `n`, while the threshold sidecar certifies selected facts about it.

### Cost consequence

Componentwise operations on S4 lanes are full integer operations only for declared S4-smooth operands. With arbitrary magnitudes they are projections: two cofactors may share an unsupported prime that changes full gcd/lcm or cancellation. Exact discovery, normalization, refresh, and bank migration require charged divisibility, gcd, or factor extraction; the sidecar’s memory and maintenance are also costs.

### Prior art

Southern et al.’s [small-prime trial-division FPGA](https://www.hyperelliptic.org/tanja/SHARCS/talks07/record.pdf) directly emits prime/exponent pairs plus a residual quotient. PARI/GP’s documented [`Z_smoothen`](https://pari.math.u-bordeaux.fr/dochtml/html-stable/usersch5.html) and FLINT’s [bounded factorization interfaces](https://flintlib.org/doc/fmpz_factor.html) likewise retain exact residual cofactors after selected-prime extraction. Exact partial factor information is established prior art.

### Open implication

The empirical problem is bank policy: whether a fixed S4 projection supplies enough repeated value to justify its overhead, and whether a larger, adaptive, sparse, or associative bank improves that trade without hiding lookup, miss, migration, and residual costs.

## 10. Why `p=2` is uniquely wiring-native in binary magnitude

### Hardware fact

Inside an already resident exponent bank, all four prime lanes use the same families of local add, subtract, compare, threshold, and status logic, modulo their different caps. The current transparent HDL cold encoder in [`pa_acquisition_sidecar.sv`](../hdl/rtl/pa_acquisition_sidecar.sv) deliberately uses the same equality-minterm constant-divisibility construction for every S4 threshold, including powers of two; `pa_bin_vsc_s4` likewise uses shared multiplier and exact-constant-divider structures. It therefore has not silently taken a special `p=2` optimization. The asymmetry is nevertheless exposed at the ordinary binary port: for nonzero `n`, `v_2(n)` is the index of the least significant set bit, so `v_2(n) >= k` is determined by the lowest `k` bits alone.

### Mathematical abstraction

Binary positional notation is expansion in radix `2`. For `n != 0`, factoring out the largest radix power is exactly factoring out `2^(v_2(n))`, so the radix and the prime coincide. No odd prime is a power of the binary radix; divisibility by `3^k`, `5^k`, or `7^k` is not characterized by a fixed suffix of zero bits. This uniqueness is relative to binary representation, not to switching physics or Boolean algebra.

### Cost consequence

An explicitly optimized `p=2` acquisition and known-factor scale/cancel path should be reported separately from odd-prime lanes and from the current uniform truth-table control. Multiplication or exact cancellation by `2^k` can use bounded shifts and direct bit placement, but a complete count-valued `v_2` unit still needs priority/count logic, zero handling, and result encoding; shifts still need overflow or exact-divisibility checks. “Wiring-native” therefore means an available positional path, not a claim of zero cost or a metric already earned by the current circuit. Odd primes require remainder/division or a synthesized equivalent whose target mapping may differ substantially from its declared NAND expansion.

### Prior art

The RISC-V Zbb specification exposes this path directly as [`ctz`](https://docs.riscv.org/reference/isa/unpriv/b-st-ext.html). Granlund and Montgomery’s [division by invariant integers](https://doi.org/10.1145/178243.178249), de Dinechin and Didier’s [table-based small-constant division](https://doi.org/10.1007/978-3-642-28365-9_5), and Gorodecky and Sousa’s [FPGA constant divider](https://doi.org/10.1109/ARITH58626.2023.00025) establish the nonuniform fixed-divisor design space. This is direct precedent for separating the `p=2` lane from odd-prime acquisition.

### Build 002 result and remaining implication

Build 002 kept the positional binary controls and the S4 structural lanes separately visible. The complete matrix did not show that persistent `v_2` metadata or the added odd-prime lanes repay their acquisition and state costs under the registered strict-Pareto rules. The positive result is narrower: once S4 structure is resident, repeated scale/cancel and predicate work has strong locality. A future design should isolate an optimized `v_2` path and time-multiplex odd-prime predicate logic instead of assuming that four always-resident lanes are the right unit.
