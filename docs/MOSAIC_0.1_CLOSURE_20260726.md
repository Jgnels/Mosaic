# Mosaic controlled 0.1 reliability closure

**Decision date:** 2026-07-26
**Status:** CLOSED
**Scope:** frozen Observer-first 0.1 reliability campaign
**Historical name:** Dagmay

## Decision

The controlled Mosaic 0.1 reliability campaign is complete. No additional owner gameplay, soak,
build, or packaging verification is required for this scope.

This decision closes the sequence comprising social-path certification, persistence torture,
failure isolation, checkpoint safety corrections, unchanged Save As idempotence, the controlled
Gate 3 long soak, and deterministic privacy-hardened release packaging.

## Live Gate 3 evidence

- certified commit: 457f1815d625f7a121f444d972d4880e70c805cb
- certified package SHA-256: d845e5165a4977b3bdc4af98f55fd6c5485970d127411c023b575409b3db676a
- configuration: RimWorld Core + Mosaic only, forced-offline reflection
- runtime: two independent 60-minute blocks
- restart boundary: full RimWorld process restart between blocks
- live samples: six total at T+0, T+30, and T+60
- identity storage: healthy in every sample
- experience storage: healthy in every sample
- reflection storage: healthy in every sample
- individual/lineage continuity: matched throughout
- provider dispatches: zero
- local-model processes: zero
- parsed error/exception lines: zero
- Mosaic read-only warnings: zero
- checkpoint verification: complete agreement after each block
- identity generation: 2 -> 8 -> 12
- experience position: 1 -> 7 -> 11
- reflection generation: 1 -> 2 -> 3

The Player.log collection harness required a shared-read correction while RimWorld held the log
open. The correction changed only evidence-file hashing/copying and did not alter Mosaic source,
package, save, sidecar, provider behavior, or runtime state.

## Final build and package evidence

- final certified commit: 21d6bf28138513d06e950cd9604592b2410ff7ec
- final deterministic package SHA-256: 5a79db4f3fc2b3306f8ff2fc66f8e4653de1b9b1f68436b9fe5882c4d6ae4ad7
- static verification: 98 C# files
- contract tests: 70 executed, 0 failed
- integration harness: 6 scenarios, 603 assertions
- Core build: 0 warnings, 0 errors
- Providers build: 0 warnings, 0 errors
- RimWorld adapter build: 0 warnings, 0 errors
- package firewall: 1 accepted fixture; 10 adversarial packages rejected
- package preflight: PASS
- package inventory: exactly 6 declared entries
- machine-specific paths: absent
- reproducibility: byte-identical package from two independent standard Windows checkouts
- frozen 0.1 source tree: unchanged
- final tracked worktree: clean

No RimWorld launch, installation, provider call, local model, save, configuration, credential, or
Gate 3 evidence mutation occurred during final packaging certification.

## Scope preserved

This closure does not authorize direct pawn control or unrestricted model authority. Model output
remains an untrusted proposal, mismatched state fails closed, and presentation reads must not mutate
canonical state.

## Provenance boundary

The recovered source self-identifies as 0.1K and contains later 0.1L/0.1M and safety work. The final
certified package retains the existing 0.2-prealpha compatibility identifier. This closure does not
claim that a separately versioned historical artifact named 0.1RC was recovered or previously
existed.

## Post-closure work

The following remain 0.2 or post-closure integration work:

- full normal-mod-stack compatibility;
- RimTalk integration;
- controlled DLC and optional-mod groups;
- large-colony and long-horizon performance;
- Character Why and later presentation work;
- bounded goal/planning mechanisms;
- any separately authorized provider protocol.

A later compatibility failure does not invalidate this controlled baseline unless it demonstrates a
contradiction in the evidence recorded here.
