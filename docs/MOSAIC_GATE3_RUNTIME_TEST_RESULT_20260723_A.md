# Mosaic Gate 3 Runtime Test Result - 2026-07-23 A

- **Test run ID:** `gate3-20260723-a`
- **Status:** LIMITING RESULT - Gate 3 failed
- **Mosaic version:** `0.2-prealpha`
- **RimWorld version:** `1.6.4871 rev591`
- **Package SHA-256:** `81376d71f5a455f27fcab20207e8f405541bd8aa3f71603f29369eaa803f7cea`

## Result

OBSERVED live: a healthy continuity save loaded once with the same three Mosaic identities and
healthy identity, experience, and reflection stores. Loading that unchanged save a second time put
only the reflection store into read-only safety mode. The save expected reflection generation 2,
while the primary external reflection store had advanced to generation 3 during the first load.
The verified generation-2 backup was loaded read-only.

Gate 3 therefore did not pass. The short and long live soaks were not started after the failure.

## Safety and execution boundary

- Reflection transport remained offline and provider dispatch remained paused.
- No credential was inspected and no provider was invoked.
- Only new disposable colonies and copied disposable saves were used.
- The frozen `rimworld/0.1-closure-candidate/` tree was not changed.
- RimWorld window capture was unavailable because the Windows capture interface failed. The test
  used RimWorld's installed `-quicktest` and development `autostart.rws` paths as a controlled
  fallback.
- Temporary automatic enrollment replaced manual Observer enrollment for this run. Manual
  Observer/Mind UI purity and caravan absence/return were not claimed.

## Evidence matrix

| Test area | Result | Live observation |
|---|---|---|
| Non-installing preflight | PASS | Six package entries, references present, no install/save/credential/provider action |
| Minimal Core + Mosaic startup | PASS | `0.2-prealpha` loaded with the Observer-only/no-pawn-control message |
| Disposable colony and first identities | PASS WITH DEVIATION | Three identities were created through temporary automatic enrollment; identity suffixes `47d65549`, `11ce8510`, and `b0d0c2e6` |
| First real save/load continuity | PASS | The same identities and lineage suffixes `ff2be5e8`, `c82ac201`, and `763ef5e7` loaded at identity generation 13 with 11 events and 11 memories; all stores were healthy |
| Observer/Mind read purity | NOT RUN | Required UI could not be captured or inspected reliably |
| Same-key temporary absence/return | NOT RUN | Caravan UI step was unavailable |
| Separate-store controlled corruption | PASS | Disposable store suffix `8fc875ca` rejected a truncated identity primary and a generation-5 backup against save generation 6 |
| Corruption fail-closed behavior | PASS | Zero identities were exposed, all stores were read-only, automatic enrollment created no replacement, and no provider result was applied |
| Failure isolation | LIMITING RESULT | The continuity store suffix `f388ef89` did not reference the corrupted store; identity and experience stayed healthy, but reflection became read-only because its own sidecar advanced beyond the unchanged save |
| Live soak | NOT RUN | Stopped at the first unexplained canonical-storage failure |

## Checkpoint failure

OBSERVED:

1. The continuity checkpoint and all three external stores matched at identity generation 13,
   reflection generation 2, and experience position 11.
2. The first live reload was healthy.
3. No new RimWorld save was made after that reload.
4. The second reload reported healthy identity and experience storage but read-only reflection
   storage. The primary reflection store was ahead of the save; its exact-generation backup was
   accepted only in read-only mode.

SUPPORTED diagnosis from the observed generations and runtime call order: post-load reflection
backlog synchronization dirtied and persisted the external reflection store, advancing it from
generation 2 to 3 without advancing the RimWorld save checkpoint. Exact-generation validation then
correctly rejected the ahead primary on the next load. This is the live form of KR-010.

No runtime fix was made during this evidence run.

## Evidence and privacy

The detailed diagnostic remains an ignored local artifact because it contains pawn names, full
opaque identifiers, and local paths. It must not be committed or uploaded without further
redaction. The committed record contains only identifier suffixes.

- Preflight JSON SHA-256:
  `d2c7d71e04cfa8b2a468ee40b6c18095c87cfa449b741a72893599e8ae49ec20`
- Runtime evidence-manifest JSON SHA-256:
  `8bceae49efaa69c180caa491812e4559873210a7b3cd7114e021e06edaf4ca9e`
- Local diagnostic report SHA-256:
  `b25fc492f3a4a429487a8099375572c49d95f1c98f7beb87d3194d20584a4728`

## Rollback

RimWorld was stopped. The original mod list and preferences were restored byte-for-byte from their
pre-test backups. The temporary Mosaic settings and installation were removed from active
locations. The five named test saves and every sidecar belonging to the two disposable store IDs
were moved to ignored private artifact quarantine. Unrelated RimWorld data was not moved.

## Required next action

Fix or defer post-load reflection mutation so an unchanged save cannot acquire an ahead external
reflection generation. Re-run this test from a fresh disposable store, including two consecutive
loads without an intervening save, before resuming the remaining UI, absence/return, and live-soak
steps.

## Follow-up

The checkpoint gate was implemented offline in commit `9a5b588`. Static verification, 68/68
contracts, all six default integration scenarios, the Release RimWorld build, and both deterministic
offline soak presets passed. A minimal live retest then verified clean Mosaic startup, but graphical
capture remained unavailable and the owner prohibited save-level workarounds. No game or save was
opened, so the two-load fix remains live-unverified. See
`MOSAIC_GATE3_RUNTIME_RETEST_20260723_A.md`.
