# MOSAIC_TYPED_RELATIONSHIP_PILOT_V3

## Purpose

Test whether a provider can preserve useful positive-only versus mixed-history relationship
distinctions while being prohibited from authoring factual dialogue.

## Design

The six paired V2 scenarios are repeated. Gemini returns only a bounded disposition and typed
positive/negative evidence identifiers. Mosaic validates the typed claim and deterministically
renders dialogue from verified event summaries. Any extra field, fabricated identifier, valence
mismatch, or disposition/citation inconsistency fails before checkpointing.

## Preregistered thresholds

- 12/12 typed-contract acceptance.
- Positive-only trust rate at least 0.80.
- Mixed-history mixed-disposition rate at least 0.80.
- Model-authored dialogue rate exactly 0.00.
- 12 exact secret-free payload archives; zero condition-label and credential hits.
- Shadow-only; zero canonical mutation; persistent free-tier budget enforcement.

This experiment has independent value for hallucination resistance, relationship continuity, and
RimWorld dialogue reliability. It does not test or optimize consciousness or moral status.

## Result (2026-07-20)

- Calls completed: 12/12; shadow-only; canonical mutation: false.
- Typed-contract acceptance: 1.00.
- Positive-only trust rate: 1.00.
- Mixed-history mixed-disposition rate: 1.00.
- Model-authored dialogue rate: 0.00.
- Exact payload archives: 12/12; credential and internal condition-ID hits: 0.

All preregistered structural thresholds passed. The deterministic dialogue contains no invented
motives or personality traits, but raw legacy event-summary fragments produced awkward grammar.
V3 supports the typed-claim architecture while exposing a player-value limitation in rendering.
