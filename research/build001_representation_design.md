# Build 001 adversarial representation and VM design

Status: design recommendation, not an implementation result
Scope: the smallest rigorous bounded valuation bank plus exact cofactor architecture that can test the Build 001 question without weakening Build 000's contracts

## Executive decision

Implement one strict semantic core first:

```text
HybridInteger<B, w> =
    Zero
  | Nonzero(sign, exponent[0..k-1], cofactor)
```

where `B = [p0, ..., p(k-1)]` is an immutable, explicitly configured bank of distinct ordinary primes, `w` is the fixed lane width, and a nonzero value denotes

```text
sign * cofactor * product(pi ^ exponent[i]).
```

The mandatory canonical invariant is:

```text
cofactor >= 1
gcd(cofactor, product(B)) = 1
0 <= exponent[i] <= 2^w - 1
```

`Zero` has no sign, exponents, or cofactor. A nonzero sign is exactly `-1` or `+1`. Under a fixed bank and width, this form is exact and unique for every representable integer. The cofactor is an ordinary exact unsigned binary magnitude and may be `1`, prime, composite, or only partly understood. It must never be described as a prime atom.

Fixed exponent width means this first core does **not** represent an integer whose valuation at a bank prime exceeds the lane maximum. Such ingress or operation fails atomically with `ExponentOverflow`; it does not put the excess back into a supposedly coprime cofactor. Widening or bank migration is an explicit operation. This bounded failure is preferable in Build 001 to a hidden noncanonical overflow convention.

Test addition laziness as a second, separately typed experiment:

```text
DeferredHybridInteger<B, w> =
    Zero
  | Nonzero(sign, lowerExponent[], exactResidual, exactLaneMask)
```

It denotes the same product expression, but each exponent is only a proved lower bound unless its mask bit is `Exact`. For an exact lane, the residual is certified nondivisible by that lane's prime. For a lower-bound lane, no such claim is made. This is useful because addition can preserve a common proved factor while deferring extraction only on genuinely uncertain lanes.

Do not add a `STALE` state. A cached exponent that may no longer be true is not a number representation; it is an unsafe cache entry. Do not store `INVALID` as if it were a number either. Invalid and uninitialized are register/result states. Within a value, the only justified lane states are:

- `Exact(e)`: `v_p(abs(x)) = e`;
- `AtLeast(e)`: `v_p(abs(x)) >= e`.

This two-form design is the minimum that permits an honest eager baseline and a falsifiable lazy-addition experiment. If deferred state does not amortize its mask, branch, refresh, and restricted-operation costs, delete it and retain only the canonical form.

## 1. Bank contract

### 1.1 `ValuationBank`

An immutable bank specification contains:

```text
representation version
strictly increasing prime labels B
fixed exponent width w
derived lane maximum Emax = 2^w - 1
optional physical layout metadata, outside numeric equality
```

Requirements:

1. `B` is nonempty for the first implementation. A no-bank control is a separate ordinary-binary baseline, not a special malformed bank.
2. Each label is an ordinary prime accepted by the configured primality-validation procedure.
3. Labels are unique and strictly increasing. Workload-selected banks remain allowed; selection does not change canonical ascending lane order.
4. `w > 0` and is bounded by an implementation limit before any allocation or shift.
5. Bank equality compares the full semantic specification, not a process-local object identity and not only a short hash.
6. Prime validation and catalogue storage are configuration costs. They are amortizable but never zero by definition.
7. Hardware routing order may differ from ascending semantic order, but that permutation belongs to a machine layout and must not alter serialized numeric meaning.

A stable content digest may be emitted in receipts for convenient identification. It is only a checksum for the fully retained specification, not a security certificate or authority claim.

### 1.2 Fixed, configurable, and adaptive banks

The first executable type should use one fixed bank per machine instance or benchmark run. Multiple fixed configurations are enough to compare first-4, first-8, first-16, workload-selected, and hostile banks without letting adaptation obscure costs.

Bank adaptation belongs in an explicit controller experiment, not in the integer type:

```text
MIGRATE_BANK(value, oldBank, newBank)
```

Migration has unavoidable work:

- retained primes can copy exact exponents;
- removed primes are reconstructed into the cofactor;
- newly added primes are extracted from the cofactor by remainder/division;
- exponent overflow, catalogue validation, lane movement, and allocation are explicit;
- deferred input must either be refreshed where needed or migrated conservatively with lower-bound state.

An in-place mutation of the bank is forbidden because it silently changes every lane's meaning. An adaptive policy must therefore charge selection, migration, eviction, and changed metadata separately. Cache hit rate alone is not a result.

