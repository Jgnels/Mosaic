# Semantic Measurement Reliability and Variance Decomposition

Experiment:

`SL-SEMANTIC-MEASUREMENT-RELIABILITY-001`

Status:

`PREPARED`

## Why

v26 classified the result as semantic partial convergence.

However, final within-branch trajectory dispersion was larger than final between-branch centroid
distance.

Before making claims about durable branch-level psychological separation, we need to separate:

1. semantic evaluator noise;
2. shadow-trajectory stochasticity;
3. persistent branch-history signal.

## Design

The nine propositions evaluated in v26 already have semantic evaluator replicate A.

v27 evaluates the exact same nine propositions two more times:

- replicate B;
- replicate C.

New calls:

`9 propositions × 2 additional evaluator replicates = 18`

Total semantic evaluations after completion:

`27`

Each evaluation remains blinded:
- one opaque item ID;
- one proposition;
- no branch label;
- no stage label;
- no other proposition.

## Primary analyses

### Evaluator reliability

Mean semantic distance among A/B/C evaluations of the exact same proposition.

### Classification stability

Recompute semantic convergence independently for evaluator replicates:

- A;
- B;
- C.

### Aggregated classification

Average A/B/C vectors for each proposition, then recompute start-to-final semantic convergence.

### Branch signal versus trajectory variance

Compare:

- final between-branch centroid distance;
- mean final within-branch trajectory dispersion.

Report:

`branch_signal_to_trajectory_variance_ratio`

Interpretation:

- >= 1.5:
  branch signal clearly exceeds trajectory variance;
- >= 1.0:
  branch signal modestly exceeds trajectory variance;
- < 1.0:
  branch signal is not separated from trajectory variance.

## Isolation

No canonical state changes.

No action-policy feedback.

## Run

```powershell
.\tools\run-semantic-measurement-reliability.ps1
```

Outputs:

- `artifacts\semantic-measurement-reliability-real-latest.json`
- `artifacts\semantic-measurement-reliability-analysis-latest.json`
