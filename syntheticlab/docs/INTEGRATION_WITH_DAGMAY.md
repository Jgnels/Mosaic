# Integration with the Main Dagmay Program

SyntheticLab should remain a separate project until J.3 and coordinated checkpoint semantics are stable.

Later integration should occur through contracts, not by copying SyntheticLab state directly into Generation One.

Recommended shared contracts:

```text
IEnvironmentAdapter
IPerceptionAdapter
IDevelopmentalLearner
IEventSegmenter
IAppraisalEngine
IBeliefStore
IRelationshipModel
IProceduralSkillStore
```

Generation One (Lynx, Schmidt, Michael, Doyle) should not be retroactively assigned SyntheticLab developmental histories.

SyntheticLab creates future experimental cohorts.

## Critical distinction

`actual historical state`

is not the same as:

`counterfactual replay of old evidence through a newer algorithm`.

Both may be useful, but they require separate branch/experiment identifiers.