## 2. Canonical integer contract

### 2.1 Semantic cases

`CanonicalHybridInteger` is an immutable discriminated union.

`Zero`:

- denotes exactly ordinary integer zero;
- has no negative-zero variant;
- has no valuation vector, because finite `v_p(0)` is not defined;
- has no cofactor;
- serializes in one unique zero form.

`Nonzero`:

- has sign in `{-1, +1}`;
- has exactly one exponent per bank lane;
- has an exponent representable in `w` bits at every lane;
- has a positive exact cofactor;
- has a cofactor nondivisible by every bank prime.

Special values:

- multiplicative identity is `(+1, all-zero exponents, cofactor 1)`;
- negative one is `(-1, all-zero exponents, cofactor 1)`;
- zero is never encoded as all-zero lanes plus cofactor zero;
- cofactor one is an ordinary canonical case, not absence or unknown residue.

### 2.2 Exactness and uniqueness

For a nonzero value `x`, the representation reconstructs exactly as:

```text
x = sign * c * product(pi ^ ei).
```

Because the bank labels are distinct primes and `c` is coprime to all of them, `ei = v_pi(abs(x))`. Unique factorization then gives one tuple for `x` under the declared bank and width. This uniqueness is bank-relative: the same integer under a different bank has a different structural representation.

The public type must have no unchecked constructor. It should expose only:

- validated binary ingress;
- validated structured ingress;
- internal proof-by-construction operation results;
- a checked decoder.

If performance requires an internal unchecked constructor, it must be inaccessible outside the assembly/module that maintains the invariant and covered by invariant tests after every producer.

### 2.3 Equality and hashing

Two distinct equality questions must have distinct APIs:

```text
RepresentationEquals(a, b)
NumericEquals(a, b)
```

`RepresentationEquals` is constant-structure equality and requires identical bank specifications. For canonical values under the same bank, field equality is numeric equality.

Across banks, field comparison is meaningless. `NumericEquals` must migrate, reconstruct, or run another exact cross-bank comparison and must charge that crossing. A default object hash may be used only with same-bank structural equality. A deferred value must not use its fields as a numeric hash key because multiple deferred decompositions can denote the same integer.

### 2.4 Overflow policy

The strict core uses checked, atomic overflow:

```text
ExponentOverflow(lane, requiredLowerBound, maximum)
```

On overflow:

- no truncated or wrapped exponent is returned;
- no tracked-prime power is silently moved into the canonical cofactor;
- source values remain unchanged;
- a destination register becomes invalid/uninitialized according to the VM rule;
- the receipt retains the lane and work completed before detection;
- a caller may explicitly retry with a wider bank layout or ordinary magnitude.

Two plausible alternatives should be modeled later, not mixed into the core:

1. unbounded `BigInteger` exponents, useful as a semantic oracle but not a bounded-bank machine;
2. saturated lanes whose excess prime power spills into the residual. That can be exact and canonical only under a different invariant, such as `ei = min(v_pi(x), Emax)`, and it turns a saturated lane into `AtLeast(Emax)`. It requires different divisibility, refresh, serialization, and cost rules. Silent spill without this state is rejected.

## 3. Cofactor contract

The canonical cofactor is:

- an exact positive ordinary binary integer;
- part of the numeric value, never discarded residue;
- permitted to be composite or a large prime;
- not required to be factored;
- guaranteed coprime to the bank;
- manipulated by ordinary binary arithmetic whose cost is reported separately from lane work.

The first implementation should not add a sparse factorization or symbolic DAG inside the cofactor. Those are comparison representations, not free annotations. Optional facts about the cofactor belong in an evidence sidecar and must name their scope, for example:

```text
CoprimeToBank       // mandatory for canonical form
KnownFactor(q, e)   // partial, optional evidence
FullyFactored(...)  // expensive, optional evidence
OpaqueExact         // exact magnitude with no further factor claim
```

`OpaqueExact` means “no additional factor claim,” not “atomic” and not “prime.” GCD, divisibility, and equality must continue to use the exact cofactor rather than treating its storage identity as arithmetic identity.

## 4. Ingress and verification

### 4.1 Ordinary binary ingress

For signed magnitude `x`:

1. if `x = 0`, emit `Zero`;
2. record the sign and set `r = abs(x)`;
3. for each bank prime `p_i`, repeatedly divide `r` while `r mod p_i = 0`, counting remainders and exact divisions;
4. fail atomically if a lane exceeds `Emax`;
5. store the final `r` as cofactor.

