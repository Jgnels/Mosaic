# Mosaic Pre-Soak Hardening Handoff

**Session date:** 2026-07-24
**Branch:** `codex/v0.2-pre-soak-hardening-20260724`
**Worktree:** `C:\Users\Jeff\Dagmay-worktrees\mosaic-v02-pre-soak-hardening-20260724`
**Mandated base:** `abd46c58ad4dde505d18575ad0c4e338d62dd76b`

## Starting-state verification

- The mandated base exists and is `Bound presentation context collections`.
- The protected overnight worktree was clean.
- The published protected overnight branch actually pointed to
  `c200634914d180f9040491c8c3bae4f65a7f3dbe`, not the supplied expected head.
- The follow-up branch was nevertheless created from the exact mandated base, as requested.
- Neither protected branch was modified.

## Session boundaries

- No RimWorld launch, mod installation, save/configuration mutation, provider invocation, or local
  model inference.
- No access to or mutation of the prepared long-soak evidence.
- No changes under `rimworld/0.1-closure-candidate/`.

## Verification and implementation results

### Independent clean-source verification

- A Git archive of the mandated base contained 98 tracked and 98 archived C# files.
- All six explicit `<Compile Include>` targets existed.
- Static verification, 78 contracts, six integration scenarios with 603 assertions, and all three
  Release builds passed from the base archive.
- Critical package inspection then found local profile paths embedded in all three managed DLLs
  and the packaged PDB. The earlier preflight had not detected this privacy/reproducibility defect.
- The final implementation head was independently exported again. Static verification, 81
  contracts, six integration scenarios with 603 assertions, all Release builds, and the hardened
  package preflight passed.
- Final clean-source package SHA-256:
  `1387154f4dfee7f9a1de10a8d8e9708b7ec49e6fd5c3c55c590524227951d7e9`.
- Machine-readable record: `docs/MOSAIC_CLEAN_SOURCE_VERIFICATION_20260724.json`.

### Implementation commits

1. `c7f0c3a` — map deterministic source paths and make package preflight reject machine-specific
   profile paths. The prior leaking package was exercised as a negative fixture and failed.
2. `421aeee` — retain the winning new evidence when saturated queue coalescing upgrades priority
   and task kind.
3. `7d88538` — reject default or duplicate evidence identifiers and default owners at the
   presentation boundary.
4. `8bd2096` — reject malformed reflection tasks with empty, oversized, duplicate, or default
   evidence and default task/owner IDs.

### Critical review conclusions

- The two diagnostic sources are tracked and present in an index-only export.
- Reflection memory and source-event ownership checks are correctly applied before context
  construction.
- Both queue implementations now isolate owners. The in-memory string composite cannot collide
  across valid `IndividualId` prefixes, while the persistent queue compares owner and key
  structurally.
- Stable identifier tie-breakers remove insertion-order dependence for equal memory/event ranks.
- Privacy threshold behavior and all three privacy classes are covered.
- Static source tracking and explicit compile-target checks work both with Git metadata and from a
  Git archive.
- The overnight documentation at the mandated base lagged the final protected branch by two
  documentation commits; the observed protected remote head was `c200634...`.
- No external source was copied or adapted in these implementation changes.

## Remaining boundaries

- Gate 3 remains `OWNER_ACTION_REQUIRED`; no runtime milestone was attempted or claimed.
- Gate 4 compatibility work and external-mod bridges remain blocked on the owner-controlled soak
  and product/license decisions.
- Package ZIP hashes can differ between builds because archive metadata is not normalized; the
  hardened preflight records exact hashes and verifies entry contents and path privacy.
