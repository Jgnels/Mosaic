# Mosaic Gate 3 Controlled Runtime Test Plan

**Audience:** project owner  
**Status:** initial run failed; offline fix implemented; minimal startup retest passed; owner-assisted continuity rerun required
**Target:** Mosaic `0.2-prealpha` on RimWorld 1.6

## Safety rules

Use a new test-run ID such as `gate3-20260723-a` for every attempt. Keep RimWorld reflection mode
offline and provider dispatch paused. Never use the only copy of a valuable save. Never edit the
frozen 0.1 tree.

Never upload:

- `.rws` saves;
- `.dagmay`, `.journal`, or `.reflection` sidecars;
- raw `Player.log` or `Player-prev.log`;
- the whole RimWorld configuration directory;
- API keys, environment-variable dumps, credentials, private archives, or screenshots containing
  secrets;
- any artifact that has not been manually reviewed.

The only intended return artifacts are the preflight JSON, the redacted diagnostics text, the
runtime evidence-manifest JSON, and a short written observation record.

## 1. Verify the minimal package

- **Purpose:** prove the exact package is complete before installation.
- **Action:** close RimWorld. In PowerShell, change to `rimworld\0.2-prealpha` and run:

  ```powershell
  .\tools\gate3-preflight.ps1 -TestRunId gate3-20260723-a
  ```

- **Expected visible result:** `Mosaic Gate 3 preflight: PASS`, six package entries, and RimWorld
  1.6 references present.
- **Expected log evidence:** `artifacts\Dagmay-gate3-preflight-gate3-20260723-a.json` with
  `installedMod=false`, `touchedSave=false`, `inspectedCredentials=false`, and
  `invokedProvider=false`.
- **Failure condition:** LIMITED/FAIL, missing assembly, wrong ZIP, unexpected private artifact, or
  hash/report missing.
- **Preserve:** the preflight JSON and displayed package SHA-256.
- **Never upload:** use the global no-upload list above.
- **Rollback:** none; preflight does not install or touch a save.

## 2. Install only Mosaic and the base game

- **Purpose:** remove mod-stack ambiguity.
- **Action:** with RimWorld closed, run:

  ```powershell
  .\tools\install.ps1
  ```

  Start RimWorld normally. In the mod list enable only Core and
  `Dagmay — Mosaic 0.2 Pre-Alpha`. No Harmony or other mod is required. Keep all optional mods,
  including RimTalk, disabled. Restart RimWorld if requested.
- **Expected visible result:** the package appears without a red dependency warning.
- **Expected log evidence:** `[Dagmay] Version 0.2-prealpha loaded.` followed by the Observer-only
  message.
- **Failure condition:** red startup error, missing dependency, wrong version, or any pawn-control
  capability.
- **Preserve:** the preflight JSON and the install-backup ZIP if the installer created one.
- **Never upload:** use the global no-upload list above.
- **Rollback:** close RimWorld. Restore the exact installer backup from
  `artifacts\install-backups`, or remove only the newly installed `RimWorld\Mods\Dagmay` folder if
  no prior Mosaic package existed.

## 3. Create a disposable test world and save copy

- **Purpose:** ensure all later persistence and corruption tests are isolated from valuable play.
- **Action:** create a new tiny test colony with a unique name containing the run ID. Save it as
  `gate3-20260723-a-source`, then immediately use **Save As** to create
  `gate3-20260723-a-working`. From this point load only the working copy. Do not start from an
  irreplaceable colony.
- **Expected visible result:** both names appear in the Load Game list.
- **Expected log evidence:** normal save completion and no Mosaic READ-ONLY/error line.
- **Failure condition:** only one copy exists, autosave replaces the intended source, or Mosaic logs
  a persistence error.
- **Preserve:** keep the source save locally as rollback material; do not return it to the repo.
- **Never upload:** both `.rws` files and every sidecar.
- **Rollback:** delete only the working save through RimWorld's UI and recreate it from the source.

## 4. Enroll the first identity

- **Purpose:** verify first creation and durable ID assignment.
- **Action:** open **Options → Mod Settings → Dagmay**. Confirm **Pause provider dispatch** remains
  checked and automatic enrollment remains off. Select one living colonist and click **Enroll**.
  Record the visible name. Open Restricted Observer only after its confirmation and record the last
  eight characters of IndividualId and LineageId in the written observation record.
- **Expected visible result:** the colonist changes from `NotEnrolled` to `Active`; Mind and Inspect
  become available.
- **Expected log evidence:** `enrolled <name>; IndividualId=...; LineageId=...`.
- **Failure condition:** duplicate rows, missing IDs, another colonist changes, provider activity,
  or enrollment reports success without an identity.
- **Preserve:** written suffixes only and later redacted diagnostics.
- **Never upload:** the save, sidecars, raw log, or full private identity inspection.
- **Rollback:** stop, close without further play, and return to the untouched source save.

## 5. Check Observer and Mind purity

