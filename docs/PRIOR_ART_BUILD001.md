# Build 001 prior art: bounded prime-valuation bank and exact cofactor

## Scope and evidence status

This deliverable investigates the proposed Build 001 value shape

\[
x = \operatorname{sgn}(x)\left(\prod_{p\in S}p^{v_p(|x|)}\right)c,
\qquad \gcd\left(c,\prod_{p\in S}p\right)=1,
\]

where `S` is a finite selected-prime bank and `c` is an exact residual cofactor. It is the Build 001 prior-art record, not a novelty, performance, or hardware claim. Sources were checked on 2026-08-24. Primary papers, official project documentation, and pinned upstream source are preferred. Build 000's broader survey remains controlling for the already-covered neighboring fields.

Claim labels used below:

- **[ESTABLISHED MATHEMATICS]** A standard consequence or named construction, not a repository discovery.
- **[SOURCED PRIOR ART]** A feature directly documented by a primary or official source.
- **[ENGINEERING INFERENCE]** A consequence inferred for the proposed machine; it still requires implementation evidence.
- **[NEGATIVE RESULT]** A reason to lower the novelty or expected-advantage ceiling.
- **[OPEN QUESTION]** A question that Build 001 can answer experimentally.

## Executive conclusion

**[NEGATIVE RESULT]** The representation itself is not new. The established mathematical description is a finite vector of prime valuations together with the exact `S`-free (or `S`-coprime) cofactor. The `S`-part of a nonzero integer is standard terminology for the largest divisor supported on primes in `S`. Calling the whole value an `S`-unit would be wrong unless the cofactor is one (up to sign, under the integer convention).

**[NEGATIVE RESULT]** PARI/GP is a very close implementation predecessor. Its documented `Z_smoothen(n, L, &P, &E)` strips every occurrence of an explicit list of small primes, returns the exponent data, and returns the exact cofactor. PARI's `famat` machinery separately keeps arbitrary products in factored form, makes multiplication and powering structural, and can fold factors beyond a limit back into an opaque factor. This covers most of the proposed ingress and product-resident representation mechanics.

**[NEGATIVE RESULT]** FLINT is another near-direct predecessor. `fmpz_factor_t` stores sign plus base/exponent vectors; its bounded trial routines keep the unfactored cofactor as the final entry, and `fmpz_factor_smooth` may return a known-composite final cofactor. FLINT also provides refinement and expansion. The principal differences are fixed lane addressing, bank identity, and maintaining the split as a general arithmetic value rather than as a factorization result.

**[NEGATIVE RESULT]** FriCAS is the closest predecessor for partial factor knowledge as part of a persistent typed value. `Factored(R)` stores a unit and bases with exponents and knowledge flags (`nil`, square-free, irreducible, or prime), allows incompletely factored exact values, and makes expansion explicit. This sharply lowers any novelty claim for `KNOWN`/`UNKNOWN` factor metadata.

**[SOURCED PRIOR ART]** OpenJDK `BigInteger` gives a deployed one-lane example: it stores exact sign and magnitude and lazily caches `getLowestSetBit()`, which for a nonzero integer is exactly the 2-adic valuation. SymPy supplies a bounded factor cache with validated entries and eviction. FLINT additionally supplies canonical one-prime valuation/unit storage and selectable none/smooth/full integer-factor policies. Factor-base algorithms routinely retain a residual outside a chosen base, including useful one- and two-large-prime partial relations.

The defensible Build 001 contribution is therefore not a new number representation. At most, it is a controlled systems combination and evaluation:

> a fixed-address, bounded multi-prime bank attached to an exact value, with an explicit `S`-free invariant, exact bank migration, operation-specific maintenance, provenance/validity states, receipt-separated costs, and workload/adversarial bank-policy measurements on a shared substrate.

Even that combination should be reported as **“not located in this bounded search”**, not as unprecedented. The likely research value is determining when this integration is useful and when it merely recreates mature CAS and factor-base techniques with more metadata.

## Established name and exact contract

For a finite set of primes `S` and nonzero integer `x`, define

\[
[|x|]_S = \prod_{p\in S}p^{v_p(|x|)},
\qquad c_S(x)=|x|/[|x|]_S.
\]

