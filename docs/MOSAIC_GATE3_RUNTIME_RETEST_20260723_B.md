# Mosaic Gate 3 Runtime Retest - 2026-07-23 B

- **Test run ID:** `gate3-fix-20260723-b`
- **Status:** PARTIAL PASS - both checkpoint regressions passed live; broader Gate 3 scope remains open
- **Tested fix commit:** `9a5b58856cd87af85ef94ef0121ef762898b7d3a`
- **Mosaic version:** `0.2-prealpha`
- **RimWorld version:** `1.6.4871 rev591`
- **Tested package SHA-256:** `ae720adb62ec0993089d2a1db9c4d1f35df53159fa01741b4fbdbf8b3530d060`

## Scope and result

The owner performed all graphical steps through the normal RimWorld UI. Windows graphics capture
remained unavailable, so graphical milestones are identified as owner-confirmed rather than
screenshot-proven. Logs and sidecar hashes independently establish the storage milestones.

OBSERVED live:

- RimWorld ran with only Core and Mosaic active;
- Mosaic selected offline reflection mode, and no local or remote model was used;
- a new disposable game enrolled one identity;
- the owner saved `New Arrivals10` and then `New Arrivals11`;
- both exact loads of `New Arrivals11`, separated by a complete process restart and no intervening
  save, loaded the same generation-3 identity with identity, experience, and reflection storage
  healthy;
- the second load retained zero events, memories, queued reflections, and audit records; and
- identity and reflection sidecar hashes and timestamps did not change across the two-load check.

This is a live PASS for the post-load reflection checkpoint fix from commit `9a5b588`.

## Separate identity Save As limiting result

The same session reproduced a distinct checkpoint defect. `New Arrivals10` recorded identity
generation 2. A second unchanged Save As to `New Arrivals11` unconditionally rewrote the identity
archive as generation 3 even though canonical identity state had not changed. Loading the older
`New Arrivals10` copy therefore found an ahead primary and entered identity read-only mode using
the exact generation-2 backup. Repeating that load produced the same fail-closed result.

The safety checks behaved correctly, but the save callback was not idempotent. This contradicted the
test plan's use of an earlier copied save as rollback material.

The narrow correction now persists the identity archive during a RimWorld save callback only when
colonist synchronization reports a real identity change. All other identity mutation paths continue
to persist immediately. A new contract models two unchanged Save As callbacks and proves that the
archive bytes and generation remain unchanged and the first copy still reloads exactly.

Offline verification of the identity correction:

- static verification: 98 C# files;
- contract tests: 69 executed, 0 failed;
- integration harness: six scenarios, 0 failed;
- RimWorld Release build: zero warnings and zero errors;
- short offline soak: 250 cycles, passed;
- long offline soak: 5,000 cycles, passed;
- non-installing preflight: passed, six package entries; and
- corrected package SHA-256:
  `273416828b2847ea6e1cef3735caeaca42e2ed9602a9de27a2d3247b7eef5e71`.

## Corrected package live retest

The corrected package from commit `e4eb4895cf636acb229e0c9701976cee1c99dea5` was installed
reversibly for test run `gate3-identity-fix-20260723-c`. Its exact package hash was
`273416828b2847ea6e1cef3735caeaca42e2ed9602a9de27a2d3247b7eef5e71`.

The owner loaded the disposable `New Arrivals11`, saved unchanged copies as `New Arrivals12` and
`New Arrivals13`, returned to the main menu, and loaded the first new copy. Direct save-manifest
inspection showed all three saves retained:

- identity generation 3;
- reflection generation 1; and
- the same store ID.

The `New Arrivals12` reload reported the same identity and healthy identity, experience, and
reflection storage. Identity primary, identity backup, and reflection sidecar bytes, hashes, sizes,
and timestamps exactly matched their pre-test state; the experience journal remained absent.
No mismatch, read-only, corruption, invalid-state, exception, or Mosaic error line appeared.

This is a live PASS for the unchanged Save As idempotence correction.

## Evidence matrix

| Milestone | Result | Evidence |
|---|---|---|
| Exact tested commit/package recorded | PASS | Commit and six-entry package hash above |
| Process launched | PASS | `Player.log` process startup and hardware lines |
| Main menu reached | OWNER-CONFIRMED | Owner used the normal UI; no screenshot was available |
| Mod loaded without observed error | PASS | Mosaic startup line and bounded log review |
| Disposable game created | OWNER-CONFIRMED | Owner-created saves and matching Mosaic store |
| Identity enrolled | PASS | Runtime inventory and persisted identity archive |
| Observer UI inspected | UNVERIFIED | No screenshot or sufficiently specific confirmation retained |
| `New Arrivals11` first load | PASS | Healthy generation-3 runtime log plus owner confirmation |
| `New Arrivals11` second unchanged load | PASS | Healthy generation-3 runtime log plus owner confirmation |
| Reflection reload continuity | PASS | Exact generation and unchanged sidecar hash across both loads |
| Earlier copied-save identity continuity | FAIL | `New Arrivals10` reproducibly entered read-only mode |
| Identity idempotence correction | LIVE PASS | `11` → Save As `12` → Save As `13` → load `12`, exact generation 3 |

