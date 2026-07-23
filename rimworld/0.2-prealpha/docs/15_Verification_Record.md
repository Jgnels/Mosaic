# 15 — Verification Record

## 2026-07-16 — Version 0.1A foundation

### Source scope

- `Dagmay.Core`
- `Dagmay.Providers`
- `Dagmay.Tests`
- passive `Dagmay.RimWorld` source and package metadata
- documentation and build tooling

### Static verification

**Status:** Passed

`python tools/static_verify.py` checked 37 C# files plus repository structure, project/About XML, every JSON document/schema, README links, dependency boundaries, Observer-only forbidden calls, explicit pawn-control denial, secret patterns, and unexpected binaries.

Static verification explicitly does not count as compilation.

### Compilation

**Status:** Pass for Core/providers/tests; RimWorld pending

The normal project files were restored and built by invoking MSBuild from the official temporary .NET SDK:

- `Dagmay.Core`: `netstandard2.0` Release build passed;
- `Dagmay.Providers`: `netstandard2.0` Release build passed against Core; and
- `Dagmay.Tests`: `net8.0` Release build passed against Core and Providers.

As an independent syntax/type check, the official Roslyn compiler was also invoked directly:

- compiler: Microsoft C# 4.11.0
- reference framework: .NET 8.0.29
- warnings treated as errors
- nullable analysis enabled
- `Dagmay.Core.dll`: compiled
- `Dagmay.Providers.dll`: compiled against Core
- `Dagmay.Tests.dll`: compiled against Core and Providers

The direct pass proves C# syntax/type compatibility in its reference environment. The MSBuild pass additionally proves the intended `netstandard2.0` Core/provider targets and `net8.0` test target. The `net472` RimWorld target remains unverified.

### Isolation tests

**Status:** Passed — 16 executed, 0 failed

Covered:

1. strong ID round-trip and empty-ID rejection;
2. bounded/composable affect;
3. accepted weighted continuity profile;
4. rename preserving identity and lineage;
5. death blocking ordinary mutation and revival preserving lineage;
6. event-ledger deduplication;
7. differing perspectives retaining shared fact provenance;
8. atomic and replay-safe validated mutation;
9. rejected ungrounded output making zero state change;
10. bounded queue protecting critical work;
11. selective enrollment rejecting half-created individuals;
12. interaction origins preventing external-operator impersonation;
13. asymmetric relationship state;
14. deterministic fake-provider success;
15. normalized fake-provider failure; and
16. Google scaffold never claiming unimplemented transport succeeded.

### Not verified

- the full `.sln` build including RimWorld;
- `net472` RimWorld compilation;
- RimWorld assembly loading;
- any Harmony patch or game event, because none is implemented;
- live Google/local inference;
- disk persistence, semantic retrieval, UI, or Observer Mode; and
- any pawn behavior, which Version 0.1 intentionally forbids.

The next promotion requires the Windows RimWorld build in `11_Development_Guide.md` against the owner's local game installation.

## 2026-07-16 — Owner RimWorld smoke test

**Status:** Passed — user-reported

The owner built and installed the passive Version 0.1A package, enabled it in RimWorld, and ran it in a test colony with approximately 50 additional mods enabled. No loading or gameplay issues were observed.

This confirms practical co-existence with the owner's current mod list. It does not yet verify identity creation, event capture, persistence, UI, live model calls, or pawn control; those features are not present in Version 0.1A.

## 2026-07-16 — Version 0.1B identity-and-persistence candidate

### Implemented scope

- restore-only construction of validated identity state;
- versioned identity archives with bounded XML parsing and SHA-256 payload integrity;
- atomic primary replacement with previous-generation backup;
- verified-backup recovery and unrecoverable-state reporting;
- real-pawn seed capture through a narrow RimWorld adapter;
- automatic free-colonist enrollment with stable Dagmay IDs;
- save-linked pawn-to-individual manifest validation;
- rename continuity, death archival, diagnostic inventory logging, and read-only failure mode; and
- packaging of both the RimWorld adapter and Core dependency.

### Static verification

**Status:** Passed

`python3 tools/static_verify.py` passed across 43 C# files. The dependency boundary, Observer-only forbidden-operation scan, XML/JSON structure, secrets scan, links, and required files passed.

### Compilation

**Status:** Passed for Core/providers/tests in the available .NET 8 reference environment; RimWorld pending

The temporary SDK extraction lacked its .NET Standard reference pack, so the normal `netstandard2.0` promotion build could not be repeated in this container. A conditional verification target compiled Core, providers, and tests as `net8.0` with repository-wide warnings-as-errors and nullable analysis. The owner's normal build retains the intended `netstandard2.0` Core/provider targets.

The 0.1B `net472` adapter has not compiled here because proprietary RimWorld assemblies are unavailable. This is the next owner test gate.

### Isolation tests

**Status:** Passed — 19 executed, 0 failed

The original 16 contracts still pass. Three new persistence contracts prove:

1. save/load round-trip preserves individual ID, lineage, version, name, lifecycle, and grounded seed facts;
2. corrupted or checksum-mismatched data is rejected; and
3. a corrupted primary archive can load only a checksum-verified previous-generation backup and reports that recovery explicitly.

### Not yet verified

- 0.1B compilation against RimWorld 1.6.4850;
- automatic GameComponent discovery and callbacks in RimWorld;
- seed-field availability across the owner's DLC and modded pawn types;
- actual save/load, rename, or death continuity in RimWorld;
- external archive location and permissions on the owner's Windows installation; and
- compatibility with the owner's approximately 50-mod list.

