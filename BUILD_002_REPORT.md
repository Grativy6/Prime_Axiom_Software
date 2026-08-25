# Prime Axiom Hardware — Build 002 Report

## Terminal result

> **Classification: `NO_HARDWARE_ADVANTAGE`**

This is the terminal classification earned by frozen protocol `PAH-BUILD002-CONF0001`. All required experiment families A–F/R are `COMPLETE_BOUNDED` at `W in {4,6,8}`; the generated campaign recorded 656,810 bounded checks with zero failures; and the imported HDL campaign completed 260/260 cases, including 15 formal cases and 150 validated synthesis rows. The final repository verifier passed 183/183 tests with zero failed or skipped and confirmed deterministic evidence replay. The decision flag in [`protocol_coverage.json`](results/build002/protocol_coverage.json) is `true`.

The negative is narrow. It does **not** mean that valuation state has no useful local behavior. The fully integrated warm S4 scale/cancel machine used substantially fewer NANDs, wires, logical levels, NAND evaluations, and settled NAND-output transitions than its matched binary machine. It nevertheless used more DFF/state bits and more port bits at both decision widths. Under the frozen definition—no worse in every applicable charged dimension and strictly better in at least one—that is a tradeoff, not Pareto dominance.

The exact magnitude-plus-valuation sidecar failed more directly. It was already statically larger than the matched binary mixed datapath at W=6 and W=8, and it performed more work on the mixed-addition and hostile-support traces. Cold acquisition, validity, refresh, unsupported-factor handling, and reconstruction displaced rather than removed the ordinary arithmetic cost.

Build 002 is a separate bounded hardware result. It does not retroactively change Build 001's status, which remains `PARTIAL — PILOT_NEGATIVE; FINAL DECISION NOT EARNED` and `PILOT_SUBSET_COMPLETE_FULL_CONFIRMATION_NOT_RUN`.

## Answer to the goal question

> Given these physical primitives, what mathematics does the machine find naturally?

The supplied primitive does not find primes. An ideal two-state NAND/DFF substrate naturally supplies Boolean distinction, composition, and finite state. Mathematics changes only after stable bits are given a representation and transition map.

- Positional binary state naturally exposes bounded magnitude, total order, addition/subtraction recurrences, shifts, comparison, and conventional multiply/divide machinery.
- Binary prime-exponent state naturally exposes componentwise exponent addition/subtraction and divisibility order. Before finite caps this is the familiar free commutative monoid; in the implemented bounded machine it is a product of finite chains with partial checked composition and cancellation.
- Thermometer valuation state naturally exposes initial segments of those chains. Meet, join, divisibility, and fixed-threshold predicates become intersection, union, inclusion, and wire selection. Compose becomes threshold convolution rather than disappearing.
- An authoritative magnitude plus valuation thresholds naturally becomes a **certificate/knowledge machine**, not a second exact integer foundation. It carries selected reusable divisibility facts, lower bounds, validity, and refresh obligations beside ordinary magnitude.

The most honest conclusion is therefore: **the representation selects a local algebra, while the workload and boundary contract decide whether that algebra is useful**. Build 002 found a strong local divisibility-lattice/valuation specialization, but no same-contract whole-machine hardware advantage under the frozen S4 experiment.

## Scope and frozen method

The controlling plan is [`research/build002_experiment_plan.md`](research/build002_experiment_plan.md), SHA-256 `24C770290A97A1C467DBCC7B4C97CA9EE875EFC21ADE837CA9D96049ACD76745`. It was frozen against repository baseline `dfd2e7a409aaa114f054a0b40e4b282c68dc0d52` before comparative execution.

The shared floor was:

```text
ideal readable two-state signal
-> two-input NAND
-> acyclic combinational netlist
-> explicitly charged DFF state boundary
```

Both lineages used the same stable-node [`NandNetlist`](src/PrimeAxiom.Core/Hardware/NandNetlist.cs), the same NAND-derived [`NandLogic`](src/PrimeAxiom.Core/Hardware/BaselineHardware.cs), the same unit-NAND depth model, and the same logical cost vector. Constants and direct nets were not gates, but ports, nets, sinks, fanout, region crossings, and DFFs remained visible.

The ordinary magnitude widths were exactly 4, 6, and 8 bits. The experimental catalog was fixed to `S4=(2,3,5,7)`, with lane caps:

| W | Caps `(v2,v3,v5,v7)` | Binary-exponent payload | Thermometer payload |
|---:|---|---:|---:|
| 4 | `(3,2,1,1)` | 6 bits | 7 bits |
| 6 | `(5,3,2,2)` | 9 bits | 12 bits |
| 8 | `(7,5,3,2)` | 10 bits | 17 bits |

