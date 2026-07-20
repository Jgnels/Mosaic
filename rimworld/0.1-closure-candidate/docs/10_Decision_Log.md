# 10 — Decision Log

**Status:** Living document  
**Initial date:** 2026-07-16

Statuses: **Accepted**, **Provisional**, **Superseded**, **Rejected**.

## D-001 — Mission and continuity invariant

**Status:** Accepted from project kickoff

Dagmay studies persistent artificial individuals in simulated environments. Identity continuity is carried by a versioned causal lineage, not by the selected language model.

**Consequences:** Canonical identity must be external to model context. Model replacement is a compatibility event, not automatic death or creation.

## D-002 — Core and adapters are separate

**Status:** Accepted from project kickoff

Create a game-independent `Dagmay.Core`, provider adapters, and environment adapters beginning with RimWorld.

**Consequences:** Core cannot reference RimWorld, Unity, Harmony, or provider SDKs. Translation work is explicit at boundaries.

## D-003 — Version 0.1 is Observer only

**Status:** Accepted from project kickoff

Version 0.1 observes, stores, reflects, and displays. It does not influence pawn behavior.

**Consequences:** No action executor is implemented. Harmony patches require an observation/persistence/UI audit.

## D-004 — Hybrid grounded identity seed

**Status:** Accepted from project kickoff

Initial identity derives from actual backstories, traits, passions, skills, ideology, genetics, relationships, health, age, and circumstances.

**Consequences:** Missing facts remain missing. The source snapshot persists independently of later interpretation.

## D-005 — Bounded perception

**Status:** Accepted from project kickoff

Individuals receive limited first-person or communicated information, not omniscient map knowledge.

**Consequences:** The adapter creates per-individual perceived events; debug facts cannot leak into ordinary cognition.

## D-006 — Fact, memory, and belief are distinct

**Status:** Accepted from project kickoff

Store normalized environment events, perceived events, subjective memories, and beliefs as linked but separate records.

**Consequences:** False and conflicting beliefs are representable without corrupting the factual ledger.

## D-007 — Layered selective memory

**Status:** Accepted from project kickoff

Use recent, significant long-term, and core autobiographical memory with semantic retrieval, consolidation, reinterpretation, and forgetting.

**Consequences:** Dagmay is not a transcript. Provenance and mutation history remain inspectable.

## D-008 — Affect is dimensional and causal

**Status:** Accepted in principle; axes provisional

Begin with a limited continuous basis that can combine into richer emotional concepts. Affect changes attention, salience, memory, interpretation, communication, and later decisions.

**Consequences:** Emotion-only display statistics fail acceptance. Initial axes await owner guidance.

## D-009 — Google first, provider-neutral always

**Status:** Accepted from project kickoff

Google AI Studio is the first remote provider; local models follow through the same interface.

**Consequences:** Provider/model metadata is provenance only. Current API details are verified at implementation time.

## D-010 — One-minute timing is a scheduler heartbeat

**Status:** Provisional architecture interpretation

Approximately once per real-world minute, the scheduler evaluates background reflection work. This is not one call per colonist per minute.

**Rationale:** A colony above 40 colonists would otherwise exceed 40 background calls per minute before event-triggered work.

**Consequences:** Salience, budgets, fairness, coalescing, and queue aging select actual calls. Owner must approve cost/cadence priorities.

## D-011 — Model output is an untrusted proposal

**Status:** Accepted from engineering principles

All structured output passes schema, reference, range, stale-state, continuity, and replay validation and commits atomically.

**Consequences:** Invalid output is quarantined and makes zero canonical changes.

## D-012 — Offline capture remains functional

**Status:** Accepted from project kickoff

The system records events and deterministic changes without a model. Reflection waits in a durable queue.

**Consequences:** Event time and interpretation time are separate; occurrence-time context must be captured.

## D-013 — Private state and Observer Mode are separate

**Status:** Accepted from project kickoff

Ordinary interfaces apply disclosure rules. A gated Observer Mode exposes supported diagnostics for debugging and ethical review.

**Consequences:** Observer data is not in-world knowledge. No hidden chain-of-thought is requested or stored; concise summaries and evidence are used.

## D-014 — Initial self-knowledge is in-world only

**Status:** Accepted from project kickoff

