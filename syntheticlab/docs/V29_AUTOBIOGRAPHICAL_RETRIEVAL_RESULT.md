# v29 Autobiographical Retrieval Result

Condition:

`TRUE_OWN_HISTORY_RETRIEVAL`

Six shadow trajectories per branch received the same four later shared-experience epochs.

At every epoch, each branch also received structural evidence reconstructed from its own verified
original 384-episode enacted history.

## Result

SelfModel-only v28 baseline:

- between-branch distance: `0.2417061946`
- signal / trajectory variance: `0.5502432681`
- permutation p: `0.1378431078`
- classification: `SEMANTIC_CONVERGENCE`

True own-history retrieval:

- between-branch distance: `0.3390676939`
- signal / trajectory variance: `0.9604190747`
- permutation p: `0.0069996500`
- convergence ratio: `0.6745897772`
- classification: `SEMANTIC_CONVERGENCE`

Status:

`OWN_HISTORY_RETRIEVAL_EFFECT_DETECTABLE_BUT_OVERLAPPING`

## Supported conclusion

Making verified branch-local prior history available to reflection causally increased persistent
history-dependent differentiation relative to the SelfModel-only baseline.

The grouping by historical branch became statistically detectable.

However, average within-branch trajectory dispersion remained slightly larger than between-branch
centroid distance, so the historical effect still overlapped strongly with stochastic trajectory
variation.

## Important control still missing

v29 compared:

- no old-history retrieval;
against
- truthful own-history retrieval.

It did not establish that the effect is specific to autobiographical alignment.

Any sufficiently distinctive historical context might alter later reflective trajectories.

v30 adds a crossed foreign-history control.

## Provenance weakness discovered

Across the v29 real run, provider rationale text visibly used retrieved historical statistics, but
the proposal `evidence_ids` did not cite the `-RETRIEVED` evidence IDs.

Because all v29 changes were shadow-only, this did not contaminate canonical state.

Before any retrieval-conditioned canonical mutation, the evidence-attribution contract must be
strengthened so retrieved evidence that influences a proposal is explicitly represented in proposal
provenance.