Zero, saturation, overflow, underflow, divide-by-zero, unsupported factors, exactness, and atomic rejection were explicit. Cold magnitude, warm resident/generated state, predicate-only output, structural output, final magnitude, and magnitude after every operation were kept as different contracts. No universal scalar or post hoc weighting was used.

PAL v2.2 and A0/Software were reference lenses only. They neither specified the circuits nor supply evidence for the result.

## What was built

### Common substrate

The declared C# substrate provides stable node IDs and names, NAND-only cells, named inputs/outputs, regions and lane labels, DFF boundary metadata, exact evaluation, topological and driver validation, settled transition replay, and gate/net/sink/fanout/depth/cross-region metrics. All 90 committed declared graph rows are acyclic.

The common derived constructions use these declared NAND2 counts:

| Function | NAND2 |
|---|---:|
| NOT | 1 |
| AND | 2 |
| OR | 3 |
| XOR | 4 |
| XNOR | 5 |
| 2:1 mux | 4 |
| half adder | 6 |
| full adder | 15 |

DFFs were retained as separately charged architectural state cells rather than decomposed into an arbitrary transistor or NAND implementation.

### Conventional lineage

The conventional lineage includes NAND-only ripple add/subtract, unsigned compare, shift-add W-by-W multiplication with a 2W product, operation-select `BIN-FU`, registers, counters, an unrolled restoring divider, and a registered subtractive-GCD controller/datapath. It also includes two matched persistent machines used by the integrated comparisons:

- `BIN-SCALE-CANCEL-WARM`: W-bit magnitude, selected multiplication or exact cancellation by 2/3/5/7, checked overflow/divisibility, and atomic hold on rejection;
- `BIN-MIXED-WARM`: load, selected scale/cancel, arbitrary magnitude addition, status/control, and atomic hold.

These are transparent conventional controls, not claims of optimal multiplier, divider, or GCD hardware.

### Experimental lineage

Three related, separately costed representations were built:

1. `VFU-BINEXP-S4`: capped binary exponent lanes with explicit zero and per-lane saturation state. Native operations are `COMPOSE`, `CANCEL`, `MEET`, `JOIN`, `DIVIDES`, `VALUATION`, and `POWER`; `RECONSTRUCT` is an adapter.
2. `VFU-THERM-S4`: canonical threshold prefixes. It implements meet/join, divisibility, fixed-threshold query, canonical validation, and direct threshold-convolution compose, plus binary-exponent/thermometer adapters.
3. `BIN+VSC-S4`: authoritative W-bit magnitude plus exact or lower-bound S4 thresholds and validity. The integrated C# datapath implements load/refresh/query/scale/cancel/add, overflow and rejection, atomic state hold, and DFF persistence. Unsupported cofactors remain in magnitude.

A separate persistent `VFU-BINEXP-S4-WARM` supplies the fair integrated scale/cancel comparison against `BIN-SCALE-CANCEL-WARM`.

## Completion and evidence map

The generated workload matrix contains 609 phase-separated rows:

| Experiment | Status | Rows | Integrated rows | `NOT_MEASURED` cost cells | Bounded content |
|---|---|---:|---:|---:|---|
| A | `COMPLETE_BOUNDED` | 45 | 0 | 114 | Binary multiply, two structural compose forms, and charged adapters |
| B | `COMPLETE_BOUNDED` | 240 | 144 | 216 | Eight 32-step traces per width, matched persistent machines, three output obligations |
| C | `COMPLETE_BOUNDED` | 24 | 6 | 24 | Full binary GCD/LCM, S4 meet/join/divides, adapters, exact-sidecar query |
| D | `COMPLETE_BOUNDED` | 54 | 0 | 162 | Eight rational cases plus denominator zero per width; full result versus catalog projection |
| E | `COMPLETE_BOUNDED` | 165 | 165 | 0 | Eight mixed-addition traces, matched binary, delayed sidecar, eager sidecar |
| F | `COMPLETE_BOUNDED` | 63 | 63 | 0 | Three hostile trace families, matched binary and both sidecar policies |
| R | `COMPLETE_BOUNDED` | 18 | 0 | 54 | Binary exponent, thermometer, presence-only, sidecar geometry and adapters |

`NOT_MEASURED` never means zero. The remaining unmeasured cells are chiefly settled-transition fields for adapters or composite GCD/LCM/rational paths. They were not used to manufacture a positive comparison or the terminal negative.

## Decisive results

### Integrated static vectors

Each compact entry is:

```text
NAND2 / DFF / state bits / port bits / wire bits /
connections / max fanout / unit-NAND depth
```

