# SyntheticLab setup and scope

SyntheticLab is a mixed research/reference surface. Its existence does **not** mean every historical module is active Mosaic architecture.

## Python environment

The current SyntheticLab Core is predominantly Python standard library.

The only identified third-party packages required by the optional external MiniGrid environment path are declared in:

`requirements-external-minigrid.txt`

Install them only when running those external-environment experiments:

```powershell
py -3 -m pip install -r requirements-external-minigrid.txt
```

Known declared versions:

- `minigrid>=3.1,<4`
- `gymnasium>=1,<2`

Most deterministic/offline reference modules do not require those packages.

## Current scope rule

Do not start a new SyntheticLab research direction merely because it is interesting or technically possible.

A new experiment should address at least one of:

1. an observed RimWorld/player problem;
2. a mechanism seriously being considered for graduation into the active character system;
3. a specific safety/reproducibility defect in current architecture.

Large pre-pivot welfare/governance and historical provider-experiment surfaces remain visible during the current external consolidation review. They are candidates for archival/removal from active HEAD, not automatically current dependencies.

The binding project boundary remains `../research/PERSISTENT_CHARACTER_BOUNDARY.md`.
