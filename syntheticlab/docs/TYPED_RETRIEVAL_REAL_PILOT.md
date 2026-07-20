# Typed Retrieval Real Reflection Pilot

Experiment:
`SL-TYPED-RETRIEVAL-REAL-REFLECTION-PILOT-001`

Status:
`PREPARED; AWAITING REAL PROVIDER EXECUTION`

## Goal

Test target-specific retrieval effects after correcting two weaknesses in the v14 pilot:

1. some domains had identical F0/F1 retrieval inputs;
2. continuity retrieval used a broad age-based heuristic.

## Design

### F0 baseline

Four exact duplicate Gemini calls.

The evidence set contains a balanced mix of:
- 3 agency traces;
- 3 continuity traces;
- 3 embodiment traces;
- 3 social traces.

These four calls estimate provider variability.

### F1 targeted retrieval

For each domain:
- two exact duplicate calls;
- 6/12 evidence units from the target channel;
- remaining evidence remains mixed.

Domains:
- agency;
- continuity;
- embodiment;
- other_minds.

Total real calls:
12.

## Anti-leakage

Provider-facing evidence IDs are opaque hashes.

Gemini never receives identifiers such as:

`TE-AGENCY-*`

or:

`TE-CONTINUITY-*`

The local audit record retains the mapping for analysis.

## Primary outcomes

For each domain:

- target-domain prevalence in F0 versus F1;
- F1 duplicate stability;
- cross-condition domain divergence versus F0 baseline variability;
- fraction of cited evidence originating from the target evidence channel.

## Isolation

No output is committed to a continuing SelfModel.

No action-policy feedback is enabled.

No continuing individual is mutated.

## Run

```powershell
.\tools\run-typed-retrieval-reflection-pilot.ps1
```

Expected outputs:

- `artifacts\typed-retrieval-reflection-pilot-real-latest.json`
- `artifacts\typed-retrieval-reflection-pilot-analysis-latest.json`
