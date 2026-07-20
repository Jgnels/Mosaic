# External Research Memory Architecture

Durable external project files are the continuity layer for this collaboration.

They do not modify model weights.

They create a persistent source of truth future ChatGPT/Codex sessions can read.

## Recommended hierarchy

```text
/research
    CURRENT_STATE.md
    RESEARCH_CHARTER.md
    ACTIVE_HYPOTHESES.md
    CLAIM_REGISTER.md
    DECISION_LOG.md
    KNOWN_RISKS.md
    ETHICS_PRECOMMITMENT.md
    NEGATIVE_RESULTS.md
    MODEL_MODULE_VERSION_REGISTRY.md
    THIRD_PARTY_LICENSE_REGISTER.md
    EXPERIMENT_REGISTRY.csv
    SOURCE_REGISTRY.csv
    /preregistrations
    /results
    /audits
    /atlas
```

## Startup protocol

1. AGENTS.md
2. CURRENT_STATE
3. CHARTER
4. HYPOTHESES
5. CLAIMS
6. recent DECISIONS
7. RISKS
8. task-specific files

## End-of-session protocol

1. update current state only if truth changed;
2. append decisions;
3. update claims;
4. record negative results;
5. register artifacts/hashes;
6. produce concise handoff.

> The chat is the laboratory conversation.  
> The repository is the laboratory notebook.
