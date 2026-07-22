# 03 — Roadmap

**Status:** Draft 0.1  
**Current gate:** Version 0.1M failure isolation is Implemented, Compiled, and Tested in isolation. The 0.1K social-path and 0.1L persistence gates are Tested in RimWorld. The next release-closure stage is 0.1N soak evidence, followed by the 0.1 RC acceptance audit.

## Release progression

### Version 0.1 — Observer

Observe colonists, establish persistent identity, record meaningful experience, create memories and reflections, and expose ordinary and diagnostic views. No pawn behavior control.

### Version 0.2 — Advisor

Form goals and propose actions in a constrained vocabulary. Proposals are visible for evaluation but RimWorld remains authoritative and Dagmay cannot execute them.

### Version 0.3 — Limited Agency

Allow selected, reversible influence over approved priorities and choices. Permissions, cooldowns, resource limits, and RimWorld fallback are mandatory.

### Version 0.4 — Autonomous Colonist

Allow goal initiation through a reviewed action vocabulary while RimWorld executes low-level jobs, pathing, reservations, and safety behavior.

### Version 1.0 — Persistent Society

Support reciprocal relationships, group memory, developing culture, collaboration, conflict, institutions, long-term plans, and portable identity under explicit continuity rules.

Version numbers describe capability gates, not calendar commitments.

## Version 0.1 implementation slices

### 0.1A — Compiling spine

- [x] **Implemented:** solution and four project files.
- [x] **Implemented:** core identifiers, clock, result type, schema versions, affect, weighted continuity, lifecycle, event/perception/memory contracts, in-memory stores, validation, and bounded scheduling foundations.
- [x] **Implemented:** deterministic fake provider with normalized success and failure fixtures.
- [x] **Implemented:** passive RimWorld load-health bootstrap source with no Harmony patches or behavior-control path.
- [x] **Implemented:** executable contract-test source, static safety verifier, and automated build/package commands.
- [x] **Compiled through project build:** Core and providers restored/built as `netstandard2.0`; tests restored/built as `net8.0`. A separate direct C# 4.11 compile with warnings as errors also passed.
- [x] **Tested in isolation:** all 16 contract tests passed.
- [x] **Compiled for RimWorld:** built by the owner against the local RimWorld 1.6 reference assemblies.
- [x] **Tested in RimWorld:** passive bootstrap loaded in the owner's test colony alongside approximately 50 other mods without observed issues.

### 0.1B — Identity and persistence

- [x] **Implemented:** seed colonist identities from immutable RimWorld snapshots.
- [x] **Implemented:** assign stable Dagmay IDs independent of pawn name and provider.
- [x] **Implemented and tested in isolation:** versioned snapshots, atomic writes, checksums, strict decoding, and verified-backup recovery.
- [x] **Completed in 0.1C:** make the initial canonical experience ledger durable once meaningful RimWorld events are observed.
- [x] **Implemented:** save-linked identity mapping, rename continuity, death archival, and copyable identity archives.
- [x] **Compiled for RimWorld:** 0.1B built cleanly against the owner's installed RimWorld 1.6 assemblies.
- [x] **Tested in RimWorld:** identity enrollment and save/load continuity passed with the owner's full mod list.

### 0.1C — Bounded perception and memory

- [x] **Implemented:** poll a narrow initial event set: critical Food/Rest/Mood, health-condition set changes, skill-level gains, and death facts.
- [x] **Implemented:** create experienced first-person perceptions without map omniscience.
- [x] **Implemented and tested in isolation:** deterministic importance/appraisal and bounded affect updates.
- [x] **Implemented and tested in isolation:** keep environment facts, perceived details, and private memories as separate provenance-linked records.
- [x] **Implemented and tested in isolation:** recent/significant retrieval foundations and a hash-chained durable journal.
- [x] **Compiled for RimWorld:** 0.1C built with zero warnings and zero errors against the owner's installed RimWorld 1.6 assemblies.
- [x] **Tested in RimWorld:** upgrade/storage health and natural bounded-event capture passed on RimWorld 1.6.4871 with the owner's full mod list.
- [x] **Tested in RimWorld:** fresh-process experience-journal save/reload and continued nonduplicate recording passed with the same three individual and lineage IDs.
- [ ] **Deferred within 0.1:** social/witnessed events, richer salience, consolidation, forgetting, and UI.

