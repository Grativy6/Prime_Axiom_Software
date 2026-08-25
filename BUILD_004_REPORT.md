# Prime Axiom Software — Build 004 report

Protocol: `PAS-BUILD004-EXACT-LINEAGE-0001`
Frozen plan SHA-256: `2482698A57E857F07DBDEB7103B09EC36317661A0413ABBC2B20FAB7F44B53D1`
Inherited baseline: merged Build 003 commit `31dd150540bac79de3ee5925b44afdb7abaf327a`
Generated-evidence status: **`PARTIAL — FINAL DECISION NOT EARNED`**
Candidate framework status after an external verifier pass: **`BOUNDED_EXACT_LINEAGE_TOOLKIT_VALIDATED`**

## Result

In the bounded exact-software probes, computation preserved multiplicative provenance with useful query semantics—but not as one universal prime number. Practical workload value remains unmeasured.

The architecture that survived the break tests is:

```text
exact payload
+ replaceable active-source projection
+ persistent typed derivation DAG
+ independent evidence/calibration envelope
```

The implemented evidence envelope now content-addresses the lineage DAG root, registry, three independent knowledge axes, and typed source/validity/uncertainty/residual reference digests. Its verifier earns integrity replay only; it does not authenticate an issuer or validate referenced content. Numeric-factor and unit-dimension receipts are separately tested components in this build, not fields in that lineage envelope and not yet one integrated measurement receipt.

The active-source projection can be a prime product, sparse exponent map, dense binary PEV, or ordinary set. Under a valid registry they answer the same unique-membership, overlap, and union questions. The PEV is a direct bounded-universe software representation. Some topology-preserving receipt is irreducible relative to a scalar output; the persistent typed DAG is the implementation tested here. It retains joint use, alternatives, transforms, replay paths, and retraction semantics that every support-only projection discards.

The strongest positive workload is exact combinatorial probability. A hypergeometric point probability remains a signed prime-exponent ratio constructed from Legendre factorial valuations; no completed result is factored. A tail/event sum is the clean boundary: it requires exact rational addition and a retained additive event receipt at the exact-magnitude crossing. Prime coordinates do not make the sum local.

The strongest application probe is distributed exact-overlap fusion. A PEV-like cache identifies duplicated ancestry with representation-local Boolean operations; no latency claim is made. It does not supply the duplicated likelihood/state. Exact fusion additionally requires the common payload to remain replayable. The hostile case retains an exact aggregate while the overlapping per-atom likelihood is actually absent from both input states; Build 004 makes that eviction, partial lineage, conflicting atom identity, recycled registry epoch, absent external authentication, and missing retraction witness typed failures rather than approximate successes.

## What was built

### Exact combinatorial engine

- canonical arbitrary-precision rational values;
- signed prime-exponent coordinates;
- factorial receipts from Legendre valuations;
- exact binomial receipts;
- exact hypergeometric point and event receipts;
- ordinary `BigInteger` cross-cancel and adjacent-recurrence controls; and
- cold/warm work counters that keep basis construction, cached factorial vectors, reconstruction, and additive work visible.

CLI examples:

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- binomial-receipt 4096 2048
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- hypergeometric-receipt 5000 1200 900 225
```

### Lineage toolkit

- occurrence-scoped atom keys with namespace and assignment epoch;
- injective prime/bit registry validation;
- explicit-set, raw prime-product, sparse-exponent, and dense-PEV representations;
- exact support and multiplicity projections with loss contracts;
- content-addressed `ZERO`, `ONE`, `ATOM`, `JOINT`, `ALTERNATIVE`, and `TRANSFORM` nodes;
- canonical commutative/associative node construction with multiplicity retained;
- replay verification, mutation detection, cycle/dangling-reference rejection, and conservative `DigestOnly` structural cache verification;
- a content-addressed DAG-root/registry evidence-reference envelope with typed source, validity, uncertainty, and residual bindings, explicitly limited to integrity replay;
- specialization/replay retraction; and
- exact positive-rational two-state likelihood fusion against a centralized unique-atom oracle.

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- lineage-demo
```

