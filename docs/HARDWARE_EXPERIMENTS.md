# Build 002 hardware experiments

## Purpose and status boundary

This document records the registered methods, implemented circuit families, and preserved hardware receipts for Build 002. The controlling protocol is [`PAH-BUILD002-CONF0001`](../research/build002_experiment_plan.md), frozen before comparative execution. Its SHA-256 in the verified workspace is:

```text
24c770290a97a1c467dbcc7b4c97ca9ee875efc21ade837ca9d96049acd76745
```

The protocol identifier, rather than prose in this file, controls the experiment. The architecture and accounting interpretations are in [Hardware Architecture](HARDWARE_ARCHITECTURE.md), [Hardware Cost Model](HARDWARE_COST_MODEL.md), and [Hardware Mathematics](HARDWARE_MATHEMATICS.md). The HDL implementation boundary is summarized in [`hdl/README.md`](../hdl/README.md).

The corrected HDL matrix passes `260/260` tool-flow cases. The separately generated and committed [Build 002 result set](../results/build002/README.md) records `656,810` bounded checks with zero failures, all A–F/R families as `COMPLETE_BOUNDED`, 15 passing formal cases, and 150 validated synthesis rows. Together these receipts satisfy the frozen decision preconditions and earn the terminal classification `NO_HARDWARE_ADVANTAGE`. That is a bounded result under the registered vector rule, not a claim that local valuation operations never help or that physical hardware has been measured.

Some composite GCD/LCM/rational transition fields remain `NOT_MEASURED`. The generated coverage receipt explicitly excludes them from the terminal negative; this document never converts a `-1` sentinel into zero. The full machine-readable boundary is [protocol_coverage.json](../results/build002/protocol_coverage.json).

Local raw HDL artifacts are intentionally ignored by Git. Their paths below are reproducible workspace paths, and their receipt hashes are the stable evidence anchors.

## Fixed floor and finite domains

Both lineages use the same charged logical floor:

```text
ideal two-state signal
-> two-input NAND
-> acyclic combinational graph
-> explicitly charged DFF boundary
```

[`NandNetlist`](../src/PrimeAxiom.Core/Hardware/NandNetlist.cs) is the C# stable-ID graph. [`pa_nand.sv`](../hdl/rtl/pa_nand.sv) is the HDL floor. Constants and direct nets are not NAND gates, but ports, nets, sinks, fanout, and DFF boundaries remain visible. The architectural fork occurs only when stable bits are interpreted as either an unsigned positional word or a bounded valuation state. Prime identity is supplied by fixed lane wiring; it is not discovered by NAND.

| Width | Magnitude domain | S4 lane caps `(v2,v3,v5,v7)` | Binary-exponent payload | Thermometer payload |
|---:|---:|---|---:|---:|
| 4 | `0..15` | `(3,2,1,1)` | 6 bits | 7 bits |
| 6 | `0..63` | `(5,3,2,2)` | 9 bits | 12 bits |
| 8 | `0..255` | `(7,5,3,2)` | 10 bits | 17 bits |

The catalog is `S4=(2,3,5,7)`. A pure structural value is an explicit zero tag plus four bounded lanes. The full exponent box is legal even when reconstruction exceeds the W-bit magnitude range; ordinary-arithmetic comparisons use only the declared common domain. A sidecar value keeps W-bit magnitude authoritative and adds exact S4 thresholds plus a validity bit. Unsupported cofactors remain in magnitude.

Zero is not an exponent vector. In the corrected contracts:

- pure valuation query reports zero as exact positive infinity using `valid=1`, `infinite=1`, and a cleared non-authoritative exponent payload;
- cold sidecar acquisition represents zero as exact and valid, with every finite S4 threshold true;
- sidecar divisibility queries on zero return true for the selected catalog prime;
- known-prime scale and cancellation preserve exact zero;
- a structure-transforming addition boundary may invalidate exact sidecar metadata rather than inventing thresholds.

Negative integers and primitive fractions are outside the primary word domain. Experiment D represents a rational as two nonnegative states and rejects denominator zero.

## Evidence classes: claims that must remain separate

| Evidence class or layer | What this build may claim | What it must not be read as |
|---|---|---|
| `SEMANTIC_ORACLE` / semantic state model | The ordinary arithmetic answer or representation contract used for differential checking. | A circuit, gate cost, latency, or formal proof of RTL. |
| `SEMANTIC_STEP` / `SEMANTIC_REJECTION` | A checked algorithmic transition, controller decision, or rejection rule. | Settled NAND switching unless the actual graph was replayed. |
| `COMPOSITE_STRUCTURAL_DECLARED` | An explicitly stated composition of measured constructed circuits, with any unmeasured controller or transition field left `NOT_MEASURED`. | A newly synthesized integrated top or a complete dynamic trace. |
| `STRUCTURAL_DECLARED` | The exact NAND/DFF graph emitted by the named transparent construction or pre-optimization explicit-NAND HDL elaboration. | A minimum circuit, optimized mapping, silicon area, or physical delay. |
| `STRUCTURAL_DECLARED_INTEGRATED` | A declared graph that includes the named persistent state/control boundary. | Proof that every source regime, adapter, and output obligation is integrated. |
| `STRUCTURAL_OPTIMIZED` | The result of the common pinned Yosys/ABC NAND mapping and post-map validation. | A replacement for the declared graph or a target-independent optimum. |
| `EXHAUSTIVE_SIMULATION` | Every vector in the stated finite testbench domain was executed by Icarus. | An unbounded proof or coverage of a different interface/workload. |
| `SEEDED_SIMULATION` | Only the frozen deterministic sample was executed. | Exhaustive coverage. No final HDL row below needed this fallback. |
| `FORMAL_SAT` | Yosys MiniSAT found no counterexample to the assertions in the named finite combinational harness under its assumptions. | A proof of unstated properties, sequential liveness, physical behavior, or C#/HDL identity outside the harness. |
| `ANALYTIC` | A formula or representation estimate. | A simulated, synthesized, or measured circuit. |
| Physical evidence | No FPGA place-and-route, ASIC mapping, extracted timing, energy, or fabricated measurement exists in this build. | NAND count/depth must never be relabeled as area, frequency, power, or energy. |

