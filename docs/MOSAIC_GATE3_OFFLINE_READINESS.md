# Mosaic Gate 3 Offline Readiness

**Status:** OBSERVED offline readiness; live Gate 3 attempted 2026-07-23 and failed on reflection checkpoint divergence
**Base:** `2aadbfd419784065d676131153bf2c1a82f0e09c`  
**Scope:** `rimworld/0.2-prealpha/` only  
**Date:** 2026-07-22

## Boundary

This record covers offline implementation and verification for Gate 3. It does not establish that
the mod loads in RimWorld, that RimWorld save callbacks preserve continuity, that a real pawn can be
reattached safely, or that a live session can soak without degradation.

No provider was invoked, no credential was inspected, no mod was installed, and no save was
touched. The frozen `rimworld/0.1-closure-candidate/` tree was not modified.

## Later live result

The controlled live attempt `gate3-20260723-a` passed package startup, disposable identity
creation, a first save/load, and corruption fail-closed checks. It then reproduced KR-010: a
post-load reflection write advanced the external store beyond the unchanged RimWorld save, so the
next load entered reflection read-only mode. Gate 3 failed, and the remaining live soak and UI
steps were stopped. See `MOSAIC_GATE3_RUNTIME_TEST_RESULT_20260723_A.md`.

## Baseline

The established non-installing Release build passed against the installed RimWorld 1.6 reference
assemblies:

- static verification: 96 C# files;
- Core and Providers: zero warnings and zero errors;
- contract runner: 67 executed, 0 failed;
- default integration harness: six scenarios, 0 failed;
- RimWorld adapter: zero warnings and zero errors;
- non-installing package: constructed and hashed.

The package ZIP includes only `About/About.xml`, the three required assemblies, the RimWorld PDB,
and `README.txt`. It contains no save, identity archive, experience journal, reflection store, or
credential file.

## Evidence matrix

| Gate 3 requirement | Existing implementation | Automated/offline evidence | Remaining offline gap | Required live evidence | Principal failure risk |
|---|---|---|---|---|---|
| Minimal mod configuration | Historical package ID, RimWorld 1.6 metadata, provider paused by default, no hard mod dependency | Static package checks; `gate3-preflight.ps1` inventories and hashes the ZIP | None demonstrated for package shape | Enable only Core plus Mosaic and confirm clean startup | Hidden dependency or wrong package version |
| Controlled copied-save procedure | Existing guarded installer does not touch saves or sidecars | Exact owner procedure in `MOSAIC_GATE3_RUNTIME_TEST_PLAN.md` | Automation does not clone or launch a save by design | Owner creates and labels a disposable copy before loading | Testing against the only valuable save |
| First identity creation | Explicit Observer enrollment calls `IndividualState.Create` and persists the mapping | Creation, ID/lineage, duplicate-ID, round-trip, and soak assertions pass | Adapter enrollment still needs a real Pawn | Enrollment log and Observer inventory in RimWorld | Duplicate or half-created individual |
| Stable identity on repeated observation | Existing external-ID map reuses the same `IndividualState` | Repeated lookup, archive reload, and bounded soak preserve IDs | No adapter-level fixture for real `ThingID` stability | Repeated UI reads/scans show identical IndividualId and LineageId | Silent replacement after observation |
| Identity/environment-binding separation | Core `EnvironmentBinding` cannot own or replace IndividualId | Containment, unresolved, destruction, and soak transitions preserve identity | Durable changed-key reattachment policy is not implemented; repository evidence does not justify guessing a new serialized matcher | Temporary absence with the same stable RimWorld key; changed-key cases must remain unresolved | Wrong-pawn attachment or replacement identity |
| Pawn disappearance without deletion | Synchronization keeps archive mappings when a pawn is absent from current maps | Identity archive and soak retain all enrolled individuals through simulated binding loss | World-pawn/container enumeration remains game-dependent | Pawn temporarily leaves current map and later returns | Treating absence as destruction |
| Save/load continuity | Save manifest links store ID, identity/reflection generations, experience head, external IDs, individual IDs, and paused IDs to sidecars | Manifest state-machine matrix, exact store/generation load, round trip, persistence torture, and failure isolation pass | RimWorld `Scribe` callbacks cannot be exercised offline | Save, exit, reload, then compare IDs, lineage, generations, and storage health | Save/sidecar checkpoint split |
| Checkpoint mismatch behavior | Manifest preflight and identity/reflection loads require structurally valid fields, exact store ID, and exact save generation | Missing/invalid/traversal-shaped IDs, malformed mappings, older/ahead stores, and stale backup rollback fail closed | Explicit identity forward-recovery workflow is intentionally not invented | Mismatch produces READ-ONLY state and no replacement identities | Silent rollback or uncheckpointed forward adoption |
| Observer and presentation purity | Restricted Observer, ordinary Mind, and presentation packet builders are read paths | Reusable canonical fingerprint covers identity, binding, event, memory, and queue state; static guard rejects scheduler/persistence calls from `CreateObserverSnapshot` | RimWorld UI calls and exception paths remain live-only | Repeated Observer/Mind reads leave generation, queue, counts, and IDs unchanged | Read causes scheduling, timestamps, or persistence mutation |
| Corruption failure | Checksummed codec, atomic primary/backup replacement, strict schema, duplicate rejection | Truncation, unsupported version, checksum failure, store mismatch, generation mismatch, stale backup, and interrupted temporary write fail closed | Deliberate live corruption must use a unique disposable store | Corrupt disposable sidecar yields READ-ONLY diagnostics; unrelated test store remains healthy | Ambiguous repair or unrelated identity loss |
| Failure isolation | Identity, experience, and reflection stores have separate safety modes | FailureIsolation: 103 assertions; corrupt identity test paths do not expose canonical state | Real file permissions and RimWorld lifecycle timing remain untested | One disposable store fails without damaging another unique store | Global failure or false success after partial persistence |
| Short and long soak | Opt-in `Gate3OfflineSoak` in the existing IntegrationHarness | Short: 250 cycles; Long: 5,000 cycles; deterministic seed, operation counts, sampled memory, failure injection, final hash | It does not use RimWorld, Verse, `Scribe`, or a real Pawn | Owner performs bounded short and longer live sessions | Leak, queue growth, identity drift, or delayed persistence failure |
| Runtime evidence collection | Redacted log collector plus test-run evidence manifest | Synthetic log smoke test emitted a bounded diagnostic report and SHA-256 manifest | Redaction cannot guarantee removal of every user-authored in-game name | Owner reviews the report before sharing | Uploading private saves, raw logs, paths, or credentials |

