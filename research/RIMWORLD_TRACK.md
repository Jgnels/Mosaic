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

### Recovered closure candidate

The newest recovered source is now canonical at `rimworld/0.1-closure-candidate/` for continued
0.1 closure work. It self-identifies as 0.1K and contains:

- social-path certification;
- 0.1L persistence torture;
- 0.1M failure isolation; and
- the later read-only scheduler safety patch.

The earlier owner report of 0.1RC is retained as historical context but is not independently
versioned in the recovered material. Exact RC status is unconfirmed. Long-session soak remains the
unresolved closure gate unless later evidence is recovered.

Fresh desktop verification on 2026-07-20 passed 42/42 contract tests, all six integration scenarios
(603 assertions), and the RimWorld assembly/package build with zero warnings/errors. The package was
not installed or launched; this does not close the live long-session-soak gate.

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

`syntheticlab/src/dagmay_synthetic_lab/bounded_game_goals.py` defines the 0.2 goal-selection
reference: fixed game goal kinds, environment-confirmed feasibility, evidence IDs, emergency
priority, bounded personality influence, anti-thrashing commitment, safe targets, and deterministic
dialogue. It remains advisory/reference-only until deliberate C# integration review.

## Non-Dagmay instability context

Historical game/system instability included:
- suspected faulty RAM;
- varied BSODs;
- AllowTool collection-modified exception;
- VTE/world-pawn relation issues;
- SmashTools shutdown cleanup behavior.

Do not attribute every crash to Dagmay.

## 0.2 pre-execution reference (not yet integrated)

`syntheticlab/src/dagmay_synthetic_lab/goal_execution_gate.py` defines the final reference boundary
before a future C# job adapter: single-use requests, current revision/feasibility/target/evidence/
reachability revalidation, bounded outcomes, and replan-on-failure. Passing it still does not issue a
job; RimWorld must perform game-native reservation and validation.

`syntheticlab/src/dagmay_synthetic_lab/goal_execution_snapshot.py` persists the single-use execution
ledger across crashes with identity/lineage binding, integrity checks, set invariants, and atomic
replacement. A future C# adapter must preserve equivalent replay protection across save/load.

`syntheticlab/src/dagmay_synthetic_lab/runtime_checkpoint.py` is the composite 0.2 recovery
reference. A future C# save adapter must bind relationship history, goal commitment, execution
authority, identity/lineage, world revision, and checkpoint generation into one atomic unit rather
than saving independently restorable stores. This remains reference code and has not been
integrated into the recovered Observer-only 0.1 C# source.

## 2026-07-26 controlled 0.1 closure result

The Gate 3 long-session soak passed at commit 457f1815d625f7a121f444d972d4880e70c805cb and package SHA-256
d845e5165a4977b3bdc4af98f55fd6c5485970d127411c023b575409b3db676a. Two 60-minute Core + Mosaic, forced-offline blocks completed with a full process
restart. All six samples retained healthy identity, experience, and reflection storage; individual
and lineage continuity matched; provider and local-model counts remained zero; and both final saves
agreed exactly with their external checkpoints. No further owner gameplay is required for the
controlled frozen-0.1 closure.

Release packaging was then hardened and independently certified at commit 21d6bf28138513d06e950cd9604592b2410ff7ec. Static
verification covered 98 C# files; 70/70 contracts and all six integration scenarios with 603
assertions passed; Core, Providers, and the RimWorld adapter built with zero warnings/errors; ten
adversarial firewall packages were rejected; two independent standard Windows checkouts produced
the byte-identical deterministic package SHA-256 5a79db4f3fc2b3306f8ff2fc66f8e4653de1b9b1f68436b9fe5882c4d6ae4ad7.

Full mod-stack, DLC-group, large-colony, and RimTalk compatibility are post-closure work. The
recovered source continues to identify as 0.1K, and no separately versioned historical 0.1RC artifact
is inferred.

## 0.2 dialogue foundation integration candidate

Offline implementation now includes:

- the preserved owner/privacy-isolated dialogue foundation;
- recoverable checkpoint-bound factual dialogue admission;
- an original bounded speech-bubble presenter with play-log fallback;
- a deterministic-fake-only observer adapter seam; and
- directed, bounded storytelling evidence fixtures with exact provenance.

The next gate is a deliberately disposable owner-controlled runtime test of
visual placement, play-log fallback, GameComponent lifecycle, actual social
trigger capture, and save/reload outbox recovery. No real provider, local
model, pawn-control feature, or normal save belongs in that test. Full
mod-stack, RimTalk, DLC-group, performance, and tuning work remains later.

### Owner-operated dialogue retest

The isolated Core + Mosaic retest at commit `c49da81` passed the
GameComponent/runtime-thread correction and primary Bubble path. It live-
verified three enrollments, qualifying opinion/direct-relationship capture,
four bounded experiences/memories, four deterministic Bubble receipts, save
checkpoint generation 2, stable identity/lineage reload, healthy storage,
two restored meaningful reflection items, and no duplicate presentation.

Play-log fallback, full normal mod stack, DLC combinations, RimTalk
coexistence, long-duration soak, broader visual/gameplay tuning, and real
provider behavior remain later test phases.