### 0.1D — Reflection and providers

- [x] **Implemented:** durable, bounded reflection queue with coalescing, priority aging, persistent retry state, and one real-world-minute background scheduling opportunities.
- [x] **Implemented:** explicit `offline`, deterministic `fake`, and opt-in `google` runtime modes; offline is the default and event recording does not depend on a provider.
- [x] **Implemented:** Google AI Studio `generateContent` REST transport using an environment-provided key header, request deadlines, bounded/cancelable response reads, normalized errors, token metadata, and `Retry-After`.
- [x] **Implemented:** rolling 12-request/hour and estimated 40,000-token/day defaults, single concurrency, three-attempt retry ceiling, exponential backoff, and a transient-failure circuit breaker.
- [x] **Implemented and tested in isolation:** strict local JSON decoding, exact identifiers, evidence allowlisting, stale-state/lifecycle checks, per-affect-dimension continuity bounds, and zero mutation for invalid output.
- [x] **Implemented and tested in isolation:** checksummed reflection sidecar, atomic primary/backup replacement, pre-dispatch audit checkpoint, pending-commit recovery, and identity-before-completion commit ordering.
- [x] **Implemented:** private diagnostic records with request context, provider/model/request provenance, structured proposals, concise decision summaries, confidence, evidence, status, and usage—never hidden chain-of-thought or API keys.
- [x] **Compiled in available environments:** Core/providers on `.NET Standard 2.0`, tests on `.NET 8`, and adapter on `.NET Framework 4.7.2` against a local API stub, all with zero warnings.
- [x] **Tested in isolation:** 33 contracts pass, including simulated Google success/rate-limit transport and the 0.1D.1 stale-Steam-environment regression without a live network call.
- [x] **Compiled for RimWorld:** 0.1D and the 0.1D.1 hotfix built with zero warnings/errors against the owner's installed RimWorld 1.6.4871 assemblies.
- [x] **Tested in RimWorld:** offline recording, fake-provider commits, and fresh-process reload passed with the owner's full mod list; the queue returned to zero and all stores stayed healthy.
- [x] **Tested in RimWorld, safe-failure path:** two Google 2.5 Flash-Lite calls returned endpoint `404`; both were audited and rejected without identity mutation or storage damage.
- [x] **Tested in RimWorld, live-success path:** a normal Steam launch selected `google` and `gemini-3.1-flash-lite`; five attempts produced five locally validated commits with no retry/failure and no reported pause or stutter. Offline reload preserved exact continuity, queue zero, audit 36, and healthy stores without duplicate dispatch.
- [ ] **Deferred:** controlled multi-model fallback with per-attempt budgets/audit, local-model provider, provider-switch regression fixtures, unload cancellation, and broader provider hardening.

### 0.1E — Interfaces and hardening