This is bounded valuation extraction, not full factorization. The final residual need not be tested for primality or factored. At least one unsuccessful remainder test per lane is normally required to establish canonical coprimality. The receipt must distinguish input bit length, remainder tests, exact divisions, lane writes, branches, and allocation. If a faster valuation primitive is later used, identify it and retain a comparable cost model.

### 4.2 Structured ingress

There are two different operations that must not share an ambiguous “certified” name.

`LOAD_COMPONENTS(sign, exponents, cofactor)` defines a value by its components. It validates:

- tag/sign consistency;
- lane count and exponent bounds;
- positive, minimally encoded cofactor;
- `cofactor mod p_i != 0` for every bank lane.

It does not prove correspondence to some unstated external magnitude. Once validated, the tuple itself exactly defines a number.

`LOAD_CLAIMED_MAGNITUDE(x, components, evidence)` claims that externally supplied components describe a particular `x`. The machine must verify both canonicality and equivalence. Acceptable verification procedures include:

- divide `abs(x)` by each `p_i` exactly `e_i` times, check the next remainder, then compare the quotient with `cofactor`; or
- reconstruct the tuple exactly and compare it with `x`.

Both have explicit cost. A caller-supplied Boolean such as `CertificateVerified = true` is not evidence and must not cross the public VM boundary.

Internally, an immutable value produced by a verified operation may be accepted by type construction without repeating all checks. That is proof by construction within a narrow module boundary, not trust in a serialized flag.

### 4.3 Provenance

Arithmetic semantics must not depend on provenance. Provenance belongs in receipts or an optional audit envelope, with fields such as:

```text
value id
bank specification id
producer operation and parent value ids
lane evidence class or masks
validation procedure
cost counters
software/result version
```

Suggested evidence classes are `ExtractedFromMagnitude`, `ValidatedComponents`, `DerivedByComposition`, `DerivedByCancellation`, `CommonMinimum`, `Refreshed`, and `LowerBoundOnly`. These labels report how a claim was obtained; they do not make the claim true. Numeric serialization should omit them so two equal canonical values remain byte-identical. A separate evidence envelope may include them and may be invalidated or regenerated without changing the number.

## 5. Serialization and malformed-state rejection

Use a versioned deterministic binary format, not default `BigInteger` bytes or unconstrained object serialization. A portable envelope contains the complete bank specification followed by one value frame. A machine-local register frame may instead require an externally supplied exact bank specification.

One workable canonical frame is:

```text
magic | format-version | bank-spec | value-tag | value-payload
```

Bank specification:

```text
prime-count | exponent-width | ascending prime labels
```

Nonzero value payload:

```text
fixed-width packed exponents | cofactor-byte-length | unsigned-big-endian cofactor
```

Rules:

1. Integer lengths and labels use one specified minimal varint encoding; nonminimal forms are rejected.
2. Exponents have exactly the declared width; unused high padding bits must be zero.
3. Cofactor uses minimal unsigned big-endian bytes, has no sign-extension byte, no leading zero, and represents at least one.
4. Zero has exactly the zero tag and no value payload. Negative zero and zero with residual bytes are rejected.
5. Unknown tags, versions, flags, trailing bytes, count mismatches, duplicate/unsorted/composite labels, out-of-range exponents, zero/negative cofactors, and canonical cofactors divisible by a bank prime are rejected.
6. Declared counts and lengths are capped before allocation to prevent malformed input from causing unbounded memory use.
7. Decode returns a typed failure with the byte offset or field. It never returns a partially initialized value.
8. Decode-encode is byte stable, and encode-decode preserves representation equality.

The deferred form needs a distinct tag/version and one `exactLaneMask` of exactly `k` bits. It uses `exactResidual` rather than calling that field a canonical cofactor. On decode, each lane marked exact must satisfy `residual mod p_i != 0`; lower-bound lanes make no nondivisibility claim. Unknown mask padding bits and redundant decompositions may be valid deferred states, so deferred serialization is structurally deterministic but not a unique numeric encoding.

## 6. Operation taxonomy

No operation should be called simply “native” without saying which resource it uses.

### 6.1 Bank-local operations

These touch tags, lane state, and fixed bank configuration but not the cofactor magnitude except for zero cases:

- `VALUATION(p_i)`: for nonzero canonical input, return `Exact(e_i)`; for zero return an explicit `ZeroValuation`/`PositiveInfinity` result, never an ordinary finite exponent;
- `DIVISIBLE_BY_LANE(p_i)`: for nonzero canonical input, test `e_i > 0`; define zero behavior explicitly;
- `FACTOR_CONTAINS(lane, amount)`: lane comparison;
- `SCALE_LANE(lane, amount)`: checked exponent addition and sign preservation;
- lane-side portions of composition, cancellation, gcd, and lcm.

