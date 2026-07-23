# Mosaic RimWorld 0.2 Pre-Alpha Guidance

This tree is derived from `rimworld/0.1-closure-candidate/` but is a separate development track.
The frozen 0.1 closure tree must remain unchanged.

`Dagmay` namespaces, project names, serialized identifiers, and compatibility paths remain in place.
Mosaic is the user-facing project name.

## Status

- Architecture and contract implementation: Implemented.
- Static verification: Passed (95 C# files on the Gate 3 offline-readiness branch).
- Full C# compilation: Passed for Core, Providers, Tests, IntegrationHarness, and the Release
  RimWorld adapter against installed RimWorld 1.6 assemblies.
- Contract tests: Passed (65 executed; 0 failed).
- Integration harness: Passed (six default scenarios; 0 failed).
- Gate 3 offline soak: Short and long deterministic presets passed; this is not a live RimWorld soak.
- RimWorld runtime: Not tested.
- Runtime installation: Not performed by offline readiness work.

## Binding boundaries

- The repository-root `AGENTS.md` and persistent-character boundary are authoritative.
- Model output and external-mod output are untrusted proposals.
- No hidden chain-of-thought is requested or stored.
- Observer/debug/prompt/UI operations must not mutate cognition.
- External integrations fail soft and must not own durable identity.
- Third-party CLR objects must not be stored in durable Mosaic state.
- No pawn-control, job-issuance, or autonomous action code may be added without a separate
  owner-approved design gate and execution-safety review.

## Required next work

1. Keep the complete offline verification chain green.
2. Run the controlled owner test in `../../docs/MOSAIC_GATE3_RUNTIME_TEST_PLAN.md`.
3. Preserve exact logs and evidence manifests without private saves or credentials.
4. Do not begin Gate 4 work until the live Gate 3 criteria are supported by actual RimWorld evidence.
