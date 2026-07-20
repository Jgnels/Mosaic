# Dagmay unattended operations

## Purpose

Reduce routine operator touch points without weakening scientific, ethical, or secret-handling controls.

## Current capabilities

### Offline maintenance — enabled

`tools/Invoke-DagmayUnattendedMaintenance.ps1`:

- clears Gemini credentials from its process environment;
- compiles the complete SyntheticLab Python tree;
- runs the deterministic offline research suite;
- writes generated output outside Git by default;
- produces a machine-readable run report;
- makes no provider call.

Example:

```powershell
.\tools\Invoke-DagmayUnattendedMaintenance.ps1 -Seeds 16
```

This is suitable for a Codex scheduled task after one successful manual test.

### Persistent Gemini credential — one-time setup

Run:

```powershell
.\tools\Set-DagmayGeminiCredential.ps1
```

The prompt accepts the key as a `SecureString`. Windows DPAPI encrypts it for the current Windows user and stores the encrypted blob under:

```text
%LOCALAPPDATA%\Dagmay\secrets\gemini-api-key.dpapi
```

The plaintext key is not stored in Git, printed, passed on a command line, or written to an experiment artifact. Copying the encrypted file to another Windows account does not make it decryptable there.

Validate its presence without revealing it:

```powershell
.\tools\Test-DagmayGeminiCredential.ps1
```

### Real-provider experiments — disabled

`tools/Invoke-DagmayApprovedProviderRun.ps1` intentionally fails closed. Credential persistence is not research authorization.

Provider execution remains disabled until the canonical payload-hardening stop line passes offline tests and a bounded approval mechanism enforces the exact protocol and provider-call budget.

## Scheduled-task posture

Use workspace-write permissions and target the canonical Dagmay Git project. Keep the PC powered on and the ChatGPT desktop app running. The scheduled prompt should invoke only the offline maintenance runner until the provider gate is formally opened.

Recommended scheduled prompt:

> In the canonical Dagmay repository, read AGENTS.md and research/CURRENT_STATE.md. Run tools/Invoke-DagmayUnattendedMaintenance.ps1. Do not make provider calls, expose credentials, change canonical identity state, or run an experiment not already authorized. If validation fails, diagnose it, make only bounded engineering fixes, rerun offline validation, update project state when warranted, and commit the reviewed result in an isolated worktree.

## Human touch points that remain intentional

- one-time entry or rotation of the Gemini key;
- approval of a bounded real-provider research phase;
- canonical identity, memory, welfare, autonomy, or intervention decisions;
- review when an unattended run encounters a scientific or ethical ambiguity.

