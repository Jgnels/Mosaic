# RimWorld Track

## Purpose

Messy-world integration and reliability laboratory.

Question:
> Does the mechanism survive a persistent life?

## Environment

Latest preserved diagnostics:
- RimWorld 1.6.4871 rev591
- all DLC including Odyssey
- large mod stack historically including HugsLib, AllowTool, Vehicle Framework/VVE, VTE, RimTalk

## Known active individuals

- Lynx
  - IndividualId `56858408f59744df9db149e6f81a12ef`
  - LineageId `e75a12c58cc9488b91b9427e1db8bc82`
- Schmidt
  - IndividualId `c24d9597421c4bb89135690e31bce238`
  - LineageId `5770263ab3dc4002848383d4e4b011b0`
- Michael
  - IndividualId `652879a6ba8a47598402fcaa18f86635`
  - LineageId `2a4faeb3ea03496e867ad22f262d6e8a`
- Doyle
  - IndividualId `84c50edb336343498248067223f31ad0`
  - LineageId `41ecf1a03e344582bb3f62b55d6f88a4`

Known store ID:
`8bbf40dc132f4d989a212b83755f3c57`

## Milestones

### 0.1E
Restricted Observer foundations.

### 0.1F
Experience → memory → queue → Gemini → validated proposal → mutation → persistence.

### 0.1G
Disclosure-filtered ordinary Mind view.

### 0.1H
Consolidation/queue.

### 0.1I
Salience/reflection admission.

### 0.1J
Social foundations.

J.1 exposed collection-modification bug.

J.2 fixed enumeration but exposed save/journal checkpoint mismatch and read-only safety mode.

### 0.1J.3
Checkpoint recovery:
- verified prefix recovery;
- external-head adoption;
- administrative audit;
- explicit confirmation;
- healthy storage after recovery.

Important caveat:
adopting newer external experience into an older save can preserve history that the current world
timeline no longer reflects.

### 0.1K+
Closure campaign:
- Social-path certification
- Persistence torture
- Failure isolation
- Long-session soak
- Release Candidate

The owner reports current work at 0.1RC.

Repository build/test artifacts after migration are authoritative.

## Freeze rule

RimWorld 0.1 cognitive scope is frozen.

SyntheticLab discoveries graduate into 0.2 only after deliberate review.

## 0.2 relationship-event reference (not yet integrated)

`syntheticlab/src/dagmay_synthetic_lab/rimworld_relationship_events.py` is now the executable
reference contract for a future C# adapter. It covers 24 relationship event kinds, deterministic
first-person rendering, explicit clause provenance, strict labels/details, valence consistency, and
bounded citation counts. It does not alter frozen 0.1 scope and must not be represented as installed
RimWorld code until the actual 0.1RC source is recovered and reconciled.

`syntheticlab/src/dagmay_synthetic_lab/relationship_history_snapshot.py` additionally specifies the
0.2 persistence boundary: identity/lineage binding, full-payload integrity hash, strict versioned
schema, atomic replacement, and exact post-restore selection/dialogue equivalence. It is a reference
contract only, not an installed C# implementation.

## Non-Dagmay instability context

Historical game/system instability included:
- suspected faulty RAM;
- varied BSODs;
- AllowTool collection-modified exception;
- VTE/world-pawn relation issues;
- SmashTools shutdown cleanup behavior.

Do not attribute every crash to Dagmay.
