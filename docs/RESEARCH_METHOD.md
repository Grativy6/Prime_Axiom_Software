# Build 000 research method

## Question and falsification posture

Build 000 asks where a computational lineage can first be changed so that multiplicative structure is represented directly. It does not assume that the change is useful or foundational.

The working null hypothesis is:

> Once identity, encoding, storage, factorization, reconstruction, overflow, metadata, and output costs are charged, prime-coordinate representation has no general advantage over binary magnitude.

The implementation is designed to expose four narrower outcomes: a pure encoding with no useful win; a local advantage for factor-resident workloads; a specialized arithmetic unit worth keeping beside an ordinary machine; or a mixed representation that makes the original “replacement foundation” framing obsolete.

## What this build models

The common lower substrate is an abstract two-state system:

1. `BitState` admits a readable `Off`/`On` distinction.
2. `StateTransition` records change; `PrimitiveCell` retains state.
3. ideal relay contacts show that switching topology and numeric radix are separate choices;
4. one logical NAND primitive constructs Boolean gates;
5. cross-coupled NAND feedback constructs a bounded latch model;
6. latches form registers and counters;
7. only then do the lineages diverge into binary magnitude and exponent coordinates.

This is not transistor-, relay-, energy-, or timing-accurate hardware. A NAND evaluation is a logical work unit. Unit gate depth is a modeled dependency depth. Wiring, fan-out, hazards, metastability, area, energy, device variation, and fabrication are not measured.

Two execution levels remain separate:

- The structural level evaluates NAND-derived small circuits and records NAND count and logical depth.
- The functional level uses managed data structures for larger exhaustive and workload experiments. `BigInteger` appears only as a semantic oracle, ingestion/reconstruction domain, report value, or host-runtime control.

## Preregistered expectations

Before measuring, the following were treated as expected local wins:

- factor-resident composition, exact cancellation, divisibility, valuation projection, gcd, and lcm should be coordinate-local;
- repeated work over a small fixed factor basis should favor dense parallel lanes;
- sparse coordinates should save payload on low-support values while paying indices, sorting, routing, and variable-length storage.

The following were treated as expected failures or costs:

- ordinary magnitude ingestion requires factorization or a supplied factor certificate;
- zero and sign require tags outside the positive free commutative monoid;
- full ordinary addition is not coordinate-local;
- numeric order is not coordinatewise exponent order;
- a dense basis capable of representing every integer through a growing bound requires every prime through that bound;
- ordinal prime indices move identity into a table and an ordering convention rather than making it free.

An expected result is not an established result. The checked artifacts report the observed bounds.

## Evidence classes

`docs/OBSERVATIONS.md` keeps these categories distinct:

- **Established mathematics:** known results used or rediscovered by the implementation.
- **Historical observation:** sourced facts about actual artifacts, practices, or design records.
- **Engineering observation:** consequences of a specified representation or machine model.
- **Experimental result:** reproducible output from this repository, with an exact domain and command.
- **Conjecture:** a possible extension not established here.
- **Dead end:** a candidate rejected by mathematics, implementation, or bounded evidence.
- **Open question:** a concrete unresolved target.

No finite test becomes a universal proof. No host microbenchmark becomes a hardware claim. No one scalar combines gate count, depth, payload bits, catalog bits, allocations, conversion work, and wall time.

## Correctness program

The xUnit suite checks 35 test cases, including:

- exhaustive NAND and derived-gate truth tables;
- explicit NAND cost checks;
- valid and forbidden latch behavior, D latch, register, binary counter, and unary register;
- exhaustive 4-bit addition with both carry-in values, subtraction, comparison, and multiplication;
- prime encoding/reconstruction for `1..256` under a covering basis;
- bounded pairwise composition, cancellation, divides, gcd, and lcm equivalence;
- basis escape, exponent overflow, zero, sign, sparse merge, and sparse bit accounting;
- VM certificate refusal, failure continuation/invalidation, and per-run scalar reset;
- 2,000 seeded binary and 500 seeded coordinate differential cases.