- [x] **Implemented:** a clearly out-of-world Mod Settings surface for provider state, storage health, queue/audit health, per-session/hour/day usage, and colonist enrollment.
- [x] **Implemented:** persisted provider pause plus adjustable per-session request, rolling-hour request, UTC-day estimated-token, and background-heartbeat limits. New 0.1E settings start with dispatch paused.
- [x] **Implemented:** explicit enroll, pause, and resume controls. Pause preserves the same individual and lineage, prevents ordinary observation/reflection, removes only that individual's waiting tasks, and quarantines an already in-flight result before mutation.
- [x] **Implemented:** separately locked Restricted Observer inspection for grounded seed facts, affect, private recent/significant memories, and successfully committed validated reflection summaries. It does not request or show hidden chain-of-thought.
- [x] **Implemented:** immutable Core Observer projection contracts so the diagnostic interface does not expose live Verse objects or make the persistent individual RimWorld-dependent.
- [x] **Compiled in available environments:** Core/providers on `.NET Standard 2.0`, tests on `.NET 8`, and adapter on `.NET Framework 4.7.2` against a strict local API stub, all with zero warnings.
- [x] **Tested in isolation:** 36 contracts pass, including targeted paused-individual queue cleanup, session-limit behavior, audit-stage token deduplication, and memory-provenance projection.
- [x] **Compiled and tested in RimWorld:** on RimWorld 1.6.4871 with the owner's full mod list, settings rendered/scrolled normally, the two-step disclosure lock worked, Michael preserved exact continuity through pause/save/reload/resume, fake transport stopped at `2/2`, final offline reload kept all stores healthy, and no Dagmay pause, stutter, red error, or unexpected behavior was reported.
- [ ] Add the ordinary disclosure-filtered pawn mind tab and a normal colony overview distinct from Restricted Observer Mode.
- [ ] Add settings confirmation language for remote-provider disclosure, compatibility diagnostics, and portable export tooling.
- [ ] Run corruption, model-switch, 40+ colonist, save/load, and RimTalk coexistence tests.
- [ ] Complete the manual release checklist.

### 0.1F — Development automation and reproducibility

- [x] **Implemented:** automatic timestamped and latest build transcripts plus a machine-readable build-result record.
- [x] **Implemented:** automatic RimWorld package creation, SHA-256 evidence, and clean source packaging without compiled binaries, runtime data, diagnostics, secrets, or Git history.
- [x] **Implemented:** guarded installation to exactly `RimWorld\Mods\Dagmay`, with staging validation, prior-package backup, atomic directory replacement where Windows permits it, and rollback on a real installation failure.
- [x] **Implemented:** current and previous `Player.log` collection with Dagmay extraction, noteworthy context, and redaction of Google-style keys, the user-profile prefix, and computer name.
- [x] **Implemented:** `START_HERE.md`, expanded repository guidance, and a project-scoped `dagmay_implementer` Codex agent so a human or coding agent can reconstruct the workflow without chat history.
- [ ] **Windows verification pending:** run the PowerShell development loop, confirm all evidence artifacts, exercise the install guards, and prove diagnostic redaction using synthetic data.
- [ ] **RimWorld verification pending:** load 0.1F offline on RimWorld 1.6.4871, confirm the three established identity/lineage pairs and healthy stores, and confirm no Dagmay pause, stutter, red error, or behavior change.
- [x] **Scope unchanged:** 0.1F adds no new cognitive state, provider behavior, event type, UI disclosure, RimTalk dependency, or pawn-control path.


### 0.1G — Ordinary Mind view and disclosure filtering

- [x] **Implemented:** separate ordinary projection contract containing no individual/lineage IDs, provider diagnostics, raw affect dimensions, private memories, or reflection payloads.
- [x] **Implemented:** conservative disclosure policy; only `Shareable` memories with sufficient accessibility may appear in ordinary view.
- [x] **Implemented:** newly encoded skill gains and resolved health conditions are initially shareable; critical needs and newly added health conditions remain private.
- [x] **Implemented:** Mod Settings enrollment list exposes a **Mind** button without requiring Restricted Observer unlock.
- [x] **Implemented:** ordinary view shows coarse current-state language, grounded identity facts, and shareable memories only.
- [x] **Statically verified:** 65 C# files and repository safety rules pass in the remote workspace.
- [ ] **Compiled/Tested in isolation:** run the local Windows development loop and confirm the new disclosure contract test passes.
- [ ] **Tested in RimWorld:** confirm Mind view renders with Restricted Observer locked, private data remains absent, shareable/private memory separation survives save/reload, and continuity remains unchanged.
- [x] **Scope unchanged:** no action executor, job control, work-priority mutation, pathing, combat control, or other pawn-control path.

