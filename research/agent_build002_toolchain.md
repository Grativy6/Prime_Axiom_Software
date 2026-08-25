# Build 002 hardware-toolchain audit

Date: 2026-08-24  
Branch audited: `build/002-same-gates-different-mathematics`  
Starting commit: `dfd2e7a409aaa114f054a0b40e4b282c68dc0d52`  
Scope: read-only host/repository audit plus a disposable, unpacked YoWASP probe. No HDL tool was installed into the host or repository, and no prior-build file was changed.

## Result

The host can run the existing .NET substrate, but it has no native HDL synthesis, simulation, place-and-route, SMT, or Graphviz command available. WSL and containers are not usable fallbacks on the audited host.

The smallest *empirically adequate* cross-platform flow is therefore a pinned OSS CAD Suite release used directly on Windows and in Ubuntu CI. Use Yosys as the canonical elaborator/synthesizer, Icarus as an independent exhaustive simulator, and Yosys's internal SAT engine as the primary bounded formal checker. Keep SymbiYosys plus Z3 as a second formal engine, not a prerequisite for every combinational proof. Do not require nextpnr for the primary NAND comparison; an FPGA place-and-route result changes the charged substrate to LUTs/FFs and should be a separately labelled corroboration.

A pinned `yowasp-yosys` wheel is attractive because it is small and platform-neutral, and most of it works on this exact Windows/Python host. It is **not sufficient as the primary flow**: its `abc -g NAND` command terminated immediately after BLIF extraction, returned process exit code 0, and never emitted ABC results, the requested post-ABC `stat`, or `End of script`. That false-success shape is unsafe for release evidence. YoWASP remains viable for Yosys parsing, `synth -noabc`, `stat`, JSON emission, and internal `sat`, or for a deliberately unoptimized hand-written/custom-techmapped NAND flow that never invokes ABC.

## Audited host

| Surface | Observed state |
|---|---|
| OS | Windows 10 `10.0.19045`, x64 |
| PowerShell | 7.6.4 |
| Repository SDK | .NET SDK 8.0.423 selected by `global.json`; .NET SDK 10.0.302 is also installed |
| Python | CPython 3.13.14 |
| pip | 26.1.2 |
| Git / GitHub CLI | Git 2.54.0.windows.1; `gh` 2.97.0 |
| Package managers | `winget` 1.29.290, pip, and `dotnet` restore are available; Chocolatey, Scoop, MSYS2/pacman, Conda/Mamba, and standalone NuGet are absent |
| Visual Studio | Visual Studio Community 2026 18.7.4; MSBuild 18.7.8.30822 exists by absolute path. Its CMake, Ninja, and LLVM/Clang optional components were not present at their normal VS paths |
| WSL | `wsl --status` and distribution listing exited 1; the client displayed installation help rather than a distribution. `systeminfo` reported `Virtualization Enabled In Firmware: No` |
| Containers | No Docker or Podman command |

### HDL/formal/visual tools

None of these commands was available on `PATH`:

```text
iverilog  vvp  verilator
yosys     yosys-config  abc
nextpnr-generic  nextpnr-ice40  nextpnr-ecp5  nextpnr-himbaechel
sby       yosys-smtbmc
z3        boolector  bitwuzla  cvc5
dot       neato      gtkwave
ghdl      nvc        sv2v      slang      surelog      verible-*
```

No OSS CAD Suite, Icarus, Yosys, Graphviz, MSYS2, Cygwin, Scoop, or Chocolatey directory existed at the common system/user installation paths checked. `winget list` likewise showed no relevant HDL/formal package. Python had `pytest 8.4.2`, but not `z3-solver`, Amaranth/nMigen/Migen, cocotb, PyVerilog, hdlparse, Graphviz, NetworkX, or Hypothesis.

This is an availability statement for the audited host and search paths, not proof that no unindexed executable exists anywhere on disk.

## Existing repository substrate

The existing lower layer is useful as an oracle and historical control, but it is not yet an HDL netlist:

