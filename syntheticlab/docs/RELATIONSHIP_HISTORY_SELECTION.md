# Relationship History Selection

Mosaic selects at most two verified direct events per valence for a counterpart. Selection favors
evidence quality and importance, uses recency only as a small tie-breaker, rejects rumors and weak
inferences, and preserves both helpful and harmful evidence when both exist.

The deterministic fallback assessment is `TRUST`, `MIXED`, `DISTRUST`, or
`INSUFFICIENT_EVIDENCE` based solely on the selected evidence composition. This provides useful
relationship continuity even when no provider is available.

The first offline long-history experiment uses 128 seeded histories containing four older direct
relationship events, 96 unrelated distractors, and eight newer same-counterpart rumors. It compares
Mosaic selection with a recency-only baseline. This is a deterministic engineering benchmark, not a
claim about human memory or psychology.
