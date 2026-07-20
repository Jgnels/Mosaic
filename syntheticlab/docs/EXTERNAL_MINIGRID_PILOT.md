# External MiniGrid Pilot

## Purpose

Test the developmental mechanism outside Dagmay-authored environments.

This is an environment-generalization pilot, not an identity-migration experiment.

## Tested external stack

Release validation environment:
- MiniGrid 3.1.0
- Gymnasium 1.3.0
- Python 3.13.5

## Environments

Stage E1:
`MiniGrid-Empty-5x5-v0`

Stage E2:
`MiniGrid-DoorKey-6x6-v0`

Stage E3:
`MiniGrid-FourRooms-v0`

## Information boundary

MiniGrid's partial observation is converted to:
- direction;
- digest of the partial image;
- opaque mission token.

The developmental learner does not receive natural-language mission semantics.

## Persistence

Because the external environment's private internal state is not treated as Dagmay-owned canonical storage, the adapter supports deterministic replay checkpoints:

- environment ID;
- seed;
- action history;
- checkpoint hash.

Actual MiniGrid replay testing achieved identical seeded action-trace outcomes in the v7.0 release environment.

## Initial result

The generic external learner performs better than random on Empty 5x5, but does not outperform random on DoorKey or FourRooms.

This is retained as evidence that the current developmental substrate lacks mechanisms likely needed for compositional external tasks, such as:
- explicit object affordance learning;
- multi-step planning;
- hierarchical procedural skill composition;
- better state abstraction.

The benchmark is not to be simplified merely to improve scores.