The manual procedure is `docs/16_V0.1B_Test_Guide.md`.

## 2026-07-16 — Owner Version 0.1B continuity test

**Status:** Passed — user-reported

The owner compiled the intended `netstandard2.0` Core/provider targets and the `net472` adapter against the installed RimWorld 1.6 assemblies. The build completed with 19 tests and zero failures after two adapter compatibility fixes. The owner then completed the beginner guide through save/load continuity with no reported Dagmay errors or mod-list issues.

This promotes 0.1B identity enrollment, save-linked persistence, and continuity through the tested reload path to **Tested in RimWorld**. Optional rename and death cases are not inferred from this report unless separately exercised.

## 2026-07-16 — Version 0.1C bounded-experience candidate

### Implemented scope

- polling observation every 600 game ticks;
- critical Food, Rest, and Mood threshold crossings;
- health-condition additions and removals;
- skill-level gains;
- factual death journal entries;
- explicit fact → perception → memory provenance;
- deterministic diary/appraisal generation and bounded affect updates;
- recent and significance-ordered memory retrieval;
- append-only, sequence-checked, SHA-256 hash-chained experience journal; and
- save-linked experience position/head-hash validation with read-only failure behavior.

### Verification

**Static verification:** Passed across 49 C# files.

**Compilation:** Core and providers compiled against their intended `.NET Standard 2.0` target, tests compiled against `.NET 8`, and the RimWorld adapter compiled against `.NET Framework 4.7.2` with a local API stub. All completed with nullable analysis, warnings as errors, zero warnings, and zero errors. Exact-target passes caught and corrected one newer dictionary constructor plus three older-framework nullable-flow mismatches before repackaging. Actual RimWorld 1.6 compilation remains owner-side.

**Isolation tests:** Passed — 23 executed, 0 failed.

The four new contracts verify deterministic fact/perception/memory separation, complete provenance round-trip, journal-tamper rejection with no partial canonical history, and bounded recent/significant retrieval.

### Not yet verified

- compilation against the owner's actual RimWorld 1.6 assemblies;
- reflection-based observation fields across vanilla, DLC, and modded pawns;
- natural event detection and false-positive/duplicate rates;
- journal checkpoint continuity through RimWorld save/load;
- runtime overhead with the owner's full mod list; and
- UI or privacy projections, which are not implemented.

The manual procedure is `docs/17_V0.1C_Test_Guide.md`.

## 2026-07-16 — Owner Version 0.1C build and first runtime test

### Compilation

**Status:** Passed — owner build output and runtime loading observed

The owner built 0.1C against the installed RimWorld 1.6 assemblies. The exact-target build completed with zero warnings and zero errors after two adapter nullable-flow corrections. All 23 isolation tests passed. The resulting assemblies then loaded in RimWorld, independently confirming that the adapter package was usable by the game.

### Upgrade and storage health

**Status:** Passed for the tested run

The supplied `Player.log` identifies RimWorld `1.6.4871 rev591`, approximately 50 enabled mods including RimTalk, and this Dagmay startup result:

```text
Individuals=3; Generation=4; Events=0; Memories=0;
IdentityStorage=healthy; ExperienceStorage=healthy
```

Dagmay loaded the existing three-person identity store rather than enrolling replacements. The runtime inventory reported:

| Pawn | Individual ID | Lineage ID |
|---|---|---|
| Lynx | `56858408f59744df9db149e6f81a12ef` | `e75a12c58cc9488b91b9427e1db8bc82` |
| Schmidt | `c24d9597421c4bb89135690e31bce238` | `5770263ab3dc4002848383d4e4b011b0` |
| Michael | `652879a6ba8a47598402fcaa18f86635` | `2a4faeb3ea03496e867ad22f262d6e8a` |

The log does not include the earlier 0.1B inventory for a direct text-to-text comparison, so exact historical ID equality is supported by the successful existing-store load and the owner's prior continuity test rather than independently re-proved from this file alone.

### Natural bounded-event capture

**Status:** Passed for the tested run

Dagmay recorded 12 distinct event/memory pairs during ordinary play:

- seven `rimworld.health.condition_added` events;
- one `rimworld.health.condition_removed` event; and
- four `rimworld.need.critical` events.

Every line had a distinct event ID and memory ID. No Dagmay line reported read-only mode, rejection, mismatch, invalid state, missing storage, or failure.

### Non-Dagmay log findings

Two unresolved pawn relationship references occurred before the Dagmay observer service became ready. A shutdown `ThreadAbortException` originated in the SmashTools/Vehicle Framework dedicated-thread path rather than a Dagmay frame. Unity's `Failed Allocations` headings were allocator statistics, not evidence of a Dagmay operation failing. These findings should be watched, but this log does not attribute them to Dagmay.

### Remaining 0.1C gate

The supplied file covers upgrade and event capture, but not the following fresh-process reload. Test C remains open:

1. restart RimWorld;
2. load the saved `Dagmay_01C_Experience_1` checkpoint;
3. let the game run for at least 20 seconds;
4. save once more and quit; and
5. verify healthy storage, the same identity/lineage IDs, at least one persisted event/memory pair (or at least 12 if all recorded experiences preceded the save), and no replayed duplicate IDs.

## 2026-07-16 — Owner Version 0.1C fresh-process checkpoint

