# Mosaic 0.3C bounded reaction lifecycle boundary

Status: offline compile-gate candidate reconstructed from the certified v38 recovery contract.

## Purpose

This slice carries an already-grounded Mosaic social-affect decision through a bounded presentation
lifecycle. It distinguishes prepared presentation, release attempt, game-owned observed display,
retry, expiry, orphan recovery, and successful private history projection.

A ticket, queue entry, intended audience, or release attempt is never presentation success. Only an
exact `ObservedDisplayReceipt` with `OBSERVED_SUCCESS`, the compiled source contract, and the
certified v38 gate digest can complete the lifecycle.

## Portable receipt

The compiled downstream receipt schema is
`mosaic.v38.downstream-observed-display-receipt.v1`. Its semantic fields are:

1. session ID;
2. release-attempt ID;
3. receipt ID;
4. utterance ID;
5. conversation ID;
6. speaker ID;
7. optional recipient ID;
8. sorted unique actual audience IDs including the speaker;
9. exact transient text SHA-256, never raw dialogue;
10. displayed tick;
11. checkpoint generation;
12. `OBSERVED_SUCCESS`, `ATTEMPT_ONLY`, or `FAILED`;
13. source contract `Mosaic.Core.BoundedReactionLifecycle.v1`;
14. source gate digest `f44b00eee5a2fe8d7609bbd80caa79948caf388760f2e33765ec64ad50c3e0d8`;
15. a deterministic fingerprint over every preceding semantic field.

Public Core callers may create only non-success receipts. Trusted success construction remains an
internal reviewed-adapter boundary.

## Determinism, privacy, and recovery

- All identifiers and fingerprints use ordinal canonical encoding and SHA-256.
- Caller-supplied ticks control retry and expiry; no clock, RNG, GUID generation, or object identity
  influences behavior.
- Live presentation state is bounded to eight tickets with deterministic eviction.
- Terminal full records compact to at most 32 tombstones/digests.
- Snapshots are versioned, integrity-checked, store/owner/session/checkpoint-bound, and retain full
  recoverable in-flight entries.
- A future lifecycle checkpoint cannot load into an older save generation.
- Intended audience is not evidence of display. Successful private projection reaches only the
  perspective owner and recorded actual witnesses.
- Raw dialogue, private memory text, display labels, local paths, credentials, saves, and runtime
  objects are absent from persisted lifecycle state.

## Explicitly inert

This gate adds no provider/model/prompt/HTTP authority, canonical affect or character mutation,
runtime wiring, save field, RimWorld/Verse/Unity/Harmony dependency, UI rendering, dialogue
delivery, installation behavior, world mutation, job issuance, movement, combat, inventory,
romance, or pawn control. It does not launch RimWorld or access saves, logs, or configuration.

The Core contract and offline tests are preparation for a separately reviewed adapter. They are not
evidence that a reaction was shown in RimWorld.
