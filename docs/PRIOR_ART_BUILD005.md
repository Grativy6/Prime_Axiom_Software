# Build 005 prior art and baseline boundary

- Protocol: `PAH-BUILD005-DEMAND-VALUATION-0001`
- Frozen plan SHA-256: `8B76649A4D4E7E60B756BCFB5FDA7954385A10A9E6DDD520C97123B845CE9031`
- Sources accessed: **2026-08-30**

## Status and method

This is a bounded review of primary papers, official manuals, and maintained
technical documentation. It is not a systematic review, patent search,
novelty opinion, or implementation result. Failure to locate an architecture
in this search is not evidence that none exists.

Source classes used below:

- **Direct**: implements substantially the same function or retained
  representation boundary.
- **Competent control**: a serious alternative that must be included before a
  performance or attribution claim is credible.
- **Analogy**: informs cache or refinement discipline but does not implement
  Build 005's integer contract.
- **Bounded integration gap**: the exact combination was not located in this
  search; this label carries no novelty claim.

## Result first

The search, factor, and cache ingredients in Build 005 are established prior
art. In particular, Build 005 cannot claim novelty for trailing-zero valuation,
constant-divisor tests, extracting `(exponent, residual)`, fixed-bank trial
division hardware, cached factors, factored-form arithmetic, producer-known
factor propagation, or batch extraction of smooth parts.

The narrow experiment that remains justified is whether one bounded service
can retain exact, resumable per-prime frontiers under mutable value slots and
repay all lookup, search, state, invalidation, propagation, transfer, and output
costs. This review did not locate the same combination of:

```text
(slot, generation, prime-index, lower bound, residual, terminal status)
+ bounded K-entry replacement
+ negative terminal receipts
+ threshold-limited refinement and exact resumption
+ transactional mutation and stale-tag rejection
+ exact prime-only multiplication propagation
+ end-to-end static and dynamic repayment accounting
```

That is a **bounded integration gap**, not a claim that the combination is new.
Its components are individually close to mature systems, and the frozen
controls are intended to measure whether integration adds anything beyond
those components.

## Ranked register

