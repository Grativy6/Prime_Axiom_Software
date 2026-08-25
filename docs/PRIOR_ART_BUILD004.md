# Build 004 prior art and source boundary

This note records the sources that shape the experiment. It separates a source's own claims from Build 004's interpretation.

## PRIMEX-PEV

Primary source: Kuochu Chang, Way Kuo, Yaakov Bar-Shalom, Chee-Yee Chong, and Shozo Mori, "Encoding Information Lineage for Scalable Distributed Fusion," *Journal of Advances in Information Fusion*, volume 20, issue 2, pages 152-171, December 2025.

- Landing page: <https://isif.org/media/encoding-information-lineage-scalable-distributed-fusion>
- Full text: <https://isif.org/files/isif/2026-05/encoding_inofrmation_lineage-scalable_distributed_fusion.pdf>
- reviewed PDF SHA-256: `6349F3BFA43FB41FBE4FFB42CB8898323815159AADE5D02DDE27453E389E9E0C`

The paper's own progression is especially relevant. It starts from prime products for information codes, identifies their impractical magnitude growth, and replaces them with binary prime-exponent vectors. GCD becomes componentwise minimum, LCM becomes maximum, and a universe of the first `8n` assigned primes occupies `n` bytes. This means the operative scalable object is a registered bit vector; the primes provide the derivation and naming story, not a smaller physical encoding.

Its fusion algorithm also exposes the distinction Build 004 tests: the GCD/PEV identifies shared ancestry, but the shared probability density or state must still be found locally, reconstructed from stored information states, or queried. When the exact shared state is unavailable, the paper introduces approximate local-channel or least-squares strategies. Support identity and payload replay are therefore different resources.

The publication reports proofs-of-concept, numerical examples, and simulations. It explicitly leaves large-scale real target-tracking validation, secure distributed integration, and further lineage compression to future work. Build 004 does not reproduce its tracking benchmark or challenge its reported simulation outcomes. It isolates the exact overlap/replay boundary with rational finite-state payloads.

## Database provenance semirings

Primary source: Todd J. Green, Grigoris Karvounarakis, and Val Tannen, "Provenance Semirings," PODS 2007.

- Paper: <https://www.cs.ucdavis.edu/~green/papers/pods07.pdf>

The paper models positive relational algebra over commutative semirings and proposes polynomial annotations as a comprehensive symbolic provenance representation. Union/projection combine annotations with addition; join combines them with multiplication. Its 2007 discussion uses the then-current term "why-provenance" for the flat set of contributing tuple identifiers, so Build 004 does not attribute the later `Lin(X)`/`Why(X)` distinction to that paper.

Primary source for the later hierarchy: Daniel Deutch, Tova Milo, Sudeepa Roy, and Val Tannen, "Circuits for Datalog Provenance," ICDT 2014.

- OpenProceedings paper: <https://openproceedings.org/ICDT/2014/paper_36.pdf>
- reviewed PDF SHA-256: `DE1C12877C8F69C26184A277544DB4737FA68681DDC5297D83DA40496CCF813E`

That paper explicitly distinguishes flat lineage `Lin(X)`, witness-set provenance `Why(X)`, and polynomial provenance `N[X]` in its semiring hierarchy. Under this modern terminology, a PEV corresponds only to flat source lineage/support; both that projection and `Why(X)` can lose information retained by `N[X]`.

This is the control against over-crediting PEVs. `a*b+c*d` and `a*c+b*d` have the same source support and total variable multiplicities but different provenance polynomials. A persistent expression DAG can retain the distinction; a support vector cannot.

Build 004 does not claim full database provenance. It implements a small positive algebra/circuit and names recursion, difference, negation, aggregates, and opaque transforms as open crossings.

## Exact combinatorial probability

Factorial valuations and binomial prime exponents are established mathematics: for prime `p`, Legendre's formula gives the exponent of `p` in `n!`; subtracting factorial valuation vectors yields `C(n,k)`. Hypergeometric point probabilities are ratios of products of binomial coefficients.

The new repository question is engineering, not mathematical novelty: how far can an exact multiplicative receipt remain local, what reuse does it enable, and where does an event sum force an additive node or exact-magnitude crossing? The ordinary control uses exact cross-cancellation and adjacent recurrence, not naive factorial materialization.

