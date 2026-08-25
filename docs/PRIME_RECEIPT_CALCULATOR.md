# Prime Receipt Calculator

Build 003 exposes one deliberately small contract:

```text
exact signed integer in
-> certified prime powers + exact residual + work receipt out
```

The controlling protocol is [`PAS-BUILD003-PRIME-RECEIPT-0001`](../research/build003_experiment_plan.md). This is a software semantic/certificate layer above ordinary exact integers. It does not claim a new factoring algorithm, a hardware result, or access to an AI model's private reasoning.

## Quick use

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- prime-receipt 360
```

The text view is:

```text
input: 360
status: ExactFactorization
prime structure: 2^3 * 3^2 * 5
unresolved cofactor: 1
reconstruction verified: True
...
```

Machine-readable output uses canonical decimal strings for unbounded values:

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- `
  prime-receipt 360 --format json
```

The public API is [`PrimeReceiptCalculator`](../src/PrimeAxiom.Core/Calculator/PrimeReceiptCalculator.cs):

```csharp
var receipt = PrimeReceiptCalculator.Analyze(new BigInteger(360));
var recovered = PrimeReceiptCalculator.Reconstruct(receipt);
```

## Receipt meanings

| Status | Exact claim |
|---|---|
| `ExactZero` | The input is zero. Zero is tagged and has no finite prime-factor list. |
| `ExactUnit` | The input is `1` or `-1`. Its factor list is empty and sign remains explicit. |
| `ExactFactorization` | Every listed factor is prime, every exponent is exact, the residual is one, and reconstruction equals the input. |
| `PartialBudget` | Every listed prime power is certified and exact, but the exact residual may be prime or composite. No primality claim is made for it. |

For every nonzero receipt:

```text
abs(input) = unresolved_cofactor * product(prime ^ exponent)
```

The receipt separately records sign, input SHA-256, factor-discovery algorithm, policy, factor proof kind, deterministic work counters, a reconstruction check, parent receipt IDs when derived, and its claim ceiling. A deterministic receipt ID hashes the semantic payload.

Calculator-issued receipts are intentionally opaque and immutable. `PrimeReceipt` has no public constructor or setters; factor powers and parent IDs are copied into read-only collections. Exact composition accepts integrity-valid exact receipts and constructs derived provenance itself: the output cites the parent receipt IDs and labels factor proofs `InheritedFromParentReceipts`. Callers cannot legitimately relabel a magnitude-discovered receipt as a derived one.

`PrimeReceiptCalculator.VerifyIntegrity(receipt)` replays canonical parsing, status/provenance/proof rules, reconstruction, and the deterministic semantic hash. This detects accidental or post-construction alteration. SHA-256 here is tamper evidence, not a secret-key signature, cryptographic authenticity, or a succinct independent primality certificate.

All unbounded or potentially interoperability-sensitive JSON integers—including inputs, factors, residuals, tested frontiers, and results—are canonical decimal strings. Consumers should not coerce them through a limited-precision JSON number type.

### Planned partial results

The budget limits new odd candidates, not the size of the input. With no odd candidates allowed:

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- `
  prime-receipt 1022117 --max-odd-candidates 0
```

returns a valid `PartialBudget` receipt containing `UNRESOLVED(1022117)`. The tool can still reproduce the integer exactly. It does not guess whether the cofactor is prime.

The generated verification campaign exercises candidate budgets `0`, `1`, and `3` on `2 * 3 * 1009 * 1013`; all three must remain `PartialBudget`, reconstruct the same exact integer, and leave a nontrivial unlabeled residual. It also names zero, positive unit, negative unit, negative prime, repeated power, square, semiprime, and positive prime cases explicitly rather than relying on their incidental appearance in the exhaustive domain.

## Acquisition algorithm and bounds

`ORDERED_TRIAL_DIVISION_V1` is intentionally transparent:

1. handle zero, units, and sign;
2. extract powers of two by the binary radix path;
3. test every odd integer candidate in order, composites included;
4. remove a successful candidate's full multiplicity;
5. call the terminal residual prime only after the candidate frontier passes its square root;
6. otherwise stop with an unresolved exact residual.

The default limit is 1,000,000 distinct odd candidates per input receipt. The CLI rejects noncanonical decimal spellings and inputs longer than 4,096 decimal digits before parsing. These are resource guards, not a promise that every accepted integer will be completely factored.

The work vector remains heterogeneous:

- radix extractions;
- odd candidates examined;
- remainder checks, including repeated multiplicity checks;
- successful odd-factor divisions.

No total score, time estimate, or hardware cost is inferred from those counts. Trial division was selected for replayability and evidence clarity, not competitiveness.

## Arithmetic comparison

The companion command compares two visible answer-producing paths:

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- `
  compare-arithmetic add 125891290390 12589127501265

