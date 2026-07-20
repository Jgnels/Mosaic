# Provider Confidence Calibration

The schema-balanced experiment exposed a serious calibration problem.

Observed task accuracy:
0.500

Mean provider-reported confidence:
0.950

Calibration gap:
0.450

The model reported essentially the same confidence when right and wrong.

## New rule

Provider confidence is metadata.

It is **not** treated as calibrated probability.

Persistent-state mutation decisions should use:

- evidence provenance;
- validator rules;
- task-specific empirical reliability;
- experimental controls;

rather than provider confidence alone.

For the current six-call schema-balanced task, a provider confidence of 0.95 receives
a conservative task-specific trust value of:

0.500

This registry is task-specific. Failure on one causal-analysis task does not imply
0.50 reliability on unrelated language tasks.