- **Purpose:** verify presentation reads do not mutate canonical state.
- **Action:** note displayed generation, event/memory/queue counts, IndividualId suffix, LineageId
  suffix, and enrollment state. Open and close **Mind** five times. Open and close Restricted
  Observer five times. Wait one in-game minute without causing a notable event, then re-read the
  same values.
- **Expected visible result:** values remain identical except the snapshot's wall-clock display;
  no new memory, queue item, rename, or identity appears because of reads.
- **Expected log evidence:** no `persist`, `enrolled`, `rename`, reflection dispatch, or storage
  error attributable to the reads.
- **Failure condition:** generation/count/ID/lineage/queue changes after reads, a provider call, or
  UI exception.
- **Preserve:** before/after written values and redacted diagnostics.
- **Never upload:** screenshots showing private Observer content unless manually redacted.
- **Rollback:** close the settings page, stop the run, and retain the working save unchanged for
  diagnosis.

## 6. Save and exit cleanly

- **Purpose:** create a known checkpoint shared by the RimWorld save and Mosaic sidecars.
- **Action:** use **Save As** to save `gate3-20260723-a-checkpoint-1`. Wait for completion, return to
  the main menu, and exit RimWorld completely.
- **Expected visible result:** the save completes and the process exits normally.
- **Expected log evidence:** storage health remains `healthy`; inventory lists the same
  IndividualId/LineageId; generation is nondecreasing.
- **Failure condition:** save exception, READ-ONLY state, crash, or missing identity inventory.
- **Preserve:** the local checkpoint and redacted evidence after the process closes.
- **Never upload:** the checkpoint save or sidecars.
- **Rollback:** retain the earlier source save; do not retry over the failed checkpoint.

## 7. Reload and verify continuity

- **Purpose:** test actual RimWorld `Scribe` plus sidecar continuity.
- **Action:** restart RimWorld with the same minimal mod list. Load
  `gate3-20260723-a-checkpoint-1`. Open Mind and Restricted Observer once and compare the recorded
  suffixes and counts.
- **Expected visible result:** the same person is Active with the same IndividualId and LineageId.
- **Expected log evidence:** `observer service ready after loaded game`, matching inventory,
  matching generation, and all three storage states `healthy`.
- **Failure condition:** new identity, changed lineage, missing mapping, generation mismatch,
  READ-ONLY state, or automatic replacement.
- **Preserve:** written comparison and redacted diagnostics.
- **Never upload:** the save, sidecars, or raw logs.
- **Rollback:** exit without saving and return to checkpoint 1.

## 8. Test temporary absence and same-key return

- **Purpose:** verify absence does not delete the individual.
- **Action:** use a normal reversible game action that temporarily removes the enrolled pawn from
  the current map while retaining the pawn in RimWorld, such as a short caravan with no optional
  mods. Save as `gate3-20260723-a-absent`, return the pawn to the same colony, and wait for at least
  one 600-tick Mosaic scan interval.
- **Expected visible result:** the identity is absent/unresolved while away and returns with the
  same recorded ID and lineage; no second individual appears.
- **Expected log evidence:** no destruction, no replacement enrollment, and the same inventory entry
  after return.
- **Failure condition:** identity deletion, duplicate enrollment, different ID/lineage, or binding
  to another pawn.
- **Preserve:** written before/absent/after suffixes and redacted diagnostics.
- **Never upload:** caravan saves, sidecars, or raw logs.
- **Rollback:** reload checkpoint 1. Do not test a changed RimWorld `ThingID`; changed-key
  reattachment is not implemented and must remain unresolved.

## 9. Prepare a unique disposable corruption store

- **Purpose:** ensure the corruption test cannot affect the continuity test or a valuable save.
- **Action:** create a second brand-new colony named `gate3-20260723-a-corrupt-only`, enroll one
  pawn, save, exit, and collect its active StoreId from the redacted diagnostic report. Confirm it
  differs from the continuity test StoreId. Copy that store's `.dagmay` identity sidecar to a local
  `.original` backup outside the active `Identities` folder.
- **Expected visible result:** the new colony has one new identity and a distinct StoreId.
- **Expected log evidence:** healthy storage and the new store-specific identity path.
- **Failure condition:** StoreId matches the continuity test, the sidecar cannot be identified
  exactly, or the backup hash cannot be recorded.
- **Preserve:** local backup and its SHA-256 only; the backup stays private.
- **Never upload:** the `.original` backup, any sidecar, or the corrupt-only save.
- **Rollback:** stop before corruption if the exact unique store cannot be proven.

## 10. Run the controlled corruption test

- **Purpose:** prove fail-closed behavior using only the unique disposable store.
- **Action:** with RimWorld closed, make a second copy of the unique `.dagmay` file named
  `.corrupt-test`. Truncate only `.corrupt-test`, then move the untouched active `.dagmay` to a
  `.hold` name and place `.corrupt-test` at the exact active `.dagmay` path. Do not alter the save,
  journal, reflection store, continuity-test store, or `.original` backup. Start RimWorld and load
  only `gate3-20260723-a-corrupt-only`.
