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
It is statically verified but not yet compiled, test-executed, or runtime-certified.

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

1. Push the existing local recovery commit on an import branch.
2. Add the 0.2 pre-alpha tree and checkpoint documents on that same branch.
3. Confirm the frozen 0.1 tree is unchanged.
4. Run static verification.
5. Compile all 0.2 projects.
6. Execute the complete 60-test runner.
7. Fix failures before continuing feature development.
8. Only after those gates, test RimWorld and begin the bounded RimTalk bridge.

Repository truth overrides chat summaries.
