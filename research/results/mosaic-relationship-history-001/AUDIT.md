# Mosaic Relationship History 001 Audit

Verdict: `MECHANISM_PASS_GENERALIZATION_UNTESTED`

The deterministic 128-seed benchmark passed all registered fixture checks with zero provider calls
and zero canonical mutation. Mosaic retrieved all four older direct events, no recent rumors, and a
balanced two-positive/two-negative set. Recency-only returned recent rumors and none of the direct
events.

The result is intentionally limited: the fixture and scoring function share the same provenance
assumptions. It demonstrates that the mechanism implements those assumptions correctly. It does not
show that the weights are optimal or that performance transfers to organic RimWorld histories.
