# Mosaic 0.2E — Determinism, Observer Purity, and Lifecycle Closure

**Status:** prepared verification milestone; compile and full offline certification required
**Stacked base:** PR #7 branch `codex/mosaic-0.2d-read-only-conversation-history-20260726`

## Purpose

Close the remaining deterministic-read and identity-lifecycle risks before Mosaic begins 0.3
subjective appraisal work.

This milestone adds no new player-facing behavior and no new runtime authority. It strengthens
the proof around the existing 0.2 dialogue, persistence, continuity, and history-viewer stack.

## Audited design sources

The verification strategy follows lessons from the audited RimWorld Multiplayer and Multiplayer
Compatibility projects:

- observation and UI reads must not alter simulation state;
- equivalent histories must produce equivalent outputs regardless of incidental enumeration order;
- equal-score choices require explicit stable tie-breakers;
- save/load and transient object reconstruction cannot replace durable identity;
- presentation and diagnostics cannot consume a simulation-random stream.

Game AI Pro, behaviac, and BehaviorTree.CPP informed the separation between canonical state and
read-only diagnostics, but no external runtime or source is imported.

## Added gates

### Contract coverage

- combined dialogue, cross-encounter, and viewer reads preserve one canonical fingerprint;
- equal-score relationship evidence uses EventId tie-breaking;
- equal-time completed conversations use ConversationId tie-breaking;
- repeated viewer reads are deterministic and duplicate-safe;
- archive/revival/rename and transient binding reconstruction preserve IndividualId and LineageId;
- the canonical fingerprint is proven sensitive to an actual fixture mutation.

### Integration replay

A new integration scenario runs 128 equivalent-history permutations and requires one identical
signature containing:

- grounded dialogue text and evidence IDs;
- selected prior ConversationId;
- participant-scoped history row EventIds.

The scenario publishes a deterministic SHA-256 digest. The verifier launches the scenario in two
independent processes and requires the same digest.

### Source firewalls

The verifier rejects simulation RNG dependencies in dialogue, observer, presentation, view, and
RimWorld-dialogue source paths. It also rejects provider, persistence-write, canonical-mutation,
and pawn-control surfaces in the read-only history viewer.

## Live closure test

This can be folded into the PR #7 owner session:

1. Produce and persist a two-turn exchange.
2. Save and reload.
3. Record the enrolled IndividualId and LineageId values.
4. Open, switch colonists in, close, and reopen the Mosaic history window repeatedly.
5. Trigger another exchange after the UI activity.
6. Confirm evidence selection and `When we last spoke` continuity remain correct.
7. Save and reload again.
8. Confirm IDs are unchanged, old bubbles do not replay, and viewer history does not duplicate.
9. Confirm no storage warning, affinity error, exception flood, pawn behavior change, or recurring stutter.

The production package may be tested immediately after PR #7 because 0.2E changes verification,
tests, harness code, and documentation only. It does not intentionally change shipped runtime logic.
