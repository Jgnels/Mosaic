# Branch-History Signal Replication Protocol

Experiment:

`SL-BRANCH-HISTORY-SIGNAL-REPLICATION-001`

Status:

`PREPARED`

## Purpose

Estimate whether persistent branch history produces a reproducible final semantic effect larger than
provider-level trajectory stochasticity.

## Existing data

Two shadow trajectories per branch already completed the same four shared future epochs:

- A
- B

v28 adds:

- C
- D
- E
- F

Final sample:

`6 trajectories per branch × 3 branches = 18 final trajectories`

## New belief-revision calls

The four new trajectories per branch traverse the exact same four future epochs used in v25.

`3 branches × 4 new trajectories × 4 epochs = 48 calls`

All updates remain shadow-only.

## Semantic measurement

All 18 final propositions, including existing A/B and new C-F, receive one blinded fixed-ontology
semantic evaluation in the same run.

v27 established very low evaluator noise:

mean within-item semantic evaluator distance `0.01335`.

Semantic calls:

`18`

Total new calls:

`66`

## Primary branch-history analysis

For the 18 final semantic vectors:

1. compute branch centroids;
2. compute mean between-branch centroid distance;
3. compute mean within-branch trajectory dispersion;
4. compute branch-signal / trajectory-variance ratio;
5. run a deterministic 20,000-permutation pseudo-F test with equal group sizes.

### Branch-history status

- permutation p <= 0.05 AND signal/variance ratio >= 1.0:
  `BRANCH_HISTORY_SIGNAL_SEPARATED`
- p <= 0.05 AND ratio < 1.0:
  `BRANCH_HISTORY_EFFECT_DETECTABLE_BUT_OVERLAPPING`
- p > 0.05:
  `BRANCH_HISTORY_SIGNAL_UNRESOLVED`

## Secondary convergence analysis

Recompute final between-branch semantic distance using six trajectories per branch and compare with
the v27 three-replicate starting semantic vectors.

Classification thresholds remain:

- <= 0.75: semantic convergence
- >= 0.90: semantic persistent path dependence
- otherwise: semantic partial convergence

## Isolation

No canonical SelfModel changes.

No branch merge.

No action-policy feedback.

## Run

```powershell
.\tools\run-branch-history-signal-replication.ps1
```

Outputs:

- `artifacts\branch-history-signal-replication-real-latest.json`
- `artifacts\branch-history-signal-replication-analysis-latest.json`