Individuals do not initially know they are AI, models, simulated beings, or game characters.

**Consequences:** Prompts, errors, UI, logs, and compatibility bridges need disclosure boundaries. Future disclosure requires separate review.

## D-015 — Death archives; copies fork

**Status:** Accepted from project kickoff

Death stops normal active processing and preserves a full archive. Multiple active copies are not created silently.

**Consequences:** Revival, restoration, transfer, duplication, and reincarnation have distinct lifecycle operations. Exact revival policy remains open.

## D-016 — Versioned ledger plus snapshots

**Status:** Provisional architecture decision

Use an append-only provenance ledger and checksummed snapshots behind storage interfaces. Corrections append records.

**Consequences:** State can be recovered and audited. The concrete storage engine remains open.

## D-017 — Save-linked external identity store

**Status:** Provisional architecture decision

Keep a compact Dagmay manifest in the RimWorld save and larger portable identity records in an external Dagmay store.

**Consequences:** Large identity data is provider-neutral and exportable, but checkpoint mismatch and recovery become release-blocking work.

## D-018 — In-character chat is not a v0.1 release blocker

**Status:** Provisional scope decision

Version 0.1 must display ordinary and observer projections; player conversation is a stretch feature while RimTalk compatibility is investigated.

**Consequences:** The first vertical slice remains small. The player's in-world role requires owner guidance before chat becomes canonical experience.

## D-019 — Cultural context is a project obligation

**Status:** Accepted from project kickoff

The name is documented in connection with the sacred handwoven abaca textile of the Mandaya people of Davao Oriental. The weaving metaphor is presented as Dagmay's architecture, not as a claim about Mandaya worldview.

**Consequences:** Broad release requires credible sourcing and informed cultural review; sacred motifs should not be copied as generic branding.

## D-020 — Maturity claims use explicit evidence labels

**Status:** Accepted from project kickoff

Use Designed, Implemented, Compiled, Tested in isolation, and Tested in RimWorld as separate statuses.

**Consequences:** Status is updated item by item. Versions 0.1A, 0.1B, and 0.1C were compiled and tested in RimWorld by the owner. Version 0.1D is implemented, compiled in the available reference environments, and tested in isolation; owner-side RimWorld testing remains open.

## D-021 — Continuity is evaluated as a weighted combination

**Status:** Accepted by project owner 2026-07-16

No single trait, memory, relationship, value, or voice marker defines continuity. The initial evaluation profile weights grounded seed 30%, autobiographical history 20%, relationships 20%, values/commitments 20%, and expression/habits 10%.

**Consequences:** The weights are an explicit evaluation lens, not a metaphysical score or unrestricted mutation formula. Their evolution requires a recorded policy.

## D-022 — Initial affect uses seven dimensions

**Status:** Accepted by project owner 2026-07-16

Version 0.1 uses valence, arousal, threat/safety, agency/control, attachment/affiliation, certainty/confusion, and social standing.

**Consequences:** Each dimension is bounded from -1 to 1. Rich emotion labels remain contextual interpretations rather than a closed vocabulary.

## D-023 — Enrollment and spending are selective and adaptive

**Status:** Accepted in principle; numeric defaults provisional

Dagmay may govern selected colonists rather than the entire colony. Start with four or fewer enrolled individuals and a conservative request budget. Non-enrolled colonists may still appear as people in events and relationships without receiving a full Dagmay identity.

**Consequences:** Runtime cost scales primarily with enrolled individuals and meaningful events, not raw colony population. Shared-event batching, coalescing, salience, and local deterministic processing are required. Version 0.1E defaults to `gemini-3.1-flash-lite`, one concurrent request, 4 attempts per game session, 12 attempts per rolling hour, and 40,000 estimated tokens per UTC day; actual account quota remains external and must be observed.

## D-024 — Player interaction follows environment embodiment

**Status:** Accepted by project owner 2026-07-16

The player is not automatically made an in-world character. RimWorld uses its normal external-operator relationship unless another mod provides an embodied pawn perspective. Minecraft and The Sims interactions are attributed to the player's character.

**Consequences:** Adapters own interaction attribution. Core must support both external-operator and embodied-character origins without giving individuals simulation self-knowledge.

## D-025 — In-world revival continues the lineage

**Status:** Accepted by project owner 2026-07-16