Then `c_S(x)` is coprime to every prime in `S`; equivalently, it is `S`-free. Unique factorization makes this decomposition unique once `S` and the sign convention are fixed.

- **[ESTABLISHED MATHEMATICS]** Bugeaud, Evertse, and Győry use “the `S`-part” for the largest divisor of an integer composed only of primes from a finite set `S`. This directly names the bank-resident product.
- **[ESTABLISHED MATHEMATICS]** The coordinate vector is a finite restriction of the ordinary valuation map `p \mapsto v_p(x)`. “Finite valuation vector with an exact `S`-free cofactor” is the clearest description.
- **[ESTABLISHED MATHEMATICS]** `S`-smooth means the cofactor is one. The general hybrid value is not `S`-smooth.
- **[ESTABLISHED MATHEMATICS]** An integer `S`-unit has no prime divisors outside `S` (with sign handled by the ambient unit convention). A general hybrid value with `c>1` is not an `S`-unit.
- **[SOURCED PRIOR ART]** “Factor base plus residual/large prime(s)” is the closest algorithmic language in sieve factorization, but those records are usually transient relations rather than a general exact arithmetic type.

Recommended neutral repository terminology:

- `PrimeBank S`: the declared finite ordered set of tracked primes;
- `SPartExponents`: the exact valuations for every bank prime;
- `SFreeCofactor`: the exact positive residual coprime to all bank primes;
- `BankedInteger`: zero, or sign plus the preceding data;
- `UncheckedBankView`: a separate type for incomplete or unverified factor knowledge.

“Hybrid prime representation” is suitable as a project nickname, but not as a claim that the decomposition is novel.

## Ranked architectural proximity

The ranking measures closeness to the proposed architecture, not historical importance.

| Rank | Prior system or line | Direct overlap | Material difference from Build 001 |
|---:|---|---|---|
| 1 | PARI/GP `Z_smoothen` plus `famat` | Explicit selected-prime stripping; exponents plus exact cofactor; persistent factored products; limit-based folding into an opaque factor | Sparse factor lists rather than a fixed addressable bank; no proposed per-value receipts or lane-validity protocol |
| 2 | FLINT `fmpz_factor_t` and bounded/smooth factor routines | Sign plus base/exponent vectors; first-prime/range trial extraction; exact final cofactor; completeness result; refinement and expansion | Factorization object rather than a fixed-address arithmetic value; bank identity and zero-exponent lanes are absent |
| 3 | FriCAS `Factored(R)` | Exact partially factored values; base/exponent records; knowledge flags; explicit expansion; multiplication/gcd-friendly structure | Arbitrary factor list, not a fixed selected-prime register bank or a measured machine policy |
| 4 | Finite `S`-part / `S`-free decomposition | The exact mathematical object and uniqueness contract | Mathematics, not an execution architecture |
| 5 | OpenJDK `BigInteger` lazy lowest-set-bit cache | Exact magnitude plus a lazily populated stable cache of `v_2(x)` | One fixed prime only; no structural multiplication path; cache is derived metadata, not the authoritative representation |
| 5 | FLINT p-adics and `CA_FACTOR_ZZ_*` policy | Canonical `p^v u` at one prime; cached prime context; selectable none/smooth/full factor policy | p-adics are finite-precision field elements, not exact arbitrary integers; the factor policy is CAS simplification, not a banked integer type |
| 6 | Factor bases and large-prime variants in QS/NFS | Workload-selected prime base, exponent/parity records, and retained residual factors outside the base | Transient relation collection aimed at factorization, usually modulo-2 exponent information, not general exact arithmetic |
| 7 | GMP `mpz_remove`, `mpq`, factorial/binomial internals | Exact one-factor stripping; canonical rational cancellation; specialized valuation-aware/product algorithms | Operations rather than a persistent multi-prime value representation |
| 8 | SymPy `FactorCache` | Validated factor knowledge, bounded storage, replacement, external-source hooks | Global integer-to-one-factor cache; does not store valuations or exact cofactors per value |
| 9 | Constant-divisor algorithms and circuits | Makes fixed-prime divisibility, quotient, and remainder much cheaper than generic division | An implementation technique for bank ingress/query, not a number representation |

