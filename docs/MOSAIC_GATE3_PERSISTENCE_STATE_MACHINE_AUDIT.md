# Mosaic Gate 3 Persistence State-Machine Audit

**Status:** OBSERVED offline; live RimWorld behavior remains untested
**Scope:** `rimworld/0.2-prealpha/` only
**Date:** 2026-07-22

## Audit question

Can a missing, malformed, corrupt, stale, or ahead-of-save persistence component cause Mosaic to
guess which history is authoritative, open an unsafe path, or create replacement identities?

The audited safety invariant is:

> A contradictory persisted state may expose verified read-only evidence for diagnosis, but it
> must not create, rename, archive, enroll, or persist an identity until continuity is unambiguous.

## Result

The audit found and corrected three offline defects:

1. An absent or invalid save-manifest store ID initialized a new identity store even when other
   checkpoint fields proved that Mosaic state had previously existed.
2. Identity synchronization could create or mutate in-memory individuals after identity storage
   had entered read-only mode.
3. Reflection restore accepted a checksum-valid sidecar from a different checkpoint generation by
   taking the larger generation, including uncheckpointed forward state.

The audit also found that an unvalidated store ID was used as a sidecar filename. Store filenames
are now derived only from a parsed GUID; invalid input maps to a fixed diagnostic-only filename,
and no identity, experience, or reflection sidecar is opened by the invalid-manifest load path.

## Deterministic state matrix

| Save manifest / sidecar state | Runtime disposition | Identity mutation | Operator evidence |
|---|---|---:|---|
| No store ID and every checkpoint field pristine | Initialize a new store | Allowed after initialization | New-store message |
| Missing store ID with any generation, history, mapping, or paused-ID evidence | Global read-only | Blocked | Contradictory-manifest error |
| Non-GUID, empty GUID, or traversal-shaped store ID | Global read-only; no sidecar opened | Blocked | Invalid-store-ID error |
| Negative generation/position or malformed experience hash | Global read-only | Blocked | Invalid-checkpoint error |
| Unequal, invalid, duplicate, or orphaned manifest mappings | Global read-only | Blocked | Mapping-specific error |
| Valid manifest plus exact identity store ID and generation | Load verified identities | Allowed | Verified-primary message |
| Referenced identity archive missing | Global read-only | Blocked | Missing-archive error |
| Identity archive store/generation mismatch, truncation, or bad schema/checksum | Global read-only | Blocked | Exact rejection diagnostic |
| Corrupt identity primary with exact verified backup | Load backup read-only | Blocked | Backup-recovery warning |
| Exact reflection store ID and generation | Load verified queue/audit | Governed by all storage gates | Verified-primary message |
| Reflection sidecar missing at generation zero | Initialize empty reflection state | Identity remains independent | New-sidecar state |
| Referenced reflection sidecar missing | Reflection read-only | Identity remains independent | Missing-sidecar error |
| Reflection store ID/generation mismatch or stale backup | Reflection read-only; expose no rejected state | Identity remains independent | Exact rejection diagnostic |
| Experience journal exactly matches position and head hash | Load verified records | Governed by storage gates | Healthy status |
| Referenced experience journal missing, invalid, behind, or divergent | Experience read-only | Identity remains independent | Checkpoint diagnostic |
| Experience journal is a verified extension of the save prefix | Load prefix read-only; require explicit administrative adoption | No automatic adoption | Forward-recovery diagnostic |

## Enforcement points

- `SaveManifestSafetyPolicy` is a pure, deterministic preflight over every serialized manifest
  field. Only an entirely pristine manifest may create a new store.
- Sidecar paths use a normalized GUID filename and never interpolate untrusted manifest text.
- Identity manifest mappings are checked against the archive snapshot before any identity enters the
  component's in-memory dictionary.
- `SynchronizeColonists`, `SynchronizePawn`, and Observer enrollment all reject identity mutation
  while identity storage is read-only.
- Saving in read-only mode preserves the original identity mapping instead of rebuilding it from
  an empty or partial in-memory state.
- Identity and reflection archives require the exact store ID and exact save generation. A verified
  but stale backup is not silently substituted.

Static verification independently requires both synchronization entry points to retain their
read-only guards.

## Offline evidence

- Static verification: 96 C# files checked; passed.
- Contract tests: 67 executed; 0 failed.
- Default integration harness: six scenarios; 0 failed.
- Release compilation: Core, Providers, Tests, IntegrationHarness, and RimWorld 1.6 adapter passed
  with zero warnings and zero errors.

The new adversarial contract matrix covers:

- pristine versus contradictory manifests;
- missing, malformed, empty, and traversal-shaped store IDs;
- negative checkpoints and malformed experience heads;
- unequal, duplicate, invalid, and orphaned identity mappings;
- exact, wrong-store, rollback, and ahead-of-save reflection stores;
- stale reflection backup after primary corruption;
- safe filename normalization.

## Remaining live gate

These results do not prove RimWorld `Scribe` callback order, actual save XML behavior, file locking,
or UI diagnostics. The owner test must still demonstrate that a disposable live save:

- retains the same IndividualId and LineageId across save/load;
- enters visible read-only mode on a controlled sidecar mismatch;
- creates no replacement identity while read-only;
- preserves the original save and external files for rollback and diagnosis.

No live save should be hand-edited merely to exercise malformed-manifest cases. Those cases are
covered offline; the live corruption step remains limited to the unique disposable sidecar named in
the runtime test plan.
