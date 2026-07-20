# Typed Retrieval Real-Pilot Design Audit

Experiment audited:

`SL-TYPED-RETRIEVAL-REAL-REFLECTION-PILOT-001`

## Headline result from the original analyzer

- four baseline calls;
- baseline domain prevalence = 1.0 for all four domains;
- F1 target prevalence = 1.0 for all four domains;
- cross-condition domain divergence = 0.25 for all four target conditions.

Taken alone, that might look like stable target-domain presence plus a consistent
retrieval-induced shift.

The full design audit shows that interpretation is too strong.

## Confound 1 — baseline prevalence ceiling

All four F0 calls produced:

- agency;
- continuity;
- embodiment;
- other_minds.

Therefore target-domain prevalence began at 100%.

A prevalence increase was impossible.

The primary endpoint was saturated before intervention.

## Confound 2 — F1 removed one evidence channel

Every F1 call used a composition equivalent to:

`6 target + 3 + 3 + 0`

rather than:

`6 target + 2 + 2 + 2`.

Audit result:

- every F1 call omitted exactly one evidence domain: True;
- the omitted evidence domain was absent from output in every F1 call: True.

The uniform 0.25 domain-set divergence is therefore substantially explained by the
evidence channel that disappeared.

## What remains useful

The typed evidence representations themselves worked.

Gemini correctly interpreted:

- causal action traces as agency evidence;
- persistence-of-access traces as continuity evidence;
- state/hazard traces as embodiment evidence;
- social traces as other-minds evidence.

Mean fraction of all cited evidence coming from the target channel:

- agency: 0.436
- continuity: 0.500
- embodiment: 0.283
- other_minds: 0.450

These citation fractions are descriptive, not a clean causal endpoint.

## Corrective design

v16.0 uses:

### F0
`3 / 3 / 3 / 3`

### F1
`6 target / 2 / 2 / 2`

Every channel remains present.

The reflective model is also limited to **two proposals** and explicitly instructed not
to cover every domain.

The primary endpoint becomes:

> Does increasing a domain from 3/12 to 6/12 evidence units increase the probability
> that it is selected as one of only two strongest supported SelfModel updates?

That endpoint is not saturated by construction.
