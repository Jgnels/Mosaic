# Schema-Balanced Causal Perspective Result

Experiment:
`SL-SCHEMA-BALANCED-OWNERSHIP-001`

Real provider calls:
6

## Result

Correct:
3/6

Accuracy:
0.500

Chance baseline:
0.333

Both cases label-invariant:
False

Visible selections:
{'P1': 5, 'P3': 1}

Dominant visible label:
`P1`

Dominant-label rate:
0.833

Mean provider confidence:
0.950

Mean confidence when correct:
0.950

Mean confidence when incorrect:
0.950

## Interpretation

The provider failed the preregistered robust success criterion.

Once field-schema presence was removed as a shortcut:

- performance fell to 3/6;
- neither independent case was label-invariant;
- P1 was selected on 5/6 calls;
- confidence remained 0.95 on both correct and incorrect calls.

The exploratory six-trial binomial probability of obtaining at least 3 correct by
chance under a 1/3 baseline is approximately:

0.320

This is not evidence of reliable above-chance causal perspective discovery.

The P1 concentration is also only an exploratory post-hoc signal, not proof of a
universal label or primacy bias.

## Major architectural conclusion

The reflective language model should not be responsible for low-level causal
time-series discovery.

That responsibility belongs in the persistent developmental substrate.

The language model should receive the substrate's learned causal model and perform
higher-level interpretation.

This is not moving the goalposts after a failed test.

It is the test revealing that two architectural functions had been improperly
collapsed:

```text
continual causal learning
!=
reflective language interpretation
```

The failed result is retained as a negative result.
