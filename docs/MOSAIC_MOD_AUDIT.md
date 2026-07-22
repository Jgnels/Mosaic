# Mosaic Mod-Stack Audit — Consolidated

## Reuse and architecture references

- **FAtiMA:** reuse appraisal ideas; reject whole-runtime mutation authority.
- **RimWorld Multiplayer:** identity, deterministic serialization, reference discipline.
- **Fluid HTN:** preferred planner candidate; do not reinvent HTN fundamentals.
- **ReGoap:** secondary planning reference.
- **Graphiti:** temporal validity, provenance, and supersession concepts only.
- **Yarn Spinner / ink:** authored narrative complements, not cognition authority.
- **BehaviorTree.CPP:** decision-stage trace inspiration.
- **Performance Fish:** optimization and compatibility benchmark.
- **PsychSim / Generative Agents:** concepts and evaluation references, not runtime imports.

## Actual mod-stack decisions

### RimTalk

Use supported context-hook and custom-variable APIs. Prefer appended/injected sections rather than
overrides. Mosaic context must be bounded, evidence-grounded, read-only, exception-safe, and fast.

### RimHUD

Do not duplicate ordinary health, needs, traits, or skills. Prefer a soft Mosaic-specific widget and
Character Why entry point.

### Interaction Bubbles

Downstream presentation only. Bubble text is not canonical evidence.

### Common Sense / Pick Up And Haul

They transform execution. Their jobs and inserted toils are not automatically Mosaic-authored goals
or learned personality.

### Hospitality

Guest, visitor, and colonist are social roles, not durable identity.

### Vehicle Framework

Vehicle/caravan/world-pawn/despawn transitions change environment binding, not identity.

### Vanilla Expanded Framework

Optional capability boundary. Namespace/API churn must disable only the affected adapter.

### Vanilla Traits Expanded

Trait code can directly interrupt jobs or force behavior. Preserve external trait provenance and do
not misclassify it as Mosaic-learned character development.

### RimCities

Quests are lifecycle-bearing story threads with stable identity and outcomes.

### Roads of the Rim

Long-running multi-leg projects use milestones and contribution attribution, not per-tick memories.

## Not yet fully source-audited

- Vanilla Social Interactions Expanded;
- Go Explore;
- Perspective Shift;
- the exact installed maintained Roads of the Rim build.
