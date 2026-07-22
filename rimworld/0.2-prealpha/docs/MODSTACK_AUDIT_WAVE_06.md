# Mod-Stack Audit Wave 06

## Roads of the Rim

Deep source audit performed against the canonical historical source lineage
`LocoNeko/RoadsOfTheRim` and used as an architectural reference for the
maintained continuation in the user's load order.

Key findings:

### Construction is a persistent world project
`RoadConstructionSite` is a WorldObject that persists:
- road type;
- construction-chain/last-leg reference;
- nearby settlements;
- optional helping faction;
- helper start tick;
- helper contribution amount;
- helper work rate.

### Progress is continuous and multi-leg
`WorldObjectComp_ConstructionSite` tracks:
- total costs;
- remaining resources/work;
- percentage done;
- repeated progress updates;
- per-leg completion;
- movement of workers to the next leg.

### The final leg has a meaningful terminal event
On the final leg:
- the road is added to the world grid;
- pathing is recalculated;
- a positive "road built" letter is emitted;
- the construction site is removed;
- allied faction help is marked finished.

### Allied factions can materially contribute
Faction help can advance the project independently of a player caravan.

## Mosaic decision

Do NOT turn every progress tick or resource decrement into autobiographical evidence.

Instead model long-running projects as:

```text
StoryEventDescriptor
    +
StoryEventProgressSnapshot
```

and record milestone crossings or terminal outcomes.

Examples worth remembering:
- construction started;
- 25/50/75% milestone if narratively relevant;
- allied faction joined/contributed materially;
- major setback/failure;
- road completed.

Examples NOT worth remembering:
- every 100-tick work increment;
- progress-bar redraw;
- cost-cache recalculation;
- every resource decrement.

This audit directly motivated:
- `StoryContributionKind`
- `StoryContribution`
- `StoryEventProgressSnapshot`
- `IStoryMilestonePolicy`
- `ThresholdStoryMilestonePolicy`

The same abstraction can later support:
- multi-stage building projects;
- faction aid campaigns;
- research/ritual milestones;
- long quests;
- recovery from colony disasters.

## Perspective Shift

A canonical public source repository was not cleanly resolved in the available GitHub index during this wave. No source-audit completion claim is made.

Architectural rule remains unchanged:
player camera/viewpoint changes are observer state and must not mutate pawn perspective, memory, or belief state.
