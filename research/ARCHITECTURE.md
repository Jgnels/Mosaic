# Dagmay Architecture

## Core principle

Dagmay is a persistent cognitive research platform.

The LLM is one replaceable reflective subsystem above durable identity, provenance, learned
structure, social history, and developmental experience.

## Recommended module map

```text
IdentityKernel                   [Dagmay-native]
CanonicalEvidenceLedger         [Dagmay-native]
PerceptionModel                 [Dagmay-native]
ProvenanceDAG                   [Dagmay-native]

EventSegmenter
  ├─ fixed baseline
  ├─ causal baseline
  ├─ prediction-error baseline
  └─ hybrid

EpisodicMemory                   [Dagmay-native]
TemporalBeliefGraph             [Graphiti + Leolani inspired]
DirectedRelationshipModel       [Dagmay-native + social evidence]

AppraisalEngine interface
  ├─ Dagmay baseline
  ├─ GAMYGDALA-inspired
  ├─ FAtiMA-inspired
  ├─ Psi-inspired
  └─ learned/LLM experimental

ActiveEmotionEpisodes           [FAtiMA/GAMYGDALA inspired]

DevelopmentalLearner interface
  ├─ transition/frequency baseline
  ├─ replay variants
  ├─ continual-learning models
  └─ future world model

ProceduralSkillStore            [Voyager inspired]
TheoryOfMindModel               [PsychSim inspired]
ReflectiveLLM                   [provider replaceable]
ProposalValidationGate          [Dagmay-native]

EnvironmentAdapter
  ├─ SyntheticLab
  ├─ RimWorld
  ├─ MiniGrid
  ├─ AI2-THOR
  └─ future physical/virtual worlds
```

## Major rule

External research code should normally enter through an experiment adapter, not become a core
dependency.

## Data distinctions

Keep separate:
1. canonical event;
2. perceived event;
3. subjective memory;
4. semantic belief;
5. self-model;
6. relationship state;
7. affect/appraisal;
8. procedural skill;
9. developmental predictive state;
10. generated language.

## Memory is plural

At least six separable functions:
1. canonical evidence;
2. subjective episodic memory;
3. semantic knowledge/beliefs;
4. working/context memory;
5. procedural skill memory;
6. learned predictive/developmental state.

Retrieval indexes are derived infrastructure, not canonical memory.

## Developmental grounding

Existing adult-initialized individuals can possess:

> knowledge without biography.

Future cohorts should distinguish:

```text
MicroPerception
    ↓
StatisticalTrace / PredictiveModel / Affordance
    ↓
DevelopmentalPrior
    ↓
Episodic / autobiographical structures
    ↓
Reflective LLM
```

Do not fabricate childhood.

Compare:
- adult-initialized;
- developmental-grounded;
- memory-only;
- shuffled-history.

## Event pipeline

```text
environment event
    ↓
bounded perceived event
    ↓
canonical evidence link
    ↓
subjective memory
    ↓
belief/appraisal/relationship proposals
    ↓
validation
    ↓
canonical state
```

## Affect dimensions

- valence;
- arousal;
- threat/safety;
- agency/control;
- attachment/affiliation;
- certainty/confusion;
- social standing.

Each bounded from -1 to 1.

## Persistence

Use:
- append-only provenance ledger;
- coordinated checksummed snapshots;
- explicit save/world checkpoint;
- versioned schema migrations;
- backups;
- no silent authority guessing on mismatches.

## Model replacement

Provider/model changes are interventions.

Identity continuity and psychological/behavioral continuity are not identical.

Record provider, model, prompt, mechanism, and transition metadata.