| Context | W | Conventional | Experimental |
|---|---:|---|---|
| Warm scale/cancel | 6 | `2313 / 6 / 6 / 15 / 2324 / 4644 / 274 / 270` | `789 / 14 / 14 / 23 / 820 / 1612 / 105 / 38` |
| Warm scale/cancel | 8 | `3959 / 8 / 8 / 17 / 3972 / 7940 / 414 / 448` | `864 / 15 / 15 / 24 / 896 / 1764 / 108 / 38` |
| Mixed load/scale/cancel/add | 6 | `2532 / 6 / 6 / 22 / 2551 / 5081 / 271 / 276` | `5638 / 19 / 19 / 42 / 5677 / 11323 / 275 / 278` |
| Mixed load/scale/cancel/add | 8 | `4242 / 8 / 8 / 26 / 4265 / 8505 / 411 / 456` | `15871 / 26 / 26 / 51 / 15919 / 31803 / 415 / 458` |

The warm structural machine reduced declared NAND count by about 66% at W=6 and 78% at W=8, with depth falling from 270/448 to 38. It nevertheless increased state from 6 to 14 bits and 8 to 15 bits, and ports from 15 to 23 bits and 17 to 24 bits. That exact vector is why the strongest local result did not satisfy strict Pareto dominance.

The exact sidecar used 2.23 times the matched binary NAND count at W=6 and 3.74 times at W=8, roughly tripled resident state, and approximately doubled port bits. Its depth was only two logical NAND levels greater because much of the extra logic was parallel, but shallow duplication is still duplication.

### Local operation-only structure

The declared C# operation-only rows show the representation effect before register and conversion boundaries:

| W | Binary multiply NAND/depth | BINEXP compose NAND/depth | THERM compose NAND/depth |
|---:|---:|---:|---:|
| 6 | `1152 / 83` | `1036 / 38` | `519 / 43` |
| 8 | `2048 / 111` | `1132 / 39` | `779 / 53` |

Thermometer meet/join/divides at W=6 used 453/466/385 NANDs, versus 934/935/824 for binary exponents; at W=8 they used 623/641/525 versus 1023/1024/899. These are real operation-local differences, but the thermometer form used more resident bits and ports and its compose circuit paid threshold convolution, validation, and cross-threshold connections.

### B — warm scale/cancel dynamics

Across all eight frozen 32-step traces, each execute path processed 256 instructions in 256 cycles, observed 275 input-bit transitions, and made 26 atomic rejections:

| W | Machine | NAND evaluations | Settled NAND transitions | State transitions | Initial NAND transitions |
|---:|---|---:|---:|---:|---:|
| 6 | Binary | 592,128 | 46,863 | 371 | 12,316 |
| 6 | BINEXP-S4 | 201,984 | 23,166 | 244 | 4,045 |
| 8 | Binary | 1,013,504 | 59,073 | 371 | 21,134 |
| 8 | BINEXP-S4 | 221,184 | 24,604 | 244 | 4,411 |

This is the strongest positive experimental result. The final-output obligation determines how far it survives:

| W | Obligation | Binary cycles/NAND | Structural cycles/NAND | Structural reconstructions |
|---:|---|---:|---:|---:|
| 6 | `MAGNITUDE_FINAL` | `256 / 592128` | `264 / 251008` | 8 |
| 6 | `MAGNITUDE_EVERY_OP` | `256 / 592128` | `512 / 1770752` | 256 |
| 8 | `MAGNITUDE_FINAL` | `256 / 1013504` | `264 / 367928` | 8 |
| 8 | `MAGNITUDE_EVERY_OP` | `256 / 1013504` | `512 / 4916992` | 256 |

On NAND evaluations alone, one 32-step W=6 trace stays below the binary execute total through seven reconstructions and loses at eight; W=8 stays below through five and loses at six. That is a useful workload break-even observation, not a whole-vector break-even: the structural state/port overhead remains present, cycles increase when reconstruction is used, and adapter transition totals are `NOT_MEASURED`.

### Cold acquisition and reconstruction

| W | Magnitude -> exact BINEXP-S4 | Exhaustive sweep | BINEXP-S4 -> magnitude | Common-domain sweep | Integrated sidecar LOAD |
|---:|---:|---:|---:|---:|---:|
| 6 | `964 NAND, depth 21` | `64 cases, 61,696 eval` | `6128 NAND, depth 42` | `36 cases, 220,608 eval` | `5638 NAND/load; 64 loads, 360,832 eval` |
| 8 | `4786 NAND, depth 23` | `256 cases, 1,225,216 eval` | `18343 NAND, depth 46` | `77 cases, 1,412,411 eval` | `15871 NAND/load; 256 loads, 4,062,976 eval` |