**Status:** Passed — owner-supplied complete log reviewed

The owner completed the final 0.1C restart/reload procedure on RimWorld `1.6.4871 rev591` with the full approximately 50-mod test list. The observer loaded:

```text
Individuals=3; Generation=12; Events=12; Memories=12;
IdentityStorage=healthy; ExperienceStorage=healthy
```

The exact identities and lineages remained:

| Pawn | Individual ID | Lineage ID | Loaded version |
| --- | --- | --- | ---: |
| Lynx | `56858408f59744df9db149e6f81a12ef` | `e75a12c58cc9488b91b9427e1db8bc82` | 11 |
| Schmidt | `c24d9597421c4bb89135690e31bce238` | `5770263ab3dc4002848383d4e4b011b0` | 1 |
| Michael | `652879a6ba8a47598402fcaa18f86635` | `2a4faeb3ea03496e867ad22f262d6e8a` | 0 |

After loading the persisted journal, Dagmay recorded a genuinely new event `c154cee728994c47818d14abaaaedc43` and memory `e945757b9173473384a63442587583ed`. No Dagmay line reported a storage mismatch, replay, read-only transition, or failure. The shutdown exception in the complete log originated in `SmashTools.DedicatedThread.Execute`, not a Dagmay stack.

This closes the 0.1C experience-journal reload/deduplication gate. Version 0.1C is **Compiled**, **Tested in isolation**, and **Tested in RimWorld** for the recorded configuration.

## 2026-07-16 — Version 0.1D reflection/provider candidate

### Implemented scope

- provider-neutral model requests carrying identity, lineage, base version, evidence, current affect, prompt version, escaped context, deadline, and output budget;
- strict reflection-proposal JSON decoding with exact members, duplicate rejection, bounded text, identifier parsing, and full local validation;
- provider-facing affect bounds narrowed dynamically around current state, followed by an independent Core continuity check;
- deterministic fake provider and real Google AI Studio REST transport using an environment-only credential;
- cancelable request/response transport, response-size bounds, timeout/error/rate-limit normalization, `Retry-After`, and usage metadata;
- persistent queue coalescing, priority aging, attempt state, hourly/daily budgets, exponential retry, and circuit breaking;
- checksummed atomic reflection sidecar with private request/response audit records;
- pre-dispatch persistence and pending-commit recovery around atomic identity replacement;
- offline-by-default runtime selection and secure Windows configuration helper; and
- RimWorld main-thread result pump with no pawn-action path and no RimTalk dependency.

### Compilation

**Status:** Passed in available local reference environments; actual RimWorld assemblies pending

- `Dagmay.Core`: Release `.NET Standard 2.0`, zero warnings/errors;
- `Dagmay.Providers`: Release `.NET Standard 2.0`, zero warnings/errors;
- `Dagmay.Tests`: Release `.NET 8`, zero warnings/errors; and
- `Dagmay.RimWorld` source: Release `.NET Framework 4.7.2` compile against the local RimWorld API stub, zero warnings/errors.

The stub proves framework/language/nullability compatibility for referenced adapter APIs but is not equivalent to compilation against the owner's installed `Assembly-CSharp.dll`.

### Static verification

**Status:** Passed across 59 C# files

Required files, XML and JSON parsing, README links, the Core game/provider dependency ban, Observer-only forbidden-operation scan, explicit pawn-control denial, secret-pattern scan, unexpected binary scan, and C# brace shape all passed. This check is separate from compilation.

### Isolation tests

**Status:** Passed — 32 executed, 0 failed

The nine added 0.1D test areas cover:

1. strict proposal round-trip plus unknown/duplicate-member rejection;
2. pure grounded, bounded, stale-state validation with no partial mutation;
3. prompt-injection text inside pawn identity/event data;
4. durable queue merge, deferral, and retry semantics;
5. reflection-store/pending-commit round-trip;
6. reflection-store tamper rejection and verified-backup recovery;
7. hourly, daily, and circuit budget gates;
8. missing Google key preventing transport; and
9. simulated Google success and HTTP 429 paths, including authentication-header placement, valid structured request JSON, dynamic affect schema bounds, token metadata, and `Retry-After`.

No live Google request was made. The HTTP tests use an isolated in-process handler and a fixture credential. No RimWorld process was available for this verification environment.

### Next promotion gate

The owner procedure is `docs/19_V0.1D_Test_Guide.md`: compile against installed RimWorld, verify offline continuity, run the deterministic fake end-to-end commit/reload test, then optionally make a tightly bounded live Google test. Version 0.1D is not yet **Tested in RimWorld**.

## 2026-07-16 — Version 0.1D owner build and runtime gates

### Owner compilation

**Status:** Passed against installed RimWorld 1.6.4871

The owner-provided complete PowerShell record shows:

- static verification passed across 59 C# files;
- Core and Providers built as Release `.NET Standard 2.0` with zero warnings/errors;
- all 32 contract tests passed;
- the RimWorld adapter built as Release `.NET Framework 4.7.2` against the installed game assemblies with zero warnings/errors; and
- the installable `Dagmay-RimWorld-0.1D.zip` package was created.

### Offline and fake-provider runtime

**Status:** Passed in RimWorld with the owner's full mod list

Offline startup created two initial evidence-backed tasks, kept all three stores healthy, and made no provider call. The deterministic fake run subsequently processed five tasks. Every dispatch produced a locally validated atomic commit; Lynx advanced to version 18, Schmidt to version 2, and Michael correctly remained at version 0 because no evidence-backed reflection targeted him. A fresh-process reload reported `ReflectionQueue=0`, `ReflectionAudit=15`, and healthy identity, experience, and reflection stores.

