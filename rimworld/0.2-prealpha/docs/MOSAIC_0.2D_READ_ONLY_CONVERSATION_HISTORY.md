# Mosaic 0.2D — Read-Only Conversation History

**Status:** SUPPORTED OFFLINE; owner-operated RimWorld UI verification required
**Stacked base:** PR #6 branch `codex/mosaic-0.2c-cross-encounter-continuity-20260726`

## Objective

Add a player-facing Mosaic main-tab window that lets the operator select an enrolled colonist
and review that colonist's verified conversation history.

This milestone does not generate speech or change the dialogue pipeline. It is deliberately the
last stacked milestone before runtime testing.

## Source of truth

The viewer is rebuilt from existing canonical factual dialogue events admitted after actual
presentation receipts. It introduces no new store.

A row may appear only when:

- the dialogue event is structurally valid;
- the selected colonist was in the actual audience;
- the selected colonist was the speaker or direct recipient;
- both participant identities are still resolvable;
- the event remains within the bounded viewer result.

The viewer does not display provider proposals, validation results, pending outbox entries,
undisplayed text, private memories, inferred thoughts, or unrelated conversations.

## Display

The main tab shows:

- enrolled colonists;
- newest verified lines first;
- speaker and recipient labels;
- displayed game tick;
- actual presentation channel;
- bounded evidence count;
- displayed text.

Current display labels are resolved at read time. Renaming a colonist updates the label in the
viewer without rewriting historical EventIds or text.

## Authority boundary

The viewer is read-only. It cannot:

- alter identity, affect, beliefs, memories, relationships, goals, or personality;
- generate a reply;
- call a provider or local model;
- issue jobs, movement, combat, or other pawn actions;
- delete, edit, replay, or re-admit dialogue;
- expose another colonist's unwitnessed conversation.

## Required owner test

Use Core, Royalty, Ideology, Biotech, Anomaly, Odyssey, and Mosaic only with provider dispatch
offline and a new disposable colony/save.

1. Produce a PR #6 two-turn exchange between A and B.
2. Save and reload.
3. Open the Mosaic main tab.
4. Select A and confirm both admitted A/B lines appear, newest first.
5. Select B and confirm the same witnessed pair appears from B's participant-scoped view.
6. Select C and confirm the A/B exchange does not appear.
7. Rename A if practical and confirm the displayed label changes without duplicating history.
8. Confirm the window reports Bubble as the actual channel and shows bounded evidence counts.
9. Close and reopen the window; confirm no dialogue replays and no state changes.
10. Confirm no UI exception, DLC error, storage warning, thread-affinity failure, exception flood,
    pawn-control behavior, or recurring stutter.

This is the final feature stack before reviewing runtime evidence from PRs #4–#7.