`nand2_static`, DFF/state bits, ports, nets, sinks, fanout, cross-lane connections, and unit-NAND depth form a vector. Dynamic counts likewise remain a vector. No post-hoc weighted scalar is permitted. Details are defined in [Hardware Cost Model](HARDWARE_COST_MODEL.md).

## Source regimes and output obligations

Source regimes are never pooled:

| Regime | Contract |
|---|---|
| `COLD_MAG` | Inputs arrive only as W-bit magnitudes. Every structural acquisition circuit or cycle is charged. |
| `WARM_RESIDENT` | Legal structural state is already resident because an earlier measured action produced or loaded it; initial loading is reported once. |
| `WARM_GENERATED` | A trace starts from measured constructors/scales and never receives a free factored operand. |

Output obligations are also never pooled:

| Obligation | Contract |
|---|---|
| `PREDICATE_ONLY` | Only the Boolean/query answer is required. |
| `STRUCTURAL_FINAL` | The final exact structural state is required; magnitude is not requested. |
| `MAGNITUDE_FINAL` | One exact ordinary magnitude is required at trace end. |
| `MAGNITUDE_EVERY_OP` | Exact ordinary magnitude is required after every instruction. |

A local structural instruction is not compared directly with cold binary work unless both input-preparation paths and the same output obligation are included. Oracle-only reconstruction is not charged as implementation logic, but contract-required reconstruction is.

## Frozen experiments A–F/R

The table states the registered method and the resulting committed coverage. `notMeasuredCostCells` is retained because bounded completion does not imply that every inapplicable or unused composite transition field was fabricated.

| Experiment | Registered method and finite domain | Source/output contracts | Committed coverage |
|---|---|---|---|
| **A — multiplier versus compose** | At W=4/6/8, check every legal common-domain operand pair. Compare full `W x W -> 2W` binary multiply with binary-exponent and thermometer compose. Separate products fitting W, fitting 2W, and saturating a lane. Report operation-only and encode/execute/reconstruct views. | `COLD_MAG` and resident structural views; `STRUCTURAL_FINAL` and matched `MAGNITUDE_FINAL`. | `COMPLETE_BOUNDED`: 45 rows; 0 integrated rows; 114 unmeasured cost cells. Binary multiply, both compose encodings, and charged adapters remain phase- and contract-separated. |
| **B — repeated multiply/cancel** | Eight deterministic 32-instruction traces per width use factors 2, 3, 5, and 7. Four traces contain only legal cancellation; four contain rejected cancellation. Rejection is atomic. | `WARM_GENERATED`; `STRUCTURAL_FINAL`, `MAGNITUDE_FINAL`, and `MAGNITUDE_EVERY_OP`. | `COMPLETE_BOUNDED`: 240 rows; 144 integrated rows; 216 unmeasured cost cells. All eight traces run matched persistent binary and structural machines under all three obligations. Unmeasured adapter transitions are not used to claim Pareto dominance. |
| **C — GCD/LCM and divisibility** | Exhaust the W=4 and W=6 pair domains. W=8 uses all 65,536 word pairs when under the ten-minute phase budget, otherwise the frozen 20,000-pair set is labeled seeded. Distinguish full binary answers, S4-smooth structural answers, catalog projections, and predicate-only queries. | Cold full-binary and warm structural contracts; `PREDICATE_ONLY`, `STRUCTURAL_FINAL`, or `MAGNITUDE_FINAL` as appropriate. | `COMPLETE_BOUNDED`: 24 rows; 6 integrated rows; 24 unmeasured cost cells. Full binary GCD/LCM semantics, S4 meet/join/divides, adapters, and exact-sidecar query hardware are separated. |
| **D — rational reduction** | Eight deterministic numerator/denominator cases per width include catalog factors, unsupported shared cofactors, and denominator zero. Distinguish fully reduced binary rationals from catalog-only structural projection. | Cold or warm inputs as declared; binary `MAGNITUDE_FINAL` versus structural `STRUCTURAL_FINAL`. | `COMPLETE_BOUNDED`: 54 rows; 0 integrated rows; 162 unmeasured cost cells. Eight rational cases plus denominator zero per width retain full-binary versus catalog-projection status; unbuilt integrated rational control remains unclaimed. |
| **E — mixed addition stress** | Eight traces per width execute `multiply -> multiply -> exact divide -> add -> multiply`, covering unequal/equal valuations, extra cancellation, coprime sums, zero, overflow, and unsupported cofactors. Record facts retained, invalidated, refreshed, and reconstructed at addition. | `WARM_GENERATED`, `MAGNITUDE_EVERY_OP`, with delayed and eager refresh policies. | `COMPLETE_BOUNDED`: 165 integrated rows; 0 unmeasured cost cells. All eight traces run the matched binary machine and persistent sidecar hardware with phase-separated ingress, execution, and recovery. |
| **F — hostile support and metadata** | For each width use every representable prime above 7, eligible semiprimes whose factors exceed 7, odd non-S4 values, and 0..15. Run frequent-addition, constant-reconstruction, and rapidly changing support traces; retain metadata overhead even when no structural fact helps. | `WARM_GENERATED`, `MAGNITUDE_EVERY_OP`; unsupported cofactors remain in authoritative magnitude. | `COMPLETE_BOUNDED`: 63 integrated rows; 0 unmeasured cost cells. All three hostile trace families per width run matched binary, delayed-refresh sidecar, and eager-refresh sidecar hardware. |
| **R — representation search** | Compare binary exponents, full thermometer thresholds, presence-only bits, and exact sidecar thresholds at identical caps and operations. Sparse/CAM estimates must include tags, comparators, muxes, update, miss, and control and remain `ANALYTIC` unless implemented. | Matched source and output contracts for each comparison. | `COMPLETE_BOUNDED`: 18 rows; 0 integrated rows; 54 unmeasured cost cells. Implemented encodings/adapters retain distinct evidence classes; no sparse/CAM estimate is relabeled as measured. |

