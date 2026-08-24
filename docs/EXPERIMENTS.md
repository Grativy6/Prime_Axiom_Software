# Build 000 experiment register

## E000 — state and switch floor

- Exhaust state transitions and ideal normally open/closed contacts.
- Verify series and parallel contact networks as AND/OR controls.
- Claim: abstract state and switching can precede numeric interpretation.

## E001 — NAND basis

- Exhaust NAND, NOT, AND, OR, XOR, XNOR, and mux behavior.
- Check half-adder and full-adder outputs and logical NAND counts.
- Claim: both lineages share one explicit combinational basis.

## E002 — memory and counting

- Exercise valid SR latch hold/set/reset, forbidden input, gated D storage, register writes, and binary counter overflow.
- Exercise unary occupancy increment/decrement and capacity.
- Claim: quantity is an interpretation of retained state, and at least two encodings exist before prime coordinates enter.

## E003 — transparent binary arithmetic

- Exhaust all 4-bit ordered pairs for addition, subtraction, comparison, and multiplication.
- Run seeded wider differential cases.
- Record NAND work and modeled depth.

## E004 — coordinate correctness

- Round-trip positive magnitudes under a covering basis.
- Differentially check compose, cancel, divides, gcd, and lcm.
- Reject zero in the positive domain; test explicit tags, basis escape, exponent overflow, mismatched basis/width, and unverified VM certificates.

## E005 — matched universal domain

- Sweep maximum input 16 through 4096.
- Give the binary machine enough bits for the range.
- Give the dense coordinate machine every prime lane through the range and enough exponent bits for products.
- Compare logical work, depth, resident payload, catalogue bits, and a minimal sparse payload.
- Raw artifact: `results/build000/fair_domain_costs.csv`.

## E006 — factor-resident domain

- Generate 64 deterministic values directly as exponent lanes over eight primes.
- Compare local compose work with a binary multiplier sized to the exact reconstructed inputs.
- Preserve both payload and logical-work dimensions.
- Raw artifact: `results/build000/resident_multiplication_workloads.csv`.

## E007 — addition disruption

- Generate 256 deterministic input pairs in `1..256`.
- Compare product support and sum support to the operand-support union.
- Record factorization work for the sum.
- Raw artifact: `results/build000/addition_disruption.csv`.

## E008 — representation pathologies

- Compare unit, prime powers, smooth values, primorials, and out-of-basis primes.
- Keep basis residual and failure type.
- Raw artifact: `results/build000/representation_cases.csv`.

## E009 — preimages and reversibility control

- Enumerate all ordered pairs in `1..64 x 1..64`.
- Count output preimages for addition and multiplication.
- Verify fixed-positive-right-operand injectivity.
- Raw artifact: `results/build000/operation_preimages.csv`.

## E010 — managed implementation timing

- Warm up and take seven trials per operation.
- Record median/min/max nanoseconds per managed call.
- Do not compare across operand shapes as a hardware ranking.
- Raw artifact: `results/build000/microbenchmarks.csv`.

## Release receipt

`results/build000/manifest.json` records the runtime, OS, architecture, CPU identity, logical processor count, build configuration, exact random-stream seeds/distributions, Core and CLI assembly SHA-256 values, canonical command, actual invocation arguments, files, and claim ceiling. `results/build000/correctness.json` owns the bounded pass domain; `results/build000/test-summary.json` records sanitized Release test counters and the test-assembly hash. The raw TRX is kept only under ignored `.artifacts/` because it contains local account and machine identifiers.
