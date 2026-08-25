# Hardware Prior Art

## Status and scope

This is the Build 002 hardware prior-art boundary, accessed 2026-08-24. It is a bounded review, not a systematic review, patent clearance, novelty opinion, or implementation result.

Build 001 remains `PARTIAL — PILOT_NEGATIVE; FINAL DECISION NOT EARNED` and `PILOT_SUBSET_COMPLETE_FULL_CONFIRMATION_NOT_RUN`. Nothing here raises that claim ceiling. In particular, established valuation mathematics, factor-form software, and existing factor-extraction circuits are prior art rather than Build 002 discoveries.

Evidence labels:

- **Direct**: substantially the same representation boundary or circuit function has been specified or built.
- **Control**: a serious conventional or alternative architecture against which a hardware claim must be tested.
- **Analogy**: an implementation or accounting idea, not evidence for prime-native arithmetic.
- **Synthesis constraint**: a limit on what a reported metric can establish.
- **Bounded gap**: not located in this search; not evidence of novelty.

## Scoped conclusion

Hardware already exists that accepts ordinary binary magnitude and emits bounded prime identities, their exponents, and an exact residual cofactor. Constant-divisor circuits and trailing-zero counters provide its principal building blocks. Therefore Build 002 cannot claim that hardware extraction of prime coordinates, or the idea of computing in factor form, is new.

The closest direct circuit is Southern, Mason, Chikkam, Baier, and Gaj's FPGA trial-division engine. Its interface is nearly the Build 001 ingress contract: for a fixed set of small primes, it emits `(prime, exponent)` pairs and a quotient not divisible by any bank prime. Its architecture also exposes the displaced cost: a prime ROM, pipelined divisibility tests, a FIFO, repeated quotient-producing division, control, and a residual value.

This review did **not** locate a general-purpose, persistent arithmetic unit whose registers remain in exact prime-exponent-plus-cofactor form across composition, exact cancellation, gcd/lcm, comparison, ordinary addition, normalization, reconstruction, and bank migration. That is a bounded search gap, not a novelty finding.

The prior art supports a narrower research question:

> On an unchanged binary substrate, can a persistent, knowledge-carrying divisibility-state machine save enough repeated work to repay acquisition, residual, routing, overflow, and reconstruction costs against competent binary, logarithmic, and residue controls?

Only paired implementation and synthesis evidence can answer that question.

## Ranked prior-art register

