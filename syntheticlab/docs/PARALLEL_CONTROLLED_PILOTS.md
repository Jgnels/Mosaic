# Parallel Controlled Pilots

Approved path: **C — Parallel controlled pilots**.

The two pilots intentionally answer different questions.

## Pilot R — Real Reflection

Environment:
- Dagmay Hard RichWorld.

Development:
- persistent AdaptiveTrace history.

Intervention:
- one real provider-backed structured reflection.

Controls:
- exact pre-reflection no-reflection branch;
- deterministic fake-provider analytical contract.

Critical isolation rule:
the SelfModel is **not readable by the action policy** in the first pilot.

Therefore the first pilot asks:
> What does a pretrained reflective model propose about a history that was actually developed before reflection?

It does **not** yet ask:
> How does an LLM-authored self-narrative change later behavior?

That feedback experiment remains a separate future intervention.

## Pilot E — External Environment

Environment:
- Farama MiniGrid.

Development:
- LLM-free recency-weighted adaptive substrate.

Mission handling:
- opaque mission token by default;
- no textual mission semantics supplied to an LLM.

The pilot asks:
> Does the developmental mechanism work at all outside a Dagmay-authored world?

It does not test identity migration and does not use a reflective model.

## Non-conflation rule

Do not combine conclusions.

A change in Pilot R cannot establish external-environment generalization.

A change in Pilot E cannot establish an effect of reflection.

Combining real reflection + external environment requires a later preregistration.
