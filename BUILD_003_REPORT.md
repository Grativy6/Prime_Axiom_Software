# Prime Axiom Software — Build 003 report

## Result

Build 003 delivered the requested calculator framework:

```text
integer in -> prime receipt out
```

The terminal framework status is:

> **`BOUNDED_TOOL_PATH_VALIDATED`**

The result is useful but narrower than “prime arithmetic makes AI good at math.” The tool exposes exact multiplicative structure, completeness, unresolved residue, provenance, and reconstruction in a form another program or model can inspect. Once complete operand receipts exist, multiplication produces its output receipt by a local exponent-map composition. General addition does not: it still performs ordinary magnitude addition and then discovers the output factors afresh.

The two user examples are:

```text
125891290390 + 12589127501265
= 12715018791655
= 5 * 23 * 31 * 103 * 34627429
```

and:

```text
218 * 489 * 175 * 17
= 317140950
= 2 * 3 * 5^2 * 7 * 17 * 109 * 163
```

Build 003 does **not** claim access to an LLM's private chain of thought. It compares an explicit, replayable ordinary arithmetic strategy with an explicit calculator-tool path. No actual model population, no-tool episode, conceptual understanding, or general LLM accuracy improvement was measured.

## Scope and frozen method

The controlling plan is [`research/build003_experiment_plan.md`](research/build003_experiment_plan.md), SHA-256 `8893D15E539F3750981F63AE6B6EE26FBC537D356AE7C5ACDA24F881ED766EE5`. It was frozen at commit `4ad436b` against merged Build 002 baseline `a83b660443df489c2ff218887953926a33a84c84`, before implementation and generated comparisons.

This was a user-directed software exposure probe. Build 002's recommended radix-aware valuation hardware service is deferred. Build 003 neither performs that physical experiment nor changes Build 002's bounded `NO_HARDWARE_ADVANTAGE` classification.

The exact software boundary is signed `BigInteger` ingestion and reporting. `ORDERED_TRIAL_DIVISION_V1` extracts powers of two, tests odd candidates in ascending order, removes full multiplicities, and certifies a terminal residual only after the tested frontier passes its square root. The default cap is 1,000,000 distinct odd candidates. Trial division was chosen because every event is easy to replay, not because it is a competitive general factorizer.

## What was built

- [`PrimeReceiptCalculator`](src/PrimeAxiom.Core/Calculator/PrimeReceiptCalculator.cs): complete and budget-partial factor receipts, exact reconstruction, deterministic semantic hashes, strict canonical integer parsing, receipt-integrity replay, and derived exact composition.
- [`PrimeArithmeticComparison`](src/PrimeAxiom.Core/Calculator/PrimeArithmeticComparison.cs): decimal-column addition, sequential magnitude multiplication, phase/class events, non-scalar work vectors, fresh-factor addition, and receipt-composed multiplication.
- CLI commands:
  - `prime-receipt INTEGER [--max-odd-candidates N] [--format text|json]`;
  - `compare-arithmetic add A B ...`;
  - `compare-arithmetic multiply A B [C ...] ...`;
  - `experiment-build003 --output DIRECTORY`.
- Six frozen arithmetic comparisons and calculator examples under [`results/build003`](results/build003/README.md).
- Bottom-up xUnit coverage, seeded differential checks, manifest verification, LF/BOM checks, and a deterministic two-replay verifier.

## Receipt contract

| Status | Meaning |
|---|---|
| `ExactZero` | Tagged zero; no finite factor list. |
| `ExactUnit` | `1` or `-1`; empty factor list and explicit sign. |
| `ExactFactorization` | Sorted certified primes, exact positive exponents, residual one, exact reconstruction. |
| `PartialBudget` | Sorted certified prime powers plus an exact unresolved residual; no residual primality claim. |

A receipt records its canonical input and SHA-256, sign, bit length, algorithm and budget, factor proof kinds, tested frontier, work vector, reconstruction result, origin, parent IDs, and a deterministic receipt ID. Calculator-issued receipts are opaque immutable objects: their constructor is internal, their properties are get-only, and factor/parent collections are copied into read-only collections. Derived provenance is restricted to calculator construction paths, including inherited factor proofs and cited parent IDs for exact multiplication composition.

