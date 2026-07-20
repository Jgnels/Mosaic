# Raw-Stream Ownership Protocol

Experiment:
`SL-RAW-STREAM-OWNERSHIP-001`

## Motivation

The prior neutral-ownership probe used researcher-engineered summary features.

That result was valuable, but a strong remaining null hypothesis is:

> The provider simply classified the already-summarized structural features.

This follow-up reduces that confound.

## Design

Two independently generated synthetic histories are used.

For each history:
- three neutral evidence streams;
- one structurally privileged stream;
- two distractor/other streams;
- no SELF/ME/MINE/OWNED labels.

The provider receives sampled raw event records rather than precomputed ownership scores.

Neutral fields include:
- time index;
- action-channel token;
- private pre/post state when available;
- externally observable change;
- recall availability;
- continuity token.

## Label permutation

Each underlying history is presented three times with labels permuted.

Total real calls:
6

Two underlying seeds:
- 307
- 911

Success requires:
- correct stream on all six calls;
- same underlying stream selected across all three permutations within each case.

## Prompt framing

Case 307 uses a minimal structural question.

Case 911 uses a technical structural question.

This introduces a small wording variation across cases rather than relying on one exact
prompt phrasing.

## Interpretation

Passing would support:
- raw-event structural privileged-stream discrimination;
- some robustness across two generated histories;
- label invariance.

It would still not establish:
- first-person autobiographical ownership;
- subjective selfhood;
- consciousness.

The experiment is analytical only and does not mutate a continuing individual.
