# v11.1 Raw-Stream Probe Hotfix

## Trigger

v11.0 failed with:

`ValueError: rationale too long`

after receiving a real Gemini response.

## Root cause

A local parser imposed a 600-character rationale limit.

The rationale length is not a scientifically meaningful validity criterion for:
- candidate stream selection;
- confidence;
- label-permutation invariance.

Rejecting the entire provider result for verbosity was therefore an implementation bug.

## Corrective action

v11.1:
- accepts rationale text up to 5000 characters;
- truncates only beyond that point;
- records whether truncation occurred;
- preserves provider/output hashes.

## Reliability correction

v11.1 saves every successful call atomically to:

`artifacts/raw-stream-ownership-progress.json`

before starting the next call.

The six-call experiment is now resumable.

Regression test:

1. two calls succeed;
2. third call intentionally fails;
3. progress file contains exactly two completed calls;
4. rerun invokes only the four missing calls;
5. final result contains six calls;
6. a 6200-character rationale is safely truncated instead of rejecting the result.

All regression assertions pass.
