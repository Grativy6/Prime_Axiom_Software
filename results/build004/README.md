# Build 004 generated evidence

> **Generated-evidence status: `PARTIAL — FINAL DECISION NOT EARNED`**
> Campaign candidate after external verification: `BOUNDED_EXACT_LINEAGE_TOOLKIT_VALIDATED`

Build 004 tests a dual receipt: a replaceable exact active-source projection plus a persistent typed derivation DAG. It also tests exact combinatorial probability, exact-overlap fusion, calibration/unit crossings, just-intonation PCM readout, a transparent non-cryptographic accumulator, and a deliberately small BOM receipt.

## Frozen coverage

- `FACTORIAL_0_512`: 513/513 cases; 1026/1026 checks; 0 failures; `BOUNDED_PASS`
- `BINOMIAL_ALL_0_256`: 33153/33153 cases; 66306/66306 checks; 0 failures; `BOUNDED_PASS`
- `HYPERGEOMETRIC_POINTS_0_24`: 20475/20475 cases; 40950/40950 checks; 0 failures; `BOUNDED_PASS`
- `HYPERGEOMETRIC_NORMALIZATION_0_32`: 12529/12529 cases; 25058/25058 checks; 0 failures; `BOUNDED_PASS`
- `SEEDED_POINTS_N_LE_4096`: 10000/10000 cases; 10000/10000 checks; 0 failures; `BOUNDED_PASS`
- `ADJACENT_STREAMS_0_48_PLUS_N2000`: 40426/40426 cases; 271229/271229 checks; 0 failures; `BOUNDED_PASS`
- `NAMED_COMBINATORIAL_CONTROLS`: 8/8 cases; 8/8 checks; 0 failures; `BOUNDED_PASS`
- `SUPPORT_PROJECTION_U8`: 65536/65536 cases; 393216/393216 checks; 0 failures; `BOUNDED_PASS`
- `MULTIPLICITY_PROJECTION_U4_E0_2`: 6561/6561 cases; 78732/78732 checks; 0 failures; `BOUNDED_PASS`
- `DERIVATION_DAG_AND_MUTATIONS`: 12/12 cases; 12/12 checks; 0 failures; `BOUNDED_PASS`
- `EXACT_FUSION_CYCLES`: 2/2 cases; 2/2 checks; 0 failures; `BOUNDED_PASS`
- `SEEDED_ASYNC_FUSION_SCHEDULES`: 512/512 cases; 512/512 checks; 0 failures; `BOUNDED_PASS`
- `FUSION_FAILURE_AND_RETRACTION_BOUNDARIES`: 9/9 cases; 9/9 checks; 0 failures; `BOUNDED_PASS`
- `CALIBRATION_AUDIO_ACCUMULATOR_BOM_PROBES`: 12/12 cases; 12/12 checks; 0 failures; `BOUNDED_PASS`

Total assertions: 887072; failures: 0.

## Bounded conclusions

- A hypergeometric point is constructed and retained as an exact signed prime-exponent ratio, then reconstructed once for exact magnitude/output; no completed result is factored. A tail or event sum crosses to exact rational addition.
- A binary PEV, explicit set, and squarefree prime product answer the same unique-support questions under one valid registry.
- Support and multiplicity projections cannot distinguish `a*b+c*d` from `a*c+b*d`; the retained DAG can.
- Exact overlap identity is insufficient for exact fusion when the shared likelihood payload cannot be replayed.
- Ratio-scale units compose structurally; affine, logarithmic, nonlinear, rounded, correlated, or expired cases require explicit transforms or remain unresolved.
- The included membership token is `NOT_CRYPTOGRAPHIC` and `NO_PRIVACY`; public membership leakage is a tested property.
- Hardware, wall-clock performance, allocation, and cross-ledger ranking are `NOT_MEASURED`.

The WAV artifact is a finite PCM approximation of a declared exact 3/2 interval above 220 Hz. The explicit numerical/render policy is recorded; deterministic byte replay is established only by the external verifier on its recorded host/runtime. It is not a perceptual or physical-audio validation.

Regenerate with:

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build004 --output results/build004
```

Bounded exact-software and abstract-structure evidence under PAS-BUILD004-EXACT-LINEAGE-0001 only; no source-authenticity, empirical-validity, privacy, cryptographic-security, PAL-conformance, universal-performance, or hardware-PPA claim.
