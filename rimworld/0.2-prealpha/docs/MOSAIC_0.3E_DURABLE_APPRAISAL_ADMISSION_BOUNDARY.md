# Mosaic 0.3E durable appraisal admission boundary

Mosaic 0.3E admits a v39 provisional appraisal proposal into checkpoint-aligned,
Mosaic-owned durable state. The implementation is inert library code and an offline
verification harness. It is not wired into RimWorld.

## Dependency and provenance

- Contract: `mosaic.0.3e.checkpoint-aligned-durable-appraisal-admission.v1`
- v40 gate digest: `639e5b4ff2d7629fed5b76303b21bbcecbf03f167068d2d4046cb9ef1b153ce9`
- Corrected v39 dependency digest: `c8557ab218dc0a6136a960cfb495dd4853e5dd3ad81d8eadd7b36082b1b47819`
- Shared fixture SHA-256: `bc60bbde6ece4ab5853fac575819d0730e027cf7fd11f58dd3aa9eccbda9f490`

The durable coordinator accepts only a proposal attested by the v39 store's internal
trusted seam. Admitted-event and checkpoint receipts also use internal trusted factories.
Success is a distinct receipt created only after durable materialization and reread
verification; an attempt or failure is never success.

## Canonical transaction

Preparation authenticates the proposal, owner, lineage, save, world, store set, admitted
event, and exact checkpoint fingerprint without changing canonical state. Application
holds one coordinator lock, completes conflict preflight for every destination before the
first write, materializes the developmental record plus target-selective affect and
relationship changes, and rereads every result before issuing success.

Requested and actual deltas are both retained. Clamping therefore remains observable, and
only a state actually targeted by the proposal advances its version. A replay converges
without double counting. Simulated crash placements never return success and converge
through the same durable outbox entry.

## Storage and bounds

The outbox snapshot contains protocol state only: admission entries, pending and failure
indexes, recent terminal quarantine, deterministic chain state, and its fixed-memory
terminal filter. It never contains the complete canonical developmental ledger. Primary,
backup, temporary-file, replace, hash, and reread behavior follows the repository's
existing atomic storage convention.

Global pending admissions, unacknowledged failures, and recent terminal entries are each
bounded at 64. The terminal membership filter is a fixed 1 MiB deterministic structure.

## Authority exclusions

Core contracts are plain data and contain no RimWorld types. The slice has no provider
call, UI or dialogue presentation change, planner authority, environment-adapter
authority, Harmony patch, pawn/job authority, save field, runtime wiring, installation,
configuration change, or save access. The outbox coordinates Mosaic-owned durable data;
it cannot generate dialogue or control a pawn.

The focused suite mirrors all 58 names from `v40-final-unit-run1.json`. The integration
harness includes a genuine v37→v38→v39→v40 path and a separate deterministic executable
fixed at 50,000 admissions.