The exact continuity identifiers after reload were:

| Individual | Individual ID | Lineage ID | Version |
| --- | --- | --- | ---: |
| Lynx | `56858408f59744df9db149e6f81a12ef` | `e75a12c58cc9488b91b9427e1db8bc82` | 18 |
| Schmidt | `c24d9597421c4bb89135690e31bce238` | `5770263ab3dc4002848383d4e4b011b0` | 2 |
| Michael | `652879a6ba8a47598402fcaa18f86635` | `2a4faeb3ea03496e867ad22f262d6e8a` | 0 |

### Live Google safe-failure path

**Status:** Passed for safety; live-success remains open

Google mode loaded with the environment-provided credential. Two requests dispatched with the then-default `gemini-2.5-flash-lite` selection and both returned normalized `HTTP_404_NOT_FOUND` provider failures. Neither was retried as transient, neither mutated an identity, and later startup reported `ReflectionQueue=0`, `ReflectionAudit=21`, and all stores healthy with the same identifiers and versions above.

The owner independently listed the models visible to the API key. That diagnostic succeeded and included `gemini-3.1-flash-lite`, `gemini-3.5-flash`, Gemini 2.5 models, and Gemma 4 variants. A later attempt intended to select 3.1 still logged `reflection mode=offline`, proving that Steam had inherited an older process environment rather than proving anything about the 3.1 endpoint. During that offline run, Dagmay continued functioning and recorded Lynx event `35be2a75db354688a32260c10c6d6ef8` with memory `d8f1627646c04211bd74c452791f260c`.

The unrelated log items were an empty MoodPatchMod settings XML recovery and a SmashTools shutdown thread-abort stack. Neither stack named Dagmay.

## 2026-07-16 — Version 0.1D.1 configuration hotfix candidate

### Implemented

- current Windows user environment values take precedence over stale process values inherited by Steam;
- the helper writes both user and current-process configuration and can reuse the stored key;
- Google defaults to the account-listed `gemini-3.1-flash-lite` model; and
- runtime/version/package diagnostics identify the build as 0.1D.1.

### Verification in the implementation environment

**Statically verified:** passed across 60 C# files.

**Compiled:** Core and Providers built as Release `.NET Standard 2.0`; tests built as Release `.NET 8`; the complete adapter source built as Release `.NET Framework 4.7.2` against the local strict RimWorld API stub. All builds completed without warnings or errors.

**Tested in isolation:** 33 executed, 0 failed. The added contract proves a persisted user value overrides a stale process value while the process value remains a fallback.

**Tested in RimWorld:** not yet. The next gate is the focused owner build and one-call procedure in `docs/20_V0.1D.1_Hotfix_Test_Guide.md`. A successful Google response must still pass the existing strict local decoder and continuity validator before any commit.

## 2026-07-16 — Version 0.1D.1 live Google success and offline reload

### Live transport and validation

**Status:** Passed in RimWorld 1.6.4871 with the owner's full mod list

The installed package loaded as Version 0.1D.1 from a normal Steam launch and selected `reflection mode=google`. Five requests dispatched to `gemini-3.1-flash-lite`; all five returned structured proposals that passed Dagmay's strict local decoder, identity/evidence/version/lifecycle checks, continuity bounds, and atomic commit path. No request retried, failed, timed out, or entered quarantine. The owner observed no gameplay pause or stutter.

The run began with the established exact continuity identifiers. Local event recording and validated reflection commits advanced only their versioned state:

| Individual | Individual ID | Lineage ID | Version after live run |
| --- | --- | --- | ---: |
| Lynx | `56858408f59744df9db149e6f81a12ef` | `e75a12c58cc9488b91b9427e1db8bc82` | 25 |
| Schmidt | `c24d9597421c4bb89135690e31bce238` | `5770263ab3dc4002848383d4e4b011b0` | 5 |
| Michael | `652879a6ba8a47598402fcaa18f86635` | `2a4faeb3ea03496e867ad22f262d6e8a` | 3 |

Seven new bounded health-condition events were recorded while provider work proceeded. The observed version transitions are consistent with those deterministic local mutations plus five validated reflection commits; no identifier changed.

### Fresh-process offline persistence

**Status:** Passed

After switching configuration back to offline, a new RimWorld process loaded the saved colony and reported:

- `ReflectionMode=offline`;
- `Individuals=3`, `Events=23`, and `Memories=23`;
- `ReflectionQueue=0` and `ReflectionAudit=36`;
- identity, experience, and reflection storage all `healthy`;
- the exact IDs, lineages, and versions 25/5/3 shown above; and
- zero reflection dispatches or duplicate commits.

No Dagmay failure, mismatch, read-only transition, quarantine, or credential appeared in either supplied log. The shutdown `ThreadAbortException` stack ends in SmashTools' dedicated thread and is unrelated to Dagmay. This closes the 0.1D.1 live-success and persistence gate for the recorded environment.

## 2026-07-16 — Version 0.1E Restricted Observer/runtime-controls candidate

### Implemented scope

