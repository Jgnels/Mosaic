# Dagmay Repository Guidance

These rules apply to every contributor and coding agent working in this repository.

## Start and routing

- Read `START_HERE.md` for the owner/agent operating map.
- Read `README.md` for current capability, `docs/03_Roadmap.md` for gates, `docs/10_Decision_Log.md` for accepted decisions, and the latest section of `docs/15_Verification_Record.md` for evidence.
- Before architecture, memory, provider, ethics, or adapter changes, read the corresponding numbered design document. Do not create duplicate project-state, architecture, decision, or roadmap files.
- The owner has no coding experience. Perform code and file work yourself. Give the owner one exact test only when Windows, credentials, or gameplay judgment is required.

## Project invariant

An individual is defined by the continuity of its identity through time, not by the language model generating a particular thought.

## Current capability boundary

Version 0.1 is Observer-only. Do not add code that starts jobs, changes priorities, alters pawn state, or otherwise influences RimWorld behavior. A future action interface may be designed only after the Version 0.1 gate and a separate owner-approved safety decision.

## Dependency boundary

- `Dagmay.Core` must not reference RimWorld, Verse, Unity, Harmony, Google SDKs, or another game/provider implementation.
- `Dagmay.Providers` implements interfaces owned by Core.
- `Dagmay.RimWorld` converts game state into immutable Core contracts.
- Background code never retains live RimWorld or Unity objects.

## State integrity

- Treat model output as untrusted input.
- Validate schema, identity, base version, references, ranges, lifecycle, and replay identifiers before mutation.
- Apply a complete proposal atomically or apply none of it.
- Never silently rewrite an event, memory source, identity ID, or lineage.
- Never request or store hidden model chain-of-thought. Store concise decision summaries and evidence references.

## Verification language

Use these statuses literally: **Designed**, **Implemented**, **Compiled**, **Tested in isolation**, and **Tested in RimWorld**. Do not promote a status without evidence.

## Required checks

Before handing off changes:

1. run `python tools/static_verify.py`;
2. run `dotnet run --project Dagmay.Tests/Dagmay.Tests.csproj` when a .NET 8+ SDK is available;
3. run `dotnet run --project Dagmay.IntegrationHarness/Dagmay.IntegrationHarness.csproj` for changes that touch identity, memory, persistence, reflection scheduling, provider resilience, or relationships;
4. build the RimWorld project only against the user's locally installed game assemblies;
5. record anything not run and why; and
6. update the decision log when behavior or architecture changes.

On the owner's Windows computer, prefer `tools/dev-loop.ps1`. It records full and structured build evidence before optionally invoking the guarded installer. A build failure must stop before installation. After a RimWorld test, use `tools/collect-logs.ps1` and inspect `artifacts/Dagmay-diagnostics-latest.txt`.

## Local installation boundary

- Only `tools/install.ps1` may replace the locally installed `RimWorld/Mods/Dagmay` package during the automated loop.
- Installation requires a successful package validation, an exact destination, a prior-package backup, and rollback on failure.
- Never delete or rewrite RimWorld saves or `AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Config/Dagmay` as part of build or installation.
- Never claim PowerShell automation was executed in an environment where PowerShell was unavailable; distinguish static inspection from a Windows run.

## Handoff format

Lead with the outcome. Name material files changed, verification actually performed, verification not performed, and the single next human action if one remains. Keep raw logs in evidence artifacts instead of flooding the main collaboration thread.

## Secrets and data

API keys stay in environment variables or a local secret store. Never commit keys, real user data, Dagmay runtime identity stores, diagnostics, or exports.
