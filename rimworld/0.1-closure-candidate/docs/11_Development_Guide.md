# 11 — Development Guide

**Status:** Updated for Version 0.1G using the established 0.1F development automation

This guide is written so the project owner does not need to write or edit code. The implementation partner should perform repository changes; the owner only installs prerequisites, launches commands, starts RimWorld, and reports results.

## What exists now

Version 0.1F retains the 0.1E Observer/runtime capabilities and adds a human- and agent-readable development loop. Source includes:

- a four-project .NET solution;
- game-independent identity, affect, event, perception, memory, provider, lifecycle, validation, and scheduling contracts;
- in-memory event and identity stores for early tests;
- a deterministic fake provider;
- a real, replaceable Google AI Studio REST transport plus deterministic fake and offline modes;
- a RimWorld identity service that scans free colonists without changing them;
- checksummed external identity archives, atomic replacement, backup recovery, and a save-linked manifest;
- rename continuity and death archival;
- bounded polling observation and deterministic local memory encoding;
- a hash-chained experience journal and memory retrieval foundation;
- a durable reflection queue and checksummed private audit sidecar;
- strict structured-proposal decoding, validation, bounded atomic commit, and pending-commit recovery;
- conservative hourly/daily budgets, retry limits, exponential backoff, and a circuit breaker;
- a hard per-session attempt cap and persisted provider pause;
- selective enroll/pause/resume controls that preserve identity and lineage;
- an out-of-world settings/health overview plus two-step-locked private Observer inspection;
- executable contract tests without a third-party test framework;
- a deterministic `Dagmay.IntegrationHarness` covering long-history continuity, durable provider-outage recovery, checkpoint rollback recovery, and asymmetric social provenance;
- a static dependency/safety verifier;
- automatic build transcript, structured result, package, and SHA-256 evidence;
- guarded backup/install/rollback for the exact local Dagmay mod directory;
- redacted current/previous RimWorld diagnostic collection;
- a clean source packager; and
- durable owner/agent instructions plus a project-scoped Codex agent.

Version 0.1F Core and providers compile against `.NET Standard 2.0`, tests compile against `.NET 8`, and the complete adapter source compiles against a local minimal RimWorld/Verse/Unity API surface under `.NET Standard 2.0`, all with warnings as errors. All 36 isolation tests pass. Every PowerShell file parses under PowerShell 7.4.6, and the portable source-packaging, redaction, synthetic install, backup, and failed-build gating paths execute in this Linux workspace. Those checks do not prove Windows path/permission behavior, the actual `.NET Framework 4.7.2` adapter target, compilation against the owner's proprietary RimWorld assemblies, or in-game loading. Version 0.1E remains the latest owner-compiled and RimWorld-tested baseline: its focused UI, disclosure-lock, continuity, fake-session-cap, and final offline reload passed with no reported Dagmay pause, stutter, red error, or unexpected behavior.

## Windows prerequisites

Do not install these until we are ready to run the first build together.

1. A current RimWorld 1.6 installation through Steam. The verified owner build used 1.6.4871.
2. The .NET 8 SDK or newer.
3. A .NET Framework 4.7.2 targeting pack or compatible Visual Studio Build Tools if the RimWorld project requests it.
4. PowerShell, included with Windows.
5. Python 3 is optional; it runs extra static checks. The C# build and tests do not require Python.

The `net472` RimWorld target has compiled against the owner's installed 1.6 assemblies. No proprietary RimWorld DLL is included in Dagmay.

## One-command development loop

Open PowerShell in the `Dagmay` folder and run:

```powershell
.\tools\dev-loop.ps1
```

The script:

1. runs static checks when Python is available;
2. builds `Dagmay.Core`;
3. builds `Dagmay.Providers`;
4. runs the executable contract tests;
5. locates the default Steam RimWorld installation;
6. builds `Dagmay.RimWorld` against the locally installed game DLLs; and
7. creates the package for the current `DagmayBuildInfo.Version` (0.1G for this candidate) and its SHA-256;
8. retains a full transcript at `artifacts\Dagmay-build-latest.txt`; and
9. retains a structured summary at `artifacts\Dagmay-build-result-latest.json`.

If RimWorld is installed somewhere else:

```powershell
.\tools\dev-loop.ps1 -RimWorldPath "D:\SteamLibrary\steamapps\common\RimWorld"
```

To build only Core, providers, and tests:

```powershell
.\tools\dev-loop.ps1 -SkipRimWorld
```

To build and safely install into the default Steam copy:

