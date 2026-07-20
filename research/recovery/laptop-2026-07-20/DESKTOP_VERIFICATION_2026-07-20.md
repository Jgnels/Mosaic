# Desktop Verification — 2026-07-20

## Scope

Source: `rimworld/0.1-closure-candidate/`

Command path: recovered `tools/dev-loop.ps1` in Release configuration, with neither `-Install` nor
`-Launch`.

Environment:

- locally installed .NET SDK 8.0.423 under ignored recovery tooling;
- local RimWorld 1.6 managed assemblies; and
- bundled Python 3 for static verification.

## Results

| Stage | Result |
|---|---|
| Static verification | PASS — 75 C# files |
| Core build | PASS — 0 warnings, 0 errors |
| Providers build | PASS — 0 warnings, 0 errors |
| Contract tests | PASS — 42 executed, 0 failed |
| Integration harness | PASS — 6 scenarios, 603 assertions, 0 failed |
| RimWorld assembly build | PASS — 0 warnings, 0 errors |
| Package build | PASS |
| Repository-wide fail-closed Mosaic offline maintenance | PASS |

Integration scenario assertions:

- LongHistoryContinuityAndPersistence: 135;
- ProviderOutageDurableQueueRecovery: 103;
- CheckpointRollbackForwardRecovery: 12;
- SocialPerspectiveAndPrivacy: 12;
- PersistenceTorture: 238; and
- FailureIsolation: 103.

Package SHA-256:

`F32E33D7432E2A564433C7B881874BBAD3982B45DD3C3704C6414D9850CEECB2`

## Limits

- The package was not installed.
- RimWorld was not launched.
- No save, LocalLow store, identity archive, journal, or reflection store was read or mutated by this
  build loop.
- This does not constitute a live gameplay test or the unresolved long-session soak.

The repository-wide offline maintenance suite also passed after import with both provider credential
variables explicitly cleared. It rechecked the persistent-character boundary, character-quality
suite, objective-surface audit (16/16), claim grounding, Python compilation, and seeded research
regressions. It made zero provider calls.
