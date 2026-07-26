# Mosaic 0.2B — Bounded Two-Turn Exchange

**Status:** implemented and verified offline at `db030c5`; owner-operated RimWorld/DLC gate pending
**Stacked base:** PR #4 branch `codex/mosaic-0.2a-grounded-relationship-dialogue-20260726`
**Required base head:** `409a92bfba037159efa5b6a6314aa4fe7251b088`

## Objective

After a qualifying grounded relationship line is actually displayed and its factual
admission is successfully queued, allow the addressed colonist to produce exactly one
deterministic grounded reply.

This is not free-running conversation. The hard runtime shape is:

1. one qualifying social event;
2. one grounded opening line;
3. actual Bubble or fallback presentation receipt;
4. successful checkpoint-bound admission queue;
5. one reply from the original recipient;
6. termination.

No third turn is possible.

## Grounding

The opening remains identical in architecture to 0.2A.

The reply is grounded in:

- the mandatory current qualifying social EventId;
- zero to two prior experienced relationship events owned by the reply speaker;
- exact EventId provenance;
- the fact that the opening line was actually displayed to both participants.

The displayed opening text is context data, not a new autobiographical memory. The reply
does not cite an uncommitted synthetic dialogue event as mandatory provenance; it cites the
underlying canonical social evidence and optional recipient-owned history.

## Privacy and authority

The reply excludes:

- private evidence;
- observer-only evidence;
- told or inferred evidence;
- unrelated counterparts;
- unverified opening proposals;
- missing or mismatched display receipts.

It cannot:

- change relationships;
- alter identity, affect, beliefs, memory, goals, or personality;
- issue jobs or pawn actions;
- invoke a real provider or local model;
- continue to a third turn.

## Runtime release gate

A reply is prepared only after all of these are true:

- the opening was strictly validated;
- the opening was actually displayed;
- the receipt matches the captured social trigger;
- both participants are in the actual display audience;
- the opening admission was successfully queued;
- the reply request is still live.

If any condition fails, no reply is created.

## Required owner test

Use a new disposable colony/save with:

- Core;
- Royalty;
- Ideology;
- Biotech;
- Anomaly;
- Odyssey;
- Mosaic only;
- provider dispatch offline.

Create reciprocal history between colonists A and B, then trigger a qualifying event from
A toward B.

Required observations:

1. A displays the grounded opening.
2. Only after A's bubble completes, B displays one reply.
3. Both bubbles concern only A and B.
4. B's reply distinguishes A's current view from B's own history.
5. No third bubble is created by the exchange.
6. The log reports one queued receipt-gated bounded reply with one to three evidence IDs.
7. Both displayed utterances queue factual admissions.
8. Save and reload do not replay either old bubble.
9. A later qualifying event can create a new independent two-turn exchange.
10. No DLC error, storage warning, thread-affinity failure, exception flood, pawn control,
    or noticeable recurring stutter occurs.

Third-party mods, RimTalk coexistence, real providers, organic multi-turn conversation,
and learned conversational policy remain outside this gate.
