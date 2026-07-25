# ADR: checkpoint-aligned dialogue admission

**Status:** Accepted by the project owner on 2026-07-24
**Scope:** Mosaic 0.2 offline dialogue foundation

## Context

Batch 02 creates a deterministic `DialogueEventAdmissionPlan` only after an
utterance has been strictly decoded, independently validated, and actually
displayed. The plan contains one factual `EnvironmentEvent` and one
factual-only `ExperienceJournalRecord`; it contains no automatic perception,
subjective memory, relationship delta, mood effect, belief, goal, or gameplay
action.

Repository truth exposes two distinct commit surfaces:

1. `IEventLedger.Append` changes the canonical event ledger.
2. `DurableExperienceJournal.Append` immediately appends and flushes a journal
   line.

They do not share a transaction, rollback operation, pending-commit record, or
recoverable outbox. Executable tests reproduce both one-sided write orders.
They also show that the event ledger rejects a repeated deterministic EventId
while the journal can append the same factual event again after restart.

## Decision

Durable dialogue admission is checkpoint-aligned.

Displaying speech may prepare a pending factual admission, but it does not
immediately mutate either durable surface. A future coordinator may release a
pending admission only when:

- the save checkpoint generation exactly matches the prepared generation;
- all required storage is writable;
- the existing experience journal is either valid or genuinely absent;
- the deterministic factual EventId is not already admitted;
- atomic or recoverable behavior across every durable surface is proven.

The current branch implements the pure fail-closed readiness policy but does
not implement or live-wire a coordinator.

## Immediate gameplay response

Checkpoint alignment does not require characters to appear emotionally inert
until the next save. A future separately approved appraisal layer may use the
witnessed displayed utterance immediately as provisional in-session context.
That response must be reversible and cannot claim durable relationship, mood,
memory, belief, goal, or identity mutation.

Any lasting interpersonal effect remains an untrusted proposal requiring
evidence, validation, provenance, and checkpoint-safe canonical admission.
This work does not implement automatic reciprocal feelings or any
dialogue-to-gameplay effect.

## Required future transaction design

Live wiring remains blocked until one of these is proven:

1. a single save-atomic record owns the pending admission and both projections
   can be rebuilt idempotently; or
2. a durable outbox records prepare/commit state and recovery reconciles both
   surfaces exactly once.

Recovery must cover journal-first failure, ledger-first failure, interruption
between surfaces, restart replay, stale checkpoints, read-only storage,
malformed journals, and deterministic prevention of duplicate admitted facts.

## Consequences

- A crash before a matching save may discard uncheckpointed dialogue history.
- Mosaic prefers bounded loss of provisional dialogue over contradictory or
  duplicated durable character history.
- Tomorrow's prepared Gate 3 soak package and evidence remain unchanged.