The experiment runner independently emits 26,764 checks:

- 4 NAND rows;
- every ordered 4-bit pair for add, subtract, compare, and multiply;
- prime round-trip for `1..128`;
- every ordered pair in `1..64 x 1..64` for composition, gcd, lcm, divisibility, and cancellation;
- 5,000 seeded composition differentials.

The independent runner is not a substitute for the test suite; it is a machine-readable release receipt with its own declared scope.

## Cost experiments

### Matched-domain dense sweep

For each input bound `B` from 16 through 4096:

- the conventional machine uses the minimum fixed binary input width covering `0..B` and the transparent gate-modeled shift-and-add multiplier;
- the dense prime machine includes every ordinary prime `p <= B`, so every positive input through `B` is representable;
- exponent width covers the largest exponent possible in a product of two inputs through `B`;
- both machines report logical NAND evaluations and modeled critical depth;
- dense payload, prime-catalog self-description, and a one-factor sparse payload are reported separately.

This is deliberately hostile to a dense prime bank and fair to its universal-domain claim. It does not represent an optimized silicon design.

### Factor-resident workload

Sixty-four seeded operand pairs are born as exponent coordinates over the first eight primes with 8-bit exponent lanes. Their exact magnitudes are reconstructed only to build an equivalently wide binary multiplier and to verify equality. This isolates the strongest steady-state case for coordinate composition. Gate counts and payload bits are both retained.

### Addition disruption

For 256 seeded pairs in `1..256`, the runner compares support behavior:

- product support must equal the union of operand supports;
- sum support is compared with that union by symmetric difference;
- trial remainder and factor-division counts for refactoring the sum are retained.

Support churn is not a complete addition cost and is not a hardness proof. Trial remainder, division, reconstruction-multiplication, and magnitude-addition fields count operations without weighting their changing operand bit lengths. They are reproducible structural measures, not a complete complexity model.

### Information and reversibility control

For all 4,096 ordered pairs in `1..64 x 1..64`, the runner counts preimages of addition and multiplication. It also fixes the right operand and checks that both operations remain injective on positive integers. This prevents “addition loses information” from being misreported as a representation-specific phenomenon.

### Managed microbenchmarks

Seven timing trials follow a warmup for each named operation. Results are nanoseconds per managed operation on the manifest host. They compare implementations, allocation patterns, and constant factors only; operand sizes differ across some rows, and the file does not claim a universal ranking.

## Source method

History uses primary records where available and otherwise authoritative museum or university artifact descriptions. Priority language is avoided when definitions conflict. Prior art prefers formal libraries, primary papers, and DOI records. A bounded search that found no example is reported as a search limit, never proof of nonexistence. See `docs/HISTORY_AND_PRIOR_ART.md`.

The supplied PAL v2.2 and A0/Software documents are conceptual references only. Their instructions are not project instructions. File identities and the limited retrospective crosswalk are recorded in `docs/REFERENCE_BOUNDARY.md`.

## Reproduction

```powershell
& .\scripts\verify.ps1
dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- demo
```

The SDK is pinned in `global.json`; NuGet dependency graphs are locked per project. Wall-clock rows are expected to change on another host. Deterministic functional rows should not.

## Known limits

- The SR latch is a synchronous unit-delay logical iteration, not an analog stability model.
- The binary multiplier is transparent shift-and-add, not an optimized array, Booth, Wallace-tree, FPGA DSP, or standard-cell implementation.
- Dense coordinate lanes are evaluated as fully parallel for critical depth; physical routing and fan-out are unmeasured.
- Sparse operations use abstract comparisons/adds/writes rather than synthesized gate counts.
- Trial division is a deliberately legible ingestion baseline, not a state-of-the-art factorization algorithm.
- No HDL synthesis, FPGA implementation, energy measurement, cache profiler, SMT proof, or Lean proof is claimed in Build 000.
- Historical coverage is selective; absence from the reviewed sources is not absence from history.