## Completed work packages

### Package B — Observer and presentation purity

- Removed scheduler-settings refresh from `CreateObserverSnapshot`.
- Added a reusable canonical-state fingerprint over identity, binding, event, memory, and reflection
  queue state.
- Added repeated-read and exception-path contract coverage.
- Added a static guard against scheduler, synchronization, dispatch, and persistence calls from the
  Restricted Observer snapshot builder.

### Package C — Corruption and failure isolation

- Added expectation-aware identity archive loading.
- Exact store ID and exact checkpoint generation are now required by the RimWorld load path.
- Verified but stale backups cannot silently roll identity state backward.
- Archives ahead of the save checkpoint are not silently adopted.
- Added truncation, unsupported-version, interrupted-temporary-write, identity mismatch, generation
  mismatch, and stale-backup tests.

### Adversarial persistence state-machine audit

- Added a deterministic preflight over every serialized save-manifest field.
- Only a completely pristine save may initialize a new identity store.
- Invalid store IDs cannot become sidecar path segments, and the invalid-manifest path opens no
  sidecar.
- Identity synchronization, per-pawn mutation, and Observer enrollment stop while identity storage
  is read-only.
- Manifest mappings are validated before identities enter in-memory state and are preserved on a
  subsequent read-only save.
- Reflection restore now requires the exact save generation; it rejects rollback, uncheckpointed
  forward state, wrong-store state, and stale backups.
- Full matrix and remaining live limits are recorded in
  `MOSAIC_GATE3_PERSISTENCE_STATE_MACHINE_AUDIT.md`.

### Package D — Deterministic offline soak

- Added an opt-in IntegrationHarness scenario with configurable cycles and seed.
- Short preset: 250 cycles.
- Long preset: 5,000 cycles.
- Exercises bounded event admission, identity lookup, persistence, reload, read-only presentation,
  binding loss/recovery, and isolated corruption injection.
- Reports operation counts, duration, sampled managed memory, and final-state SHA-256.

### Package E — Non-installing evidence tooling

- Added a package/reference preflight that installs nothing and reads no credential.
- Added package entry and assembly SHA-256 inventory.
- Added private-runtime-artifact exclusion checks.
- Added test-run IDs and a hashed evidence manifest to bounded runtime log collection.
- Hardened Python detection so an inaccessible Windows Store shim does not abort the C# build.

## Packages intentionally not implemented

### Package A — changed-key identity reattachment

Core coverage already proves that binding states preserve IndividualId and that confirmed
destruction cannot rebind. The live adapter, however, keys identities by RimWorld `ThingID`. A
policy for reattaching an individual after that key changes would require a new durable matching
contract and collision policy. That is a serialization and identity-continuity decision. Current
repository evidence does not justify guessing it, so no heuristic matcher was added. Same-key
temporary absence remains testable in Gate 3; changed-key cases must fail unresolved.

### Additional corruption auto-repair

Ambiguous corruption is deliberately not repaired. Raw files remain available for diagnosis and
the runtime enters read-only safety mode.

## Offline soak commands

```powershell
.\tools\run-gate3-offline-soak.ps1 -Preset Short -Seed 2031
.\tools\run-gate3-offline-soak.ps1 -Preset Long -Seed 2031
```

The scenario is opt-in so the normal six-scenario gate remains quick.

## Owner handoff

The next engineering action is to prevent post-load reflection persistence from advancing the
external generation outside the RimWorld save checkpoint, then rerun the controlled procedure in
`docs/MOSAIC_GATE3_RUNTIME_TEST_PLAN.md`. Gate 4 must not begin until actual RimWorld evidence
supports identity creation, repeated observer purity, save/load continuity, same-key absence and
return, corruption failure isolation, and a bounded live soak.