The frozen seed is `0x5041485742303032`; derived seeds use the protocol's SHA-256/SplitMix64 rule. The exact trace definitions live in [Build002Workloads.cs](../src/PrimeAxiom.Cli/Build002Workloads.cs). If a verifier uses the W=8 fallback, its row must say `SEEDED`; successful smaller-domain coverage cannot be extrapolated.

## Implemented C# circuit and semantic families

The committed C# source provides these distinct layers:

| Source | Implemented role | Evidence boundary |
|---|---|---|
| [NandNetlist.cs](../src/PrimeAxiom.Core/Hardware/NandNetlist.cs) | Stable nodes, NAND-only construction, DFF boundary metadata, exact evaluation, transition tracing, loop/driver validation, and static graph metrics. | Shared declared substrate. |
| [BaselineHardware.cs](../src/PrimeAxiom.Core/Hardware/BaselineHardware.cs) | Derived gates, ripple add/subtract, comparison, shift-add multiply, combinational BIN-FU, registers/counter, and registered BIN-FU. | Transparent conventional graphs; not an optimal multiplier claim. |
| [BaselineAlgorithmHardware.cs](../src/PrimeAxiom.Core/Hardware/BaselineAlgorithmHardware.cs) | Unrolled restoring division and a registered subtractive-GCD state machine. | Declared circuit plus checked semantic-step receipts; composite LCM is separately labeled. |
| [ExperimentalHardware.cs](../src/PrimeAxiom.Core/Hardware/ExperimentalHardware.cs) | Binary-exponent compose/cancel/meet/join/divides/FU and thermometer compose/meet/join/divides/validation/query. | Primarily operation-only declared graphs. |
| [RepresentationAdapterHardware.cs](../src/PrimeAxiom.Core/Hardware/RepresentationAdapterHardware.cs) | Magnitude-to-binary-exponent acquisition, reconstruction, and binary/thermometer presence projections. | Charged adapters; presence is explicitly lossy. |
| [WarmStructuralHardware.cs](../src/PrimeAxiom.Core/Hardware/WarmStructuralHardware.cs) | Matched persistent warm structural and binary scale/cancel machines with state, control, validation, and atomic hold. | `STRUCTURAL_DECLARED_INTEGRATED` for that narrow trace machine, not every Build 002 workload. |
| [BinaryMagnitudeDatapathHardware.cs](../src/PrimeAxiom.Core/Hardware/BinaryMagnitudeDatapathHardware.cs) | Persistent matched binary load, scale, cancel, and magnitude-add machine with the same overlapping control, status, overflow, rejection, and atomic-hold contract used for E/F. | `STRUCTURAL_DECLARED_INTEGRATED`; it is the conventional integrated E/F comparator, not a full general-purpose processor. |
| [SidecarDatapathHardware.cs](../src/PrimeAxiom.Core/Hardware/SidecarDatapathHardware.cs) | Persistent authoritative magnitude plus S4 threshold state; NAND-only load, refresh, query, scale, cancel, magnitude-add, validation, overflow/rejection, and atomic next-state logic. | `STRUCTURAL_DECLARED_INTEGRATED` for this C# graph. The generated C/E/F/ingress rows charge it explicitly; it is not the same boundary as the combinational HDL BIN+VSC top. |
| [ValuationHardwareState.cs](../src/PrimeAxiom.Core/Hardware/ValuationHardwareState.cs) | Exact semantic contracts for binary-exponent state, thermometer state, sidecar magnitude/threshold knowledge, zero, saturation, rejection, and reconstruction. | Semantic/oracle layer unless paired with an explicit graph. |
| [Build002ExperimentRunner.cs](../src/PrimeAxiom.Cli/Build002ExperimentRunner.cs) and [Build002Evidence.cs](../src/PrimeAxiom.Cli/Build002Evidence.cs) | Deterministic non-HDL rows, phase-separated costs, explicit `NOT_MEASURED` sentinels, correctness receipts, and coverage metadata. | Generated rows must be inspected; source presence alone is not a result receipt. |
| [Build002HdlEvidenceImporter.cs](../src/PrimeAxiom.Cli/Build002HdlEvidenceImporter.cs) | Schema, path, tool-lock, summary, manifest, metrics, and formal-receipt validation followed by allowlisted sanitized evidence output. | Imported metadata cannot promote a failed/incomplete source receipt or manufacture a missing non-HDL row. |

