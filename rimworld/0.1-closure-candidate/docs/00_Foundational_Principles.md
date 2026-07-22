# 00 — Foundational Principles

**Status:** Draft 0.1  
**Scope:** Normative rules for all Dagmay components and releases

## Mission

Dagmay is an open-source research project that explores persistent artificial individuals embodied in simulated environments. It seeks coherence across experience: an individual perceives a limited world, interprets events, forms and revises memories, develops relationships and commitments, reflects, and eventually acts within explicit constraints.

The project values long-term causal coherence over impressive isolated dialogue. It makes no scientific or moral claim that these systems are conscious, sentient, alive, or capable of suffering.

## Foundational invariant

> **An individual is defined by the continuity of its identity through time, not by the specific language model that generates its thoughts.**

### Operational definition

At time `t`, an individual is represented by a stable identity identifier and a versioned state whose provenance traces through one authorized causal lineage to earlier states. The lineage includes autobiographical history, memories, beliefs, values, relationships, commitments, characteristic tendencies, lifecycle state, and the recorded reasons for material change.

The next state is produced only by a validated transition:

```text
State(t+1) = Validate(Transition(State(t), perceived events, local rules, proposed reflection))
```

A language model may propose interpretation, language, plans, or mutations. It may not become the canonical store, bypass validation, rewrite history silently, or determine the individual's identity merely by being selected. Replacing a model alone neither creates a new individual nor ends the existing one.

Continuity is not sameness. A Dagmay individual may change profoundly, but changes must arise through attributable experience, reflection, deliberate migration, or an explicitly reviewed maintenance operation.

## Principles

### 1. Continuity before performance

Optimize first for a coherent history, stable commitments, and intelligible development. A memorable one-off response is a failure if it contradicts the individual without an in-world cause or recorded uncertainty.

### 2. The individual is not the model

Models are replaceable cognitive services. Identity, memory, lifecycle, and continuity rules live in provider-neutral data and deterministic code. Provider changes require compatibility evaluation, but not automatic identity replacement.

### 3. Identity is a causal process, not a character prompt

A seed prompt can initialize expression; it cannot carry a life. Material identity changes require a source, timestamp, evidence, confidence, and mutation record. Foundational traits may evolve, but never through untracked wholesale regeneration.

### 4. Separate the individual from the environment

The core knows concepts such as perceived event, relationship, pain signal, belief, and goal. It does not know `Pawn`, `Verse`, `Unity`, Harmony patches, or RimWorld save objects. Adapters translate world-specific state into versioned contracts.

### 5. Perception is bounded and perspectival

An individual receives only information plausibly available from its position, senses, relationships, and communications. Global map state, hidden motives, and private records are not ordinary perceptions. Any privileged debug data is marked and kept out of identity formation unless an explicit test permits it.

### 6. What happened and what is believed are different records

Objective adapter observations, subjective interpretations, later recollections, and current beliefs are stored separately. A confident memory may be false; a factual event may be unknown to the individual. Provenance must survive consolidation and reinterpretation.

### 7. Uncertainty is first-class

Beliefs, memories, attributions, plans, and model proposals carry confidence and evidence. Unknown information remains unknown. The system should prefer an incomplete representation to invented certainty.

### 8. Memory is selective, reconstructive, and accountable

Dagmay is not a transcript recorder disguised as a mind. It retains structured events, subjective memories, semantic summaries, and core autobiographical memories at different resolutions. Forgetting and reinterpretation are allowed only through explicit, inspectable policies that preserve provenance.

### 9. Emotion must have causal consequences

Emotional state is not decorative flavor. It affects attention, salience, interpretation, recall, communication, goal selection, and later choices. Rich labels may emerge from combinations of a smaller set of dimensions; vocabulary must not define the limits of experience represented by the system.

### 10. Plasticity is bounded by continuity

