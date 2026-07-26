# Mosaic 0.2A — Grounded Relationship Dialogue

**Status:** prepared implementation candidate; requires Windows/RimWorld compilation and offline verification
**Base:** PR #3 tested implementation commit `c49da815bd9271d132f557cec40745f50a3b9962`

## Objective

Replace the single generic deterministic sentence with bounded relationship dialogue grounded in:

1. the current qualifying social event;
2. up to two prior, same-counterpart, first-person experienced events;
3. exact canonical `EventId` provenance;
4. positive/negative balance when the available history is mixed.

This remains an observer-first presentation feature. It does not modify pawn behavior, relationships,
goals, identity, memory, appraisal, or any RimWorld job.

## Selection rules

- The current event is mandatory and always first in the evidence list.
- At most two prior events may enter one line.
- Only `Experienced` evidence may enter this first milestone.
- Only `Shareable` or `RelationshipSensitive` evidence may enter.
- Private, observer-only, told, inferred, malformed, cross-owner, and cross-counterpart evidence fail closed.
- A meaningful direct relationship transition outranks ordinary opinion movement.
- When both positive and negative history exist, the selector retains one of each before filling any remaining slot.
- Ordering and tie-breaking are deterministic.

## Rendering rules

The renderer uses only supported factual payload fields:

- `opinion_before`
- `opinion_after`
- `opinion_delta`
- `relations_before`
- `relations_after`

It may state that the speaker feels conflicted when selected evidence contains both positive and
negative valence. It may not invent motives, hidden intentions, personality traits, relationship
changes, actions, or future commitments.

The exact displayed line remains fake-provider deterministic. No network provider or local model is
authorized by this milestone.

## Persistence

No new durable store is introduced. Prior evidence is rebuilt from the existing verified experience
journal after load. Dialogue itself remains factual-only and checkpoint-aligned through the existing
recoverable outbox.

## Required live gate

The first owner test should use:

- RimWorld Core;
- Royalty;
- Ideology;
- Biotech;
- Anomaly;
- Odyssey;
- Mosaic only;
- provider dispatch offline;
- a new disposable colony/save.

Required observations:

1. enroll at least three colonists;
2. create a positive event between two colonists;
3. later create a negative event between the same pair;
4. trigger another qualifying event;
5. confirm the bubble refers only to those two characters and reflects the correct mixed history;
6. confirm the log records one to three exact evidence IDs;
7. save and reload;
8. trigger another qualifying event and confirm prior history remains available without replaying old dialogue;
9. confirm no DLC-specific error, exception flood, thread-affinity error, storage warning, or pawn-control behavior.

Third-party mod compatibility and RimTalk coexistence remain outside this gate.
