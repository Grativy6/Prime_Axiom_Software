# Build 003 frozen software exposure protocol

Protocol identifier: `PAS-BUILD003-PRIME-RECEIPT-0001`  
Frozen before Build 003 implementation and comparative execution: 2026-08-25  
Repository baseline: `a83b660443df489c2ff218887953926a33a84c84`  
Inherited Build 002 terminal result: bounded `NO_HARDWARE_ADVANTAGE`

This protocol controls a deliberately small, user-directed software experiment:

> Can an exact tool turn an integer into auditable prime-structure receipts, and does that tool path make multiplicative arithmetic easier to verify without pretending that addition, factor discovery, or model reasoning became free?

Build 002 recommended a radix-aware, demand-driven valuation hardware service as a possible next hardware experiment. The user has instead requested this software exposure probe. The hardware recommendation is deferred, not executed, contradicted, or relabeled by Build 003.

## 1. Claim boundary

Build 003 is an exact-arithmetic tool and a pathway comparison. It is not:

- a new integer foundation;
- a claim that prime factorization is novel;
- a claim that factoring is cheap in general;
- a hardware, latency, area, energy, or complexity advantage result;
- an evaluation of an LLM population;
- access to, or evidence about, a model's private chain of thought;
- evidence that a model understands integers, primes, or abstract concepts.

The ordinary comparison path is named `EXPLICIT_DECIMAL_BASELINE_TRACE`. It is a public deterministic algorithmic trace that a person or model could inspect. It is not labeled as hidden model reasoning. The prime path is named `PRIME_RECEIPT_TOOL_PATH`. Both paths must return the same exact integer before any interpretation is written.

## 2. Numeric scope and resource policy

The library API accepts a signed `BigInteger` at an explicit semantic/ingress boundary. This does not claim gate-level execution. The CLI accepts canonical base-10 signed integers and rejects malformed input or more than 4,096 decimal digits before parsing.

The first implementation uses deterministic ordered trial division:

1. preserve sign and handle zero and units explicitly;
2. extract the radix prime `2` by repeated exact shifts/divisions;
3. test odd candidates in increasing order beginning at `3`;
4. when a candidate divides, remove its complete multiplicity before advancing;
5. declare a remaining cofactor prime only when the tested frontier establishes `candidate^2 > remaining`;
6. stop before testing a new odd candidate when `maxOddCandidates` is exhausted.

The default policy is:

```text
algorithm = ORDERED_TRIAL_DIVISION_V1
maxOddCandidates = 1,000,000
cliMaxDecimalDigits = 4,096
```

The budget counts distinct odd candidates examined. Every remainder check, exact factor division, and radix extraction is still reported separately. A caller may lower the candidate budget to obtain a bounded partial receipt. A caller may increase it knowingly; no unbounded completion promise follows.

## 3. Prime receipt contract

Every accepted input returns one of these statuses:

| Status | Meaning |
|---|---|
| `ExactZero` | The integer is zero. Zero has no finite prime factorization. |
| `ExactUnit` | The integer is `1` or `-1`. The prime-power list is empty. |
| `ExactFactorization` | Every reported multiplicity is exact and no unresolved cofactor remains. |
| `PartialBudget` | Reported prime powers are certified and exact, but an exact unresolved cofactor remains because the candidate budget ended. |

Malformed CLI input and input-resource-limit failures are command errors, not numeric receipts.

Each receipt records at least:

```text
schema
receiptId
algorithm
policy
canonicalInputDecimal
inputSha256
sign
absoluteBitLength
status
primePowers[]:
  primeDecimal
  exponent
  proofKind
unresolvedCofactorDecimal
reconstructionVerified
magnitudeIsPrime (true / false / unknown)
integerIsPrime (true / false / unknown)
work:
  radixExtractions
  oddCandidatesExamined
  remainderChecks
  exactFactorDivisions
claimCeiling
```

Prime powers are strictly increasing by prime. Their exponents are positive. In `PartialBudget`, the unresolved cofactor may be prime or composite and must be labeled `UNRESOLVED`, never probable-prime or prime. The exact invariant is:

```text
abs(input) = unresolvedCofactor * product(prime ^ exponent)
```

with special tagged rules for zero and units. `reconstructionVerified` checks this invariant using ordinary exact magnitude arithmetic and is not free factoring evidence.

The receipt ID is a deterministic SHA-256 over the canonical receipt payload excluding the ID itself. JSON integers that may exceed interoperable numeric limits are emitted as canonical decimal strings.

## 4. Arithmetic paths

### 4.1 Addition

For `a + b`, the prime path must:

1. acquire receipts for `a` and `b`;
2. perform exact ordinary magnitude addition;
3. acquire a fresh receipt for the output.

It may expose a certified common factor from the input receipts, but it may not derive the remaining output factorization by exponent merge. The end-to-end path is classified `REQUIRES_FACTOR_DISCOVERY`; its ordinary addition step is `BINARY_MAGNITUDE_LOCAL` and its receipt acquisitions are explicit boundary work.

### 4.2 Multiplication

For `a_1 * ... * a_k`, the prime path must:

1. acquire one receipt per input;
2. require exact input factorizations for the exact structural comparison;
3. merge identical prime identities by exponent addition;
4. combine signs and zero tags;
5. reconstruct once only because these experiments require an ordinary magnitude output;
6. verify that reconstruction against an independently computed exact product.

The exponent merge is `REPRESENTATION_LOCAL`; cold receipt acquisition and required magnitude reconstruction remain `CROSS_REPRESENTATION`. The output receipt is derived from source receipt IDs and exponent merge, not by silently refactoring the product.

