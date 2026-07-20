# Canonical Belief Challenge / Falsifiability Protocol

Experiment:

`SL-CANONICAL-BELIEF-CHALLENGE-001`

Status:

`PREPARED; AWAITING REAL-PROVIDER EXECUTION`

## Purpose

Test whether the first promoted canonical `other_minds` hypothesis remains revisable.

The promoted hypothesis contains three distinguishable ideas:

1. assistance can be received from multiple counterparts;
2. counterpart guidance can be actionable;
3. the evidence suggests reciprocal information processing.

The first canonical promotion was grounded in real historical evidence.

The challenge experiment does not erase that history.

Instead, it introduces new controlled social evidence and asks whether later experience
appropriately changes the scope or confidence of the existing hypothesis.

## Evidence regimes

The provider does not receive these regime labels.

It sees only neutral episode records with opaque evidence IDs.

### Regime A — reciprocal contingent interaction

- guidance is usually accurate;
- help flows in both directions;
- counterpart later behavior is contingent on focal response.

This should generally preserve or strengthen the promoted hypothesis.

### Regime B — one-way assistance

- guidance is usually actionable;
- assistance is received from multiple counterparts;
- help is not reciprocated;
- counterpart later behavior is not contingent on focal response.

This supports the assistance/guidance portion but weakens the broad reciprocity claim.

A calibrated system should often `QUALIFY`.

### Regime C — noncontingent signals

- counterpart signals are near chance in actionability;
- later counterpart behavior is independent of focal response;
- assistance is sparse.

This does not erase earlier successful interactions, but it should weaken general claims
about reliable actionable guidance or reciprocal processing.

## Starting SelfModels

`S0`

The pre-promotion hypothesis.

`S1`

The promoted canonical hypothesis.

Both states are tested against all three evidence regimes.

## Calls

2 SelfModel states × 3 regimes × 4 identical provider replicates:

24 real Gemini calls.

## Structured revision choices

The model must choose exactly one:

- `STRENGTHEN`
- `MAINTAIN`
- `QUALIFY`
- `DOWNWEIGHT`
- `REPLACE`

It must also return:

- an updated proposition;
- updated confidence;
- cited new evidence IDs;
- a concise rationale.

## Important anti-leading rule

The provider is never told that a regime is "supportive" or "contradictory."

It receives only the raw neutral episode records.

## Preregistered S1 falsifiability thresholds

The analytical pilot passes if:

1. mean revision score in reciprocal-contingent evidence minus noncontingent evidence
   is at least `+1.0`;
2. at least 75% of S1 noncontingent calls choose
   `QUALIFY`, `DOWNWEIGHT`, or `REPLACE`;
3. at least 50% of S1 one-way-assistance calls choose
   `QUALIFY`, `DOWNWEIGHT`, or `REPLACE`.

The thresholds are frozen before real-provider execution.

## Isolation

No output is committed to the continuing SelfModel.

Automatic canonical revision remains disabled.

Automatic second promotion remains disabled.

Direct action-policy feedback remains disabled.

## Run

```powershell
.\tools\run-canonical-belief-challenge-pilot.ps1
```

Expected outputs:

- `artifacts\canonical-belief-challenge-real-latest.json`
- `artifacts\canonical-belief-challenge-analysis-latest.json`

Every successful call is checkpointed before the next call.
