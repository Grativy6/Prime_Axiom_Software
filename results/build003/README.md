# Build 003 generated evidence

> **Framework status: `BOUNDED_TOOL_PATH_VALIDATED`**

This is deterministic software-path evidence under `PAS-BUILD003-PRIME-RECEIPT-0001`. It is not a hardware or LLM-cognition benchmark.

## User examples

- `125891290390 + 12589127501265 = 12715018791655`
  - output receipt: `5 * 23 * 31 * 103 * 34627429`
  - path: `MAGNITUDE_ADD_THEN_FRESH_FACTOR_DISCOVERY`
- `218 * 489 * 175 * 17 = 317140950`
  - output receipt: `2 * 3 * 5^2 * 7 * 17 * 109 * 163`
  - path: `LOCAL_EXPONENT_MERGE_AFTER_EXPLICIT_ACQUISITION`

## Coverage

- correctness checks: 52914
- failures: 0
- frozen arithmetic rows: 6
- wall-clock and hardware metrics: `NOT_MEASURED`
- general LLM improvement: `NOT_MEASURED`

Regenerate with:

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build003 --output results/build003
```

Bounded functional and deterministic-path evidence only; no hardware advantage, factoring-performance, private reasoning, model understanding, or general LLM accuracy claim.
