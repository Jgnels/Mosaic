# RimWorld Relationship Event Contract

## Purpose

Graduate Mosaic's typed relationship mechanism toward the RimWorld adapter without importing the
missing 0.1RC source or changing its frozen cognitive scope.

The adapter emits a supported event kind plus structured actor, target, and optional short detail.
Mosaic maps that event to valence and a grammatical first-person clause. Neither the environment
adapter nor the reflective model may submit an arbitrary factual sentence through this contract.

## Initial coverage

The contract covers 24 event kinds represented in the V1-V3 relationship corpus: aid, rescue,
warning, sharing, treatment, rebuilding, defense, watch, theft, deception, insult, broken agreements,
privacy breach, abandonment, credit theft, and related colony interactions.

Inputs are length- and character-restricted. Unsupported kinds, unexpected/missing details, unsafe
labels, fabricated evidence IDs, valence mismatches, and non-deterministic clause provenance fail
closed.

## Integration boundary

This Python contract is an executable reference for the future C# RimWorld adapter. It is not yet a
RimWorld integration and makes no claim that the owner-reported 0.1RC source has been recovered.
