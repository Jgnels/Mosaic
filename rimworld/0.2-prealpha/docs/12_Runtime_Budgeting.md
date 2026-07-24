# 12 — Runtime Enrollment and Model Budgeting

**Status:** Version 0.1E persisted controls compiled and tested in isolation and in RimWorld; Version 0.1F changes only development automation

## Two separate uses of AI

1. **Development AI:** ChatGPT Work/Codex helps design, write, inspect, and debug Dagmay. The owner's Plus usage applies here.
2. **Dagmay runtime AI:** the RimWorld mod sends reflection work to Google AI Studio or a later local provider. This requires its own quota, API key, or local hardware.

ChatGPT Plus is not treated as an API credential and Dagmay will not automate a ChatGPT web session.

## Selective enrollment

Not every colonist needs a full Dagmay identity.

| Level | Meaning | Runtime cost |
| --- | --- | --- |
| Enrolled individual | Full persistent identity, memories, affect, beliefs, goals, and reflection | Highest |
| Known person | Appears in relationships and memories of enrolled individuals but has no full inner lifecycle | Low |
| World actor | Referenced only as an event participant until relevant | Minimal |

The first test colony should enroll at most four colonists. Enrollment is reversible only at the service level: disabling future processing does not delete an existing individual's history.

## How cost scales

If every colonist received identical reflection frequency and context size, four colonists would use roughly one-tenth of forty. Real use will not scale perfectly because:

- some work is fixed per colony;
- one shared event can be batched;
- quiet colonists generate less event-triggered work;
- relationship events may involve two enrolled people;
- consolidation frequency depends on accumulated memories; and
- context size grows with history unless retrieval stays selective.

Dagmay budgets by enrolled individuals, event salience, queue pressure, and estimated tokens—not total colony population alone.

## Initial free-tier-oriented profile

- enrolled individuals: 1–4;
- scheduler heartbeat: once per real-world minute;
- concurrent remote requests: 1;
- maximum concrete provider attempts: 4 per RimWorld process/session;
- maximum remote requests: 12/hour;
- initial estimated-token ceiling: 40,000/day;
- background reflection: deferred whenever meaningful events are queued;
- critical death/lifecycle records: never dropped;
- low-priority repeated events: coalesced; and
- provider unavailable: record locally and wait.

These are safety defaults, not claims about any current Google free-tier quota. Version 0.1E retains the account-tested `gemini-3.1-flash-lite` default. Provider availability, terms, and quotas can change and must be rechecked. The runtime records actual provider token metadata when available, but the 40,000-token daily gate remains a conservative estimate rather than a billing guarantee.

Version 0.1E exposes session, rolling-hour, UTC-day estimated-token, and heartbeat limits in RimWorld's Dagmay Mod Settings page. Bounds are validated when settings load; invalid or unsupported settings fail back to conservative defaults with dispatch paused. The per-session limit is a hard transport stop until RimWorld restarts or the owner raises it. Hour/day/circuit limits defer work to a calculated retry time. At most one request is active. A completed transient failure receives at most three total attempts; a permanent or exhausted failure is quarantined.

Provider dispatch is independently paused by default. Pausing does not stop deterministic event recording or erase the queue. The Observer health view distinguishes session attempts, rolling-hour attempts, UTC-day estimated use, and provider-reported tokens. Reported tokens are deduplicated by request ID so the pre-commit and committed audit stages do not multiply usage.

## What should trigger a model call

### Usually yes

- death, revival, severe injury, rescue, or major betrayal;
- major relationship formation or rupture;
- ideology conversion or identity conflict;
- a cluster of events ready for consolidation;
- unresolved contradiction affecting current behavior; and
- periodic autobiographical integration for an enrolled individual.

### Usually local-only

- ordinary hunger/rest changes;
- routine work completion;
- repeated low-severity needs;
- deduplication, thresholds, affect decay, retrieval filters, and queueing;
- event formatting and persistence; and
- obvious deterministic state transitions.

## Batching rule

A shared event should create one factual event and multiple bounded perspectives. When safe, several reflection tasks may share a provider request, but results must return as separately validated proposals for each individual. A batch failure cannot partially corrupt one member.

## Local-model path on the owner's PC

The owner-confirmed machine has an RTX 3080 Ti with 12 GB VRAM and 16 GB system RAM. Local inference remains a possible later experiment, but model size, quantization, context cache, RimWorld load, and inference software must be measured together. No local model should run during Gate 3 runtime testing, and no comfortable model-size range is assumed from the hardware specification alone.

Local inference removes per-token charges but not performance costs. Dagmay must keep the same queue, timeout, schema validation, and graceful-degradation boundaries for local providers.

## Provider configuration

RimTalk's provider selection does not configure Dagmay. Version 0.1E reads only Dagmay's explicit environment variables. On Windows it reads the current persisted user value first, so a stale setting inherited by an already-running Steam process cannot override the owner's newer choice:

- `DAGMAY_REFLECTION_MODE`: `offline`, `fake`, or `google`;
- `DAGMAY_GOOGLE_API_KEY`: secret Google AI Studio credential; and
- `DAGMAY_GOOGLE_MODEL`: optional model ID, default `gemini-3.1-flash-lite`.

The source package includes `tools/configure-reflection.ps1` so the owner does not have to expose the key in command history. ChatGPT Plus remains unrelated to runtime API quota.

The key is not written to Dagmay source, saves, sidecars, requests bodies, or logs. A Windows user environment variable is per-account configuration, not encrypted vault storage; the helper can remove it after testing.

## Model fallback accounting

RiMind demonstrates that one Google key can access multiple account-enabled models and that fallback can improve quota availability. Dagmay will adopt that capability only behind its own provider policy. Every network attempt in a fallback chain must:

- consume an attempt/rate budget before transport;
- record the exact model, result, latency, and usage independently;
- satisfy the same response schema and local validator;
- stop on authentication, malformed-request, or other permanent failures; and
- never reinterpret a failed model's partial response as canonical state.

Until that accounting exists, Version 0.1E uses one explicitly selected model per request. This is deliberate cost and provenance control, not a dependency on one model.
