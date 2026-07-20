# FAtiMA Toolkit Source-Code Audit — Dagmay Summary

**Audit date:** 2026-07-18  
**Repository:** `GAIPS/FAtiMA-Toolkit`  
**Audited revision:** `56b7cbd992f953cfe21a7b12cb1a0e6cdf6ccf9f`  
**Observed license:** Apache-2.0; reverify before reuse

## Executive decision

Do not embed the full FAtiMA Toolkit as Dagmay's cognitive core.

Conflicts:
- autobiographical records can be updated/forgotten;
- appraisal can mutate storage/knowledge;
- KB lacks Dagmay-style temporal provenance;
- some social identifiers regenerate after deserialization;
- identity semantics are not `IndividualId`/`LineageId`;
- modules are too tightly coupled for Dagmay's research architecture.

## High-value concepts

1. rule-driven appraisal;
2. optional OCC emotion labels;
3. transient emotion threshold/decay;
4. candidate-action generation separated from execution;
5. perspective-aware beliefs with certainty;
6. derived Social Importance;
7. Comme il Faut social exchanges;
8. explainable causal tooling.

## Recommended integration

```text
FAtiMA research/code
    ↓
isolated comparative prototype
    ↓
small Dagmay-native contracts
    ↓
provenance-preserving adapter/selective port
    ↓
Dagmay validation gate
```

## Appraisal contract

Input:
- IndividualId;
- PerceivedEventId/MemoryId;
- grounded facts;
- active goals;
- current beliefs;
- relationships;
- needs.

Output:
- desirability;
- praiseworthiness;
- expectedness/surprise;
- controllability/agency;
- social significance;
- certainty;
- goal-progress deltas;
- evidence IDs.

Appraisal returns a proposal.

It does not record events, rewrite beliefs, or mutate identity.

## Memory decision

Reject FAtiMA AutobiographicMemory as canonical storage.

## Belief decision

Adapt perspective + certainty.

Build Dagmay-native temporal provenance.

## Social decision

Use Social Importance only as derived salience.

Keep canonical directed relationships.

## Comparative Appraisal Lab

A — current Dagmay baseline  
B — minimal FAtiMA-inspired rules  
C — GAMYGDALA-inspired appraisal  
D — LLM-only negative/control baseline
