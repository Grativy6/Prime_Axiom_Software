# Build 002 hardware cost model

## Purpose

Build 002 compares two organizations of computation above the same ideal binary floor. This document defines what is charged and what the measurements mean. It intentionally does not collapse unlike hardware resources into one score.

The controlling frozen protocol is `PAH-BUILD002-CONF0001` in `research/build002_experiment_plan.md`, SHA-256 `24C770290A97A1C467DBCC7B4C97CA9EE875EFC21ADE837CA9D96049ACD76745`.

## Evidence boundary

The primary substrate is:

```text
ideal two-state signal
-> two-input NAND
-> acyclic combinational netlist
-> edge-delimited state bit
```

Both lineages are evaluated at the same boundary. The model can establish Boolean function, explicit logical structure, finite-state behavior, and bounded operation/workload cost. It cannot establish fabricated area, clock frequency, physical wire delay, energy, thermal behavior, metastability tolerance, or reliability.

Results carry one of these evidence classes:

| Class | Meaning |
|---|---|
| `SEMANTIC_ORACLE` | Ordinary arithmetic used only to state an expected result. |
| `STRUCTURAL_DECLARED` | Stable-ID NAND graph emitted by the repository's explicit construction. |
| `STRUCTURAL_OPTIMIZED` | Common Yosys/ABC flow mapped to NAND and validated from JSON. |
| `EXHAUSTIVE_SIMULATION` | Every input in the declared finite domain was executed. |
| `SEEDED_SIMULATION` | The frozen deterministic sample was executed; no exhaustive claim. |
| `FORMAL_SAT` | The recorded RTL property was proved by the named SAT flow. |
| `ANALYTIC` | A formula or estimate, not an implemented measurement. |

A result may carry more than one compatible class, but an analytic estimate does not become a synthesized result merely because it agrees with one.

## Static resource vector

Every implemented circuit reports the full vector below.

| Field | Definition |
|---|---|
| `nand2_static` | Number of two-input NAND cells in one elaborated instance. |
| `dff_static` | Number of D-type state cells at the measured architecture boundary. |
| `state_bits` | Architecturally visible stored bits. It normally equals `dff_static`; a mismatch requires explanation. |
| `input_bits` | Primary combinational/control inputs at the boundary. |
| `output_bits` | Primary combinational/status outputs at the boundary. |
| `port_bits` | `input_bits + output_bits`, retained so a narrower contract cannot masquerade as a free area win. |
| `wire_bits` | Unique one-bit nets in the flattened logical model, including boundary nets. Buses are counted bitwise. |
| `connections_static` | Total net sinks: NAND input pins, named output sinks, and DFF data/control sinks represented by the model. |
| `max_fanout` | Largest sink count of any logical net. |
| `cross_lane_connections` | Net sinks whose source and destination have different declared prime-lane/functional regions. Boundary/control crossings are separately identified where available. |
| `unit_nand_critical_depth` | Maximum NAND dependency count from a primary input or DFF Q to a primary output or DFF D. Direct nets add zero. |
| `combinational_loop_status` | `ACYCLIC` only when topological validation succeeds. A loop invalidates depth and dynamic results. |

### What a wire count is not

`wire_bits` and `connections_static` reveal graph pressure, not physical routing. They do not include geometric length, congestion, buffering, capacitance, metal layers, or placement. `max_fanout` identifies a possible loading problem but does not predict its delay. Cross-lane connections are a locality diagnostic, not a routed interconnect result.

### State and control

Operation-only combinational units and integrated registered units are both reported. An integrated comparison includes:

- equal operand/result register boundaries where the contract needs them;
- opcode decoding and result muxes;
- valid, zero, overflow, saturation, underflow, and rejection state;
- lane-selection and query muxes;
- encode/reconstruct/refresh control when required by the source and output contract.

Incremental sidecar cost is useful for design, but final classification uses the integrated total. A sidecar cannot count the host magnitude register or acquisition datapath as someone else's free hardware.

## Dynamic resource vector

For each operation and ordered trace, the runner reports:

| Field | Definition |
|---|---|
| `instructions` | Architecturally requested operations. Adapters are not hidden inside this count. |
| `cycles` | Declared machine cycles, including sequential acquisition or reconstruction. |
| `nand_evaluations` | Settled NAND-cell evaluations. For a one-cycle acyclic combinational unit this normally equals static NAND count per invocation. |
| `nand_output_transitions` | Gate outputs that differ from the preceding settled vector. |
| `state_bit_transitions` | DFF-visible bits changed at a cycle boundary. |
| `input_bit_transitions` | Primary input bits changed between invocations. |
| `rejections` | Atomic illegal/unsupported operations. Rejected operations may still consume validation/control cost. |
| `encodes` | Binary-magnitude to structural acquisitions. |
| `reconstructs` | Structural to exact magnitude conversions required by the contract. |
| `refreshes` | Re-establishment of exact metadata after invalidation. |

The switching estimate compares settled Boolean states. It deliberately excludes hazards, glitches, clock-tree activity, short-circuit current, capacitance, and data-dependent analog effects. It is a deterministic switching proxy, not power or energy.

The transition from an all-zero observation state to the first vector is reported separately. This prevents initialization convention from changing a trace ranking.

## Phase accounting

Costs are retained under four phase labels:

```text
INGRESS
EXECUTE
ADDITION_RECOVERY
EGRESS
```

For one implementation, workload, source regime, and output obligation, like fields may be summed across phases. The full phase rows remain present so cost displacement is visible.

Examples:

