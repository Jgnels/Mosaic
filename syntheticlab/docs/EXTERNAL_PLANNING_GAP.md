# External Planning Gap

The first MiniGrid benchmark is intentionally not a success story.

## Observed pattern

Current generic adaptive learner:
- improves over random in Empty 5x5;
- does not outperform random in DoorKey 6x6;
- does not outperform random in FourRooms.

## Working interpretation

The current substrate is good at:
- recency-weighted action-value adaptation;
- local repeated-state learning;
- deterministic history-dependent updates.

It is weak at:
- discovering object affordances;
- representing subgoals;
- composing action sequences;
- planning over delayed sparse reward;
- transferring procedural structure between different layouts.

## Source-audit-aligned development direction

Relevant audited inspirations:
- Monty: modular sensor/learning/action boundaries;
- Voyager: procedural skill storage and reuse;
- continual-learning resources: explicit retention/adaptation mechanisms.

Dagmay-native direction:
1. learn opaque affordance models from action consequences;
2. derive bounded validated procedural skills;
3. store skills separately from autobiography;
4. compose skills under explicit planning;
5. benchmark within-layout learning separately from cross-layout transfer.

Do not solve the benchmark by exposing MiniGrid semantic object names directly to a pretrained LLM in the LLM-free pilot.
