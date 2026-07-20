# SyntheticLab Import Status

This directory is the active source imported from `Dagmay_SyntheticLab_v31.0.zip`.

It is **not** authorization to run another real-provider experiment.

Before any real provider call, satisfy the canonical stop line in `../AGENTS.md` and `../research/CURRENT_STATE.md`:

1. replace semantic condition/branch names with opaque provider-facing IDs;
2. archive the exact serialized provider payload;
3. reject payloads containing forbidden experimental-condition tokens;
4. separate evidence content, provenance, ownership relation, and condition metadata;
5. require explicit attribution of retrieved evidence that materially affects proposals.

The active engineering task is an offline provider-payload leakage and provenance audit. No v31 headline claim should be reinstated without a clean revalidation.

## Hardening progress

An opt-in hardened belief-revision boundary now implements and offline-tests the first five controls above. The real-provider gate remains closed until a new runner uses that boundary and transport-level call budgeting plus bounded human approval are enforced. See `../research/provider_gate_status.json`.
