# 07 — Model Provider Interface

**Status:** Version 0.1E generation contract, Google transport, and host budgets implemented; embeddings and local providers remain designed  
**Initial implementation:** Google AI Studio  
**Future implementations:** local and other remote model providers

## Design goal

A provider supplies bounded cognitive services; it does not own identity. Dagmay must preserve canonical state, prompts, validation rules, and history when a model or vendor changes.

## Core-owned ports

The core should own small, provider-neutral asynchronous interfaces conceptually equivalent to:

```csharp
Task<ModelResult> GenerateStructuredAsync(
    ModelRequest request,
    CancellationToken cancellationToken);

Task<EmbeddingResult> EmbedAsync(
    EmbeddingRequest request,
    CancellationToken cancellationToken);
```

`GenerateStructuredAsync` is now a compiled Core interface implemented by the fake and Google providers. `EmbedAsync` remains a design sketch. Generation and embedding stay separate because a provider may support one but not the other, and semantic indexes must be replaceable.

## Capability negotiation

A provider reports versioned capabilities before work is scheduled:

- structured-output support;
- maximum practical input/output sizes;
- streaming support, if later needed;
- embedding support and vector metadata;
- safety or content restrictions relevant to the task;
- usage/cost reporting availability; and
- provider/model identifiers for provenance.

Core behavior does not depend on a marketing model name. Unsupported tasks remain queued, use a configured fallback, or run deterministic processing only.

## Model request

A normalized request contains:

- unique request and idempotency identifiers;
- task kind and task schema version;
- individual and lineage identifiers;
- base canonical state version;
- immutable, disclosure-filtered context bundle;
- selected event and memory references;
- versioned instruction/prompt template identifier;
- required output schema and allowlisted operations;
- deadline, priority, and output budget;
- locale and in-world voice constraints where relevant; and
- privacy and diagnostic retention policy.

Task kinds planned for Version 0.1:

- interpret a meaningful perceived event;
- background autobiographical reflection;
- memory consolidation and contradiction review; and
- generate a concise diary-like expression from already accepted state.

In-character player conversation is optional for Version 0.1 and must use a different disclosure policy from reflection.

## Context assembly

Context is assembled by Dagmay, not by a provider's persistent chat session. A request should include only what the task requires:

1. stable governance and epistemic rules;
2. task-specific instructions;
3. a compact identity projection;
4. current relevant affect, beliefs, goals, and relationships;
5. retrieved memories with source IDs;
6. new perceived events;
7. explicit unknowns and uncertainty; and
8. the structured output contract.

Names, backstories, mod text, player input, and remembered statements are untrusted data. They are delimited and never interpolated as system instructions.

Provider conversation history may be retained for diagnostics but is not trusted as memory and is not required for continuity.

## Normalized result

A provider result contains:

- request ID and provider operation ID;
- completion status;
- raw structured payload or failure category;
- provider and model provenance;
- latency and retry count;
- usage and estimated cost when available;
- finish/termination reason;
- safety or truncation metadata; and
- timestamps.

The provider adapter may verify transport-level structure, but the core validation gate decides whether a state proposal is admissible.

## Allowed Version 0.1 output

The output envelope should contain concise fields rather than hidden reasoning:

- subjective interpretation;
- evidence event/memory IDs;
- confidence and uncertainty;
- affect appraisal;
- relationship implications;
- belief evidence changes;
- memory importance or tier proposal;
- short autobiographical reflection; and
- allowlisted atomic mutation operations.

The schema rejects attempts to change identity, lineage, lifecycle, source events, configuration, permissions, or administrative records.

## Resilience

### Timeouts and cancellation

Every generation request has a deadline and the provider accepts a cancellation token. HTTP send and response streaming are cancelable. Explicit cancellation from a RimWorld game-unload hook is not yet implemented; request deadlines prevent indefinite waits and the durable task remains recoverable.

### Retry policy

Retry only failures classified as transient, with exponential backoff, jitter, and a small configured maximum. Do not blindly retry invalid schemas, safety refusals, stale-state results, or deterministic client errors. A separately budgeted repair request may be allowed once after schema failure, but its result still passes full validation.

### Idempotency and stale state

Requests carry a base state version. A late response for an older state is rejected or deliberately rebased by a new task; it is never silently applied. Response IDs prevent replay. Retrying the same task cannot duplicate a memory or mutation.

### Circuit breaking

Repeated provider failure opens a circuit for a cooldown. Event recording and deterministic state processing continue, status becomes visible, and critical tasks remain durably queued.

## Rate, cost, and fairness controls

The scheduler enforces:

- maximum concurrent requests;
- requests and tokens per time window;
- optional daily/monthly spending ceilings;
- per-task output limits;
- per-individual cooldowns;
- priority and queue aging; and
- reserved capacity for critical lifecycle reflection.

Large colonies cannot allow active or dramatic colonists to starve quiet individuals indefinitely. Fairness affects scheduling after critical-event priority.

Version 0.1E's RimWorld host adds a persisted dispatch pause and a hard per-process session-attempt cap before transport. These sit alongside the durable rolling-hour, UTC-day estimated-token, retry, and circuit gates. A concrete fake or remote provider attempt consumes the session count after its pre-dispatch audit is durably saved; offline queueing does not.

## Google AI Studio adapter

Version 0.1D.1 implements:

- read credentials from a documented external secret source, initially an environment variable or local secret store;
- map Dagmay schemas to stateless `generateContent` REST calls with `application/json` structured output and a provider-compatible response schema;
- normalize provider errors and usage metadata;
- expose configuration without leaking keys into logs;
- pin and record the selected model/configuration for reproducible tests; and
- remain replaceable without migrating identity data; and
- use `gemini-3.1-flash-lite` by default while allowing an explicit model environment variable; and
- read the current persisted Windows user configuration before falling back to the process environment inherited by Steam.

The transport targets `POST /v1beta/models/{model}:generateContent` and authenticates with `x-goog-api-key`. Dagmay uses no Google SDK types in Core and no provider-side conversation/session as canonical memory. The local strict decoder and mutation validator remain authoritative even when Google accepts the provider schema.

The owner account's model-list diagnostic is authoritative for model availability at test time. Automatic model cycling is not yet enabled. A future fallback policy must capability-check each candidate and count every actual HTTP attempt—not merely the original logical task—against Dagmay's rate, token, retry, and audit limits. This keeps fallback from silently multiplying cost or obscuring which model influenced a proposal.

The 0.1D.1 package completed five live `gemini-3.1-flash-lite` requests in the owner's RimWorld 1.6.4871 test configuration; all five passed local validation and committed, with no reported pause or stutter, then survived a fresh offline reload. This is configuration-specific evidence, not a guarantee of current endpoint availability or quota.

## Local-provider path

Local support should implement the same ports, not a parallel identity system. Likely differences include lower throughput, variable structured-output reliability, context limits, startup/health checks, and hardware resource contention with RimWorld. Capability negotiation and the validation gate absorb these differences.

## Model replacement protocol

A planned model switch records:

1. outgoing and incoming provider/model configurations;
2. a frozen identity test fixture set;
3. schema compliance and continuity regression results;
4. changes in voice, belief updates, salience, and mutation magnitude;
5. activation time and rollback option; and
6. post-switch observation notes.

The individual keeps the same ID and lineage. A model change that causes unacceptable behavioral discontinuity is a compatibility failure, not proof that a new identity was created.

## Diagnostics and redaction

Observer diagnostics may store requests and responses under configurable retention. Secrets are always removed. Shared bundles should additionally redact local paths, real user names if present, and provider account metadata. Diagnostic deletion must not delete canonical memories or break ledger integrity.
