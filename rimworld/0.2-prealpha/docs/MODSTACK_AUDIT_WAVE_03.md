# Mod-Stack Audit Wave 03

## RimHUD
Audited source: `Jaxe-Dev/RimHUD` (RimWorld 1.6 lineage).

Findings:
- integrates into the inspect pane and can render as a floating HUD;
- supports custom widgets/layout presets for other mods;
- patches inspect-pane/UI paths with Harmony.

Mosaic decision:
- do not duplicate ordinary pawn health/skills/needs/traits;
- Character Why should specialize in Mosaic-only causal state;
- prefer soft RimHUD widget integration if its public API is sufficient;
- all UI/context providers must remain observationally pure.

## Interaction Bubbles
Audited source: `Jaxe-Dev/Bubbles` (RimWorld 1.6 lineage).

Finding:
- presentation-only design patches `Verse.PlayLog.Add` and forwards the `LogEntry` to the bubble renderer.

Mosaic decision:
- do not treat Bubbles output as canonical evidence;
- avoid duplicate speech presentation when RimTalk/Bubbles already owns display;
- presentation subscribes downstream of validated content and never becomes state authority.

## Common Sense
Audited source: `catgirlfighter/RimWorld_CommonSense`, current 1.6 source tree.

Finding:
- can replace/transform existing job-driver toil sequences; e.g. `JobDriver_SocialRelax.MakeNewToils`
  inserts cleaning, changes joy timing, and changes ingestion/execution details.

Mosaic decision:
- execution-policy changes are not automatically new Mosaic intentions;
- this directly motivated `ActionOriginKind` / `ActionAttribution`.

## Pick Up And Haul
Audited source: `Mehni/PickUpAndHaul`.

Findings:
- adds custom `WorkGiver_HaulToInventory`;
- creates `HaulToInventory` jobs and queues multiple things/storage targets;
- changes hauling execution/efficiency;
- includes compatibility handling for other mods.

Mosaic decision:
- an optimized haul job is external execution policy unless a separate Mosaic commitment explicitly caused the higher-level objective;
- external job provenance and Mosaic-authored goal provenance remain distinct.

## Architecture produced

```text
RimWorld / another mod creates or transforms a job
        ↓
ActionAttribution
        ├── GameNative
        ├── ExternalModExecution
        ├── PlayerCommand
        └── MosaicCommitment
                ↓
Only this branch can claim Mosaic-authored intention
```

Read-only consumers:

```text
Mosaic canonical state
       ↓
IReadOnlyCharacterContextProvider
       ↓
CharacterContextPacket
       ↓
RimTalk / RimHUD / Character Why / diagnostics
```
