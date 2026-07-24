# Mosaic v0.2 Overnight Audit

**Audit date:** 2026-07-24
**Branch:** `codex/v0.2-overnight-prep-20260724`
**Base:** `457f1815d625f7a121f444d972d4880e70c805cb`
**Status:** v0.2 pre-alpha; not released or Gate-certified

## Current architecture

Mosaic uses a ports-and-adapters boundary:

- Core owns model-independent identity, lineage, facts, perceptions, subjective memories,
  relationships, affect, scheduling, validation, persistence contracts, and read-only projections.
- RimWorld is an environment adapter. It maps game observations to immutable contracts, owns no
  durable individual identity, and currently exposes Observer-only behavior.
- Providers are replaceable inference modules. Their output is an untrusted proposal and cannot
  mutate canonical state without schema, evidence, continuity, boundedness, and provenance checks.
- Identity archive, hash-chained experience journal, reflection queue/audit store, and RimWorld
  save manifest remain distinct but checkpoint-coupled persistence surfaces.
- Ordinary presentation and Observer/debug projections are separate from canonical cognition.

## Implemented capabilities

- Stable individual and lineage identifiers with explicit environment bindings.
- Deterministic event/perception/memory encoding and provenance.
- Atomic identity and reflection sidecars plus a hash-chained experience journal.
- Fail-closed checkpoint mismatch, backup recovery, pending-commit reconciliation, and
  read-only storage policies.
- Bounded/fair reflection queues, budgets, deterministic fake provider, and normalized provider
  errors.
- Strict reflection JSON decoding and atomic bounded mutation validation.
- Observer-only RimWorld adapter, settings, health diagnostics, and offline runtime mode.
- External-event admission, action attribution, story lifecycle/milestones, temporal facts,
  appraisal contracts, and decision-trace foundations.
- Six deterministic integration scenarios and Gate 3 offline-soak presets.

## Gate 3 lessons carried forward

1. A fresh save can lag newer external sidecars; contradictory checkpoints must fail closed.
2. Post-load reflection writes must wait for an intentional RimWorld save checkpoint.
3. Unchanged Save As operations must not advance canonical generations.
4. Empty reflection audit history is not evidence of read-only storage.
5. Temporary environment absence must preserve the same individual and lineage.
6. Corruption tests require isolated StoreIds; healthy stores must never adopt disposable state.
7. Runtime evidence must distinguish process launch, load, enrollment, save, and reload continuity.
8. Reproducibility requires a fresh Git worktree, not only a populated long-lived checkout.

The remaining two-hour live soak is `OWNER_ACTION_REQUIRED`; it is not complete and is not a Gate 3
PASS. The prepared local harness is resumable and keeps providers/local models off.

## Overnight findings and completed work

### P0 — Fresh-checkout build reproducibility

- Problem: `.gitignore` ignored every directory named `Diagnostics`. Two required C# files existed
  in the populated certified checkout but were absent from Git, so a fresh worktree could not
  compile the contract project.
- Invariants: repository provenance, reproducible verification, frozen 0.1 immutability.
- Resolution: track the exact v0.2 diagnostic sources and narrowly unignore only C# source under
  `Dagmay.Core/Diagnostics` and `Dagmay.RimWorld/Diagnostics`.
- Compatibility: no runtime or serialization behavior change.
- Testability: fresh worktree compile plus the full contract suite.
- Risk: low.

### P0 — Cross-individual private-memory boundary

- Problem: `ReflectionContextBuilder` capped private memories at 20 but accepted a mixed-owner list,
  permitting accidental inclusion of another individual's private memory.
- Invariants: private state separation, individual isolation, bounded context, untrusted provider
  boundary.
- Resolution: filter by `OwnerId` before applying the existing 20-memory cap; add an executable
  contract proving foreign exclusion and cap preservation.
- Compatibility: no schema, persistence, provider, package ID, or namespace change.
- Testability: deterministic offline contract.
- Risk: low.

### P0 — Cross-individual reflection evidence and queue isolation

- Problem: persistent queue coalescing trusted caller-generated keys to include the owner, the
  in-memory queue treated matching keys across owners as duplicates, and context assembly did not
  verify that source events named the target individual.
- Invariants: individual isolation, evidence provenance, bounded authority, and fail-closed model
  input.
- Resolution: scope both queue key paths by `IndividualId`; reject foreign source events before a
  model request can be built; retain shared events when the target is one of their subjects.
- Compatibility: no schema or persistence-format change; existing same-owner cross-task-kind
  coalescing remains intact.
- Testability: three deterministic offline contracts.
- Risk: low.

### P1 — Coalesced reflection task consistency

- Problem: persistent queue coalescing upgraded the priority and evidence of higher-priority work
  but retained the lower-priority task kind, producing a semantically inconsistent queued task.
- Invariants: deterministic scheduling, auditable work classification, and bounded coalescing.
- Resolution: when new work wins the priority comparison, upgrade its task kind together with its
  priority; otherwise preserve the existing kind.
- Compatibility: no persistence schema change; existing queue records remain readable.
- Testability: the persistent queue merge contract now checks the upgraded task kind explicitly.
- Risk: low.