### 0.1H–0.1J.3 — Consolidation, salience, social foundations, and checkpoint recovery

- [x] **Implemented:** bounded experience consolidation and reflection admission retain canonical event/memory provenance while reducing routine provider work.
- [x] **Implemented:** grounded enrolled-counterpart opinion/direct-relationship changes create asymmetric relationship-sensitive memories without motive inference or pawn control.
- [x] **Implemented:** verified forward-only experience-journal mismatch is exposed read-only and requires explicit Restricted Observer adoption.
- [x] **Tested in RimWorld:** the owner-established 0.1J.3 baseline recovered a specific verified experience checkpoint mismatch while preserving established individual/lineage IDs and healthy stores.
- [x] **Scope frozen:** these mechanisms define the RimWorld 0.1 cognitive boundary for release closure.

### 0.1K — Social Path Certification

- [x] **Implemented:** diagnostic-only certification for event-to-memory, exact stable counterpart, relationship-sensitive privacy, counterpart provenance, reflection evidence, post-load observation, and storage health.
- [x] **Implemented:** certification v2 is tied to the active StoreId; evidence tools refuse unrelated historical PASS sidecars.
- [x] **Compiled:** Core, Providers, Tests, IntegrationHarness, and RimWorld adapter built on Windows against local RimWorld 1.6.4871 assemblies with zero warnings/errors.
- [x] **Tested in isolation:** 41 contracts and six integration scenarios pass, including false-PASS regressions.
- [x] **Tested in RimWorld:** certification v2 returned `Status=PASS` and `Gate.Overall=True` for the active StoreId after real social events, save, and reload.

### 0.1L — Persistence Torture

- [x] **Implemented:** six-cycle deterministic persistence-torture harness with provider outage, new offline experiences, non-empty save-time queue, one-record-ahead journal recovery, and uniqueness/no-loss assertions.
- [x] **Tested in isolation:** 238 assertions pass and `artifacts\Dagmay-persistence-torture-latest.json` is produced by one PowerShell command.
- [x] **Tested in RimWorld:** eight post-load snapshots across two RimWorld processes preserved one StoreId, four identity/lineage mappings, monotonic event/memory state, a durable paused-provider queue, and healthy stores without Dagmay failures or recovery.
### 0.1M — Failure Isolation

- [x] **Implemented:** deterministic unavailable, invalid-response, timeout, rate-limit, provider-error, cancellation, malformed-output, interrupted-commit, mismatch, and pending-shutdown fixtures use real Core/Providers contracts without exposing live fault injection.
- [x] **Compiled:** the complete Windows development loop builds the six-scenario harness and RimWorld adapter against local 1.6.4871 assemblies with zero warnings/errors.
- [x] **Tested in isolation:** `FailureIsolation` passes 103 assertions; every failure preserves canonical bytes/state, retryable work remains durable, invalid work is quarantined, and only one fully revalidated absent commit may mutate state.
- [x] **Live evidence reused:** 0.1L proves playable normal shutdown with pending work and no provider dispatch; the owner-established 0.1J.3 run proves explicit checkpoint mismatch recovery. Destructive fault injection remains isolated from the owner's real stores.
## Version 0.1 acceptance criteria

Version 0.1 is accepted only when every release-blocking item below has evidence. An item may be marked not applicable only through a recorded decision.

### A. Build, installation, and scope

- [ ] A clean checkout builds with one documented command using pinned dependencies.
- [ ] Automated tests run with one documented command and report failures normally.
- [ ] The packaged mod installs into the documented Windows Steam mod location and the supported current RimWorld 1.6 patch loads it with all DLC, including Odyssey, without red startup errors attributable to Dagmay. Each release record names the exact tested patch.
- [ ] A dedicated test colony can be created, saved, closed, reopened, and continued.
- [ ] Project status clearly records **Compiled**, **Tested in isolation**, and **Tested in RimWorld** separately.
- [ ] No source file, package, log bundle, save export, or model request contains an API key.

