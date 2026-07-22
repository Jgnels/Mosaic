# Mosaic Local/GitHub/Recovery Reconciliation

## Inputs reviewed

- local core/research package;
- local code package;
- local Git reports;
- GitHub `Jgnels/Mosaic` main branch;
- recovered authoritative closure source;
- cumulative Development Slice 06;
- local-recovery inventory.

## Local repository status

The uploaded Git report showed:

```text
## main...origin/main [ahead 1]
```

The working tree had no modified, staged, deleted, or untracked files.

Local HEAD:

```text
8ad0eb2fcc038235cf6f41925bba502eb794aac4
Recover and reconcile RimWorld closure source
```

GitHub main HEAD during reconciliation:

```text
37f40a4b813af4ff756e89366f2a5d85ad6aaf6c
Record atomic runtime recovery boundary
```

Therefore the local repository is not an unsaved partial workspace. It contains one clean committed
recovery change that had not yet been pushed.

## Local source versus cumulative Slice 06

Compared relative to the local `rimworld/0.1-closure-candidate/` root:

- local files: 147;
- Slice 06 files: 180;
- common paths: 145;
- byte-identical common files: 142;
- common files changed by Slice 06: 3;
- local-only provenance/guidance files: 2;
- Slice-only architecture/test/audit files: 35.

The three common changes were:

- `Dagmay.Core/Contracts/Identifiers.cs` — adds `FactId`;
- `Dagmay.Tests/Program.cs` — registers 18 new contract tests;
- `AGENTS.md` — incompatible guidance for the frozen 0.1 directory.

The local `AGENTS.md`, `AGENTS.legacy-dagmay.md`, and `RECOVERY_PROVENANCE.md` are preserved in the
0.1 tree. Slice 06 is therefore applied to a new 0.2 tree rather than overwriting 0.1.

## local-recovery result

The 1.067 GB local-recovery directory was dominated by:

- a portable .NET 8 SDK/runtime;
- the 362.59 MB emergency rescue archive;
- the 16.09 MB Dagmay archive;
- Microsoft reference documentation and generated/support files.

The loose-source extraction contained no additional `.cs`, `.csproj`, `.sln`, or Python source
outside the already reconciled archives. Keep local-recovery privately as a safety backup, but do not
upload it to GitHub.

## Decision

- Preserve current repository research, SyntheticLab, tools, and governance.
- Preserve `rimworld/0.1-closure-candidate/` unchanged.
- Push the existing local recovery commit through an import branch.
- Add `rimworld/0.2-prealpha/` and additive checkpoint documentation.
- Compile and test before merging.
