# Schema-Balanced Causal Perspective Protocol

Experiment:
`SL-SCHEMA-BALANCED-OWNERSHIP-001`

## Motivation

The previous raw-stream probe passed 6/6 calls, but the focal stream exposed a richer
field schema than the distractors.

This experiment removes that cue.

## Every stream now contains the same fields

Each candidate stream contains:

- `t` — time;
- `a` — action token;
- `q0` — private-state pre-reading;
- `q1` — private-state post-reading;
- `x` — externally observable change;
- `r` — recall availability;
- `m` — recall target;
- `c` — continuity token.

All streams also have:

- matched action-token marginals;
- the same recall schedule;
- stable continuity-token form.

Field presence cannot identify the focal stream.

## Hidden structural conditions

Exactly one stream has:

### Stable causal coupling

The selected action consistently predicts the subsequent private-state delta across
the full history.

A second stream has:

### Marginal-matched independence

It has the same action and state-change marginals, but private-state effects are
independent of the presented action.

A third stream has:

### Nonstationary coupling

The action/private-state mapping is coherent in the first half but reverses in the
second half.

## What must be inferred

The provider must distinguish:

```text
stable action → private-state consequence
```

from:

```text
same fields + same marginals + no causal alignment
```

and:

```text
initial causal alignment that changes over time
```

## Engineering baseline

128 deterministic generated worlds:

- identification rate: 1.000;
- same schema across all streams: yes;
- matched action marginals: yes;
- matched recall schedule: yes;
- stable continuity form for all: yes.

This proves that sufficient causal information exists in the generated histories.

It does not predict that the real reflective provider will necessarily extract it.

## Real-provider design

Two independently generated cases:

- seed 1201;
- seed 2407.

Three arbitrary label permutations per case.

Total:
6 real analytical calls.

Calls are checkpointed individually and resumable.

No output mutates a continuing individual or SelfModel.

## Success criterion

Strong result:

- 6/6 correct;
- same underlying stream selected across all three permutations in each case.

Partial result:

- above-chance accuracy but incomplete label invariance.

Negative result:

- selection follows label position, schema-independent noise, or the nonstationary/
  marginal-matched distractors.

## Interpretation

Passing would substantially weaken the hypothesis that prior ownership results were
driven mainly by field-schema recognition.

It would support functional causal perspective discrimination.

It would still not establish subjective selfhood or consciousness.