The demo shows that `a*b+c*d` and `a*c+b*d` have equal support and equal total multiplicities but different derivation roots.

### Boundary probes

- numeric-factor coordinates separated from physical-dimension coordinates;
- evidence/calibration envelopes separated from both;
- exact ratio-scale composition;
- explicit affine, logarithmic, nonlinear, rounded, correlated-uncertainty, and expired-validity crossings;
- exact just-intonation interval composition/inversion and deterministic PCM16 WAV rendering;
- a transparent structural accumulator labeled `NOT_CRYPTOGRAPHIC` and `NO_PRIVACY`; and
- a deliberately small BOM example preserving equal-value/different-lineage and shared-component queries.

```powershell
dotnet run --project src/PrimeAxiom.Cli --configuration Release -- render-just-interval 3 2 220 artifacts/fifth.wav
```

That command renders a separate one-second 48 kHz listening demo. The deterministic campaign artifact described below uses an independently declared 8 kHz/8,000-sample policy.

## Evidence

The committed generator receipt contains **887,072 assertions with zero failures** across the deterministic generated campaign:

| Family | Cases | Checks | Result |
|---|---:|---:|---|
| factorials `0..512` | 513 | 1,026 | pass |
| all binomials through `n=256` | 33,153 | 66,306 | pass |
| all in-support hypergeometric points through `N=24` | 20,475 | 40,950 | pass |
| every distribution normalization through `N=32` | 12,529 | 25,058 | pass |
| seeded valid points through `N=4096` | 10,000 | 10,000 | pass |
| adjacent streams through `N=48` plus `N=2000` | 40,426 | 271,229 | pass |
| named combinatorial controls | 8 | 8 | pass |
| all universe-eight support pairs | 65,536 | 393,216 | pass |
| all four-lane multiplicity pairs with exponents `0..2` | 6,561 | 78,732 | pass |
| DAG and mutation controls | 12 | 12 | pass |
| exact fusion cycles | 2 | 2 | pass |
| seeded asynchronous fusion schedules | 512 | 512 | pass |
| fusion/authentication/retraction boundaries | 9 | 9 | pass |
| calibration/audio/accumulator/BOM probes | 12 | 12 | pass |

Malformed-graph, identity, cache-axis, collision, and semantic regressions that are not enumerated in this generated count remain in the separately executed repository unit suite. The external verifier runs that suite without filters or skipped tests before evaluating generated receipts.

Named large cases include `C(4096,2048)`, hostile `C(100000,1)`, `(5000,1200,900,225)`, and `(1000,413,271,117)`. The ordinary controls do not materialize factorials, so the comparison is not a deliberately weak baseline.

The committed generator cannot award the candidate framework status to itself. It records the complete zero-failure campaign but remains `PARTIAL — FINAL DECISION NOT EARNED`; `scripts/verify-build004.ps1` must independently validate the inventory, frozen values, repository tests, inherited evidence, and two byte-identical external replays for the current checkout.

The deterministic audio artifact is 16,044 bytes, requests the exact ratio `3/2` above exact base `220 Hz`, emits nominal `330 Hz` through a declared binary64 sine/PCM16 policy, clips zero samples, and has SHA-256 `67F75DFB9E57D156BD320DCC8E56F070BFD89C014773366694FFA063C4BFE0B4`.

The verifier runs the whole repository test assembly with zero skips, executes the Build 004 generator twice, compares every byte including the WAV, compares both replays with committed evidence, validates every manifest entry, and protects inherited Build 000-003 evidence:

```powershell
& .\scripts\verify-build004.ps1
```

## Exact combinatorial probability

For a hypergeometric point,

```text
P(X=x) = C(K,x) C(N-K,n-x) / C(N,n)
```

each binomial is a difference of factorial valuation vectors. Combining those vectors yields one signed exponent vector for the exact rational probability. This avoids factor discovery on the completed numerator and denominator.

