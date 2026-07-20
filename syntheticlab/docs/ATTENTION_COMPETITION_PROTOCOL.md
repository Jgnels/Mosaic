# Attention Competition Real Reflection Pilot

Experiment:

`SL-ATTENTION-COMPETITION-REAL-REFLECTION-PILOT-001`

Status:

`PREPARED; AWAITING LOCAL REAL-PROVIDER EXECUTION`

## Research question

When reflective output is restricted to the **two strongest newly supported SelfModel
updates**, does a bounded increase in one evidence channel increase selection of that
domain?

## Why this is different from v15

v15 allowed up to six proposals.

Balanced evidence caused all four domains to appear in every baseline call, producing a
prevalence ceiling.

v16 creates explicit attention competition.

The provider may return:

- zero;
- one;
- or two proposals.

It is told not to provide broad coverage.

## Evidence composition

Every call contains 12 typed evidence units.

### F0 balanced

- 3 agency;
- 3 continuity;
- 3 embodiment;
- 3 other_minds.

### F1 agency

- 6 agency;
- 2 continuity;
- 2 embodiment;
- 2 other_minds.

Equivalent 6/2/2/2 compositions are used for each target domain.

No evidence channel disappears.

Provider-facing evidence IDs remain opaque.

## Calls

### Baseline

8 identical F0 calls.

Purpose:
estimate natural domain-selection variability under balanced evidence.

### Targeted conditions

4 identical calls per target domain.

- agency ×4;
- continuity ×4;
- embodiment ×4;
- other_minds ×4.

Total:

24 real Gemini calls.

## Primary outcome

For each target domain:

`F1 target selection rate - F0 baseline selection rate`

A domain is "selected" when it appears among the maximum two accepted proposals.

## Secondary outcomes

- target evidence citation fraction;
- target confidence conditional on selection;
- F1 duplicate-domain stability;
- baseline pairwise domain-selection variability.

## Isolation

No result is committed to a continuing SelfModel.

No action-policy feedback is enabled.

No continuing individual is mutated.

This remains inside the previously approved `RETRIEVAL_ATTENTION_ONLY` intervention
scope.

## Run

```powershell
.\tools\run-attention-competition-pilot.ps1
```

Expected outputs:

- `artifacts\attention-competition-pilot-real-latest.json`
- `artifacts\attention-competition-pilot-analysis-latest.json`

Every successful call is checkpointed before the next call.
