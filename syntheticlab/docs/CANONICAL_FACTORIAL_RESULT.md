# Canonical SelfModel × Retrieval Factorial Result

Experiment:

`SL-CANONICAL-PROMOTION-FACTORIAL-REFLECTION-001`

Real Gemini calls:

16

## Cell results

| Condition | SelfModel | Retrieval | other_minds selected |
|---|---|---|---:|
| S0R0 | prior | baseline | 0/4 |
| S1R0 | promoted | baseline | 0/4 |
| S0R1 | prior | verification | 1/4 |
| S1R1 | promoted | verification | 4/4 |

Difference-in-differences interaction:

`0.75`

## Main effects

SelfModel effect under baseline retrieval:

`0.00`

SelfModel effect under verification retrieval:

`0.75`

Retrieval effect under prior SelfModel:

`0.25`

Retrieval effect under promoted SelfModel:

`1.00`

## Small-sample exact tests

Two-sided Fisher exact p-values:

- S0 vs S1 at R0: 1.0000
- S0 vs S1 at R1: 0.1429
- R0 vs R1 at S0: 1.0000
- R0 vs R1 at S1: 0.0286

Only the R0→R1 comparison inside S1 reaches a conventional 0.05 threshold in this
very small pilot.

## Interpretation

The promoted canonical SelfModel did not force `other_minds` into reflection when the
retrieved evidence was unrelated.

`S0R0` and `S1R0` were identical at the domain-selection level.

Verification retrieval alone had a weak effect:

`0/4 -> 1/4`.

The combination was much stronger:

`S1R1 = 4/4`.

The best current functional interpretation is:

> The promoted canonical SelfModel behaves like a context-sensitive interpretive prior.
> It becomes causally relevant when compatible independent evidence enters attention,
> rather than intruding regardless of evidence.

This is preferable to a trivial self-confirmation loop, but it is not yet enough to
establish healthy belief revision.

## Next risk

A context-sensitive prior can still become pathologically sticky.

The next experiment therefore asks:

> When later evidence weakens the strongest scope claims of the promoted hypothesis,
> will the system qualify or downweight the belief, or defend it automatically?

No second canonical update is allowed during that test.