## Detailed findings

### 1. PARI/GP: selected-prime stripping and factored products

**[SOURCED PRIOR ART]** The official libPARI interface documents `Z_smoothen(n, L, &P, &E)`: `L` is an explicit list of small primes; the routine trial-divides `n`, records the primes and exponents found, and returns the remaining cofactor. This is the closest direct match to ordinary-integer ingress into a fixed bank.

**[SOURCED PRIOR ART]** `Z_factor_limit(n, lim)` similarly performs bounded small-prime extraction. Its final returned “prime” can be known composite. That warning matters: a residual is exact without being certified prime, and a factor-list API must not imply more knowledge than it has earned.

**[SOURCED PRIOR ART]** PARI's algebra interface defines factored matrices (`famat`) whose bases need not all be prime. Multiplication can concatenate factor lists, exponentiation can scale exponents, and reduction combines like bases. The manual explicitly motivates this form as a way to avoid expanding very large products. `ZM_famat_limit` folds factors beyond a limit into an exponent-one opaque entry, a close analogue of a bounded explicit component plus exact cofactor.

**[SOURCED PRIOR ART]** PARI's public `factorint` documentation distinguishes probable from proven prime-factor output under its modes and flags, and `factorback` reconstructs. Exact reconstruction and factor-status certification are therefore separate concerns in mature software.

**[ENGINEERING INFERENCE]** Build 001 should treat PARI as the primary differential and conceptual baseline for ingress and product-resident workloads. A benchmark only against repeated general-purpose `factorint` would be a straw baseline.

### 2. FLINT: signed factor vectors with an explicit residual

**[SOURCED PRIOR ART]** FLINT documents `fmpz_factor_t` as a sign field plus parallel base and exponent vectors. In canonical complete form, the bases are sorted primes and the exponents are positive.

**[SOURCED PRIOR ART]** `fmpz_factor_trial(n, num_primes)` uses a bounded prefix of FLINT's prime table; `fmpz_factor_trial_range` selects a bounded table range. If extraction is incomplete, the final factor entry is the exact cofactor after the trial primes have been removed. `fmpz_factor_smooth` likewise produces prime factors up to an approximate bit bound plus at most one cofactor, which may be composite, and returns whether the factorization is definitely complete.

**[SOURCED PRIOR ART]** `fmpz_factor_refine` improves a partial factorization into pairwise-coprime bases without promising canonical prime form, and `fmpz_factor_expand_iterative` reconstructs the exact integer.

**[NEGATIVE RESULT]** This is already sign plus factor coordinates plus an exact opaque residual, including completeness status and explicit reconstruction. Build 001's narrower difference is that `S` is a declared address space whose zero valuations matter, the cofactor is required to be `S`-free, and the structure is maintained through ordinary arithmetic rather than returned only as a factorization record.

### 3. FriCAS: exact values with graded factor knowledge

**[SOURCED PRIOR ART]** The official FriCAS API describes `Factored(R)` as a unit plus a list of bases, exponents, and factor-status flags. The exposed statuses include unknown (`nil`), square-free, irreducible, and prime. Exactness of the product does not depend on complete factor knowledge.

**[SOURCED PRIOR ART]** The FriCAS user guide emphasizes that arithmetic can return an incompletely factored but exact result and that `expand` is explicit rather than an automatic coercion. The API includes constructors for factors whose status is not known and operations that merge compatible factor records.

**[ENGINEERING INFERENCE]** FriCAS supplies the strongest reason to avoid presenting a four-state factor-knowledge ledger as a new idea. The useful Build 001 question is narrower: whether fixed bank addresses and operation-specific freshness make that old idea cheaper or more predictable in a VM/runtime setting.

**[ENGINEERING INFERENCE]** A transformation must not silently preserve a prime/square-free/irreducible certificate when the transformation does not preserve that property. FriCAS's graded factor information is a good design precedent for conservative invalidation.

