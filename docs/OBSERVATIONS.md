# Build 000 observations

This ledger classifies claims by evidence type. “Bounded pass” never means universal proof. Raw receipts are under `results/build000/`.

## Established mathematics

1. For nonzero positive integers, unique factorization identifies multiplication with addition in a finitely supported map from ordinary primes to natural exponents. This is the free commutative monoid on the primes. The repository implements a bounded dense and sparse instance; it does not originate the mathematics.
2. In exponent coordinates, exact division is coordinate subtraction when no exponent underflows; divisibility is coordinatewise order; gcd is coordinatewise minimum; lcm is coordinatewise maximum; and positive-integer `1` is the empty/all-zero factor vector.
3. Zero is not a finite-support prime-exponent vector with the same semantics as positive integers. A machine that maps both zero and one to an empty factor map is not injective; an explicit zero tag repairs that collision.
4. Signed integers require a unit/sign component. Positive rationals are naturally modeled by integer-valued exponent coordinates, but zero remains exceptional.
5. Prime-exponent coordinates do not determine ordinary addition when prime atoms are anonymous. Permuting generators preserves the free commutative monoid but not ordinary sums. An evaluation map, magnitude/order structure, successor, or independently specified addition is additional structure.
6. For a selected prime `p`, `v_p(a+b) >= min(v_p(a),v_p(b))`; unequal valuations determine equality in that bound. Thus ordinary addition can preserve a common known factor while still generating an unrelated residual cofactor.
7. A system with `M` distinguishable semantic states needs at least `ceil(log2 M)` independent binary payload bits unless identity is carried by another charged resource such as location, timing, or wiring.
8. Exact encodings do not make an operation uniquely information-losing. A binary operation is many-to-one when operands are discarded; fixed-positive-operand addition and multiplication are both injective.

## Historical observation

1. Quantity did not historically require a binary numeral string. Marks, counters, abacus columns, digit wheels, pulse counts, relay states, and electronic ring counters all separated physical state from numeric interpretation.
2. Positional value existed in spatial devices before modern machine words. Radix was contingent: sexagesimal, decimal, and binary systems were all workable under different material and human constraints.
3. Mechanical carry was a concrete engineering burden. Babbage considered alternative radices, including binary, and selected decimal in relation to mechanism and familiarity rather than because decimal or binary was physically inevitable.
4. The Difference Engine simplified its machine by choosing a finite-difference workload that reduced multiplication to repeated addition. Workload-specific architecture preceded general ALUs.
5. Punched media first served control and record selection as well as numeric data. A two-state physical mark did not force a binary-number interpretation.
6. Boolean algebra and relay/electrical switching were separate lineages before Shannon's 1938 synthesis. Shannon showed an extraordinarily productive correspondence, not that Boolean gates and binary magnitude are one indivisible primitive.
7. Binary relay/mechanical machines and decimal electronic machines coexisted. Medium did not uniquely determine radix.
8. Accumulators in machines such as ENIAC fused storage and arithmetic before a clean modern register/ALU boundary stabilized.
9. Cards, tape, and plugboards supported programmable control before instructions and data shared stored-program memory.

The cited evidence, source cautions, and contested “first” claims are in `docs/HISTORY_AND_PRIOR_ART.md`.

## Engineering observation

