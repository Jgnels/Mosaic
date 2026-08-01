# Mosaic 0.3D provisional dialogue appraisal boundary

Status: inert Core candidate for the corrected v39 compile gate.

Gate digest:
`c8557ab218dc0a6136a960cfb495dd4853e5dd3ad81d8eadd7b36082b1b47819`

## Purpose

This slice converts an exact Mosaic 0.3C observed-success display receipt into a temporary,
perspective-owned appraisal. It preserves actual-audience witnessing, admitted owner knowledge,
claim-versus-fact separation, quote suppression, deterministic cue composition, bounded trait
response, decay, and fixed-memory duplicate protection.

The appraisal is an in-memory overlay. It is not canonical affect, relationship state, memory,
belief, a RimWorld thought, or a gameplay command.

## Corrected reconciliation contract

A public caller may request only a `PROPOSAL_ONLY_NO_MUTATION_AUTHORITY` durable application packet.
It cannot manufacture observed canonical success. Trusted display-success and canonical-success
factories remain internal reviewed seams.

Canonical success is accepted only when:

- the receipt matches the pending packet, owner, admitted event, checkpoint, source contract, and
  complete fingerprint;
- every canonical store targeted by a nonzero delta advances its version;
- every untargeted canonical store preserves its exact prior version and fingerprint;
- an absent relationship state remains absent unless no relationship delta was proposed; and
- a relationship delta is never proposed without an existing relationship state.

Failure removes the pending packet and returns the overlay to active. Success removes both packet
and overlay. Complete successes retain at most 64 recent records; a fixed one-MiB fail-closed filter
and deterministic completion chain protect older duplicates.

## Bounds

- per-utterance affect: `0.15`
- per-utterance relationship stance: `0.08`
- cumulative conversation affect: `0.35`
- cumulative conversation relationship stance: `0.20`
- live records per owner: `32`
- lifetime: `15,000` caller-supplied ticks
- trait response: `0.75` through `1.25`, without sign reversal
- terminal filters: one MiB, eight deterministic positions
- recent complete successes: `64`

## Explicitly absent authority

This candidate adds no RimWorld adapter, Def, UI, Harmony patch, provider path, scheduler behavior,
save field, persistence write, canonical mutation implementation, world mutation, pawn/job/action
path, ambient time, or random input. Diagnostics contain hashes and counts, not raw dialogue,
display labels, owner IDs, speaker IDs, or private evidence content.

The focused gate includes both unreduced 50,000-record stresses: appraisal admissions and successful
promotions.
