# Decision Log

## D-001 — Identity continuity is model-independent
Status: Accepted

## D-002 — Core and adapters remain separate
Status: Accepted

`Dagmay.Core` is game/provider independent.

## D-003 — RimWorld 0.1 is Observer-first and non-agentic
Status: Accepted

No direct pawn-control loop belongs in 0.1.

## D-004 — Fact, perception, memory, and belief remain distinct
Status: Accepted

## D-005 — Model output is an untrusted proposal
Status: Accepted

Invalid output makes zero canonical changes.

## D-006 — Offline capture remains functional
Status: Accepted

## D-007 — Ordinary privacy and Observer Mode are separate
Status: Accepted

## D-008 — Initial self-knowledge is in-world only
Status: Accepted

Individuals are not initially told they are AI, models, game characters, or simulated beings.

## D-009 — Death archives; normal in-world revival continues lineage; copies fork
Status: Accepted

Forks create equal branches.

## D-010 — Google first, provider-neutral always
Status: Accepted

## D-011 — Seven-dimensional affect basis
Status: Accepted

Valence, arousal, threat/safety, agency/control, attachment/affiliation, certainty/confusion, social
standing.

## D-012 — Development subscription and runtime inference are separate
Status: Accepted

ChatGPT/Codex supports development. Dagmay runtime uses separately configured providers/local models.

## D-013 — Dagmay remains private personal-use research for now
Status: Accepted

## D-014 — Cultural context is an obligation
Status: Accepted

The name Dagmay is connected to the sacred handwoven abaca textile of the Mandaya people of Davao
Oriental.

Public release requires credible sourcing and informed cultural review.

## D-015 — SyntheticLab and RimWorld have separate jobs
Status: Accepted

SyntheticLab = mechanism discovery and causal inference.

RimWorld 0.1 = frozen mechanisms, integration, persistence, reliability.

## D-016 — External research code enters through experiments/adapters
Status: Accepted

## D-017 — Developmental learning belongs below the LLM
Status: Accepted research direction

## D-018 — Reflection is an intervention
Status: Accepted

## D-019 — Exact forks require equal moral/identity standing
Status: Accepted governance principle

## D-020 — Post-fork private life is private by default
Status: Accepted

## D-021 — RETRIEVAL_ATTENTION_ONLY intervention approved
Status: Accepted bounded human gate

May change retrieval/attention over already-existing memories/hypotheses.

May not fabricate memories, change memory content, drives, actions, identity, or directly rewrite
canonical SelfModel.

## D-022 — Bounded canonical SelfModel promotion approved only for exact-fork experiment
Status: Accepted bounded human gate

Not general deployment authority.

## D-023 — Two v23 branch-specific canonical revisions approved
Status: Accepted bounded human gate

Approved:
- reciprocal-contingent STRENGTHEN;
- one-way-assistance QUALIFY.

## D-024 — No further canonical revision without fresh human approval
Status: Accepted

## D-025 — v31 attribution-framing claim invalidated
Status: Accepted scientific correction

## D-026 — Provider-input leakage audit is the next scientific gate
Status: Accepted current stop line

## D-027 — Dedicated desktop becomes canonical lab host
Status: Accepted infrastructure direction

## D-028 — GitHub becomes canonical institutional memory
Status: Accepted

Chat is discussion.

Repository is durable project memory.

## D-029 — Unattended work is capability-separated
Status: Accepted

Offline compilation, testing, auditing, documentation, and bounded engineering may run unattended.

Persistent provider credentials do not constitute authorization for provider calls. Real-provider execution remains fail-closed until payload controls, enforceable call budgets, and a bounded human approval record pass offline validation.

## D-030 — Historical provider paths remain reproducible but cannot authorize new runs
Status: Accepted

The hardened provider boundary is opt-in so historical experiments remain reproducible and auditable. Every new real-provider protocol must use the hardened boundary; legacy mode is not an acceptable unattended execution path.

## D-031 — Full unbilled Gemini 3.1 Flash Lite quota approved with local enforcement
Status: Accepted

The project owner approved up to the displayed free-tier limits of 15 requests/minute, 250,000 tokens/minute, and 500 requests/day. Billing activation and paid fallback remain prohibited. Provider credentials and quota approval do not authorize canonical mutation or unpreregistered experiments.

## D-032 — The Boundary Decision and Mosaic rename
Status: Accepted; binding

The project neither pursues artificial consciousness nor optimizes deceptive pseudo-consciousness.
It pursues causally coherent, persistent, robust, believable, efficient, and enjoyable artificial
characters. Credible moral-status signals trigger pause and preservation rather than reward.

The active name is **Mosaic**. `Dagmay` remains in immutable artifacts, namespaces, paths, and
compatibility surfaces until a provenance-safe migration is separately tested.

