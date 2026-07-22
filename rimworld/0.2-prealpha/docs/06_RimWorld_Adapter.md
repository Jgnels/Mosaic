# 06 — RimWorld Adapter

**Status:** Version 0.1E observer/runtime-controls implementation plus remaining Version 0.1 design  
**Target:** RimWorld 1.6 on Windows through Steam, all DLC including Odyssey. Original baseline: 1.6.4850; current owner test patch: 1.6.4871.

## Responsibility

`Dagmay.RimWorld` is an environment adapter. It translates RimWorld state and callbacks into bounded Dagmay perceptions, links Dagmay identity to save/world lifecycle, and presents UI projections. In Version 0.1 it must not influence pawn behavior.

## Hard boundary

RimWorld types remain inside this project. `Pawn`, `Map`, `Thing`, `Hediff`, `Thought`, Verse serialization types, Unity types, and Harmony patch details never enter `Dagmay.Core` contracts.

The adapter may depend on the core; the core may not depend on the adapter.

Version 0.1D's RimWorld project is also the executable composition root: it selects an optional provider implementation and passes only immutable Core contracts to it. That dependency does not move identity into RimWorld. The identity, reflection contract, validation, queue records, and portable intrinsic state remain provider-neutral Core types and files; another environment can host the same Core and Providers assemblies with its own perception adapter.

Version 0.1E adds only a RimWorld rendering shell and host-policy controls around Core-owned Observer projections. It does not copy RimTalk/RiMind memory state into the individual, and it does not make Verse settings canonical identity. A future Minecraft or Sims host can render the same Core projection through a different UI while preserving the same individual and lineage.

## Main-thread safety

Harmony callbacks run in game-sensitive code paths. Each callback should:

1. determine quickly whether the event is relevant;
2. read only fields safe at that point;
3. build an immutable, minimal snapshot;
4. enqueue the snapshot without waiting; and
5. return control to RimWorld.

Callbacks do not perform network access, provider calls, bulk serialization, synchronous disk flushes, semantic search, or model parsing. Background workers never retain or access live RimWorld object references.

Completed core work is surfaced through a bounded main-thread update pump. In Version 0.1 that pump updates Dagmay projections and UI only.

## Pawn identity mapping

Dagmay uses its own stable `IndividualId`. The adapter maintains a versioned mapping from RimWorld's most stable available pawn identity data plus world/save provenance. Names are display fields and may change.

Creation must be idempotent: discovering the same colonist through initialization, map load, caravan return, or save load cannot create additional individuals. Ambiguous mapping stops activation and opens diagnostics rather than guessing.

## Seed snapshot

At first eligible observation, capture available:

- biological and chronological age;
- childhood and adulthood backstories;
- traits;
- skills, levels, passions, and relevant incapabilities;
- ideology, certainty, roles, and precepts visible to the pawn;
- xenotype and genes where applicable;
- direct relationships and game-provided opinions;
- major health conditions, pain, consciousness, and mobility context;
- current colony role and circumstances; and
- DLC/mod provenance for fields not present in the base game.

The snapshot is versioned and preserved even after later game changes. Fields from absent DLC or mods remain absent.

## Minimum Version 0.1 event set

The adapter should implement a small, testable event catalog before broad coverage:

| Event | Factual capture | Perspective rule |
| --- | --- | --- |
| Identity discovered | seed source and mapping | administrative; not a subjective memory by default |
| Joined/left colony | transition and cause if exposed | the pawn experiences its own transition; witnesses require proximity/context |
| Social interaction | participants, interaction type, outcome exposed by game | participants receive direct perspectives; nearby witnesses only if plausibly observable |
| Relationship change | relationship/opinion delta and exposed cause | affected individual receives its own state; others only through observation/communication |
| Injury/damage | type, severity, instigator if known, context | injured pawn and plausible witnesses |
| Downed | cause/context if known | pawn plus plausible witnesses |
| Tended/rescued | caregiver, recipient, material outcome | participants and plausible witnesses |
| Death | identity, cause if exposed, place/time | lifecycle always archives the dead; subjective memory only for witnesses or later communication |
| Extreme need threshold | hunger, rest, temperature, pain, or similar band crossing | that pawn only; hysteresis prevents spam |
| Skill-level change | skill and old/new level | that pawn; observers only when a separate event warrants it |
| Material ideology change | conversion, certainty threshold, or role change | affected pawn and participants with plausible knowledge |

Every patch point must be documented in a generated patch inventory with method, purpose, event kind, known conflicts, and game versions tested.

## Perspective filtering

Do not build one omniscient event and hand it unchanged to every colonist. The adapter first records the environment event, then derives individual views based on:

- direct participation;
- map and location;
- line of sight or other game-supported awareness when practical;
- consciousness and relevant capacities;
- social communication events;
- relationship knowledge exposed by RimWorld; and
- explicit test fixtures.

