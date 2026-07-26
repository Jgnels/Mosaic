# ADR: recoverable checkpoint-aligned dialogue admission outbox

**Status:** Accepted offline implementation
**Date:** 2026-07-26
**Scope:** Mosaic 0.2 factual dialogue evidence only

## Context

The canonical event ledger and durable experience journal remain separate
destinations. They do not share a filesystem transaction. The earlier
checkpoint policy correctly blocked an unsafe best-effort dual write, but it
could only report readiness.

## Decision

Mosaic records a complete factual dialogue admission in an integrity-checked
outbox before either destination is changed. Each immutable entry:

- derives its identity exactly from the deterministic `EventId`;
- binds to the Mosaic store, stable save lineage, world, and checkpoint
  generation;
- contains the full factual event and factual-only journal payload;
- carries a SHA-256 hash over canonical deterministic event encoding; and
- is either pending or completed.

The lock-serialized recovery coordinator inspects both destinations. It writes
only a missing side, rereads and verifies both sides, then persists a completed
tombstone. A completed tombstone remains through its checkpoint and may be
compacted only when a later successful checkpoint is explicitly advanced.

The outbox itself uses deterministic encoding, an envelope checksum,
same-directory temporary replacement, and primary/backup recovery. Store,
save, world, rollback, forward-generation, conflicting-payload, invalid
journal, and read-only mismatches fail closed.

This is a recoverable protocol. It is not a claim that the event ledger,
journal, and outbox participate in one filesystem-level atomic transaction.

## Authority boundary

Only the factual event and factual-only journal record may be admitted. The
coordinator has no authority to create perception, subjective memory,
relationship changes, mood effects, beliefs, goals, pawn jobs, or gameplay
actions.

## Verification

Twenty offline contract tests cover every interruption boundary, both
one-sided destination states, restart replay, identical and conflicting
duplicates, checkpoint mismatch and rollback, read-only and invalid storage,
primary/backup recovery, unchanged Save As, binding mismatch, deterministic
round trip, tombstone retention/compaction, and interrupted replacement.
