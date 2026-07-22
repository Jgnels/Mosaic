# 13 — Implemented Data Contracts

**Status:** Version 0.1E Core/provider/reflection/persistence/Observer contracts compiled and tested in isolation

This document maps the architecture to the first concrete C# types. It is intentionally narrower than the final memory model.

## Identity and continuity

| Type | Purpose | Invariant |
| --- | --- | --- |
| `IndividualId` | Stable individual identity | Never empty; independent of name/model |
| `LineageId` | Authorized continuity lineage | Preserved through ordinary change and revival |
| `IdentitySeed` | Grounded environment facts | Original sources retained |
| `ContinuityProfile` | Weighted continuity evaluation lens | Weights total 1.0; no sole defining field |
| `IndividualState` | Immutable canonical state slice | Mutations advance version exactly once |
| `LifecycleRules` | Allowed lifecycle transitions | Death blocks ordinary mutation; revival is explicit |

Provider and model identifiers do not occur in `IndividualState`.

`EnrollmentRecord` distinguishes a full Dagmay individual from a known person or a minimal world actor. Only the full level may carry an `IndividualId`, preventing partially created identities.

## Affect

`AffectVector` implements the seven owner-approved dimensions. Each is finite and bounded from -1 to 1. `BlendToward` provides a deterministic bounded update operation.

The vector is not a claim that the individual feels emotion. It is causal internal state intended to influence later salience, memory, interpretation, and choice.

## Events, perception, memory, and belief

| Type | Represents |
| --- | --- |
| `EnvironmentEvent` | Adapter-observed fact with deduplication, time, source, payload, and subjects |
| `PerceivedEvent` | One individual's bounded view with omitted fields and confidence |
| `SubjectiveMemory` | Concise diary interpretation, appraisal, affect, importance, access, tier, and privacy |
| `BeliefRecord` | Proposition, confidence, status, and supporting/contradicting evidence |

These are separate objects by construction. A perception or belief cannot overwrite its source event.

## Interaction and relationships

`InteractionOrigin` distinguishes an external operator from an embodied character. An ordinary RimWorld player cannot silently impersonate a pawn; a perspective/possession adapter must supply the actual environment-entity reference and may link a Dagmay `IndividualId` only when that character is enrolled.

`RelationshipRecord` is intentionally asymmetric. Mira's trust, affection, fear, resentment, and familiarity toward Jo can differ from Jo's state toward Mira while both cite the same factual events.

## Persistence foundations

`InMemoryEventLedger` provides ordered append and deduplication by both event ID and adapter key. `InMemoryIdentityStore` provides version-checked replacement while protecting identity and lineage.

`InMemoryIdentityStore` remains a mutation-test foundation. Durable identity snapshots arrived in 0.1B. Version 0.1C added an append-only hash-chained experience journal whose records preserve the fact → perception → memory provenance path. Version 0.1D adds `ReflectionStoreSnapshot`, `PendingReflectionTask`, and `ReflectionAuditRecord` in a separate atomic, checksummed sidecar. A pending validated commit is recorded before the identity archive changes, allowing startup recovery to distinguish not-applied, already-applied, and conflicting states.

## Model boundary

`IModelProvider` receives a provider-neutral `ModelRequest` and returns a normalized `ModelResult`. The result is diagnostic/untrusted data; it is not itself a state mutation.

The deterministic fake provider emits a complete schema-valid, evidence-preserving proposal without network access. `GoogleAiStudioProvider` implements the same interface using the stateless Google `generateContent` REST operation. It reads the API key only from an environment variable, places it in `x-goog-api-key`, requires JSON structured output, bounds and cancels response reads, records provider/model/operation/token metadata, and normalizes missing credentials, timeouts, cancellation, HTTP failures, rate limits, and malformed envelopes.

`ModelRequest` now carries individual and lineage IDs, base state version, prompt version, system instruction, escaped context, provider-facing JSON schema, evidence IDs, current affect, deadline, and output budget. `ModelResult` remains untrusted and additionally carries retryability, `Retry-After`, finish reason, operation ID, and token use.

## Mutation gate

`ProposedAffectMutation` remains the early mutation-test type. Version 0.1D's provider path uses `ReflectionProposal`, `ReflectionProposalJson`, and `ReflectionProposalValidator`. The strict local decoder requires exactly the documented members, rejects duplicates/unknowns/oversize/nesting errors, and does not trust provider-side schema enforcement. Validation rejects:

- nonexistent identity;
- stale base version;
- dead or archived lifecycle;
- absent/unknown evidence;
- mismatched request or individual ID;
- mismatched lineage or stale base state;
- per-dimension changes above the configured bound; and
- evidence outside the dispatched allowlist or absent from the canonical ledger; and
- dead/archived lifecycle.

Only a fully valid proposal can produce an immutable replacement state. The RimWorld composition root saves a `PendingCommit` audit record, atomically replaces the identity archive, then saves `Committed`; recovery verifies any interrupted interval. Provider name, prose confidence, or fluent output cannot bypass the gate.

## Scheduling foundation

`ReflectionQueue` remains the in-memory scheduling foundation. `PersistentReflectionQueue` adds durable attempt count, next-attempt time, failure code, evidence-preserving coalescing, priority aging, capacity protection, and targeted removal for a paused individual without removing a preserved in-flight task. `ReflectionBudgetGate` enforces per-session attempts, rolling hourly requests, daily estimated tokens, provider `Retry-After`, exponential backoff, and circuit cooldown.

Version 0.1E persists queue and audit changes around every dispatch and commit and exposes validated host settings for session/hour/day/heartbeat limits. Multi-individual batches, fairness metrics, jitter, and explicit cancellation during game unload remain later hardening work.

## Observer projections

`ObserverSystemSnapshot`, `ObserverIndividual`, `ObserverMemory`, `ObserverReflection`, `ObserverSeedFact`, and `ReflectionUsageSummary` are immutable Core projections. They contain no `Pawn`, Verse, Unity, provider-client, or UI types. Existing, paused, and archived individuals retain identity and lineage fields; a not-yet-enrolled world entity cannot masquerade as a full individual. Usage calculations deduplicate token metadata across audit stages by request ID. The RimWorld host includes a private reflection only when its locally validated `PendingCommit` payload has a later durable `Committed` record.

## Known omissions

- no semantic index;
- no ordinary disclosure-filtered pawn mind tab or colony overview; the implemented 0.1E route is Restricted Observer/runtime settings only;
- the 0.1E UI and budget controls have not yet been compiled or run against the owner's actual RimWorld assemblies; the predecessor's live Google path is verified;
- no local provider transport;
- reflection prose is retained in private audit records but is not yet promoted into a richer autobiographical memory/consolidation model;
- synchronous bounded sidecar writes still occur in the polling component and require performance measurement; and
- the friendly recovery screen and explicit branch choice for deliberately loading an older RimWorld save beside newer external sidecars are not implemented, so all 0.1E experiments must use copied saves and data backups; and
- no action or pawn-control code.