1. The earliest coherent fork in this build is after stable state, switching, identity, memory, and counters, but before committing all numeric data to positional magnitude. Prime identity cannot coherently sit below distinguishability or stable identity.
2. Binary is not refuted. The experimental prime machine uses ordinary two-state cells and binary exponent counters naturally. What changes is the data structure immediately above registers.
3. A dense lane can make generator identity implicit in wiring, but that moves identity cost into the configured lane layout and prime catalogue. A sparse form makes the cost visible as indices and routing.
4. `COMPOSE` is not “free multiplication.” In this implementation it is a parallel bank of ripple-carry exponent adders followed by a balanced overflow-status reduction. The modeled 16-lane, 8-bit case evaluates 1,965 NAND gates per compose operation (`16 x 8 x 15 + 3 x 15`); generated receipts, rather than notation, control each reported count.
5. Dense coordinates are plausible for a small fixed smooth-number domain and implausible as a universal integer store. Covering every input through `B` requires every prime through `B`, while binary width grows only logarithmically in `B`.
6. Sparse coordinates address dense empty-lane waste but replace it with key comparisons, exponent additions, entry writes, capacity, and locality costs. Their local operation is a merge rather than a flat bank.
7. Exponents reintroduce quantity arithmetic. Unary exponent tokens expand storage; binary exponent lanes reintroduce adders and carry; bit-plane/Fermi-Dirac variants relocate the same counter structure.
8. Ordinary addition is best treated as a typed domain crossing or a partially structured result. Common-factor extraction can remain local; the residual sum generally needs magnitude, residues/unit data, a lazy expression, or new factorization.
9. Numeric equality and divisibility are native to canonical coordinates. Numeric order is global and cannot be replaced by componentwise exponent order.
10. The useful architecture suggested by Build 000 is multi-domain: ordinary magnitude plus verified factor/valuation sidecars and explicit validity/crossing receipts. It is closer to a coprocessor or intermediate representation than a replacement CPU foundation.

## Experimental result

### Correctness

1. `dotnet test PrimeAxiom.sln --configuration Release --no-build` passed 35/35 xUnit tests on the recorded Build 000 run; `results/build000/test-summary.json` is the sanitized receipt and the identity-bearing raw TRX is ignored.
2. The independent experiment receipt passed 26,764 checks with zero failures. Its exact domain is 4 NAND rows; every 4-bit ordered pair for add/subtract/compare/multiply; prime round-trip `1..128`; every pair `1..64 x 1..64` for compose/gcd/lcm/divides/cancel; and 5,000 deterministic random composition trials with seed `99536896`.
3. These results establish implementation equivalence only on those finite domains. They do not prove unbounded arithmetic correctness or physical implementation properties.

### Matched-domain gate sweep

The dense prime machine was charged enough lanes to represent every input through each bound. Selected rows:

| Maximum input | Binary input bits | Binary multiplier NAND evaluations | Prime lanes | Exponent bits/lane | Dense payload bits | Prime compose NAND evaluations |
|---:|---:|---:|---:|---:|---:|---:|
| 16 | 5 | 800 | 6 | 4 | 24 | 375 |
| 128 | 8 | 2,048 | 31 | 4 | 124 | 1,950 |
| 256 | 9 | 2,592 | 54 | 5 | 270 | 4,209 |
| 1,024 | 11 | 3,872 | 172 | 5 | 860 | 13,413 |
| 4,096 | 13 | 5,408 | 564 | 5 | 2,820 | 43,989 |

The transparent dense coordinate circuit used fewer modeled NAND evaluations through the tested bound 128, then lost from 256 onward. It used far more resident payload throughout and additionally required a catalogue. This is a model comparison, not a technology-independent crossover point.

### Factor-resident workload

Across 64 seeded pairs born as coordinates over the first eight primes:

- coordinate composition used fewer modeled NAND evaluations in all 64 cases;
- median binary multiplication work was 59,168 NAND evaluations;
- prime composition, including overflow-status reduction, was a fixed 981 NAND evaluations;
- median payload for two binary operands was 86 bits;
- payload for two dense coordinate operands was 128 bits.

This is the strongest positive result: when factor structure is already resident and remains resident, multiplication-like work can trade extra state for much less local logical work and shallow lane-parallel depth.

### Representation cases

With 25 dense lanes (primes through 97) and 8-bit exponents:

- every dense value used 200 payload bits regardless of support;
- `12` used 4 binary bits, 200 dense bits, or an estimated 31 sparse bits including the declared index/exponent/length fields;
- `30,030` used 15 binary bits, 200 dense bits, or an estimated 83 sparse bits;
- primes 101, 99,991, and 104,729 returned `BasisEscape` rather than being mislabeled or truncated.