- persisted provider-dispatch pause, defaulting to paused for safe upgrade;
- persisted user-adjustable session-attempt, rolling-hour-attempt, UTC-day estimated-token, and background-heartbeat limits with fail-closed settings validation;
- selective enroll, pause, and resume controls that preserve identity and lineage;
- targeted removal of only a paused individual's waiting reflection work;
- permanent quarantine of a response that was already in flight when its individual was paused, even if processing is resumed before completion;
- immutable game/provider-neutral Observer projections for enrollment, identity, seed, affect, private memory provenance, committed reflection summaries, provider usage, and storage health;
- a one-second-cached RimWorld Mod Settings renderer with a two-step private-information confirmation and no pawn-control path;
- request/token accounting that separates session/hour/day attempts and deduplicates provider-reported usage across audit stages; and
- a more robust Windows build-script check that ignores the Microsoft Store Python alias when no working Python interpreter exists.

### Static verification

**Status:** Passed across 63 C# files

Required repository shape, XML/JSON parsing, README links, Core dependency boundary, Observer-only forbidden-operation scan, explicit pawn-control denial, secret-pattern scan, unexpected binary scan, and C# brace shape all passed.

### Compilation

**Status:** Passed in available local reference environments; owner's real RimWorld assemblies pending

- `Dagmay.Core`: Release `.NET Standard 2.0`, zero warnings/errors;
- `Dagmay.Providers`: Release `.NET Standard 2.0`, zero warnings/errors;
- `Dagmay.Tests`: Release `.NET 8`, zero warnings/errors; and
- complete `Dagmay.RimWorld` source: Release `.NET Framework 4.7.2` against a strict local RimWorld/Verse/Unity API stub with nullable analysis and warnings as errors, zero warnings/errors.

The adapter stub verifies the referenced method shapes available to this source check, framework compatibility, and nullable correctness. It is not a substitute for compiling against the owner's installed `Assembly-CSharp.dll` and Unity assemblies.

### Isolation tests

**Status:** Passed — 36 executed, 0 failed

The four new 0.1E contract areas prove:

1. the hard per-session gate blocks the next transport while leaving durable work eligible;
2. pausing one individual removes only its waiting tasks and can preserve an already-dispatched task for quarantine;
3. usage accounting does not multiply provider tokens across pending/committed audit stages; and
4. Observer memory projection defensively preserves perception/event sources, occurrence versus encoding time, affect, emotional weight, accessibility, and involved-person provenance.

No live provider request was made by these tests.

### RimWorld status and next gate

**Pre-owner-test status at this point in the record:** 0.1E had not yet run in RimWorld. Version 0.1D.1 supplied the known continuity baseline: the exact three individual/lineage pairs, versions 25/5/3, queue zero, audit 36, healthy stores, and no reported pause or stutter. The later corrected-build result is recorded below.

The owner procedure is `docs/21_V0.1E_Test_Guide.md`: build against installed RimWorld 1.6.4871, install exactly one package, inspect offline health and continuity, exercise the two-step privacy lock, pause/save/reload/resume Michael, then use the deterministic fake provider to prove a two-attempt session cap without spending Google quota.

### Owner compilation attempt and corrected dependency references

**Initial owner build:** Core and Providers compiled, and all 36 isolation tests passed. The RimWorld adapter then failed with 13 `CS0012` errors because the settings UI transitively uses Unity `TextAnchor` and `GUIContent`, while the project did not explicitly reference RimWorld's `UnityEngine.TextRenderingModule.dll` and `UnityEngine.IMGUIModule.dll`.

**Correction implemented:** both RimWorld-provided Unity modules are now explicit non-copying project references. The Windows build script checks for them before compilation, and static verification requires the references so the same packaging mistake cannot silently recur. No Dagmay behavior, persistence contract, provider logic, or pawn-control boundary changed.

**Status after correction in this workspace:** static verification, Core/Provider compilation, all 36 isolation tests, and the strict local adapter API-stub compilation pass. Compilation against the owner's installed RimWorld assemblies must be rerun with the corrected source archive; this workspace does not contain proprietary RimWorld binaries.

### Corrected owner build and focused RimWorld test

**Status:** Passed in RimWorld 1.6.4871 with the owner's full mod list

The corrected source archive contained no prebuilt Dagmay assemblies. The owner built and installed it, and the supplied final `Player.log` shows `Dagmay 0.1E` loading from the resulting package. This proves the corrected adapter compiled against the installed RimWorld assemblies and loaded in game. The PowerShell window was not retained, so the exact corrected build transcript and its owner-side warning summary are not available; local verification independently retains the 63-file static pass, zero-warning Core/Provider/API-stub builds, and 36/36 isolation-test pass.

The owner directly reported the five requested focused results as: settings page rendered and scrolled normally **yes**; two-step Restricted Observer lock worked **yes**; Michael remained the same individual through pause/save/reload/resume **yes**; fake mode stopped at exactly `2/2` **yes**; any pause, stutter, red error, or unexpected behavior **no**.

The final fresh-process offline log records:

- `ReflectionMode=offline`, with durable transport remaining offline;
- `Individuals=3`, `Events=27`, `Memories=27`, `ReflectionQueue=3`, and `ReflectionAudit=42`;
- identity, experience, and reflection stores all `healthy`;
- Lynx: Individual `56858408f59744df9db149e6f81a12ef`, Lineage `e75a12c58cc9488b91b9427e1db8bc82`, Version 27;
- Schmidt: Individual `c24d9597421c4bb89135690e31bce238`, Lineage `5770263ab3dc4002848383d4e4b011b0`, Version 8; and
- Michael: Individual `652879a6ba8a47598402fcaa18f86635`, Lineage `2a4faeb3ea03496e867ad22f262d6e8a`, Version 4.