- **Expected visible result:** Mosaic enters READ-ONLY safety mode or refuses the identity archive;
  it does not create a replacement individual.
- **Expected log evidence:** checksum/XML/archive failure classification plus
  `identity storage entered read-only safety mode`.
- **Failure condition:** silent success, new identity, write to corrupted state, crash loop, or
  damage to another store.
- **Preserve:** redacted diagnostics, evidence manifest, and private original backup.
- **Never upload:** corrupt or original sidecars, the save, or raw logs.
- **Rollback:** exit without saving. Delete only the corrupt active file and restore `.hold` to the
  exact `.dagmay` name. Verify its SHA-256 matches the pre-test original.

## 11. Verify failure isolation

- **Purpose:** prove one corrupt disposable store does not damage a healthy unique store.
- **Action:** after restoring the corrupt-only sidecar, load the continuity checkpoint again. Do not
  save over it. Compare its ID/lineage and storage health.
- **Expected visible result:** continuity identity remains unchanged and healthy.
- **Expected log evidence:** the continuity StoreId loads its own healthy archive; no reference to
  the corrupt-only StoreId is adopted.
- **Failure condition:** unrelated READ-ONLY state, changed identity, missing sidecar, or cross-store
  mapping.
- **Preserve:** written comparison and redacted diagnostics.
- **Never upload:** either save or either store's sidecars.
- **Rollback:** close without saving and restore all exact backups before another launch.

## 12. Run the short live soak

- **Purpose:** observe bounded runtime behavior after continuity and failure tests.
- **Action:** load a fresh copy of checkpoint 1. Play 30 minutes at normal speed with provider
  dispatch paused. Every 10 minutes record generation, event/memory/queue counts, storage health,
  and ID/lineage suffixes. Save As at the end, exit, reload once, and compare.
- **Expected visible result:** stable identity, responsive UI, bounded queue, healthy storage, and no
  stutter attributable to Mosaic.
- **Expected log evidence:** no recurring exceptions, no provider attempt, nondecreasing generation,
  and healthy post-load inventory.
- **Failure condition:** drift, uncontrolled queue growth, repeated errors, stutter, memory growth
  symptoms, or failed reload.
- **Preserve:** the written 10-minute samples and redacted evidence.
- **Never upload:** saves, sidecars, task-manager dumps containing private paths, or raw logs.
- **Rollback:** stop at the first failure and retain the last known-good checkpoint locally.

## 13. Run the longer live soak

- **Purpose:** expose delayed persistence, queue, or performance failures.
- **Action:** only after the short soak passes, play two hours in two one-hour blocks. Save As and
  fully restart RimWorld between blocks. Record the same values every 30 minutes. Do not add other
  mods or enable a provider.
- **Expected visible result:** stable ID/lineage, bounded counts consistent with play, healthy
  storage, and successful restart.
- **Expected log evidence:** clean checkpoint/reload records and no repeated exception signature.
- **Failure condition:** any short-soak failure, monotonic unexplained queue growth, storage pause,
  identity replacement, or inability to reload.
- **Preserve:** timestamped samples and redacted evidence from each block.
- **Never upload:** saves, sidecars, raw logs, screenshots with private Observer text, or credentials.
- **Rollback:** end the run, restore the last known-good local checkpoint, and do not proceed to
  compatibility testing.

## 14. Collect, review, and clean up

- **Purpose:** return evidence without private runtime state.
- **Action:** after closing RimWorld, run:

  ```powershell
  .\tools\collect-logs.ps1 -CurrentOnly -TestRunId gate3-20260723-a
  ```

  Manually open and review:

  ```text
  artifacts\Dagmay-diagnostics-latest.txt
  artifacts\Dagmay-gate3-evidence-gate3-20260723-a.json
  ```

  Return those two reviewed files, the preflight JSON, and the written observations. Remove the
  disposable saves through RimWorld's UI. Restore or remove only the exact test mod folder using
  the installer backup.
- **Expected visible result:** collector reports both files and the manifest says
  `includesSave=false` and `includesCredential=false`.
- **Expected log evidence:** bounded `[Dagmay]` lines and relevant error context only.
- **Failure condition:** report contains a secret, private path, unwanted full log, or unrelated
  personal data.
- **Preserve:** only reviewed redacted text, manifest JSON, preflight JSON, hashes, and written
  results.
- **Never upload:** anything on the global no-upload list; if uncertain, do not upload it.
- **Rollback:** delete the unreviewed evidence artifact, correct the collection/redaction process,
  and collect again from the still-private local logs.

## Gate decision

Gate 3 can be marked passed only after the live steps above have actual evidence for startup,
identity creation, repeated read purity, save/load continuity, same-key temporary absence and
return, corruption failure isolation, and both live soaks. Offline PASS results remain offline
evidence and must not be promoted into a live-runtime claim.