```powershell
.\tools\dev-loop.ps1 -Install
```

Add `-Launch` only when RimWorld should start after every build, test, package, validation, backup, and installation step succeeds. A failed build never invokes the installer. See `docs/22_V0.1F_Development_Automation.md` for the safety contract and evidence map.

## RimWorld 0.1F check

Use the single test given by the implementation agent after the Windows development loop passes. Do not manually replace files when `tools\install.ps1` is available; it validates and backs up the previous `RimWorld\Mods\Dagmay` package without touching saves or continuity sidecars.

The package must contain `Dagmay.RimWorld.dll`, `Dagmay.Core.dll`, and `Dagmay.Providers.dll`. After starting RimWorld and loading the dedicated test save, look for:

```text
[Dagmay] Version 0.1F loaded. Restricted Observer interface active; no pawn-control code is present.
```

Version 0.1F records the same selected experiences, deterministic private memories, and provider-neutral bounded reflections as 0.1E. Its Mod Settings route retains system health, budgets, enrollment, and restricted private inspection. It still has no ordinary pawn mind tab, dialogue observation, local-model provider, RimTalk dependency, or pawn control.

## How to report a failure

After a build failure, attach `artifacts\Dagmay-build-latest.txt`; its paired JSON result tells an agent which stages completed. After closing RimWorld, run:

```powershell
.\tools\collect-logs.ps1
```

Attach `artifacts\Dagmay-diagnostics-latest.txt`. It includes current and previous Dagmay lines plus nearby noteworthy context and applies documented redaction. Also state whether the failure happened during build, installation, startup, mod enable, or save/load and whether RimTalk was enabled.

The owner should not attempt to edit project files or guess at a repair.

## Reflection mode and API keys

No API key is needed for offline or deterministic fake testing. Runtime mode is explicit and defaults to `offline`.

Use the hidden-input helper rather than putting a key in a command, source file, or chat message:

```powershell
.\tools\configure-reflection.ps1 -Mode Google -Model "gemini-3.1-flash-lite"
```

The helper asks for the key without displaying it and stores it in the Windows user environment variable `DAGMAY_GOOGLE_API_KEY`. It stores the selected model in `DAGMAY_GOOGLE_MODEL` and mode in `DAGMAY_REFLECTION_MODE`. Version 0.1F reads the current user values directly, even when Steam inherited an older process value. Close RimWorld completely and relaunch it normally from Steam after changing mode. The key is never placed in Dagmay source, the RimWorld save, the identity/experience/reflection sidecars, provider request bodies, or logs. The Windows environment variable itself is per-user configuration, not encrypted vault storage.

Selecting Google makes the provider available but does not override 0.1F's in-game safety pause. The owner must also open **Options → Mod Settings → Dagmay**, review the provider warning and budgets, and uncheck **Pause provider dispatch** before a network call can occur.

For the no-network end-to-end test:

```powershell
.\tools\configure-reflection.ps1 -Mode Fake
```

To stop all provider calls while continuing local event capture:

```powershell
.\tools\configure-reflection.ps1 -Mode Offline
```

To also remove Dagmay's stored Google key from the Windows user environment:

```powershell
.\tools\configure-reflection.ps1 -Mode Offline -RemoveStoredGoogleKey
```

## Status promotion checklist

- Source present: **Implemented**.
- `dotnet build` succeeds: **Compiled**.
- contract executable reports zero failures: **Tested in isolation**.
- RimWorld emits the load-health message and the manual scenario passes: **Tested in RimWorld**.


## Version 0.1G ordinary Mind-view check

Build/install with the existing fail-closed automation:

```powershell
.\tools\dev-loop.ps1 -Install
```

After a successful install, launch RimWorld normally and load a copied test save. In **Options → Mod Settings → Dagmay**, keep Restricted Observer locked and click **Mind** beside an enrolled individual. The view should show only coarse current state, grounded identity facts, and explicitly shareable memories. It must not show raw affect numbers, individual/lineage IDs, provider diagnostics, private memories, or reflection payloads.

A newly joined colonist may appear as `NotEnrolled` when automatic enrollment is disabled. That is expected and is not a continuity failure. Enrolling the colonist explicitly should create a new Dagmay identity without changing any existing individual's ID or lineage.

After the scenario, close RimWorld and run:

```powershell
.\tools\collect-logs.ps1
```

Attach `artifacts\Dagmay-diagnostics-latest.txt` for combined continuity, provider, queue, storage, and event evidence.