### B. Observer-only boundary

- [ ] Dagmay does not start or end jobs; alter work priorities, schedules, drafted state, areas, outfits, policies, ideology, relationships, needs, mood, health, inventory, combat, pathing, or player commands; or write any equivalent pawn-control state.
- [ ] The RimWorld adapter has no implemented action executor in Version 0.1.
- [ ] Disabling Dagmay leaves ordinary RimWorld pawn behavior unchanged apart from removal of Dagmay UI and observation overhead.
- [ ] A documented patch audit lists every Harmony patch and proves its Version 0.1 purpose is observation, lifecycle, persistence, or UI only.

### C. Identity creation and continuity

- [ ] Each eligible colonist receives one stable `IndividualId` that does not depend on name, provider, model, prompt, or transient in-memory object identity.
- [ ] The source seed records available backstories, traits, skills, passions, ideology, genes, relationships, age, health, and circumstances with source metadata.
- [ ] Missing DLC or pawn data remains explicitly absent rather than fabricated.
- [ ] Renaming a pawn, saving/loading, temporarily disabling network access, and switching between the fake and Google providers do not create a new individual.
- [ ] Every material state change is attributable to events, deterministic rules, accepted model proposals, schema migration, or an explicit repair record.
- [ ] At most one active lineage is permitted for a given identity in the normal store.

### D. Minimum event and perspective set

- [ ] The adapter reliably handles identity discovery, colony joining/leaving, social interaction, relationship change, material injury or damage, downing, tending or rescue, death, witnessed death, extreme need-state threshold crossings, skill-level change, and material ideology change where the game exposes them.
- [ ] Each normalized event has an event ID, schema version, source, subjects, occurred-at game time, observed-at time, factual payload, adapter version, and deduplication key.
- [ ] Repeated callbacks do not create duplicate canonical events.
- [ ] An individual receives only a perspective it could plausibly perceive; death elsewhere on the map does not become its memory without observation or communication.
- [ ] Occurrence-time context is retained even if subjective interpretation happens later.
- [ ] Unsupported events degrade to diagnostics rather than guessed semantics.

### E. Memory and internal state

- [ ] Factual event records and subjective memories are separate objects linked by identifiers.
- [ ] A subjective memory supports importance, emotional weight, confidence, source, involved people, occurrence time, encoding time, privacy/disclosure state, and current accessibility.
- [ ] Beliefs cite supporting and contradicting evidence and may disagree with the factual ledger.
- [ ] Recent, significant long-term, and core autobiographical memory tiers exist with explicit promotion rules.
- [ ] Semantic retrieval is available through a replaceable index; loss of the index does not destroy canonical memory and it can be rebuilt.
- [ ] Consolidation and reinterpretation append mutation records and preserve original provenance.
- [ ] Forgetting reduces cognitive accessibility without pretending the audit record never existed.
- [ ] Affect changes at least attention/salience, memory importance, and interpretation context; it is not display-only.

### F. Reflection, provider behavior, and offline operation

- [ ] Event recording, deterministic updates, save/load, and UI health status work with no network and no configured provider.
- [ ] Reflection tasks persist across game shutdown and retain event-time context.
- [ ] A scheduler heartbeat occurs approximately once per real-world minute while playing, but model calls are selected by salience, fairness, and configured budgets rather than guaranteed per pawn.
- [ ] Concurrency, request rate, token use, queue size, retry count, and optional spending have configurable bounds.
- [ ] Timeouts, cancellation, unload, rate limits, provider errors, and malformed output cannot block the game thread or corrupt canonical state.
- [ ] The fake provider can deterministically produce success, timeout, invalid schema, stale-state, and provider-error cases.
- [ ] The Google provider implements the same core contract and can be replaced without storage migration.
- [ ] Provider name, model name, prompt version, and request ID are diagnostic provenance, not identity fields.