- `COLD_MAG + STRUCTURAL_FINAL` charges factor/valuation acquisition but not final magnitude reconstruction.
- `WARM_RESIDENT + PREDICATE_ONLY` charges resident state and query hardware but no fictional encode on every query.
- `WARM_GENERATED + MAGNITUDE_FINAL` charges measured structural construction and one final reconstruction.
- `MAGNITUDE_EVERY_OP` charges a magnitude path at every instruction boundary; delayed normalization is not allowed to erase that obligation.

## Representation cost

Representation cost includes all state needed to distinguish legal values and statuses:

- binary magnitude bits;
- exponent fields or thermometer thresholds;
- zero and validity tags;
- saturation/overflow state;
- unsupported residual/cofactor state when present;
- sparse tags, comparators, miss state, and update muxes for any CAM-like design;
- canonical-state validation when malformed inputs can reach the port.

The pure valuation unit's legal state box is not claimed to have the same cardinality or interface as W-bit unsigned magnitude. Cross-lineage operation comparisons use only their declared common semantic domain. Full-state resource comparisons keep the differing port and representable-state counts visible.

## Cold acquisition and warm persistence

Factor information is never free.

In `COLD_MAG`, the experimental lineage receives the same W-bit magnitude as the baseline. A combinational valuation detector is charged by its synthesized netlist. A repeated constant-division detector is charged by its datapath, controller/state, and actual cycles. Host-language factoring is permitted only as an oracle.

In `WARM_RESIDENT`, structural state exists because a prior measured operation produced it or because one initial load is explicitly charged. This is a different workload question, not a discounted cold benchmark.

Break-even is reported as a vector and an instruction index. If acquisition adds gates/state but later queries reduce cycles or switching, the report identifies the first frozen trace position where the applicable cumulative dynamic fields recover the acquisition delta. Static resources do not disappear at break-even and remain in the row.

## Output obligations and semantic support

The following are different contracts:

- a Boolean divisibility answer;
- a bounded S4 structural state;
- a catalog-only GCD/LCM projection;
- an exact conventional magnitude;
- an exact conventional magnitude after every instruction.

Rows with different obligations are not dominance candidates. Likewise, an S4 projection is not compared as if it were a full GCD, full factorization, or full exact division result. `support_status` distinguishes at least:

```text
FULL_EXACT
CATALOG_PROJECTION
SMOOTH_DOMAIN_ONLY
SATURATED
REJECTED
STALE_METADATA
```

Correctness and support are eligibility conditions, not cost dimensions that can be traded away.

## Addition recovery

If `a = p^x A` and `b = p^y B`, the exact valuation law gives:

- `v_p(a+b) = min(x,y)` when `x != y`;
- at least `min(x,y)` when `x = y`, with any extra valuation determined by `A+B` after the common factor is removed.

The sidecar may retain a proved lower bound without claiming an exact valuation. Any operation that requires exact thresholds must refresh them or reject. The model separately charges:

- extraction/preservation of common known thresholds;
- binary addition;
- exact refresh of uncertain lanes;
- state invalidation when refresh is deferred;
- eventual reconstruction/refactor under the output contract.

This prevents “preserved some structure” from being scored as “retained exact representation.”

## Synthesis views

Two NAND views are retained.

### Declared view

`STRUCTURAL_DECLARED` is the repository's transparent construction. It answers: how many NAND cells and graph levels did this particular earned implementation use?

### Optimized view

`STRUCTURAL_OPTIMIZED` is produced for every compared top by the same pinned Yosys/ABC pass sequence and NAND target. It answers: after common Boolean optimization, what remains of the architectural difference in this logical target?

Every optimized JSON netlist is rejected unless:

- the synthesis script reaches an explicit completion marker;
- hierarchy is flattened as declared;
- every combinational cell is exactly NAND2 after NOT-to-tied-NAND conversion;
- only the allowed DFF types remain in sequential wrappers;
- no combinational loop exists;
- raw cell histograms and the JSON SHA-256 are recorded.

Declared and optimized counts are never substituted for one another. A synthesis optimization can expose a useful equivalence or erase a hand-construction artifact, but it does not retroactively change the explicit circuit.

## Bounded transistor-equivalent context

A static CMOS NAND2 is translated as four transistors, so the optional combinational context is:

```text
nand2_transistor_equivalent = 4 * nand2_static
```

DFFs remain a separate count unless an exact flip-flop schematic is selected and named. Build 002 does not sum an assumed DFF topology into the NAND total. This bounded translation omits wiring, buffers, clock generation, input/output cells, and physical effects and therefore is not an area estimate.

## Dominance and classification

There is no weighted total. For rows with the same semantics and contract, implementation X Pareto-dominates Y only when X is no worse in every applicable charged field and strictly better in at least one. Applicability must be declared before looking at the result.

The final classification uses the frozen rules in Section 14 of the experiment protocol. In particular:

- a cheap native instruction does not establish workload advantage;
- a warm result does not establish cold advantage;
- a predicate coprocessor does not establish a replacement arithmetic unit;
- missing synthesis or formal coverage cannot support a positive hardware classification;
- incomplete required coverage yields `PARTIAL — FINAL DECISION NOT EARNED`.

## Reproducibility

Each result row carries protocol ID, implementation, width, source regime, output obligation, evidence class, and support status. The self-excluding manifest hashes every other committed result artifact and records runtime/tool versions, the exact generator command, platform, and frozen baseline commit. The source/generator revision that produced the canonical imported receipts is anchored separately by the Build 002 report and CI receipt. Generated text uses UTF-8 without BOM and LF endings. Frozen seeds and boundary cases are defined in the experiment protocol and implemented by the Build 002 runner.
