# ADR: speech-bubble-first dialogue presentation

**Status:** Accepted offline implementation
**Date:** 2026-07-26
**Scope:** Mosaic 0.2 observer-only dialogue presentation

## Context

A validated provider response is not evidence that anyone in the game saw an
utterance. Dialogue admission requires a receipt from an actual presentation
channel. The owner selected speech bubbles as the primary channel, with the
RimWorld play log as a narrow mirror and fallback.

## Decision

Core owns an immutable plain-data presentation row keyed by `EventId`,
`UtteranceId`, `ConversationId`, and speaker `IndividualId`. It contains no
RimWorld, Unity, provider, prompt, persistence, or cache object.

The presentation controller:

- permits one active bubble per speaker identity;
- bounds queued rows to three per speaker and 32 globally by default;
- uses deterministic priority, sequence, and identifier tie breaks;
- prevents a displayed `UtteranceId` from displaying twice;
- treats display labels as non-authoritative;
- wraps by Unicode text element and clamps text, timing, and screen bounds;
- dismisses expired, despawned, or off-map rows safely; and
- clears subscriptions and pending presentation evidence on disposal.

The RimWorld renderer is main-thread-only and reprojects the pawn's current
world position on every draw. A successful bubble produces a Bubble receipt,
even if play-log mirroring also succeeds. A failed bubble with a successful
mirror produces a PlayLog receipt. If both channels fail, no factual receipt
exists and no dialogue admission may follow.

## Authority boundary

The renderer has no provider, persistence, canonical mutation, pawn-job,
gameplay-action, or random-number authority. The custom play-log entry stores
only its displayed text and optional speaker reference for RimWorld's own log
serialization. Mirror failure is contained and cannot mutate cognition.

## Verification

Ten focused offline contract tests cover queue bounds, deterministic eviction,
duplicate prevention, identity isolation, rename/name collisions, Unicode
wrapping, timing, screen clamping, lifecycle/disposal, thread/binding/map
availability, actual-channel receipts, and canonical fingerprint purity.

The full documented Release build passed 139 contract tests, seven integration
scenarios with 631 assertions, static verification over 122 C# files, all
Release builds with zero warnings/errors, 17 negative firewall fixtures, and
an eight-entry deterministic package. RimWorld was not launched and the mod
was not installed; visual placement and live play-log behavior remain owner
runtime-test items.
