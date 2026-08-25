# Build 004 provenance architecture

Status: bounded tested architecture under `PAS-BUILD004-EXACT-LINEAGE-0001`; not a universal provenance model.

## Result-first summary

The useful architecture is not one giant prime product. The integrated lineage object earned here is a persistent derivation circuit with typed source projections and a content-addressed evidence-reference envelope:

```text
immutable derivation DAG --+-- exact source support cache
                           +-- source multiplicity cache
                           `-- projection loss declarations
              |
              `-- content-addressed root/registry binding
                    `-- external source/validity/uncertainty/residual digests

separately tested probe receipts:
exact numeric-factor projection + physical-dimension projection
```

The DAG answers "how was this derived?" A support vector answers "which registered atoms are active?" The evidence envelope binds that DAG root and registry to typed external-reference digests and independent completeness, replayability, and authenticity declarations. Its verifier establishes content integrity only, not the truth, freshness, or issuer of the referenced evidence.

The numeric-factor and unit-dimension probes answer different questions: "how is this exact rational value composed?" and "which base dimensions occur?" Build 004 tests their projection contracts and calibration crossings, but does **not** bind them into the lineage envelope or claim one integrated measurement receipt. That synthesis remains a Build 005 candidate, not an earned Build 004 result.

Collapsing these objects into one untyped field would make exact arithmetic look like evidence authority, make a source address look like a witness, or make a compact query index look like recoverable history. Build 004 keeps those category errors mechanically unavailable and leaves the final typed bundle unimplemented.

## Identities

### Atomic occurrence

An atom is an observation occurrence, input tuple, calibration record, component lot, or other declared leaf. It is not merely a sensor, person, table, or supplier. Its key is:

```text
namespace / assignment epoch / occurrence id
```

The descriptor additionally carries a source identifier and payload digest. The same key plus the same source identifier and payload digest is a retransmission. The same key plus different descriptor content is a conflict. Reusing a prime or bit position after retirement requires a new epoch; an old delayed vector must not silently acquire the new meaning.

### Derivation node

Nodes are content-addressed from canonical semantic bytes. Build 004 uses:

- `ZERO`: no derivation/result;
- `ONE`: valid source-free multiplicative identity;
- `ATOM`: supplied leaf identity;
- `JOINT`: all children jointly contribute;
- `ALTERNATIVE`: one of several derivations contributes to the same output;
- `TRANSFORM`: a named operation whose semantics are not captured by joint/alternative composition.

Child sorting and flattening make `JOINT` and `ALTERNATIVE` associative and commutative. Repeated children remain visible. Distributive rewrites can be semantically equal while retaining different structural roots; a structural receipt is not automatically an optimizer proof.

### Receipt axes

The full lineage/fusion receipt architecture keeps these axes independent:

| Axis | Example states | What it means |
|---|---|---|
| lineage completeness | exact, known lower bound, conflict | whether all contributing atom identities are known |
| payload replayability | replayable, missing witness, opaque transform | whether the declared value can be recomputed |
| issuer authenticity | `NotProvided`, `ExternalClaimNotVerified` | whether an external authentication claim is absent or remains unverified |
| numeric exactness | exact, rounded, approximate, unavailable | whether value equality is mathematical or tolerance-bound |

An exact support set with evicted leaf payloads can identify overlap but cannot replay exact fusion. The hostile fixture retains the aggregate likelihood while the shared per-atom likelihood is absent from both states. A fully replayable unsigned DAG remains unauthenticated. Verified/authenticated issuer status is outside Build 004; a future signed root would still depend on external keys, freshness, revocation, and trust rules.

## Projections and losses

| Representation | Preserves | Native query | Does not preserve |
|---|---|---|---|
| raw squarefree prime product | unique active source identities under a registry | divisibility, GCD intersection, LCM union | operation order, alternatives, timing, authority; grows as arbitrary-width magnitude |
| sparse exponent map | source multiplicity | exponent add/min/max | joint-versus-alternative structure |
| dense PEV/bitset | unique active source identities | Boolean union/intersection/difference | multiplicity and derivation structure; registry/epoch still required |
| explicit set | unique active source identities without numeric disguise | ordinary set queries | multiplicity and derivation structure |
| persistent DAG | typed joint/alternative/transform history | replay, explanation, specialization/retraction | authenticity and empirical validity; incurs graph storage and traversal |
| DAG plus cached projections | full retained history plus direct common queries | representation-local overlap with structural verification path | pays for both layers; Build 004 verifies only `DigestOnly` support/multiplicity, not payload availability |

For unique ancestry, raw squarefree products, PEVs, and explicit sets are isomorphic under a valid registry. Primality is not doing hidden work after the mapping exists. The prime product is a useful arithmetic embedding; the vector/set is usually the direct representation.

The DAG verifier recomputes reachable structure and checks conservative `DigestOnly` support/multiplicity caches. It does not infer whether an external payload is replayable, missing, or blocked by an unsupported transform. Those states belong to the independently bound evidence envelope or fusion-state receipt.

## Exact fusion contract

The bounded fusion probe uses strictly positive rational two-state likelihood weights. A state for source set `S` retains an unnormalized pair:

```text
W_S(0) = product of source likelihood weights at state 0
W_S(1) = product of source likelihood weights at state 1
```

For two states with exactly known overlap `G`, the unique-source merge is:

```text
W_union(j) = W_left(j) * W_right(j) / W_G(j)
```

The support projection can identify `G`; it cannot supply `W_G`. Exact merge therefore requires the shared payload to be retained, reconstructible from the DAG, or obtained through a declared query. In the hostile fixture, both states retain their aggregate weights but neither stores the shared atom likelihood, producing `OVERLAP_IDENTIFIED_PAYLOAD_UNAVAILABLE` rather than exact fusion. Approximation is a different operation and is outside this exact probe.

The centralized oracle multiplies every unique atom once. Every successful pairwise or network result must equal that oracle exactly. Duplicate or reordered messages may issue transport attempts, but no-new-information merge stabilizes the semantic expression root.

## Retraction and replay

Removing an atom from a pure invertible product can be a local division with an exact witness. General provenance does not have a quotient operation. For an expression such as:

```text
a*b + c*d
```

retracting `a` means specializing `a` to zero and replaying, leaving `c*d`. Subtracting `a` from the support set would incorrectly leave `b` as a contributing source. Opaque and nonlinear transforms must either provide a retraction contract or return `NON_INVERTIBLE`/`MISSING_WITNESS`.

## Database and ETL interpretation

The user's "irreducible receipt layer" reading is accurate relative to the scalar result: some topology-preserving receipt is required, while this DAG is one tested encoding. Evaluation is generally many-to-one, so the value alone cannot reconstruct its derivation. Database provenance makes this precise for positive relational algebra: multiplication records joint contribution and addition records alternative derivations. A source set is only a projection of that richer object.

"Layer above arithmetic" is useful if read semantically, not physically. The receipt travels beside evaluation and can sometimes be regenerated when all source data and the exact deterministic program remain available. Once either is gone, the scalar output cannot generally recover it.

The Build 004 ETL boundary is deliberately positive: joins, unions, projections, and explicit transforms. Negation, difference, aggregation, recursion, nondeterminism, external calls, and stateful UDFs require richer semantics and are not silently absorbed.

## Three independent cost ledgers

An elegant abstract operation is not a fast program or cheap circuit by implication.

1. Abstract structure records vector width, active coordinates, product bit length, DAG nodes/edges/depth, replay dependencies, and information lost.
2. Host software records serialized bytes, `BigInteger` operations, map work, hashes, traversals, reductions, cache behavior, and optional environment-specific timings.
3. Hardware implications record only interface/state width, variable-width arithmetic need, parallel Boolean depth, lookup/routing need, and memory/hash traffic until a gate/netlist experiment is actually frozen and run.

No common weight or score is introduced in Build 004.

## Security boundary

Prime products, PEVs, bitsets, and content hashes are not encryption. An exposed ancestry vector can intentionally reveal membership and overlap. Content addressing detects some accidental or adversarial mutation only when a trusted root is already available; it does not authenticate the issuer. The structural accumulator probe is labeled `NOT_CRYPTOGRAPHIC` and `NO_PRIVACY`.

A future system can pursue structural data minimization: do not collect or propagate fields that a receiver does not require, and design prohibited flows out of its reachable state space. Build 004 did not implement an information-flow policy or prove such unreachability. Any deployed privacy claim still needs a threat model, protocol, authentication, implementation review, and, where appropriate, published cryptography.

## Framework comparison boundary

Only after implementation and tests may the project note these removable correspondences:

- PAL-like append-only trace and typed relation are useful design lenses for the DAG and receipt checks.
- BLA can describe residual-triggered reopening when a projection no longer answers the active question.
- CLEF motivates named observable, aperture/resolution, noise floor, baseline, cost channel, uncertainty, and stopping rule.
- BRT&AIC offers a source-to-boundary-to-trace-to-readout vocabulary for exact symbolic intent becoming approximate PCM, sensor state, log record, or API output.

These correspondences are not computational evidence. Numeric primes are not PAL primitives; PEV intersection is not PAL relation or jointness; a multi-parent DAG is a repository experiment because PAL v2.2 leaves multi-parent account semantics open; exact replay does not issue authority or closure.
