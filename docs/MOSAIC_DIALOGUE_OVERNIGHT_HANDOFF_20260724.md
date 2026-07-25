# Mosaic dialogue overnight handoff — 2026-07-24

## Session identity

- Base commit: `a8fa3ae9634afa98cb1c56870475e5874767e757`
- Branch: `codex/v0.2-dialogue-cleanroom-overnight-20260724`
- Worktree: `C:\Users\Jeff\Dagmay-worktrees\mosaic-v02-dialogue-cleanroom-overnight-20260724`
- Source ZIP SHA-256: `654a22c81fa757236ad7f5ff6519d556ba4f0fb40e5f60fc28a352cc11e72b47`

## Intake verification

All 43 substantive handoff files match `CONTENTS_SHA256.txt`. The manifest's
recorded hash for itself is inconsistent: expected
`c1aabb883f8abbbe09285cf79a84eb3be2339ce688851ee4193abef9837450f1`, actual
`ab292f83f7300da4734912817657fd2dbd33accfff9cd09a7ca15e93afb9843c`.
This self-referential packaging defect did not affect any candidate or dossier
file.

## Baseline

- Static verification: PASS, 98 C# files.
- Contract tests: PASS, 81/81.
- Integration harness: PASS, 6/6 scenarios.
- Core, Providers, and RimWorld adapter Release builds: PASS, zero warnings.
- Non-installing package preflight: PASS.
- Baseline package SHA-256:
  `c09a1db447f8c21e5139dd464a6cf1403568a21ca2c9bf9fa4aebe1960cf6e08`.

## Provenance boundary

- Exact upstream commit `df9f4ef799a44a07b1f9d2d67814fb819b2df763`
  was fetched from `https://github.com/craftingmod/RimTalk.git`; its committed
  `LICENSE` is MIT, copyright 2025 Juicy.
- Exact upstream commit `9338c63df05ec8adb287a367e73a3583dd009a14`
  was fetched from the same repository; its committed `LICENSE` is
  CC BY-NC-SA 4.0.
- No upstream source was copied. Both supplied batches remain uncompiled
  clean-room candidates pending adversarial review.

## Progress

### Batch 01

Integrated the provider- and environment-independent dialogue contracts,
bounded context assembler, deterministic prompt planner, owner-scoped
scheduler, and utterance validation boundary. Adversarial review found that
default value-type IDs and undefined enum values could bypass their public
constructors; those inputs now fail closed and have a focused regression test.

- Static verification: PASS, 105 C# files.
- Contract tests: PASS, 90/90.
- Integration harness: PASS, 6/6 scenarios.
- Core, Providers, and RimWorld adapter Release builds: PASS, zero warnings.
- Non-installing package preflight: PASS.
- Package SHA-256:
  `0551309ee9c0c18f3dc191905c5a32e2fbbe828ebdd3423f32aa1edeaac9c35a`.

### Batch 02

Integrated deterministic displayed-utterance admission and witness-scoped
history projection over the existing canonical event ledger. No second memory
store or live game wiring was added. The supplied candidate initially failed
nullable compilation. Review also found that an undefined history view could
fall through to Observer access, and that a missing required recipient payload
field could be interpreted as a recipientless utterance. All three paths now
fail closed with focused regressions.

- Static verification: PASS, 110 C# files.
- Contract tests: PASS, 102/102.
- Integration harness: PASS, 6/6 scenarios.
- Core, Providers, and RimWorld adapter Release builds: PASS, zero warnings.
- Non-installing package preflight: PASS.
- Package SHA-256:
  `a86996faccaca65ec045bc970b90273456bfcba7a83d62e39f9cf2e18741f78a`.

### Backlog priority 1 — strict utterance proposal codec

Added a provider-neutral strict JSON boundary tied to the dispatched request.
It bounds UTF-8 bytes and decoded characters before parsing; rejects duplicate,
missing, unknown, action-bearing, malformed, oversized, control-bearing, or
foreign data; and deterministically encodes the six-field proposal schema.
Runtime-owned utterance identity and generation time remain outside model
output. Focused round-trip and negative fixtures pass.

- Static verification: PASS, 111 C# files.
- Contract tests: PASS, 104/104.
- Integration harness: PASS, 6/6 scenarios.
- Core, Providers, and RimWorld adapter Release builds: PASS, zero warnings.
- Package SHA-256:
  `6d2911fa74424680a02d75deaba3b9b3433a78b0a75fa7fc4148e0cf0170781d`.

Backlog priority 2 is next.