The sparse estimates omit allocator/alignment overhead, so they are lower-level payload estimates, not managed-memory measurements.

### Addition structure

Across 256 seeded additions in `1..256`:

- product support differed from the union of operand support in 0 cases;
- only 4 sums preserved exactly that support set;
- the mean support symmetric difference for addition was 4.488 lanes and the maximum was 7;
- trial refactoring tested a mean of 99.859 prime remainders under the covering basis.

This measures support reorganization and the trial-division implementation. It is not evidence that every addition is hard or equivalent to general factoring.

### Preimages and information

For 4,096 ordered pairs in `1..64 x 1..64`:

- addition produced 127 distinct outputs, with maximum preimage size 64;
- multiplication/compose produced 1,263 distinct outputs, with maximum preimage size 14;
- fixing the positive right operand left 64 distinct outputs for both operations.

Both operations become many-to-one when the pair is discarded. Addition is more collision-heavy on this domain, but “information loss” is not a unique property of factor coordinates.

### Managed runtime

On the manifest host, the transparent managed gate and coordinate models ran at microsecond scale while the two optimized host `BigInteger` controls ran below one microsecond. Exact medians, spread, iteration counts, and trial counts are kept in `results/build000/microbenchmarks.csv`; they are deliberately not frozen into this prose because reruns vary. The operands and abstraction levels are not all matched, so these timings are implementation evidence and profiling controls, not a hardware or architectural performance claim.

## Conjecture

1. A bounded valuation bank plus exact binary cofactor can retain useful small-prime structure for arbitrary integers without the dishonest failure mode of a closed finite basis.
2. A hybrid value with independently valid magnitude, factor certificate, selected valuations, and residues may outperform either pure representation on mixed workloads if invalidation and storage costs are controlled.
3. A factor-expression DAG may postpone additive refactorization usefully for product-heavy symbolic workloads, but equality/order checkpoints and memory growth will bound the win.
4. Selected p-adic unit residues may predict more of `v_p(a+b)` than exponents alone in equal-valuation cases, making a small sidecar useful even when full factorization is absent.
5. A synthesized exponent bank may retain a latency advantage for small fixed bases, but routing and area could erase the logical-depth result.

## Dead end

1. **Prime identity below distinction.** A number-theoretic prime requires an arithmetic structure; an opaque generator requires at least persistent identity. Neither can be the pre-distinction physical floor of this experiment.
2. **Dense universal prime memory.** Lane count grows as the number of primes in the supported magnitude interval. The matched-domain sweep shows the local-compose win being consumed rapidly by lane state and logic.
3. **Prime index as a free primitive.** An index requires ordering, an address namespace, and usually a table or ranking operation. Wiring can amortize that state but cannot erase it.
4. **Notation as performance.** Writing `12 = 2^2 * 3` does not pay factorization, exponent storage, identity, or reconstruction.
5. **Zero equals the empty vector.** That collides with one and breaks injectivity. The implementation uses a tag.
6. **Addition is wrong or intrinsically destructive.** Addition is ordinary arithmetic with different locality in this representation. Both addition and multiplication discard operand provenance if used irreversibly.
7. **A high-level factor library as a new floor.** It would test an API, not earn the substrate. Build 000 instead starts at explicit state and NAND-derived circuits.
8. **Gödel coding, RNS, LNS, or prime moduli as proof of novelty.** They are important controls and prior art, but none by itself establishes a canonical general-purpose prime-factor machine.

## Open question

1. What is the Pareto frontier after synthesizing a parameterized exponent bank and matched binary multipliers to one FPGA or standard-cell library?
2. Which real workloads keep values factor-resident long enough to amortize certificate construction: exact rational cancellation, factorial/binomial products, smooth-number sieves, divisibility filters, symbolic monomials, or something else?
3. How should a `basis exponents + exact cofactor` type preserve canonicality, partial factor certificates, and gcd semantics when the cofactor is composite?
4. Can `ADD_SPLIT` preserve a common factor and return a typed residual expression that is useful before normalization?
5. Does a selected residue/p-adic sidecar materially reduce additive crossings, and at what storage/update cost?
6. Can certified logarithm intervals compare large compact products efficiently on realistic adversarial cases without reconstruction?
7. How do sparse layouts, small-vector optimization, allocation, and cache behavior change the result beyond payload-bit estimates?
8. What reversible embedding is useful when operands or provenance must be retained, and what ancilla/cleanup cost does it require?

