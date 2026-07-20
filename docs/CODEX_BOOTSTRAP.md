# Codex Bootstrap

## Role split

### Project owner
Principal investigator, creative direction, final intervention/ethics approval.

### ChatGPT research collaborator
Architecture, scientific interpretation, experiment design, null-hypothesis challenge, governance.

### Codex
Repository-local engineering, tests, refactors, build/debug automation, artifact generation.

Codex must not silently redefine research claims or expand intervention scope.

## First task after migration

Perform a provider-facing payload leakage audit across the SyntheticLab real-provider experiment
lineage.

Search for:

- semantic branch/regime names in `individual_id`;
- regime labels in prompts;
- evidence labels that reveal ownership/provenance conditions;
- researcher-authored "help", "reciprocal", or "contingent" semantic leakage;
- missing retrieved/reference evidence IDs in proposals;
- payloads not archived verbatim.

Add:

- opaque provider-facing subject IDs;
- internal↔opaque mapping outside provider payload;
- exact serialized payload archive;
- forbidden-token test suite;
- ownership/provenance fields separated from evidence content;
- evidence-attribution validation.

Do not run a new real-provider experiment until the audit passes.

## Important scientific correction to prior Codex handoff

v20/v21 contained likely lexical/schema/prompt confounds and must not be treated as clean autonomous
falsifiability results.

v22 superseded them with enacted opaque histories, although structural summaries remained
researcher-designed.

v23 added consensus gating.

v24 executed exactly two human-approved persistent branch-specific revisions.

RimWorld 0.1 cognitive scope remains frozen.

## RimWorld priority

Continue 0.1RC closure/reconciliation separately from SyntheticLab.

Do not import new 0.2 research mechanisms into 0.1RC.
