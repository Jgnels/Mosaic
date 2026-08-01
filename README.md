# Mosaic — Persistent Artificial Characters

Mosaic is an experimental framework and RimWorld research mod for **persistent artificial characters** whose identity, memories, relationships, interpretations, and behavior are grounded in durable state rather than in any particular language model.

> **Foundational invariant:** character continuity belongs to persistent state, not to the model generating the current inference.

`Dagmay` is the historical project name. Existing namespaces, serialized identifiers, paths, hashes, and immutable research artifacts retain that name where renaming could harm compatibility or reproducibility.

Mosaic does **not** pursue or claim machine consciousness, sentience, pseudo-consciousness, or deceptive personhood. See [`research/PERSISTENT_CHARACTER_BOUNDARY.md`](research/PERSISTENT_CHARACTER_BOUNDARY.md).

## Current status

The latest accepted research checkpoint is **Mosaic v44R / 0.3I semantic correction**:

- accepted commit: `89be3f89fdd3802abd92f0b2821dd7ec28cfc0a1`;
- tree: `4b634c0b33b5ee96be04e6242e930ae8ba4f86f2`;
- 104/104 substantive v44 semantic cases reported passing;
- 18/18 integration scenarios reported passing;
- 669 logical contracts at this checkpoint;
- Core, Providers, Tests, IntegrationHarness, and RimWorld 1.6 builds reported at 0 warnings / 0 errors;
- portable full stress reported passing at 5,000 exact chains and 50,000 lifecycle cycles;
- authority, package, adversarial, and GateKit A/B gates reported passing.

This is a **research/offline checkpoint, not a production release**. The next authorized work is repository consolidation and a live RimWorld 0.3 shadow vertical slice. **v45 and later architecture are frozen until that runtime/player-value checkpoint is reviewed.**

See [`research/CANONICAL_CHECKPOINT.md`](research/CANONICAL_CHECKPOINT.md) for the compact authoritative project state.

## Repository map

- `rimworld/0.2-prealpha/` — **active RimWorld development/research source**.
- `rimworld/0.1-closure-candidate/` — **frozen historical/recovery source**, retained temporarily while consolidation decisions are externally reviewed. It is not the active implementation.
- `syntheticlab/` — mixed mechanism-research/reference surface. It contains useful current references **and** substantial historical/pre-pivot research; consolidation is in progress. Do not treat every module as active architecture.
- `research/` — canonical checkpoints, claims, decisions, risks, evidence records, historical ledger, and recovery material.

## Start here

For a new AI/developer session:

1. `research/CANONICAL_CHECKPOINT.md`
2. `AGENTS.md`
3. `research/PERSISTENT_CHARACTER_BOUNDARY.md`
4. task-specific source/tests

Read `research/CURRENT_STATE.md` when historical context is needed; it is a long-form ledger, not the fastest current-state bootstrap.

For development setup, see [`CONTRIBUTING.md`](CONTRIBUTING.md). For a research-level overview, see [`research/EXECUTIVE_SUMMARY.md`](research/EXECUTIVE_SUMMARY.md).

> Chat is the workshop conversation. The repository is the durable project record.
