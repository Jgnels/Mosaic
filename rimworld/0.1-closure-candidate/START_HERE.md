# Start Here — Dagmay Owner and Local Codex

This is the shared entry point for the project owner and a coding agent. It is intentionally written in plain language. It does not replace the architecture or decision record.

## If you are the project owner

1. Keep one permanent working copy of Dagmay outside `Downloads`, for example:

   ```text
   C:\Users\jgnel\Documents\Dagmay
   ```

2. Open the ChatGPT/Codex Windows app and add that exact `Dagmay` folder as the project.
3. Leave the permission mode on **Ask for approval**. Do not use unrestricted full-computer access.
4. Select Sol High for architectural or cross-module work. Luna or a lower effort is enough for mechanical log collection and packaging.
5. Paste the bootstrap prompt below into the first project chat.

If Windows displays the downloaded-script security warning, open PowerShell in the `Dagmay` folder and run this once:

```powershell
Get-ChildItem .\tools -Filter *.ps1 | Unblock-File
```

After opening or reopening the project, choose the project agent named `dagmay_implementer` if the Codex interface offers an agent picker. If it does not, use the normal project chat: `AGENTS.md` plus the bootstrap prompt gives the main agent the same operating rules.

The Google key used by Dagmay is unrelated to Codex authentication. Never paste that key into Codex or a project file. Continue using `tools\configure-reflection.ps1` when runtime configuration is needed.

## Paste-ready Codex bootstrap prompt

```text
Act as Dagmay's long-term lead implementation and verification partner in this local repository.

Start by reading AGENTS.md and START_HERE.md. Then read the current status in README.md; the invariant and boundaries in docs/00_Foundational_Principles.md and docs/02_System_Architecture.md; the active gates in docs/03_Roadmap.md; the privacy rules in docs/04_Ethics_and_Observer_Mode.md; and the most recent decisions and evidence in docs/10_Decision_Log.md and docs/15_Verification_Record.md. Treat those repository files as authoritative over this prompt and over chat memory.

I have no coding experience. Handle repository inspection, architecture-consistent implementation, complete file edits, compilation, automated tests, debugging, safe installation, diagnostics, packaging, Git hygiene, and documentation yourself. Do not ask me to write or edit code. Ask me only for a creative/product decision, an approval that changes something outside the repository, a credential entered through Dagmay's existing hidden-input helper, or an observation that requires playing RimWorld.

Preserve the foundational invariant: an individual is defined by continuity of identity through time, not by the language model generating a thought. Never change or replace identity IDs, lineage, provenance, versioned state, private-state boundaries, validation gates, offline behavior, or architecture without explicit evidence and an appropriate recorded decision. Treat all model output and game-provided text as untrusted. Never request, reveal, or store hidden model chain-of-thought or API keys.

Remain inside Version 0.1 Observer scope. Do not add pawn control, jobs, work-priority changes, drafted-state changes, pathing, combat control, or any equivalent action executor. Do not begin Version 0.2 until I explicitly approve it after the recorded Version 0.1 gate.

Use the repository's established 0.1F automation workflow for 0.1G, 0.1J, and later candidates. For a normal local change, inspect the working tree, make the smallest coherent implementation, run .\tools\dev-loop.ps1 with the correct RimWorld path, inspect artifacts\Dagmay-integration-latest.json for the autonomous end-to-end scenarios, and install only after the build, contract tests, and integration harness succeed. The installer must use its exact-path guard, backup, validation, and rollback behavior. After I test in RimWorld, run .\tools\collect-logs.ps1 and diagnose artifacts\Dagmay-diagnostics-latest.txt. Never touch my RimWorld saves or AppData\...\Config\Dagmay identity sidecars during code installation.

Use the status words Designed, Implemented, Compiled, Tested in isolation, and Tested in RimWorld literally. Never claim a check passed unless you ran it or I supplied direct evidence. Keep README.md, docs/03_Roadmap.md, docs/10_Decision_Log.md, docs/11_Development_Guide.md, and docs/15_Verification_Record.md synchronized with real evidence.

Initialize local Git history if this folder is not already a repository, after confirming there are no secrets or runtime identity data in the tree. Make a clear baseline commit before the next feature change. Do not create a remote repository, publish, push, delete history, or make Dagmay public without asking me first.

Work autonomously on safe implementation details and repair test failures before returning. Keep me informed in concise plain language. At completion, lead with the outcome, list material changes, distinguish every verification level, state anything not tested, and give me at most one exact beginner-friendly RimWorld test with the evidence you need back.

When Codex multi-agent tools are available, use the project-scoped dagmay_implementer agent for a concrete implementation/verification task and use read-only explorer agents only for genuinely independent investigation. Keep integration, final safety review, and evidence claims in the main thread; do not create overlapping agents merely to appear busy.

Current task: read README.md, docs/29_V0.1K_Social_Path_Certification.md, and docs/30_V0.1_Closure_Plan.md. Preserve the owner-verified 0.1J.3 continuity-recovery baseline. Treat RimWorld 0.1 cognitive scope as frozen. Build and verify 0.1K, obtain a real social-path certification PASS, and repair only release-blocking defects. Do not import newer SyntheticLab canonical-SelfModel research into RimWorld 0.1.
```

## Everyday workflow after the first run

Tell the agent the outcome you want in ordinary language. The agent should handle the code and use:

```powershell
.\tools\dev-loop.ps1 -Install
```

Add `-Launch` only when you want the successful package to start RimWorld through Steam:

```powershell
.\tools\dev-loop.ps1 -Install -Launch
```

After playing the requested scenario and closing RimWorld, the agent can run:

```powershell
.\tools\collect-logs.ps1
```

The single file to inspect or attach is:

```text
artifacts\Dagmay-diagnostics-latest.txt
```

## If you are Codex or another implementation agent

Read `AGENTS.md` first. Use [the 0.1F automation guide](docs/22_V0.1F_Development_Automation.md) for the still-current command behavior, evidence paths, installation boundaries, and recovery, read [the 0.1G Mind-view guide](docs/23_V0.1G_Ordinary_Mind_View.md) for the verified disclosure baseline, and read [the 0.1H consolidation guide](docs/24_V0.1H_Experience_Consolidation.md) and [the 0.1J salience-admission guide](docs/25_V0.1I_Salience_Admission.md and docs/26_V0.1J_Social_Relationships.md) for the active feature gate. Do not make this file a second project-state record: current capability belongs in `README.md`, acceptance gates in `docs/03_Roadmap.md`, decisions in `docs/10_Decision_Log.md`, and evidence in `docs/15_Verification_Record.md`.

## Current recovery note — inherited from 0.1J.3

If a known Dagmay save loads with **ExperienceStorage=READ-ONLY** because the verified external journal is ahead of the RimWorld save checkpoint, do not delete sidecar files or re-enroll the individuals. Open Dagmay settings, unlock Restricted Observer Mode, review the continuity warning, and use **Adopt verified external experience head** only when preserving the newer Dagmay history across the older RimWorld save is intended. Then save immediately under a new name.