All three exact individual and lineage identifiers match the pre-upgrade continuity baseline. The audit count increased from 36 to 42; this is consistent with the two directly observed fake attempts producing three durable audit stages each, but that stage-level interpretation is an inference because the preceding fake-process log was overwritten by the final restart. The three queued tasks remaining offline demonstrate graceful deferred work rather than loss.

The log also contains unresolved references to `Thing_Human82868` and a null `Parent` relation for pawn Nelo, neither attributed to Dagmay. The shutdown `ThreadAbortException` again terminates in SmashTools' dedicated Vehicle Framework thread and is likewise unrelated to Dagmay. No Dagmay failure, quarantine, storage mismatch, read-only transition, credential, or identity replacement appears.

## 2026-07-17 — Version 0.1F development-automation candidate

### Implemented scope

- one-command build/test/package orchestration with timestamped and latest human-readable transcripts;
- a structured build-result record with stage completion, failure, package path, and package hash;
- staged package validation, exact `Mods\Dagmay` targeting, prior-package backup, and rollback for real installation failure;
- current/previous RimWorld log extraction with nearby error context and documented redaction;
- clean source packaging and SHA-256 evidence;
- centralized adapter version reporting; and
- `START_HERE.md`, expanded `AGENTS.md`, and the project-scoped `dagmay_implementer` Codex definition.

This slice changes development reproducibility only. It adds no cognitive feature, event type, provider behavior, canonical state field, UI disclosure, RimTalk dependency, or pawn-control path.

### Static verification

**Status:** Passed across 64 C# files

Required repository shape, XML/JSON/TOML parsing, README links, Core dependency boundary, Observer-only forbidden-operation scan, explicit pawn-control denial, secret-pattern scan, unexpected binary scan, version alignment, and C# brace shape all passed.

### Compilation and isolation tests

**Status:** Passed in the available local reference environments

- `Dagmay.Core`: Release `.NET Standard 2.0`, zero warnings/errors;
- `Dagmay.Providers`: Release `.NET Standard 2.0`, zero warnings/errors;
- `Dagmay.Tests`: Release `.NET 8`, zero warnings/errors;
- complete `Dagmay.RimWorld` source: local minimal RimWorld/Verse/Unity API-surface compile under `.NET Standard 2.0` with nullable analysis and warnings as errors, zero warnings/errors; and
- executable contracts: 36 executed, 0 failed, with no live provider call.

The adapter reference compile proves current source syntax, project-boundary integration, and nullable correctness against the methods represented by the harness. It does not prove the real `.NET Framework 4.7.2` target or compatibility with the owner's installed RimWorld assemblies.

### PowerShell automation checks in this workspace

**Status:** Portable paths passed under PowerShell 7.4.6 on Linux; Windows remains pending

- all six `tools\*.ps1` files parsed with zero PowerShell AST errors;
- `package-source.ps1` produced a clean archive and SHA-256;
- `collect-logs.ps1` included synthetic current and previous Dagmay processes and redacted two Google-style key forms, the configured user-profile prefix, and computer name;
- an incomplete synthetic package was rejected before the installed fixture changed;
- a non-directory object at the exact `Mods\Dagmay` destination was rejected before extraction or replacement;
- a complete synthetic package replaced only the exact temporary `Mods\Dagmay` fixture and produced a prior-package backup;
- a forced build failure produced both transcript and structured failed result, and `dev-loop.ps1 -Install` stopped without invoking the installer; and
- after independent compilation, a success-path harness exercised the `-SkipRimWorld` transcript/result flow and re-executed all 36 contracts. The harness did not masquerade as a second compilation.

### Unverified gate

0.1F has not run under the owner's Windows PowerShell environment, has not compiled against the owner's RimWorld 1.6.4871 and Unity assemblies, has not installed a real 0.1F package into the owner's Steam mod directory, and has not loaded in RimWorld. Version 0.1E remains the continuity baseline: exact individual/lineage pairs, versions 27/8/4, queue 3, audit 42, and all stores healthy.

The next owner-side action is one `tools\dev-loop.ps1 -Install` run from the 0.1F source followed by a focused offline load. The automatically produced build and diagnostic files replace copied PowerShell output for subsequent diagnosis.


## 2026-07-18 UTC — Version 0.1F owner verification and 0.1G candidate

### 0.1F local build

**Status:** Passed

The owner's Windows development loop completed static verification, Core build, provider build, isolation tests, RimWorld adapter build against the installed RimWorld 1.6.4871 assemblies, and package creation with zero build warnings and zero build errors.

### 0.1F RimWorld continuity/load

**Status:** Passed for the tested scenario

RimWorld loaded Version 0.1F with three established individuals. Lynx, Schmidt, and Michael retained the exact previously recorded individual and lineage identifiers. Identity, experience, and reflection stores reported healthy.

### 0.1F deterministic reflection

**Status:** Passed

Fake-provider mode dispatched and committed two validated reflections before the configured 2-request session limit stopped further transport. Schmidt advanced from identity version 8 to 9 and Michael from 4 to 5. New health experiences continued recording after the session cap.

### 0.1F Google AI Studio reflection

**Status:** Passed for the tested bounded session

Google mode dispatched requests using the configured `gemini-3.1-flash-lite` model. Responses passed Dagmay validation and committed canonical changes. The tested session also continued recording bounded health, skill, and critical-need experiences while respecting session-request limits.