All provider protocols are suspended until objectives, evaluators, mutation criteria, and
acceptance tests pass the persistent-character boundary audit.

## D-033 — Relationship dialogue uses typed selection and deterministic rendering
Status: Accepted; implemented in SyntheticLab reference layer

Retain provider reasoning only for bounded disposition/evidence selection. Factual relationship
clauses are produced from typed environment events, must carry deterministic-adapter provenance,
and are capped at two examples per valence. Regex scanning remains defense in depth, not the primary
grounding mechanism.

V2 showed citation-complete free prose can still invent traits. V3 showed typed selection preserves
relationship nuance without provider-authored factual claims. The event contract repairs V3's
grammar limitation while keeping facts under adapter control.

## D-034 — Relationship histories require identity-bound atomic snapshots
Status: Accepted; implemented in SyntheticLab reference layer

Persist the ordered typed evidence and correction history with individual and lineage IDs, a strict
schema version, and a canonical hash over the full payload. Restore must fail closed on identity,
integrity, schema, or retraction errors. Writes use flushed same-directory temporary files followed
by atomic replacement, and recovery must reproduce the same evidence selection and dialogue.

## D-035 — Goals are bounded environment candidates, never free model inventions
Status: Accepted; implemented in SyntheticLab reference layer

The environment supplies feasible goal candidates with observed evidence, bounded urgency, utility,
personality fit, risk, and optional target IDs. Mosaic selects or validates only those candidates.
Critical needs form a priority pool; personality cannot suppress emergencies. Generated language
renders the validated goal deterministically and cannot add motives or new objectives.

## D-036 — Goal selection and job execution are separate authority boundaries
Status: Accepted; implemented in SyntheticLab reference layer

A bounded goal decision never directly authorizes a game job. A single-use execution request must
pass a fresh world-revision, feasibility, target, evidence, and reachability check. Any mismatch or
failed outcome returns to replanning. The reference gate never performs the job itself.

## D-037 — Crash recovery cannot reopen consumed action authority
Status: Accepted; implemented in SyntheticLab reference layer

Persist every evaluated, accepted, and completed execution request in an identity-bound integrity-
checked ledger. Restore enforces `completed subset accepted subset evaluated`; all evaluated IDs
remain single-use across restart, and atomic writes preserve the last good authority state.

## D-038 — Durable character runtime state shares one atomic checkpoint generation
Status: Accepted; implemented in SyntheticLab reference layer

Relationship evidence, goal continuity, execution authority, identity/lineage, and world revision
must not be restored independently. Persist them beneath one versioned outer payload and hash, with
a monotonically checked generation. Restore fails closed on component identity mismatch, integrity
failure, or rollback. Component snapshots retain their own validation as defense in depth.

## D-039 — Recovered RimWorld source is a closure candidate, not an inferred RC
Status: Accepted

Import the newest recovered audit-work tree at `rimworld/0.1-closure-candidate/`. Preserve its
Dagmay 0.1K version identifier and later closure patches exactly; do not relabel it 0.1RC without a
separately versioned artifact or new verified release decision. Treat the old 0.1K manifest as stale
relative to the final scheduler safety patch.

## D-040 — Raw laptop recovery remains local and runtime data stays out of Git
Status: Accepted

Retain the complete rescue archives under ignored `local-recovery/` storage. Commit only selected
secret-safe source, hashes, contextual records, and verification artifacts. Never commit saves,
identity/reflection stores, experience journals, API credentials, installed assemblies, or raw
diagnostics containing personal runtime state.

## D-041 — Contradictory RimWorld persistence state fails closed
Status: Accepted; implemented offline in RimWorld 0.2 pre-alpha

Only a save manifest with no store ID and no checkpoint evidence may initialize a new Mosaic store.
Any malformed identifier, impossible mapping, or identity/reflection checkpoint mismatch enters the
appropriate read-only safety mode. Identity read-only mode blocks synchronization and enrollment;
it may not manufacture replacement individuals. Sidecar filenames derive only from validated store
GUIDs. Verified experience extensions still require explicit administrative adoption. Live
RimWorld behavior remains unconfirmed until Gate 3 owner testing.

## D-042 - Post-load reflection writes wait for a matching RimWorld save
Status: Accepted; implemented and live-verified in RimWorld 0.2 pre-alpha

Loading a save may prepare deterministic reflection-queue maintenance in memory, but it may not
advance the external reflection generation before RimWorld records the same checkpoint. Reflection
sidecar persistence, provider dispatch, and pending-commit recovery wait for the first RimWorld save
callback after load. A successful matching save releases the gate. This bounded delay is preferred
to poisoning an unchanged save with uncheckpointed forward reflection state.

## D-043 - Unchanged RimWorld saves do not rewrite identity checkpoints
Status: Accepted; implemented and live-verified in RimWorld 0.2 pre-alpha

