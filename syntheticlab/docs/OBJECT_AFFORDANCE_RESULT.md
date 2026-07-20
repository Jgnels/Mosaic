# Object-Relative Affordance Follow-up

v9.0 added an opaque object-relative learner that tracks:

- uninterpreted cell-token identity;
- relative position;
- action-induced observation change;
- reward association;
- successful abstract token/action traces.

No token is labeled key, door, wall, or goal.

## Tiny diagnostic result

2 seeds, 1200 steps per condition.

DoorKey fixed layout:
- ObjectAffordance mean completions: 0
- FeatureTrace mean completions: 0.5

DoorKey varying layout:
- ObjectAffordance mean completions: 0
- FeatureTrace mean completions: 0.5

## Interpretation

Simply making representations object-relative is still insufficient.

The missing architecture is increasingly specific:

- persistent object identity through movement/occlusion;
- causal multi-stage dependency discovery;
- subgoal induction;
- delayed credit across interaction chains;
- hierarchical skill composition.

This negative result is retained.
