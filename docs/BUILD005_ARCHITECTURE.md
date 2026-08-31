# Build 005 architecture: a demand-driven valuation frontier

## Result being sought

Build 005 does not replace binary magnitude. It asks whether a tiny service can
retain just enough exact multiplicative evidence to avoid repeating later
search. The authoritative value remains an unsigned W-bit binary word.

```text
authoritative binary value
|-> p=2: count trailing zeros / shift
`-> selected odd p: one shared DIVMOD path
    `-> optional K-entry exact frontier cache, K <= 4
```

This is the deferred radix-aware design suggested by Build 002, not the dense
S4 sidecar that Build 002 already rejected under its registered whole-machine
comparison.

The controlling protocol is
[`PAH-BUILD005-DEMAND-VALUATION-0001`](../research/build005_experiment_plan.md).

## Where prime meaning enters

NAND and DFF state do not discover number-theoretic primality. The machine is
configured with the frozen ordered catalogue:

```text
2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31
```

The catalogue supplies validated labels and lane addresses. Its identity,
selection logic, tag bits, and validation are configuration costs. An
out-of-catalog label is rejected rather than accepted as a prime because a
caller named it one.

## Exact frontier

For nonzero magnitude `n` and selected prime `p`, one cache line carries:

```text
(valid, slot, generation, prime-index, L, residual R, terminal, infinite)
```

with invariant:

```text
n = p^L * R
```

`L` counts exact divisions already completed. If `terminal` is false, `L` is a
proved lower bound and a later request resumes with `R`. If `terminal` is true,
one failed divisibility probe has established `p` does not divide `R`, so `L`
is the exact valuation. A first failed probe therefore creates a useful exact
zero-exponent receipt; negative results are not silently discarded.

Zero uses `infinite`, not an ordinary exponent payload. One uses terminal
exponent zero and residual one. An unresolved residual is an exact cofactor; it
is never promoted to prime.

### Why a frontier rather than an answer cache?

Threshold questions need not discover the full exponent. To decide
`v_p(n) >= 2`, two successful divisions suffice. If a later request asks for
`v_p(n) >= 4`, it can continue from the stored residual instead of repeating
the first two divisions. An ordinary answer cache is a required control: a
repeated complete answer is generic memoization, not evidence for prime-native
computation.

## Identity, version, and invalidation

The service owns four logical value slots. Each mutation advances an eight-bit
generation. Lines are keyed by `(slot,generation,prime-index)`. A tag mismatch
is a miss, never a weak hint. Generation wrap causes a full flush before a
reused generation can be observed.

`LOAD`, arbitrary overwrite, and addition invalidate destination evidence.
Overflow and other rejected operations are atomic: neither a new magnitude nor
new certificates become visible. Equal magnitudes in different slots remain
different identities, while the separate content-addressed cache control may
reuse an immutable magnitude answer across them.

## The prime-specific propagation test

Checkpointing repeated division works for any divisor. Small constant-divisor
circuits also work for primes and composites alike. The candidate receives
prime-specific credit only for an exact propagation that the generic controls
cannot make.

For a prime `p`:

```text
n1 = p^L1 R1, p does not divide R1
n2 = p^L2 R2, p does not divide R2

n1*n2 = p^(L1+L2) (R1*R2)
and p does not divide R1*R2.
```

The output certificate is terminal without another divisibility search. The
last inference depends on primality. It fails for a general composite base:
six divides `2*3` despite dividing neither factor. Size-matched composite
controls keep constant-division and generic-cache savings from being credited
to this law.

## Policy controls

The experiment keeps five mechanisms separate:

| Policy | Retained state | Mutation behavior | Question isolated |
|---|---|---|---|
| `BIN_DIRECT_BEST` | none | recompute | competent direct baseline |
| `BIN_CONTENT_ANSWER_LRU_K` | completed immutable answer | content-keyed | generic memoization |
| `BIN_FRONTIER_NOPROP_K` | resumable `(L,R,terminal)` | invalidate | generic checkpointing |
| `BIN_PRIME_FRONTIER_PROP_K` | same frontier | exact legal transport | prime-certificate value |
| `BIN_PRIME_FRONTIER_SPEC_B_K` | candidate plus ordered scouting | work remains charged | whether blind search pays |

All policies use the same `ctz` and odd DIVMOD semantics. A better divider is
not allowed to masquerade as a better representation.

## Demand and speculation

Demand mode starts search only when a request names a prime. Speculation mode
spends a frozen budget of one or four odd DIVMOD steps after a value-producing
event, in catalogue order. Prefetched lines that are never queried, cache
evictions they cause, and transitions during otherwise idle time remain in the
ledger.

Speculation must beat demand-only after those costs to earn a scout result. If
demand wins and speculation loses, the architectural lesson is to preserve
requested evidence and stop blind search.

## Output contracts

The service can owe only a Boolean threshold, an exact exponent, an exponent
plus residual, a final ordinary magnitude, or an ordinary magnitude after
every arithmetic event. These contracts are not interchangeable. Exact
magnitude remains resident and authoritative in the primary architecture, but
all transfers and ordinary arithmetic events remain visible.

## Hardware boundary

The declared implementation uses the repository's stable-identity NAND graph
and explicit DFF metadata. Static evidence reports gates, state, ports, wires,
sinks, fanout, cross-region connections, and unit-NAND depth. Dynamic replay
reports settled transitions, not analog energy.

The model omits placement, route length, buffering, clock trees, hazards,
capacitance, setup/hold, metastability, process corners, and fabricated-device
behavior. Logical counts are not FPGA LUTs, standard-cell area, frequency, or
power.

## Honest possible outcomes

- Repeated hits only: generic cache advantage.
- Partial thresholds only: generic checkpoint advantage.
- `p=2` only: conventional radix advantage.
- Producer-supplied facts only: provenance preservation, not search repayment.
- Demand-driven odd-prime propagation repays: bounded prime-structural
  candidate with static tradeoff.
- Speculation also repays: bounded prime-scout candidate.
- No complete dynamic repayment: stop the hardware-search branch.

Static cache state never disappears at a break-even point. A positive result
can therefore be a measured dynamic repayment with a disclosed static trade,
not an automatic strict whole-vector Pareto win.