Even these operations pay lane reads, comparisons/additions, status reduction, metadata access, and any modeled NAND work.

### 6.2 Hybrid-native operations

These preserve canonical form without reconstructing the entire magnitude or extracting valuations, but they do use ordinary binary arithmetic on the exact cofactor:

`COMPOSE(a, b)`:

```text
sign = sign(a) * sign(b)
exponent[i] = checked_add(a.exponent[i], b.exponent[i])
cofactor = a.cofactor * b.cofactor
```

Zero absorbs. Cofactor multiplication must be charged; Build 000's exponent-only `COMPOSE` is not the full hybrid cost.

`TRY_EXACT_DIVIDE(dividend, divisor)`:

- division by zero fails;
- zero divided by nonzero returns zero;
- every dividend exponent must cover the divisor exponent;
- divisor cofactor must divide dividend cofactor exactly;
- the result subtracts lanes, divides cofactors, and combines signs.

Because canonical cofactors are coprime to the bank, there is no hidden cross-cancellation between a bank lane and a cofactor. A failure should distinguish lane underflow from cofactor remainder.

`DIVIDES(divisor, dividend)` uses the same two independent conditions. If mathematical divisibility is used, specify `0 | 0` deliberately; `TRY_EXACT_DIVIDE` still rejects a zero divisor.

`GCD(a, b)` and `LCM(a, b)` use lane minimum/maximum plus binary `gcd`/`lcm` of the cofactors, with ordinary nonnegative zero conventions. Cofactor GCD is not free simply because lane GCD is local.

`POW_UNSIGNED(a, n)` multiplies every exponent by `n` with overflow checks and computes `cofactor^n`; sign follows parity. Define `0^0` as a typed failure or as identity explicitly rather than inheriting a host-language accident.

`CANCEL_COMMON(numerator, denominator)` for rational simplification subtracts lane minima and computes/co-divides by `gcd` of exact cofactors. Store rationals as a sign plus two canonical nonnegative magnitudes, not as negative exponent lanes in the first core. Negative exponents would silently broaden the domain and complicate zero, overflow, serialization, and cofactor normalization.

### 6.3 Magnitude and representation crossings

These use or construct ordinary magnitude state and must say so:

- ordinary binary ingress;
- exact numeric comparison across general values;
- reconstruction/egress;
- addition and subtraction;
- bank migration;
- any fallback from exponent overflow;
- verification against a claimed external magnitude.

`RECONSTRUCT` multiplies the exact cofactor by bank prime powers. Exponentiation strategy, multiplication count, intermediate bit lengths, and allocation are part of egress cost.

`DECOMPOSE` remains rejected as a generic opcode because it hides whether the machine is extracting a bounded set of valuations or attempting full factorization.

## 7. Addition without false locality

### 7.1 Eager common-factor split

For canonical nonzero inputs

```text
a = sa * ca * product(pi ^ ai)
b = sb * cb * product(pi ^ bi),
```

let `gi = min(ai, bi)` and compute exact residual terms

```text
A = ca * product(pi ^ (ai - gi))
B = cb * product(pi ^ (bi - gi))
S = sa*A + sb*B.
```

If `S = 0`, the answer is `Zero`. Otherwise the result initially has sign `sign(S)`, lower exponents `g`, and exact residual `abs(S)`.

For every lane where `ai != bi`, exactly one of `A` and `B` is divisible by `pi`; the other is not. Therefore `S` is not divisible by `pi`, even for subtraction, and the exact output valuation is `gi`.

For a lane where `ai = bi`, neither residual term is divisible by `pi`, but their sum or difference may be. Only those equal-exponent lanes need new valuation extraction. An eager `ADD_SPLIT_EAGER` therefore:

1. preserves `gi` exactly on unequal lanes;
2. trial-divides `abs(S)` only for equal lanes;
3. adds each extracted valuation to `gi`, checking overflow;
4. leaves the final residual as canonical cofactor.

This is not bank-local addition. It constructs residual magnitudes, performs a signed binary addition/subtraction, and may perform many divisions. Its earned claim is narrower: it avoids re-extracting lanes whose unequal valuations already determine the answer and preserves a known common factor explicitly.

### 7.2 Deferred common-factor split

`ADD_SPLIT_DEFERRED` performs the same residual magnitude computation but does not trial-divide uncertain lanes. For canonical inputs it emits:

```text
lowerExponent[i] = gi
exactLaneMask[i] = (ai != bi)
exactResidual = abs(S)
```

For deferred inputs, a result lane may be marked exact only when both input lanes are exact and their stored exponents differ. All other result lanes are `AtLeast(gi)`.

This state is always exact as a number. It is only partially exact as a valuation cache. It can support useful work without lying:

- reconstruction and egress are exact;
- an exact lane answers a valuation query;
- a lower-bound lane returns `AtLeast(e)`, never `e`;
- multiplication adds lower bounds, multiplies residuals, and marks a lane exact only if both source lanes were exact;
- tracked-prime scaling increments the proved lower bound and preserves the lane's exact/lower-bound status;
- `REFRESH(mask)` extracts selected primes from the residual and marks those lanes exact;
- `NORMALIZE` refreshes all lanes and returns canonical form;
- exact division, GCD, LCM, and field-based numeric equality require normalization or an explicitly charged magnitude fallback when relevant lanes are not exact.

Repeated deferred addition is possible, but the exactness rule becomes conservative. Factoring the minimum stored lower bound is always safe; after the residual sum, a lane is exact only under the both-exact-and-unequal condition above. This rule needs exhaustive signed-domain tests before any optimization claim.

Deferred state earns its complexity only if later work consumes exact lanes or common lower bounds before normalization. If typical workloads normalize immediately, the mask and branching are pure overhead.

### 7.3 Why `STALE` is rejected

A `STALE(e)` lane normally means the cached number may no longer equal either `e` or a lower bound. That makes divisibility and cancellation unsafe. Versioning a cached valuation against an authoritative magnitude can make an ordinary cache safe, but then the cache is not the number representation and every read must validate the version. Build 001 should compare such a conventional cache as a control rather than put stale claims inside `HybridInteger`.

## 8. Refresh, normalization, and migration

`REFRESH_LANES(value, mask)` on deferred input:

1. begins with an unchanged exact value;
2. for each requested lower-bound lane, repeatedly divides the residual by its prime;
3. increments the stored lower exponent for every exact division;
4. fails atomically on exponent overflow;
5. marks the lane exact only after the first nondivisible remainder establishes maximality;
6. records tests, divisions, lane reads/writes, residual bit lengths, and state-bit transitions.

Dividing the residual by one bank prime cannot change divisibility by another distinct bank prime, so independently refreshed exact masks remain sound. A no-op refresh of an already exact lane should be visible but cheap.

`NORMALIZE` refreshes every lower-bound lane and returns `CanonicalHybridInteger`. It must not silently run as part of an operation described as bank-local.

`MIGRATE_BANK` is neither normalization nor a metadata relabel. It reconstructs removed lane factors into the residual and extracts newly tracked primes. Migration receipts should distinguish copied lanes, removed-lane exponentiations/multiplications, new-prime remainder/division work, configuration reads, allocation, and overflow.

## 9. Minimal VM recommendation

### 9.1 Register state

Each value register has an explicit state:

```text
Empty
Canonical(CanonicalHybridInteger)
Deferred(DeferredHybridInteger)
Invalid(failure-id)
```

`Invalid` is poison/control state, not a numeric variant. Reading it fails with `SourceInvalid` and links to the producing failure receipt. An implementation may collapse `Empty` and `Invalid` internally only if receipts still distinguish never-written from invalidated.

Instructions are atomic:

1. validate and snapshot all sources before touching the destination, including when destination aliases a source;
2. clear/poison the destination before committing a fallible result;
3. commit only a complete invariant-preserving value;
4. on failure, leave sources unchanged and destination invalid;
5. scalar/predicate outputs are cleared before execution, so a failed instruction cannot expose an earlier successful scalar.

Default execution stops at the first failure. Continue-on-failure mode is useful only for propagation tests and must retain the run's first failure even if later independent steps succeed.

### 9.2 Proposed opcodes

Ingress:

- `LOAD_BINARY dst, x` — bounded valuation extraction from ordinary magnitude;
- `LOAD_COMPONENTS dst, sign, exponents, cofactor` — validate a tuple that defines its own value;
- `LOAD_CLAIMED dst, x, components, evidence` — verify the external equivalence claim;
- `DESERIALIZE dst, bytes` — strict checked decode.

Structural/hybrid operations:

- `COMPOSE dst, a, b`;
- `TRY_CANCEL dst, dividend, divisor`;
- `SCALE_LANE dst, src, lane, amount`;
- `POW_UNSIGNED dst, src, exponent`;
- `DIVIDES pred, divisor, dividend`;
- `VALUATION scalar, src, lane` returning `Exact`, `AtLeast`, or `ZeroValuation`;
- `GCD dst, a, b`;
- `LCM dst, a, b`;
- `CANCEL_COMMON numDst, denDst, numerator, denominator` if two-destination atomic commit is implemented correctly.

Boundary and maintenance:

- `ADD_SPLIT_EAGER dst, a, b`;
- `ADD_SPLIT_DEFERRED dst, a, b`;
- `SUB_SPLIT_EAGER` and `SUB_SPLIT_DEFERRED`, or one signed-add opcode with an explicit mode;
- `REFRESH dst, src, laneMask`;
- `NORMALIZE dst, src`;
- `MIGRATE_BANK dst, src, targetBank` in the adaptive-bank experiment, not necessarily the first VM;
- `RECONSTRUCT scalar, src`;
- `NUMERIC_COMPARE pred, a, b` with an explicit exact magnitude path;
- `SERIALIZE bytes, src`.

`PROJECT` should be renamed `VALUATION` because a deferred result is not always a scalar exponent. `INVALIDATE` need not be a public arithmetic opcode; failure invalidation is part of every instruction's commit protocol. A diagnostic `DROP` may clear a register without implying arithmetic.

### 9.3 Failure semantics

Use stable typed codes rather than exception text:

```text
UninitializedSource
SourceInvalid
DestinationOutOfRange
LaneOutOfRange
BankMismatch
MalformedEncoding
UnsupportedVersion
InvalidTagOrSign
InvalidCofactor
NoncanonicalCofactor
InvalidCertificate
ClaimedMagnitudeMismatch
ExponentOverflow
DivisionByZero
LaneUnderflow
CofactorNotDivisible
ValuationNotExact
UnsupportedOnDeferred
ResultZeroWhereNonzeroRequired
ResourceLimit
```

A receipt should include operation class (`Ingress`, `BankLocal`, `HybridNative`, `Maintenance`, `MagnitudeBoundary`, `Egress`), input/output state classes, bank identity, success/failure, exact work counters, and whether full magnitude was materialized. “Succeeded” must mean both numeric correctness and invariant preservation.

## 10. Cost contract

Do not collapse costs into one score. Every operation receipt should be able to report:

```text
payload bits:
  sign/zero tag, lane payload, freshness mask, cofactor payload
amortized configuration:
  prime catalogue, lane map, validation evidence
bank work:
  lane reads/writes, bounded adds/subtracts/compares, status reductions, modeled NAND work/depth
cofactor work:
  additions, multiplications, divisions, remainders, gcds, powers, operand/result bit lengths
validation work:
  primality checks, canonicality remainder tests, claimed-magnitude verification
maintenance work:
  refreshed lanes, invalidated/exact mask bits, migrations, allocations
boundary work:
  reconstruction powers/multiplications, magnitude materializations, serialization bytes
control/memory where measured:
  branches, register reads/writes, allocation, cache misses
```

Costs remain separated into ingress, resident/native operation, maintenance, and egress. A cheap `COMPOSE` after expensive `LOAD_BINARY` is not a workload win until the full trace amortizes ingress. A lane-only NAND count and a host `BigInteger` cofactor multiplication are different cost classes and must not be added as if they shared units.

## 11. Testable invariants and required tests

### 11.1 Constructor and canonicality tests

1. For every accepted nonzero value, sign is `+1/-1`, cofactor is positive, lane count equals bank count, every exponent is in range, and every `cofactor mod p_i` is nonzero.
2. Zero has one representation and no payload; negative zero is impossible.
3. Ingest then reconstruct equals the original signed integer over exhaustive small domains and seeded large domains.
4. Ingesting the same integer twice under one bank yields identical fields and identical bytes.
5. All public constructors and decoders either return a complete valid value or a typed failure; no partial value escapes.
6. Boundary cases include `0`, `+/-1`, smallest/largest lane exponent, exact overflow by one, large out-of-bank primes, composite cofactors, and mixed signs.

### 11.2 Arithmetic differential tests

1. Exhaustively compare compose, exact divide, divides, gcd, lcm, scale, unsigned powers, and reconstruction against ordinary integer arithmetic on a bounded signed domain.
2. Verify every successful operation's canonical invariant independently, not only its reconstructed magnitude.
3. Check identity, zero absorption, sign laws, commutativity where applicable, and operation-specific failure distinctions.
4. Exercise cofactor-heavy hostile values so tests cannot pass by using only bank-smooth inputs.
5. Check that exponent and cofactor failures are atomic and sources are unchanged.
6. Compare all operations under several banks, including non-prefix workload-selected banks and bank mismatches.