This is a real local structural advantage after the prime basis and factorial valuations exist. It is not automatically a software speed advantage. The shared `0..5000` prime context required 4,999 candidate visits, 8,087 sieve composite-mark iterations, and retained 669 primes once; it was not double-charged to the point and tail. The named `(5000,1200,900,225)` receipt then used 2,891 coordinate reads, 5,548 writes, 2,222 nonzero existing-coordinate exponent merges, and no rational additions in this implementation. This counter does not claim to count every CLR integer addition. In the ordered named campaign's shared warm-cache state—after the preceding named point and full-event probes—the `P(X>=117)` event for `(1000,413,271)` retained 155 point terms, performed 155 exact rational additions, incurred 116,160 coordinate reads, and performed 83 nontrivial residual reductions after rational addition. That reduction counter is narrower than the separate GCD and exact-division counters. Its serialized receipt was 781,519 bytes versus 16,756 bytes for the named point under the pinned compact-JSON contract; these are current host receipt schemas, not minimum encodings or a performance ranking. The cost did not vanish; it moved into basis construction, coordinate storage, cache work, reconstruction, receipt materialization, and finally addition.

The point/event distinction is useful beyond probability. It mechanically separates structure-preserving multiplicative construction from structure-transforming alternatives.

## PRIMEX-PEV overlap and the open space

PRIMEX-PEV is closely aligned with Build 004's support projection. Its paper replaces impractically large raw prime products with binary PEVs; componentwise min/max implements GCD/LCM ancestry queries. It also describes the crucial second step: after identifying the common information code, the corresponding shared density/state must be stored, reconstructed, or queried. Approximation begins when that shared payload is unavailable.

Build 004 isolates that gap:

```text
overlap identity       exact PEV/set query
shared payload         separate availability/replay obligation
source authenticity    separate external obligation
statistical validity   separate model/evidence obligation
```

In the exact three-node cycle, the semantic root stabilized after all three unique observations were present. All 512 seeded duplicate/reordered schedules converged to the centralized unique-atom oracle. In the hostile eviction case, the aggregate likelihood remained available while the duplicated atom's likelihood witness was absent from both states; exact merge refused. It also refused when an atom ID named conflicting content, lineage was only a lower bound, or an old registry epoch arrived without migration.

That looks like a worthwhile open space: keep PRIMEX-style active ancestry as the query cache, retain a content-addressed derivation circuit with separately retained or replayable payload references as the audit object, and issue a loss/availability receipt for every projection. The next unknown is whether that dual structure remains tolerable in a realistic distributed workload.

## Database/ETL provenance

The user's reading was accurate with one refinement: a topology-preserving provenance receipt is irreducible relative to basic scalar evaluation. The DAG is this build's tested encoding, not the only possible encoding. Evaluation is many-to-one. A result such as `10` cannot reveal which sources, joins, alternatives, retries, or transformations produced it.

The qualification is that it is a semantic sidecar, not a physically higher substance. If all inputs and the exact deterministic program remain available, provenance can sometimes be regenerated. If either is gone, it cannot generally be recovered from the scalar result.

Database provenance semirings provide the established control: multiplication records joint contribution and addition records alternatives. A PEV is flat source lineage/support (`Lin(X)`-like), not `Why(X)` and not complete polynomial (`N[X]`) provenance. Both flat support and witness-set provenance remain less informative than the polynomial form. Build 004 is bounded to positive composition; negation, difference, arbitrary aggregates, recursion, nondeterminism, and opaque UDFs remain open typed crossings.

## Calibration, scientific units, and the PAL-adjacent lenses

There is real potential, but it is not "factor the electron."

The useful synthesis is four independent objects:

```text
exact/approximate numeric coefficient
physical dimension vector
typed derivation history
evidence/calibration envelope
```

Build 004 implements and tests all four as a toolkit, but integrates only the lineage DAG, its source projections, and the evidence-reference envelope. It does not yet bind the numeric-factor and physical-dimension projections into the same canonical receipt. That missing integration is an explicit boundary, not implied validation.

The positive case is exact ratio-scale composition. The probe represents the SI-defined elementary charge value as an exact rational coefficient with charge dimension and a source envelope. That exactness is earned because BIPM defines the constant's numerical value exactly; it would be wrong to treat a measured, uncertainty-bearing electron property the same way merely because it has decimal digits.

