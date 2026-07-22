# Mosaic RimWorld 0.2 Pre-Alpha Guidance

This tree is derived from `rimworld/0.1-closure-candidate/` but is a separate development track.
The frozen 0.1 closure tree must remain unchanged.

`Dagmay` namespaces, project names, serialized identifiers, and compatibility paths remain in place.
Mosaic is the user-facing project name.

## Status

- Architecture and contract implementation: Implemented.
- Static verification: Passed in the packaging environment.
- Full C# compilation: Not yet performed for this 0.2 tree.
- Contract tests: Registered but not yet executed for this 0.2 tree.
- RimWorld runtime: Not tested.
- Merge into main/runtime installation: Not authorized until compilation and tests pass.

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

1. Run `python tools/static_verify.py`.
2. Compile Core, Providers, Tests, IntegrationHarness, and the RimWorld adapter.
3. Execute the complete contract-test runner.
4. Resolve all failures before adding another feature slice.
5. Preserve exact logs and update repository state only after verified results exist.