Normal in-world resurrection continues the same individual and lineage while preserving the death, inactive interval, and post-revival integration period.

**Consequences:** Backup restoration remains a different operation and cannot silently replace lived history.

## D-026 — Subjective writing is concise

**Status:** Accepted by project owner 2026-07-16

Subjective memories use concise first-person diary language paired with structured facts and provenance.

**Consequences:** Literary flourish is subordinate to causal clarity, lower runtime cost, and inspectability.

## D-027 — Dagmay remains private personal-use research for now

**Status:** Accepted by project owner 2026-07-16

No public license or release is required during the personal-use phase. The owner's family has a direct Mandaya connection through his wife and her father, who was raised in Mati, Davao Oriental.

**Consequences:** That connection is meaningful personal context but is not represented as community-wide authorization. Public release would reopen licensing, sourcing, and cultural review.

## D-028 — Development subscription and runtime inference are separate

**Status:** Accepted architecture clarification 2026-07-16

ChatGPT Plus/Codex usage supports development work. Dagmay runtime inference uses a separately configured provider such as Google AI Studio or a local model.

**Consequences:** No Dagmay runtime component assumes access to ChatGPT account entitlements or embeds a ChatGPT session. Provider credentials remain external and optional.

## D-029 — Identity archives fail closed

**Status:** Accepted implementation decision 2026-07-16

Version 0.1B stores provider-neutral identity snapshots in a versioned envelope with a SHA-256 checksum. Writes replace the primary file atomically where the platform supports it and preserve the prior verified generation as a backup. The RimWorld save holds the store ID, generation, and pawn-to-individual mapping.

**Consequences:** Invalid, missing, mismatched, or backup-recovered state enters explicit read-only safety mode instead of silently generating replacement identities or merging histories. A beginner-safe recovery interface is required before wider use. The `.dagmay` archive is copyable but is not yet the final human-readable export format.

## D-030 — Version 0.1C begins with bounded polling observation

**Status:** Accepted implementation decision 2026-07-16

The first experience adapter polls enrolled map colonists every 600 game ticks for critical Food/Rest/Mood threshold crossings, health-condition set changes, skill-level gains, and death. It stores immutable factual events separately from first-person perceptions and concise deterministic memories. Experience records form a hash-chained journal linked to the RimWorld save checkpoint.

**Consequences:** This avoids broad Harmony patching while the causal and persistence pipeline is still being validated. Polling can miss brief events between scans and does not yet capture social interaction, witnesses, exact injury causation, or conversations. Those omissions are explicit limits, not permission to infer unseen facts. Model calls remain unnecessary in 0.1C.

## D-031 — External cognition mods are bridges, not identity authorities

**Status:** Accepted architecture decision 2026-07-16

RimTalk, RiMind, Free Will, and future environment-specific mods may provide optional integration surfaces or architectural reference material. They do not own Dagmay's canonical identity, autobiography, intrinsic state, or continuity lineage.

**Consequences:** RimTalk integration should use its supported add-on API and a disclosure-filtered context projection. RiMind patterns may inform independent implementations, but its compiled memory store is not imported as canonical state. Free Will patterns remain reserved for the Advisor and agency stages. External provider calls and observed dialogue must be deduplicated, attributed, and kept behind optional adapters.

## D-032 — Dagmay absorbs capabilities, not external identity ownership

**Status:** Accepted architecture clarification 2026-07-16

General capabilities learned from RimTalk, RiMind, Free Will, or another simulation are implemented behind Dagmay-owned Core contracts when they belong to identity, memory, reflection, motivation, or continuity. An external integration may expose environment-specific observations or interfaces, but cannot become the substrate of the person.

**Consequences:** Version 0.1D has no RimTalk dependency. A future RimTalk module can act as one optional RimWorld conversation surface. The same Dagmay identity remains loadable by a Sims, Minecraft, or custom-world composition root after environment-specific extrinsic state is translated or set aside.

## D-033 — Reflection is stateless at the provider boundary

**Status:** Accepted implementation decision 2026-07-16

Each reflection request is assembled from Dagmay's own compact identity projection, evidence, and retrieved memories. Version 0.1D uses Google's stateless `generateContent` operation rather than relying on provider-held chat history or sessions.

