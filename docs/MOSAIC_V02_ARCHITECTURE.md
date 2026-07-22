# Mosaic RimWorld 0.2 Architecture

## Main pipeline

```text
RimWorld / optional mods
          |
          v
capability detection
          |
          v
portable event proposal
          |
          v
deterministic admission
          |
          v
validated evidence / temporal facts
          |
          +----------------------+
          |                      |
          v                      v
subjective appraisal       story lifecycle
          |                      |
          v                      v
beliefs, relationships, learned preferences, commitments
          |
          v
bounded future planning proposal
          |
          v
current-world execution gate
```

No direct LLM job issuance is implemented or authorized.

## Read-only presentation

```text
canonical state
      |
      v
IReadOnlyCharacterContextProvider
      |
      v
CharacterContextPacket
      |
      +-- RimTalk
      +-- RimHUD
      +-- Character Why
      +-- diagnostics
```

Building or reading presentation context must not alter memories, relationships, goals, timestamps,
RNG, or canonical state.

## Critical distinctions

- identity versus environment binding;
- evidence versus interpretation;
- job execution versus personal intention;
- static/mod trait versus learned preference;
- progress bookkeeping versus story milestone;
- source-mod objects versus portable Mosaic state;
- explanation trace versus hidden chain-of-thought.
