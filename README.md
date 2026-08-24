# Prime Axiom Software

Build 000 asks where a conventional computational lineage can be forked without pretending that notation is hardware. It constructs one abstract two-state/NAND substrate, builds conventional binary arithmetic above it, and compares bounded prime-coordinate machines on the same accounting model.

The current evidence supports a narrow result: prime coordinates are useful as a specialized, binary-hosted representation for multiplicative workloads, not as a replacement for state, switching, memory, addressing, or general arithmetic. Multiplication-like composition, divisibility, gcd, and lcm become local; ingestion, addition, order comparison, reconstruction, zero/sign handling, and unbounded prime support carry the displaced cost.

See `BUILD_000_REPORT.md` for the claim-by-claim result and Build 001 recommendation. See `docs/OBSERVATIONS.md` for the status-separated research ledger.

![Matched-domain logical NAND work](results/build000/figures/fair_domain_gate_counts.svg)

## Evidence map

- [Build 000 report](BUILD_000_REPORT.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Representation contracts](docs/REPRESENTATION_CONTRACTS.md)
- [Experiment register](docs/EXPERIMENTS.md)
- [Classified observations](docs/OBSERVATIONS.md)
- [History and prior art](docs/HISTORY_AND_PRIOR_ART.md)
- [Research method and limits](docs/RESEARCH_METHOD.md)
- [Supplied-reference boundary](docs/REFERENCE_BOUNDARY.md)
- [Raw Build 000 receipts](results/build000/manifest.json)

## Quick start

Requires the .NET 8 SDK pinned by `global.json`.

```powershell
& .\scripts\verify.ps1
dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- demo
```

The checked-in experiment outputs are deterministic evidence from the environment recorded in `results/build000/manifest.json`; rerunning wall-clock benchmarks on another machine is expected to produce different timings.