### 0.1G remote implementation check

**Status:** Implemented and statically verified; local compilation pending

Added a separate ordinary Mind projection, conservative disclosure policy, ordinary Mod Settings Mind button, coarse state language, grounded identity facts, and shareable-memory rendering. Added an isolation contract proving private memories are excluded and raw diagnostic affect values are not emitted. Remote static verification passes across 65 C# files. Proprietary RimWorld assemblies are unavailable in the remote workspace, so the next promotion requires the owner's local `tools\dev-loop.ps1 -Install` run and one combined in-game disclosure/continuity test.


## 2026-07-18 UTC — Version 0.1G owner verification and 0.1H candidate

### 0.1G RimWorld integration

**Status:** Passed for the tested scenario

The owner confirmed the ordinary Mind button appeared and the ordinary view did not expose Observer-only information. Four individuals were present after Doyle's enrollment. The diagnostic evidence showed all three stores healthy, continued bounded event/memory capture, successful Google AI Studio reflection commits, and continuity for Lynx, Schmidt, Michael, and Doyle across save/reload.

The observed reflection queue grew to 22 while events/memories grew to 57, motivating queue-intelligence work rather than simply raising request limits.

### 0.1H implementation

**Status:** Implemented; local compile and RimWorld verification pending

Added routine-event window coalescing, conservative compaction of unattempted legacy routine tasks, equal-priority cross-individual fairness, queue-composition logging, and diagnostic build-version mismatch warnings. Added an isolation contract for fair tie-breaking. Canonical event/memory history is unchanged by queue compaction.

## 2026-07-18 — Automated integration harness development package

**Status:** Implemented and statically verified in the packaging environment; not compiled here.

Added `Dagmay.IntegrationHarness`, a .NET 8 executable project that runs four deterministic end-to-end scenarios against real Dagmay Core/Providers code: 64-individual long-history identity/persistence continuity, durable reflection-queue recovery after retryable provider outage, verified forward-only checkpoint rollback recovery, and asymmetric relationship-sensitive social provenance. `tools/build.ps1` now runs the harness after contract tests and records `artifacts/Dagmay-integration-latest.json` plus an `integration-harness` completed stage.

Static verification was run after the changes. A .NET SDK and proprietary RimWorld assemblies are not available in this packaging environment, so compilation, harness execution, Windows PowerShell execution, and RimWorld testing remain unverified until the owner's local `tools/dev-loop.ps1` run.

## 2026-07-18 — Version 0.1K engineering audit and 0.1L isolation harness

### Audit defects reproduced and corrected

The first full Windows development loop failed at the real RimWorld adapter compile with two `CS0246` errors because `SocialPathCertificationReport.cs` referenced `EventLedgerEntry` without `Dagmay.Core.Abstractions`. The missing namespace import was added and the same pure certification source is now linked into `Dagmay.Tests`, so this class of compile failure is covered before the proprietary adapter stage.

Certification format v2 also corrected release-blocking diagnostic defects: unrelated historical sidecars are no longer selected; exact stable-counterpart and owner-memory provenance is required; duplicate owner-memory links fail; secondary-subject events cannot satisfy another individual's reflection evidence; unhealthy/read-only stores cannot produce `Gate.Overall=True`; and sidecar replacement is atomic with a prior diagnostic backup. The static verifier now classifies `bin`, `obj`, and `artifacts` relative to repository root instead of silently excluding source when an ancestor directory has one of those names.

### Static and PowerShell verification

**Status:** Passed

- `python tools\static_verify.py`: 73 C# files checked.
- all PowerShell files parse through the PowerShell AST parser;
- an isolated synthetic Windows fixture proved `check-social-certification.ps1` returns non-PASS for the active partial store even when another store has PASS, then returns PASS only for the active store; and
- the same fixture proved `collect-logs.ps1` embeds only the active store's sidecar.
- the guarded installer succeeded twice against an isolated temporary RimWorld-like path containing spaces, targeted only `Mods\Dagmay`, and retained a prior-package backup on replacement.

### Compilation and packaging

**Status:** Compiled on Windows against the owner's local RimWorld 1.6.4871 assemblies

The full non-installing `tools\dev-loop.ps1` completed Core, Providers, Tests, IntegrationHarness, and RimWorld adapter builds with zero warnings and zero errors. It created `artifacts\Dagmay-RimWorld-0.1K.zip` with SHA-256 `197dd605980aa2e94f58c2840e276c8aaa13bf398eb6dd59b515ad6a1885ee88`. The clean source packager created `artifacts\Dagmay-v0.1K-source.zip` plus its external SHA-256 sidecar; its 141 entries contain no build/runtime output, and the packaged verifier passed after extraction beneath a parent directory named `artifacts`.

### Isolation tests

**Status:** Tested in isolation

- 41 contract tests executed, 0 failed;
- five integration scenarios executed, 0 failed;
- the new `PersistenceTorture` scenario passed 238 assertions over six repeated save/reload cycles;
- its final state contained four continuous identities, 13 unique experiences/memories, 12 unique queued reflection tasks, six unique provider-failure audit/request records, and one explicit verified forward recovery; and
- `tools\run-persistence-torture.ps1` independently produced `artifacts\Dagmay-persistence-torture-latest.json` with one successful scenario.

### Not yet verified

