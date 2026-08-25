# Build 002 HDL substrate

This directory implements the hardware portion of frozen protocol
`PAH-BUILD002-CONF0001`. Both lineages use the same two-state floor:
`pa_nand2` is the only primitive combinational design cell, and each `pa_dff`
is one separately charged edge-delimited state bit. Constants and direct nets
are not counted as gates.

## Implemented circuit families

- `pa_nand.sv`: NAND2 and transparent NAND-only NOT/AND/OR/XOR/XNOR/mux,
  half/full adders, reductions, registers, and DFF boundary.
- `pa_binary.sv`: ripple add/subtract, unsigned comparison, min/max,
  increment/counter, W-by-W shift-and-add multiply, combinational BIN-FU, and
  an equal-boundary registered BIN-FU.
- `pa_binexp.sv`: S4 binary-exponent COMPOSE, atomic CANCEL, MEET, JOIN,
  DIVIDES, VALUATION, and POWER. Lane saturation is explicit. The chainable
  checked-COMPOSE adapter conserves prior per-lane bad tags and newly earned
  saturation, so a clamped result cannot re-enter as exact.
- `pa_therm.sv`: canonical thermometer validation, threshold meet/join,
  implication-based DIVIDES, direct monotone threshold convolution, and both
  binary-exponent/thermometer converters.
- `pa_acquisition_sidecar.sv`: a charged `COLD_MAG` combinational S4 encoder
  built from constant-divisibility minterms, plus a BIN+VSC exact threshold
  query/multiply/cancel/invalidate functional unit. Rejected updates are
  atomic. This deliberately expensive cold encoder is a transparent measured
  implementation, not an assertion that lookup minterms are optimal.
- `pa_wrappers.sv`: named W=4, W=6, and W=8 elaborations for stable artifact
  identities.

Zero is an explicit tag in pure structural operations. For DIVIDES, `a | b`
uses the ordinary existential convention: nonzero divides zero, zero divides
only zero. VALUATION reports zero as `valid=1`, `infinite=1`, with exponent
bits cleared as a non-authoritative sentinel. A cold BIN+VSC zero is exact and
valid with every finite S4 threshold set; its divisibility query is true and
known-factor scale/cancel preserve zero. BIN+VSC magnitude remains
authoritative, and unsupported cofactors remain in that magnitude.

The ordinary binexp operation wrappers are deliberately one-shot leaf
measurement tops: they expose newly earned saturation but do not define
persistent re-entry. `pa_binexp_checked_compose_w{4,6,8}` is the chainable
path; callers must carry its `bad_y` metadata into the next `bad_a`/`bad_b`.

## Reproduce

The exact suite is locked in `toolchain.lock.json`. The bootstrap verifies the
archive length and SHA-256 before extraction. It also rejects launcher stubs
that return exit zero with a failure message.

```powershell
& .\scripts\build002-hdl-bootstrap.ps1
& .\scripts\build002-hdl-verify.ps1
```

For a W=4 development pass:

```powershell
& .\scripts\build002-hdl-verify.ps1 -Quick
```

Artifacts are written under ignored `.artifacts/build002-hdl/`. Each run
retains lint, independent Icarus simulation, Yosys internal-SAT, declared
netlist, optimized netlist, analyzer, summary, and hash-manifest receipts.

On Windows, the release's tiny SBY/SMTBMC launchers can falsely succeed when
the extraction path contains spaces. The bootstrap calls their bundled Python
scripts directly and gives `verilator_bin.exe` a verified 8.3 short-path
`VERILATOR_ROOT`. Linux uses the native suite entry points.

## Synthesis evidence classes

`STRUCTURAL_DECLARED` is measured from the hierarchical explicit-NAND graph
before flattening or Boolean optimization. `STRUCTURAL_OPTIMIZED` uses the same
ordered sources and passes for every top:

```text
proc -> flatten -> opt -> memory_map -> techmap -> opt -> dffunmap
     -> abc -g NAND -> NOT-to-tied-NAND techmap -> opt_clean
```

The runner requires Yosys's normal completion marker and ABC reintegration
markers. The deterministic analyzer then rejects X/Z constants, unresolved or
forbidden cells, duplicate drivers, undriven used nets, and combinational
cycles. Optimized combinational cells must be exactly `$_NAND_`; recognized
DFF boundary cells are counted separately. `$scopeinfo` metadata is deleted
before emission and never counted as hardware.

The reported depth is unit-NAND dependency depth. It is not silicon timing,
and NAND/DFF/wire/fanout dimensions are never collapsed into one score.

## Verification boundary

Self-checking Icarus benches exhaust primitive truth tables, all binary word
pairs at W=4/6/8, all legal bounded S4 vector pairs, every W-bit cold encoder
input, zero/malformed/saturation/overflow cases, sidecar operations, and
two-stage bad-tag re-entry checks for checked COMPOSE.
Formal harnesses use high-level arithmetic only as proof-oracle logic; it is
not included in synthesis top lists or cost netlists. The harnesses prove
baseline, binary-exponent, checked bad-tag conservation, thermometer, cold
acquisition, and sidecar contracts at each registered width with Yosys MiniSAT
after `chformal -lower`.

No result here models analog voltage, transistor sizing, hazards, wire length,
placement, clock trees, setup/hold, metastability, process variation, energy,
or FPGA LUT place-and-route.