### P1 — Ordinary disclosure privacy boundary

- Problem: disclosure behavior existed, but executable coverage did not prove the
  relationship-sensitive class or the exact accessibility boundary.
- Invariants: ordinary presentation must not leak private or relationship-sensitive memories;
  disclosure remains deterministic and model-independent.
- Resolution: add a contract covering all privacy classes, a shareable memory immediately below
  the threshold, and a shareable memory exactly at the threshold.
- Compatibility: test-only; no runtime, schema, or persistence change.
- Testability: deterministic offline contract with fixed timestamps and no provider.
- Risk: none to runtime behavior.

### P1 — Bounded presentation context

- Problem: presentation item text was bounded, but packet item counts and per-item evidence counts
  were not; packets also accepted null items.
- Invariants: read-only presentation remains bounded, evidence-grounded, and safe for optional mod
  consumers.
- Resolution: cap packet items and per-item evidence IDs at 100 and reject null packet items.
- Compatibility: no persistence or serialization change; ordinary bounded packets are unaffected.
- Testability: deterministic rejection contracts for 101 items, 101 evidence IDs, and a null item.
- Risk: low.

### P1 — Deterministic rebuild and context ordering

- Problem: equal-score memories and equal-time source events inherited caller/insertion order,
  allowing reload/rebuild order to change bounded selection or context ordering.
- Invariants: deterministic replay, provider-independent continuity, and reproducible context.
- Resolution: use stable `MemoryId` and `EventId` tie-breakers after the existing semantic sort
  fields.
- Compatibility: no stored data or scoring change; only exact ties are affected.
- Testability: two deterministic offline contracts.
- Risk: low.

### P1 — Source-tracking verifier guard

- Problem: the populated checkout could mask source excluded by Git, while explicit project source
  links were not checked by static verification.
- Resolution: validate explicit `<Compile Include>` paths in every project and, when Git metadata is
  available, compare local C# source with the tracked inventory. Source archives without Git
  metadata remain supported.
- Testability: direct clean pass plus temporary negative fixtures for both failure modes.
- Risk: low; tooling only.

## Candidate v0.2 work

| Order | Candidate | Dependency | Risk | Testability | Compatibility / provenance |
| --- | --- | --- | --- | --- | --- |
| 1 | Finish the owner-operated Gate 3 long soak | Human GUI operation and prepared harness | Medium runtime risk | Direct logs, samples, hashes, save/sidecar verification | No code reuse; blocks declaring Gate 3 complete |
| 2 | Add a clean-clone/source-tracking verification guard | Completed | Low | Both negative paths and clean tree passed | Tooling only; no runtime change |
| 3 | Deterministic tie-breaking for equal-score memory retrieval | Completed | Low | Rebuild in opposite insertion orders passed | Core behavior only; no format change |
| 4 | Bounded presentation/context assembly tests across privacy classes | Completed | Low | Privacy/accessibility and collection-boundary contracts passed | Original code; no external dependency |
| 5 | Gate 4 compatibility fixtures and adapter seams | Gate 3 runtime completion and selected mod versions | Medium | Extracted plain-data fixtures plus manual matrix | External APIs are optional and version-gated |
| 6 | RimTalk bridge design/ADR | Gate 4, owner product decision, license compatibility review | Medium/high | Absent-mod, timeout, exception, duplicate-call tests | API interoperability preferred; no copied source |
| 7 | Semantic retrieval experiment | Explicit provider-neutral index/rebuild policy | Medium | Offline lexical baseline and deterministic rebuild tests | No provider or dependency chosen |

## Tasks rejected or deferred

- No live provider, local model, network inference, or credential work: provider experiments remain
  paused and are unrelated to Gate 3.
- No RimWorld GUI automation: Computer Use is unreliable for this installation; blind interaction
  would invalidate evidence and waste owner allocation.
- No broad rewrite, serialization migration, namespace/package-ID change, or identity replacement.
- No autonomous action, pawn control, Free Will integration, or RiMind action-module reuse.
- No RimTalk implementation before Gate 3/Gate 4 and a dedicated CC BY-NC-SA compatibility review.
- No changes to `rimworld/0.1-closure-candidate/`.

## Recommended order

Jeff should run the prepared two-block long soak when available. In parallel only after RimWorld is
closed, the next safest engineering task is a clean-clone/source-tracking guard, followed by
deterministic memory-retrieval ordering. Compatibility and dialogue bridges should wait for the
runtime foundation and explicit license/product decisions.

## Verification

- Static verification: PASS, 98 C# files.
- Contract tests: PASS, 78 executed, 0 failed.
- Integration harness: PASS, 6 scenarios and 603 assertions.
- Core, Providers, and RimWorld adapter Release builds: PASS, 0 warnings and 0 errors.
- Non-installing package preflight: PASS, 6 entries and no prohibited runtime artifacts.
- Package SHA-256:
  `2d5b423ff4b70ca392dd2c7f45826afd7751e5f14919221d529777a2ae817685`.
- No provider, local model, install, save mutation, or RimWorld launch occurred.