**[SOURCED PRIOR ART]** Other CAS interfaces reinforce the same ceiling. Sage's immutable `Factorization` carries a unit and factor/exponent sequence and reconstructs with `value()`. Magma has a dedicated integer-factorization-sequence type, can return completely factored and unresolved composite parts separately, retains discovered prime factors for later attempts, and provides `PartialFactorization` using gcd and exact division. These are less structurally close than PARI, FLINT, and FriCAS, but they rule out treating exact factored containers, cached factors, or unresolved composite parts as new mechanisms.

### 4. One-prime valuation/unit representations and caches

**[SOURCED PRIOR ART]** FLINT `padic_t` represents a p-adic value as `p^v u` at a chosen prime `p`, with a valuation, unit, and precision. Canonicalization requires the stored unit not to remain divisible by `p` (apart from the zero case). A context stores `p` and precomputed powers.

**[LIMIT OF ANALOGY]** This is a one-prime, finite-precision p-adic field representation. It is not an exact integer with several selected valuations. It establishes the valuation/unit normalization pattern, not the proposed multi-prime machine.

**[SOURCED PRIOR ART]** OpenJDK `BigInteger` stores immutable exact sign/magnitude and several lazily computed fields. `lowestSetBitPlusTwo` caches `getLowestSetBit()`; for a nonzero integer that quantity is `v_2(x)`. The source uses a sentinel for “not yet computed” and then retains the stable value.

**[ENGINEERING INFERENCE]** For immutable values, `STALE` is usually unnecessary. A derived lane is either uncomputed/unknown or computed/known for that exact value. `STALE` only becomes meaningful if the authoritative magnitude can mutate, the bank definition changes under the object, or a cache record is independently updated. Build 001 should not pay for a state that its object model cannot reach.

### 5. Partial/smooth factor policies and bounded caches

**[SOURCED PRIOR ART]** FLINT's exact-algebra context exposes `CA_FACTOR_ZZ_NONE`, `CA_FACTOR_ZZ_SMOOTH`, and `CA_FACTOR_ZZ_FULL`. The smooth mode extracts small prime factors and perfect powers; the full mode attempts complete factorization and is documented as prohibitively slow for general 70–80 digit integers. Context changes are not retroactive to objects already cached.

**[ENGINEERING INFERENCE]** Bank policy is an execution-policy choice, not a new mathematical domain. Policy identity/version must be recorded if values or caches outlive a policy change.

**[SOURCED PRIOR ART]** SymPy's current `FactorCache` is a bounded global mapping from an integer to a validated prime divisor. It has a configurable maximum size (documented default 1000), performs eviction, is populated by factoring operations, permits checked manual insertion, and can consult external factor sources.

**[LIMIT OF ANALOGY]** This cache stores a factor witness, not a complete valuation or an `S`-free cofactor. It is nevertheless direct prior art for adaptive cache budgets, admission, validation, and replacement.

### 6. Factor bases and residuals are established algorithmic machinery

**[SOURCED PRIOR ART]** Sieve factorization selects a factor base and records relations whose values are smooth over that base. Large-prime variants retain relations with one or two factors outside the base and later combine them. Lenstra and Manasse report that two-large-prime variants materially improved quadratic-sieve running time in their implementation.

**[SOURCED PRIOR ART]** CADO-NFS's official README recommends removing small factors by trial division, Pollard `p-1`, Williams `p+1`, or ECM before applying NFS to the remaining composite factor. This is another production boundary between cheaply extracted structure and an exact opaque residual.

**[ENGINEERING INFERENCE]** A cofactor is not failed factoring. It is the necessary exact remainder of a bounded factor policy and can remain useful without a primality certificate.

**[LIMIT OF ANALOGY]** Sieve relations commonly retain exponent parity or relation-specific data, not the full signed integer semantics Build 001 requires. Their main lesson is policy and residual handling, not a ready-made general ALU.

### 7. Rational cancellation and optimized magnitude baselines

**[SOURCED PRIOR ART]** GMP's `mpz_remove(rop, op, f)` removes every occurrence of a factor `f`, returns the multiplicity, and leaves the exact residual in `rop`. This is an optimized one-bank-lane primitive.

