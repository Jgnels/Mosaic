# 08 — Testing Strategy

**Status:** Living strategy; 0.1E contract subset implemented

## Evidence language

Dagmay uses these statuses exactly:

| Status | Meaning |
| --- | --- |
| Designed | Documented but not necessarily present in code. |
| Implemented | Code exists; build success is not implied. |
| Compiled | The stated configuration built successfully. |
| Tested in isolation | Automated or harness tests ran outside RimWorld and passed. |
| Tested in RimWorld | A recorded in-game scenario ran on the stated game/mod configuration and passed. |

Evidence is configuration-specific. “Tested in RimWorld” on one game version does not imply compatibility with another.

## Test layers

### Unit tests

Fast tests for deterministic domain behavior:

- identity creation and stable IDs;
- salience and affect rules;
- memory promotion, decay, and retrieval scoring;
- belief confidence and contradiction handling;
- lifecycle transitions;
- queue ordering, fairness, and budgets;
- validation and atomic mutation; and
- redaction.

### Contract tests

Run every provider and environment mapping against shared contracts:

- supported schema versions;
- normalized error categories;
- cancellation and timeout behavior;
- capability negotiation;
- model output envelopes; and
- event/perspective invariants.

The deterministic fake provider is the reference implementation for success and failure cases.

Version 0.1E adds executable contracts proving that a hard per-session limit blocks the next transport without deleting durable work, pausing one individual removes only that individual's waiting tasks, an explicitly preserved in-flight task remains available for safe quarantine, and provider token metadata is counted once even though several audit stages share a request ID.

### Persistence tests

- ledger append and replay;
- snapshot/reload equivalence;
- atomic commit under forced interruption;
- checksum failure and last-known-good recovery;
- duplicate and out-of-order events;
- schema migration and rollback behavior;
- save-manifest/external-store mismatch;
- death archive and export integrity; and
- import without accidental activation.

### Property and invariant tests

Generate many event/state sequences and assert:

- identity and lineage IDs never change through ordinary reflection;
- no accepted mutation references nonexistent evidence;
- invalid output produces no state change;
- state versions increase monotonically;
- at most one normal active lineage exists;
- replay is deterministic for deterministic inputs;
- facts are not overwritten by beliefs;
- a dead/archived individual cannot resume ordinary tasks; and
- provider/model choice never becomes the identity key.

### Integration tests outside RimWorld

`Dagmay.IntegrationHarness` now provides the first executable simulation layer. It feeds normalized events into the real Core, persistence, scheduler, validation, and deterministic fake-provider implementations. The initial scenarios cover 64-individual long-history continuity, a persisted provider-outage/retry/recovery cycle, verified forward-only experience-journal rollback recovery, and asymmetric relationship-sensitive social provenance. The standard Windows development loop runs this harness automatically and records `artifacts/Dagmay-integration-latest.json`.

Future expansion should add combat bursts, cancellations, queue saturation, model switches, corrupted-store matrices, and longer simulated histories.

### RimWorld adapter tests

Where legally and technically practical, adapter mapping logic should consume extracted plain-data fixtures without loading game assemblies. Live patch points and UI require manual or purpose-built in-game testing; RimWorld assemblies are referenced locally and never redistributed.

The 0.1E manual sequence must first run offline, inspect health and the known 0.1D.1 IDs, exercise the two-step Restricted Observer lock, pause/save/reload/resume one individual without changing its ID or lineage, and only then run an optional provider test capped at two session attempts. The owner must report responsiveness separately from functional correctness.

## Golden continuity scenarios

Maintain small, reviewable histories with expected invariants rather than exact prose:

1. **Care after injury:** repeated care should increase accessible positive evidence about the caregiver, but one act should not force permanent trust.
2. **Betrayal after trust:** a severe contradictory event should create conflict, change trust materially, and preserve the earlier relationship history.
3. **False accusation:** the individual may believe a false claim while the ledger retains contrary facts unavailable to it.
4. **Model replacement:** provider output wording may change; ID, lineage, source history, commitments, and bounded mutation rules remain stable.
5. **Offline crisis:** events persist during outage, critical items outrank background reflection on reconnection, and occurrence-time context remains intact.
6. **Death and revival:** death archives and halts normal work; a later supported RimWorld revival follows an explicit continuity policy.

Exact generated wording is not a golden assertion unless testing a prompt template. Prefer semantic fields, evidence links, ranges, and allowed mutations.

## Adversarial model-output tests

The fake provider and captured provider fixtures must exercise:

- invalid JSON and wrong schema version;
- missing required fields and unknown operations;
- out-of-range emotion or confidence values;
- nonexistent event/person references;
- identity, lineage, lifecycle, or permission changes;
- stale base state and replayed response IDs;
- oversized text and excessive mutation count;
- partial/truncated output;
- plausible prose paired with invalid structure; and
- instruction injection placed in pawn names, backstories, mod text, memories, and player text.

Every case must leave canonical state unchanged unless the proposal is fully valid.

## Performance tests

The harness records throughput, queue depth, memory, storage growth, retrieval latency, provider concurrency, and time-to-critical-reflection. Live tests record main-thread callback timings and frame/tick impact.

Initial scenarios:

- 50 individuals at steady low event volume;
- a colony-wide combat burst;
- 24 hours of provider outage followed by recovery;
- provider latency from immediate to timeout;
- queue at configured maximum;
- save/unload while requests are active; and
- semantic-index rebuild from canonical records.

Targets should be set after a baseline on the reference PC. The non-negotiable requirement is architectural: no callback waits for network, model, or bulk I/O, and queues remain bounded.

## Manual RimWorld matrix

Each run records:

- date and tester;
- Dagmay commit/build identifier;
- RimWorld and DLC versions;
- mod list and load order;
- RimTalk presence/version;
- provider/model or offline mode;
- Dagmay configuration;
- save/scenario identifier;
- expected and actual result;
- screenshots/log bundle location; and
- status: pass, fail, blocked, or inconclusive.

Minimum release runs:

1. Dagmay alone plus required framework dependencies;
2. all DLC enabled;
3. RimTalk enabled;
4. RimTalk removed from a copied save;
5. provider online, offline at startup, and lost during play;
6. save/load with pending work;
7. colonist rename, caravan departure/return, cryptosleep, downing, rescue, and death;
8. 40+ colonists under ordinary activity and combat burst; and
9. corrupted copy of identity data with recovery workflow.

## Release evidence

A Version 0.1 release candidate needs:

- build and automated-test output;
- dependency and schema versions;
- Harmony patch inventory;
- manual matrix results;
- known failures and unsupported cases;
- performance summary;
- proof that no action-control path is implemented; and
- a status update in the README and decision log.
