# Mosaic 0.2C — Cross-Encounter Conversation Continuity

**Status:** prepared implementation candidate; Windows/RimWorld compilation and full offline verification required
**Stacked base:** PR #5 branch `codex/mosaic-0.2b-bounded-two-turn-exchange-20260726`

## Objective

Allow a later qualifying exchange between the same two colonists to acknowledge their most
recent completed, actually displayed, admitted two-turn exchange.

This milestone does not lengthen one exchange. Each new event still permits only:

1. one grounded opening;
2. one receipt-gated reply;
3. termination.

Continuity operates across separate qualifying encounters.

## Canonical boundary

A prior exchange qualifies only when both turns already exist as verified factual dialogue
events in the canonical ledger. The selector requires:

- exactly two admitted turns in one ConversationId;
- opposite speakers and recipients;
- both participants in the actual audience;
- chronological display receipts;
- the exact same pair as the new event;
- display time no later than the new event.

These do not qualify:

- a provider proposal;
- a validated but undisplayed utterance;
- a pending outbox item;
- a one-sided PR #4 statement;
- an incomplete PR #5 exchange;
- an unrelated pair;
- a future or malformed event.

## Semantic safety

Earlier generated text is used only as an attributed factual record of what was displayed:

- `I said "..."`
- `you said "..."`

The prior text is not promoted into independent world truth, belief, personality, or memory.
The new utterance remains grounded in the current social EventId, relationship evidence, and
the exact admitted dialogue-event IDs proving the prior speech occurred.

## Persistence

No new store is introduced. The prior exchange is rebuilt from the existing verified experience
journal and canonical event ledger after load. This deliberately makes save/reload part of the
live gate.

## Required owner test

Use a new disposable colony/save with Core, Royalty, Ideology, Biotech, Anomaly, Odyssey,
and Mosaic only. Keep provider dispatch offline.

1. Create one qualifying A→B event.
2. Confirm A displays one grounded opening and B displays one reply.
3. Save and reload so both admissions are canonical ledger history.
4. Confirm neither old bubble replays.
5. Create a later qualifying event between A and B.
6. Confirm the new opening includes `When we last spoke` and attributes both prior turns as
   `I said`/`you said` from the current speaker's perspective.
7. Confirm the new request reports the current social EventId plus the two prior admitted
   dialogue EventIds, in addition to any bounded relationship history.
8. Confirm B produces only one new reply and no third turn appears.
9. Trigger a qualifying event involving colonist C and confirm A/B dialogue does not leak.
10. Confirm no DLC errors, storage warnings, thread-affinity failures, exception flood,
    pawn-control behavior, or recurring stutter.

Third-party mods, RimTalk coexistence, real providers, free-running conversation, learned
personality, goals, and action selection remain outside this gate.