| Rank | Class | Source | What it establishes | What it does not establish |
|---:|---|---|---|---|
| 1 | **Direct** | Southern et al., [“FPGA Implementation of High Throughput Circuit for Trial Division by Small Primes”](https://www.hyperelliptic.org/tanja/SHARCS/talks07/record.pdf), SHARCS 2007; [GMU slides](https://people-ece.vse.gmu.edu/~kgaj/publications/conferences/GMU_SHARCS_2007_slides.pdf) | Fixed-bank hardware can emit prime/exponent pairs plus a residual quotient; fast divisibility scanning and slower repeated extraction are separable datapaths. | A persistent factor-form ALU, ordinary addition in factor form, or an advantage over a complete conventional pipeline. |
| 2 | **Direct building block** | RISC-V [Zbb `ctz`](https://docs.riscv.org/reference/isa/unpriv/b-st-ext.html); Granlund and Montgomery, [invariant division](https://doi.org/10.1145/178243.178249); Gorodecky and Sousa, [constant division on FPGA](https://doi.org/10.1109/ARITH58626.2023.00025) and [paper](https://arith2023.arithsymposium.org/papers/Scalable%20Architecture%20of%20Constant%20Division%20on%20FPGA.pdf) | `v_2` is directly exposed by trailing-zero structure for nonzero binary words; odd-prime valuation can be built from exact constant quotient/remainder tests and repetition. | A uniform-cost valuation primitive. Binary physically privileges `p = 2`; odd primes require different circuitry. |
| 3 | **Direct semantics** | Wells, [*Elements of Combinatorial Computing*](https://shop.elsevier.com/books/elements-of-combinatorial-computing/wells/978-0-08-016091-7), publisher [preview](https://api.pageplace.de/preview/DT0400.9781483186665_A23861482/preview-9781483186665_A23861482.pdf); factor-form systems recorded in [Build 001 prior art](PRIOR_ART_BUILD001.md) | Prime-factor representation and operational factor objects are established computing representations. | Hardware benefit, architectural persistence, or novelty of exponent-vector operations. |
| 4 | **Control** | Mitchell, [binary logarithmic multiplication/division](https://doi.org/10.1109/TEC.1962.5219391); Swartzlander and Alexopoulos, [sign/logarithm number system](https://doi.org/10.1109/T-C.1975.224172); Lewis, [LNS addition architecture](https://doi.org/10.1109/12.61042); Lee and Burgess, [FPGA LNS add/sub](https://doi.org/10.1109/FPT.2003.1275734) | A representation can make multiplication/division local while moving difficulty into nonlinear ordinary addition/subtraction hardware. | Exact prime-factor semantics or a complete answer for integer workloads. |
| 5 | **Adversarial control** | Garner, [“The Residue Number System”](https://doi.org/10.1109/TEC.1959.5219515) | Number-theoretic coordinates can make both addition and multiplication componentwise and carry-free within a declared dynamic range. | Cheap comparison, division, sign, overflow detection, or conversion. |
| 6 | **Conventional control** | Brent and Kung, [systolic linear-time GCD](https://maths-people.anu.edu.au/~brent/pub/pub082.html); Yun and Zhang, [carry-free extended GCD](https://doi.org/10.1145/32439.32455); Guyot, [GCD coprocessor](https://doi.org/10.1109/ARITH.1991.145564) | Binary magnitude has serious dedicated GCD and extended-GCD architectures, including systolic and redundant-number implementations. | That a factor-lane `min` is end-to-end cheaper after ingress and residual obligations. |
| 7 | **Conventional control** | Booth, [signed multiplication](https://doi.org/10.1093/qjmam/4.2.236); Wallace, [fast multiplier](https://doi.org/10.1109/PGEC.1964.263830); Dadda, [parallel multiplier schemes](https://ieeemilestones.ethw.org/File%3ASome_schemes_for_parallel_multipliers_%28reprint%29.pdf); Baugh and Wooley, [two's-complement array multiplication](https://doi.org/10.1109/T-C.1973.223648) | Shift/add is only one conventional multiplier point; array, recoded, and parallel-compression architectures are established baselines. | A target-independent best multiplier at tiny widths; synthesis may collapse distinctions. |
| 8 | **Analogy** | Rudolph and Batcher, [STARAN associative processor](https://tcm.computerhistory.org/ComputerTimeline/Chap21_staran_CS2.pdf); Batcher, [bit-serial parallel processing](https://doi.org/10.1109/TC.1982.1676015) | Compare/tag/write machinery can perform bit-serial arithmetic across many associative rows and suggests sparse factor-key matching. | That CAM is cheaper than hard-wired fixed lanes or sorted sparse merge. Match storage, routing, and write cycles remain costs. |
| 9 | **Analogy** | Dennis and Misunas, [basic data-flow processor](https://doi.org/10.1145/642089.642111), [primary paper mirror](https://www.cs.cmu.edu/~15740-f20/papers/dennis-75.pdf) | Independent lanes can be scheduled as readiness-driven tokens, with communication and buffering made explicit. | Arithmetic simplification. Dataflow changes execution order, not factorization or reconstruction cost. |
| 10 | **Analogy / constraint** | Landauer, [irreversibility](https://doi.org/10.1147/rd.53.0183); Bennett, [logical reversibility](https://doi.org/10.1147/rd.176.0525); Fredkin and Toffoli, [conservative logic](https://doi.org/10.1007/BF01857727) | Reversibility supplies an information-loss audit: composition or cancellation is not reversible if inputs/history are discarded. | Free cancellation, lower energy, or a prime-native reversible ALU. |
| 11 | **Outside-floor control** | Moscow State University [Ternary Informatics](https://ternarycomp.cs.msu.ru/) and [Setun collection](https://ternarycomp.cs.msu.ru/setun.html) | Computing has historically forked below binary into balanced-ternary machinery. | A same-NAND-floor comparison unless trits are explicitly encoded and costed in binary logic. |

### Direct constant-division sources

The odd-prime ingress boundary has its own substantial hardware literature:

- Florent de Dinechin and Laurent-Stéphane Didier, [“Table-Based Division by Small Integer Constants”](https://doi.org/10.1007/978-3-642-28365-9_5), with [author PDF](https://perso.citi-lab.fr/fdedinec/recherche/publis/2012-ARC-LUTConstDiv.pdf).
- H. Fatih Ugurdag et al., [“Hardware Division by Small Integer Constants”](https://doi.org/10.1109/TC.2017.2707488), with [author PDF](https://perso.citi-lab.fr/fdedinec/recherche/publis/2017-TC-ConstDiv.pdf).
- Danila Gorodecky and Leonel Sousa, [“Scalable Architecture of Constant Division on FPGA”](https://doi.org/10.1109/ARITH58626.2023.00025), with [symposium PDF](https://arith2023.arithsymposium.org/papers/Scalable%20Architecture%20of%20Constant%20Division%20on%20FPGA.pdf).

The 2023 design is especially instructive for evidence discipline: it emits quotient and remainder using adders and Boolean-function encoders, while explicitly making minimization depend on the ASIC library or FPGA LUT width. A logical half-adder/full-adder gate-count difference need not survive FPGA LUT mapping.

## Mathematical prior art and the actual native structure

### Factorization coordinates

Mathlib formalizes an equivalence between positive natural numbers and finitely supported natural-valued functions supported on primes, as well as factorization, divisibility, gcd, and lcm laws:

- [Mathlib natural factorization definitions](https://leanprover-community.github.io/mathlib4_docs/Mathlib/Data/Nat/Factorization/Defs.html)
- [Mathlib natural factorization lemmas](https://leanprover-community.github.io/mathlib4_docs/Mathlib/Data/Nat/Factorization/Basic.html)

For fully known positive values:

- multiplication is componentwise exponent addition;
- divisibility is componentwise exponent order;
- gcd is componentwise meet/min;
- lcm is componentwise join/max;
- exact division, when defined, is componentwise subtraction.

Thus a finite dense prime bank realizes a product of counter/comparator lanes. Without saturation, its exponent portion is the commutative monoid `N^S` under addition. The fully factorized positive integers form the free commutative monoid on the primes. Allowing signed integer exponents gives the group completion used for positive rationals. Componentwise order supplies a distributive divisibility lattice.

This is established mathematics, not a hardware result. A fixed Build 002 bank plus exact cofactor is only a projection of that structure. Zero, sign, cofactor contents, finite-basis escape, exponent overflow, and knowledge state remain explicit representation obligations.

Finite registers also change the algebra. Saturating addition, trapping overflow, wraparound, and an absorbing `Overflow` state define different machines. No result may name the unbounded monoid law without stating which bounded behavior was implemented and tested.

### Valuation laws and the tropical analogy

The established valuation laws are:

`v(ab) = v(a) + v(b)`

and, for a nonzero sum,

`v(a + b) >= min(v(a), v(b))`.

Source: [Stacks Project, Lemma 10.50.14](https://stacks.math.columbia.edu/tag/00IF).

For a discrete prime valuation, unequal input valuations make the minimum exact. Equal valuations can cancel, so unit/residue information determines whether the output valuation rises. Exponent lanes alone cannot generally compute exact `v_p(a+b)`.

The hardware motivation therefore has a legitimate **tropical-like shadow**—multiplication becomes lane addition and ordinary addition yields a minimum lower bound—but not a complete tropical realization of integer addition. Returning `min` as exact on equal lanes without additional evidence would be incorrect.

### Divisibility order is not magnitude order

Prime lanes naturally expose divisibility, not ordinary magnitude. Componentwise comparison can answer a bounded divisibility question; it does not answer whether one represented integer is numerically smaller. Exact magnitude comparison requires reconstruction, a separately maintained positional shadow, or proved bounds/metadata. Each option carries state and maintenance cost.

The most defensible architectural description is therefore a **divisibility-lattice coprocessor** or **bounded-valuation functional unit**, not a replacement general-purpose integer ALU.

## Synthesis evidence ladder

NAND count is exact only for the declared constructed NAND netlist. It is not a technology-neutral area, timing, power, or optimality unit.

| Evidence rung | What may be reported | What must not be inferred |
|---|---|---|
| Constructed two-input NAND model | Exact NAND instances, modeled combinational depth, declared fanout/storage omissions | FPGA LUTs, transistors, physical delay, power, or minimum circuit complexity |
| Optimized Boolean/AIG/NAND mapping | Tool- and script-specific mapped nodes/depth; equivalence to the RTL boundary | Target-independent gate advantage or a physical result |
| FPGA synthesis | Target-specific LUTs, flip-flops, carry primitives, DSPs, BRAMs, estimated timing, warnings | ASIC area, transistor count, routed timing, or silicon energy |
| ASIC standard-cell mapping | Library/corner/constraint-specific cell area and rough timing | Post-layout PPA or signoff timing |
| Placement/global route | Utilization, congestion, increasingly realistic wire delay and timing | Detailed-route/signoff truth |
| Detailed route plus parasitic extraction and STA | Flow-, library-, corner-, and constraint-bounded physical estimate | Universal hardware advantage or fabricated-silicon measurement |

Primary and official synthesis sources:

- Yosys [`abc` mapping](https://yosyshq.readthedocs.io/projects/yosys/en/0.47/cmd/abc.html) can target selected gate sets, FPGA LUTs, or a Liberty library.
- Yosys [cell-library mapping](https://yosyshq.readthedocs.io/projects/yosys/en/v0.56/using_yosys/synthesis/cell_libs.html) makes the selected physical cell library part of the result.
- Yosys/ABC's [FPGA mapping documentation](https://yosyshq.readthedocs.io/projects/yosys/en/v0.64/using_yosys/synthesis/abc.html) states that the basic LUT mapper uses a simplified unit-delay model and lacks the broader endpoint/timing awareness of a physical flow.
- Berkeley [ABC](https://people.eecs.berkeley.edu/~alanmi/abc/abc.htm) performs optimization and technology mapping for LUT and standard-cell targets; mapping and area recovery are tool decisions.
- AMD [UG474 CLB overview](https://docs.amd.com/r/en-US/ug474_7Series_CLB/CLB-Overview) and [carry logic](https://docs.amd.com/r/en-US/ug474_7Series_CLB/Carry-Logic) document 6-input LUTs, storage, and dedicated arithmetic carry paths. An FPGA is not a sea of NAND2 cells.
- OpenROAD's [design-space evidence ladder](https://openroad.readthedocs.io/en/latest/contrib/DesignSpaceExploration.html) distinguishes quick synthesis, rough full-synthesis timing, placement/routing evidence, and detailed-route plus extraction.

Paired architecture comparisons require the same tool versions, target, top-level semantic boundary, widths, signedness, clock/constraints, I/O registration, optimization effort, and synthesis seed where relevant. Pre-optimization, post-mapping, and post-route artifacts are distinct evidence classes.

## Required controls and cost disclosures

A hardware comparison is incomplete unless it separates:

1. **Native-state workload**: values already resident in each architecture's preferred representation.
2. **End-to-end workload**: ordinary ingress, native work, maintenance, and ordinary egress.

Prime-coordinate accounting must include bank configuration/ROM, all odd-prime divisibility and quotient work, exponent and cofactor registers, FIFO/control state, sign/zero/overflow/invalid states, residual arithmetic, reconstruction, and any refresh or bank migration. `p = 2` must be reported separately because binary representation provides a special trailing-zero path.

Competent controls depend on the claim and include:

- iterative/reused shift-add and a combinational/native synthesized multiplier;
- dedicated binary GCD rather than only a software Euclidean loop;
- logarithmic arithmetic when the claim concerns relocating multiplication into addition;
- residue arithmetic when the claim concerns number-theoretic channel locality;
- dense, sorted-sparse, and associative factor stores when a sparse keyed design is claimed.

Area, latency, initiation interval, throughput, and output exactness are a vector. They must not be collapsed into an unregistered weighted score. A structured lower bound, unknown valuation, or factor-identity-only overflow output is not an exact ordinary integer result.

## Claim limits and bounded gaps

The following claims are **not earned** by prior art or mathematical elegance:

- that prime-native hardware is novel;
- that multiplication is physically free or universally cheaper;
- that componentwise gcd/lcm is end-to-end cheaper than binary GCD/lcm;
- that valuation `min` computes exact ordinary addition;
- that NAND count predicts FPGA or ASIC PPA;
- that RTL or synthesis estimates predict fabricated silicon;
- that CAM, dataflow, reversible, or ternary computing independently validates a prime-coordinate ALU;
- that a fixed bank naturally discovers a prime hidden in the cofactor without charged computation;
- that Build 001's pilot result has reached a terminal label.

This bounded search did not locate a published architecture combining all of these features:

- persistent exact prime-exponent lanes plus exact cofactor;
- explicit exact/lower-bound/unknown/overflow/zero/invalid knowledge states;
- native compose, checked cancel, project/refresh, divisibility meet/join, and reconstruction;
- ordinary addition with honest valuation knowledge propagation;
- exact global bank migration;
- paired NAND, FPGA, and ASIC comparison against competent binary, RNS, and LNS controls.

That absence may motivate experiments. It must not be reported as novelty without a broader scholarly, patent, dissertation, citation-network, and non-English literature search.

The defensible prior-art statement for Build 002 is:

> Factor-form representation, valuation laws, constant-divisor circuits, hardware extraction of bounded small-prime powers, logarithmic arithmetic, residue arithmetic, GCD hardware, associative arithmetic, dataflow execution, and reversible computing all predate this build. Build 002 asks only how an exact bounded-valuation-plus-cofactor contract maps onto the same binary substrate as conventional arithmetic, with every conversion and uncertainty state charged.