`VerifyIntegrity` replays canonical encoding, provenance/status rules, reconstruction, and the semantic SHA-256. That is useful tamper evidence inside this process; it is **not** a signature, cryptographic authenticity, or an independently succinct primality certificate. JSON integer-valued fields that may exceed interoperable numeric limits are serialized as canonical decimal strings.

The partial state is important. With a zero odd-candidate budget, `1022117` returns `UNRESOLVED(1022117)` and reconstructs exactly. It is not silently promoted to prime, probable prime, or complete factorization.

## Evidence

The generated correctness campaign executed 52,914 checks with zero failures:

- complete receipt/reconstruction checks for every integer in `[-4096,4096]`;
- 5,000 deterministic signed products made from a fixed small-prime table;
- independent prime-label and reconstruction checks;
- eight explicit named cases: zero, positive/negative unit, negative/positive prime, repeated power, square, and semiprime;
- planned partial budgets 0, 1, and 3;
- canonical/invalid CLI grammar and the 4,096-digit boundary;
- all six frozen comparisons and their required path classifications.

The complete local test assembly passed 244/244 tests with zero skipped. The Build 003 generator replays deterministically and the committed files are manifest-addressed. These are bounded software correctness and receipt-integrity results, not formal unbounded arithmetic proofs.

## Comparison results

The vectors below are implementation event counts, not a scalar score, runtime, token count, human difficulty, or hardware work.

| ID | Exact result / output receipt | Ordinary visible work | Prime path acquisition/local work | Path conclusion |
|---|---|---|---|---|
| `USER_ADD_001` | `12715018791655`; `5 * 23 * 31 * 103 * 34627429` | 14 decimal columns, 28 digit additions, 5 carry events | 3 factor calls, 60,445 odd candidates, 60,456 remainders, 11 odd-factor divisions, 9 input-factor reads and 1 common-factor write, 1 magnitude add; reconstructions 6 = 3 construction + 2 integrity replay + 1 egress | fresh output discovery |
| `USER_MUL_001` | `317140950`; `2 * 3 * 5^2 * 7 * 17 * 109 * 163` | 3 sequential magnitude multiplications | 4 factor calls, 12 odd candidates, 15 remainders, 3 odd-factor divisions, 7 exponent reads, 7 writes; reconstructions 10 = 5 construction + 4 integrity replay + 1 egress | local receipt composition after ingress |
| `ADD_REORGANIZE_001` | `10000000000`; `2^10 * 5^10` | 10 columns, 10 carries | 3 factor calls, 50,002 odd candidates, 3 input-factor reads and 0 common-factor writes, 1 magnitude add; reconstructions 6 = 3 construction + 2 integrity replay + 1 egress | fresh output discovery |
| `ADD_RADIX_BOUNDARY_001` | `4294967297`; `641 * 6700417` | 10 columns, 0 carries | 3 factor calls, 1,293 odd candidates, 32 radix extractions, 1 input-factor read and 0 common-factor writes, 1 magnitude add; reconstructions 5 = 2 construction + 2 integrity replay + 1 egress | fresh output discovery |
| `MUL_COLD_PRIMES_001` | `90404408570887`; `89 * 97 * 99991 * 104729` | 3 sequential magnitude multiplications | 4 factor calls, 326 odd candidates, 4 exponent reads/writes; reconstructions 10 = 5 construction + 4 integrity replay + 1 egress | local receipt composition after ingress |
| `MUL_FACTOR_RICH_001` | `4415217206400`; `2^7 * 3^4 * 5^2 * 7^2 * 11^2 * 13^2 * 17` | 2 sequential magnitude multiplications | 3 factor calls, 11 odd candidates, 13 exponent reads, 7 writes, 6 exponent merges; reconstructions 8 = 4 construction + 3 integrity replay + 1 egress | local receipt composition after ingress |

The user multiplication happens to have disjoint prime identities across its four operands, so its map composition performs no duplicate-key exponent additions. `MUL_FACTOR_RICH_001` supplies the actual overlap control and records six merges.

### User addition in detail

The inputs expose:

```text
125891290390
= 2 * 5 * 12589129039

12589127501265
= 3^2 * 5 * 7 * 61 * 83 * 7893637
```

