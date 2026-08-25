# Build 002 prior-art reconnaissance: same gates, different mathematics

Status: research note for integration, not a Build 002 result  
Access date for all web sources: 2026-08-24  
Controlling repository boundary: Build 000 and Build 001 reports and receipts are unchanged. Build 001 remains `PARTIAL — PILOT_NEGATIVE; FINAL DECISION NOT EARNED` and `PILOT_SUBSET_COMPLETE_FULL_CONFIRMATION_NOT_RUN`.

## Question and evidence labels

This note asks which parts of a binary, prime-coordinate hardware fork already have direct precedent and which are only useful comparisons. It does **not** make a novelty claim.

Labels used below:

- **DIRECT** — substantially the same representation, conversion, or circuit function has been built or specified.
- **CONTROL** — a serious alternative number system or conventional circuit that Build 002 should compare against.
- **ANALOGY** — an architectural idea that may help implementation but is not evidence for prime-native arithmetic.
- **SYNTHESIS CONSTRAINT** — a fact about what a gate, FPGA, or ASIC measurement can support.
- **BOUNDED SEARCH GAP** — no close source was located in this search, which is not evidence that none exists.

## Executive result

The strongest direct hardware predecessor is not a speculative prime computer. It is Southern, Mason, Chikkam, Baier, and Gaj's 2007 FPGA trial-division engine. Given a magnitude and a fixed set of small primes, its declared output is a sequence of prime/exponent pairs plus a residual quotient not divisible by any prime below the bound. That is almost exactly a hardware implementation of the Build 001 bounded-valuation ingress contract. It separates a fast, pipelined divisibility scan from a slower sequential quotient-producing divider that extracts repeated powers. It therefore supplies both precedent and a warning: prime-coordinate acquisition is a bank of constant-divisor work plus state, queues, and a residual, not a free relabeling.

No source found in this bounded search describes a general-purpose, persistent arithmetic unit whose architectural registers remain in exact prime-exponent-plus-cofactor form across multiply, exact divide, gcd/lcm, comparison, addition, normalization, and bank migration. The gap is narrower than “prime arithmetic hardware”: factor-form software, bounded-factor extraction hardware, GCD hardware, constant dividers, logarithmic arithmetic, and residue arithmetic are all substantial prior art.

The mathematical answer suggested by the prior art is plural rather than unique. The same Boolean substrate exposes different local algebras when state is encoded differently:

| State immediately above bits | Locally natural laws | Cost that reappears elsewhere |
|---|---|---|
| Positional binary word | Boolean bit operations; carry-chain addition; shifts; modular or checked word arithmetic | General multiply/divide networks and width/overflow policy |
| Fixed prime-exponent lanes | Exponent-vector addition; componentwise order; meet/min and join/max; cancellative subtraction when defined | Ingress factor extraction, residual arithmetic, reconstruction, basis escape, exponent overflow, exact ordinary addition |
| Sparse keyed exponent records | Finite-map union/merge plus exponent arithmetic | Key comparison, routing, allocation, canonical ordering, CAM or memory traffic |
| Residues modulo coprime moduli | Componentwise addition and multiplication in a product of finite rings | Magnitude comparison, sign, division, overflow detection, forward/reverse conversion |
| Logarithmic code | Multiplication/division become code addition/subtraction | Ordinary addition/subtraction require nonlinear correction and careful cancellation handling |

For a fixed bank `S`, the genuinely native exact structure is therefore not “the primes” in general. It is a finite product of bounded exponent machines plus an ordinary exact cofactor. On fully factorized positive values it realizes the free commutative monoid law, the divisibility product order, and its gcd/lcm lattice. Ordinary addition exposes only a valuation lower bound without further unit/residue information. Calling that lower-bound datapath a complete tropical arithmetic unit would overstate what the hardware knows.

## Ranked closest prior art

### 1. Bounded-prime trial-division FPGA: nearly the Build 001 ingress contract — **DIRECT**

Gabriel Southern, Chris Mason, Lalitha Chikkam, Patrick Baier, and Kris Gaj, “FPGA Implementation of High Throughput Circuit for Trial Division by Small Primes,” *SHARCS 2007 Workshop Record*, pp. 3–20:

