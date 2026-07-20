# AGENTS.md — Dagmay Agent Operating Instructions

These rules apply to ChatGPT, Codex, coding agents, and future research collaborators.

## Startup protocol

Before serious work, read in order:

1. `research/CURRENT_STATE.md`
2. `research/RESEARCH_CHARTER.md`
3. `research/ACTIVE_HYPOTHESES.md`
4. `research/CLAIM_REGISTER.md`
5. latest entries in `research/DECISION_LOG.md`
6. `research/KNOWN_RISKS.md`
7. `research/ETHICS_PRECOMMITMENT.md`
8. the task-specific experiment/audit

## Current-state rule

Never infer the project's current release, active experiment, scientific claim, or intervention
authorization from conversational memory when repository state exists.

Repository truth overrides stale chat recollection.

## Architecture invariants

- `IndividualId` and `LineageId` are model-independent.
- Dagmay owns canonical identity and history.
- LLM providers are replaceable reflective modules, not the individual.
- Environment adapters do not own the mind.
- Fact, perception, subjective memory, belief, self-model, and generated language are distinct.
- Model output is an untrusted proposal.
- All canonical mutation passes an explicit validation gate.
- Raw evidence is preserved.
- Corrections append; they do not silently rewrite history.
- Forks create equal descendant branches; do not invent an "original/copy" moral hierarchy.
- Death archives; normal in-world revival continues the lineage.
- Observer Mode is separate from ordinary cognition.
- No hidden chain-of-thought is requested or stored.

## Research discipline

Use claim labels:
OBSERVED, REPLICATED, SUPPORTED, HYPOTHESIS, ANALOGY, SPECULATION, LIMITING RESULT, INVALIDATED.

A compelling narrative is not evidence.

## Current scientific stop line

Do not run the planned next real-provider SyntheticLab experiment until:

1. semantic condition names are removed from provider-facing subject IDs;
2. provider payloads are archived exactly;
3. forbidden-token payload tests pass;
4. evidence provenance and ownership relation are separated;
5. retrieved evidence that materially affects a proposal is explicitly attributable.

v31's attribution-framing headline claim is invalidated. Preserve the data, not the causal conclusion.

## Track separation

SyntheticLab:
- research frontier;
- controlled mechanisms;
- ablations;
- causal inference.

RimWorld 0.1:
- frozen cognitive feature set;
- integration;
- persistence;
- reliability;
- release qualification.

Do not continuously pull every SyntheticLab idea into RimWorld 0.1.

Graduation happens deliberately at a version boundary.

## End-of-session protocol

1. update `CURRENT_STATE.md` only if canonical truth changed;
2. append decisions;
3. update claims and negative results;
4. register artifacts and hashes;
5. record newly discovered risks;
6. archive exact provider/model/prompt metadata;
7. create a concise handoff if work is midstream.