---

# Build 001 observations

Build 001 receipts are under `results/build001/`. Their coverage is `PILOT_SUBSET_COMPLETE_FULL_CONFIRMATION_NOT_RUN`; every statement below inherits that ceiling unless a narrower domain is named.

## Established mathematics

1. For a finite set of primes `S`, every nonzero integer has a unique sign, exact `S`-part, and positive `S`-free cofactor. Build 001 implements this established decomposition; it does not originate it.
2. For a prime `p`, `v_p(a+b) >= min(v_p(a), v_p(b))`; the minimum is exact when the two finite valuations differ. Equal valuations can cancel and raise the result valuation.
3. Multiplication adds exact valuations, powers scale them, exact division subtracts them when defined, and the bank portions of gcd/lcm take minima/maxima. These local rules do not remove corresponding cofactor operations.
4. A certified lower bound plus an exact residual still denotes an exact integer. It is incomplete knowledge of decomposition, not approximate magnitude.
5. Zero has infinite `p`-adic valuation for every prime and requires an explicit result kind; reporting finite zero would conflate it with an ordinary nonzero valuation.

## Prior art

1. PARI/GP selected-prime stripping and factored matrices, FLINT partial factor vectors, and FriCAS exact partially factored values closely anticipate the bank/exponent/cofactor mechanism.
2. Factor bases, GMP factor removal and rational cross-GCD, OpenJDK's lazy `v_2` cache, and bounded factor caches already separate preserved factor knowledge from authoritative magnitude in specialized contexts.
3. Mature prior art treats exact residuals and factor-certification status separately. A composite or unresolved cofactor is not a failed value and must not be called prime merely because it is stored as one field.

## Engineering observation

1. A bounded bank is coherent immediately above ordinary binary numeric representation. It is neither a pre-binary primitive nor a total replacement for magnitude arithmetic.
2. The exact cofactor removes finite-basis escape but retains the full general-arithmetic burden. Hybrid multiplication pays exponent work *and* a cofactor multiplication unless the cofactor is one.
3. Canonical and partial valuation knowledge can coexist in one immutable exact type if lower-bound lanes permit additional bank factors to remain in the cofactor and exact-only operations refuse or refresh partial inputs.
4. Addition can preserve proved common bank factors without claiming a complete factorization. Wider dense banks create many equal-zero lanes whose exact status becomes deferred after addition.
5. Bank identity is semantic configuration. Migration is exact only when evicted powers are folded into every affected cofactor and admitted primes are extracted; changing a global bank is therefore potentially working-set-wide maintenance.
6. An adaptive policy cannot discover a hidden outside-bank factor from magnitude input without charged computation. Treating such discovery as a cache hit would hide ingress/factorization work.
7. Phase-separated heterogeneous receipts are necessary. NAND evaluations, remainders, divisions, cofactor operations, lane traffic, metadata, storage, migrations, and time do not form a universal scalar cost without an externally justified model.
8. VM failure must be transactional. Invalidating a failed destination and clearing query outputs prevents an earlier successful value from masquerading as the failed instruction's result.
9. Valid structured values can request impractically large reconstructions because exponent and cofactor magnitude are not bounded by an output-resource policy. Reconstruction remains an explicit resource-sensitive boundary.

## Experimental result

