# Goal Execution Replay Recovery

The execution gate's evaluated, accepted, and completed request IDs are persisted in a versioned,
individual/lineage-bound snapshot with a canonical payload hash. Restore validates the invariants
`completed ⊆ accepted ⊆ evaluated` and rejects unsafe IDs, schema drift, identity mismatch, and
integrity failure.

Atomic replacement preserves the last good ledger if a temporary write is interrupted. After
restart, both accepted and rejected request IDs remain non-replayable; an accepted but unfinished
request may record exactly one outcome; a completed request cannot complete twice.

This prevents crash recovery from silently reopening action authority. It remains a reference for a
future RimWorld adapter and does not itself issue or resume game jobs.
