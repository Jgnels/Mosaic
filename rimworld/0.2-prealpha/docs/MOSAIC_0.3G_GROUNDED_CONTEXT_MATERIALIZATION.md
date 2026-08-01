# Mosaic 0.3G — Grounded Developmental Context Materialization

## Status

Mosaic 0.3G is an inert Core milestone. It resolves the exact accepted 0.3F retrieval bundle
through trusted compact projections and emits an immutable, bounded, appraisal-ready evidence
packet. It is not wired into RimWorld runtime behavior.

## Dependency and contract

- Accepted v41 commit: `8d25c29eedb2fddbbf1a5c750a05505a81fdf8dd`
- Accepted v41 tree: `2297f5e41a9e925213523c8126403470598d6954`
- v42 contract: `Mosaic.Core.GroundedDevelopmentalContextMaterialization.v1`
- v42 formal digest: `c48b1e977c96b0450f97e8579e158cfe2353e412ba6dbcdf30e4056cbdd5edb9`
- Authority: `READ_ONLY_APPRAISAL_CONTEXT_NO_CANONICAL_PROVIDER_PLANNER_UI_OR_PAWN_AUTHORITY`

## Trusted input

`TrustedContextProjectionRegistry` rebuilds compact projections only from the exact v40 canonical
record/event join and accepted owner-private v41 memory evidence. Queries must preserve exact save,
world, store-set, owner, lineage, counterpart, checkpoint generation, checkpoint ancestry, tick,
selected-item metadata, and complete v41 bundle fingerprint binding.

Missing, forged, foreign, future, rollback-orphaned, substituted-checkpoint, recursively derived,
current-event-root, duplicate-root, and duplicate-selected inputs fail closed. The registry retains
no complete canonical event, developmental-record, or memory object.

## Materialization

- At most four selected items are emitted.
- Fixed v41 role caps remain 4000/3000/2000/1000 basis points.
- Directional record dimensions are explicitly namespaced as `affect.*` and `relationship.*`.
- Each role cap is allocated by deterministic largest remainder.
- Memory category, salience, durability, and provenance remain visible, but memory contributes no
  invented directional affect or relationship value.
- Positive and negative evidence remain separate; contradiction lowers confidence.
- Unused role capacity remains unused and is never renormalized.

The public packet serializer is deterministic JSON. Its fingerprint payload contains the exact
public `source_gate_digest` value and omits only `fingerprint`. `VerifyFingerprint()` reproduces the
stored SHA-256, and `RequireValid` rejects a mismatched public fingerprint.

## Boundary

The packet contains no raw dialogue, display labels, prompts, provider objects, RimWorld objects,
persistence writers, canonical mutation methods, UI rendering, planner commands, jobs, movement,
combat, or pawn authority. Materialization is an observer-pure read. A later appraisal milestone
may consume a verified packet only through a separately reviewed authority boundary.

## Verification

The focused gate contains 58 corrected contracts, including public packet self-verification and
mismatch rejection. The sixteenth default integration executes genuine v39 proposal, v40
checkpoint-aligned canonical admission, accepted-v41 retrieval, and v42 materialization. Its stress
mode processes 50,000 developmental records, 50,000 memories, and 10,000 materializations in each
admission direction and requires identical retrieval, projection-state, and packet-chain digests.
