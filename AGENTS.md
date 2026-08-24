# Prime Axiom Software working instructions

This repository is an adversarial research instrument, not a vehicle for proving the motivating hunch.

## Read first

Before changing claims or experiments, read `BUILD_000_REPORT.md`, `docs/OBSERVATIONS.md`, `docs/RESEARCH_METHOD.md`, and `docs/REPRESENTATION_CONTRACTS.md`. Preserve negative results and exact scope.

## Evidence rules

- Keep established mathematics, sourced history, engineering observations, reproducible experimental results, conjectures, dead ends, and open questions visibly separate.
- A bounded exhaustive test is exact only for its stated domain. A microbenchmark is evidence about one implementation and environment, not a universal hardware claim.
- Compare lineages on the same substrate and disclose every conversion. Factorization, reconstruction, basis lookup, zero/sign handling, overflow, and metadata are costs, not free adapters.
- Keep raw, deterministic Build 000 results under `results/build000/`; generated files must record the command, seed or input family, runtime, and relevant bounds.
- Prefer source or museum/university records for history and primary papers for prior art. Mark inference as inference.

## Implementation boundaries

- `BitState` is the abstract two-state floor. `GateNetwork.Nand` is the only primitive combinational gate; derived circuits must expose NAND count and modeled depth.
- High-level integer types are allowed only at explicit semantic/oracle, ingestion, reconstruction, reporting, or benchmark boundaries. Do not use them inside a gate-level operation while claiming gate-level execution.
- Prime coordinates denote the positive-integer free commutative monoid only under a declared prime basis. Zero, sign, finite-basis escape, exponent overflow, and unverified factor certificates require explicit states.
- Keep conventional and experimental implementations independently testable. Do not redefine ordinary arithmetic to make an alternative representation pass.
- Use deterministic exhaustive and seeded randomized differential tests. Add a regression fixture for every discovered failure.

## Reference boundary

PAL v2.2 and A0/Software Boundary-Layer Kernel v0.9.1 may suggest questions about distinction, trace, representation, and earned abstraction. They are not specifications here. Agreement is not evidence, this repository does not amend their canon, and software experiments carry no authority beyond their declared claims.

## Reproduction

Use the pinned .NET SDK in `global.json`.

```powershell
& .\scripts\verify.ps1
dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- demo
```