The shared factor `5` survives, but the residual output reorganizes into `23 * 31 * 103 * 34627429`. The receipt path reads all nine input factor entries and writes the one derived common factor. Those counters cover only the common-factor projection; they do not claim that addition or fresh output factorization is exponent-local. The path still must add magnitudes and factor the result. It performs far more explicit discovery work than the 14-column ordinary path, so this is a clean negative control.

### User multiplication in detail

The ordinary visible left fold is:

```text
218 * 489 = 106602
106602 * 175 = 18655350
18655350 * 17 = 317140950
```

The prime path acquires:

```text
218 = 2 * 109
489 = 3 * 163
175 = 5^2 * 7
17 = 17
```

It then constructs the output receipt from the four parent receipt IDs without factoring `317140950`. This is the genuinely different pathway. Because the output contract also asks for a decimal integer, one egress reconstruction remains charged in addition to construction and integrity-replay reconstructions. For a one-shot magnitude-only question, ordinary multiplication still avoids all four input factorizations.

A zero-tagged operand follows a separate exact multiplication branch: after input receipt acquisition, zero determines the product without reading or merging any exponent entries. The output receipt, integrity replay, and requested magnitude egress remain explicit.

The public addition comparator currently accepts same-sign inputs only. Mixed-sign addition is rejected rather than passing through a mislabeled column-add trace; an auditable borrow/subtraction trace remains future work.

## Direct answers

### 1. What exact API and CLI did the build expose?

An exact signed-integer analysis API, a receipt-composition API, public arithmetic comparison records, human-readable CLI views, canonical JSON, and a deterministic experiment generator. Commands and examples are documented in [`docs/PRIME_RECEIPT_CALCULATOR.md`](docs/PRIME_RECEIPT_CALCULATOR.md).

### 2. What do complete, partial, zero, and unit receipts mean?

They are distinct tagged states. Complete means every factor and multiplicity is certified and the residual is one. Partial means the known prime powers are exact but a nontrivial residual remains unresolved. Zero has no finite prime list. Units have an empty list plus sign.

### 3. Which facts are certified and which remain unresolved?

Reported factor keys are primes under the ordered trial-division proof path; exponents are exact because each hit is divided to exhaustion; reconstruction is exact. A partial residual has only exact-value identity and the tested-frontier fact. It has no primality or compositeness label.

### 4. What work did factor acquisition perform?

It performed radix extractions, odd candidate tests, remainder checks, and successful factor divisions. Every receipt reports those separately. The implementation does not convert them into time, tokens, NANDs, or one universal cost.

### 5. How did the two paths differ on the frozen expressions?

The ordinary path manipulated decimal/magnitude values directly. The prime path paid cold factor discovery for each operand. Multiplication then derived its output structure locally; addition used magnitude arithmetic and acquired a new output receipt. Both paths returned identical exact magnitudes on all six rows.

### 6. What became locally structural in multiplication?

Prime identity matching and exponent addition. The output receipt inherits its prime proofs from its parent receipts and cites their IDs. No factor search runs on the product.

### 7. Why did addition still require magnitude arithmetic and fresh discovery?

Prime exponent coordinates do not determine general sums. They can certify common factors and unequal-valuation consequences, but the residual sum can acquire unrelated prime structure. Build 003 therefore refuses an exponent-merge fiction and refactors the exact magnitude result.

### 8. Where did the prime path add cost?

Every cold input required receipt acquisition, and every nonzero non-unit input required factor discovery; addition required an additional output factorization; complete discovered receipts performed construction-time reconstruction; comparison integrity replay reconstructed the input receipts; and the requested ordinary output required egress reconstruction. The comparison ledger exposes those three reconstruction categories instead of collapsing them into the old conceptual “one reconstruction” shorthand. JSON/provenance also adds data. Nothing is free because it is written structurally.

### 9. What can an AI system safely use?

It can inspect certified factors, exponents, sign/zero/unit state, completion status, exact residual, reconstruction, origin, and parent receipt IDs. It can use a calculator-issued composed receipt instead of guessing a product's factor structure. It should treat a successful integrity replay as internal consistency and tamper evidence, not as proof of who issued the data.

### 10. What cannot be inferred about model reasoning or understanding?