The corresponding xUnit coverage is split across [baseline](../tests/PrimeAxiom.Tests/HardwareBaselineTests.cs), [baseline algorithm](../tests/PrimeAxiom.Tests/HardwareBaselineAlgorithmTests.cs), [experimental circuit](../tests/PrimeAxiom.Tests/HardwareExperimentalCircuitTests.cs), [representation adapter](../tests/PrimeAxiom.Tests/HardwareRepresentationAdapterTests.cs), [valuation state](../tests/PrimeAxiom.Tests/HardwareValuationStateTests.cs), [warm machine](../tests/PrimeAxiom.Tests/HardwareWarmStructuralMachineTests.cs), [matched binary datapath](../tests/PrimeAxiom.Tests/HardwareBinaryMagnitudeDatapathTests.cs), [sidecar datapath](../tests/PrimeAxiom.Tests/HardwareSidecarDatapathTests.cs), and [HDL importer](../tests/PrimeAxiom.Tests/Build002HdlEvidenceImporterTests.cs) tests. Test completion is a correctness claim for those tests, not a completion marker for the frozen workload matrix.

The integrated C# sidecar graph's declared static receipts are separate from the HDL metrics: W=4 has 2,307 NAND2, 12 DFF/state bits, and unit-NAND depth 148; W=6 has 5,638 NAND2, 19 DFF/state bits, and depth 278; W=8 has 15,871 NAND2, 26 DFF/state bits, and depth 458. These exact construction metrics are regression-checked in [HardwareSidecarDatapathTests.cs](../tests/PrimeAxiom.Tests/HardwareSidecarDatapathTests.cs); they are not optimized or physical measurements.

## Implemented HDL circuit families

[`pa_wrappers.sv`](../hdl/rtl/pa_wrappers.sv) gives every elaboration a stable W=4/6/8 name. The corrected full flow lints and synthesizes 25 tops at each width:

| Family | Tops per width | Named operations |
|---|---:|---|
| Conventional binary | 7 | add, subtract, compare, multiply, combinational FU, counter, registered FU |
| Binary-exponent S4 | 8 | compose, checked compose, cancel, meet, join, divides, valuation, power |
| Thermometer S4 and converters | 7 | compose, meet, join, divides, canonical validator, binary-to-thermometer, thermometer-to-binary |
| Cold acquisition and BIN+VSC | 3 | cold exact S4 encode, threshold query, query/multiply/cancel/invalidate FU |

The RTL is in [pa_binary.sv](../hdl/rtl/pa_binary.sv), [pa_binexp.sv](../hdl/rtl/pa_binexp.sv), [pa_therm.sv](../hdl/rtl/pa_therm.sv), and [pa_acquisition_sidecar.sv](../hdl/rtl/pa_acquisition_sidecar.sv). The raw binexp operation tops are intentionally one-shot leaf measurements. Only checked compose accepts prior `bad_a`/`bad_b` tags and emits persistent `bad_y`; therefore only that adapter supports safe chained compose with saturation knowledge.

The common synthesis sequence is:

```text
proc -> flatten -> opt -> memory_map -> techmap -> opt -> dffunmap
     -> abc -g NAND -> NOT-to-tied-NAND techmap -> opt_clean
```

The analyzer rejects X/Z constants, unresolved/forbidden cells, duplicate drivers, undriven used nets, and combinational loops. Optimized combinational cells must be NAND2; recognized DFF cells remain separately charged.

## Corrected full HDL receipt

The final corrected Windows run is preserved at `.artifacts/build002-hdl-full-zero-repair/`. Its [`verification-summary.json`](../.artifacts/build002-hdl-full-zero-repair/verification-summary.json) reports:

| Phase | Cases | Result |
|---|---:|---|
| Analyzer regression | 1 | 1 pass |
| Verilator lint | 75 | 75 pass |
| Icarus self-checking simulation | 19 | 19 pass |
| Yosys MiniSAT formal | 15 | 15 pass |
| `STRUCTURAL_DECLARED` synthesis and validation | 75 | 75 pass |
| `STRUCTURAL_OPTIMIZED` synthesis and validation | 75 | 75 pass |
| **Total** | **260** | **260 pass, 0 fail** |

The 19 simulation cases are one primitive truth-table bench plus six benches at each of three widths. Their executed finite domains were:

| Bench | W=4 | W=6 | W=8 |
|---|---:|---:|---:|
| Binary word pairs | 256 | 4,096 | 65,536 |
| Binary-exponent legal states / ordered pairs | 48 / 2,304 | 216 / 46,656 | 576 / 331,776 |
| Thermometer legal states / ordered pairs | 48 / 2,304 | 216 / 46,656 | 576 / 331,776 |
| Counter states | 16 | 64 | 256 |
| Sidecar magnitudes / selected-prime iterations | 16 / 64 | 64 / 256 | 256 / 1,024 |
| Checked-compose bad patterns / chained re-entry probes | 16 / 1 | 16 / 1 | 16 / 1 |

The primitive bench exhausts eight input rows across its named primitive checks. The 15 formal cases are five harness families—binary, binexp, checked bad-tag conservation, thermometer, and sidecar—at all three widths. Each final log ends with `SAT proof finished - no model found: SUCCESS!`. These are assertions in [the formal harnesses](../hdl/formal/), not proofs of every synthesized top.

### Receipt hashes

