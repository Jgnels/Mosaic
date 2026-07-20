# SyntheticLab v31 provider-payload audit

Date: 2026-07-19  
Scope: imported `Dagmay_SyntheticLab_v31.0.zip` source  
Method: static inspection and offline tests only; no provider calls were made.

## Status

**STOP LINE REMAINS ACTIVE.** The imported v31 provider-facing experiments are historical, reproducible source, not approved templates for another real-provider run.

## Confirmed findings

### 1. Condition labels leak through provider-facing subject identifiers

`BeliefRevisionProvider._build_input_text()` serializes `BeliefRevisionRequest.individual_id` directly into `SUBJECT DATA`. Several experiment runners construct that identifier from `branch_id` or other condition-bearing values. In v31's attribution-framing runner, for example, the identifier is built as:

`branch_id + "-HISTORY-RETRIEVAL-" + trajectory`

The branch vocabulary includes labels such as `RECIPROCAL_CONTINGENT`, `ONE_WAY_ASSISTANCE`, and `NONCONTINGENT_SIGNALS`. A provider can therefore infer the experimental condition from a field that should have been opaque.

Relevant locations include:

- `src/dagmay_synthetic_lab/belief_revision_provider.py:95-110`
- `src/dagmay_synthetic_lab/own_history_attribution_framing_ablation.py:455-460`
- analogous `individual_id` construction in the autobiographical-retrieval, branch-history, crossed-history, common-future, enacted-history, and related provider runners.

### 2. v31 did not implement the stated attribution-withheld manipulation

`own_history_attribution_framing_ablation.py` prefixes retrieved summaries with:

`PRIOR LIVED-HISTORY EVIDENCE (retrieved context; this is not a new event):`

This language tells the provider that the material is lived-history evidence. It does not match the intended neutral framing described in the later analysis. The canonical invalidation of v31's attribution-framing claim is therefore supported by the source code.

Relevant location:

- `src/dagmay_synthetic_lab/own_history_attribution_framing_ablation.py:190-205`

### 3. Exact provider payloads are not durably archived at the shared boundary

The provider classes build request dictionaries and pass them to transports, and test transports retain calls in memory. The shared production boundary does not itself write an immutable, secret-free record of each exact provider-facing payload. Reconstructing a request later therefore depends on experiment-specific checkpoints and code version rather than a single hardened payload ledger.

### 4. Evidence ownership, experimental condition, and provider-visible evidence are not cleanly separated

Evidence is primarily passed as dictionaries containing identifiers and summaries. Ownership/attribution semantics are embedded in human-readable summary text, while branch/condition metadata can appear in subject identifiers. This makes it difficult to prove which semantics were intentionally visible to the provider.

### 5. Citation validation is set-membership validation, not provenance-role validation

`belief_revision_provider.py` rejects evidence IDs outside the supplied set, which is useful. It does not require a proposal whose rationale uses retrieved history to cite a retrieved-history evidence ID, nor does it validate ownership/role semantics. This matches the provenance weakness recorded after v29/v30.

## Required remediation before another real-provider experiment

1. Generate provider-facing opaque subject IDs independently of branch names, regimes, trajectories, or conditions.
2. Define a typed provider-evidence envelope separating content, provenance, ownership relation, temporal role, and experimental condition.
3. Keep experimental condition metadata host-side unless its disclosure is an explicit preregistered manipulation.
4. Archive the exact secret-free request payload and its canonical hash before transport.
5. Add forbidden-token scans over the fully serialized provider input, including identifiers and labels.
6. Require cited evidence roles appropriate to claims that rely on retrieved/reference history.
7. Add offline regression tests that fail on condition leakage or incorrect attribution framing.
8. Revalidate only the minimum critical provider-dependent causal chain after the controls pass.

## Validation performed during import

- Archive SHA-256 matched the canonical v31 hash.
- No API-key-like secret was found in the supplied source or handoff archives by pattern scan.
- All Python files passed offline bytecode compilation using an external cache directory.
- The SyntheticLab v0.6 offline research suite passed with 16 seeds.
- No Gemini/API key was present in the validation environment and no real-provider call was made.