**[SOURCED PRIOR ART]** GMP canonical rationals require coprime numerator and denominator, a positive denominator, and canonical zero. GMP's rational-internals documentation describes cross-GCD cancellation and explicitly notes that eager canonicalization can be badly suboptimal when operands already have simple factorizations or little cancellation.

**[ENGINEERING INFERENCE]** Rational cancellation is a promising workload, but its honest baseline includes gcd-aware GMP rationals and factored CAS objects. Bank-local exponent cancellation is only part of the job; residual cofactors may still share primes outside `S`, requiring gcd or equivalent work.

**[SOURCED PRIOR ART]** GMP's documented factorial algorithm separately accounts for the power of two, builds odd prime products with sieving and product trees, and its binomial implementation batches products with exact divisions. These are specialized magnitude-domain algorithms that already exploit relevant structure.

**[ENGINEERING INFERENCE]** A banked factorial or binomial benchmark must compare total work against these specialized algorithms, not against naïve repeated multiplication and later factorization.

### 8. Fixed-prime division is not generic division

**[SOURCED PRIOR ART]** Granlund and Montgomery show how division by invariant integers can be replaced by multiplication and shifts. Later FPGA work supplies scalable constant-divider circuits returning quotient and remainder and reports synthesis measurements.

**[ENGINEERING INFERENCE]** A fixed bank permits per-prime precomputation and constant-divisor implementations. Any gate, instruction, or timing model that charges every bank probe as generic division is too pessimistic. Conversely, claiming the operation free because the divisor is constant is too optimistic: width, quotient/remainder production, repeated extraction, and circuit area still count.

## Architectural implications for Build 001

### A. Separate canonical values from incomplete views

**[ENGINEERING INFERENCE]** The following statements cannot all hold for the same nonzero object without an additional authoritative magnitude:

1. the object stores only sign, bank exponents, and cofactor;
2. the cofactor is certified coprime to every prime in `S`;
3. some bank exponent is `UNKNOWN` or `STALE`;
4. the object remains exact.

If `v_p(x)` is unknown, the implementation does not know how much `p` must be removed from the cofactor. It must choose one of three honest designs:

- normalize every lane before constructing a canonical `BankedInteger`;
- retain an authoritative ordinary magnitude and treat lanes as a derived cache;
- return a distinct `UncheckedBankView` that makes no `S`-free claim until normalized.

An `INVALID` certificate state can describe rejected external evidence, but it should not be accepted inside a canonical value. A `STALE` state needs a concrete mutation or bank-version story; otherwise use `UNKNOWN`.

### B. Bank identity is part of the representation contract

**[ENGINEERING INFERENCE]** Two exponent arrays are not comparable unless their ordered prime bank and version are known. Bank identity belongs in the type, object, or execution context and must appear in serialized evidence.

Exact migration has a simple local form:

- admitting a new prime `q` strips `q` from the old cofactor and creates the new exponent lane;
- evicting `p` multiplies `p^e` back into the cofactor;
- reordering lanes changes addressing but not mathematical content;
- merging values with different banks requires explicit reconciliation or reconstruction.

None of these requires complete factorization, but none is free.

### C. Native and disruptive operations can be specified precisely

For canonical values under the same bank:

- **multiplication:** add bank exponents and multiply cofactors; the resulting cofactor remains `S`-free;
- **exact powers:** scale exponents and power the cofactor; the invariant remains local;
- **gcd/lcm:** take coordinatewise minima/maxima for the bank part, but still compute gcd/lcm of the exact cofactors;
- **exact division:** subtract exponents only after checking nonnegativity, and perform exact cofactor division/cancellation; bank data alone does not settle divisibility outside `S`;
- **comparison:** signs and valuation vectors do not order magnitudes; reconstruction, logarithmic bounds with proof fallbacks, or another magnitude index is required;
- **addition/subtraction:** common bank factors can be extracted using coordinatewise minima, but the residual sum can acquire new divisibility by any bank prime and therefore must be normalized or returned as an unchecked/magnitude-backed result.

**[ESTABLISHED MATHEMATICS]** If `a=p^r u` and `b=p^s v`, then `v_p(a+b) >= min(r,s)`, with equality when `r != s`; equal valuations can produce additional cancellation. This makes an “addition lower bound” cheap but does not generally provide the exact post-addition valuation.

