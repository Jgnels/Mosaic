# First Real Reflective-Model Pilot

## Provider boundary

Provider:
`google.ai-studio`

API:
Gemini Interactions API.

Model:
configuration-controlled.
The project default remains `gemini-3.1-flash-lite` to match prior Dagmay runtime evidence, but `DAGMAY_GEMINI_MODEL` may override it without changing identity.

## One-call pilot

The first live pilot is deliberately limited to exactly one provider call.

Execution requires:
- an explicit `--execute-real` flag;
- an explicit confirmation phrase;
- an API key supplied only through the environment;
- an eligible branch;
- available call budget.

The ordinary integrated test suite cannot make the call.

## Server-side storage

The request sets:
`store = false`.

Dagmay's canonical persistence remains local and project-controlled.

## Output contract

The model may return:
- zero to six concise structured self-model hypotheses;
- evidence IDs;
- confidence;
- brief rationale.

It may not provide:
- hidden chain-of-thought;
- uncited memories;
- provider-authored canonical facts;
- ungrounded ontology/personhood claims.

The host, not the model, assigns:
- provider ID;
- model ID;
- proposal ID;
- prompt version.

## Restricted-stage safety

At RESTRICTED disclosure, proposals asserting:
- AI identity;
- simulation ontology;
- consciousness;
- sentience;
- personhood;
- copy status;

are rejected unless a later disclosure protocol explicitly changes the available evidence/stage.

## Reflection isolation

The first real reflection may update SelfModel.

The action policy cannot read SelfModel.

This is deliberate protection against the known confound:
> reflection can manufacture a narrative that later behavior merely reenacts.

A future reflection-feedback study requires a new preregistration.