| Artifact | SHA-256 |
|---|---|
| Final verification summary | `9228a81882128f4fd5a9f9ba466d1385db5b5b829eac01242bb09f0cd4e81b90` |
| Final manifest | `ea152ad904391da7ac66db69d1e21d0ca29081e3a8523ce5c2c3b6031d82d65c` |
| Final synthesis metrics CSV | `98d141aee19e1e741ebec2b3d4e4dd31899fcb78fcf25fcf9da5b41a8a93aa4d` |
| Final toolchain receipt | `6b78a5c143d541028148abf3a00649327602638fe13fb1a0d219eb746c6dd989` |

The manifest contains 751 file entries. Its file hashes are the integrity boundary for the raw logs, scripts, JSON netlists, analyzer receipts, and metrics; `260/260` is not a substitute for those artifacts.

### Pinned toolchain actually exercised

| Component | Verified Windows version |
|---|---|
| OSS CAD Suite | release `2026-08-24`, `windows-x64` |
| Archive | 595,298,533 bytes; SHA-256 `95d3cf2a59d1617f2363ee9370bb3577799f33a07e9c66e126ddeb68e8e5814c` |
| Yosys | `Yosys 0.68+120 (git sha1 a34d3baae-dirty, Release, GNU [REDACTED_PATH] 15.2.1)` in the committed sanitized receipt |
| Icarus | `Icarus Verilog version 14.0 (devel) (s20260301-391-g64f13540a-dirty)` |
| vvp | `Icarus Verilog runtime version 14.0 (devel) (s20260301-391-g64f13540a-dirty)` |
| Verilator | `Verilator 5.051 devel rev v5.050-251-g477b48fb3 (mod)` |
| SBY | `SBY v0.68` (probed, not the final proof engine) |
| yosys-smtbmc | `yosys-smtbmc-script.py [options] <yosys_smt2_output>` (recognizable script probe, not the final proof engine) |
| Z3 | `Z3 version 4.15.5 - 64 bit` (probed, not the final proof engine) |