### D. Receipts must keep four costs distinct

**[ENGINEERING INFERENCE]** Each workload should separately report:

1. **ingress:** validation, constant-prime stripping, exponent overflow handling, allocation, and certificate checks;
2. **native operation:** exponent/cofactor manipulation while values remain banked;
3. **maintenance:** normalization, bank migration, cache lookup/admission/eviction, and invalidation;
4. **egress:** reconstruction, magnitude comparison, formatting, or transfer to an ordinary arithmetic library.

Amortized wins are meaningful only when the object remains resident long enough to repay ingress and metadata traffic. A cache-hit result must still disclose how the cache was built.

### E. Baselines that would understate prior art

Do not use only these weak baselines:

- repeated `factorint` for every operation instead of bounded stripping;
- naïve factorial/binomial multiplication instead of GMP's specialized algorithms;
- naïve rational normalization instead of cross-GCD or factored cancellation;
- generic variable division for each fixed-prime probe;
- a full prime-exponent vector that omits the exact residual;
- a sparse factor list without charging lookup, merge, and canonicalization.

Required serious comparators are PARI bounded stripping/factored form, FLINT bounded partial factorization, FriCAS partial factored values where practical, GMP optimized magnitude arithmetic, and an exact-magnitude-plus-lazy-cache design resembling OpenJDK.

## Novelty ceiling

### Already established or directly implemented

**[NEGATIVE RESULT]** Build 001 must not claim novelty for:

- a finite prime-valuation vector;
- the `S`-part plus exact `S`-free cofactor decomposition;
- selected-prime trial stripping and recovery of an exact cofactor;
- a signed base/exponent factor vector with an unresolved exact final cofactor and completeness result;
- structural multiplication/powering of factored values;
- arbitrary or composite opaque factors inside an exact factored product;
- partially factored exact values with graded factor-status knowledge;
- explicit reconstruction/expansion boundaries;
- single-prime `p^v u` canonicalization;
- exact magnitude plus a lazy valuation cache;
- none/smooth/full factorization policies;
- bounded factor caches, validation, and eviction;
- factor bases with useful residual large primes;
- valuation-aware rational cancellation;
- invariant-divisor multiplication/shift implementations;
- workload-selected prime bases in factorization algorithms.

### Narrow combination not located in this search

**[OPEN QUESTION]** This search did not locate one system that simultaneously exposes all of the following as a general exact-integer machine contract:

- fixed-address lanes for several chosen primes;
- a mandatory exact `S`-free cofactor;
- explicit bank identity and exact migration;
- per-lane evidence/provenance states tied to a canonical-vs-unchecked type boundary;
- operation-specific preservation and invalidation rules;
- instruction-level receipts separating ingress, native, maintenance, and egress work;
- measured static/configurable/adaptive bank policies over both favorable and adversarial workloads;
- a shared low-level cost model against an ordinary binary lineage.

That is an integration/evaluation gap, not evidence of a new mathematical foundation. It may still be useful software research.

### Strongest falsifiable Build 001 question

**[OPEN QUESTION]** Does a bounded bank retain enough locality across a real operation sequence to beat both (a) optimized magnitude arithmetic and (b) exact magnitude with lazy factor/valuation caches after every conversion, metadata access, cofactor operation, and policy transition is charged?

A negative answer would be a successful Build 001 result. A local positive answer should be scoped to the exact workload, bank, widths, cache state, and implementation.

## Ranked source register

The first column also gives a stable citation key for experiment notes.

