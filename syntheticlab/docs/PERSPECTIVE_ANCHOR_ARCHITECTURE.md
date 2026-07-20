# Perspective Anchor Architecture

v11.0 adds a new conceptual layer:

```text
Identity Kernel
    objective continuity
        ↓
Evidence Attribution
    which evidence belongs to which stream
        ↓
Perspective Anchor
    which stream is functionally coupled to the currently operating process
        ↓
SelfModel
    fallible hypotheses about agency, continuity, embodiment, relationships, etc.
        ↓
Language Rendering
    optional wording such as "I", "the individual", or another surface form
```

## Why this matters

A system may possess a stable functional self-reference without choosing first-person
English pronouns.

Conversely, an LLM can easily say "I" without possessing any persistent autobiographical
continuity.

Therefore Dagmay should not use pronoun choice as the primary operational definition of
a self-model.

## PerspectiveAnchorStore

The store records a fallible hypothesis about which evidence stream is directly coupled
to:

- current action output;
- private-state input;
- memory access;
- temporal continuity.

It is versioned and supersedable.

It is separate from:
- administrative identity;
- objective evidence provenance;
- SelfModel propositions.

## Claim limit

A correct PerspectiveAnchor is a functional/deictic binding result.

It does not establish:
- subjective experience;
- consciousness;
- sentience;
- personhood.
