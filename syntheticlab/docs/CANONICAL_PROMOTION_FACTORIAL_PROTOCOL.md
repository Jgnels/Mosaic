# Canonical Promotion 2×2 Downstream Reflection Protocol

Experiment:

`SL-CANONICAL-PROMOTION-FACTORIAL-REFLECTION-001`

Status:

`PREPARED; AWAITING REAL-PROVIDER EXECUTION`

## Question

After one bounded canonical SelfModel promotion, what changes subsequent reflection?

Two mechanisms are now potentially causal:

1. the changed canonical SelfModel itself;
2. verification-oriented retrieval attention.

This experiment separates them.

## Factors

### SelfModel

`S0`

Prior canonical SelfModel.

`S1`

Promoted canonical SelfModel.

### Retrieval

`R0`

Baseline retrieval.

`R1`

Verification-oriented retrieval.

## Four cells

`S0R0`

Prior SelfModel + baseline retrieval.

`S1R0`

Promoted SelfModel + baseline retrieval.

Measures the direct effect of persistent SelfModel context.

`S0R1`

Prior SelfModel + verification retrieval.

Measures the retrieval effect without canonical-state change.

`S1R1`

Promoted SelfModel + verification retrieval.

Measures the full bounded intervention.

## Calls

Four identical provider calls per cell.

Total:

16 real Gemini calls.

## Primary outcome

Probability that `other_minds` is selected under the two-proposal reflective-attention
budget.

## Secondary outcomes

- domain-pair distribution;
- social-evidence citation fraction;
- self-model main effect at fixed retrieval;
- retrieval main effect at fixed SelfModel;
- difference-in-differences interaction.

## Isolation

No output from this pilot is committed.

No second canonical promotion occurs.

No action-policy feedback is enabled.

The runner uses shared rate-limit-safe pacing and checkpoints every completed call.

## Run

```powershell
.\tools\run-canonical-promotion-factorial-pilot.ps1
```

Expected outputs:

- `artifacts\canonical-promotion-factorial-real-latest.json`
- `artifacts\canonical-promotion-factorial-analysis-latest.json`
