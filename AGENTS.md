# Prime Axiom Software working instructions

This repository is an adversarial research instrument, not a vehicle for proving the motivating hunch.

## Read first

Before changing claims or experiments, read `BUILD_001_REPORT.md`, `docs/EXPERIMENTS_BUILD001.md`, `docs/COST_MODEL.md`, `docs/PRIOR_ART_BUILD001.md`, `docs/OBSERVATIONS.md`, and `docs/REPRESENTATION_CONTRACTS.md`. Read `BUILD_000_REPORT.md` and `docs/RESEARCH_METHOD.md` for the earned lower-layer result. Preserve negative results and exact scope.

## Evidence rules

- Keep established mathematics, sourced history, engineering observations, reproducible experimental results, conjectures, dead ends, and open questions visibly separate.
- A bounded exhaustive test is exact only for its stated domain. A microbenchmark is evidence about one implementation and environment, not a universal hardware claim.
- Compare lineages on the same substrate and disclose every conversion. Factorization, reconstruction, basis lookup, zero/sign handling, overflow, and metadata are costs, not free adapters.
- Keep deterministic evidence under its build directory and preserve its declared raw/aggregate status. Build 000 is pinned to commit `7792b8b2a83c95693a6db48a0ed4b153bb0808f4`; do not rewrite its report or `results/build000/` receipts from Build 001 work.
- Build 001 evidence is a completed pilot subset, not the frozen full confirmation matrix. Preserve `PILOT_SUBSET_COMPLETE_FULL_CONFIRMATION_NOT_RUN` and `PARTIAL — PILOT_NEGATIVE; FINAL DECISION NOT EARNED`. Do not assign any frozen terminal label until the registered full-matrix stop condition is satisfied.
- Generated files must record the command, seed or input family, runtime, relevant bounds, and hashes. Never hand-edit generated Build 001 receipts.
- Prefer source or museum/university records for history and primary papers for prior art. Mark inference as inference.

## Implementation boundaries

- `BitState` is the abstract two-state floor. `GateNetwork.Nand` is the only primitive combinational gate; derived circuits must expose NAND count and modeled depth.
- High-level integer types are allowed only at explicit semantic/oracle, ingestion, reconstruction, reporting, or benchmark boundaries. Do not use them inside a gate-level operation while claiming gate-level execution.
- Prime coordinates denote the positive-integer free commutative monoid only under a declared prime basis. Zero, sign, finite-basis escape, exponent overflow, and unverified factor certificates require explicit states.
- A Build 001 nonzero hybrid value denotes `sign * cofactor * product(bank[i]^exponent[i])`. The cofactor is exact ordinary magnitude. `KnownExact` lanes certify the cofactor is free of that bank prime; `CertifiedLowerBound` lanes permit additional copies in the cofactor while keeping the represented integer exact.
- `Canonical` and `Partial` describe valuation knowledge, not numeric exactness. Never read a lower bound as an exact exponent or silently replace unknown structure by zero. Refresh, normalize, migration, and reconstruction are charged operations.
- Keep bank configuration/validation, ingress, native work, maintenance, and egress separate. The heterogeneous cost-vector fields are not a universal unit and must not be summed with post hoc weights.
- Bank migration is an exact global maintenance operation over affected values, not metadata relabeling. A prime hidden in a magnitude is not adaptively discovered without a charged computation.
- Keep conventional and experimental implementations independently testable. Do not redefine ordinary arithmetic to make an alternative representation pass.
- Use deterministic exhaustive and seeded randomized differential tests. Add a regression fixture for every discovered failure.
- Failed VM producers invalidate their destination atomically and preserve distinct source registers; if a destination aliases a source, destination invalidation wins. Failed scalar queries clear old scalar output. Never allow prior successful state to survive as the result of a failed instruction.

## Reference boundary

PAL v2.2 and A0/Software Boundary-Layer Kernel v0.9.1 may suggest questions about distinction, trace, representation, and earned abstraction. They are not specifications here. Agreement is not evidence, this repository does not amend their canon, and software experiments carry no authority beyond their declared claims.

## Reproduction

Use the pinned .NET SDK in `global.json`.

```powershell
& .\scripts\verify-build001.ps1
dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- demo
```

Use `& .\scripts\verify.ps1` only for the immutable Build 000 verification path. Build 001's script includes Build 000 preservation checks, regenerates Build 001 evidence, and verifies manifest hashes.