The pure encoder records whether a value is fully S4-supported; it does not discard an unsupported cofactor. The sidecar LOAD instead accepts every W-bit magnitude, atomically stores authoritative magnitude, and acquires exact S4 thresholds. Direct binary ingress uses no combinational NANDs, while equal state boundaries remain charged in the integrated rows.

Binary-exponent to thermometer and reverse conversion were also nonzero: 237/257 NANDs at W=6 and 331/411 at W=8. A change of structural encoding is an adapter, not a renaming.

### E — mixed addition and F — hostile support

Totals below combine each policy's recorded `INGRESS`, `EXECUTE`, and `ADDITION_RECOVERY` phases under the same `WARM_GENERATED + MAGNITUDE_EVERY_OP` contract. E contains eight traces per policy; F contains three hostile trace families per policy.

| Family | W | Policy | Cycles | NAND evaluations | Settled NAND transitions | State transitions | Refreshes | Rejections |
|---|---:|---|---:|---:|---:|---:|---:|---:|
| E | 6 | Binary | 48 | 121,536 | 9,678 | 77 | 0 | 1 |
| E | 6 | Sidecar delayed | 48 | 270,624 | 25,942 | 149 | 0 | 1 |
| E | 6 | Sidecar eager | 55 | 310,090 | 27,466 | 168 | 7 | 1 |
| E | 8 | Binary | 48 | 203,616 | 11,966 | 77 | 0 | 1 |
| E | 8 | Sidecar delayed | 48 | 761,808 | 52,671 | 159 | 0 | 1 |
| E | 8 | Sidecar eager | 55 | 872,905 | 54,533 | 180 | 7 | 1 |
| F | 6 | Binary | 99 | 250,668 | 24,538 | 95 | 0 | 60 |
| F | 6 | Sidecar delayed | 99 | 558,162 | 50,337 | 141 | 0 | 60 |
| F | 6 | Sidecar eager | 121 | 682,198 | 61,420 | 242 | 22 | 60 |
| F | 8 | Binary | 99 | 419,958 | 38,802 | 140 | 0 | 54 |
| F | 8 | Sidecar delayed | 99 | 1,571,229 | 122,482 | 202 | 0 | 54 |
| F | 8 | Sidecar eager | 126 | 1,999,746 | 154,201 | 339 | 27 | 54 |

Delayed mode kept only sound lower-bound thresholds when exactness was not earned. Eager mode refreshed after invalidating additions; seven of eight E traces required refresh at every width. Neither sidecar policy recovered its added hardware or dynamic work on E or F.

Across the 125 hostile-support values, pure S4 state supported 45 and explicitly reported 80 unsupported cofactors. The sidecar remained fully exact for all 125 because magnitude stayed authoritative. This is semantic robustness purchased by carrying both systems, not free support expansion.

## Frozen classification audit

The ordered decision rules were applied without a weighted score:

1. **`ALTERNATIVE_ARITHMETIC_UNIT_CANDIDATE`: not satisfied.** The integrated sidecar was statically larger at W=6 and W=8, and both delayed and eager policies lost the same-contract E comparison in NAND evaluations, settled transitions, and state activity. It could not Pareto-dominate three families including E in cold and warm `MAGNITUDE_FINAL` use.
2. **`PRIME_STRUCTURAL_COPROCESSOR_CANDIDATE`: not satisfied.** Before dynamic work, the W=6/W=8 sidecar (`5638/15871` NANDs and `19/26` DFFs) exceeded even the full transparent binary divider context (`1021/1729` NANDs, zero DFFs). Static overhead cannot be amortized away under the registered Pareto rule.
3. **`WARM_STATE_SPECIALIZED_ADVANTAGE`: not satisfied.** B produced major reductions in logic, depth, wiring, evaluations, and settled transitions, but required more DFF/state and port bits at both decision widths. It was not no-worse in every dimension.
4. **`UNEXPECTED_ARCHITECTURE`: not satisfied.** No architecture outside the preregistered binary-exponent, thermometer, presence, or exact-sidecar families survived the integrated adversarial tests.
5. **Fallback rule selected:** `NO_HARDWARE_ADVANTAGE`.

## Direct answers to the twenty report questions

### 1. What exact primitive gate/substrate was used?

An ideal deterministic two-state substrate with two-input NAND as the only primitive combinational cell and explicit DFF metadata as the state boundary. The C# and HDL views both enforce this floor. The model counts gates, state, ports, nets, sinks, fanout, crossings, unit-NAND depth, and settled transitions; it does not model analog or physical device behavior.

### 2. What conventional functional unit was built?

A W=4/6/8 unsigned positional `BIN-FU` with NAND-derived ripple add, subtract, compare, W-by-W shift-add multiply, operation select, and registered boundaries. Restoring division, subtractive GCD, registers/counters, a persistent selected-factor scale/cancel machine, and a persistent mixed load/scale/cancel/add datapath supplied the matched algorithm and workload controls.