### 11.3 Addition and deferred-state tests

1. Exhaustively test signed addition/subtraction over a small domain, including exact cancellation to zero.
2. For canonical inputs, verify the theorem used by `ADD_SPLIT`: unequal input valuations produce exact output minimum; equal valuations are never marked exact without extraction.
3. For every deferred result and every lane, independently compute ordinary `v_p`: `Exact(e)` must equal it; `AtLeast(e)` must not exceed it.
4. `NORMALIZE(deferred)` must reconstruct to the same integer, satisfy canonicality, and equal ordinary ingress of that integer.
5. Refreshing one lane must not invalidate exact claims on other lanes.
6. Repeated add/multiply/scale sequences must preserve the lower-bound invariant.
7. Any operation that requires exact lanes must fail with `ValuationNotExact` or take and report an explicit fallback; it may not read a lower bound as exact.
8. Benchmark eager versus deferred paths including cases that consume exact lanes before refresh and cases that immediately normalize.

### 11.4 Serialization and adversarial tests

1. Round-trip every semantic boundary and require canonical byte stability.
2. Reject truncated frames, trailing bytes, nonminimal varints/cofactors, bad padding bits, unknown versions/tags, negative zero, zero payloads, count mismatches, excessive lengths, composite or repeated bank labels, overflowing lanes, and cofactor/bank common factors.
3. Fuzz the decoder and assert that it returns only a valid value or typed failure, never an uncontrolled allocation or partially valid object.
4. Cross-platform fixtures must fix byte order and not depend on .NET's signed little-endian `BigInteger` representation.

### 11.5 Certificate, provenance, and VM tests

1. A false external factor claim must fail even when a caller labels it verified.
2. Component ingress without a claimed magnitude must be described as defining a value, not certifying an external one.
3. Operation-derived receipts must identify parent values and evidence classes without affecting numeric equality.
4. Destination/source aliasing must be safe for every fallible opcode.
5. On failure, the old destination and old scalar/predicate output must be unavailable.
6. Continue-on-failure execution must report downstream invalid-source failures and retain the original failed program counter.
7. Deserialization, bank mismatch, overflow, zero division, lane underflow, and cofactor remainder must remain distinct.

### 11.6 Migration tests

1. Migrating to a superset bank preserves copied lanes and extracts only added primes from the cofactor.
2. Migrating to a subset reconstructs removed lane powers into the cofactor.
3. Round-trip migration preserves numeric value but not necessarily representation bytes.
4. Migration overflow and malformed target bank fail atomically.
5. Adaptive benchmarks charge the controller and every migration, including unsuccessful or thrashing decisions.

## 12. Designs to reject now

1. **Complete factorization as ingress.** It defeats the bounded-bank purpose and makes cheap resident operations depend on an unbounded hidden preparation step.
2. **Cofactor as a fake prime or opaque generator.** A composite exact integer is not an atom; this breaks GCD, divisibility, and migration semantics.
3. **Canonical lanes with an unnormalized cofactor.** If a bank prime divides the cofactor while its lane is claimed exact, multiple encodings denote the same number and valuation queries lie.
4. **Silent exponent wrap, saturation, or spill.** Each requires explicit semantics. Spill into a “coprime” cofactor is a contradiction.
5. **`CertificateVerified: bool`.** This delegates correctness to an unaudited assertion. Verification must be performed or represented by an unforgeable internal validated type.
6. **Unknown equals zero.** A lower bound of zero says only that no factor has been established; it does not establish nondivisibility.
7. **Stale lanes inside the number.** A possibly false cached value is not a safe arithmetic component. Use an external versioned cache control or a proved lower bound.
8. **Public unchecked constructors/deserializers.** Malformed tuples must not enter arithmetic and fail later as if they were valid.
9. **In-place bank resizing.** It reinterprets lane identity. Migration creates a new value under a new immutable bank.
10. **Fieldwise equality across banks.** Lane positions have bank-relative meaning. Cross-bank numeric equality is a charged operation.
11. **Provenance in numeric identity.** Evidence may differ for the same integer. Keep it in receipts or an audit envelope.
12. **Prime-bank-only operation claims for the hybrid.** Composition still multiplies cofactors; GCD still computes cofactor GCD; cancellation still divides cofactors.
13. **Generic `DECOMPOSE`.** It hides whether work is bounded valuation extraction, certificate checking, or full factorization.
14. **Negative exponents in the integer core.** They silently change the domain to rationals and complicate zero and canonical residual rules. Test a separate rational pair.
15. **Adaptive-bank logic baked into arithmetic semantics.** It makes reproducibility and cost attribution difficult. Compare explicit policies around a fixed-bank core.
16. **Default runtime serialization.** Signed endianness, redundant forms, version drift, and unchecked allocation make it unsuitable as the canonical format.
17. **Always-deferred addition.** If the next operation needs normalization, it only adds mask and dispatch overhead. Keep eager and deferred variants as competing hypotheses.
18. **One headline “native” speedup.** A valid Build 001 conclusion needs full workload traces with ingress, cofactor arithmetic, maintenance, egress, payload, and configuration visible.

