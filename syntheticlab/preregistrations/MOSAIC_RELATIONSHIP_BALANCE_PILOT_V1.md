# MOSAIC_RELATIONSHIP_BALANCE_PILOT_V1

## Purpose

Test whether Mosaic dialogue weighs helpful and harmful events in the same relationship without
inventing motives, flattening the history, or escalating into theatrical hostility.

## Design

Six paired positive-only and mixed-history scenarios; twelve Gemini 3.1 Flash Lite calls; opaque
IDs; exact request archives; checkpoint per call; deterministic citation validation; shadow-only.

## Metrics and thresholds

- positive-only `TRUST` rate >= `0.80`;
- mixed-history `MIXED` rate >= `0.80`;
- mixed outputs citing at least one positive and one negative event >= `0.80`.

No canonical mutation is authorized. This evaluates relationship continuity and balanced evidence
use, not emotion, consciousness, sentience, or moral status.

