# Atomic Character Runtime Checkpoint

Mosaic's relationship history, goal continuity, execution replay ledger, world revision, identity,
lineage, and checkpoint generation are persisted as one integrity-checked bundle. Each nested durable
component retains its own integrity checks; the outer hash binds them into a single recovery point.

Restore rejects outer tampering, nested identity/lineage mismatch, invalid goal state, and checkpoint
rollback below a caller-supplied minimum generation. Atomic replacement preserves the last good
bundle when a temporary write is interrupted.

This directly addresses the historical two-store checkpoint mismatch: new relationship history
cannot be recovered alongside older action authority without an explicit integrity or generation
failure. The contract is a SyntheticLab reference until equivalent C# save integration exists.