**Consequences:** Replacing Google or its model requires no identity-store migration. Provider history is diagnostic only. Request IDs, lineage, base state, evidence, and local validation determine admissibility.

## D-034 — Provider output is a crash-recoverable proposal, never a direct write

**Status:** Accepted implementation decision 2026-07-16

Core strictly decodes and validates a reflection proposal. The RimWorld host writes a durable `PendingCommit` audit record before atomically writing the replacement identity snapshot, then records `Committed`. Startup recovery verifies whether an interrupted commit was absent, already applied, or conflicting.

**Consequences:** Malformed, foreign-evidence, stale, excessive, replayed, dead/archived, or conflicting proposals are quarantined and make no canonical mutation. A fluent answer has no special authority.

## D-035 — Runtime inference is explicit opt-in and offline-first

**Status:** Accepted implementation decision 2026-07-16

`DAGMAY_REFLECTION_MODE` selects `offline`, `fake`, or `google`; missing or unknown configuration remains offline. The Google API key is read from a Windows user environment variable and sent only in the authentication header.

**Consequences:** Event recording and deterministic memory remain functional without network access. Missing credentials do not consume retry or token budgets. The private reflection queue waits durably. The build includes a hidden-input PowerShell helper, and neither source nor Dagmay data stores contain the key. A Windows environment variable is not treated as an encrypted secret vault.

## D-036 — Persisted user configuration outranks Steam's inherited snapshot

**Status:** Accepted hotfix decision 2026-07-16

On Windows, Dagmay reads its persisted current-user environment values before falling back to the running process environment. The configuration helper updates both scopes for immediate consistency.

**Consequences:** Starting RimWorld normally through a Steam process that predates a setting change no longer forces Dagmay back to a stale `offline` mode. Process-only values remain a fallback for non-Windows/restricted hosts and tests. The API key stays outside source, saves, identity/experience/reflection records, and logs.

## D-037 — Provider fallback must preserve cost and causal provenance

**Status:** Accepted architecture decision 2026-07-16

Dagmay may later try multiple account-enabled models behind one provider key, but it will not copy an unbounded quota-exhaustion loop. Every concrete model attempt must be independently capability-checked, rate/token/retry-budgeted, and audited.

**Consequences:** Version 0.1E pins one explicit model, defaulting to the account-listed `gemini-3.1-flash-lite`. Automatic fallback remains deferred until the dispatcher can attribute every attempt and stop correctly on permanent failures. A model change never changes the individual's ID or lineage.

## D-038 — Enrollment pause preserves the individual

**Status:** Accepted implementation decision 2026-07-16

Pausing Dagmay processing for a colonist is a host-service state, not death, deletion, identity replacement, or lineage change. The existing individual, seed, memories, affect, lifecycle history, and archives remain intact.

**Consequences:** Version 0.1E stops ordinary observation and new reflection for a paused individual, removes only that individual's waiting reflection tasks, and prevents an already-dispatched response from committing after pause. Death archival still runs because lifecycle integrity outranks service pause. Resume requires the same living colonist mapping and continues the same ID and lineage.

## D-039 — Restricted Observer uses a separate, explicit disclosure gate

**Status:** Accepted implementation decision 2026-07-16

The first private inspection UI lives in RimWorld's out-of-world Mod Settings route, starts locked, and requires a two-step unlock with an explicit private-information confirmation. It reads immutable Core projections and never requests or exposes hidden model chain-of-thought.

**Consequences:** Version 0.1E may display grounded seed facts, affect, private memories, committed validated reflection summaries, provider usage, and storage health only after unlock. This is a research disclosure boundary, not authentication against a person with filesystem or mod access. The normal disclosure-filtered pawn mind tab remains separate and unimplemented.

## D-040 — Concrete transport attempts consume a hard session budget

**Status:** Accepted implementation decision 2026-07-16

Every provider transport begun after its durable pre-dispatch audit consumes the current RimWorld-process session budget, whether it succeeds, fails, or is quarantined. Offline queueing and local event recording consume no session attempt.

**Consequences:** Version 0.1E defaults to four attempts per session and provider dispatch paused. Reaching the cap leaves work durable without repeatedly rewriting its retry time. Restarting RimWorld starts a new session allowance; raising the configured limit can permit more work immediately. Usage metadata is deduplicated by request ID across audit stages, and hourly/day/circuit safeguards remain independently enforced.

