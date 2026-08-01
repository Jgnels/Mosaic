# Mosaic Canonical Checkpoint

**Status:** ACCEPTED RESEARCH CHECKPOINT
**Date:** 2026-08-01
**Current accepted milestone:** v44R / Mosaic 0.3I semantic correction
**Not a production release.**

This file is the smallest first-read source of current project truth. For history, evidence detail, claims, and prior checkpoints, follow the linked canonical ledgers rather than expanding this file indefinitely.

## Exact accepted identity

- Commit: `89be3f89fdd3802abd92f0b2821dd7ec28cfc0a1`
- Tree: `4b634c0b33b5ee96be04e6242e930ae8ba4f86f2`
- Parent: `5c279b6c864808a67f411b2efbdc26267947466e`
- Repair branch: `codex/mosaic-v44r-semantic-correction`
- Defective preserved ancestor: `a52fad6fe06efa778fa733075610d7a7e1db1d44`

Repair lineage:

1. `5229b0f95c00e4734b8cf718416c16832808d50a` — substantive Core semantic repair.
2. `5c279b6c864808a67f411b2efbdc26267947466e` — substantive v44 semantic suite and defects found by that suite.
3. `89be3f89fdd3802abd92f0b2821dd7ec28cfc0a1` — substantive integration and final certification candidate.

## Accepted v44R evidence level

Reported final certification:

- v44 semantic C#: **104/104 PASS**;
- inherited + v44 logical contracts: **565 + 104 = 669**;
- integration scenarios: **18/18 PASS**;
- new scenario #18: **28 substantive assertions**;
- static gate: **190 C# source files**, substantive v44 cases/scenario present;
- Core, Providers, Tests, IntegrationHarness, RimWorld 1.6 builds: **0 warnings / 0 errors**;
- portable smoke: **PASS**;
- portable full stress: **PASS** — 5,000 exact chains; 50,000 lifecycle cycles; maximum active 32; two 1,048,576-byte replay filters;
- full stress A/B aggregate SHA-256: `3804838985a4187d75a959825560497a20076bd75a083275ce700efb5eaf1f8b`;
- GateKit self-test: **PASS**;
- GateKit A/B package SHA-256: `6165ed652b2f414579d6541abdc20931cb0c1ea61c23927e944d901ff98d7537`;
- GateKit evidence SHA-256: `00d9ad7ac2459247003bdfb9b27784525c23d0eafcd1b0aef59740ae2f538dda`;
- package firewall: **9 entries, 0 direct adaptations**;
- adversarial firewall: **1 valid accepted / 19 rejected**;
- non-installing RimWorld preflight: **PASS**;
- reported final certification SHA-256: `f2eae8e32d58ab1b63903b3de430ed7d35a3e4fae34f209528f37543e8560d62`.

Independent source review verified that the repaired 104-case suite no longer aliases all names to one generic assertion body: named cases exercise distinct trusted-chain, tamper, provenance, authority, replay, capacity, lifecycle, privacy, determinism, and state-guard behaviors. Integration #18 is a substantive end-to-end Core scenario rather than a count-only placeholder.

The full stress/build/package commands were executed in the Codex development environment; this repository checkpoint records their reported evidence and exact identities rather than claiming they were independently rerun in every reviewing environment.

## Authority boundary at v44R

v44 is **session-local provisional only**.

It must not provide:

- durable/canonical promotion;
- persistence, restore, or snapshot authority;
- provider authority;
- planner, job, or pawn authority;
- player-knowledge authority.

It binds trusted observed-display/dialogue evidence and the exact v43 proposal chain, applies bounded deterministic provisional overlays, maintains bounded lifecycle/replay state, and leaves canonical affect/relationship state unchanged.

## Active working surfaces

- **Active RimWorld source:** `rimworld/0.2-prealpha/`.
- **Frozen historical/recovery source:** `rimworld/0.1-closure-candidate/` — not current implementation; temporarily retained in active HEAD until consolidation review is complete.
- **SyntheticLab:** mixed current reference mechanisms + substantial historical/pre-pivot research. Do not infer active architectural scope from directory size or existence.
- **Historical ledger:** `research/CURRENT_STATE.md`.

## Current project constraint

The project has accumulated architecture and offline certification faster than equivalent end-to-end live/player-value evidence. Phase-0 repository hygiene and external source-level review are complete enough to proceed to measurement, but the immediate goal remains **not another cognition milestone**.

`v45` and later architecture are frozen until semantic-ingress measurement, live validation, player-visible evaluation of continuity, contextual appropriateness, distinctiveness, naturalness, repetition, and memorability, and an architecture review establish that later complexity earns its cost.

## Next authorized action

**Experiment 0A semantic-ingress/opportunity telemetry only. No new cognition.**

Measure current Mosaic social triggers, pair-linked Thought memories, pair-linked PlayLog social interactions, Tales involving enrolled pawns, dialogue preparation/presentation outcomes, unique and repeated pairs, prior same-pair history depth, and cross-source overlap. The telemetry is bounded and read-only: no canonical writes, dialogue changes, provider calls, HistoryEvent hook, pawn/job authority, or v41-v44 runtime wiring.

See `research/SEMANTIC_INGRESS_AND_EPISTEMIC_MODEL.md` for the binding epistemic model, vanilla semantic-source map, experiment constraints, and evidence-dependent branch. v41 is preserved for one conditional equal-budget retrieval ablation rather than automatically wired or deleted. v45 remains frozen.

## Binding objective

> Build causally coherent, persistent, robust, believable, efficient, and enjoyable artificial characters whose continuity belongs to durable state, not to the model.

See next:

1. `research/PERSISTENT_CHARACTER_BOUNDARY.md`
2. `AGENTS.md`
3. task-specific source/tests
4. `research/KNOWN_RISKS.md`, `research/CLAIM_REGISTER.md`, and recent `research/DECISION_LOG.md` when relevant
5. `research/CURRENT_STATE.md` for historical context
