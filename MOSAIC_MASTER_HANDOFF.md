# Mosaic Master Handoff

**Checkpoint:** RimWorld 0.2 Pre-Alpha  
**Prepared:** 2026-07-21  
**Historical name:** Dagmay

## Project objective

Build excellent persistent game characters whose memories, relationships, beliefs, preferences,
goals, and behavior develop causally through simulated experience.

Mosaic does not pursue or claim consciousness, sentience, or moral patienthood and does not optimize
deceptive appearances of consciousness.

## Foundational invariant

> Character continuity belongs to durable state, not to the language model generating the current
> inference.

## Repository tracks

### SyntheticLab

Controlled research and offline reference mechanisms. Preserve its existing governance and
scientific stop lines.

### RimWorld 0.1 closure candidate

Path:

```text
rimworld/0.1-closure-candidate/
```

Recovered Observer-only reliability/closure source. It remains frozen. Do not add 0.2 mechanisms to
this directory and do not silently relabel it 0.1RC.

### RimWorld 0.2 pre-alpha

Path:

```text
rimworld/0.2-prealpha/
```

A separate derived development tree containing the cumulative architecture work through Slice 06.
Static verification, compilation, all 60 contract tests, all six integration scenarios, and the
Release RimWorld adapter build pass. It is not yet in-game runtime- or save-compatibility-certified.

## Core 0.2 architecture

- durable identity separated from environment binding;
- temporal facts with evidence, validity, confidence, and supersession;
- pure appraisal contracts;
- decision-stage traces without hidden chain-of-thought;
- portable external-mod event proposals;
- deterministic adapter admission;
- explicit action-origin attribution;
- explicit trait/behavior influence provenance;
- lifecycle-bearing story threads;
- milestone progress and contribution attribution;
- immutable evidence-grounded presentation packets;
- observer, prompt, and UI purity.

## Immediate next actions

1. Preserve the frozen 0.1 closure candidate unchanged.
2. Complete the full non-installing 0.2 release build and record its package hash.
3. Review and merge the import pull request after verification documentation is committed.
4. Test the packaged 0.2 build in RimWorld using a copied save and minimal dependency set.
5. Validate save/load, observer purity, identity continuity, and failure isolation in-game.
6. Add the owner's real mod stack in controlled compatibility groups.
7. Begin the bounded, read-only RimTalk bridge only after the runtime foundation passes.

Repository truth overrides chat summaries.