**Tested in RimWorld:** pending for this patched build. No mod was installed and RimWorld was not launched during this audit. A real certification v2 PASS still requires a social event, save, reload, and active-store diagnostic collection from a copied colony save.
## 2026-07-18 — Version 0.1K owner RimWorld certification

**Status:** Tested in RimWorld for the copied-save social-path scenario.

The guarded development loop rebuilt and retested the audited package, compiled the RimWorld adapter against the owner's installed RimWorld 1.6.4871 assemblies with zero warnings and zero errors, backed up the prior installed package, and installed only to `RimWorld\Mods\Dagmay`. Saves, Dagmay stores, configuration, and credentials were not modified by installation.

The first copied-save run correctly remained `WAITING_FOR_SOCIAL_EVENT`: post-load audit and all storage-health checks passed, but no qualifying social event had been observed. A retry used RimWorld developer mode to make romance possible in the all-women colony; this altered world setup but did not bypass or mutate any Dagmay certification gate.

The active StoreId `8bbf40dc132f4d989a212b83755f3c57` then produced certification format v2 `Status=PASS` after save/reload. Evidence recorded three social events, three exact stable-counterpart links, three owner memories, three relationship-sensitive memories with exact counterpart provenance, two reflection-eligible direct-relationship events, healthy identity/experience/reflection stores, and `Gate.Overall=True`. The combined diagnostic scan contained zero Dagmay failure lines. This certifies the tested event-to-memory-to-provenance-to-reflection-to-post-load path; it does not certify unmodified emergent relationship frequency or any capability outside the frozen Observer-only 0.1 scope.
## 2026-07-19 — Version 0.1L live persistence torture

**Status:** Tested in RimWorld for the recorded normal-save/normal-shutdown scenario.

The owner used one copied colony save with provider dispatch paused for repeated save, main-menu reload, normal process exit, relaunch, and reload cycles. `Player-prev.log` and `Player.log` retained eight post-load Dagmay snapshots across two RimWorld processes, all for StoreId `8bbf40dc132f4d989a212b83755f3c57`.

Generation increased strictly from 165 to 178. Events and memories remained equal and monotonic from 111 to 117. The six newly recorded EventIds and MemoryIds were unique and exactly matched the persisted count increase. All four pawn mappings retained one unchanged `IndividualId` and `LineageId`. Reflection queue depth remained two at every load while provider dispatch count remained zero. Identity, experience, and reflection storage reported healthy in every snapshot. No Dagmay failure, corruption, mismatch, rollback, recovery, duplicate, or fatal line appeared.

This satisfies 0.1L for real Verse save/load callbacks and normal shutdown persistence in the tested mod set. Forced termination, deliberate storage corruption/file locking, and explicit mismatch injection remain 0.1M failure-isolation scenarios.
## 2026-07-19 — Version 0.1M deterministic failure isolation

**Status:** Implemented, Compiled, and Tested in isolation; exit criterion met with existing live outage/shutdown and checkpoint-recovery evidence.

Added the `FailureIsolation` integration scenario and `tools\run-failure-isolation.ps1`. The scenario exercises unavailable/offline transport, invalid response, timeout, rate limit, provider error, cancellation, malformed nominal-success output, durable retry/quarantine reload, absent/already-applied/conflicting `PendingCommit` recovery, checkpoint mismatch, and shutdown/reload with pending work. Fault injection remains outside the live RimWorld UI and does not alter the frozen cognitive feature set.

The focused runner passed 103 assertions. Only one fully revalidated absent pending commit may mutate canonical state; already-applied recovery performs no second write, conflicting recovery preserves the independently persisted state and quarantines the pending work, mismatched history adopts nothing automatically, and every other provider/validation failure preserves canonical archive bytes, version, identity, lineage, and affect.

The complete Windows development loop passed static verification across 74 C# files, 41 contract tests, all six integration scenarios (603 assertions), and the RimWorld adapter build against local 1.6.4871 assemblies with zero warnings and zero errors. All 10 PowerShell scripts parse. 0.1L supplies live normal-shutdown evidence with a durable pending queue and zero dispatch; 0.1J.3 supplies live explicit mismatch-recovery evidence. No additional destructive owner-side test is required for 0.1M.

## 2026-07-23 - Mosaic 0.2 Gate 3 checkpoint retest

**Status:** Reflection two-load correction passed live; identity Save As correction passed offline.

With Core and Mosaic only, offline reflection mode, and no local model, the owner loaded the same
disposable `New Arrivals11` checkpoint in two separate RimWorld processes without an intervening
save. Both runtime logs reported one identical generation-3 identity and healthy identity,
experience, and reflection stores. Sidecar hashes and timestamps remained unchanged. This verifies
the post-load reflection checkpoint correction in live RimWorld 1.6 for the tested sequence.

The setup also reproduced a distinct defect: two unchanged Save As operations advanced identity
generation from 2 to 3, so loading the first copy failed closed as an older checkpoint. Identity
archive persistence in the save callback is now conditional on a real synchronization change. A new
contract proves two unchanged saves preserve exact archive bytes/generation and the first copy
remains loadable. The corrected build passed static verification over 98 C# files, 69 contract
tests, all six integration scenarios, both Gate 3 offline soak presets, a warning-free RimWorld
Release build, and six-entry package preflight. Corrected package SHA-256:
`273416828b2847ea6e1cef3735caeaca42e2ed9602a9de27a2d3247b7eef5e71`.

The corrected identity package has not yet run in RimWorld, so Gate 3 remains open.