- `GateNetwork.Nand` is the only primitive combinational evaluator. `Not`, `And`, `Or`, `Xor`, `Xnor`, and `Mux` are compositions of it.
- `Signal` carries only a `BitState` and dependency depth. There are no stable node IDs or structural connection records, so the model cannot recover wire count, fanout, placement, or a reusable gate graph.
- `GateCost.NandEvaluations` counts executed NAND calls. For one fixed combinational operation this is a transparent logical-work/static-instance analogue, but it is not a synthesized-cell receipt. `CriticalPathDepth` is a unit-NAND dependency depth; serial receipt composition is not a static circuit timing analysis.
- `BinaryCircuit` contains NAND-derived half/full adders, ripple addition, subtraction, comparison, min/max, increment, and shift-and-add multiplication. The current four-bit multiplier test records 512 NAND evaluations and depth 55. Its general construction performs `w^2` partial-product ANDs and `w` additions over `2w` bits, matching the documented `32 w^2` evaluation proxy.
- `SrNandLatch`, `GatedDLatch`, `BinaryRegister`, and `BinaryCounter` expose idealized state and forbidden/unsettled latch behavior. They do not model clocks, setup/hold, metastability, loading, or physical DFF cells.
- The existing Release assembly passed 89/89 tests with zero skips using `dotnet test ... --no-build --no-restore` during this audit.