dotnet run --project src/PrimeAxiom.Cli --configuration Release -- `
  compare-arithmetic multiply 218 489 175 17
```

`EXPLICIT_DECIMAL_BASELINE_TRACE` is implemented as decimal column addition or sequential exact magnitude multiplication. It is a public replayable strategy, not a claim about hidden chain of thought.

`PRIME_RECEIPT_TOOL_PATH` behaves differently by operation:

```text
addition:
  factor inputs
  -> ordinary magnitude addition
  -> factor output afresh

multiplication:
  factor inputs
  -> merge prime identities / add exponents
  -> one egress reconstruction for the requested magnitude
```

Every event retains its phase and one of the inherited operation classes:

- `REPRESENTATION_LOCAL`;
- `BINARY_MAGNITUDE_LOCAL`;
- `CROSS_REPRESENTATION`;
- `REQUIRES_FACTOR_DISCOVERY`.

Cold receipt acquisition is never compared as if the operands arrived already factored. A composed output receipt cites its parents and performs zero output factor discovery; its required magnitude reconstruction is still explicit.

The work vector splits reconstruction into three auditable categories:

- `receiptConstructionReconstructions`: reconstruction performed while issuing eligible complete or partial receipts;
- `integrityReplayReconstructions`: reconstruction used to replay input-receipt integrity before a comparison result is accepted;
- `egressReconstructions`: reconstruction required because the command asks for an ordinary decimal result.

`reconstructions` is their checked sum. For the six frozen rows, the totals are respectively `6 = 3+2+1`, `10 = 5+4+1`, `6 = 3+2+1`, `5 = 2+2+1`, `10 = 5+4+1`, and `8 = 4+3+1` in protocol order. These are implementation events, not timings or a universal cost score.

For addition, `exponentReads` counts every input factor entry inspected by the certified-common-factor projection and `exponentWrites` counts only the common factors it derives. They do not count the freshly discovered output factors and do not imply that addition became representation-local. `USER_ADD_001` records nine reads and one write; the small `5 + 10` regression control records three reads and one write.

Two edge contracts are deliberate:

- The visible decimal addition comparator currently accepts same-sign operands only. Mixed-sign input is rejected until an explicit borrow/subtraction trace exists; it is not silently represented by a carry-only trace.
- If any multiplication operand has an exact zero tag, the structural phase short-circuits without reading or merging exponent entries. Input acquisition, output receipt construction, integrity replay, and requested magnitude egress remain charged.

## AI-facing use

The JSON receipt is suitable as a calculator-tool response because it gives a model exact objects to inspect:

- factor identities and multiplicities;
- completeness versus unresolved structure;
- sign, zero, and unit distinctions;
- exact reconstruction;
- provenance for composed receipts;
- a machine-readable claim ceiling.

This can replace plausible guessing for a tool call. Build 003 did not run a model study, so it does not establish that models reason better, use the tool correctly, understand integers, or outperform another calculator. A proper follow-up would compare final answers under fixed no-tool, ordinary-calculator, and prime-receipt-tool conditions without requesting hidden reasoning.

## Reproduction

```powershell
& .\scripts\verify-build003.ps1
```

The verifier protects inherited Build 000/001/002 evidence, restores locked dependencies, checks formatting, builds Release, requires a zero-skip test pass, generates Build 003 twice, checks byte-identical replay, validates every manifest hash, and compares the replay with [`results/build003`](../results/build003/README.md).

Completion is exact rather than inferred from a green generator status: the verifier requires 52,914 checks with zero failures, the exact six frozen comparison IDs, the exact required protocol-family set, and the frozen multiplication/addition/LLM claim values. A single generator run records that deterministic replay is not yet established; only the external two-replay verifier earns that evidence.

Verifier-owned output must be below repository `artifacts/` or `.artifacts/`. The guard rejects source, test, research, documentation, script, HDL, and generated-result targets; rejects reparse-point traversal and output/test overlap; and compares a recursive inventory plus SHA-256 snapshot of inherited evidence after all writes.