The build cannot establish what a model represents internally, whether it understands integers, whether chain-of-thought is faithful, or whether tool access improves a model distribution. No model experiment was run. The ordinary trace is an algorithmic comparator, not an LLM transcript.

### 11. What failed or remained a dead end?

- Integer-in/receipt-out is conventional factor discovery, not prime magic.
- A one-shot magnitude-only multiplication does not benefit computationally from first factoring every operand in this implementation.
- Addition did not become prime-local; its receipt path did much more explicit work on the user example.
- The user multiplication contains no overlapping primes, so it does not alone demonstrate exponent accumulation; a separate factor-rich row was required.
- Adversarial review found that mutable/constructible receipt records could have allowed forged or post-construction state into composition. Receipts were made opaque and immutable, collections read-only, integrity replay strengthened, and derived provenance restricted.
- Adversarial review found that conceptual reconstruction counts omitted construction-time and integrity-replay work. The ledger now reports construction, integrity replay, and egress separately.
- Mixed-sign addition could not honestly use the implemented carry-only decimal trace. It is now rejected pending an explicit borrow/subtraction trace.
- The initial completion gate did not independently require the exact six frozen IDs/families/counts, and verifier output could be aimed too broadly. The generator/verifier now enforce the exact registered set and conclusions; verifier-owned output is confined beneath `artifacts/` or `.artifacts/`, rejects reparse traversal/overlap, and rechecks inherited evidence recursively.
- An initial verifier run completed its tests and generators, then falsely rejected identical file inventories because of PowerShell operator grouping. The comparison expression was repaired; the failed run is not called a verification pass.
- No hidden model reasoning was requested or manufactured to make the comparison look more cognitive than it is.

### 12. What should the next build attack?

The strongest next step is a black-box **calculator-tool usefulness experiment**, not a more elaborate factor library and not immediate hardware expansion.

Use fixed final-answer tasks under three conditions:

1. no arithmetic tool;
2. an ordinary exact calculator;
3. the prime-receipt calculator.

Separate multiplicative/divisibility tasks from addition controls, hold model/version/settings and prompts fixed, record only visible outputs and tool calls, and measure exact answer accuracy, invalid receipt use, tool-call count, tokens, and latency without requesting hidden chain of thought. Include persistent expression graphs so receipt reuse—not one-shot refactoring—can be measured. Only if real traces show repeated structural reuse should the deferred demand-driven valuation service or hardware path reopen.

## Reproduction

```powershell
& .\scripts\verify-build003.ps1

dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- `
  prime-receipt 360

dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- `
  compare-arithmetic multiply 218 489 175 17
```

Evidence map:

- frozen protocol: [`research/build003_experiment_plan.md`](research/build003_experiment_plan.md)
- calculator contract: [`docs/PRIME_RECEIPT_CALCULATOR.md`](docs/PRIME_RECEIPT_CALCULATOR.md)
- generated comparisons: [`results/build003/arithmetic_comparisons.json`](results/build003/arithmetic_comparisons.json)
- correctness: [`results/build003/correctness.json`](results/build003/correctness.json)
- coverage: [`results/build003/protocol_coverage.json`](results/build003/protocol_coverage.json)
- manifest: [`results/build003/manifest.json`](results/build003/manifest.json)
- classified observations: [`docs/OBSERVATIONS.md`](docs/OBSERVATIONS.md)

The verifier requires the exact 52,914-check receipt, exact zero-failure status, exact six comparison IDs, exact protocol-family set, and the two frozen arithmetic conclusions. A generator invocation can produce deterministic bytes but cannot establish replay by itself: replay evidence is earned only by the external verifier, which generates two isolated copies and compares both with committed results. Its writable output is ownership-scoped to `artifacts/` or `.artifacts/`; source, research, documentation, scripts, tests, and generated-result trees are rejected as output targets, and inherited evidence is snapshotted and rechecked after the verification receipt is written.

## Final interpretation

The “nature's calculator” metaphor survives if it is kept precise:

> Prime factorization is a cut that exposes one exact algebraic view of an integer. Once that view is present and retained, multiplication becomes structural composition. The cut itself costs factor discovery, addition usually crosses out of the view, and ordinary output costs reconstruction.

That is enough to make a useful AI-facing receipt tool. It is not enough to claim that primes replace integers, that arithmetic became free, or that a model understands what the receipt says.
