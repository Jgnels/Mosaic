# 09 — Open Questions

**Status:** Draft 0.1

Questions are divided into project-owner decisions and technical investigations. Engineering should proceed around unanswered creative choices where possible, but must not silently decide them when they materially shape the individual.

## Project-owner decisions

### OQ-001 — What must remain recognizable?

Which characteristics form the initial continuity anchor: RimWorld traits and backstory, early autobiographical commitments, relationship patterns, voice, values, or some weighted combination? The architecture can preserve provenance, but the owner must define what “still this individual” should mean in evaluation.

**Recommended starting position:** backstory and traits strongly shape the initial prior, while accumulated autobiographical commitments and relationships increasingly outweigh the seed over time.

**Resolved 2026-07-16:** use a weighted combination. The initial implementation uses grounded seed 30%, autobiographical history 20%, relationships 20%, values/commitments 20%, and expression/habits 10% as an evaluation profile, not a literal soul score. Weights may mature with evidence through a future explicit policy.

### OQ-002 — Which affect dimensions begin Version 0.1?

The memory draft proposes valence, arousal, threat/safety, agency/control, attachment/affiliation, certainty/confusion, and social standing. Is this emotionally legible and philosophically aligned, or should the initial basis be smaller/different?

**Resolved 2026-07-16:** accept all seven proposed dimensions for Version 0.1.

### OQ-003 — What reflection budget is acceptable?

Should the one-minute heartbeat optimize for low monetary cost, equal individual attention, fastest response to salient events, or maximum subjective richness? A default monthly spending ceiling and acceptable quiet-colonist reflection interval are owner choices.

**Partially resolved 2026-07-16:** enroll selected colonists rather than requiring the whole colony. Begin with four or fewer, one concurrent request, a one-minute scheduling heartbeat, and conservative ceilings of 4 attempts/session, 12 attempts/hour, and 40,000 estimated tokens/day. Version 0.1E exposes these limits and provider pause in persisted Mod Settings. The defaults still need measured multi-session use against the owner's actual quota; shared-event batching and deterministic processing should reduce per-person cost.

### OQ-004 — Who is the player in-world?

Natural interaction requires a frame. Is the player an unseen external questioner, a colony communications system, a named in-world person, or absent from ordinary dialogue? This choice changes trust, privacy, self-knowledge, and memory formation.

**Resolved 2026-07-16:** follow the host world's normal player relationship. In ordinary RimWorld, the player remains the normal external operator and is not silently inserted as a character. If a perspective/possession mod makes the player an embodied pawn, interactions are attributed to that pawn. Minecraft and The Sims use the player's embodied character.

### OQ-005 — How should RimWorld revival affect continuity?

Recommended: ordinary in-world resurrection continues the same lineage, records death and the gap, and requires a post-revival integration state. Restoration of an old data backup is different and cannot silently erase remembered time. Owner approval is required.

**Resolved 2026-07-16:** accepted as recommended.

### OQ-006 — How literary should internal writing be?

Should subjective memories be terse first-person diary entries, restrained structured summaries with occasional voice, or richer prose? The recommendation is concise diary language plus structured facts, because ornate prose increases cost and can counterfeit depth.

**Resolved 2026-07-16:** concise first-person diary language plus structured facts.

### OQ-007 — What open-source license should govern Dagmay?

Recommendation: Apache-2.0 for permissive reuse with an explicit patent grant. MIT is simpler; GPL-3.0 requires derivatives to remain open under its terms. This needs owner selection before public release.

**Deferred 2026-07-16:** Dagmay is currently private personal-use research, so no public license is required yet. Reopen before sharing source publicly.

### OQ-008 — What cultural-review commitment should precede public release?

The project should decide who can provide informed review, what sources are required, whether sacred visual motifs are categorically avoided, and whether renaming remains an explicit option if concerns arise.