### 3. What experimental functional unit was built?

A capped four-lane `VFU-BINEXP-S4`; a thermometer-threshold ablation with direct lattice/query circuits and convolution compose; a persistent warm scale/cancel machine; and an integrated exact `BIN+VSC-S4` datapath whose binary magnitude remains authoritative while thresholds carry selected valuation knowledge.

### 4. Where exactly do their architectures diverge?

After stable binary state and before numeric meaning. One state vector is wired and decoded as positional magnitude; the other is partitioned into fixed-address lanes whose externally supplied labels are 2, 3, 5, and 7. Prime identity is configuration/wiring above the DFF boundary, not a property of NAND.

### 5. What mathematical operations are native to each?

Binary positional state makes ordinary bounded addition, subtraction, comparison, shifts, and magnitude-oriented multiply/divide local. Binary exponent state makes compose, checked cancel, componentwise meet/join, divisibility, and valuation projection local. Thermometer state makes threshold predicates, meet, join, and inclusion especially direct. Exact general addition, magnitude order, acquisition, and reconstruction are not local to pure valuation state.

### 6. What algebraic structure best describes the experimental machine?

For exact nonzero S4-smooth values before caps, it is the familiar free commutative monoid `N^S4` under exponent addition. The implemented exact bounded payload is the product of finite chains

```text
C_W = product over p in S4 of {0,...,T_p(W)}.
```

Its componentwise order is a distributive divisibility lattice with meet/min and join/max. Compose and cancel are partial checked operations because caps, saturation, zero, and underflow matter. Thermometer lanes encode chain order ideals. Zero is a tagged extension, not an exponent vector. The exact sidecar is better described as magnitude plus a finite lattice of certified valuation knowledge.

### 7. Which operations became cheaper?

Already-resident known-factor scale/cancel became much smaller and shallower in the integrated S4 machine, and its B traces used fewer evaluations and settled transitions. Operation-only compose also beat the transparent binary multiplier at the measured widths. Thermometer meet/join/divides and fixed-threshold query used shallow monotone logic. These wins apply to resident structural or predicate contracts, not automatically to cold or magnitude-output workloads.

### 8. Which operations became more expensive?

Cold valuation acquisition, reconstruction, magnitude comparison/order, arbitrary addition recovery, unsupported-factor handling, and representation conversion. Exact sidecar execution paid ordinary magnitude arithmetic plus metadata acquisition/update/control. Frequent reconstruction reversed the warm NAND-evaluation win, and eager addition refresh added cycles, evaluations, and state transitions.

### 9. How much cost comes from representation rather than operation?

A large fraction. At W=6 the logical state geometries were 6 bits for binary, 14 for binary-exponent S4, 17 for thermometer S4, and 19 for binary plus exact thresholds; at W=8 they were 8, 15, 22, and 26. Presence-only used five bits but lost multiplicity. The warm S4 operator reduced logic while adding 8 state bits at W=6 and 7 at W=8. The exact sidecar more than doubled or tripled integrated NANDs before a query executed. Acquisition, tags, validity, zero, saturation, selector/control, ports, and adapters are representation costs and remained charged.

### 10. What changes between cold ingress and warm structural execution?

Warm execution exposes the genuine local advantage because factor structure already exists. Cold execution must acquire it. At W=8, the pure exact S4 encoder used 4,786 NANDs and the reconstructor 18,343, compared with 1,132 for a compose leaf; the full persistent sidecar LOAD exercised all 15,871 NANDs per magnitude. One final reconstruction per B trace retained a NAND-evaluation advantage, while reconstruction after every operation lost badly. Cold and warm evidence cannot be pooled.

### 11. What happens under arbitrary addition?

The exact magnitude datapath adds normally. For each prime, the sidecar can preserve `min(v_p(a),v_p(b))` as a lower bound; the value is exact when the two valuations differ, but equal valuations require unit/residue information or refresh. The implementation therefore invalidates exactness or refreshes rather than inventing exponents. Seven of eight E traces per width required eager recovery, and both delayed and eager sidecars cost more than the matched binary path. Addition was not declared wrong; it is structure-transforming in this representation.

### 12. What happens when factors lie outside the native basis?

Pure S4 state cannot denote them and reports unsupported cofactor/basis escape instead of truncating. Catalog meet/join/divides are only projections unless operands are S4-smooth. The sidecar remains exact because the full magnitude contains every outside factor, but it does not discover their identities or include them in S4-local gcd/lcm. The hostile set produced 80 explicit pure-S4 misses and no sidecar loss of numeric exactness.

