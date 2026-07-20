# Known Risks

## KR-001 — Provider-facing condition-label leakage
Severity: HIGH

Semantic condition names have appeared in provider-facing `individual_id` values.

Examples:
- `RECIPROCAL_CONTINGENT`
- `ONE_WAY_ASSISTANCE`
- `NONCONTINGENT_SIGNALS`

Action:
opaque provider IDs + automated forbidden-token scans.

## KR-002 — v31 source-relation-withheld manipulation failed
Severity: HIGH

The real payload still labeled evidence as prior lived-history evidence.

## KR-003 — Retrieved evidence attribution is incomplete
Severity: HIGH before canonical mutation

Provider rationales sometimes use retrieved/reference context while returned `evidence_ids` cite only
current evidence.

## KR-004 — Reflective-model stochasticity can exceed branch signal
Severity: SCIENTIFIC

## KR-005 — Recursive free-text SelfModel rewriting creates surface drift
Severity: SCIENTIFIC

Lexical divergence can increase while semantics converge.

## KR-006 — Same-model evaluator is not independent ground truth
Severity: SCIENTIFIC

## KR-007 — Structural summaries are researcher-designed abstractions
Severity: SCIENTIFIC

v22+ providers did not independently discover reciprocity from raw opaque time series.

## KR-008 — "Assistance" and "reciprocity" can anthropomorphize intent
Severity: SCIENTIFIC

## KR-009 — Identity continuity is not psychological continuity
Severity: CONCEPTUAL

## KR-010 — Two-store checkpoint mismatch
Severity: ENGINEERING

External identity/history and RimWorld save checkpoints can diverge.

## KR-011 — Laptop hardware instability
Severity: OPERATIONAL

Repeated restarts and Automatic Repair occurred.

Migrate canonical work to desktop.

## KR-012 — Long chat context dilution
Severity: PROJECT MEMORY

Repository-first startup protocol is mandatory.

## KR-013 — Full original research packs not yet committed to GitHub
Severity: PROJECT MEMORY

Known hashes:
- Atlas v2 300-source ZIP:
  `3bc2a47ce5ab11123e5413843e9787783ca28ab6be03396b1201403f94302ec9`
- 30-resource audit ZIP:
  `61fcd5e812c023d730783d4f6e0759f42092a119f18a9c82486dfee11798e2e9`

## KR-014 — Regex claim gates miss semantic overgeneralization
Severity: HIGH before canonical dialogue or state mutation

V2 allowed sparse events to become unsupported personality descriptions ("temper" and
"inconsistent nature"). Future provider dialogue needs typed claims, explicit supporting evidence,
and entailment checks; regex remains only a defense-in-depth layer.

## KR-015 — Deterministic rendering can preserve facts while degrading character voice
Severity: PRODUCT / PLAYER VALUE

V3 removed unsupported claims but exposed ungrammatical legacy summary fragments. A safe renderer
needs typed event semantics, first-person transformation, and style tests that cannot add facts.

## KR-016 — Relationship-history benchmark matches its authored scoring assumptions
Severity: SCIENTIFIC

The first long-history fixture deliberately contrasts high-quality direct events with weak rumors.
Its perfect result validates causal plumbing and failure resistance, not optimal retrieval weights.
Future tests need distribution shifts, retractions, repeated low-grade direct evidence, conflicting
sources, and organic RimWorld event traces.
