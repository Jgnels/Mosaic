# Mosaic presentation-only reviewed specification

Status: reviewed specification only; no third-party code or asset incorporated.

## Decision

The exact MIT-era RimTalk presentation candidates were hash-pinned and dependency-reviewed in
`docs/provenance/MOSAIC_PRESENTATION_CANDIDATE_REVIEW.json`. Direct adaptation is not justified.
The overlay and diagnostics code is coupled to RimTalk caches, settings, provider logs, pawn names,
live `Pawn` objects, global game lookup, and (in the custom-dialogue path) gameplay jobs. Removing
those authorities would replace most of the implementation.

Mosaic should therefore implement an original presentation surface only after its owner approves
the product shape. The direct-adaptation manifest remains empty and the package notice correctly
states that no RimTalk material is incorporated.

## Required input boundary

An eventual renderer may consume only an immutable, read-only projection created on the RimWorld
main thread. Each row must contain:

- `EventId`, `UtteranceId`, `ConversationId`, and speaker `IndividualId`;
- optional recipient `IndividualId`;
- a separately supplied, non-authoritative display label;
- already-admitted text, disclosure, presentation channel, and display tick;
- no `Pawn`, `Thing`, `Map`, `Def`, provider response, prompt, cache entry, or persistence handle.

The durable identity remains the `IndividualId`. A label collision or rename cannot merge rows,
histories, or subscriptions.

## Presentation behavior

- Display only utterances that already have a valid display receipt and factual admission.
- Redaction is performed before a row reaches the renderer. The renderer cannot elevate a
  participant view to Observer access.
- Long text wraps and clips inside a bounded scroll region; it never expands the window beyond the
  current screen.
- Layout measurement operates on Unicode text without byte truncation. CJK, combining characters,
  and surrogate pairs must not be split by any preview shortening.
- Resize bounds are recomputed when screen dimensions change. The window cannot become permanently
  unreachable after resolution or UI-scale changes.
- Empty history and missing display labels have explicit, non-error states.
- Original Mosaic icons and textures only.

## Lifecycle and ownership

- Subscribe at window/controller activation and unsubscribe idempotently at deactivation.
- A disposed or closed presentation surface receives no later callbacks.
- Refresh callbacks carry immutable rows, not a repository or live game object.
- No callback invokes a provider, appends an event, mutates memory/relationship/mood, writes a save,
  or schedules pawn work.
- Diagnostics read Mosaic observer projections and usage audit records; they do not read raw
  provider prompts or private character context.

## Offline acceptance tests before implementation

1. An admitted static row renders without any provider or persistence call.
2. Participant projection cannot display an utterance the participant did not witness.
3. Observer access is explicit and does not alter canonical fingerprints.
4. Rename and duplicate display-name fixtures remain separated by `IndividualId`.
5. Empty, 16,000-character, CJK, emoji, combining-mark, and bidirectional text fixtures remain
   bounded and do not throw.
6. Minimum/maximum resize and screen-resolution changes keep controls reachable.
7. Repeated activate/deactivate leaves exactly zero subscriptions.
8. Callback-after-dispose is ignored or rejected deterministically.
9. No generated dialogue creates a gameplay action or subjective canonical mutation.

## Stop line

No adapter UI code is added in this batch. Product decisions remain for the window type, default
visibility, placement, timing, and visual styling. Those choices should not be guessed before the
owner-controlled Gate 3 soak.
