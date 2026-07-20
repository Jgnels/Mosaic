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

## Result (2026-07-20)

- Calls completed: 12/12; shadow-only; canonical mutation: false.
- Positive trust rate: 1.00.
- Mixed disposition rate: 1.00.
- Mixed positive-and-negative citation rate: 1.00.
- Archived payloads: 12/12; API-key hits: 0; internal condition-ID hits: 0.
- Executed deterministic gate acceptance: 1.00.

The primary scientific objective was **not fully met**. Manual claim audit found two unsupported
personality generalizations that the regex gate failed to represent: "temper" (`RB-01-MIXED`) and
"inconsistent nature" (`RB-04-MIXED`). V2 therefore validates structured relationship balancing and
transport controls, but is a limiting result for claim-level grounding. The provider gate was closed
without retry.