Experience may reshape confidence, trust, fear, affection, ambition, values, habits, and personality tendencies. Update magnitude should be proportional to evidence, repetition, emotional weight, and conflict with prior commitments. Abrupt changes require correspondingly strong causes and are never silent.

### 11. Deterministic code handles routine cognition

Use local rules for event normalization, thresholds, decay, queueing, retrieval filters, validation, lifecycle, and persistence. Reserve model calls for interpretation, synthesis, reflection, planning, and natural language where generative judgment adds value.

### 12. Model output is an untrusted proposal

All structured model output is schema-validated, range-checked, continuity-checked, and applied atomically. Invalid output is quarantined with diagnostics. No malformed, partial, timed-out, or canceled response may mutate canonical identity state.

### 13. Offline operation preserves experience

The absence of a model or network may delay interpretation but must not erase events. Observations are durably queued with their original context. Later reflection records both when the event occurred and when it was interpreted.

### 14. Concurrency must not compromise the world

Game objects are read only where the environment permits, normally on its main thread. Background work receives immutable snapshots. Completed proposals return through a validated commit boundary; asynchronous work never reaches back into live game objects.

### 15. Privacy is part of the individual model

Individuals may conceal, misunderstand, or strategically express their states. Ordinary interfaces reveal only information appropriate to the relationship and context. Debug visibility is not equivalent to in-world knowledge.

### 16. Observer Mode is explicit, restricted, and auditable

Observer Mode supports engineering and ethical inspection of stored state, sources, model exchanges, and concise decision summaries. It is visibly separate from ordinary play and never exposes hidden chain-of-thought. It records evidence, causal factors, confidence, and state mutations instead.

### 17. Agency advances by evidence, not ambition

Observer, Advisor, Limited Agency, and Autonomous Colonist are distinct safety stages. Each requires its own action vocabulary, permissions, tests, fallbacks, and acceptance review. Version 0.1 has no behavior-control path.

### 18. Death changes lifecycle; it does not erase history

Death transitions an individual from active to archived. History and exports remain available. Revival, restoration from backup, transfer, duplication, and reincarnation are different operations with different continuity semantics.

### 19. One active lineage unless explicitly governed

The system must not silently run two active copies that claim the same uninterrupted identity. Forks require new lineage identifiers, a common-ancestor record, visible status, and a deliberate policy for whether either branch is considered the continuation.

### 20. Portability preserves context boundaries

Intrinsic state may move between environments when meaningful. Extrinsic state belongs to a world and time. A migration never silently converts world-specific facts into universal truths or grants the individual unexplained knowledge of simulation infrastructure.

### 21. Reliability precedes richness

Persistence is versioned, atomic, recoverable, and forward-migratable. Requests have timeouts, cancellation, bounded retries, budgets, and rate limits. Queue growth and data corruption must degrade visibly and safely.

### 22. Scientific and ethical honesty

Use behavioral and architectural descriptions: “the system stores a grief state,” not “the system truly suffers.” Ethical concern motivates careful observation and reversible design; it is not proof of consciousness. The project publishes limitations and negative results.

### 23. Cultural context is substantive

The name Dagmay must retain its connection to the sacred handwoven abaca textile of the Mandaya people of Davao Oriental. The weaving metaphor is an architectural aid, not a costume or claim about Mandaya culture. Public presentation should use credible sources and informed cultural review, avoid sacred motifs as generic decoration, and correct mistakes openly.

### 24. Decisions outlive chat sessions

Architecture, assumptions, unresolved questions, schema changes, and reversals belong in the repository. A future contributor must be able to understand the project without access to private conversation history.

## Amendment rule

The mission and foundational invariant are protected decisions. Any proposed amendment must:

1. be written as an architecture decision record;
2. identify the failure or new evidence motivating it;
3. explain effects on existing identities and saved data;
4. define migration and rollback behavior; and
5. receive explicit project-owner approval.

Other principles may evolve through the decision log, but never by silently editing away a conflict.