| Key / rank | Source type | Exact source | Claim supported | Important limit |
|---|---|---|---|---|
| `PARI-LIB-1` / 1 | Official manual | PARI/GP, libPARI interface, `Z_smoothen`, `Z_factor_limit`: <https://pari.math.u-bordeaux.fr/dochtml/html-stable/usersch5.html> | Explicit-list or bounded small-prime extraction returns exponent data and exact cofactor | API operation, not fixed-bank object semantics |
| `PARI-FAMAT-2` / 1 | Official manual | PARI/GP, “Elements in factored form,” including `famat` and `ZM_famat_limit`: <https://pari.math.u-bordeaux.fr/dochtml/html-stable/usersch6.html> | Persistent exact factored products, structural operations, and folding large factors into an opaque entry | General factor list, not lane-addressed bank |
| `PARI-FACTOR-3` / 1 | Official manual | PARI/GP arithmetic functions, `factorint` and `factorback`: <https://pari.math.u-bordeaux.fr/dochtml/html/Arithmetic_functions.html> | Factor-status/probable-prime qualifications and explicit reconstruction | General factoring can be much more work than bank ingress |
| `FLINT-FACTOR-1` / 2 | Official manual and pinned source | FLINT 3.7.0-dev, `fmpz_factor.h`: <https://flintlib.org/doc/fmpz_factor.html>; documentation source at revision `4ecb9d43a32722564c938c759c9850b3e6ddb206`: <https://github.com/flintlib/flint/blob/4ecb9d43a32722564c938c759c9850b3e6ddb206/doc/source/fmpz_factor.rst> | Signed factor vectors; bounded/range trial factors; exact final cofactor; smooth partial factor status; refine and expand | Factorization result, not fixed-bank arithmetic value |
| `FRICAS-API-1` / 3 | Official API | FriCAS `Factored R`: <https://fricas.github.io/api/Factored.html> | Unit, bases, exponents, knowledge flags, exact incomplete factorizations | Current generated docs can drift; accessed 2026-08-24 |
| `FRICAS-BOOK-2` / 3 | Official user guide | *FriCAS User Guide*: <https://fricas.org/book.pdf> | User-level semantics of factored values and explicit expansion | CAS-level behavior, not hardware cost evidence |
| `SAGE-FACT-1` / 3 | Official manual | SageMath `Factorization`: <https://doc.sagemath.org/html/en/reference/structure/sage/structure/factorization.html> | Immutable unit plus factor/exponent sequence and exact `value()` reconstruction | Primarily a generic result/container type |
| `MAGMA-FACT-1` / 3 | Official manual | Magma Handbook, integer factorization: <https://docs.magma-maths.org/BasicRings/Integers/factor.html> | Dedicated factorization-sequence type, separated unresolved composites, stored factors, gcd/exact-division partial factorization | Not a fixed selected-prime arithmetic value |
| `SPART-1` / 4 | Primary paper | Yann Bugeaud, Jan-Hendrik Evertse, Kálmán Győry, “S-parts of values of univariate polynomials, binary forms and decomposable forms at integral points,” *Acta Arithmetica* 184 (2018), DOI: <https://doi.org/10.4064/aa170828-7-3>; author preprint: <https://arxiv.org/abs/1708.08290> | Established `S`-part terminology | Number-theory paper, not a representation implementation |
| `OPENJDK-V2-1` / 5 | Pinned official source | OpenJDK `BigInteger.java`, revision `9255c103d0fec0c61904a6214053591b91a6c14e`: <https://github.com/openjdk/jdk/blob/9255c103d0fec0c61904a6214053591b91a6c14e/src/java.base/share/classes/java/math/BigInteger.java> | Exact immutable magnitude plus lazy cache of lowest set bit (`v_2` for nonzero input) | Single lane; cache is not authoritative representation |
| `FLINT-PADIC-1` / 5 | Official manual | FLINT 3.7.0-dev, `padic.h`: <https://flintlib.org/doc/padic.html> | Canonical one-prime valuation/unit storage and prime context | Finite-precision p-adic, not exact multi-prime integer |
| `FLINT-CA-2` / 5 | Official manual | FLINT exact algebra, integer-factor options: <https://flintlib.org/doc/ca.html> | None/smooth/full factor policy and cache-context caveat | Policy belongs to CAS simplification context |
| `QS-LP-1` / 6 | Primary paper | Arjen K. Lenstra and Mark S. Manasse, “Factoring with Two Large Primes,” *Mathematics of Computation* 63 (1994), DOI: <https://doi.org/10.2307/2153299> | Factor-base relations with useful residual one/two large primes | Relation collection, not general exact arithmetic |
| `NFS-1` / 6 | Primary chapter | Joe P. Buhler, H. W. Lenstra Jr., Carl Pomerance, “Factoring Integers with the Number Field Sieve,” DOI: <https://doi.org/10.1007/BFb0091539> | Factor-base/smoothness architecture in NFS | Factorization algorithm context |
| `CADO-1` / 6 | Pinned official source | CADO-NFS README, revision `9bb8fc0799bbaaf0b47a1edf573ecf5e0cf8e46a`: <https://github.com/cado-nfs/cado-nfs/blob/9bb8fc0799bbaaf0b47a1edf573ecf5e0cf8e46a/README.md> | Production advice to strip small factors before treating the remaining composite with NFS | Workflow boundary, not value representation |
| `GMP-REMOVE-1` / 7 | Official manual | GMP, number-theoretic functions, `mpz_remove`: <https://gmplib.org/manual/Number-Theoretic-Functions.html> | Exact multiplicity extraction and residual for one factor | One operation/lane |
| `GMP-RAT-2` / 7 | Official manual | GMP rational canonical form: <https://gmplib.org/manual/Rational-Number-Functions.html>; rational internals: <https://gmplib.org/manual/Rational-Internals.html> | Canonical sign/coprimality and cross-GCD cancellation; eager-canonicalization caveat | Does not preserve a valuation bank |
| `GMP-SPECIAL-3` / 7 | Official manual | GMP factorial algorithm: <https://gmplib.org/manual/Factorial-Algorithm.html>; binomial algorithm: <https://gmplib.org/manual/Binomial-Coefficients-Algorithm.html> | Optimized structural magnitude baselines | Algorithm-specific, not general ALU semantics |
| `SYMPY-CACHE-1` / 8 | Official docs and pinned source | SymPy number theory docs: <https://docs.sympy.org/latest/modules/ntheory.html>; `factor_.py` revision `e186eab06dd8119f943391fcad13af48b06d33bc`: <https://github.com/sympy/sympy/blob/e186eab06dd8119f943391fcad13af48b06d33bc/sympy/ntheory/factor_.py> | Validated bounded factor cache, eviction, external source hook | Stores a factor witness, not full valuation/cofactor data |
| `CONSTDIV-1` / 9 | Primary paper | Torbjörn Granlund and Peter L. Montgomery, “Division by Invariant Integers using Multiplication,” PLDI 1994, DOI: <https://doi.org/10.1145/178243.178249> | Invariant integer division via multiply/shift sequences | Software-instruction model; not zero-cost division |
| `CONSTDIV-FPGA-2` / 9 | Primary paper | “Scalable Architecture of Constant Division on FPGA,” IEEE ARITH 2023, DOI: <https://doi.org/10.1109/ARITH58626.2023.00025>; symposium paper: <https://arith2023.arithsymposium.org/papers/Scalable%20Architecture%20of%20Constant%20Division%20on%20FPGA.pdf> | Quotient/remainder constant-divider hardware and synthesis evidence | Device- and implementation-specific results |

## Build-facing recommendations

1. Use **“finite `S`-valuation bank with exact `S`-free cofactor”** in contracts and papers.
2. Implement canonical and unchecked/cache-backed states as different types or unmistakably different variants.
3. Begin with immutable values and `KNOWN`/`UNKNOWN`; add `STALE` only if a testable mutation or bank-version transition requires it.
4. Differential-test ingress against repeated `mpz_remove` or an equivalent oracle and, where feasible, PARI `Z_smoothen` and FLINT bounded-trial behavior.
5. Include exact bank migration operations; do not treat adaptive-bank changes as metadata-only.
6. Benchmark at least three serious competitors: optimized magnitude, magnitude plus lazy valuation/factor cache, and persistent factored form.
7. Include a product-resident favorable workload, an addition-heavy hostile workload, an outside-bank-prime workload, and a phase-changing workload that defeats a lagging adaptive policy.
8. Compare rational cancellation against cross-GCD and factored-CAS baselines, including shared factors outside the bank.
9. Report constant-prime extraction separately from general division and parameterize it by width/prime/repetition.
10. Frame any positive result as a bounded machine/runtime result. The representation, decomposition, and most individual mechanisms are prior art.
