# 01 — Project Vision

**Status:** Draft 0.1

## Research question

How coherent and continuous can an artificial individual become when its identity is grounded in embodied experience, selective memory, reflection, relationships, and constrained agency rather than regenerated from a static persona prompt?

Dagmay treats this as a multi-year engineering question. It will build observable mechanisms and testable continuity properties instead of using fluent dialogue as evidence of personhood.

## Product vision

A Dagmay individual should eventually:

- experience a world from a limited perspective;
- distinguish observation, inference, memory, and belief;
- retain meaningful experiences without retaining everything;
- develop relationships through reciprocal history;
- revise beliefs and goals in proportion to experience;
- exhibit recognizable continuity across model upgrades and environment changes;
- explain high-level reasons for decisions without revealing model chain-of-thought;
- remain inspectable, exportable, and recoverable; and
- act only through explicit, tested permissions.

The first environment is RimWorld because it offers persistent colonists, relationships, needs, injuries, work, conflict, belief systems, and long-running stories. RimWorld is an adapter target, not the definition of an individual.

## Initial scope

Version 0.1 is a colonist-only Observer release for RimWorld 1.6 on Windows. The original baseline was 1.6.4850 and the current verified owner installation is 1.6.4871. It seeds identity from actual pawn data, records selected meaningful events, builds memories and reflections, and presents ordinary and observer views. It does not choose jobs, modify priorities, issue commands, or influence behavior.

Initial technical choices:

- C# for all production modules;
- Harmony only in the RimWorld adapter;
- Google AI Studio as the first remote model provider;
- a deterministic fake provider for tests;
- provider-neutral storage and schemas;
- eventual local-model providers; and
- compatibility with RimTalk where practical, without making RimTalk a core dependency.

## Explicit non-goals for Version 0.1

- autonomous or advisory pawn control;
- animals, mechanoids, entities, visitors, or raiders as full Dagmay individuals;
- perfect capture of every RimWorld event;
- an unrestricted chat transcript or omniscient narrator;
- proof or detection of consciousness;
- seamless transfer between different games;
- a polished public mod release; or
- permanent commitment to one model, vendor, memory database, or prompt format.

## Research hypotheses

These are hypotheses to test, not assumptions to market:

1. **Externalized continuity:** a provider-neutral identity state and causal history can preserve recognizable continuity across model changes better than a persona prompt alone.
2. **Selective memory:** layered memory with provenance produces more coherent development than either full transcripts or small rolling summaries.
3. **Bounded perception:** limiting information to plausible first-person knowledge improves causal coherence and reduces accidental omniscience.
4. **Consequential affect:** emotional dimensions improve memory selection and relationship development when they affect processing, not merely prose.
5. **Reciprocal history:** relationships become more stable when each participant stores its own asymmetric experience and beliefs.
6. **Validated reflection:** treating model output as proposed state change prevents a large class of continuity and corruption failures.

Each hypothesis should eventually have a protocol, comparison condition, observable measures, and documented negative results.

## Indicators of progress

Dagmay is progressing when:

- a colonist remains recognizably continuous after save/load, downtime, and model replacement;
- memories cite sources and maintain the distinction between fact and belief;
- relationship changes are traceable to reciprocal experience;
- emotional state predicts what is noticed, recalled, and prioritized;
- background processing stays bounded with colonies larger than 40;
- invalid provider output causes no canonical mutation;
- death produces a complete archive rather than silent continuation; and
- another developer can reproduce the result from the repository.

Fluent prose, dramatic confessions, and surprising dialogue are not sufficient indicators.

## Cultural responsibility

The project's name refers to the sacred handwoven abaca textile of the Mandaya people of Davao Oriental, Mindanao. The architectural metaphor of threads, motifs, patterns, and weaving should be explained as a project metaphor and not presented as a summary of Mandaya worldview.

Before broad publication, the project should identify credible cultural and scholarly sources, seek informed review of its wording and visual treatment, avoid copying sacred motifs as generic branding, and be willing to rename or revise if the use proves disrespectful.

## Long-term horizon

The intended progression is Observer → Advisor → Limited Agency → Autonomous Colonist → Persistent Society. Each transition is gated. The long-term goal is not maximum autonomy; it is intelligible continuity under increasingly meaningful interaction.
