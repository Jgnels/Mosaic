# ADR: offline RimWorld dialogue path

**Status:** Accepted offline implementation
**Date:** 2026-07-26
**Scope:** Mosaic 0.2 deterministic fake-provider integration

## Context

The dialogue foundation, recoverable factual outbox, and speech-bubble
presenter were independently verified, but no RimWorld adapter path composed
them. The first path must be testable without launching RimWorld, must use no
real provider, and must preserve the distinction between a generated proposal,
an actual display, and durable factual admission.

## Decision

Material observed opinion and direct-relationship changes are the only
supported initial triggers. The existing main-thread observation component
resolves both stable Mosaic identities and captures an immutable plain-data
record containing the source `EventId`, tick, time, event kind, bounded factual
payload, display labels, lineage, state version, and affect.

After capture, no Pawn, Map, Thing, Def, job, or other live RimWorld object
enters scheduling, context assembly, prompt planning, provider work, strict
decoding, or validation. The integration composition root constructs
`DeterministicFakeProvider` directly; no provider-selection or network seam is
available in this path. IDs derive deterministically from the source EventId.

Validated output returns to a main-thread GameComponent, which resolves current
Pawn bindings only for presentation. A repaint-confirmed bubble or successful
play-log fallback is required for a receipt. Failure or abandonment removes the
pending adapter record and cannot enter admission.

## Checkpoint and recovery boundary

An actual receipt prepares a factual-only admission and persists only a pending
outbox entry at the current dialogue checkpoint. It does not immediately
materialize the event ledger or experience journal. During RimWorld's save
callback, the coordinator:

1. recovers and verifies all pending destinations at the current checkpoint;
2. reloads the verified journal head into the save manifest;
3. advances the dialogue checkpoint; and
4. compacts eligible completed tombstones.

Pending work is rediscovered read-only after restart and remains queued until
the next save callback. Unchanged saves without dialogue work do not advance
the dialogue checkpoint. Invalid, mismatched, forward, missing, or conflicting
outbox evidence fails closed.

## Authority boundary

The path is observer-only. It has no pawn-control, jobs, priorities, drafted
state, movement, combat, action execution, canonical interpretation, automatic
memory, relationship, mood, belief, goal, identity mutation, or random-number
authority.

## Verification

Six focused offline tests cover absent identity bindings, unsupported triggers,
deterministic strict fake-provider preparation, duplicate suppression,
expiration, cancellation, despawn, total display failure, actual-channel
receipt gating, pending-only outbox enqueue, restart discovery, exactly-once
recovery, factual-only journal records, and stale-checkpoint rejection.

RimWorld was not launched and no package was installed. Live social-trigger
frequency, GUI placement, play-log serialization, callback ordering, and save
checkpoint behavior remain owner-controlled runtime-test items.