UCUM's distinction is decisive. Proper ratio-scale units form the multiplicative space; Celsius-like interval scales and logarithmic units require conversion functions. Build 004 accordingly rejects exponent merging for affine, logarithmic, nonlinear, rounded, correlated, and expired cases.

After the computational result, the optional framework correspondences are:

- **BLA v0.9.1:** a residual can reopen a compressed calibration/support layer; it neither identifies the cause nor supplies authority.
- **CLEF v1.0:** a useful measurement receipt should name observable, aperture/resolution, baseline, noise, uncertainty/covariance, cost channel, stopping rule, and readability wall.
- **BRT&AIC v1.0.1:** exact symbolic intent crossing into a sensor state, API/log record, or PCM waveform can be analyzed as source, relevant boundary, receiver response, trace, and later readout.
- **PAL v2.2:** append-only trace, typed relation, contrary evidence, residuals, and no authority backflow are useful lenses. PAL v2.2 explicitly leaves multi-parent account semantics open, so this repository DAG is not a PAL account merge and does not amend canon.

These frameworks did not validate the software and the software did not validate them.

## "Impractically large" in abstract and physical space

In abstract mathematics, a prime product can be arbitrarily large without conceptual trouble. The encoding remains injective under its registry.

Once the product is materialized, transmitted, compared, or stored, its bit length is real. For full support over the first registered primes, Build 004 measured:

| Universe | Raw product | Dense PEV |
|---:|---:|---:|
| 8 | 24 bits | 8 bits |
| 64 | 417 bits | 64 bits |
| 256 | 2,290 bits | 256 bits |
| 1,024 | 11,583 bits | 1,024 bits |

So raw magnitude is harmless as a symbolic definition but usually a poor concrete data structure. The measured full-support widths favor the dense PEV over the raw product, while the sparse form stores one entry per active source and the DAG alone retains the tested derivation topology. Which representation wins in a real workload was not measured.

The cost artifact keeps three ledgers separate: 58 abstract-structure rows, 78 host-software diagnostic rows, and 17 physical-hardware implication rows (153 total). The physical rows state obligations such as width, routing, variable-width arithmetic, and graph/hash memory traffic; every one remains `NOT_MEASURED`. Build 004 performed no wall-clock, allocation, NAND/DFF, placed/routed, energy, or PPA comparison.

## Music and sound

Just intonation is a natural exact fit, but established prior art rather than prime magic. `3/2`, `5/4`, and their composition `15/8` are signed prime-coordinate intervals. Composition and inversion are local.

The interesting experiment is the boundary to sound. The exact ratio and lineage remain in the receipt while finite PCM records the approximation policy. Octave-equivalent pitch classes can collide while retaining different derivation receipt identifiers. Equal temperament, mixing, phase, timbre, sample clock, DAC, transducer, room, hearing, and musical interpretation are not prime-local.

## Structural accumulators and privacy

The transparent accumulator is a successful negative control. Products `10={2,5}` and `15={3,5}` intersect at `5`, but anyone with the public registry can test every membership. The artifact therefore says:

```text
cryptographic classification = NOT_CRYPTOGRAPHIC
privacy classification       = NO_PRIVACY
authenticated commitment     = NOT_PROVIDED
membership proof             = NOT_PROVIDED
zero-knowledge proof         = NOT_PROVIDED
```

The defensible version of the user's intuition is a future research direction in structural data minimization: omit data that a receiver does not require and design prohibited flows out of the reachable state space. Build 004 did not implement an information-flow policy or prove noninterference or forbidden-flow unreachability. Any implemented privacy guarantee still depends on algorithms, protocols, physical boundaries, authentication, and explicit assumptions.

## BOM

The toy confirmed the intended value without becoming the project: two BOM receipts both compute quantity `10`, have different lineage digests, and expose one shared component. This is a good eventual demonstration surface. A real tool would need units, tolerances, substitutions, lots/serials, scrap, revisions, supplier authority, and lifecycle rules.

## Negative results and displaced costs

