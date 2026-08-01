# Contributing to Mosaic

Mosaic is pre-alpha research software. Contributions should reduce risk or improve observable persistent-character quality without expanding scope unnecessarily.

## Before changing code

Read:

1. `research/CANONICAL_CHECKPOINT.md`
2. `AGENTS.md`
3. `research/PERSISTENT_CHARACTER_BOUNDARY.md`
4. the task-specific source/tests

Do not treat `research/CURRENT_STATE.md` as the fastest startup file; it is the long-form historical ledger.

## Active source

The active RimWorld development tree is:

`rimworld/0.2-prealpha/`

`rimworld/0.1-closure-candidate/` is frozen historical/recovery source and should not receive new feature development.

SyntheticLab contains mixed active reference and historical research. Before extending it, identify the current RimWorld/player problem the experiment is intended to resolve.

## Current development freeze

At the v44R checkpoint, **v45 and later cognition are not authorized**. The next work is consolidation, live 0.3 validation, and player-value evaluation.

## Verification principles

- Prefer semantic tests over headline test counts.
- A named test must exercise the behavior it claims to test.
- Offline evidence is not a substitute for live RimWorld evidence when the claim concerns runtime behavior.
- Generated language quality is evaluated separately from canonical correctness.
- Do not weaken authority boundaries to make a test pass.
- Preserve deterministic/provenance checks that have demonstrated defect-detection value.

Useful current RimWorld verification entry points live under `rimworld/0.2-prealpha/tools/`, including:

- `static_verify.py`
- `build.ps1`
- `run-integration-harness.ps1`
- milestone-specific `verify-*.ps1` scripts
- package/firewall tools

The accepted v44R branch also includes `verify-0.3i-contextual-provisional-admission.ps1`.

Run the smallest relevant gate while developing, then the controlling milestone gate before claiming a candidate is complete.

## Python / SyntheticLab setup

SyntheticLab's current Core research code is primarily Python standard library. The known third-party dependency surface in the current tree is the optional MiniGrid/external-environment path:

- `minigrid>=3.1,<4`
- `gymnasium>=1,<2`

See `syntheticlab/SETUP.md` and `syntheticlab/requirements-external-minigrid.txt`.

## Pull-request/change expectations

A change should state:

- the demonstrated risk or player-visible capability it addresses;
- what authority/state surfaces it changes;
- what tests/runtime evidence support it;
- what it deliberately does **not** claim.

Avoid drive-by documentation generation. Historical evidence and hashes should remain reproducible, but active startup context should stay small.
