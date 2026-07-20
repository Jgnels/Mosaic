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
