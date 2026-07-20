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

## Adapter-authored first-person clauses

The environment adapter may attach a deterministic `first_person_clause` to each verified event,
for example `Mira warned me before a raid`. It must begin with the recorded actor, remain under 160
characters, contain no terminal punctuation, and carry the exact provenance marker
`DETERMINISTIC_ENVIRONMENT_ADAPTER`. The renderer prefers this clause over the legacy
third-person summary. The reflective provider never writes or edits it.

This keeps factual wording grounded in typed game events while producing grammatical dialogue. A
RimWorld adapter should generate these clauses from event kinds and fields, not from unconstrained
model prose.