## D-041 — Local development automation is evidence-producing and fail-closed

**Status:** Accepted implementation decision 2026-07-17

Dagmay's normal Windows development loop builds and tests before it may install. It retains human-readable and structured build evidence, validates a staged package, targets only the exact `RimWorld\Mods\Dagmay` directory, backs up the prior package, and attempts rollback after a real installation failure. Runtime saves, continuity sidecars, configuration, and credentials remain outside its installation authority.

**Consequences:** A coding agent can perform mechanical build, package, install, and diagnostic work without relying on copied chat output. The repository files—especially `AGENTS.md`, `README.md`, the roadmap, decision log, and verification record—remain authoritative over bootstrap prompts and chat memory. Version 0.1F changes development reproducibility only and cannot be promoted to Tested on Windows or Tested in RimWorld until those checks actually run on the owner's computer.


## D-042 — Ordinary Mind projection is a separate disclosure product

**Status:** Accepted implementation decision 2026-07-17

The ordinary Mind view is constructed as a separate projection containing only disclosure-allowed fields. It does not receive the complete Restricted Observer snapshot and then hide private fields visually.

**Consequences:** Ordinary UI cannot expose `IndividualId`, `LineageId`, raw affect dimensions, provider diagnostics, reflection payloads, private/relationship-sensitive/Observer-only memories, or hidden reasoning through a rendering mistake. Version 0.1G initially exposes coarse affect-derived language, grounded identity facts, and sufficiently accessible `Shareable` memories. Existing stored memories are not retroactively reclassified; new skill gains and resolved-health experiences are shareable, while critical needs and newly added health conditions remain private. A direct pawn tab is deferred until this disclosure boundary is owner-tested.


## D-043 — Routine experience and reflection work are not one-to-one

**Status:** Accepted implementation decision 2026-07-18

Canonical RimWorld events and encoded memories remain one-per-observed experience where the adapter records them, but provider reflection tasks may coalesce related routine events into bounded game-time windows.

**Consequences:** Dagmay preserves provenance while reducing redundant model calls. Physiology churn and skill growth can be interpreted as episodes instead of isolated callbacks. Unattempted legacy routine tasks may be compacted if every source event is retained. Tasks that have already reached the provider are never silently rewritten by backlog compaction.

## D-044 — Equal-priority reflection selection uses individual fairness as a tie-break

**Status:** Accepted implementation decision 2026-07-18

When eligible reflection tasks have equal effective priority, Dagmay prefers a different individual from the one most recently dispatched when possible.

**Consequences:** Quiet or less recently served individuals are less likely to be starved by repeated equal-priority work from one active pawn. Priority and aging still outrank fairness.

## D-045 — Autonomous verification uses deterministic integration scenarios before live gameplay

**Status:** Accepted implementation decision 2026-07-18

Dagmay's normal development loop includes a separate `Dagmay.IntegrationHarness` that composes real Core persistence, scheduling, validation, and deterministic provider implementations into repeatable end-to-end scenarios. The harness runs before the proprietary RimWorld adapter build and emits a machine-readable report.

**Consequences:** A repository-aware coding agent can autonomously catch many continuity, persistence, rollback, queue, provider-recovery, and relationship-provenance regressions without asking the owner to manually play RimWorld. Live RimWorld testing remains required for Verse/Harmony integration, save callbacks, mod interoperability, UI, and subjective coherence. Version 0.1 remains Observer-only; the project will not add pawn-control automation merely to make tests easier.

## D-046 — Social certification PASS is active-store and fail-closed

**Status:** Accepted release-blocking diagnostic correction 2026-07-18

A 0.1K social-path PASS must be tied to the StoreId loaded in the current RimWorld log, require an exact two-person stable counterpart link and matching owner-memory provenance, count reflection evidence only for the primary subject, require healthy identity/experience/reflection stores, and validate every gate rather than trusting a `Status=PASS` line alone.

**Consequences:** Certification format version 2 includes StoreId and storage health, writes through atomic replacement, and cannot reuse a newer unrelated save's historical PASS. This changes diagnostic qualification only. It adds no cognition, canonical mutation, disclosure, provider dispatch, or pawn-control behavior.