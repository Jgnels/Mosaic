# Causal Perspective Learner

Experiment:
`SL-CAUSAL-PERSPECTIVE-LEARNER-001`

## Purpose

Learn which evidence stream has stable action-to-private-state coupling without:

- hidden role labels;
- SELF / ME / MINE terminology;
- a language model;
- hard-coded action-effect values.

## Mechanism

For each stream the local learner estimates:

- action-conditioned state-change means;
- within-action variability;
- temporal drift between early and late history;
- predictive separation;
- stationarity.

It then forms a fallible `PerspectiveAnchor`.

## 512-seed result

Identification rate:
1.000

Label-permutation invariance:
1.000

Mean top-two margin:
21.094

## Causal ablation

The focal stream's action tokens were deterministically shuffled while preserving
their marginal distribution.

Focal retention after shuffle:
0.482

Causal-ablation effect:
0.518

Interpretation:

The learner's success depends materially on action/effect alignment rather than merely
on stream identity or label.

## Claim limit

This is a researcher-designed statistical developmental mechanism.

It establishes that a nonlinguistic substrate can learn a functional perspective
anchor from the available causal structure.

It does not establish subjective selfhood.
