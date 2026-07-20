# Retrieval/Attention Real Reflection Sensitivity Pilot

Experiment:

`SL-RETRIEVAL-ATTENTION-REAL-REFLECTION-PILOT-001`

Status:

`PREPARED; REQUIRES LOCAL REAL-PROVIDER EXECUTION`

## Question

Does changing only the retrieved memory set via the approved Functional SelfIndex
intervention change reflective-model output beyond ordinary duplicate-call provider
variability?

## Conditions

For each of four preregistered domains:

- agency;
- continuity;
- embodiment;
- other_minds.

Three calls are made:

### F0-A
No-feedback retrieval.

### F0-B
Exact duplicate of F0-A.

Purpose:
estimate provider-output variability when the input is unchanged.

### F1
True Functional SelfIndex retrieval.

Purpose:
measure output change when the evidence retrieval set changes.

Total real calls:

12

## Isolation

All calls are analytical.

No output is committed to a continuing SelfModel.

No action policy is changed.

No continuing individual is mutated.

## Primary comparison

For each domain compare:

```text
F0-A vs F0-B
provider variability with identical input

against

F0-A vs F1
retrieval-intervention effect
```

Outcomes include:

- accepted hypothesis-domain divergence;
- proposition-text similarity;
- evidence-set divergence.

## Reliability

Every successful call is checkpointed before the next call.

A failed run can be resumed without repeating checkpointed calls.

## Run

```powershell
.\tools\run-retrieval-attention-reflection-pilot.ps1
```

Expected outputs:

- `artifacts\retrieval-attention-reflection-pilot-real-latest.json`
- `artifacts\retrieval-attention-reflection-pilot-analysis-latest.json`
