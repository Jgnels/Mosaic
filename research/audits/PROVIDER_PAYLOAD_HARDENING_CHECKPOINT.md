# Provider-payload hardening checkpoint

Date: 2026-07-20  
Status: engineering controls implemented and passing offline; real-provider gate remains closed.

## Implemented

- deterministic opaque provider-facing subject IDs derived from internal IDs and an experiment namespace;
- typed evidence envelopes separating evidence content, provenance ID, ownership relation, and temporal role;
- forbidden-token scanning over the final serialized provider request;
- exact, secret-free, hash-addressed provider-request archives written atomically before transport;
- explicit `retrieved_evidence_material` response field;
- validation requiring at least one retrieved-prior citation when retrieved evidence is declared materially influential;
- neutral hardened prompt language that avoids leaking historical condition vocabulary;
- backward-compatible legacy mode for reproducing and auditing historical experiments without silently rewriting them.

## Offline results

The following controls passed:

1. opaque subject ID determinism and non-disclosure;
2. normalized forbidden-token rejection;
3. typed evidence validation;
4. exact request archival and idempotence;
5. integrated hardened belief-revision boundary;
6. retrieved-evidence attribution enforcement;
7. legacy rationale-length hotfix regression;
8. complete deterministic SyntheticLab offline maintenance suite.

No provider key was exposed and no network/provider call occurred.

## Remaining before real-provider use

1. migrate a new, explicitly versioned experiment runner to the hardened boundary;
2. prevent legacy provider runners from being selected by the unattended provider entry point;
3. implement an enforceable provider-call budget at the transport boundary;
4. implement a bounded human approval record tied to the exact protocol, model, budget, and expiry;
5. test interruption/resume without double-spending the call budget;
6. preregister and run only the minimum revalidation chain after explicit approval.

The canonical status is machine-readable in `research/provider_gate_status.json`.
