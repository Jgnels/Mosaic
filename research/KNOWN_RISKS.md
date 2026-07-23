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

Mitigation status: the SyntheticLab 0.2 reference supplies an atomic composite character checkpoint.
The RimWorld 0.2 pre-alpha C# path now performs deterministic manifest preflight, exact identity and
reflection store/generation matching, verified-prefix experience recovery, read-only mutation
blocking, and safe sidecar path construction. Adversarial offline tests pass.

Live status (2026-07-23): REPRODUCED. A first healthy load performed post-load reflection
synchronization that advanced the external reflection store from generation 2 to generation 3
without advancing the RimWorld save. A second load of the unchanged save correctly rejected the
ahead primary and entered reflection read-only mode using the exact-generation backup. Identity and
experience isolation and the separate controlled-corruption fail-closed path passed, but Gate 3
failed.

Mitigation update (2026-07-23): IMPLEMENTED OFFLINE, LIVE RETEST OPEN. Reflection sidecar writes and
provider dispatch now wait for the first matching RimWorld save after load; pending-commit recovery
also moved into that checkpoint. The new two-load regression and the complete offline chain pass.
A minimal live retest verified startup only because the graphics surface could not be inspected;
no save workaround was used. Complete the owner-assisted two-load sequence before closing this
risk.

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

## KR-017 — Goal commitment may become sticky under regime change
Severity: PRODUCT / ENGINEERING

The first continuity fixture uses near-tied stationary work goals plus brief emergencies. Its low
regret does not establish safe behavior when task utility changes permanently, feasibility becomes
stale, or commitments depend on multi-step prerequisites. Add nonstationary and stale-sensor tests
before RimWorld job control.

## KR-018 — Freshness gates trust environment clocks and revisions
Severity: INTEGRATION / SAFETY

The selector rejects internally stale, future, and revision-mismatched candidates but cannot detect
an adapter that labels incorrect observations as current. RimWorld integration needs monotonic tick
checks, revision ownership, and independent pre-execution feasibility validation.

## KR-019 — Recovered 0.1K manifest predates final closure patch
Severity: PROVENANCE / RELEASE

`rimworld/0.1-closure-candidate/MANIFEST_0.1K.json` does not describe the final recovered tree. It
predates `ReflectionStorageSafetyPolicy.cs` and the final revision of
`DagmayIdentityGameComponent.cs`. Use the recovery manifest and Git history for the imported tree;
do not use the stale manifest to certify a release package.

## KR-020 — Exact RimWorld 0.1RC designation is unconfirmed
Severity: RELEASE / PROJECT MEMORY

The recovered source self-identifies as 0.1K while incorporating later closure work. No separately
versioned 0.1RC source or commit was found. Do not infer release-candidate completion from the prior
conversation label. Long-session soak and a fresh desktop build/live verification remain open.

## KR-021 — Recovered runtime state is private and checkpoint-sensitive
Severity: DATA INTEGRITY / PRIVACY

The emergency archive contains saves, identity and reflection stores, journals, logs, and config.
These remain outside Git. Do not restore HourTest with the later Nelson journal; preserve each save
and external store as a matched recovery set and validate hashes/checkpoints before any live use.