### 13. What gate/depth/state/wiring costs dominate?

For warm structural scale/cancel, the conventional multiply/divide paths dominate NAND, wiring, and depth; small independent exponent lanes avoid most of that dependency. For cold and mixed exact work, valuation detection, reconstruction, duplicated authoritative magnitude arithmetic, threshold update/control, and wide fanout dominate. The W=8 sidecar's 15,871 NANDs and 15,919 nets coexist with depth 458, only two above binary's 456, showing that extra parallel area/state—not an extra serial chain—is the main declared burden there.

### 14. What did synthesis show?

The pinned common Yosys/ABC flow preserved local representation differences but also removed large construction artifacts. Representative declared/optimized NAND and depth pairs were:

| Top | W=6 | W=8 |
|---|---:|---:|
| Binary multiply | `720/335; 60/53` | `1280/639; 80/75` |
| BINEXP compose | `469/111; 26/12` | `506/124; 26/12` |
| THERM compose | `219/141; 14/8` | `379/241; 18/10` |
| Cold S4 encoder | `1821/172; 73/17` | `10234/632; 269/25` |
| Combinational BIN+VSC FU | `3978/732; 91/34` | `15830/1781; 287/37` |

Each entry is `declared NAND / optimized NAND; declared depth / optimized depth`. All 75 elaborated tops have both views, for 150 rows. This is script- and NAND-target-specific logical synthesis, not FPGA LUT place-and-route, ASIC area, frequency, power, or optimality.

### 15. What did exhaustive/formal verification establish?

The generated arithmetic campaign checked 656,810 bounded cases with zero failures. It exhaustively covered divider and multiplier ordered pairs at W=4/6/8, subtractive-GCD semantics, all declared smaller structural domains, every W-bit load/query domain, and every frozen workload step/status. W=8 sidecar addition combined exhaustive semantic differential checking with the frozen 20,000 gate-level pair set; that gate sample is not relabeled exhaustive.

The final HDL receipt passed 260/260 cases: one analyzer regression, 75 lint cases, 19 independent Icarus simulations, 15 Yosys MiniSAT formal harnesses, 75 declared-netlist validations, and 75 optimized-netlist validations. The simulations exhausted all 65,536 W=8 binary word pairs and all 331,776 ordered legal W=8 pairs for each structural encoding. Formal covered binary, binary-exponent, saturation-tag conservation, thermometer, and sidecar harness families at all three widths. It proved those finite combinational contracts—not unbounded arithmetic, sequential liveness, physical timing, or every integrated workload.

### 16. What prior art is closest?

The closest direct hardware is Southern, Mason, Chikkam, Baier, and Gaj's [small-prime FPGA trial-division engine](https://www.hyperelliptic.org/tanja/SHARCS/talks07/record.pdf), which emits prime/exponent pairs plus a residual quotient. It already exposes the bank ROM, divisibility pipeline, repeated extraction, control, and residual costs.

