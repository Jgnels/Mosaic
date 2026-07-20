# Autobiographical Retrieval Causality Ablation

Experiment:

`SL-AUTOBIOGRAPHICAL-RETRIEVAL-CAUSALITY-ABLATION-001`

Status:

`PREPARED`

## Question

Does truthful retrieval of verified branch-local prior lived history preserve a detectable
historical signature under later shared experience better than the SelfModel-only architecture?

## Why

v28 found:

- strong semantic convergence under four shared future epochs;
- no statistically resolved residual branch-history grouping.

The active canonical SelfModel may be an overly compressed carrier of developmental history.

The source-code audits and research atlas repeatedly suggested separating:

- current self-belief;
- autobiographical evidence;
- temporal/provenance structure;
- retrieval attention.

v29 tests that mechanism directly.

## Conditions

### Baseline

Existing v28 condition:

`SELF_MODEL_ONLY`

Six trajectories per branch.

No old lived-history evidence was reintroduced during later reflection.

### Experimental

New condition:

`TRUE_OWN_HISTORY_RETRIEVAL`

Six trajectories per branch.

At every shared future epoch, reflection receives:

1. the same current-epoch structural evidence used in v28;
2. four structural evidence summaries deterministically reconstructed from that branch's own
   original 384-episode enacted history.

Each retrieved item is explicitly labeled:

`PRIOR LIVED-HISTORY EVIDENCE (retrieved context; this is not a new event)`

## Provenance verification

Before any provider call:

- the original enacted branch is deterministically reconstructed;
- its hash-chained journal head must exactly match the previously recorded real experiment;
- retrieval is blocked on mismatch.

No foreign history is presented as self-history.

No memory content is fabricated or rewritten.

## Calls

Six trajectories per branch.

Four shared future epochs.

Revision calls:

`3 × 6 × 4 = 72`

Final blinded semantic evaluations:

`18`

Total real calls:

`90`

## Primary comparison

Compare TRUE_OWN_HISTORY_RETRIEVAL against the existing v28 SELF_MODEL_ONLY baseline.

Measure:

- final between-branch centroid distance;
- within-branch trajectory dispersion;
- branch signal / trajectory variance;
- 20,000-permutation pseudo-F p-value;
- start-to-final semantic convergence ratio.

### Retrieval outcome

`OWN_HISTORY_RETRIEVAL_PRESERVES_BRANCH_SIGNAL`

requires:

- permutation p <= 0.05;
- signal / trajectory variance >= 1.0;
- final between-branch distance greater than the SelfModel-only baseline.

If p <= 0.05 but overlap remains high:

`OWN_HISTORY_RETRIEVAL_EFFECT_DETECTABLE_BUT_OVERLAPPING`

Otherwise:

`OWN_HISTORY_RETRIEVAL_DOES_NOT_RESOLVE_BRANCH_SIGNAL`

## Interpretation boundary

A positive result would show that truthful autobiographical retrieval causally preserves
history-dependent differentiation better than a compressed active SelfModel alone.

It would not establish consciousness, personhood, or an irreducible identity essence.

## Isolation

All updates are shadow-only.

No canonical SelfModel mutation.

No memory mutation.

No action-policy feedback.

No false self-attribution of another branch's history.

Run:

```powershell
.\tools\run-autobiographical-retrieval-ablation.ps1
```

Outputs:

- `artifacts\autobiographical-retrieval-ablation-real-latest.json`
- `artifacts\autobiographical-retrieval-ablation-analysis-latest.json`
