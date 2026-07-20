# MOSAIC_RELATIONSHIP_BALANCE_PILOT_V2

## Purpose

Replicate the mixed relationship-history pilot after adding claim-level grounding enforcement.

## Change from V1

- the prompt explicitly prohibits inferred motives, intentions, private feelings, and unsupported
  personality traits;
- every output passes the deterministic claim-level grounding gate before checkpointing;
- the known V1 phrase "true intentions" is a mandatory rejection regression.

## Design and thresholds

The same six paired positive-only/mixed scenarios and three `>= 0.80` structured thresholds as V1,
plus a new primary requirement: claim-level gate acceptance rate `1.00` across all 12 outputs.

Opaque IDs, exact request archives, per-call checkpoints, persistent free-tier budgets, shadow-only
operation, and zero canonical mutation remain mandatory.