- stable workshop record: [SHARCS 2007 Workshop Record](https://www.hyperelliptic.org/tanja/SHARCS/talks07/record.pdf)
- author/institution slides: [George Mason University slides](https://people-ece.vse.gmu.edu/~kgaj/publications/conferences/GMU_SHARCS_2007_slides.pdf)
- author-group download endpoint: [GMU Cryptographic Engineering Research Group](https://cryptography.gmu.edu/research/download.php?docid=944)

The paper defines the circuit problem as follows: accept `k`-bit integers and all primes below a bound `B`; emit every discovered `(p_i, e_i)` and a remaining `M_i` such that the input is `product(p_i^e_i) * M_i` and `M_i` is not divisible by any prime below `B`. The demonstrated design used 9,592 primes below 100,000, was designed and tested at 512 input bits, and was also synthesized at smaller cryptanalytic widths.

The architecture matters more than its historical performance claim:

- A pipelined array divider tests one new fixed prime per cycle after fill, producing a remainder but not a quotient.
- Zero remainders enqueue candidate factors.
- A multi-cycle sequential divider emits quotient and remainder and repeatedly divides to obtain the exponent.
- A ROM holds the fixed prime bank; FIFO capacity and a declared overflow mode are part of correctness and throughput.
- In overflow mode the published circuit may report prime identities without powers, which is precisely the kind of partial-knowledge state Build 002 must not silently read as exact.
- The design intentionally targets FPGA block RAM and dedicated carry resources. Its results cannot be transferred unchanged to NAND count or an ASIC library.

**Build 002 consequence:** cite this as direct prior art for `INGRESS_S`, `PROJECT/VALUATE`, and exact-cofactor output. The new experimental question is whether keeping those coordinates resident across a workload repays this already-built ingress machinery—not whether hardware can ever emit factor/exponent pairs.

### 2. Constant-divisor and trailing-zero circuits: valuation-lane acquisition — **DIRECT building blocks**

For `p = 2`, a positional binary word exposes `v_2(n)` as its count of trailing zero bits for nonzero `n`. The ratified RISC-V Zbb extension specifies `ctz` directly; its zero result is the register width, whereas the mathematical convention usually assigns `v_p(0) = infinity`, so zero still needs an explicit architectural state.

- [RISC-V Unprivileged ISA, Chapter 29, B extension](https://docs.riscv.org/reference/isa/unpriv/b-st-ext.html)

For an odd bank prime, exact extraction needs a quotient/remainder test and repetition, or tests against prime powers. Prior art includes:

- Torbjörn Granlund and Peter L. Montgomery, “Division by Invariant Integers using Multiplication,” PLDI 1994, [DOI 10.1145/178243.178249](https://doi.org/10.1145/178243.178249).
- Florent de Dinechin and Laurent-Stéphane Didier, “Table-Based Division by Small Integer Constants,” 2012, [DOI 10.1007/978-3-642-28365-9_5](https://doi.org/10.1007/978-3-642-28365-9_5), [author PDF](https://perso.citi-lab.fr/fdedinec/recherche/publis/2012-ARC-LUTConstDiv.pdf).
- H. Fatih Ugurdag, Florent de Dinechin, Yakup S. Gener, Sinan Gören, and Laurent-Stéphane Didier, “Hardware Division by Small Integer Constants,” *IEEE Transactions on Computers* 66(12), 2017, [DOI 10.1109/TC.2017.2707488](https://doi.org/10.1109/TC.2017.2707488), [author PDF](https://perso.citi-lab.fr/fdedinec/recherche/publis/2017-TC-ConstDiv.pdf).
- Danila Gorodecky and Leonel Sousa, “Scalable Architecture of Constant Division on FPGA,” ARITH 2023, [DOI 10.1109/ARITH58626.2023.00025](https://doi.org/10.1109/ARITH58626.2023.00025), [symposium PDF](https://arith2023.arithsymposium.org/papers/Scalable%20Architecture%20of%20Constant%20Division%20on%20FPGA.pdf), [official University of Lisbon record](https://researchportal.ulisboa.pt/en/publications/scalable-architecture-of-constant-division-on-fpga/).

The 2023 circuit is especially relevant at Build 002 widths: it is combinational, consists of adders and Boolean-function encoders, and emits quotient and residue. It also makes the target-dependence explicit: Boolean minimization is shaped by the ASIC cell library or the FPGA LUT width. Its paper notes that the same half-adder and full-adder decomposition can occupy the same LUT resources, directly demonstrating why a transparent NAND expansion and an FPGA resource result answer different questions.

**Build 002 consequence:** measure `v_2` separately from odd-prime lanes. At the binary floor, 2 is physically privileged by representation; 3, 5, and 7 require constant modular/division networks. A single undifferentiated “valuation unit” would conceal the most important asymmetry.

### 3. Factor-form values in computing systems — **DIRECT semantics, software rather than hardware**

Build 001 already records the closest operational software precedents and should remain the canonical repository account:

- PARI/GP `factorback`, `Z_smoothen`, and factor matrices/factorization forms;
- FLINT `fmpz_factor_t`;
- FriCAS `Factored(R)`;
- sparse symbolic factorization and factorized polynomial representations.

Mark B. Wells's *Elements of Combinatorial Computing* includes “Prime Factor Representation” immediately beside positional, Gray-code, and residue representations in its chapter on computer representations of natural numbers. That is direct historical evidence that the representational alternative is not new, but the source does not by itself establish a hardware ALU:

- [Elsevier book page](https://shop.elsevier.com/books/elements-of-combinatorial-computing/wells/978-0-08-016091-7)
- [publisher preview containing the table of contents](https://api.pageplace.de/preview/DT0400.9781483186665_A23861482/preview-9781483186665_A23861482.pdf)

**Build 002 consequence:** the hardware contribution must be stated as an empirical mapping/trade-off result at declared widths and targets. Prime-exponent notation, factor objects, and local multiplication laws are established prior art.

### 4. Logarithmic number systems: the closest operation-relocation control — **CONTROL**

LNS is not prime-exponent representation, but it is the most important direct control for the claim that a representation can turn multiplication/division into addition/subtraction while displacing the hard work to ordinary addition.

- John N. Mitchell, “Computer Multiplication and Division Using Binary Logarithms,” 1962, [DOI 10.1109/TEC.1962.5219391](https://doi.org/10.1109/TEC.1962.5219391).
- Earl E. Swartzlander Jr. and Aristides G. Alexopoulos, “The Sign/Logarithm Number System,” 1975, [DOI 10.1109/T-C.1975.224172](https://doi.org/10.1109/T-C.1975.224172).
- David M. Lewis, “An Architecture for Addition and Subtraction of Long Word Length Numbers in the Logarithmic Number System,” *IEEE Transactions on Computers* 39(11), 1325–1336, 1990, [DOI 10.1109/12.61042](https://doi.org/10.1109/12.61042).
- Barry R. Lee and Neil Burgess, “A Parallel Look-up Logarithmic Number System Addition/Subtraction Scheme for FPGA,” FPT 2003, 76–83, [DOI 10.1109/FPT.2003.1275734](https://doi.org/10.1109/FPT.2003.1275734).

Lewis's architecture uses small lookup tables and piecewise linear treatment of the nonlinear correction functions. This is not merely a notational observation: there is a hardware literature on paying the displaced addition cost.

**Build 002 consequence:** any claim that exponent-lane multiplication is structurally simple must be paired with a complete addition/egress path, just as LNS literature accounts for nonlinear add/sub correction. Prime lanes may retain exact discrete structure where finite-precision logs approximate; that is a real distinction, not permission to omit conversion cost.

### 5. Residue number systems: adversarial number-theoretic control — **CONTROL**

H. L. Garner's foundational hardware-oriented paper treats a number as its residues under pairwise-coprime moduli:

- H. L. Garner, “The Residue Number System,” *IRE Transactions on Electronic Computers* EC-8(2), 140–147, 1959, [DOI 10.1109/TEC.1959.5219515](https://doi.org/10.1109/TEC.1959.5219515).

Within the representable dynamic range, addition, subtraction, and multiplication are componentwise and carry-free across residue channels. Relative magnitude, division, sign, and conversion to/from a weighted code are the difficult side.

This is a stronger control than binary magnitude alone. It demonstrates that number-theoretic coordinates can make **both** addition and multiplication local. Therefore the multiplication-local/addition-disruptive asymmetry of prime valuations is a property of that coordinate map, not a general law of unconventional arithmetic.

**Build 002 consequence:** at 4/6/8 bits, include at least a paper design or synthesized RNS control with a dynamic range comparable to the tested word domain, and disclose conversion and range/overflow logic. Otherwise “number-theoretic hardware” is being compared to only one conventional lineage.

### 6. Binary GCD/extended-GCD hardware: competent conventional baseline — **CONTROL**

Prime coordinates make gcd/lcm componentwise min/max only after the relevant valuations are known. Binary GCD has substantial hardware precedent and should not be represented only by a high-level software Euclidean loop.

- Richard P. Brent and H. T. Kung, “Systolic VLSI Arrays for Linear-Time GCD Computation,” *VLSI 83*, 145–154, [author publication page and PDF](https://maths-people.anu.edu.au/~brent/pub/pub082.html). They give a linear systolic array with `O(n)` cells and `O(n)` time for `n`-bit integer GCD.
- Richard P. Brent and H. T. Kung, “A Systolic VLSI Array for Integer GCD Computation,” ARITH-7, 1985, indexed in [Brent's publication list](https://maths-people.anu.edu.au/~brent/pub/pubsall.html).
- D. Y. Y. Yun and C. N. Zhang, “A Fast Carry-Free Algorithm and Hardware Design for Extended Integer GCD Computation,” SYMSAC 1986, [DOI 10.1145/32439.32455](https://doi.org/10.1145/32439.32455).
- Alain Guyot, “OCAPI: Architecture of a VLSI Coprocessor for the GCD and the Extended GCD of Large Numbers,” ARITH 1991, [DOI 10.1109/ARITH.1991.145564](https://doi.org/10.1109/ARITH.1991.145564).

**Build 002 consequence:** compare the entire pipelines:

`binary inputs -> binary GCD unit -> binary result`

versus

`binary inputs -> bank valuation extraction -> lane min + residual GCD/contract handling -> reconstruction or structured result`.

If inputs are already resident factor objects, report that separately as a native-state workload rather than charging ingress to one lineage and not the other.

### 7. Associative/CAM arithmetic and sparse factor stores — **ANALOGY**

Associative processors show how arithmetic can be implemented as repeated compare/tag/write operations over many rows, often bit-serial within a word and massively parallel across words.

- Jack A. Rudolph and Kenneth E. Batcher, “A Productive Implementation of an Associative Array Processor: STARAN,” compiled from 1972/1974 primary accounts, [Computer History Museum scan](https://tcm.computerhistory.org/ComputerTimeline/Chap21_staran_CS2.pdf), [Gordon Bell archive HTML](https://gordonbell.azurewebsites.net/computer_structures_principles_and_examples/csp0333.htm).
- Kenneth E. Batcher, “Bit-Serial Parallel Processing Systems,” *IEEE Transactions on Computers* C-31(5), 377–384, 1982, [DOI 10.1109/TC.1982.1676015](https://doi.org/10.1109/TC.1982.1676015), [Kent State author-collection record](https://oaks.kent.edu/kenneth-e-batcher-collection/bit-serial-parallel-processing-systems).
- Robert A. Walker, Jerry Potter, Yanping Wang, and Meiduo Wu, “Implementing Associative Processing: Rethinking Earlier Architectural Decisions,” 2001, [author PDF](https://www.cs.kent.edu/~walker/papers/walker01.pdf).

A sparse prime-coordinate register file could use associative identity matching to locate lanes, merge equal prime keys, or broadcast an operation across matching keys. That is a plausible implementation analogy, not proof of an advantage. Fixed tiny banks are more naturally hard-wired or directly indexed; CAM introduces row storage, match lines, key/mask/tag registers, write cycles, routing, and energy.

**Build 002 consequence:** use CAM only as an explicitly costed sparse-bank variant. Compare against sorted sparse merge and dense fixed lanes. Do not count an associative lookup as one abstract operation while counting gates for the conventional path.

### 8. Dataflow hardware — **ANALOGY**

Jack B. Dennis and David P. Misunas's basic dataflow processor fires an operator when operand tokens are present and routes result tokens to successors:

- Jack B. Dennis and David P. Misunas, “A Preliminary Architecture for a Basic Data-Flow Processor,” ISCA 1975, 126–132, [DOI 10.1145/642089.642111](https://doi.org/10.1145/642089.642111), [primary paper mirror](https://www.cs.cmu.edu/~15740-f20/papers/dennis-75.pdf), [MIT Computation Structures Group archive](https://csg.csail.mit.edu/CSGArchives/memos.html).

Prime lanes and residual work can be independent tokens, and a structured arithmetic unit may benefit from readiness-driven issue. But dataflow changes scheduling, storage, and communication; it does not make factorization, reconstruction, or exact addition disappear.

**Build 002 consequence:** if a lane/token machine emerges, label the arithmetic representation and the execution model separately. Compare wire/token traffic and buffering, not merely available mathematical parallelism.

### 9. Reversible computing — **ANALOGY and accounting constraint**

- Rolf Landauer, “Irreversibility and Heat Generation in the Computing Process,” 1961, [DOI 10.1147/rd.53.0183](https://doi.org/10.1147/rd.53.0183).
- Charles H. Bennett, “Logical Reversibility of Computation,” 1973, [DOI 10.1147/rd.176.0525](https://doi.org/10.1147/rd.176.0525).
- Edward Fredkin and Tommaso Toffoli, “Conservative Logic,” 1982, [DOI 10.1007/BF01857727](https://doi.org/10.1007/BF01857727).

Exponent-vector composition `e_out = e_a + e_b` is not a reversible two-input/one-output operation if the inputs are discarded. Cancellation is not reversible when it erases which input contributed a factor. A reversible embedding must preserve inputs or history/ancilla and eventually uncompute garbage.

**Build 002 consequence:** reversibility is useful as an information-loss audit. It is not direct precedent for a prime ALU and should not be used to claim that factor cancellation is physically free.

### 10. Ternary and other nonbinary ALUs — **CONTROL outside the same-floor experiment**

The Setun lineage is direct evidence that hardware number systems have forked below binary, using balanced ternary machinery:

- [Lomonosov Moscow State University Laboratory of Ternary Informatics](https://ternarycomp.cs.msu.ru/)
- [MSU Setun and Setun-70 source collection](https://ternarycomp.cs.msu.ru/setun.html)
- [MSU laboratory history](https://cs.msu.ru/en/laboratories/tc?page=3)

Modern balanced-ternary circuit research continues, e.g. Behrooz Parhami, “Truncated Ternary Multipliers,” [DOI 10.1049/iet-cdt.2013.0133](https://doi.org/10.1049/iet-cdt.2013.0133).

This work is historically important but is not a same-NAND-substrate comparison unless the trits are encoded in binary gates. Physical ternary cells change the floor. Build 002 should cite the branch point and keep it out of a binary-NAND PPA table unless a disclosed binary encoding is synthesized.

## Conventional multiplication baselines

A transparent shift/add multiplier is educational, but it is not by itself a competent bound on conventional hardware multiplication.

Primary architectural sources include:

- Andrew D. Booth, “A Signed Binary Multiplication Technique,” 1951, [DOI 10.1093/qjmam/4.2.236](https://doi.org/10.1093/qjmam/4.2.236).
- C. S. Wallace, “A Suggestion for a Fast Multiplier,” 1964, [DOI 10.1109/PGEC.1964.263830](https://doi.org/10.1109/PGEC.1964.263830).
- Luigi Dadda, “Some Schemes for Parallel Multipliers,” *Alta Frequenza* 34, 349–356, 1965, [IEEE History Center reprint](https://ieeemilestones.ethw.org/File%3ASome_schemes_for_parallel_multipliers_%28reprint%29.pdf).
- C. R. Baugh and B. A. Wooley, “A Two's Complement Parallel Array Multiplication Algorithm,” 1973, [DOI 10.1109/T-C.1973.223648](https://doi.org/10.1109/T-C.1973.223648).

At Build 002's small widths, the comparison should include at least:

1. iterative/reused shift-add, reporting registers, controller, cycles, and throughput;
2. a combinational array or explicitly constructed partial-product network;
3. native synthesizable `*`, allowing the same tool to select carry/DSP resources on the target;
4. explicit signedness, truncation/full-product width, overflow behavior, and input/output registration.

Wallace/Dadda trees are useful controls when many partial products exist. At 4 bits an optimizer may collapse all named architectures into the same LUTs or cells; that collapse is an experimental result, not a reason to omit the baseline.

## The mathematics the lane hardware actually realizes

### Established structure

Mathlib documents an explicit equivalence between positive naturals and finitely supported natural-valued functions whose support consists of primes, together with factorization/divisibility and gcd/lcm lemmas:

- [Mathlib natural factorization definitions](https://leanprover-community.github.io/mathlib4_docs/Mathlib/Data/Nat/Factorization/Defs.html)
- [Mathlib natural factorization lemmas](https://leanprover-community.github.io/mathlib4_docs/Mathlib/Data/Nat/Factorization/Basic.html)

For fully known positive values:

- `factor(ab) = factor(a) + factor(b)` componentwise;
- `a divides b` iff every exponent of `a` is at most the corresponding exponent of `b`;
- `factor(gcd(a,b))` is the componentwise meet/min;
- `factor(lcm(a,b))` is the componentwise join/max;
- exact division, when divisibility holds, is componentwise subtraction.

Thus dense fixed lanes implement a product of small counter/comparator slices. Abstractly, unsaturated exponent lanes form `N^S` under addition, a cancellative commutative monoid; allowing rational exponents with signed integer valuations gives `Z^S`, the group completion for the chosen prime bank. Componentwise order makes gcd/lcm the meet/join of a distributive lattice.

Finite exponent registers alter the algebra. Saturating addition, trapping overflow, wraparound, and an explicit `Overflow` state are four different machines. Only the selected, tested policy may be named in a hardware claim.

### Valuation and the “tropical” resemblance

The Stacks Project records the valuation laws:

- `v(ab) = v(a) + v(b)`;
- `v(a+b) >= min(v(a),v(b))` when the sum is nonzero.

Source: [Stacks Project, Lemma 10.50.14](https://stacks.math.columbia.edu/tag/00IF).

For a discrete prime valuation, equality holds when the two input valuations differ. When they are equal, unit/residue information determines whether cancellation raises the output valuation. A lane containing only `v_p(a)` and `v_p(b)` cannot in general produce exact `v_p(a+b)`.

The hardware therefore has a real min-plus or tropical **shadow**:

- multiplication maps to exponent addition;
- a sum yields at least the minimum valuation;
- unequal lanes permit exact minimum;
- equal lanes require more information or must emit a certified lower bound/unknown state.

It does not implement ordinary integer addition as tropical `min`, and it does not by itself form a complete tropical semiring realization. If Build 002 emits `min` on equal valuations without a knowledge bit or unit calculation, it is wrong rather than approximate.

### Divisibility lattice versus arithmetic magnitude

The most coherent nonconventional ALU that emerges from the established laws is a **divisibility-lattice coprocessor**, not a replacement general-purpose integer ALU:

- `COMPOSE`: exponent add plus residual multiply;
- `CANCEL_EXACT`: checked exponent subtract plus residual division/contract work;
- `MEET`: lane min, corresponding to the known-factor part of gcd;
- `JOIN`: lane max, corresponding to the known-factor part of lcm;
- `LE_DIVIDES`: componentwise exponent comparison plus residual obligations;
- `PROJECT_p`: expose exponent/knowledge state;
- `REFRESH_p`: charged constant-division extraction from the cofactor;
- `RECONSTRUCT`: charged powers/products into positional magnitude.

This machine's native order is divisibility, not magnitude. `12` and `18` are incomparable in divisibility order even though ordinary comparison orders them. A magnitude comparator therefore requires reconstruction, bounds/log metadata, or a separate positional shadow, each of which must be costed.

## NAND count, depth, and what synthesis can establish

### NAND is a declared logical basis, not a technology-neutral unit

The repository's `GateNetwork.Nand` rule makes two-input NAND count and modeled NAND depth exact **for that constructed netlist**. It supports bottom-up transparency and comparisons on one declared basis. It does not provide transistor count, FPGA LUT count, standard-cell area, routed delay, energy, or an optimizer-independent lower bound.

Official Yosys documentation makes the separation concrete:

- [`abc` gate mapping](https://yosyshq.readthedocs.io/projects/yosys/en/0.47/cmd/abc.html) can target selected gate sets such as NAND/NOR or a Liberty library.
- [Mapping to cell libraries](https://yosyshq.readthedocs.io/projects/yosys/en/v0.56/using_yosys/synthesis/cell_libs.html) maps internal logic to the cells and area/timing data of a selected library.
- [Yosys/ABC FPGA mapping](https://yosyshq.readthedocs.io/projects/yosys/en/v0.64/using_yosys/synthesis/abc.html) maps to LUTs and explains that basic ABC uses a simplified unit-delay LUT model and is unaware that endpoints may be I/O, flip-flops, memories, or DSPs.
- [Berkeley ABC](https://people.eecs.berkeley.edu/~alanmi/abc/abc.htm) performs logic optimization and technology mapping for LUTs and standard cells, with heuristic area recovery and delay-aware mapping.

Consequences for receipts:

- Give the exact expansion rules used for inverter, XOR, mux, half adder, full adder, register enable, and comparator cells.
- State whether fanout, buffers, wires, and sequential storage are outside the NAND model.
- Report combinational NAND depth and sequential cycle latency separately.
- Do not compare hand-expanded NAND for one design with optimized HDL for another as if the difference were architectural.
- If both are synthesized, synthesize both from behaviorally equivalent boundaries with the same tool/version/script/constraints and preserve the mapped netlists and reports.

### FPGA mapping is not NAND mapping

AMD's 7-series CLB documentation specifies 6-input LUTs, flip-flops, distributed memory/shift-register modes, wide muxes, and dedicated high-speed carry logic:

- [AMD UG474 CLB overview](https://docs.amd.com/r/en-US/ug474_7Series_CLB/CLB-Overview)
- [AMD UG474 carry logic](https://docs.amd.com/r/en-US/ug474_7Series_CLB/Carry-Logic)

Dedicated carry chains strongly favor adders, subtractors, counters, and comparators; DSP blocks may absorb multiplication. A dense exponent-vector unit is also rich in small adders/comparators and may benefit from those same carry resources. The result is empirical: “prime hardware uses no multipliers” is not equivalent to “prime hardware uses fewer FPGA resources.”

At minimum publish, per target and seed if applicable:

- LUTs by type, flip-flops, carry primitives, DSPs, BRAMs;
- critical path and requested/achieved clock;
- latency and initiation interval;
- warnings about inferred latches, trimming, constant propagation, or unused outputs;
- exact top-level I/O and whether registers are included.

### ASIC synthesis is library- and flow-dependent

`abc -liberty` maps to the supplied standard-cell library. Cell area, pin timing, drive strength, buffering, fanout, wire load, clocking, power assumptions, process/voltage/temperature corner, and constraints affect the result. Pre-layout cell area and logic depth are not post-route PPA.

OpenROAD's official design-space documentation explicitly grades evidence by stage:

- quick synthesis: cell count and logic depth;
- full synthesis: area and rough timing;
- placement/routing: congestion, utilization, and increasingly realistic wire delay;
- detailed route plus parasitic extraction: the signoff-grade rung.

Source: [OpenROAD Design Space Exploration documentation](https://openroad.readthedocs.io/en/latest/contrib/DesignSpaceExploration.html); tool source and flow entry point: [OpenROAD repository](https://github.com/The-OpenROAD-Project/OpenROAD).

**Build 002 claim ceiling:** unless a physical flow is completed and its inputs are preserved, say “mapped standard-cell estimate” or “post-synthesis estimate,” not “ASIC area,” “silicon speed,” or “energy advantage.”

## Fair-comparison checklist derived from the sources

1. Use one semantic contract for values, zero, sign, overflow, invalid/unknown knowledge, and output exactness.
2. Keep native-state workload and end-to-end workload separate.
3. Charge prime bank ROM/configuration and validate primality/distinctness once in a disclosed configuration cost.
4. Charge each odd-prime ingress lane; isolate the `p=2` CTZ special case.
5. Include exponent and cofactor registers, control/FIFOs, and failure states—not only combinational lane arithmetic.
6. Ensure a failed or overflowed producer cannot leave stale valid output, preserving the Build 001 VM rule.
7. Match output semantics. A structured gcd lower bound is not an exact reconstructed gcd.
8. Match throughput. One-cycle combinational results and multi-cycle iterative units need area/latency/initiation-interval vectors, not a single score.
9. Run at least iterative shift-add, array/native multiply, binary GCD hardware, LNS, and RNS controls where the experiment's claim touches them.
10. Preserve pre-optimization and post-mapping reports. Constant propagation at tiny widths can erase the very hardware intended for comparison.
11. Use the same target, clock, I/O registration, synthesis version, script, effort, and seed for paired architectures.
12. Treat NAND model, FPGA mapping, ASIC mapping, and post-route estimates as separate evidence classes.

## Bounded search gap and no-novelty boundary

Searches included combinations of:

- `prime factor representation arithmetic hardware processor exponent vector`;
- `prime exponent FPGA arithmetic`;
- `factorized/factored representation arithmetic unit hardware`;
- `valuation extraction hardware constant divider`;
- `trial division FPGA prime powers quotient remainder`;
- `associative arithmetic CAM processor`;
- `GCD VLSI systolic hardware`;
- `logarithmic/residue number system arithmetic hardware`.

Sources were followed to primary papers, author/institution copies, DOI landing records, official ISA/vendor manuals, and official synthesis-tool documentation. General web summaries and patents were not used to support technical claims.

What was **not** located:

- a general-purpose persistent register file of exact prime-exponent vectors plus exact cofactors;
- a published ALU/ISA centered on compose, cancel, valuation projection, divisibility meet/join, and charged reconstruction;
- a fair gate/FPGA/ASIC comparison of that machine against competent binary, RNS, and LNS controls;
- a hardware knowledge-state protocol distinguishing exact valuations, lower bounds, hidden cofactor copies, overflow, zero, and invalid results across ordinary addition.

This is a bounded search gap, not a novelty finding. Before any later novelty language, search IEEE Xplore, ACM DL, INSPEC/Scopus, Espacenet/Google Patents, dissertations, non-English computer-arithmetic literature, and cited/citing networks for Southern et al., Wells, and the constant-divider papers.

## Recommended prior-art framing for the Build 002 report

The narrow defensible framing is:

> Build 002 did not invent factor-form values, valuation laws, constant-divider circuits, hardware extraction of small-prime powers, logarithmic arithmetic, residue arithmetic, GCD hardware, associative arithmetic, or dataflow/reversible execution. Its experiment places an exact bounded-valuation-plus-cofactor contract on the same binary gate substrate as a conventional word ALU and measures where the resulting divisibility-lattice operations survive conversion, state, and synthesis costs.

The most interesting remaining gap is not “can multiplication become exponent addition?” It is:

> Does a persistent, knowledge-carrying divisibility-state machine have a workload and physical mapping in which saved repeated work exceeds the acquisition, residual, routing, and reconstruction burden—and can that be shown against RNS/LNS and competent binary controls on identical targets?

That is a Build 002/003 engineering question. Prior art supplies the components and the mathematical laws; only reproducible implementation can decide whether their composition is useful.

## Compact source register

All links accessed 2026-08-24.

| Area | Primary or official source | Relationship |
|---|---|---|
| Bounded factor extraction | [Southern et al., SHARCS 2007](https://www.hyperelliptic.org/tanja/SHARCS/talks07/record.pdf) | Direct ingress/output precedent |
| Constant division | [Granlund & Montgomery 1994](https://doi.org/10.1145/178243.178249) | Direct fixed-divisor method |
| FPGA constant division | [de Dinechin & Didier 2012](https://doi.org/10.1007/978-3-642-28365-9_5) | Direct table/LUT divider |
| Small-constant hardware division | [Ugurdag et al. 2017](https://doi.org/10.1109/TC.2017.2707488) | Direct quotient/remainder building block |
| Scalable FPGA constant division | [Gorodecky & Sousa 2023](https://doi.org/10.1109/ARITH58626.2023.00025) | Direct combinational quotient/remainder circuit |
| `v_2` primitive | [RISC-V Zbb `ctz`](https://docs.riscv.org/reference/isa/unpriv/b-st-ext.html) | Direct binary-lane special case |
| Factor representation history | [Wells, publisher page](https://shop.elsevier.com/books/elements-of-combinatorial-computing/wells/978-0-08-016091-7) | Direct representation precedent |
| GCD VLSI | [Brent & Kung 1983](https://maths-people.anu.edu.au/~brent/pub/pub082.html) | Conventional hardware control |
| Carry-free extended GCD | [Yun & Zhang 1986](https://doi.org/10.1145/32439.32455) | Conventional hardware control |
| GCD coprocessor | [Guyot 1991](https://doi.org/10.1109/ARITH.1991.145564) | Conventional hardware control |
| Residue arithmetic | [Garner 1959](https://doi.org/10.1109/TEC.1959.5219515) | Number-theoretic control |
| Binary logarithmic arithmetic | [Mitchell 1962](https://doi.org/10.1109/TEC.1962.5219391) | Operation-relocation control |
| Sign/log system | [Swartzlander & Alexopoulos 1975](https://doi.org/10.1109/T-C.1975.224172) | LNS control |
| LNS addition hardware | [Lewis 1990](https://doi.org/10.1109/12.61042) | Displaced-addition hardware control |
| Associative arithmetic | [Batcher 1982](https://doi.org/10.1109/TC.1982.1676015) | Sparse/CAM analogy |
| Static dataflow processor | [Dennis & Misunas 1975](https://doi.org/10.1145/642089.642111) | Scheduling analogy |
| Logical irreversibility | [Landauer 1961](https://doi.org/10.1147/rd.53.0183) | Information accounting |
| Reversible embedding | [Bennett 1973](https://doi.org/10.1147/rd.176.0525) | Information accounting |
| Conservative logic | [Fredkin & Toffoli 1982](https://doi.org/10.1007/BF01857727) | Reversible architecture analogy |
| Prime factor equivalence/lattice | [Mathlib factorization](https://leanprover-community.github.io/mathlib4_docs/Mathlib/Data/Nat/Factorization/Basic.html) | Formalized established mathematics |
| Valuation laws | [Stacks Project 00IF](https://stacks.math.columbia.edu/tag/00IF) | Established mathematics |
| Booth multiplication | [Booth 1951](https://doi.org/10.1093/qjmam/4.2.236) | Conventional multiplier baseline |
| Parallel multiplier tree | [Wallace 1964](https://doi.org/10.1109/PGEC.1964.263830) | Conventional multiplier baseline |
| Parallel multiplier reduction | [Dadda 1965 reprint](https://ieeemilestones.ethw.org/File%3ASome_schemes_for_parallel_multipliers_%28reprint%29.pdf) | Conventional multiplier baseline |
| Two's-complement array multiply | [Baugh & Wooley 1973](https://doi.org/10.1109/T-C.1973.223648) | Conventional multiplier baseline |
| NAND/cell mapping | [Yosys `abc`](https://yosyshq.readthedocs.io/projects/yosys/en/0.47/cmd/abc.html) | Synthesis constraint |
| FPGA LUT mapping model | [Yosys/ABC documentation](https://yosyshq.readthedocs.io/projects/yosys/en/v0.64/using_yosys/synthesis/abc.html) | Synthesis constraint |
| FPGA physical resources | [AMD UG474](https://docs.amd.com/r/en-US/ug474_7Series_CLB/CLB-Overview) | Synthesis constraint |
| Physical-flow evidence ladder | [OpenROAD DSE](https://openroad.readthedocs.io/en/latest/contrib/DesignSpaceExploration.html) | Synthesis constraint |
