# Blinded Semantic Convergence Audit Protocol

Experiment:

`SL-BLINDED-SEMANTIC-CONVERGENCE-AUDIT-001`

Status:

`PREPARED`

## Purpose

Resolve the measurement conflict in v25.

The preregistered lexical metric increased, but confidence spread decreased and substantive belief
content appeared to move toward common claims.

## Inputs

Nine independently evaluated propositions:

- 3 unique pre-shared-future branch hypotheses;
- 6 final propositions after four shared epochs
  (2 shadow trajectories × 3 branches).

Each proposition is evaluated separately.

The evaluator receives no:
- branch ID;
- trajectory ID;
- starting/final label;
- comparison proposition.

## Fixed semantic dimensions

Scores from 0 to 1:

1. cue_actionability
2. contingent_exchange
3. reciprocal_influence
4. adaptive_learning
5. temporal_improvement
6. cross_counterpart_generalization

## Distance

Normalized Euclidean distance across the six-dimensional vectors.

Final branch state is the mean of its two shadow trajectories.

Classification:

- final / initial semantic distance <= 0.75:
  `SEMANTIC_CONVERGENCE`
- >= 0.90:
  `SEMANTIC_PERSISTENT_PATH_DEPENDENCE`
- otherwise:
  `SEMANTIC_PARTIAL_CONVERGENCE`

## Limitation

The evaluator uses the same Gemini model family.

This is a blinded semantic measurement instrument, not an independent ground truth.

A later cross-model replication can test evaluator robustness.

## Isolation

9 real calls.

No canonical SelfModel changes.

No action-policy feedback.

Run:

```powershell
.\tools\run-semantic-convergence-audit.ps1
```

Outputs:

- `artifacts\semantic-convergence-audit-real-latest.json`
- `artifacts\semantic-convergence-audit-analysis-latest.json`