When the game cannot support a reliable perception claim, use less detail and lower confidence. Never infer private motive from a game callback.

## Event noise control

RimWorld emits many rapidly changing values. The adapter should use:

- threshold bands with hysteresis for needs;
- debouncing for repeated notifications;
- stable deduplication keys;
- coalescing for repeated low-severity events;
- severity thresholds configurable by event kind; and
- critical-event bypass for death and major harm.

Raw callbacks may be counted in diagnostics without becoming memories.

## Save and lifecycle bridge

The RimWorld save contains a compact Dagmay manifest:

- world/save identifier;
- active individual mappings;
- canonical external-store identifier;
- last committed ledger position and checksum;
- adapter/schema version; and
- lifecycle and recovery flags.

The larger identity store lives outside the save in a Dagmay-managed location. Saving creates a coordinated checkpoint. Loading verifies both sides. A mismatch presents explicit choices such as use last matched checkpoint, open read-only diagnostics, or cancel Dagmay activation; it does not silently merge histories.

Caravans, pods, cryptosleep, world pawns, temporary map removal, and death must not be confused. Lifecycle mapping requires dedicated tests for each supported transition.

## UI surfaces

### Implemented 0.1E settings/Observer surface

RimWorld's Mod Settings route now provides:

- an explicit provider-dispatch pause that does not stop event capture or durable queueing;
- adjustable hard limits for requests per game session, requests per rolling hour, estimated tokens per UTC day, and background-reflection heartbeat;
- provider/model, queue, audit, usage, and three-store health summaries;
- explicit colonist enroll, pause, and resume operations; and
- a locked, two-step-confirmed private view of seed facts, affect, memories, and successfully committed reflection summaries.

New 0.1E settings keep provider dispatch paused and future-colonist auto-enrollment disabled. Existing 0.1D.1 individuals remain the same individuals after upgrade. Pausing one stops its ordinary observation and future reflection without deleting its ID, lineage, memories, or lifecycle record. Death archival remains continuity-critical even for a paused individual. The interface caches immutable snapshots for one second while open; it never holds Verse objects in Core or waits on the provider.

This is an out-of-world research/debugging route, not the ordinary pawn mind tab or colony overview described below. Those disclosure-filtered normal-play surfaces remain required.

### Pawn mind tab

An ordinary projection may show a self-description, selected disclosed concerns, notable accessible memories, current goals when shareable, and service status. It does not show private confidence tables, raw prompts, hidden memories, or contradiction diagnostics.

### Colony overview

Shows enrolled colonists, lifecycle, last processed event, pending reflection indicator, provider/offline status, storage health, and actionable errors. It should avoid turning private internal state into colony-wide knowledge.

### Observer Mode

Uses separate controls and data projections defined in `04_Ethics_and_Observer_Mode.md`.

## RimTalk compatibility

RimTalk v1.0.14 is the preferred optional dialogue integration because its source provides a public add-on API for context variables, hooks, prompt entries, and injected prompt sections. Dagmay should:

- avoid requiring RimTalk for identity, memory, persistence, or reflection;
- isolate compatibility code from both core and ordinary adapter logic;
- detect presence/version without hard failure;
- avoid patching the same methods when a safer public integration point exists;
- document which system owns dialogue generation and context;
- prevent duplicate provider calls and duplicate memories; and
- test enabled, disabled, and removal-after-save cases.

A future bridge may supply disclosure-filtered Dagmay context to RimTalk or ingest completed conversations as objective utterance events and listener-specific perceptions. That bridge must not expose Observer-only data. RimTalk provider history or display history cannot become canonical memory, and live RimWorld objects must be snapshotted on the game thread before asynchronous processing.

No documented public completed-utterance callback was found in the reviewed v1.0.14 source. Capture therefore remains a version-gated investigation; prefer an upstream event or stable public record before a private-method patch. The evidence and license boundary are recorded in `18_External_Mod_Reference_Review.md`.

Version 0.1D deliberately has no RimTalk assembly reference, API call, copied implementation, or runtime requirement. RimTalk can later become one optional RimWorld “mouth” and observation source. It cannot own Dagmay identity, intrinsic memory, reflection, provider choice, or cross-world continuity. Removing RimTalk must leave the individual and its archive intact.

## DLC and other mods

Optional content is mapped through capability checks and namespaced fields. Dagmay should not treat a missing DLC type as corrupt data. Odyssey and other DLC-specific events can be added after the base event pipeline is stable.

Compatibility failures should disable the narrow affected event source, report diagnostics, and preserve the remainder of Dagmay when safe.

## No Version 0.1 action adapter

Interfaces may reserve a future concept of environment actions for architectural planning, but `Dagmay.RimWorld` Version 0.1 contains no implementation capable of issuing or influencing pawn actions. Adding one is a Version 0.2/0.3 decision and requires a separate review.
