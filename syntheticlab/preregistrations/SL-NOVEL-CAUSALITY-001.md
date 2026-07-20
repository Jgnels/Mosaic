# Preregistration — SL-NOVEL-CAUSALITY-001

## Hypothesis

A learner that continually updates context-conditioned causal expectations from its own experiences will select the hidden optimal action more accurately than:

1. a context-free frequency learner;
2. a no-learning control;
3. a learner trained on an equally large but causally mismatched history.

## Null

Personal causal history provides no reliable advantage over those controls.

## Positive control

An oracle supplied with the hidden mapping should perform at ceiling.

The oracle is not evidence of learning. It demonstrates that identical behavioral success can arise from a fundamentally different causal process.

## Primary metric

Fraction of opaque contexts for which the agent selects the world's true optimal action after training.

## Default confirmatory configuration

- 64 seeds
- 320 exploration episodes per seed
- 4 contexts
- 4 actions
- environment noise = 0.08

These defaults must not be changed after viewing a result and then described as the same confirmatory experiment.