### G. Validation and state integrity

- [ ] Every model-generated structured response is validated against a versioned schema and allowlisted operation set.
- [ ] References, ranges, text sizes, base-state version, lifecycle constraints, and replay identifiers are checked before commit.
- [ ] A response commits all accepted operations atomically or commits none.
- [ ] Invalid responses are quarantined with a concise failure reason and make zero canonical mutations.
- [ ] Tests cover prompt injection text inside game-provided names, backstories, and event descriptions.
- [ ] No hidden chain-of-thought is requested, stored, or displayed; decision records contain concise summaries, evidence references, causal factors, and confidence.

### H. Persistence, recovery, death, and export

- [ ] Stores and exports declare schema versions and support forward migration from every released Version 0.1 schema.
- [ ] Writes are atomic and retain at least one known-good prior snapshot.
- [ ] Checksums and ledger positions detect partial, stale, or mismatched data.
- [ ] A corrupted newest snapshot recovers from the last valid state when possible and shows a visible warning; it never silently invents missing history.
- [ ] RimWorld save and external identity-store mismatches open an explicit recovery choice with diagnostics.
- [ ] Death immediately changes lifecycle to dead/archived, cancels ordinary future reflection, preserves pending terminal processing where safe, and retains the complete exportable life record.
- [ ] Export includes lineage, intrinsic state, environment-namespaced context, provenance, schema versions, lifecycle, and an integrity manifest, but no secrets.
- [ ] Importing an export does not activate a second copy automatically.

### I. Interfaces and Observer Mode

- [ ] The pawn mind tab shows an ordinary, disclosure-filtered projection and never exposes all private beliefs or diagnostics.
- [ ] The colony overview shows identity presence, lifecycle, queue/provider health, and actionable errors without revealing every private thought.
- [ ] Observer Mode is off by default, requires an explicit settings enable plus confirmation, and displays a persistent visual indicator while open.
- [ ] Observer Mode can inspect memories and sources, affect, beliefs and confidence, goals/conflicts, state-statement conflicts, concise decision rationale, model exchanges, system health, and memory mutations.
- [ ] Ordinary interaction cannot retrieve Observer-only fields through prompt wording.
- [ ] Logs and exports clearly label observed fact, system inference, individual belief, and model proposal.
- [ ] Basic in-character player conversation is a stretch goal, not a release blocker for Observer; compatibility with RimTalk must be tested and documented.

### J. Performance and graceful degradation

- [ ] No network request, model wait, bulk serialization, or disk flush occurs synchronously inside a RimWorld observation callback.
- [ ] Main-thread callbacks perform only bounded snapshot and enqueue work and emit timing diagnostics when a configured threshold is exceeded.
- [ ] An isolated stress test with at least 50 individuals, event bursts, provider outages, and slow responses demonstrates bounded concurrency and queue behavior.
- [ ] In a 40+ colonist manual test, critical events are retained, low-priority tasks coalesce or defer, and gameplay does not freeze because of Dagmay work.
- [ ] Queue saturation, storage failure, and provider unavailability are visible; degradation never silently drops critical lifecycle events.

### K. Documentation and reproducibility

- [ ] Architecture, schemas, prompts, patch inventory, configuration, installation, known limitations, and recovery steps match the released code.
- [ ] Every accepted design change appears in the decision log.
- [ ] Manual RimWorld test evidence records game version, DLC, mod list/order, Dagmay commit, provider/model, configuration, scenario, outcome, and logs.
- [ ] A new contributor can follow the instructions without access to prior ChatGPT conversations.
- [ ] Public text includes the project's cultural context and avoids consciousness claims.

## Gates for later agency

Version 0.2 work may begin after 0.1 acceptance, but no execution permission exists until a separate action-safety architecture is approved. Version 0.3 requires an allowlisted action vocabulary, simulation/dry-run support, permissions, preconditions, postcondition checks, cooldowns, rollback where possible, and safe RimWorld fallback.