1. The recorded Release test assembly passed 89/89 tests with zero skipped. The independent experiment executed 39,621 bounded checks with zero failures: exhaustive signed ingress, exhaustive ordered small signed pairs, 5,000 seeded random trials, and malformed/zero-valuation probes.
2. In 1,000 seeded additions, partial outputs occurred for 975/1,000 values at bank 4 and every value at banks 8, 16, and 32. Lower-bound lanes grew from 2,186/4,000 at bank 4 to 28,900/32,000 at bank 32.
3. Eagerly refreshing those additions required 3,513, 7,251, 15,035, and 30,868 remainder probes at banks 4, 8, 16, and 32. On the manifest host, the checked timing receipt placed bank-8 lazy addition below eager normalization; exact variable timings remain in `microbenchmarks.csv`.
4. The deterministic `K+1` LRU attack migrated 16 live values 320 times at `K=4` and 2,112 times at `K=32`; lane reads and writes grew from 1,280 each to 67,584 each. Exactness failures remained zero.
5. A favorable resident-product proxy used fewer modeled NAND evaluations than binary at every registered `Q` from 1 through 4,096. At `K=4,Q=1`, it used 1,001 versus 10,368. The hybrid two-operand payload was 174 versus 36 bits. This cross-profile diagnostic is not a complete frozen Pareto profile and cannot apply the registered 2x rule.
6. On the recorded managed host, both bank-8 hybrid multiplication probes were substantially slower than the rough `BigInteger` multiplication control. Exact variable timings remain in `microbenchmarks.csv`; these unmatched implementation timings establish no runtime win and no hardware conclusion.
7. Workload rows retained 115 bounded passes, 77 not-supported controls, and two expected Build 000 dense-basis escapes across five pilot families. The full sparse and true Build 000 sparse-operation comparators remain unsupported rather than receiving relabeled oracle/dense costs. These are pilot mechanism rows, not the frozen full confirmation matrix.
8. Build 001 status is `PARTIAL — PILOT_NEGATIVE; FINAL DECISION NOT EARNED`. Complete profiles, registered correctness scale, multiple widths, eight replicates, per-cell schemas, and two-family confirmation were not run, so the frozen stop rule and every terminal decision label remain open.

## Conjecture

1. A sparse per-value cache of demanded valuation certificates may preserve most useful reuse without the dense zero-lane payload and global migration debt of a fixed bank.
2. A tiny fixed-prime valuation or constant-divider accelerator may help a query-heavy workload if trace evidence demonstrates enough reuse to amortize ingress, area, and transfer.
3. Structured producers that already emit verified factors may offer better economics than magnitude ingress, but only against mature persistent-factored and optimized magnitude baselines.

## Dead end

1. **Bounded bank as a general exact-integer replacement.** The cofactor remains general magnitude, while dense lanes add storage and maintenance. The tested architecture paid both systems' costs.
2. **More lanes automatically preserve more useful structure.** Under addition, wider banks mostly accumulated uncertain zero-valuation lanes and substantially more refresh work.
3. **Adaptive membership as free locality.** Hidden factors require discovery, and changing membership across a live set caused exact but rapidly growing migration traffic.
4. **Resident NAND proxy as an end-to-end advantage.** The favorable proxy omitted several heterogeneous boundary costs and was paired only with a separate logical-input payload diagnostic, not a complete eligible profile.
5. **Representation novelty.** The finite `S`-decomposition, exact cofactor, partial factor knowledge, structural product arithmetic, factor bases, and valuation caches are established prior art.

## Open question

1. Does sparse demand-driven valuation evidence beat no-cache magnitude arithmetic, mature factored forms, and lazy single-prime caches on real rational-cancellation or divisibility-filter workloads?
2. Can an immutable exact magnitude plus stable per-value certificates eliminate global bank migration while preserving useful evidence through multiplication and addition?
3. What is the measured Pareto frontier against GMP, PARI, FLINT, and FriCAS when source modes, native memory, allocation, output obligations, and factor validation are matched?
4. Which certificate replacement policy survives phase shifts without inspecting hidden factor structure for free?
5. Does synthesized fixed-prime valuation hardware retain any area/latency advantage after divider, routing, register, control, and transfer costs are included?
6. What bounded reconstruction/resource policy rejects denial-of-service-sized structured values without narrowing ordinary exact-integer semantics silently?
