# Dedicated Dagmay Host Migration Plan

## Target machine

Unused desktop:

- RTX 3090
- Ryzen 5 5600G
- 16 GB system RAM

Recommended later:

- 32 GB minimum;
- 64 GB preferred for local models + RimWorld + research tooling.

## Role

The desktop becomes the canonical Dagmay laboratory host.

The laptop is not trusted as the canonical host due repeated instability/restarts.

## Preserve before migration

Copy:

- current Dagmay code repository;
- SyntheticLab code/packages;
- RimWorld mod source;
- research docs;
- raw provider artifacts;
- analysis files;
- checkpoints/progress files;
- hashes;
- migration handoffs.

Keep more than one copy.

## Host verification

Install/verify:

- Git;
- Python;
- .NET SDK;
- PowerShell;
- Codex;
- Steam/RimWorld dependencies as needed.

Run offline:

- Python syntax/tests;
- SyntheticLab deterministic tests;
- .NET tests;
- RimWorld static/build verification.

Do not make real Gemini calls until provider-input audit completes.

## Remote workflow

Desktop:
canonical lab host.

Phone:
remote steering/review where supported.

ChatGPT:
research/architecture/scientific collaborator.

Codex:
engineering agent.

## GitHub

Create an empty private repository.

Merge this knowledge-base tree with the canonical codebase.

Commit current code and documentation together only after reconciling:

- actual 0.1RC commit/build status;
- latest SyntheticLab source;
- current raw experiment artifacts.

## First scientific task after migration

Provider-facing payload leakage/provenance audit.

Then minimally rerun the critical causal chain.
