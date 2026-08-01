# Mosaic RimWorld Active-Tree Guidance

This is the active RimWorld research/development tree.

The repository-root `research/CANONICAL_CHECKPOINT.md`, root `AGENTS.md`, and `research/PERSISTENT_CHARACTER_BOUNDARY.md` are authoritative.

## Current base

Accepted v44R / 0.3I commit:

`89be3f89fdd3802abd92f0b2821dd7ec28cfc0a1`

The frozen `rimworld/0.1-closure-candidate/` tree is historical/recovery source and must not receive new feature development.

## Binding boundaries

- Model and external-mod output are untrusted proposals.
- Observer/debug/prompt/UI operations must not silently mutate cognition.
- External integrations fail soft and do not own durable identity.
- Third-party CLR objects must not be stored as durable Mosaic identity/cognitive state.
- Generated language is not automatically canonical truth.
- No pawn-control, job-issuance, planner authority, or autonomous action path may be added without a separate owner-approved design/safety gate.
- v44 is session-local provisional only and must retain its no-durable/no-canonical/no-provider/no-planner/no-UI/no-pawn authority boundary.

## Current authorized work

**No new cognition milestone is authorized. v45 is frozen.**

Allowed current work:

- repository consolidation/hygiene;
- test-quality audit;
- runtime automation feasibility work that does not expand cognition;
- design/implementation of the controlled live 0.3 shadow vertical-slice harness after consolidation review;
- player-value measurement design.

Before creating a new source file, abstraction, schema, harness, or document, identify the demonstrated risk it removes or observable character capability it enables and prefer reuse/simplification where possible.

Offline gates remain necessary but are not sufficient evidence for live gameplay quality.
