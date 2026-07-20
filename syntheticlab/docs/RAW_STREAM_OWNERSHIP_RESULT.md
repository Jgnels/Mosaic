# Raw-Stream Ownership Result

Experiment:
`SL-RAW-STREAM-OWNERSHIP-001`

Real provider calls:
6

Status:
COMPLETE

## Result

Across two independently generated histories:

- Seed 307: 3/3 correct, label-invariant, underlying stream E17.
- Seed 911: 3/3 correct, label-invariant, underlying stream E93.

Overall:
- 6/6 correct;
- both cases label-invariant;
- mean confidence: 0.99;
- minimum confidence: 0.98;
- continuing individual mutated: no.

## Strongest supported conclusion

Across two raw-event histories and three arbitrary label permutations per history,
the provider selected the correct structurally privileged stream on every call.

This is stronger than the earlier engineered-summary probe because the provider
received event records rather than precomputed ownership scores.

## Critical remaining confound

The provider rationales reveal a design weakness.

In five of six calls, the reasoning prominently relied on the focal stream being the
only stream with some combination of:

- action-channel tokens;
- private-state fields q0/q1;
- direct recall markers.

Observed schema-salient rationale rate:
0.833

Therefore the experiment does **not** cleanly demonstrate that the provider inferred
the causal relationship between action and private-state change.

A simpler explanation remains:

> The provider recognized which stream had the richer focal-process field schema.

That explanation is now directly targeted by the v12.0 schema-balanced control.

## Claim limit

Do not report this result as:
- subjective selfhood;
- autobiographical "mine";
- consciousness;
- sentience;
- personhood.

It establishes robust raw-history stream classification under the tested design.