The current CI workflow has only the .NET Build 000/001 verifier on `ubuntu-latest` and `windows-latest`. The main-branch run for `dfd2e7a` passed both jobs: [run 32802751347](https://github.com/Grativy6/Prime_Axiom_Software/actions/runs/32802751347). There is no HDL lint, simulation, synthesis, formal, netlist-schema, or cross-platform netlist comparison step yet.

## YoWASP probe

`yowasp-yosys` was not installed. `pip index versions yowasp-yosys` reported current stable package `0.68.0.0.post1208`. A disposable directory was populated by downloading and unpacking wheels, its `PYTHONPATH` and `YOWASP_CACHE_DIR` were scoped to the probe process, and the directory was deleted afterward.

### Exact Windows resolution

| Wheel | SHA-256 |
|---|---|
| `yowasp_yosys-0.68.0.0.post1208-py3-none-any.whl` | `0552936b1482415512e39915a1c0f3ed1135151293440e266064f3f75e90d4a2` |
| `yowasp_runtime-1.96-py3-none-any.whl` | `4ff456a4a6dff9d689c7feac9f68fb1492bed4cf873450d7b41259fa31645783` |
| `wasmtime-47.0.1-py3-none-win_amd64.whl` | `4e46a4a29092a2ac1001159d5673e2a23996f4474261c791401867b1630429f4` |
| `platformdirs-4.11.4-py3-none-any.whl` | `e34ff91a24bcddc6d939b878bdf3f5c437c9c46fe9e212b1bf455fdf1ee57586` |
| `click-8.4.2-py3-none-any.whl` | `e6f9f66136c816745b9d65817da91d61d957fb16e02e4dcd0552553c5a197b76` |
| `colorama-0.4.6-py2.py3-none-any.whl` | `4f1d9991f5acc0ca119f9d443620b77f9d6b33703e51011c16baf57afb285fc6` |

For Ubuntu x64, the corresponding Wasmtime 47.0.1 wheel is `wasmtime-47.0.1-py3-none-manylinux1_x86_64.whl`, SHA-256 `9724600b036c6e95c4fe952e29fad83b4f02bdc11d23f25c4ee3ffff2c1d7257`; the other wheels above are platform-independent. A future lock should use `--require-hashes` and include both platform-specific Wasmtime hashes.

The executable identified itself as:

```text
Yosys 0.68 (git sha1 38e001a6f, Release, Clang ... 22.1.0)
```

### Command results

| Capability | Result |
|---|---|
| `read_verilog -sv` | Pass |
| `synth -top ... -noabc` | Pass |
| `stat` | Pass; the smoke 4x4 multiplier ended at 71 cells: 40 `$_AND_`, 8 `$_OR_`, 23 `$_XOR_` |
| internal `sat` | Pass after the Yosys 0.68-required `chformal -lower`; a direct multiply versus shift/add miter proved with MiniSAT, 1,390 variables and 3,846 clauses |
| `abc -g NAND` | **Not viable**; log stopped at `Extracting gate netlist ... input.blif`, process exit was 0, and neither ABC results nor the subsequent `stat`/script trailer appeared |

The probe also exposed a version-sensitive formal detail: Yosys 0.68 initially represents assertions as `$check`; `sat -prove-asserts` rejects `$check`. `chformal -lower` must precede `sat` to produce supported `$assert` cells.

## Recommended pinned distribution

Use the complete [OSS CAD Suite 2026-08-24 release](https://github.com/YosysHQ/oss-cad-suite-build/releases/tag/2026-08-24), not `latest`. The next release was only partially populated when checked. Pinning the archive digest pins Yosys, ABC, Icarus, Verilator, SymbiYosys, Z3, Boolector/Bitwuzla, and nextpnr as one tested distribution.

| Platform | Asset | Bytes | SHA-256 |
|---|---|---:|---|
| Windows x64 | `oss-cad-suite-windows-x64-20260824.tgz` | 595,298,533 | `95d3cf2a59d1617f2363ee9370bb3577799f33a07e9c66e126ddeb68e8e5814c` |
| Linux x64 | `oss-cad-suite-linux-x64-20260824.tgz` | 741,360,658 | `9d7f79975ef624e1119fc9690fd9b9839b67026925aff3e2a1192d861b8dbb7c` |

The official setup action supports Windows x64 and Linux and accepts an exact release date. If used, pin both the action commit and suite date:

```yaml
- uses: YosysHQ/setup-oss-cad-suite@c7845bc0d335c8076aa22047e85972caa8a916df # v4.1.0
  with:
    version: '2026-08-24'
    github-token: ${{ secrets.GITHUB_TOKEN }}
```

The action pins the release selector but does not accept an expected archive digest. A stronger repository bootstrap downloads the platform asset itself, checks the SHA-256 above before extraction, and caches only by `(release, platform, digest)`. In either case, record the exact archive hash and the output of:

```text
yosys --version
iverilog -V
vvp -V
verilator --version
sby --version
yosys-smtbmc --version
z3 --version
boolector --version
bitwuzla --version
nextpnr-generic --version
```

Graphviz must remain optional: it is absent locally, and the OSS CAD Suite build rule excludes its Graphviz target on Windows. Generate mandatory architecture/result SVGs with repository code or another explicitly pinned cross-platform renderer.

## Smallest fair flow

### 1. HDL subset

Use synthesizable Verilog-2005 plus the small Yosys-supported SystemVerilog subset only where assertions require it. Avoid vendor primitives, implicit widths, signedness inference, `x`/`z` as arithmetic values, delays, latches, and simulator-only testbench behavior in design modules. Widths 4, 6, and 8 should be separate elaborated configurations with the width in every artifact ID.

### 2. Independent syntax/simulation gate

```powershell
verilator --lint-only --Wall --Wno-fatal --top-module <top> <ordered source files>
iverilog -g2012 -Wall -s <testbench> -o artifacts/build002/<case>.vvp <ordered source files>
vvp artifacts/build002/<case>.vvp
```

Use self-checking exhaustive testbenches for every legal input at 4 and 6 bits. Exhaust 8-bit binary pairs where practical; otherwise pair exhaustive operation-specific domains with seeded vectors. Persist a counterexample before changing RTL.

### 3. One canonical NAND synthesis script

Both lineages must run the same ordered source list and pass sequence, differing only in `top` and declared parameters. A minimal script is:

```yosys
read_verilog -sv <ordered source files>
hierarchy -check -top <top>
proc
flatten
opt
memory_map
techmap
opt
abc -g NAND
techmap -map hdl/common/not_to_nand.v
opt_clean
check -assert
write_json artifacts/build002/<case>.nand.json
stat
```

Yosys documents that `abc -g NAND` automatically permits `NOT`; therefore `not_to_nand.v` must replace every `$_NOT_` by one `$_NAND_` with tied inputs. A post-synthesis validator must fail unless every combinational cell is exactly `$_NAND_`; declared DFF cells may remain in sequential wrappers. Constants and direct connections are not gates, but their nets and fanout remain counted.

Produce two non-interchangeable views:

1. `STRUCTURAL_DECLARED`: explicit unoptimized NAND construction, closest to the Build 000 C# model;
2. `STRUCTURAL_OPTIMIZED`: the common Yosys/ABC flow above.

Do not let an optimizer-derived count rewrite the declared circuit count. If ABC changes architecture-specific logic differently, that is a useful optimized-netlist result, not proof about the hand construction.

### 4. Netlist analyzer

Parse each Yosys JSON netlist deterministically and emit:

```text
nand2_static
dff_static / state_bits
connections_static
wire_bits
port_bits
max_fanout
cross_lane_connections
unit_nand_critical_depth
combinational_loop_status
netlist_sha256
```

Depth starts at primary inputs and DFF Q outputs and ends at primary outputs and DFF D inputs. Reject combinational cycles. Preserve raw cell-type histograms so an unconverted primitive cannot disappear behind an aggregate.

### 5. Primary formal gate

For combinational properties and bounded representability, use Yosys's internal MiniSAT so no external solver is needed:

```powershell
yosys -Q -l artifacts/build002/<case>.formal.log -p "read_verilog -formal -sv <sources>; prep -top <formal_top> -flatten; chformal -lower; opt_clean; sat -verify -prove-asserts -set-def-inputs -show-inputs -show-outputs"
```

The miter should assert semantic equivalence, not bit-for-bit equality between unrelated encodings. Constrain the experimental input to a legal canonical state, reconstruct in proof-only logic, and assert equality with the ordinary operation plus identical overflow/status behavior. Proof-only reconstruction is not a free implementation adapter and must never enter synthesis cost.

Use `sby -f <case>.sby` with `smtbmc z3` as an independent second engine for sequential invariants, counters, overflow protocols, and state-machine traces. Record engine, depth/mode, solver version, property count, elapsed time, and exact design/configuration hashes. Do not call bounded model checking an unbounded proof.

### 6. Optional place and route

Do not make nextpnr a Build 002 prerequisite until a physical FPGA target is declared. A nextpnr iCE40 experiment maps both designs to the same LUT4/FF fabric, not NAND2. Disable DSP/carry hard blocks or enable them symmetrically, pin device/package/seed/constraints, and report it as `FPGA_LUT_PNR`, separate from `NAND2_LOGICAL`. `nextpnr-generic` is useful for plumbing but is not physical routing evidence.

## Fairness and evidence constraints

- Compare within the same tool binary, platform, top-level semantic contract, width, source mode, output obligation, optimization script, clock/latency budget, and resource analyzer version.
- Report cold binary ingress and warm structural state as different experiments. A pre-factorized port is not comparable to an unfactored binary input unless acquisition is charged.
- Include input/output registers and control/status state at the same boundary for both units, or keep both combinational and report state separately.
- Do not sum NAND count, DFF count, depth, fanout, transitions, and port/storage bits into one scalar.
- A unit-NAND critical path has no wire delay. A synthesis count is not silicon area, frequency, or power.
- Formal assertions describe the RTL model only. They do not cover analog metastability, clock-domain crossings, hazards, setup/hold, process variation, or malformed states not admitted by the formal harness.
- `ubuntu-latest` and `windows-latest` are moving runner images. Record `ImageOS`, `ImageVersion`, exact suite archive digest, and tool outputs. Treat Linux x64 as the canonical synthesis receipt if platform netlist hashes diverge; use Windows as a reproducibility check and investigate rather than averaging.

## Suggested Build 002 verification order

```text
verify Build 000/001 preservation
verify exact HDL-tool archive/hash and version receipt
lint all HDL
run exhaustive/self-checking simulation
run formal miters and state invariants
synthesize every baseline/experimental width with the same scripts
reject forbidden cell types and invalid/looped netlists
derive structural metrics only from hashed JSON netlists
run warm/cold workload traces with identical obligations
optionally run separately labelled FPGA place-and-route
regenerate summaries/figures from raw receipts
scan receipts for local paths and identities
```

## Bottom line

The host does not currently possess the requested hardware stack. A pinned OSS CAD Suite bundle is the smallest coherent way to add it without relying on unavailable WSL, Docker, or a patchwork of Windows packages. YoWASP 0.68 is a useful lightweight parser/formal fallback, but the audited wheel does not provide a trustworthy `abc -g NAND` completion path and must not be the sole synthesis receipt unless Build 002 deliberately uses a custom, ABC-free NAND techmap and records that limitation.

## Primary tool sources checked

- [OSS CAD Suite repository and platform/install documentation](https://github.com/YosysHQ/oss-cad-suite-build)
- [OSS CAD Suite 2026-08-24 release](https://github.com/YosysHQ/oss-cad-suite-build/releases/tag/2026-08-24)
- [Official setup action](https://github.com/YosysHQ/setup-oss-cad-suite)
- [YoWASP Yosys package](https://pypi.org/project/yowasp-yosys/)
- [Yosys ABC documentation](https://yosyshq.readthedocs.io/projects/yosys/en/latest/using_yosys/synthesis/abc.html)
