# Mosaic Gate 3 Runtime Retest - 2026-07-23 A

- **Test run ID:** `gate3-fix-20260723-a`
- **Status:** PARTIAL PASS - startup only; continuity fix not yet verified live
- **Fix commit:** `9a5b58856cd87af85ef94ef0121ef762898b7d3a`
- **Mosaic version:** `0.2-prealpha`
- **RimWorld version:** `1.6.4871 rev591`
- **Package SHA-256:** `ae720adb62ec0993089d2a1db9c4d1f35df53159fa01741b4fbdbf8b3530d060`

## Scope and result

OBSERVED live:

- the exact RimWorld process launched;
- RimWorld reported an NVIDIA GeForce RTX 3080 Ti with 12,086 MB VRAM;
- the active mod configuration contained only Core and Mosaic;
- RimWorld loaded Mosaic `0.2-prealpha` and emitted the Observer-only/no-pawn-control message;
- the bounded 30-line log contained no Mosaic error;
- no Ollama, Qwen, Llama, or LM Studio process was running during the test.

The Windows graphics-capture interface again failed with `0x80004002`, and UI Automation exposed
only a blank RimWorld pane. In accordance with the owner instruction, no blind input, QuickTest
substitution, save editing, save copying, or autostart manipulation was used. The run stopped before
claiming that the main menu was visible.

The fixed two-load checkpoint sequence therefore remains unverified in live RimWorld. Gate 3 is
still open.

## Evidence matrix

| Milestone | Result | Evidence |
|---|---|---|
| Exact commit recorded | PASS | Git commit `9a5b58856cd87af85ef94ef0121ef762898b7d3a` |
| Exact package recorded | PASS | Six-entry package; SHA-256 above |
| Reversible minimal installation | PASS | Installer validation plus Core/Mosaic-only active list |
| Process launched | PASS | Exact process ID and UTC launch time were observed |
| Main menu reached | UNVERIFIED | Graphics capture failed; logs do not directly establish menu visibility |
| Mod loaded without observed error | PASS | Mosaic load line in `Player.log`; no Mosaic error line |
| Test save loaded | NOT ATTEMPTED | Stopped at the graphical checkpoint |
| Identity enrolled | NOT ATTEMPTED | No game was loaded |
| Observer UI inspected | NOT ATTEMPTED | No verifiable graphics surface |
| Save completed | NOT ATTEMPTED | No game was loaded and no save was touched |
| Reload continuity verified | NOT ATTEMPTED | Requires the preceding save milestone |

## Provider and resource boundary

- No local language model was launched.
- Two pre-existing Ollama processes were stopped before installation; zero matching local-model
  processes were observed during the run and at cleanup.
- The RimWorld child process was launched with reflection mode forced to `offline`.
- Google provider environment values were removed from the child process without reading their
  values.
- Mosaic provider dispatch remained paused by default.
- No credential was inspected and no provider call was observed.
- The owner-confirmed system configuration is RTX 3080 Ti with 12 GB VRAM and 16 GB system RAM.
  The GPU was independently confirmed by `Player.log`; system-RAM telemetry was access-denied, so
  the 16 GB value remains owner-confirmed.

## Pre-launch record

Before launch, the run recorded:

- exact commit and clean worktree;
- package SHA-256 and all six package entries with byte sizes;
- original six-item Core-plus-DLC active list;
- planned and actual Core-plus-Mosaic active list;
- developer mode `False`;
- absence of a prior Mosaic settings file and installation;
- pre-test `Player.log` and `Player-prev.log` locations, sizes, timestamps, and hashes.

The detailed pre-launch record and diagnostics remain ignored local artifacts.

## Offline fix evidence

The fix introduces a post-load reflection checkpoint gate:

1. load-time reflection maintenance may change in-memory queue state;
2. the external reflection sidecar cannot advance until a RimWorld save callback is in progress;
3. provider dispatch waits for that first matching post-load save;
4. successful sidecar persistence during the save releases the gate;
5. pending-commit recovery is moved into the save checkpoint.

Offline verification:

- static verification: 97 C# files;
- contract tests: 68 executed, 0 failed;
- default integration harness: six scenarios, 0 failed;
- RimWorld adapter Release build: zero warnings and zero errors;
- short Gate 3 soak: 250 cycles, passed;
- long Gate 3 soak: 5,000 cycles, passed;
- non-installing preflight: passed, six package entries.

The new regression proves that a load-only session leaves reflection generation 2 unchanged for a
second exact load, while a real save callback may advance and reload generation 3.

## Evidence hashes

- Post-run `Player.log`:
  `b420a9ad34f93f1c7c6c3e04ae8342fd66a1ee7d408c79eb9ca8a30929e9a565`
- Local redacted diagnostic:
  `c3dd8478efbb55042a3c7ac4701db4be93733a0391565674339dae57ef37d098`
- Runtime evidence manifest:
  `bffabbb76ad8cc1bcf6d13838c1921f30431e53caa7c91fca3bd7da7556b982a`

The diagnostic remains local because its structured build section includes a user-profile path. It
must not be uploaded without further redaction.

## Cleanup

RimWorld was stopped before cleanup. The original mod list and preferences were restored
byte-for-byte. The temporary Mosaic installation was removed from the active Mods directory and
moved to ignored private quarantine. No Mosaic settings file, test save, identity archive,
experience journal, or reflection sidecar was created by this retest.

## Manual continuation checkpoint

The owner must visually confirm the main menu, then create a new disposable game through the normal
UI. After the first save, the critical fix check is:

1. exit to the main menu normally;
2. load the same disposable save once and confirm Mosaic storage is healthy;
3. exit without making another save;
4. load that unchanged save a second time;
5. preserve `Player.log` before any further action.

PASS requires the second load to report the same identities and healthy reflection storage with no
ahead-generation or read-only diagnostic. This test must not use a valuable save or a file-level
autostart workaround.