Other direct controls are RISC-V [`ctz`](https://docs.riscv.org/reference/isa/unpriv/b-st-ext.html) for the radix-native `v_2` path; invariant and small-constant division by [Granlund and Montgomery](https://doi.org/10.1145/178243.178249), [de Dinechin and Didier](https://doi.org/10.1007/978-3-642-28365-9_5), and [Gorodecky and Sousa](https://doi.org/10.1109/ARITH58626.2023.00025); factor-form software and Wells's prime-factor representation; logarithmic number systems as the closest multiplication-versus-addition cost-displacement control; residue number systems as a number-theoretic coordinate control; and established GCD and multiplier hardware. The bounded search found no published system combining the entire persistent exact/lower-bound/cofactor/addition/migration contract, but that is a search gap, not novelty evidence.

### 17. Which apparently interesting results were merely known mathematics expressed in hardware?

NAND functional completeness; prime factorization as a free commutative monoid; valuation addition under multiplication; componentwise divisibility, gcd, and lcm; finite products of chains; distributive lattices; thermometer prefixes as chain ideals; the valuation lower bound for addition; explicit zero extension; and binary radix's special alignment with `p=2`. The circuits made their costs concrete but did not discover these structures.

### 18. Did any genuinely useful architectural idea survive adversarial testing?

No architecture earned a positive frozen classification. A narrower engineering lead survived: **persistent structural state is useful when it is generated by prior operations, queried repeatedly, and allowed to remain structural**. The B result is too strong to dismiss, but the winning object is not a general prime-native CPU or dense exact sidecar. It is a candidate small, workload-triggered divisibility/valuation service whose state is acquired only when reuse is demonstrated. That remains a Build 003 hypothesis, not a Build 002 hardware-advantage claim.

### 19. What mathematics appeared naturally that was not explicitly requested?

No new mathematics was established. Two emphases became clearer than the motivating framing suggested:

1. The thermometer machine is best understood as hardware over **order ideals of finite chains**, making the distributive divisibility lattice more fundamental to its cheap operations than “prime multiplication” alone.
2. The exact sidecar is primarily an **epistemic/certificate state machine**: true thresholds, exactness, lower bounds, invalidation, and refresh form a knowledge discipline beside magnitude arithmetic. Its natural product is reusable proof of selected divisibility facts, not an alternative integer.

The most important asymmetry was also conventional: binary already makes `v_2` unusually local through trailing zeros. Treating 2, 3, 5, and 7 as uniform hardware lanes concealed that radix-specific advantage.

### 20. What should the next build investigate?

Build 003 should investigate a **radix-aware, demand-driven valuation service**, not a wider dense prime-native ALU.

The minimum credible fork is:

```text
authoritative binary magnitude
|-> competent binary baseline: ctz, shifts, constant division, GCD
|-> p=2 path: trailing-zero/count/shift logic
`-> one time-multiplexed odd-prime exact-divisibility/extraction path
    with a tiny tagged certificate cache only when trace reuse earns it
```

The experiment should compare binary-only, `v_2`-only, one odd-prime query unit, and 1–4-entry demand-driven certificate caches. It should use producer-native and cold traces from rational cancellation, divisibility filtering, smooth-number work, and symbolic factored products; record certificate hits, reuse distance, invalidation, transfer, and exact output obligations; and include phase-shift attacks. A residue/unit extension for addition should be a separately gated ablation, not bundled into the main candidate.

Build 003 should also cross the physical evidence boundary that Build 002 did not: identical FPGA place-and-route and one open standard-cell ASIC flow, with matched I/O registration, clock/throughput constraints, and reports for LUT/cell area, registers, routing/congestion, timing, latency/initiation interval, and power estimates. Competent constant-divider and GCD controls are mandatory.

Pre-register the stop rule. If no narrow unit reaches a Pareto point at W=16/32 (and preferably W=64) on at least one independently chosen real trace family after cold acquisition, cache misses, transfer, and exact egress are charged, stop the prime-hardware branch. Do not respond by widening the bank. If a point survives, Build 004 can test selected unit residues or producer-carried sparse certificates.

## Failures and dead ends preserved

The build retained failures rather than rewriting them:

- `.artifacts/build002-hdl-failed-quick-0001/`: 69/82 pass, 13 fail; summary SHA-256 `08dbc63151fda2aca35d63427e2edb87d74085d90cb81ed66e17482523e0aefe`. Four formal harness and nine analyzer failures exposed unconstrained/X-state proof logic and alias/constant/parameter-elision analyzer defects.
- `.artifacts/build002-hdl-full/`: 245/245 pass, later superseded because its zero and saturation-reentry contract was wrong. A green suite did not make an incomplete contract correct.
- `.artifacts/build002-hdl-full-zero-repair/`: corrected 260/260 receipt; summary SHA-256 `9228a81882128f4fd5a9f9ba466d1385db5b5b829eac01242bb09f0cd4e81b90`.

The two substantive repaired defects were:

1. **Zero contract:** the first HDL cold encoder made zero invalid and cleared thresholds, conflicting with the chosen `v_p(0)=+infinity`/finite-threshold contract. The repaired sidecar represents zero as exact/valid with every finite threshold true and preserves it through known-factor updates.
2. **Saturation re-entry:** a clamped compose result could re-enter a raw leaf as apparently exact because prior saturation was not an input. The chainable checked wrapper now carries `bad_a/bad_b` into `bad_y`; tests and formal harnesses cover two-stage conservation.

Dead ends established by the completed matrix include:

- interpreting the same NANDs differently is not by itself a hardware advantage;
- warm local logic savings do not erase added state and interfaces;
- an exact fixed sidecar is not a free coprocessor;
- cold conversion is not negligible;
- thermometer encoding moves carry into state, validation, and convolution rather than abolishing cost;
- presence bits are not exact valuation state;
- fixed S4 is not general integer state;
- prime identity does not exist at the NAND floor.

## Reproduction and receipts

The committed result manifest is [`results/build002/manifest.json`](results/build002/manifest.json), whose file SHA-256 is `CE25AF5F5DCEAF74E0182E729E09B3E6463F6D286BAE165424C2AF78EA01D809`. It records `.NET 8.0.29`, master seed `0x5041485742303032`, the exact generator command, and hashes for every committed result artifact. The imported raw-source hashes are:

- HDL verification summary: `9228A81882128F4FD5A9F9BA466D1385DB5B5B829EAC01242BB09F0CD4E81B90`;
- HDL synthesis metrics: `98D141AEE19E1E741EBEC2B3D4E4DD31899FCB78FCF25FCF9DA5B41A8A93AA4D`;
- HDL toolchain bootstrap: `6B78A5C143D541028148ABF3A00649327602638FE13FB1A0D219EB746C6DD989`;
- pinned Windows OSS CAD Suite archive: 595,298,533 bytes, SHA-256 `95D3CF2A59D1617F2363EE9370BB3577799F33A07E9C66E126DDEB68E8E5814C`.

The strengthened local full-verifier receipt `.artifacts/build002-verify-final-strengthened-verification.json` records `PASS`, 183/183 tests, zero failures, zero skipped, 656,810 arithmetic checks, 260 HDL cases, 15 formal cases, 150 synthesis rows, `deterministic_replay=true`, and replay-manifest SHA-256 `ABF8EFB022FDBE6B9DAAFE800E46DB26E1D8303683398B82A4169AA9F68CF239`. That replay hash differs from the committed manifest only because its recorded output directory is `.artifacts/build002-verify-final-strengthened`; all other generated artifact bytes match.

From the repository root, the full verification path is:

```powershell
& .\scripts\build002-hdl-bootstrap.ps1
& .\scripts\verify-build002.ps1 `
  -OutputDirectory 'artifacts/build002-reproduction' `
  -HdlOutputDirectory '.artifacts/build002-hdl-reproduction'
```

The verifier checks inherited Build 000/001 evidence against the frozen baseline, restores locked dependencies, enforces formatting, builds, requires a zero-skip test pass, runs the full HDL flow, generates the evidence twice, compares all manifest hashes for deterministic replay, checks operation-class coverage, and requires the terminal decision.

To regenerate only the committed non-HDL set from the preserved corrected HDL receipt:

```powershell
dotnet run --project .\src\PrimeAxiom.Cli `
  --configuration Release `
  -- experiment-build002 `
  --output results/build002 `
  --hdl-verification-summary .artifacts/build002-hdl-full-zero-repair/verification-summary.json `
  --hdl-synthesis-metrics .artifacts/build002-hdl-full-zero-repair/synthesis-metrics.csv `
  --hdl-toolchain .artifacts/build002-hdl-full-zero-repair/toolchain-bootstrap.json
```

## Limits

- The evidence is bounded to W=4/6/8, fixed S4, the frozen workloads, and the implemented transparent constructions. It is neither an asymptotic nor universal negative.
- NAND2/DFF count and unit depth are logical metrics. There is no placed/routed FPGA result, standard-cell PPA, extracted timing, power, analog, transistor, or fabricated-silicon evidence.
- The conventional controls are explicit and reproducible, not best-known implementations. Stronger Booth/Wallace/Dadda multipliers, constant dividers, binary GCD units, and target carry structures may improve the baseline.
- Some composite GCD/LCM/rational and adapter settled-transition fields remain `NOT_MEASURED`; they were excluded from dominance claims.
- W=8 sidecar addition uses the frozen 20,000 gate-level pairs plus exhaustive semantic differential checking. Only the latter is exhaustive over the semantic pair domain.
- Pure S4 operations are full integer operations only on the declared S4-smooth common domain. Sidecar S4 results are catalog projections unless authoritative magnitude supplies the requested full result.
- The C# integrated sidecar and the combinational HDL BIN+VSC top are distinct boundaries and are not substituted for one another.
- The committed sanitized formal receipt records all 15 cases as `PASS`/complete and anchors them to the verified source-summary hash; its per-case `detail` string is `SUCCESS_MARKER_MISSING`. The raw final logs are the stronger transcript-level receipt and are preserved under the ignored corrected HDL artifact directory.
- The Linux archive is hash-locked, but the cited canonical HDL execution receipt is Windows x64. No cross-platform physical or mapped-netlist identity claim is made here.

## Final interpretation

Build 002 reached the intended fork: same bits, same NAND, same explicit state boundary, different representational mathematics. The fork was coherent. It exposed the free-commutative-monoid shadow, a product-of-chains divisibility lattice, thermometer order ideals, and a certificate/knowledge layer above authoritative magnitude.

What it did not expose was a better general arithmetic foundation. The warm structural machine bought local multiplicative and divisibility locality with more resident/interface state. The exact sidecar bought semantic completeness by carrying the conventional machine along with it. Once cold acquisition, addition, outside factors, and output crossings were charged, no tested architecture met the frozen whole-vector rule.

The machine's answer is therefore not “primes belong below bits.” It is:

> **Bits are neutral; representations make different algebras local. Prime valuation structure is a useful resident certificate domain in narrow multiplicative workloads, but it is not an earned replacement for binary magnitude.**
