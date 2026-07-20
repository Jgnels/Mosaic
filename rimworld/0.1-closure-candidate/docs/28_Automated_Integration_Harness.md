# 28 — Automated Integration Harness

**Status:** Implemented, Compiled, and Tested in isolation on Windows

## Purpose

The integration harness reduces the amount of manual RimWorld play required for engineering verification. It exercises Dagmay's real Core, persistence, scheduling, and deterministic fake-provider implementations without requiring proprietary RimWorld assemblies or a graphical game session.

It does **not** replace live RimWorld adapter testing or the owner's judgment about psychological coherence. It is a deterministic engineering layer between unit/contract tests and human gameplay.

## Current scenarios

`Dagmay.IntegrationHarness` runs six end-to-end scenarios:

1. **LongHistoryContinuityAndPersistence** — simulates 64 individuals and 1,280 persisted experiences, then verifies journal hash-chain integrity, identity archive reload, memory/event counts, and stable IndividualId/LineageId continuity.
2. **ProviderOutageDurableQueueRecovery** — simulates retryable provider failure for 12 individuals, persists the reflection queue, reloads it, recovers with the deterministic fake provider, validates proposals, commits bounded state replacements, and verifies the queue drains without identity replacement.
3. **CheckpointRollbackForwardRecovery** — simulates an older save checkpoint with a newer verified external journal, loads only the checkpoint prefix, explicitly adopts the verified suffix, and confirms the adopted head and identity continuity. A mismatched checkpoint hash is distinguishable and is not treated as safe forward recovery.
4. **SocialPerspectiveAndPrivacy** — persists asymmetric social memories, verifies counterpart IndividualId provenance and RelationshipSensitive privacy, and confirms two people can hold different relationship dimensions toward one another.
5. **PersistenceTorture** — runs six save/reload cycles with a non-empty queue, provider outage, new skill/social experiences during outage, one-record-ahead journal recovery, and uniqueness/no-loss assertions for identity, experience, memory, task, source-link, audit, and request records.
6. **FailureIsolation** — exercises unavailable, invalid, timeout, rate-limit, provider-error, cancellation, malformed-output, interrupted-commit, mismatch, and pending-shutdown paths; it proves failures cannot mutate canonical identity/history and persists retry/quarantine evidence across reload.

## Normal execution

The standard Windows development loop now runs the harness automatically after the existing contract tests and before the RimWorld build:

```powershell
.\tools\dev-loop.ps1
```

A successful run writes:

```text
artifacts\Dagmay-integration-latest.json
```

and adds `integration-harness` to `completedStages` in `Dagmay-build-result-latest.json`.

The harness can also run independently:

```powershell
dotnet run --project Dagmay.IntegrationHarness\Dagmay.IntegrationHarness.csproj --configuration Release
```

The focused 0.1L entry point writes a dedicated machine-readable artifact:

```powershell
.\tools\run-persistence-torture.ps1
```

Output: `artifacts\Dagmay-persistence-torture-latest.json`.

The focused 0.1M entry point is:

```powershell
.\tools\run-failure-isolation.ps1
```

Output: `artifacts\Dagmay-failure-isolation-latest.json`.
To run one scenario:

```powershell
dotnet run --project Dagmay.IntegrationHarness\Dagmay.IntegrationHarness.csproj --configuration Release -- --scenario CheckpointRollbackForwardRecovery
```

## Agent workflow

A repository-aware coding agent should treat the harness as mandatory pre-handoff evidence for changes touching identity, memory, persistence, reflection scheduling, provider resilience, or relationship provenance. The preferred autonomous loop is:

```text
edit
→ static verification
→ Core/Providers build
→ contract tests
→ integration harness
→ RimWorld adapter build against local assemblies
→ package/install only after all prior stages pass
```

The agent may diagnose and repair failures in that loop without owner intervention. The owner should only be required for live gameplay, credentials entered through the existing hidden-input helper, or subjective product/creative decisions.

## Boundary: what remains manual

The harness cannot prove live RimWorld patch behavior, mod interoperability, GUI behavior, game-save serialization callbacks, or subjective continuity. Those remain live integration tests. A future purpose-built in-game test mode may expose machine-readable adapter fixtures and controlled Dev Mode scenarios, but Version 0.1 remains Observer-only and will not add pawn-control automation merely for testing.
