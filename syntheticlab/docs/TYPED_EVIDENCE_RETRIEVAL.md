# Typed Evidence Retrieval Fabric

v15.0 replaces the single homogeneous memory-retrieval heuristic with domain-specific
structural evidence channels.

## Channels

### Agency

`AGENCY_CAUSAL_TRACE`

Derived from observed action → consequence records.

### Continuity

`CONTINUITY_PERSISTENCE_TRACE`

Derived from objective persistence of access:

- an older record was encoded at an earlier step;
- it remains retrievable at the current step;
- a later record is also accessible in the same memory store.

Continuity is no longer defined merely as:

> memory is old

### Embodiment

`EMBODIMENT_STATE_TRACE`

Derived from accessible private-state changes and hazard exposure.

### Other minds

`SOCIAL_RELATION_TRACE`

Derived from counterpart, hint, help-given, and help-received events.

## Baseline retrieval

F0 uses a deliberately channel-balanced evidence set.

With 12 evidence units:

- 3 agency;
- 3 continuity;
- 3 embodiment;
- 3 other_minds.

## Functional SelfIndex retrieval

F1 raises the target channel to 6/12 evidence units while retaining mixed-channel
evidence.

This preserves bounded attention rather than creating a one-topic monopoly.

## 64-seed synthetic validation

Every domain:

- baseline target rate: 0.25;
- F1 target rate: 0.50;
- target-rate effect: +0.25;
- nonzero retrieval divergence: 100% of seeds.

The typed fabric therefore fixes the v14 saturation problem where agency/embodiment
sometimes produced identical F0 and F1 evidence sets.