Relevant implementation controls:

- GNU MP rational representation: <https://gmplib.org/manual/Rational-Internals.html>
- RRHO paper using exact hypergeometric overlap probabilities: <https://academic.oup.com/nar/article/38/17/e169/1033168>

## Units and calibration

Primary standards:

- BIPM SI defining constants: <https://www.bipm.org/en/measurement-units/si-defining-constants>
- Unified Code for Units of Measure (UCUM): <https://unitsofmeasure.org/ucum>
- JCGM measurement-uncertainty publications: <https://www.bipm.org/en/committees/jc/jcgm/publications>

UCUM represents a proper unit relative to a basis as a magnitude plus a dimension vector, which is an exact structural analogue of the project's split between numeric coefficient and unit exponents. UCUM separately classifies interval and logarithmic scales as special units defined by conversion functions rather than members of the multiplicative unit group. Celsius and dB are therefore required hostile cases, not inconvenient exceptions to hide.

BIPM states that the seven SI defining constants have exact numerical values and no uncertainty. For example, the elementary charge is defined exactly as `1.602176634 x 10^-19 C`. That is suitable for an exact coefficient receipt. A measured non-defining quantity must not be promoted to exact merely because a decimal is available. Build 004 tests the type boundary; it does not publish new scientific constants or calibration claims.

## Just intonation

Rational just-intonation intervals and prime-exponent/monzo descriptions are established prior art. Scala is a mature tool for creating, analysing, storing, and playing just-intonation and other tunings:

- Scala: <https://huygens-fokker.org/scala/>

Build 004's narrow experiment is the boundary from an exact rational interval receipt to finite PCM. The requested ratio remains exact; nominal frequency uses a declared base; the waveform is an approximate readout governed by sample rate, duration, amplitude, phase, envelope, rounding, and clipping. It is not a claim of perceptual equivalence or musical novelty.

## Cryptographic accumulators

Primary survey/model:

- "Revisiting Cryptographic Accumulators, Additional Properties and Relations to other Primitives": <https://eprint.iacr.org/2015/087>

Cryptographic accumulators compact a finite set and provide membership witnesses under explicit computational security properties. That is not what divisibility of a public prime product provides. A transparent product or PEV leaks the very membership relation it is designed to query. A SHA-256 DAG root is content addressing, not issuer authentication or zero knowledge.

Build 004 implements only the transparent structural control and labels it `NOT_CRYPTOGRAPHIC` and `NO_PRIVACY`. Selecting and integrating a published accumulator scheme, threat model, authenticated registry, proof system, key lifecycle, and external audit is separate future work.

## PAL-adjacent reference material

These sources were reviewed as optional comparative lenses, not specifications or independent corroboration:

- PAL v2.2 Spine, Mathematical Atlas, Ledger, Tests, and Compatibility Note;
- A0 Software Boundary-Layer Kernel v0.9.1;
- *Boundary-Ledger Accounting in Primitive Axiom Layers*, file version v0.9.1, public working draft, reviewed PDF SHA-256 `7EC1FBD21A792770A1617B74360B3AF1B2AB1FD056D9A81BB7BA1B136008185E`, <https://zenodo.org/records/20807530>;
- *CLEF: Cluster-Layer Entropy Focus*, v1.0, theoretical framework, reviewed PDF SHA-256 `24589330EDA2020145492FC7C3395911E0CF7A17CC67DFE47DAD4E7AF1280D43`, <https://zenodo.org/records/21193511>; and
- *Boundary-Readable Trace and Absorber-Informed Closure*, v1.0.1, public review paper and not a physics proof, reviewed PDF SHA-256 `33DA8DD002102D661976D0258A05FB5E8CFFC9B93F1B708A9E86FFD91CC7A0B7`, <https://zenodo.org/records/21008317>.

Their productive use is methodological: typed trace, residuals, named measurement boundaries, separated cost channels, uncertainty, and reopening. They do not make the implementation correct, do not turn a source code into a witness, and do not close PAL's explicitly open multi-parent account semantics.
