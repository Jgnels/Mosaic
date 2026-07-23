# Mosaic RimWorld 0.2 Pre-Alpha

This is the separate 0.2 architecture-development tree.

It is derived from the recovered and locally committed
`rimworld/0.1-closure-candidate/` baseline. The 0.1 closure source remains frozen and must not be
overwritten by this work.

## Included cumulative development

- strong temporal fact identity and provenance;
- pure appraisal contracts;
- decision-stage tracing;
- durable identity/environment-binding separation;
- portable external-event proposals;
- action-origin attribution;
- evidence-grounded read-only character context;
- optional mod capabilities and deterministic event admission;
- lifecycle-bearing story events;
- trait/influence provenance;
- milestone-based long-running project memory;
- contribution attribution;
- 18 new executable contract tests.

## Verification status

```text
Static verification:      PASS (95 C# files checked)
Core/Providers compile:   PASS
Contract tests:           PASS (65 executed; 0 failed)
Integration harness:      PASS (6 scenarios; 0 failed)
Gate 3 offline soak:      PASS (short + long deterministic presets)
RimWorld adapter compile: PASS (Release; 0 warnings; 0 errors)
RimWorld runtime:         NOT YET TESTED
```

Compilation and offline verification pass. This is still not RimWorld runtime or save-compatibility certification.

Read:

1. repository-root `AGENTS.md`;
2. repository-root `research/CURRENT_STATE.md`;
3. repository-root `research/PERSISTENT_CHARACTER_BOUNDARY.md`;
4. this tree's `AGENTS.md`;
5. repository-root `MOSAIC_MASTER_HANDOFF.md`;
6. repository-root `docs/MOSAIC_V02_ARCHITECTURE.md`.
