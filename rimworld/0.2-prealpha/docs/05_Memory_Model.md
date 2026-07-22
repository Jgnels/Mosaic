# 05 — Memory Model

**Status:** Draft 0.1

## Goals

Dagmay memory must support continuity without becoming a complete transcript. It must distinguish world events from what an individual perceived, encoded, later recalled, and currently believes. It must also remain inspectable enough to diagnose drift and false confidence.

## Four related records

### 1. Environment event

A normalized record of something the adapter observed. It may be globally true within the limits of the adapter, but it is not automatically known by any individual.

Required conceptual fields:

- `EventId`, kind, and schema version;
- environment/world and adapter identifiers;
- occurred-at game time and observed-at time;
- subjects, objects, location scope, and factual payload;
- source hook and deduplication key;
- occurrence-time context snapshot; and
- correction/supersession links.

### 2. Perceived event

An individual's bounded perspective on an environment event.

It records:

- `IndividualId` and source `EventId`;
- perception channel: witnessed, experienced, told, inferred, or privileged test input;
- details available and intentionally omitted;
- perception confidence and ambiguity;
- distance/context where relevant; and
- the state version at perception time.

Different people may receive different perceived events from one environment event.

### 3. Subjective memory

An encoded interpretation, not a copy of the event.

It records:

- `MemoryId`, owner, and source event/memory identifiers;
- occurrence time and encoding/reflection time;
- concise diary-like recollection;
- appraisal and inferred meaning;
- affect vector before/after encoding;
- importance, emotional weight, confidence, and accessibility;
- involved people and relationship relevance;
- privacy/disclosure classification;
- recent, long-term, or core tier;
- contradictions and unresolved questions; and
- mutation/provenance history.

### 4. Belief

A currently held proposition supported or challenged by evidence. Beliefs may be derived from memories, testimony, inference, or repeated patterns and may be false.

It records:

- stable `BeliefId` and a structured proposition where feasible;
- confidence and uncertainty type;
- supporting and contradicting evidence references;
- source categories and social trust weighting;
- created and last-reviewed state versions;
- relationship or value implications; and
- status: active, questioned, superseded, or rejected.

## Why the separation matters

Example:

- Environment event: Mira gave Jo medicine after Jo was injured.
- Jo's perception: Jo experienced care from Mira while in severe pain.
- Jo's memory: “Mira stayed when I was helpless; I can rely on her.”
- Jo's belief: Mira is dependable, confidence 0.72.
- Mira's memory: “I treated Jo because no one else was available.”

The facts are shared; meaning and relationship consequences are asymmetric.

## Memory tiers

| Tier | Purpose | Typical contents | Retention behavior |
| --- | --- | --- | --- |
| Recent | Immediate context and unresolved experience | latest events, active conversations, current concerns | high detail; ages or consolidates |
| Significant long-term | Durable episodic history | major harm, help, conflict, milestones, recurring patterns | selective detail with source links |
| Core autobiographical | Identity-defining narrative anchors | formative relationships, commitments, traumas, achievements, turning points | rarely changed; reinterpretation requires strong cause and audit |

Tier is not truth. A core memory may be inaccurate but deeply identity-shaping.

## Importance and salience

Initial importance is computed deterministically from factors such as:

- threat, pain, loss, relief, novelty, and surprise;
- relevance to active goals and values;
- relationship strength and trust violation;
- repetition and pattern confirmation;
- agency or helplessness;
- social consequences; and
- current affective sensitivity.

Model reflection may propose a bounded adjustment with evidence. It cannot assign maximum importance without a qualifying cause or bypass configured ranges.

## Fundamental affect dimensions

The owner accepted seven initial dimensions. The representation uses continuous values with appraisal causes rather than a closed list of emotion words:

- valence: unpleasant ↔ pleasant;
- arousal: calm ↔ activated;
- threat/safety;
- agency/control;
- attachment/affiliation;
- certainty/confusion; and
- social standing: shame/subordination ↔ pride/status.

Labels such as gratitude, jealousy, nostalgia, guilt, relief, admiration, and betrayal are interpretations of patterns plus context. They are not prohibited merely because they are absent from the base vector.

## Retrieval

Retrieval is a scored, inspectable pipeline:

1. enforce owner, lifecycle, privacy, and time filters;
2. gather candidates through structured links and a semantic index;
3. score semantic relevance, recency, importance, emotional congruence, relationship relevance, goal relevance, and unresolved status;
4. diversify results to avoid retrieving near-duplicates only;
5. fit the context budget; and
6. record which memories were selected and why at a summary level.

The semantic index is a rebuildable projection. Embeddings are not canonical memory and do not define identity. Provider-specific embeddings must be versioned so the index can be rebuilt after a model change.

## Consolidation

Consolidation runs when queues and budgets permit. It may:

- group related recent memories;
- identify repeated patterns;
- promote a memory to long-term or core status;
- update belief evidence and confidence;
- create a compact semantic summary;
- lower accessibility of redundant detail; or
- mark a contradiction for later reflection.

Every consolidation creates a `MemoryMutation` with source IDs, before/after fields, actor (`deterministic`, `model-proposed`, `migration`, or `repair`), confidence, rationale summary, and state versions.

Consolidation cannot invent a new environment fact. Inferences are stored and labeled as inferences.

## Reinterpretation

Later experience may change the meaning of an old memory without changing the original event. Reinterpretation appends a new lens and may update beliefs or importance. The prior interpretation remains available to Observer Mode so personality development is traceable.

## Forgetting

Forgetting initially means reduced cognitive accessibility, loss of detail, or replacement by a consolidated gist. It does not mean silently deleting the audit ledger.

Candidate mechanisms:

- accessibility decay with rehearsal and emotional reinforcement;
- detail decay while retaining people, outcome, and appraisal;
- interference among similar low-importance memories;
- suppression from normal retrieval while remaining observable; and
- explicit archival retention policies for old raw data.

Whether audit events are retained forever is an unresolved privacy and storage decision. Cognitive forgetting and data deletion must remain distinct operations.

## Contradiction handling

Dagmay does not force immediate consistency. When evidence conflicts:

- retain both evidence paths;
- reduce confidence or mark the belief contested;
- consider source reliability and relationship trust;
- schedule reflection only when material; and
- allow defensive or biased interpretation to exist as individual state without relabeling it as fact.

Observer Mode should show the conflict. Ordinary dialogue may not disclose it.

## Safe model mutation envelope

Version 0.1D implements only the narrowest first subset: a concise interpretation/autobiographical reflection retained in the private audit record plus one bounded target-affect vector. Richer memory, belief, relationship, and tier operations below remain designed and require their own schemas and validators before activation.

Version 0.1 model proposals may only:

- create a subjective memory linked to supplied sources;
- add an interpretation to an existing memory;
- propose bounded affect adjustments;
- add or revise belief evidence and confidence within limits;
- propose memory importance/tier changes;
- identify a relationship implication; and
- create a concise autobiographical reflection.

They may not change identity IDs, lineage, lifecycle, source events, schema versions, administrative records, API configuration, or activation status.

## Initial persistence approach

Use human-inspectable, versioned structured records for the first release. The exact database is deferred until a prototype measures volume and query needs. The domain model must not depend on SQLite, JSON files, a vector database, or any one storage engine.