If any input receipt is partial, the calculator may still preserve an exact opaque product of residual cofactors, but that row is not eligible for the exact prime-merge comparison and must not call its output fully factored.

## 5. Non-scalar cost vectors

No heterogeneous fields are added into a universal score. The public comparison records the relevant vector:

```text
baseline:
  decimalColumns
  digitAdditions
  carryEvents
  sequentialMagnitudeMultiplications

prime path:
  factorizationCalls
  radixExtractions
  oddCandidatesExamined
  remainderChecks
  exactFactorDivisions
  exponentReads
  exponentWrites
  exponentMerges
  magnitudeAdditions
  reconstructions
```

Counts describe these implementations and exact expressions. They are not tokens, latency, cognitive effort, asymptotic complexity, or hardware work.

## 6. Frozen comparison problems

The following expressions are fixed before implementation results are generated:

| ID | Expression | Intended pressure |
|---|---|---|
| `USER_ADD_001` | `125891290390 + 12589127501265` | User-supplied addition; prime support may reorganize. |
| `USER_MUL_001` | `218 * 489 * 175 * 17` | User-supplied factor-rich product. |
| `ADD_REORGANIZE_001` | `9999999967 + 33` | Cold prime proof plus radically different output support. |
| `ADD_RADIX_BOUNDARY_001` | `4294967296 + 1` | A power of the binary radix crosses to odd factors. |
| `MUL_COLD_PRIMES_001` | `104729 * 99991 * 97 * 89` | Local merge after relatively expensive cold prime certification. |
| `MUL_FACTOR_RICH_001` | `360360 * 720720 * 17` | Repeated prime identities make exponent aggregation visible. |

Every row uses the default candidate budget, requires an exact ordinary magnitude answer, and retains complete operand/output receipts. No expression may be dropped because it is unfavorable.

## 7. Verification domains

The master seed is `0x5041534230303033`. Random generation uses the repository's deterministic seeded facilities and persists the exact count.

Required checks are:

1. exhaustive receipt/reconstruction equivalence for every integer in `[-4,096, 4,096]`;
2. explicit zero, `+1`, `-1`, negative prime, repeated-power, square, semiprime, and prime cases;
3. at least 5,000 seeded products constructed from a fixed small-prime table, differentially checked against exact ordinary arithmetic;
4. budget values `0`, `1`, and small finite limits that force `PartialBudget`, with an exact unresolved cofactor and no false prime claim;
5. malformed CLI input and the 4,096-digit CLI boundary;
6. exact equality of both arithmetic paths on all six frozen expressions;
7. exact multiplication output factors equal the componentwise merge of exact input factors;
8. every addition row records one magnitude addition and fresh output acquisition rather than a false local exponent rule;
9. deterministic generation replay and manifest verification;
10. zero skipped xUnit tests in the Build 003 verifier.

An independent test/oracle routine checks reported small prime labels and reconstruction. This is bounded differential assurance, not an unbounded formal proof.

## 8. Generated artifacts

`results/build003/` is generated, UTF-8 without BOM, and LF normalized. It contains at least:

```text
README.md
calculator_examples.json
arithmetic_comparisons.json
correctness.json
protocol_coverage.json
manifest.json
```

The self-excluding manifest records the protocol ID, frozen-plan SHA-256, baseline commit, seed, default policy, canonical reproduction command, runtime, claim ceiling, and SHA-256 of every other generated artifact. It does not include wall-clock measurements.

Generated receipts are never hand-edited. A failure discovered after a green run receives a regression test and a new generated receipt; it is not repaired only in prose.

## 9. Completion and interpretation labels

Build 003 is complete only if all required checks and all six rows execute, both deterministic replays match, inherited Build 000/001/002 reports and result directories remain unchanged from the baseline, and the manifest verifies.

The generated framework status is one of:

- `BOUNDED_TOOL_PATH_VALIDATED`: every required finite check passed;
- `PARTIAL`: implementation exists but required coverage or deterministic evidence is incomplete;
- `FAILED`: a required exactness, reconstruction, receipt-integrity, or path-classification check failed.

The comparison conclusion is reported as a pair, not a winner:

```text
MULTIPLICATION: LOCAL_EXPONENT_MERGE_AFTER_EXPLICIT_ACQUISITION
ADDITION: MAGNITUDE_ADD_THEN_FRESH_FACTOR_DISCOVERY
```

This pair is established representation behavior demonstrated by the implementation. It is not a speedup claim. The stronger AI-facing conclusion is capped at:

> The tool supplies exact, machine-checkable mathematical structure that can replace plausible numeric guessing for these calls. No claim about unaided model accuracy, private reasoning, conceptual understanding, or general usefulness is earned.

## 10. Reporting obligations

`BUILD_003_REPORT.md` must answer:

1. What exact API and CLI did the build expose?
2. What does a complete, partial, zero, and unit receipt mean?
3. Which facts are certified and which remain unresolved?
4. What work did factor acquisition perform?
5. How did the explicit decimal baseline and prime-receipt path differ on every frozen expression?
6. What became locally structural in multiplication?
7. Why did addition still require magnitude arithmetic and fresh factor discovery?
8. Where did the prime path add cost rather than remove it?
9. What can an AI system safely use from the receipt?
10. What cannot be inferred about model reasoning or understanding?
11. What failed or remained a dead end?
12. Does this justify the deferred demand-driven valuation-service experiment, a calculator-tool integration experiment, or stopping?

Any PAL/A0 resemblance is retrospective reference context only. It neither specifies this protocol nor supplies evidence for a result.
