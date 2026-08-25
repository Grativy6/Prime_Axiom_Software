# Build 002 generated evidence

Protocol: `PAH-BUILD002-CONF0001`

Classification: `NO_HARDWARE_ADVANTAGE`

This directory preserves cold/warm source regimes, output obligations, phase costs, support restrictions, and evidence classes as separate rows. A numeric cost of `-1` always means `NOT_MEASURED`; it never means zero cost and is excluded from comparison.

## Reproduce

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build002 --output results/build002 --hdl-verification-summary .artifacts/build002-hdl-full-zero-repair/verification-summary.json --hdl-synthesis-metrics .artifacts/build002-hdl-full-zero-repair/synthesis-metrics.csv --hdl-toolchain .artifacts/build002-hdl-full-zero-repair/toolchain-bootstrap.json
```

Master seed: `0x5041485742303032`

Runtime: `.NET 8.0.29`

## Evidence boundary

- `correctness.json` records bounded executed checks and failures.
- `static_costs.csv` contains declared NAND graphs only; it does not infer silicon area or frequency.
- `dynamic_operations.csv` contains deterministic settled-vector sequences. Initial all-off transitions remain separate.
- `workload_matrix.csv` keeps `INGRESS`, `EXECUTE`, `ADDITION_RECOVERY`, and `EGRESS` phases separate.
- `ingress_egress.csv` charges implemented acquisition/reconstruction circuits explicitly.
- `representation_search.csv` reports exact state-bit geometry as analytic evidence, not a synthesized circuit claim.
- `addition_adversary.csv` and `hostile_support.csv` preserve semantic/support facts independently from integrated workload costs.
- `synthesis_metrics.csv`, `formal_receipts.json`, and `toolchain.json` are validated, path-sanitized imports from the pinned common HDL flow.

Correctness checks: 656810

Correctness failures: 0

HDL evidence: `COMPLETE_VERIFIED` (260 checks; 15 formal; 150 synthesis rows)

## Decision boundary

The terminal negative is not a claim that valuation operations lack local advantages. The warm structural unit uses fewer NANDs, less depth, and fewer transitions for known-factor composition/cancellation, but it uses more state/port bits; the exact sidecar is larger than the matched binary datapath at W6 and W8; and cold adapters dominate the operation savings. Under the frozen Pareto rule none of those tradeoffs is a whole-machine hardware advantage. No universal scalar score or post-hoc weighted ranking is emitted.