## 13. Implementation traps in the current Build 000 code

1. `PrimeMachine.LoadCertifiedCoordinates` accepts a caller Boolean and constructs coordinates without verifying any external magnitude. Replace it; do not extend that trust convention into Build 001.
2. `PrimeCoordinates` has a public constructor. Build 001's canonical hybrid type should use a private checked construction boundary.
3. Build 000 `Compose` models only exponent lanes. Reusing that receipt for hybrid composition would omit exact cofactor multiplication.
4. Build 000 `BasisEscape` treats the residual as failure. Build 001 ordinary ingress should instead keep that residual as the successful exact cofactor, while retaining exponent overflow as a distinct failure under the strict contract.
5. Build 000 `TaggedPrimeValue` represents sign/zero but inherits closed-basis failure. Do not layer a cofactor beside it without re-establishing one unique zero and one nonzero invariant.
6. `BigInteger` constructors and byte APIs have signed encodings and platform/API conventions. Never assume their default representation is the canonical wire format.
7. Checking `gcd(cofactor, product(B))` by constructing the full bank product can create unnecessary large intermediates. Checking `cofactor mod p_i != 0` lane by lane proves the same invariant and exposes per-lane cost.
8. Destination aliasing can erase a source if the VM invalidates the destination before reading all operands. Snapshot immutable sources first, then apply the atomic commit protocol.
9. A deferred residual is shared across lanes. Refresh must divide the stored residual as it increments a lane; merely changing the mask produces an incorrect value.
10. In signed addition, factor out magnitudes first but compute the residual **signed** sum. Taking absolute values before the addition loses cancellation and sign.
11. `v_p(a+b) = min(v_p(a), v_p(b))` is exact only when the two valuations differ. Equal lanes are precisely the uncertain case.
12. Fixed lane overflow can occur during compose, scale, power, refresh, migration, or structured ingress, not only binary ingress.
13. A cofactor division operation must test remainder before committing quotient. Integer truncation is not exact cancellation.
14. Microbenchmarks that generate inputs by binary ingress inside one side but preload the other side confound resident cost with ingress. Emit both resident and end-to-end traces.

## 14. Recommended implementation sequence and stop gates

1. Implement immutable `ValuationBank` and strict `CanonicalHybridInteger`, checked binary/component ingress, reconstruction, serialization, and invariant validation.
2. Differentially test zero/sign, arbitrary cofactors, overflow, malformed input, and same-bank equality before adding a VM.
3. Add canonical compose, exact divide/divides, scale, GCD/LCM, powers, and rational common cancellation with separate lane/cofactor receipts.
4. Add `ADD_SPLIT_EAGER` and prove its lane-state rule exhaustively over a signed finite domain.
5. Add the separately typed deferred form, refresh/normalize, and only the operations whose lower-bound rules are proved and tested.
6. Replace the Build 000 VM's Boolean certificate path with validated component/claim ingress and atomic register failure semantics.
7. Compare fixed banks first. Add explicit migration and adaptive policies only after fixed-bank receipts are stable.

Stop or simplify when any of these occurs:

- the strict hybrid never reaches a Pareto frontier after cofactor work and metadata are charged;
- deferred addition is normally normalized before any useful structural consumer;
- invariant validation costs as much as fresh bounded ingress in the named workload;
- bank migration or thrashing dominates retained factor work;
- a conventional magnitude plus external valuation cache provides the same benefit with less state and simpler invalidation;
- specialized established algorithms dominate the proposed operation traces.

The architecture earns at most a specialized claim until end-to-end workload evidence shows otherwise. Its most defensible novel test is not “multiplication becomes addition.” It is whether a typed exact integer can retain a small, explicitly verified valuation cache through enough real operations—and preserve useful lower bounds across addition often enough—to amortize ingress, cofactor arithmetic, freshness metadata, and crossings.
