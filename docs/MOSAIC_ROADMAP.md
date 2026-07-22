# Mosaic Roadmap

## Gate 1 — Import safely

- keep 0.1 closure candidate unchanged;
- create/push `mosaic-v0.2-prealpha-import`;
- add the independent 0.2 tree and checkpoint docs;
- verify no unexpected deletions.

## Gate 2 — Compile and test

- static verification;
- Core/Providers/Tests/IntegrationHarness compile;
- RimWorld adapter compile against installed assemblies;
- all 60 contract tests;
- integration harness;
- preserve exact output.

No new feature slice while this gate is red.

## Gate 3 — Runtime foundation

- minimal mod list;
- controlled copied save;
- identity creation and persistence;
- observer purity;
- save/load;
- unresolved binding recovery;
- corruption failure;
- long-session soak.

## Gate 4 — Compatibility groups

1. RimTalk, RimHUD, Interaction Bubbles;
2. Hospitality, Vehicle Framework;
3. Common Sense, Pick Up And Haul;
4. Vanilla Expanded Framework and Traits;
5. world/faction/quest mods;
6. remaining visual/combat/QoL mods.

## Gate 5 — RimTalk bridge

Read-only bounded context through supported APIs, absent-mod failure, exception/timeout handling,
and duplicate-presentation prevention.

## Gate 6 — Character Why

Evidence-backed causal explanation with no hidden chain-of-thought and no observer mutation.

## Gate 7 — Bounded goals/planning

Fixed goal vocabulary, evidence/feasibility checks, critical-needs override, commitment continuity,
Fluid HTN integration, and current-world pre-execution validation.
