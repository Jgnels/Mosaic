# Multi-History Attention Replication Protocol

Experiment:

`SL-MULTI-HISTORY-ATTENTION-REPLICATION-001`

Status:

`PREPARED; AWAITING REAL-PROVIDER EXECUTION`

## Purpose

Test whether the positive v16 attention-competition result survives:

1. independent developmental histories;
2. different evidence content;
3. deterministic evidence-order randomization.

## Histories

Three new SyntheticLab histories:

- seed 1701;
- seed 1702;
- seed 1703.

The original seed 1601 result remains prior evidence and is not counted toward the
new replication thresholds.

## Per-history calls

### F0 baseline

4 identical calls using:

`3 / 3 / 3 / 3`

### F1 targeted

2 identical calls for each domain using:

`6 target / 2 / 2 / 2`

Domains:

- agency;
- continuity;
- embodiment;
- other_minds.

Total per history:

12 calls.

Total new real calls:

36.

## Evidence-order control

Evidence is sorted by a deterministic cryptographic key derived from:

- the history seed;
- the evidence ID;
- a frozen order-version string.

The target domain itself is not used to choose the presentation order.

Shared evidence units retain the same sort key across conditions.

This removes the v16 domain-block presentation order as a systematic explanation.

## Primary replication metrics

For each history × domain cell:

`targeted selection rate - baseline selection rate`

Primary aggregate:

mean selection-rate effect across all 12 new history/domain cells.

Secondary:

- fraction of unsaturated cells with a positive effect;
- preservation rate for baseline-saturated cells.

## Preregistered replication thresholds

The new three-history cohort passes if:

- mean effect across all cells > +0.20;
- at least 70% of unsaturated cells have a positive effect;
- every saturated baseline cell remains selected at 100% under targeting.

These thresholds apply to the **new histories only**.

## Isolation

- analytical calls only;
- maximum two proposals;
- all evidence channels present;
- provider-facing evidence IDs opaque;
- no continuing SelfModel commit;
- no action-policy feedback.

## Rate limiting

The live runner uses one shared rate-limit-safe transport across the entire call series.

Request starts are paced to remain below the observed Gemini 3.1 Flash Lite free-tier
RPM ceiling, with automatic 429 backoff.

## Run

```powershell
.\tools\run-multi-history-attention-replication.ps1
```

Expected files:

- `artifacts\multi-history-attention-replication-real-latest.json`
- `artifacts\multi-history-attention-replication-analysis-latest.json`

Every successful call is checkpointed before the next call.
