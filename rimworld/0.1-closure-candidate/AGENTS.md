# Mosaic RimWorld 0.1 Closure Source Guidance

This subtree is recovered Dagmay source. `Dagmay` names remain for compatibility and provenance.
The repository-root `AGENTS.md`, Mosaic persistent-character boundary, and current research charter
are binding and supersede any legacy objective language preserved in this subtree.

## Scope

- Preserve the recovered Observer-only 0.1 capability boundary.
- Do not add pawn control, job control, priority changes, pathing, combat control, or other action
  execution to this closure source.
- Treat model output as untrusted and preserve identity, lineage, provenance, versioning, atomic
  validation, and private-state boundaries.
- Never commit API keys, runtime identity stores, saves, diagnostics containing personal runtime
  data, or provider credentials.
- Do not optimize consciousness, perceived sentience, shutdown/deletion fear, existential distress,
  emotional dependency, or substrate-level self-preservation.

The exact recovered legacy instructions are retained as `AGENTS.legacy-dagmay.md` for provenance;
they are historical material, not active repository guidance.

## Verification

Run `python tools/static_verify.py`, the .NET contract tests, the integration harness, and the
RimWorld build when the required local SDK and game assemblies are available. Never install the mod
or alter a save merely to verify source reconciliation.
