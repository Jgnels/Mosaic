# Multi-Epoch Common-Future Convergence Protocol

Experiment:

`SL-MULTI-EPOCH-COMMON-FUTURE-CONVERGENCE-PILOT-001`

Status:

`PREPARED; AWAITING REAL-PROVIDER EXECUTION`

## Question

When psychologically divergent branches repeatedly encounter the same later environment, do their
self-model trajectories converge, partially converge, or remain path-dependent?

## Design

Starting branches:

- reciprocal-history canonical branch;
- one-way-history canonical branch;
- unchanged noncontingent-history branch.

Each branch has two independent provider trajectories:

- A
- B

All six trajectories receive the same four future epochs in the same order.

Each epoch is a newly generated 384-interaction moderately contingent social history.

Total:

`3 branches × 2 trajectories × 4 epochs = 24 real Gemini calls`

## Sequential shadow state

After each validated provider revision:

- the updated proposition and confidence become the next epoch's **shadow** prior;
- the real canonical SelfModel remains unchanged.

This allows longitudinal belief dynamics without crossing another canonical intervention gate.

## Primary convergence proxy

At every epoch, compute mean pairwise lexical Jaccard distance between branch propositions.

Classification is frozen before real execution:

- final / initial distance <= 0.75 -> `CONVERGENCE`
- final / initial distance >= 0.90 -> `PERSISTENT_PATH_DEPENDENCE`
- otherwise -> `PARTIAL_CONVERGENCE`

This is explicitly a surface-language proxy, not a direct metric of latent psychological distance.

## Secondary outcomes

- confidence spread;
- branch-specific decision distributions by epoch;
- trajectory consistency.

## Isolation

No shadow revision is committed.

No canonical SelfModel changes.

No action-policy feedback.

## Run

```powershell
.\tools\run-multi-epoch-common-future-pilot.ps1
```

Expected outputs:

- `artifacts\multi-epoch-common-future-real-latest.json`
- `artifacts\multi-epoch-common-future-analysis-latest.json`