| Rank | Class | Closest source | Constraint on Build 005 |
|---:|---|---|---|
| 1 | **Direct hardware** | Gabriel Southern, Chris Mason, Lalitha Chikkam, Patrick Baier, and Kris Gaj, [“FPGA Implementation of High Throughput Circuit for Trial Division by Small Primes”](https://people-ece.vse.gmu.edu/~kgaj/publications/conferences/GMU_SHARCS_2007.pdf), SHARCS, September 2007; [workshop record](https://www.hyperelliptic.org/tanja/SHARCS/talks07/record.pdf) | A fixed prime bank, high-throughput divisibility pipeline, slower quotient-producing extraction, prime/exponent outputs, and residual quotient already exist in FPGA work. Build 005's valuation datapath and catalogue are not novel. |
| 2 | **Direct factor-form computation** | Magma [Factorization Sequences](https://docs.magma-maths.org/BasicRings/Integers/factorization-sequence.html), V2.29, 10 June 2026; PARI/GP [Arithmetic functions](https://pari.math.u-bordeaux.fr/dochtml/ref/Arithmetic_functions.html); FLINT [`fmpz_factor`](https://flintlib.org/doc/fmpz_factor.html) | Mature systems preserve prime/exponent form, reconstruct on demand, and perform operations without refactoring already factored products. `PRODUCER_GENERATED` must therefore be compared with direct factored propagation, not forced rediscovery. |
| 3 | **Direct query semantics / competent software control** | GNU MP 6.3.0 [`mpz_remove`](https://gmplib.org/manual/Number-Theoretic-Functions), [small-integer divisibility guidance](https://gmplib.org/manual/Efficiency), and [exact division](https://gmplib.org/manual/Exact-Division.html) | Exact multiplicity plus terminal residual, predicate-only testing, grouped screening, and quotient-on-hit are established baselines. A naive repeated `%` loop is not `BIN_DIRECT_BEST`. |
| 4 | **Direct generic reuse** | SymPy 1.14 [`FactorCache`](https://docs.sympy.org/latest/modules/ntheory.html), released 27 April 2025; PARI/GP [`getcache`](https://pari.math.u-bordeaux.fr/dochtml/html-stable/Programming_in_GP__other_specific_functions.html); Magma [Factorization, “Storing Potential Factors”](https://docs.magma-maths.org/BasicRings/Integers/factor.html) | Reusing factors and completed factorizations is established. A repeated-value win is generic memoization unless it survives the content-answer-cache control. |
| 5 | **Direct radix primitive** | RISC-V [“B” Extension for Bit Manipulation, Version 1.0.0](https://docs.riscv.org/reference/isa/unpriv/b-st-ext.html), Zbb `ctz`, v1.0 ratified | For nonzero binary magnitude, `ctz(n) = v_2(n)`. This is a radix-2 result and must remain isolated from odd-prime conclusions. |
| 6 | **Competent fixed-divisor control** | Torbjörn Granlund and Peter L. Montgomery, [“Division by Invariant Integers Using Multiplication”](https://gmplib.org/~tege/divcnst-pldi94.pdf), 1 June 1994; Daniel Lemire, Owen Kaser, and Nathan Kurz, [“Faster Remainder by Direct Computation: Applications to Compilers and Software Libraries”](https://arxiv.org/abs/1902.01961), first published 27 February 2019; H. Fatih Ugurdag, Florent de Dinechin, Y. Serhan Gener, Sezer Gören, and Laurent-Stéphane Didier, [“Hardware Division by Small Integer Constants”](https://perso.citi-lab.fr/fdedinec/recherche/publis/2017-TC-ConstDiv.pdf), December 2017 | Fixed and repeated divisors admit reciprocal-multiply, direct-remainder, predicate-only, table, and specialized hardware techniques. These apply to primes and composites; shared savings are not prime-specific. |
| 7 | **Competent batch control** | Daniel J. Bernstein, [“How to Find Smooth Parts of Integers”](https://cr.yp.to/factorization/smoothparts-20040510.pdf), draft dated 10 May 2004; author-maintained [batch trial division explanation](https://facthacks.cr.yp.to/batchtrial.html) | Product/remainder-tree methods can extract a fixed factor base across many values more efficiently than independent per-value trial division. Build 005 cannot generalize a sequential-service result to batch workloads. |
| 8 | **Checkpointing analogy only** | Christopher Doris, [“Exact p-adic Computation in Magma”](https://doi.org/10.1016/j.jsc.2020.08.005), *Journal of Symbolic Computation* 104 (May-June 2021), 476-493; [arXiv version](https://arxiv.org/abs/2008.11063), 24 August 2020 | Lazy dependency graphs, cached increasingly precise approximations, epochs, and consistency checks demonstrate mature demand-refinement design. This is not an integer valuation cache and gives no direct prime-hardware claim. |

## Separate attribution boundaries

### 1. `p = 2`: radix-aware extraction

The RISC-V Zbb specification defines `ctz` as a ratified bit-manipulation
instruction. For a nonzero binary word, the count of trailing zero bits is
exactly the exponent of two in the represented integer. Shifting right by that
count yields the odd residual. Zero needs its own contract because an ordinary
finite valuation is undefined there; Build 005's `infinite=true` state is a
semantic choice above the instruction.

This advantage comes from positional radix. It does not show that hardware has
found odd prime identity. Accordingly, every `RADIX_V2` row and any
`RADIX_V2_ONLY` result must remain separate from odd-prime search and
propagation.

### 2. Constant-divisor and small-prime extraction

Granlund and Montgomery give multiplication-based code sequences for constant
or run-time-invariant integer divisors, including exact division and remainder
tests. Lemire, Kaser, and Kurz derive direct fixed-divisor remainder and
divisibility tests that can avoid first producing a quotient. Ugurdag et al.
compare custom quotient, remainder, and quotient-plus-remainder circuits for
small positive constants and explicitly report different relevance domains for
FPGA and ASIC targets.

Southern et al. are the closest direct hardware predecessor. Their circuit
stores 9,592 primes below 100,000 in ROM, pipelines fixed-dividend divisibility
tests, queues hits, then uses a sequential quotient-producing divider to obtain
each exponent and remaining quotient. Once full, the predicate pipeline tests
the next prime each cycle; exponent extraction remains the slower path. The
reported interface includes factor, power, quotient, and overflow outputs.

The implication is strict: a prime catalogue does not make the division
primitive prime-specific. Build 005 must pair prime divisors with similarly
sized composite divisors. A saving seen for both belongs to constant-divisor
specialization, not prime identity. Predicate-only rows also require a direct
predicate baseline rather than paying for an unused quotient.

GNU MP's official efficiency guidance sharpens the software control:
`mpz_divisible_ui_p` is preferred for one small divisor; several small divisors
should be screened through one remainder modulo their product; and a quotient
needed only after a hit can then use exact division. Its `mpz_remove` operation
directly returns how many copies of a supplied factor were removed and the
terminal residual. These semantics are the appropriate controls for
`PREDICATE_ONLY` and `EXPONENT_AND_RESIDUAL`, respectively.

### 3. Generic memoization and checkpointing

SymPy 1.14's `FactorCache` is a concrete content-keyed cache: `factorint`
automatically consults and populates it, and users may add validated prime
factors. PARI permits an integer to be passed as its factorization or as
`[N, factorization]`, so multiple arithmetic functions need factor `N` only
once; it also reports an automatically growing cache of small-integer
factorizations. Magma has retained a list of ECM/MPQS-discovered factors since
V2.14 (October 2007) and tries those factors in later factorizations.

These systems do not match Build 005's exact per-`(slot,generation,p)` partial
frontier. SymPy caches a factor of immutable integer content, PARI reuses a
completed supplied factorization or range cache, and Magma stores factor
candidates rather than one value's division frontier. They nonetheless make
generic reuse the default explanation for repeated-value savings.

Doris's exact p-adic system is a useful but deliberately weaker analogy. It
keeps a dependency graph, caches successively refined approximations, and uses
epochs and consistency checks so later demands can reuse prior work. That
supports the engineering legitimacy of exact resumable checkpoints; it does
not establish the integer frontier contract or prime-specific propagation.

Therefore the frozen `BIN_CONTENT_ANSWER_LRU_K` and
`BIN_FRONTIER_NOPROP_K` controls are essential. The first isolates ordinary
memoization of completed answers; the second isolates generic resumption of
partial work. Only a strict saving beyond both can be attributed to
prime-certificate propagation.

### 4. Known-prime symbolic and factored computation

Magma's `RngIntEltFact` stores ordered prime/exponent pairs and supports
factor-form arithmetic, exact division, GCD, LCM, predicates, and explicit
reconstruction. Its manual says directly that refactoring a product whose
operands are already factored would be inefficient. PARI accepts factorization
matrices as native inputs to multiplicative arithmetic functions. FLINT's
`fmpz_factor_t` stores bases, exponents, and sign; `fmpz_factor_smooth` also
preserves the distinction between a proved full factorization and a final
possibly composite cofactor.

GNU MP's rational-efficiency guidance makes the same point from the ordinary
magnitude side: applications that know simple factors can sometimes replace
general GCD work with targeted divisibility and exact division. Producer-known
structure is thus already a competent optimization, not evidence that search
paid for itself.

Build 005's `PRODUCER_FACTORED` family should answer only whether a bounded
hardware-like receipt can carry this established advantage through its exact
mutation contract. It cannot support a search-repayment conclusion because no
cold rediscovery was required.

### 5. Batch smooth-parts and adaptive search organization

Bernstein's 2004 algorithm finds the largest factor-base-smooth divisor of each
integer in a batch using product and remainder trees. The result constrains the
scope of every one-value or small-cache comparison: when many values share a
factor base, per-value trial division is not the only competent organization.
The frozen plan correctly states that omitting a full Bernstein implementation
limits any wider batch claim.

Magma's general factorization engine also stages different algorithms and
retries stored factors; FLINT exposes bounded smooth factoring with an explicit
possibly-composite remainder; Southern's hardware separates cheap dense
screening from expensive extraction. Together these sources show that search
policy and requested output matter independently of prime representation.

The implication for `BIN_PRIME_FRONTIER_SPEC_B_K` is an inference from the
sources and protocol, not a source claim: scouting small primes in order may be
sensible under a known distribution, but unused tests, stalls, cache pollution,
and search-to-first-use distance remain real costs. Speculation earns a result
only by beating the demand-only policy after those costs are charged.

### 6. Prime-specific terminal propagation

The mathematical law being isolated is established, not new. Mathlib's
[`Nat.Prime.dvd_mul`](https://leanprover-community.github.io/mathlib4_docs/Mathlib/Data/Nat/Prime/Defs.html)
formalizes Euclid's lemma:

```text
p prime => (p divides a*b iff p divides a or p divides b)
```

Its natural-number factorization library also formalizes product valuation and
residual-complement laws in
[`Mathlib.Data.Nat.Factorization.Basic`](https://leanprover-community.github.io/mathlib4_docs/Mathlib/Data/Nat/Factorization/Basic.html).
Consequently, if two exact terminal prime receipts say

```text
n1 = p^e1 * R1,  p does not divide R1
n2 = p^e2 * R2,  p does not divide R2
```

then their product has exact terminal receipt

```text
n1*n2 = p^(e1+e2) * (R1*R2),  p does not divide R1*R2.
```

The terminal inference is genuinely prime-specific. It fails for an arbitrary
composite base: `6` divides neither `2` nor `3`, but divides `2*3`. However,
fully factored systems already exploit the corresponding prime-exponent product
law. Build 005 can at most establish that a *partial, bounded, mutation-aware*
certificate realizes a useful local propagation saving over generic caches on
the frozen traces. It cannot claim the law or factor propagation as new.

## Fair baseline consequences

| Frozen question | Minimum competent baseline | Attribution ceiling |
|---|---|---|
| One `p=2` valuation | `ctz`/scan-one plus shift, with explicit zero handling | Radix-2 only |
| One odd divisibility predicate | Constant-divisor direct predicate | Constant-divisor specialization |
| Several small divisibility predicates | Grouped-product/primorial remainder screen | Grouped screening, not prime cache |
| Exact exponent and residual | `mpz_remove`-equivalent extraction | Extraction semantics, not retained provenance |
| Repeated identical immutable value | Content-plus-divisor answer cache | Generic memoization |
| Increasing threshold queries | Equal-budget resumable frontier without propagation | Generic checkpointing unless candidate is strictly better |
| Many values, one factor base | Grouped/batched screening; Bernstein product/remainder tree bounds wider claims | No universal per-value-search claim |
| Rational cancellation | Binary GCD/exact division plus known-factor specialization | Prime benefit only beyond the known-factor control |
| Producer-known factors | Magma/PARI/FLINT-style factor propagation | Producer provenance only |
| Multiplication of terminal receipts | Equal cache/frontier machinery, with prime and composite paired rows | Prime propagation only for the strict prime-only difference |

All comparisons must preserve the same source regime and output obligation. A
predicate cannot be compared with a residual-producing service as though the
extra output were free; a warm or producer-supplied receipt cannot answer
whether cold search repaid itself; and a static cache cost does not amortize to
zero.

## Claim boundary

This prior art supports the following bounded statement:

> Build 005 is not testing whether valuation, small-prime search, cached
> factors, factored arithmetic, or prime-exponent multiplication are new. It is
> testing whether exact resumable valuation receipts, held in a very small
> versioned cache and propagated only when the prime law justifies it, repay
> their complete end-to-end costs on the frozen workloads.

No positive result may be generalized to unrestricted factorization, arbitrary
prime bases, batch workloads, FPGA/ASIC physical efficiency, or universal
arithmetic advantage. No negative result is an impossibility theorem outside
the frozen widths, catalogue, cache sizes, traces, policies, and output
contracts.
