# Mosaic v44 / 0.3I — Contextual Provisional Reaction Admission

## Gate intent

v44 is the smallest safe bridge from the inert, read-only v43 grounded compound appraisal proposal to a bounded, session-local provisional reaction. It does not create a durable developmental record, mutate canonical affect or relationships, persist state, render UI, invoke a provider or planner, or issue pawn actions.

The gate closes only the following question:

> When may one exact v43 proposal become a temporary reaction owned by one witnessed character, and how is actual admission distinguished from an attempt?

Formal gate digest: `50fcd9611b04799e50a2e3e71d2402abbbade4343a96e8b56daf479805361ef5`.

## Required trusted chain

An admission requires all of the following simultaneously:

1. An exact self-verifying v43 proposal at gate digest `045ef8a2fd6084c183ced7d2ec9516b3289a309f0b3c55963a895746c70de0d8`.
2. A one-use internal v43 proposal attestation bound to the exact proposal, identity, lineage, source event, v42 context, v41 request, save, world, store set, generation, and checkpoint fingerprint.
3. An exact v38 `OBSERVED_SUCCESS` display receipt at gate digest `f44b00eee5a2fe8d7609bbd80caa79948caf388760f2e33765ec64ad50c3e0d8`.
4. A one-use internal canonical dialogue-event admission receipt proving the displayed utterance was admitted as the exact canonical event used by v43.
5. Exact agreement across utterance, conversation, speaker, recipient, audience, text hash, display tick, event ID, event hash, save, world, store set, generation, and checkpoint fingerprint.
6. Actual audience membership for the reaction owner.
7. Direct participant and exact-counterpart alignment before any relationship overlay is permitted.
8. Canonical affect and relationship fingerprints and versions as read-only stale-state guards.

A deterministic hash is necessary for tamper evidence, but it is not treated as a trust capability. Session-only factory products use retained object-identity attestations and are consumed exactly once.

## Attempt versus success

`prepare_attempt` creates an immutable `ATTEMPT_ONLY` packet. It may describe the proposed temporary overlay, but it cannot claim admission.

Only `ContextualProvisionalReactionStore.admit` may:

- consume both trusted factory objects;
- acquire the exclusive v44 reaction-path claim;
- insert the reaction into the bounded live store;
- update the utterance and canonical-event replay filters;
- emit an `OBSERVED_SUCCESS` provisional-admission receipt.

A failed or rejected attempt changes none of those states.

## Temporary reaction semantics

The admitted reaction preserves:

- exact v43 proposal provenance;
- owner, lineage, counterpart, utterance, conversation, speaker, canonical event, and checkpoint;
- up to three positive-support appraisal families;
- up to six affect dimensions and four relationship dimensions;
- unknown namespaced dimensions rather than silently dropping them;
- up to four deterministic conversational biases;
- confidence, intensity, uncertainty, and reason codes;
- a deterministic presentation recommendation of `NONE`, `ICON`, or `THOUGHT`.

The overlay is confidence-adjusted and uses shared L1 budgets:

- affect overlay: at most 1,500 basis points total;
- relationship overlay: at most 800 basis points total.

Unused influence remains unused. Zero-confidence dimensions and zero-weight families cannot inject an effect or bias. Traits and personality labels do not rescale evidence.

## Lifecycle

The lifecycle is:

`ATTEMPT_ONLY → OBSERVED_SUCCESS admission → PREPARED → ACTIVE → EXPIRED or DISCARDED`

Rules:

- creation time is the original observed display tick;
- admission time is separately retained;
- activation cannot precede admission;
- expiry is fixed at display tick + 15,000 ticks, so late admission cannot extend the reaction;
- admission at or after expiry fails closed;
- a checkpoint mismatch discards affected live reactions;
- terminal full objects are removed and represented only by counts and a deterministic terminal hash chain.

There is deliberately no save, restore, snapshot, promotion, durable-apply, or canonical-write method.

## Replay and double-counting rules

For each owner, v44 independently protects:

- `(owner, utterance)` identity;
- `(owner, canonical event)` identity.

The same event cannot be admitted again under a different utterance ID. Active exact replay is idempotent only when the complete request and attempt are identical. Conflicting active replay fails. Any terminal replay fails closed through fixed-memory deterministic filters.

A shared `ReactionPathClaimRegistry` prevents the legacy v39 cue path and the contextual v44 path from simultaneously owning the same `(owner, utterance)` provisional overlay.

## Bounds

- 32 active reactions per owner;
- 1,024 active reactions globally;
- 256 tracked owners;
- 64 pending attestations per factory;
- 1,024 reaction-path claims;
- two independent 1 MiB fail-closed replay filters;
- 24 reason codes;
- four conversational biases.

Capacity eviction is deterministic. At owner capacity, the store removes expired entries first, then lower-confidence and older entries with stable ID tie-breaking.

## Privacy and knowledge boundary

v44 accepts only the compact grounded v43 proposal and trusted receipts. It does not accept or emit:

- raw dialogue text;
- display names;
- player-only or observer-only knowledge;
- provider responses or prompts;
- RimWorld or Unity objects;
- personality or trait labels;
- plans, jobs, movement, combat, or relationship-repair instructions.

Diagnostics expose hashes, counts, bounds, checkpoint generation, and authority markers only.

## Authority boundary

Exact authority marker:

`SESSION_LOCAL_PROVISIONAL_ONLY_NO_DURABLE_NO_CANONICAL_NO_PERSISTENCE_NO_PROVIDER_NO_PLANNER_NO_UI_NO_PAWN_AUTHORITY`

v44 must never acquire methods or imports that provide persistence, canonical mutation, provider calls, presentation, planning, UI, or pawn/job control.

## Durable promotion boundary

v44 cannot promote its reaction into v40. v40 currently admits a different durable proposal contract; directly translating v44 output would lose full v43 context provenance and risk attempt-versus-success confusion or double counting.

A later gate may design an explicit v43-provenance-carrying durable proposal compatible with v40's guarded application protocol. Until that gate is independently closed, promotion is forbidden.


## Precompile status
This translation has not been compiled and must be rebound to the accepted v43 API before application.
