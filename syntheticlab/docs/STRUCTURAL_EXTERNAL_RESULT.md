# Structural External-World Follow-up

A new opaque structural learner was tested after the v7.3 MiniGrid results.

Mechanisms:
- state-transition graph;
- action-change affordance estimates;
- known-reward reverse path search;
- bounded successful action-sequence storage.

No semantic MiniGrid object names were supplied.

## Result

The structural model strongly improved completion count in Empty 5x5.

It did not improve:
- DoorKey;
- FourRooms.

In the small 4-seed follow-up:
- DoorKey structural mean completions: 0;
- FourRooms structural mean completions: 0.

FeatureTrace remained better on those harder tasks.

## Interpretation

Adding an explicit graph is not sufficient.

The current missing mechanisms are likely more specific:
- object-relative affordances;
- persistent latent object identity;
- subgoal discovery;
- credit assignment across pickup/unlock/open/goal chains;
- hierarchical skill composition.

This is a useful negative result.

The project should not equate:
> explicit world model

with:
> useful planning model.
