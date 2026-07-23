# Mosaic Gate 3 Runtime Retest - 2026-07-23 B

- **Test run ID:** `gate3-fix-20260723-b`
- **Status:** PARTIAL PASS - reflection two-load fix passed live; identity Save As fix awaits live retest
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

The corrected package was not installed or run. A fresh live Save As/Save As/load-first-copy check
is required before this identity result can be promoted from offline to live.

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
| Identity idempotence correction | OFFLINE PASS | New contract plus complete offline verification chain |

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

Raw logs and runtime state remain ignored local evidence because they contain private pawn and
identity details.

## Cleanup

RimWorld was stopped without another save after the decisive second load. The preserved evidence
copy exactly matched the closed `Player.log`. The original mod list and preferences were restored
byte-for-byte. The temporary tested installation was removed from the active Mods directory and
moved to ignored recoverable quarantine. The disposable saves and their sidecars were left
untouched.

Gate 3 remains open. This run closes the live reflection regression but not the newly corrected
identity Save As regression, Observer visual inspection, or the remaining planned live-soak scope.