## Provider and resource boundary

- The RimWorld child processes used `DAGMAY_REFLECTION_MODE=offline`.
- Provider dispatch remained paused.
- Provider environment values were removed from the child without inspecting their values.
- No Ollama, Qwen, Llama, LM Studio, or other local-model process was observed.
- No credential was inspected and no network-dependent reflection was attempted.
- `Player.log` directly reported an RTX 3080 Ti with 12,086 MB VRAM.

## Evidence hashes

- First correct `New Arrivals11` load log:
  `C8295E17DBDEDAE67C360F11AD602B9BD816B473BD476B3A30492A28B95E5B84`
- Second correct `New Arrivals11` load log:
  `E0C2535C39C25EF15CD6FF00AFE39932A6445976DC2CBF7375CFD97F5933E040`
- Identity primary after the test:
  `18D1233F5075EFBC6554D485CE78E60823ADEA2A3AE30AD8E6B6F8882AC35424`
- Exact generation-2 identity backup:
  `E0E2A9B58A3C9E1581070463762AF0B4105F54ACBD6CACE629921A537752EE98`
- Reflection sidecar:
  `CDF3003DE7EFFF56E387A68160A28CA8AB9FAB6BD50536B036A19B0E6236BD42`
- Corrected Save As retest `Player.log`:
  `984BF6632F91E3EE6BA24A4F1EF7B34CF5E141EE54FD43E533466AF4BFDA0DDB`
- `New Arrivals12` save:
  `A5761FFF0BEC57A8F4498C6AEB4F535AE779282F7008E4DE20A8213240124560`
- `New Arrivals13` save:
  `03D04D53AF200A62C1FA094D99C118C825390B4DF5730CC6281D6C2091D75B53`

Raw logs and runtime state remain ignored local evidence because they contain private pawn and
identity details.

## Cleanup

RimWorld was stopped without another save after the decisive second load. The preserved evidence
copy exactly matched the closed `Player.log`. The original mod list and preferences were restored
byte-for-byte. The temporary tested installation was removed from the active Mods directory and
moved to ignored recoverable quarantine. The disposable saves and their sidecars were left
untouched.

The corrected-package retest was likewise stopped without another save after the decisive reload.
Its preserved evidence copy exactly matched the closed `Player.log`. The original mod list and
preferences were again restored byte-for-byte, the temporary corrected installation was moved to a
separate ignored recoverable quarantine, and the disposable saves and sidecars were left untouched.

Gate 3 remains open pending owner review, Observer visual inspection, and the remaining planned
live-soak scope. Both checkpoint regressions found during this test are now corrected and verified
live.

## Gate 3 false read-only diagnostic resolution (2026-07-24)

The apparent canonical-storage failure observed during the 41-minute offline gameplay soak was traced to a diagnostic-control-flow defect rather than an actual storage transition.

RecoverPendingCommits() previously combined two independent conditions:

- canonical storage was unavailable; or
- the reflection audit contained no records.

Both conditions called LogStorageSafetyPauseOnce(). Once a reflection task entered the queue, a healthy empty audit could therefore emit the warning that canonical storage was read-only. The preserved runtime log contained no corresponding identity, experience, or reflection storage-disable transition, while startup and Restricted Observer both reported all three stores healthy.

The correction introduces an explicit pending-commit recovery classification:

- StorageUnavailable
- NothingToRecover
- Recover

Only StorageUnavailable now produces the storage-safety pause warning. An empty audit with healthy stores exits normally without claiming that storage is read-only. Existing fail-closed behavior remains unchanged.

Verification completed:

- Static verification: 98 C# files passed.
- Contract suite: 70 tests executed; 0 failed.
- Integration suite: 6 scenarios executed; 0 failed.
- RimWorld adapter: Release build completed with 0 warnings and 0 errors.
- Verified package SHA-256: d845e5165a4977b3bdc4af98f55fd6c5485970d127411c023b575409b3db676a.
- Targeted live run: gate3-false-readonly-retest-20260724-a.
- Active mods: RimWorld Core and Mosaic only.
- Reflection mode: offline.
- Owner inspection: enrolled individual present; identity, experience, and reflection storage all healthy; no red RimWorld error dialog.
- Targeted runtime result: 0 false read-only warnings and 0 genuine storage transitions.
- Disposable Save As completed successfully.
- Final evidence and a SHA-256 manifest were preserved locally before cleanup.
- Original RimWorld configuration and all 7 pre-test StoreId files were restored.
- The test package and disposable working saves were removed.

The earlier soak's read-only finding is therefore closed as a false diagnostic. Its gameplay and experience-recording evidence remains valid; the targeted retest specifically certifies the corrected save-path behavior. Gate 3 remains subject to any other live-test requirements not addressed by this defect.