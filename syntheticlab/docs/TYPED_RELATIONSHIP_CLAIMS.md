# Typed Relationship Claims

## Why

V2 showed that citation-complete dialogue can still invent personality-level meaning. A keyword
filter caught known phrases but missed semantically equivalent overgeneralizations.

## Boundary

The reflective model may propose only:

- one bounded disposition: `TRUST`, `MIXED`, `DISTRUST`, or `INSUFFICIENT_EVIDENCE`;
- positive evidence identifiers;
- negative evidence identifiers.

Mosaic validates identifier existence, evidence valence, and disposition/citation consistency. It
then renders dialogue deterministically from the verified event summaries. The model does not author
new factual clauses or durable personality traits.

This is intentionally conservative. Richer prose may later be added as a separately validated style
layer, but it cannot add claims.