The identity archive generation represents a durable canonical identity change, not the number of
times RimWorld's save callback ran. During a save, Mosaic writes the identity archive only when
colonist synchronization reports a real identity change. Rename, lifecycle, enrollment, and
validated mutation paths retain their immediate persistence behavior. This keeps successive
unchanged Save As copies compatible with one external identity checkpoint while preserving exact
generation mismatch rejection for genuinely divergent state.

## D-044 - Controlled Gate 3 pass closes the frozen-0.1 reliability campaign
Status: Accepted

The two-block live Gate 3 soak at commit 457f1815d625f7a121f444d972d4880e70c805cb is accepted as the controlled closure
evidence for the frozen 0.1 reliability scope. The run used Core + Mosaic only in forced-offline
mode, preserved continuity across a full process restart, maintained healthy identity, experience,
and reflection storage in all six samples, produced complete checkpoint agreement after both
blocks, and invoked no provider or local model.

The shared-read Player.log harness correction is accepted as evidence-tool maintenance because it
changed only log hash/copy access and did not change Mosaic source, package, saves, sidecars, or
runtime behavior.

The deterministic packaging commit 21d6bf28138513d06e950cd9604592b2410ff7ec is the final distributable baseline. It removes
machine-specific build-path disclosure, normalizes package ordering and metadata, enforces a strict
package allowlist, and passes adversarial firewall fixtures and two-checkout reproducibility. Its
certified package SHA-256 is 5a79db4f3fc2b3306f8ff2fc66f8e4653de1b9b1f68436b9fe5882c4d6ae4ad7.

Full-stack, DLC-group, and RimTalk compatibility are post-0.1 integration work. Closing this
campaign does not rename the recovered 0.1K source or the 0.2-prealpha package as an unlocated
historical 0.1RC artifact.

## D-045 - Durable dialogue admission is checkpoint-aligned
Status: Accepted; offline policy and limiting-result tests implemented

An actually displayed, validated utterance may prepare a factual admission,
but it may not immediately dual-write the event ledger and durable experience
journal. Those surfaces expose no shared transaction, rollback, or recoverable
outbox. Executable tests reproduce both one-sided failure orders and show that
restart replay can duplicate the same factual event in the journal even while
the event ledger rejects the duplicate EventId.

A future coordinator must wait for an exact matching save checkpoint, writable
healthy storage, and an idempotent atomic/outbox design. Provisional
in-session appraisal may be designed separately for immediate believability,
but lasting relationship, mood, memory, belief, goal, or identity changes
remain validated checkpoint-aligned mutations. No live coordinator or
automatic dialogue-to-gameplay effect is approved by this decision.

## D-046 - Dialogue admission uses a recoverable factual outbox
Status: Accepted; implemented and verified offline

Before either non-transactional destination changes, Mosaic persists an
integrity-checked outbox entry bound to the exact store, stable save lineage,
world, and checkpoint generation. Recovery inspects the event ledger and
experience journal, materializes only a missing side, rereads both, and retains
a completed tombstone through a later successful checkpoint.

This is an idempotent recovery protocol, not a claim of filesystem-level
atomicity. It admits factual dialogue evidence only and has no authority to
change memory, relationships, mood, beliefs, goals, identity, pawn behavior,
or gameplay.

## D-047 - Dialogue presentation is speech-bubble-first and receipt-gated
Status: Accepted; implemented and verified offline

Mosaic presents validated dialogue through a bounded, main-thread RimWorld
speech bubble anchored to the stable speaker `IndividualId` binding. The
RimWorld play log is a narrow mirror and fallback. If the bubble succeeds, the
receipt identifies Bubble even when mirroring also succeeds. If only the play
log succeeds, the receipt identifies PlayLog. If neither succeeds, Mosaic has
no factual display receipt and may not admit the utterance.

Presentation remains observer-only and non-authoritative. It cannot call a
provider, persist cognition, mutate canonical character state, control a pawn,
or execute gameplay. Offline compilation and tests do not establish live
visual layout or play-log compatibility.

## D-048 - The first RimWorld dialogue path is fake-only and save-checkpoint admitted
Status: Accepted; implemented and verified offline

The first adapter path observes only material opinion or direct-relationship
changes already captured by Mosaic. It resolves stable identity on RimWorld's
main thread, releases all live game references, and then uses the deterministic
fake provider, strict codec, validator, and bounded speech-bubble presenter.
There is no real-provider selection seam in this path.

Actual presentation queues a factual-only pending outbox entry. The event
ledger and experience journal materialize and verify only inside RimWorld's
save callback, after which the journal head and dedicated dialogue checkpoint
are serialized. Generation, validation, or display alone never constitutes
admission. No subjective cognition or gameplay authority is added.