1. Prime labels add no extra information once an injective coordinate registry exists; a PEV or set is the direct unique-support representation.
2. Support does not preserve how-provenance, multiplicity, operation order, payload, authority, or authenticity.
3. Exact overlap identity does not reconstruct the common payload.
4. Event sums, affine/logarithmic units, nonlinear transforms, correlated uncertainty, and general retraction are not exponent merges.
5. SHA-256 content addressing is integrity replay, not issuer authentication.
6. A public prime product is a linkage mechanism, not encryption or privacy.
7. Retaining both DAG and cache pays for both. Build 004 verifies only the cache's structural support/multiplicity against the DAG under a conservative `DigestOnly` declaration. Payload replayability remains an independent declared/evidenced obligation; cache replay does not derive `MissingDependency` or `UnsupportedTransform` semantics.
8. The exact combinatorial representation can avoid result factorization while still paying for prime-basis construction, valuation vectors, caching, reconstruction, and additive crossings.

These are successful results because they locate the useful boundary.

## Prior art closest to the implementation

- Chang et al., PRIMEX-PEV: active information-lineage support, overlap/deduplication, and shared-payload recovery in distributed fusion: <https://isif.org/media/encoding-information-lineage-scalable-distributed-fusion>
- Green, Karvounarakis, and Tannen, provenance semirings: joint/alternative symbolic provenance beyond source sets: <https://www.cs.ucdavis.edu/~green/papers/pods07.pdf>
- Deutch, Milo, Roy, and Tannen, *Circuits for Datalog Provenance*: explicit `Lin(X)`/`Why(X)`/`N[X]` hierarchy and circuit representation: <https://openproceedings.org/ICDT/2014/paper_36.pdf>
- UCUM: magnitude plus dimension vectors and explicit special-unit conversion functions: <https://unitsofmeasure.org/ucum>
- BIPM SI defining constants: exact definition fixtures and the boundary to measured constants: <https://www.bipm.org/en/measurement-units/si-defining-constants>
- Scala and monzo-like tuning practice: exact rational/prime-coordinate musical intervals: <https://huygens-fokker.org/scala/>
- cryptographic accumulator models: the missing security properties that a transparent product does not provide: <https://eprint.iacr.org/2015/087>

The reviewed local source hashes and status boundaries are recorded in `docs/PRIOR_ART_BUILD004.md`.

## Build 005 recommendation

Build 005 should be **Lineage Under Loss**, not another arithmetic expansion.

The recommended experiment is a durable, append-only derivation store with a verified PEV/bitset cache, exercised by a realistic positive ETL or distributed likelihood workload under message duplication, delay, omission, registry migration, payload eviction, conflict, and retraction. It should also decide whether a typed composite receipt can bind numeric factors and unit dimensions to the lineage/evidence root without collapsing their independent semantics. Correctness must come first. Only after centralized-oracle and recovery gates pass should the build compare:

- DAG-only versus cache-only versus DAG-plus-cache bytes;
- overlap-query latency and replay latency;
- cache verification and payload-fetch cost;
- compaction, checkpoint, and garbage-collection consequences;
- provenance coverage after payload eviction; and
- which residual should reopen which compressed layer.

That experiment directly tests whether the dual architecture earns its storage and operational cost. A cryptographic branch should begin only by selecting a published audited accumulator/signature scheme and threat model. A PAL-adjacent branch should remain removable and explicitly investigate, rather than presume to close, multi-parent semantics.

## Reproduction map

- frozen plan: `research/build004_experiment_plan.md`
- architecture: `docs/PROVENANCE_ARCHITECTURE.md`
- boundary probes: `docs/BUILD004_BOUNDARY_PROBES.md`
- prior art: `docs/PRIOR_ART_BUILD004.md`
- generated evidence: `results/build004/README.md`
- manifest: `results/build004/manifest.json`
- verifier: `scripts/verify-build004.ps1`
- deterministic sound: `results/build004/just_intonation_demo.wav`

The final claim remains bounded: this build validates an exact software toolkit and a set of structural counterexamples. It does not establish source authenticity, empirical calibration, privacy, cryptographic security, PAL conformance, universal performance, or hardware advantage.
