# Development Slice 01

Implemented directly on the cleaned recovered authoritative C# source:

- `FactId` strong identifier.
- First-party `TemporalFact` with evidence provenance and supersession without evidence erasure.
- `EvidenceDomain`: Personal / Group / Faction / World.
- Side-effect-free appraisal contract boundary (`AppraisalInput -> AppraisalResult`).
- Common `DecisionTraceEvent` foundation for the Character Why Inspector.
- Three new executable contract tests wired into the existing test runner.

Verification in this environment:
- `python tools/static_verify.py`: run separately and must pass.
- Compilation/tests cannot be executed here because this container does not include the dotnet CLI.

This slice intentionally does NOT vendor Fluid HTN or FAtiMA yet. Those should be pinned only after the authoritative recovery is published and a build-capable machine can immediately compile/test the import.