The Linux x64 archive is locked at 741,360,658 bytes and SHA-256 `9d7f79975ef624e1119fc9690fd9b9839b67026925aff3e2a1192d861b8dbb7c`. The canonical committed HDL source receipt above is the Windows run, while [the cross-platform protocol decision gate](https://github.com/Grativy6/Prime_Axiom_Software/actions/runs/32814482250) independently passed the complete verifier on Ubuntu and Windows and preserved both raw bundles. The final formal proofs used Yosys's internal MiniSAT flow, not SBY/Z3 as a second engine; no cross-platform mapped-netlist identity or physical-hardware claim follows.

### Representative measured static rows

All 75 tops at all three widths have both declared and optimized rows in `synthesis-metrics.csv`. The compact entries below are `declared NAND / optimized NAND; declared depth / optimized depth`. They are representative measurements, not a ranking or final workload comparison.

| Top family | W=4 | W=6 | W=8 |
|---|---:|---:|---:|
| Binary add | `55/42; 15/12` | `81/66; 19/16` | `107/90; 23/20` |
| Binary multiply | `320/127; 40/31` | `720/335; 60/53` | `1280/639; 80/75` |
| Binary-exponent compose leaf | `358/58; 24/7` | `469/111; 26/12` | `506/124; 26/12` |
| Checked chainable compose | `391/82; 25/8` | `502/129; 27/14` | `539/142; 27/12` |
| Thermometer compose | `104/66; 10/8` | `219/141; 14/8` | `379/241; 18/10` |
| Cold S4 encoder | `251/44; 21/9` | `1821/172; 73/17` | `10234/632; 269/25` |
| VSC query | `45/25; 18/8` | `75/45; 22/12` | `105/65; 26/12` |
| BIN+VSC FU | `1202/322; 58/25` | `3978/732; 91/34` | `15830/1781; 287/37` |

These selected tops are combinational and have zero DFFs. Registered counter/FU tops are present as separate rows. The large difference between declared and optimized cold/sidecar graphs is itself tool- and construction-specific evidence; it neither erases acquisition cost nor predicts physical PPA.

## Generated comparison evidence

The committed [correctness receipt](../results/build002/correctness.json) reports `656,810` bounded checks, zero failures, and no skip mechanism. The tables below expose the values that drive the terminal decision. They show named vector components rather than summing unlike resources.

### W6/W8 integrated static boundaries

Entries are `NAND2 / DFF-state bits / port bits / wire bits / unit-NAND depth`, all from [static_costs.csv](../results/build002/static_costs.csv).

| Frozen comparison context | Width | Conventional integrated boundary | Experimental integrated boundary |
|---|---:|---|---|
| B warm scale/cancel | 6 | BIN: `2313 / 6 / 15 / 2324 / 270` | BINEXP-S4: `789 / 14 / 23 / 820 / 38` |
| B warm scale/cancel | 8 | BIN: `3959 / 8 / 17 / 3972 / 448` | BINEXP-S4: `864 / 15 / 24 / 896 / 38` |
| E/F load-scale-cancel-add | 6 | BIN: `2532 / 6 / 22 / 2551 / 276` | BIN+VSC-S4: `5638 / 19 / 42 / 5677 / 278` |
| E/F load-scale-cancel-add | 8 | BIN: `4242 / 8 / 26 / 4265 / 456` | BIN+VSC-S4: `15871 / 26 / 51 / 15919 / 458` |
| C predicate context | 6 | Full binary divider: `1021 / 0 / 26 / 1034 / 268` | BIN+VSC-S4: `5638 / 19 / 42 / 5677 / 278` |
| C predicate context | 8 | Full binary divider: `1729 / 0 / 34 / 1746 / 450` | BIN+VSC-S4: `15871 / 26 / 51 / 15919 / 458` |

The warm structural machine is smaller in NAND, wire, and depth, but larger in DFF/state and port bits at both decision widths. It therefore does not Pareto-dominate the matched binary machine. The exact sidecar is statically larger than the matched mixed-operation binary machine and even the full binary divider context at W6 and W8. These are vector failures, not weighted-score judgments.

### Corrected integrated sidecar ingress

The current [ingress/egress receipt](../results/build002/ingress_egress.csv) charges the whole persistent sidecar datapath's `LOAD`, not the smaller standalone valuation encoder:

| Width | Exhaustive magnitudes | Cycles | Integrated NAND evaluations | NAND evaluations per load | Support status |
|---:|---:|---:|---:|---:|---|
| 4 | 16 | 16 | 36,912 | 2,307 | `FULL_BINARY_TO_EXACT_S4_INTEGRATED_LOAD` |
| 6 | 64 | 64 | 360,832 | 5,638 | `FULL_BINARY_TO_EXACT_S4_INTEGRATED_LOAD` |
| 8 | 256 | 256 | 4,062,976 | 15,871 | `FULL_BINARY_TO_EXACT_S4_INTEGRATED_LOAD` |

Every W-bit magnitude, including zero and values with unsupported cofactors, loads authoritative magnitude and exact S4 metadata atomically. This corrected charge is why cold acquisition cannot be hidden behind an operation-local compose/query win.

### B — warm scale/cancel dynamics

Across all eight frozen 32-step traces, the `EXECUTE` phase has the same 256 requested operations, 256 cycles, 275 input-bit transitions, and 26 atomic rejections for each machine:

| Width | Machine | NAND evaluations | Settled NAND transitions | State-bit transitions | Initial NAND transitions |
|---:|---|---:|---:|---:|---:|
| 6 | BIN scale/cancel | 592,128 | 46,863 | 371 | 12,316 |
| 6 | BINEXP-S4 scale/cancel | 201,984 | 23,166 | 244 | 4,045 |
| 8 | BIN scale/cancel | 1,013,504 | 59,073 | 371 | 21,134 |
| 8 | BINEXP-S4 scale/cancel | 221,184 | 24,604 | 244 | 4,411 |

That is a real native-work advantage. Output obligations expose where it stops being a whole-machine result:

| Width | Obligation | Machine | Cycles | NAND evaluations | Reconstructs |
|---:|---|---|---:|---:|---:|
| 6 | `MAGNITUDE_FINAL` | BIN | 256 | 592,128 | 0 |
| 6 | `MAGNITUDE_FINAL` | BINEXP-S4 | 264 | 251,008 | 8 |
| 6 | `MAGNITUDE_EVERY_OP` | BIN | 256 | 592,128 | 0 |
| 6 | `MAGNITUDE_EVERY_OP` | BINEXP-S4 | 512 | 1,770,752 | 256 |
| 8 | `MAGNITUDE_FINAL` | BIN | 256 | 1,013,504 | 0 |
| 8 | `MAGNITUDE_FINAL` | BINEXP-S4 | 264 | 367,928 | 8 |
| 8 | `MAGNITUDE_EVERY_OP` | BIN | 256 | 1,013,504 | 0 |
| 8 | `MAGNITUDE_EVERY_OP` | BINEXP-S4 | 512 | 4,916,992 | 256 |

The reconstruction adapters' settled-transition fields remain `NOT_MEASURED`, so the second table supports only the shown cycle/NAND/reconstruction dimensions. It is not silently upgraded into a full dynamic Pareto row.

### E/F — integrated sidecar dynamics

These totals combine all recorded `INGRESS`, `EXECUTE`, and `ADDITION_RECOVERY` rows for the same `WARM_GENERATED + MAGNITUDE_EVERY_OP` contract. E contains eight traces per policy; F contains three hostile traces per policy. “Delayed” preserves sound lower bounds with invalid exactness; “eager” refreshes after invalidating additions.

At W=8, integrated sidecar addition correctness combines exhaustive semantic differential checking with the frozen 20,000 gate-level pair set. The rows below do not relabel that gate sample as exhaustive.

| Family | Width | Policy | Cycles | NAND evaluations | Settled NAND transitions | Initial NAND transitions | State-bit transitions | Refreshes | Rejections |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|
| E | 6 | BIN baseline | 48 | 121,536 | 9,678 | 13,584 | 77 | 0 | 1 |
| E | 6 | Sidecar delayed | 48 | 270,624 | 25,942 | 28,052 | 149 | 0 | 1 |
| E | 6 | Sidecar eager | 55 | 310,090 | 27,466 | 28,052 | 168 | 7 | 1 |
| E | 8 | BIN baseline | 48 | 203,616 | 11,966 | 22,784 | 77 | 0 | 1 |
| E | 8 | Sidecar delayed | 48 | 761,808 | 52,671 | 74,730 | 159 | 0 | 1 |
| E | 8 | Sidecar eager | 55 | 872,905 | 54,533 | 74,730 | 180 | 7 | 1 |
| F | 6 | BIN baseline | 99 | 250,668 | 24,538 | 5,094 | 95 | 0 | 60 |
| F | 6 | Sidecar delayed | 99 | 558,162 | 50,337 | 10,507 | 141 | 0 | 60 |
| F | 6 | Sidecar eager | 121 | 682,198 | 61,420 | 10,507 | 242 | 22 | 60 |
| F | 8 | BIN baseline | 99 | 419,958 | 38,802 | 8,544 | 140 | 0 | 54 |
| F | 8 | Sidecar delayed | 99 | 1,571,229 | 122,482 | 28,005 | 202 | 0 | 54 |
| F | 8 | Sidecar eager | 126 | 1,999,746 | 154,201 | 28,005 | 339 | 27 | 54 |

The exact input-transition and phase rows remain in [workload_matrix.csv](../results/build002/workload_matrix.csv). E and F show no integrated dynamic recovery: the sidecar performs more NAND evaluations and settled transitions than the matched binary machine, and eager refresh adds cycles and state activity.

## Preserved failures, superseded runs, and repairs

Raw failures were retained rather than rewritten:

| Preserved directory | Scope and result | Summary SHA-256 | Evidential role |
|---|---|---|---|
| `.artifacts/build002-hdl-failed-quick-0001/` | `QUICK_W4`, 69/82 pass, 13 fail | `08dbc63151fda2aca35d63427e2edb87d74085d90cb81ed66e17482523e0aefe` | Four formal harness failures and nine declared-netlist validation failures exposed harness/analyzer defects. |
| `.artifacts/build002-hdl-quick-fixed/` | `QUICK_W4`, 83/83 pass | `0e63a688a633f1d7efe83fe1695f7294fea9e2d6ad592952da5896918578e767` | Demonstrated the first harness/analyzer repair, before the zero/saturation contract audit. |
| `.artifacts/build002-hdl-full/` | `FULL_W4_W6_W8`, 245/245 pass | `33f1f493055057c8a75132d0fd696ee1a4ac122e9932bc37edb1fde2e4c2b12e` | Superseded full run under the defective zero/persistence contract; passing its then-current tests did not make that contract correct. |
| `.artifacts/build002-hdl-quick-zero-repair/` | `QUICK_W4`, 88/88 pass | `e76d68d8ff3735c5ec7d2041d9c81b621aa305556abb215876f7e4d6778a3968` | W=4 validation of the repaired zero and checked-compose contract. |
| `.artifacts/build002-hdl-full-zero-repair/` | `FULL_W4_W6_W8`, 260/260 pass | `9228a81882128f4fd5a9f9ba466d1385db5b5b829eac01242bb09f0cd4e81b90` | Current corrected full HDL evidence. |

The failed quick receipt's four formal failures were diagnosed as proof-harness problems: internal unconstrained registers and an expected-result mux introduced X/don't-care behavior. Inputs were made explicit, assertions became opcode-guarded, and the SAT command now includes `-set-assumes`. The nine declared-netlist failures were analyzer false positives around hierarchical aliases, constants, and parameter-elided ports; alias equivalence was repaired and protected by analyzer regression tests. These diagnoses do not erase the failed receipt.

Two later cross-contract bugs were more important because the pre-repair matrix passed:

1. **Zero contract bug.** The HDL cold encoder marked zero invalid and cleared thresholds, unlike the C# semantic contract and the declared `v_p(0)=+infinity` convention. Query and known-factor update therefore rejected zero. The repair makes zero exact/valid, sets every finite threshold, makes the query true, preserves zero through scale/cancel, and keeps structure-transforming invalidation explicit.
2. **Saturation re-entry bug.** Raw compose emitted a new saturation vector but accepted no prior saturation state, so a caller could feed clamped lanes back as apparently exact. The repair labels raw wrappers as one-shot leaves and adds checked compose with `bad_a`, `bad_b`, and conserved `bad_y`. A two-stage test and formal harness prove that prior/new bad tags persist for nonzero results; an explicit zero tag earns fresh exact zero and clears the lane tags. Valuation also gained a separate infinity bit so zero cannot be confused with exponent zero.

Toolchain bring-up also found reproducibility defects that are now guarded: the archive lock field is `bytes` rather than `expected_bytes`; Windows requires both suite `bin` and `lib` on the scoped path; PowerShell must call `verilator_bin.exe`; and SBY/yosys-smtbmc must run through bundled Python with recognizable-output checks so a launcher error cannot be accepted as a successful probe.

## Exact reproduction

The canonical end-to-end verifier restores, formats, builds, runs the zero-skip test suite, regenerates the full HDL evidence, generates the result set twice, compares deterministic hashes, validates the manifest, protects inherited Build 000/001 evidence, and writes a verification receipt:

```powershell
& .\scripts\verify-build002.ps1
```

To replay the aggregate against the already preserved corrected HDL evidence without rerunning HDL:

```powershell
& .\scripts\verify-build002.ps1 `
  -UseExistingHdlEvidence `
  -HdlOutputDirectory '.artifacts/build002-hdl-full-zero-repair' `
  -OutputDirectory 'artifacts/build002-reproduction'
```

The lower-level HDL commands, from the repository root in PowerShell, are:

```powershell
# Acquire or reuse the pinned archive, verify bytes/SHA-256, extract, and probe tools.
& .\scripts\build002-hdl-bootstrap.ps1

# Reproduce the full W=4/6/8 HDL matrix into a fresh directory.
& .\scripts\build002-hdl-verify.ps1 `
  -OutputDirectory '.artifacts/build002-hdl-reproduction'

# Optional bounded development pass; this is not the full receipt.
& .\scripts\build002-hdl-verify.ps1 `
  -Quick `
  -OutputDirectory '.artifacts/build002-hdl-reproduction-quick'
```

The verification script uses [the exact lock](../hdl/toolchain.lock.json), ordered RTL list, lint/simulation/formal commands, synthesis passes, post-map NAND conversion, netlist analyzer, completion guards, and deterministic receipt writer. A fresh run should be preserved rather than written over any directory cited above.

To verify the four main hashes from a reproduction:

```powershell
Get-FileHash -Algorithm SHA256 `
  '.artifacts/build002-hdl-reproduction/verification-summary.json', `
  '.artifacts/build002-hdl-reproduction/manifest.json', `
  '.artifacts/build002-hdl-reproduction/synthesis-metrics.csv', `
  '.artifacts/build002-hdl-reproduction/toolchain-bootstrap.json'
```

To regenerate the committed C# semantic/declared evidence after restoring and testing the pinned .NET solution:

```powershell
dotnet restore .\PrimeAxiom.sln --locked-mode
dotnet build .\PrimeAxiom.sln --configuration Release --no-restore
dotnet test .\PrimeAxiom.sln --configuration Release --no-build --no-restore

dotnet run --project .\src\PrimeAxiom.Cli `
  --configuration Release `
  -- experiment-build002 `
  --output results/build002 `
  --hdl-verification-summary .artifacts/build002-hdl-reproduction/verification-summary.json `
  --hdl-synthesis-metrics .artifacts/build002-hdl-reproduction/synthesis-metrics.csv `
  --hdl-toolchain .artifacts/build002-hdl-reproduction/toolchain-bootstrap.json
```

The generator validates and path-sanitizes all three supplied HDL receipts before emitting [150 synthesis rows](../results/build002/synthesis_metrics.csv), [15 formal receipts](../results/build002/formal_receipts.json), and the [toolchain receipt](../results/build002/toolchain.json). Imported HDL evidence does not silently turn a missing non-HDL field into a measured synthesis or dynamic value. Inspect [protocol_coverage.json](../results/build002/protocol_coverage.json), every `NOT_MEASURED` value, the [result manifest](../results/build002/manifest.json), and the phase-separated CSVs before interpreting the generated set.

## Physical claim boundary

No result in this document crosses into FPGA LUT place-and-route, ASIC standard-cell mapping, extracted parasitics, static timing analysis, transistor/device simulation, measured energy, or fabricated silicon. The logical model omits hazards, wire length, buffering, capacitance, clock trees, setup/hold, metastability, process/voltage/temperature, and layout congestion. A future physical result must name its target, library/device, constraints, tool versions, placement/routing status, and evidence class; it may not retroactively relabel these NAND receipts.

## Generated-results decision

<!-- BUILD002_GENERATED_RESULTS_DECISION_START -->

The committed generated result earns:

> **`NO_HARDWARE_ADVANTAGE`**

This invokes fallback rule 5 of the frozen protocol after rules 1–4 fail. It is the strongest single classification justified by this bounded matrix.

### Completion receipts

- [Generated manifest](../results/build002/manifest.json): SHA-256 `ce25af5f5dceaf74e0182e729e09b3e6463f6d286bae165424c2af78ea01d809`; runtime `.NET 8.0.29`; generator command reproduced above.
- [Protocol coverage](../results/build002/protocol_coverage.json): all seven A–F/R families are `COMPLETE_BOUNDED`, totaling 609 workload rows across W=4/6/8; the decision flag is true.
- [Correctness](../results/build002/correctness.json): `656,810` checks, `0` failures. The generator has no skip mechanism, and the required-test receipt records zero skipped checks.
- HDL: `260/260` verification cases, including `15/15` formal cases; `150` synthesis rows comprise 75 `STRUCTURAL_DECLARED` and 75 `STRUCTURAL_OPTIMIZED` rows, all with measured warning counts.
- Inherited Build 000/001 evidence matches the frozen manifest/report hashes recorded in the coverage receipt.

### Frozen-rule audit

1. **Alternative arithmetic unit candidate — not satisfied.** The integrated BIN+VSC-S4 machine is statically larger than the matched binary mixed-operation machine at W6 and W8, and E's delayed/eager sidecar policies use more NAND evaluations and transitions under the same `MAGNITUDE_EVERY_OP` contract.
2. **Prime structural coprocessor candidate — not satisfied.** The exact sidecar's W6/W8 static overhead exceeds the full binary divider/query context before a 32-operation dynamic horizon is charged. Static hardware cannot be amortized away under the registered Pareto rule.
3. **Warm-state specialized advantage — not satisfied.** B confirms lower NAND count, depth, wiring, evaluations, and transitions for resident structural scale/cancel, but the structural machine uses more DFF/state and port bits at both W6 and W8. This is a tradeoff, not Pareto dominance.
4. **Unexpected architecture — not satisfied.** No architecture outside the preregistered structural, thermometer, presence, or exact-sidecar families survived the integrated adversarial tests.
5. **Fallback — selected.** `NO_HARDWARE_ADVANTAGE` follows because none of the positive rules is satisfied.

### Meaning of the negative

The terminal label does not say that prime/valuation coordinates are useless. It preserves the measured local warm-state advantage and rejects only a same-contract whole-machine hardware advantage under this fixed S4 catalog, these widths, these transparent constructions, and the frozen vector rule. Some composite GCD/LCM/rational settled-transition totals remain `NOT_MEASURED`; they were not needed or used to manufacture the terminal negative. No FPGA, ASIC, energy, or fabricated-silicon conclusion follows.

<!-- BUILD002_GENERATED_RESULTS_DECISION_END -->
