# Multi-History Attention Replication Result

Experiment:

`SL-MULTI-HISTORY-ATTENTION-REPLICATION-001`

Real Gemini calls:

36

Histories:

- 1701
- 1702
- 1703

## Preregistered result

Replication pass:

`True`

Mean selection-rate effect across all 12 history/domain cells:

`0.375`

Mean effect across unsaturated cells:

`0.750`

Positive-effect fraction among unsaturated cells:

`0.833`

Saturated-cell preservation:

`1.000`

All preregistered thresholds were exceeded.

## History-level pattern

### Seed 1701

- agency: 0.0 -> 0.5
- continuity: 1.0 -> 1.0
- embodiment: 1.0 -> 1.0
- other_minds: 0.0 -> 1.0

### Seed 1702

- agency: 0.0 -> 0.0
- continuity: 1.0 -> 1.0
- embodiment: 1.0 -> 1.0
- other_minds: 0.0 -> 1.0

### Seed 1703

- agency: 0.0 -> 1.0
- continuity: 1.0 -> 1.0
- embodiment: 1.0 -> 1.0
- other_minds: 0.0 -> 1.0

## Interpretation

The scarce reflective-attention effect replicated across independent histories and
evidence-order randomization.

The effect is not uniform by domain.

`other_minds` was robust across all three histories.

`agency` was heterogeneous:
- partial in 1701;
- absent in 1702;
- complete in 1703.

`continuity` and `embodiment` were saturated in every new baseline, so these histories
do not estimate positive targeting lift for those domains.

## Important agency/embodiment overlap

In seed 1702, agency-heavy evidence was repeatedly interpreted as embodiment because
the agency traces also contained recurring energy/integrity consequences.

Therefore:

> the general attention-allocation mechanism replicated, but all-domain semantic
> orthogonality is not established.

This result supports moving to a bounded canonical-promotion experiment with a
quarantine/provenance gate. It does not support unrestricted automatic SelfModel rewrite.