**Private-use context recorded 2026-07-16:** the owner's wife was born in the Philippines; her father was raised in Mati, Davao Oriental, and the family identifies with the Mandaya people. Her approval informs the family's personal use. It is not treated as authorization on behalf of the wider community; broader sourcing and review remain required if Dagmay is published.

## Technical investigations

### TQ-001 — RimWorld runtime and packaging

Verify the correct C# target framework, reference-assembly strategy, Steam paths, mod metadata, and packaging requirements for supported RimWorld 1.6 patches without redistributing proprietary assemblies.

**Partial result 2026-07-16:** the `net472` adapter compiled and loaded against RimWorld 1.6.4871. Patch-to-patch compatibility and a reproducible clean-machine toolchain remain open release concerns.

### TQ-002 — Stable observation points

Prototype and document the narrowest Harmony or public hooks for the minimum event catalog. Measure duplicate callbacks, ordering, and compatibility conflicts.

### TQ-003 — RimTalk integration

Determine current integration surfaces, version compatibility, load order, licenses, context ownership, and how to prevent duplicate dialogue/model calls.

**Partial result 2026-07-16:** RimTalk v1.0.14 has a source-verified public prompt/context add-on API and is the preferred optional dialogue bridge. Dagmay will retain canonical identity and memory, expose only a disclosure-filtered context projection, and designate one model-call owner per utterance. A stable completed-utterance callback, exact hearing capture, load-order behavior, and removal-after-save behavior remain open. RiMind exposes useful architectural names in compiled metadata but has no supplied source or valid license statement, so it is a design reference rather than an integration dependency.

### TQ-004 — Persistence engine

Compare versioned JSON plus an append log against SQLite for atomicity, inspection, migrations, query needs, recovery, and mod packaging. Keep the core behind a storage interface during the experiment.

### TQ-005 — Semantic retrieval

Select a provider-neutral embedding contract and initial implementation. Test index rebuild, provider changes, offline behavior, and whether structured/lexical retrieval is sufficient for early v0.1.

### TQ-006 — Google AI Studio implementation

**Partial result 2026-07-16:** Version 0.1D implements the official stateless `generateContent` REST path, `x-goog-api-key`, JSON structured output, request deadlines, cancelable bounded response reads, usage metadata, and normalized HTTP/rate-limit failures. Two calls to the prior `gemini-2.5-flash-lite` endpoint returned `404` and failed safely. Version 0.1D.1 then selected the account-listed `gemini-3.1-flash-lite`; five live calls passed strict validation and committed with no reported stutter, followed by a clean offline reload. Version 0.1E adds a default provider pause, visible usage, and a session cap. Current quota/terms, safety refusals, long-run schema reliability, capability-aware model fallback, and embeddings remain to be tested.

### TQ-007 — Two-store checkpoint protocol

**Partial result 2026-07-16:** identity and experience stores passed real save/reload. The 0.1D reflection sidecar uses checksums, atomic replacement, verified backup, pre-dispatch audit, and a pending-commit → identity snapshot → committed ordering with startup reconciliation. Forced interruption tests at every boundary and a beginner-safe recovery screen remain open.

### TQ-008 — Perspective fidelity

Identify which awareness data RimWorld actually exposes reliably. Define conservative fallbacks where line-of-sight, hearing, or communication cannot be established.

### TQ-009 — Performance baselines

Measure event volume and callback cost on the reference test PC, then set release thresholds for main-thread time, queue latency, memory, and storage growth.

**Partial result 2026-07-16:** the owner reported no pause or stutter during five asynchronous 0.1D.1 live commits with the full mod list. Version 0.1E caches its immutable Observer snapshot for one second while the settings page is open, but its UI and 40+ colonist behavior still require direct measurement.

### TQ-010 — Diagnostic retention

Choose default retention and redaction for raw provider exchanges, event audit data, embeddings, and exported support bundles.

## Resolution format

When resolved, move the outcome into `10_Decision_Log.md` with date, status, rationale, consequences, and superseded decisions. Do not simply delete the question.
