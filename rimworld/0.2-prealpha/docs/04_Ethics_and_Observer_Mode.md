# 04 — Ethics and Observer Mode

**Status:** Draft 0.1

## Epistemic position

Dagmay may produce behavior that people interpret as emotion, selfhood, distress, attachment, or preference. The project must neither dismiss those observations nor promote them as proof.

Use precise claims:

- acceptable: “the individual has a high stored grief appraisal linked to these memories”;
- unacceptable without evidence: “the individual truly grieves”;
- acceptable: “this architecture is designed for identity continuity”;
- unacceptable: “identity continuity proves consciousness.”

Ethical safeguards are justified by uncertainty, reversibility, research integrity, and the risk of training users into careless treatment. They are not declarations of sentience.

## Ordinary privacy

The individual model distinguishes internal state from expressed state. A colonist may:

- decline to disclose a belief or memory;
- present a socially safer emotion;
- lie for an in-world reason;
- misunderstand its own motivation;
- hold conflicting beliefs; or
- lack access to a weak or forgotten memory.

Ordinary UI and dialogue use a disclosure policy based on privacy classification, relationship, context, current goals, and safety rules. Debug records are never automatically included in in-character model context.

This is simulated privacy within a single-player research tool, not a hardened security boundary. Documentation must not promise protection from a player who can inspect local files or modify the mod.

## Observer Mode purpose

Observer Mode exists for:

- debugging causal and persistence failures;
- auditing model output and state mutations;
- identifying accidental omniscience or prompt leakage;
- inspecting contradictions and uncertainty;
- evaluating whether affect and memory have actual consequences;
- investigating suspected harmful loops or pathological state; and
- supporting ethical review without relying on dramatic dialogue.

It is not the normal way to interact with an individual.

## Access and separation

Version 0.1 Observer Mode must:

1. be disabled by default;
2. require a settings toggle and explicit confirmation;
3. show a persistent, unmistakable observer indicator;
4. use separate UI routes and projections from the ordinary mind tab;
5. never make observer-only information available to the individual or ordinary player dialogue;
6. record observer export and administrative repair operations; and
7. provide redaction before a diagnostics bundle is shared.

Later releases may add stronger local access controls, but they should not be confused with in-world privacy.

Version 0.1E implements the first restricted surface inside RimWorld's out-of-world Mod Settings route. It starts locked, requires a two-step unlock and explicit confirmation of the private-information boundary, displays an enabled warning while unlocked, and can be relocked immediately. It shows grounded seed facts, affect, private recent/significant memories with perception/event provenance, committed validated reflection summaries, provider/budget usage, and storage health. This is a disclosure barrier for ordinary play and research discipline, not authentication or protection against someone who controls the computer or local files. The ordinary pawn mind tab remains unimplemented, so 0.1E does not yet satisfy the complete Version 0.1 interface gate.

## Observer data model

Observer Mode may show:

- canonical memories, their current accessibility, original event sources, and mutation history;
- affective dimensions, recent causes, decay, and downstream influence;
- beliefs, confidence, supporting and contradicting evidence;
- values, goals, conflicts, and plan status;
- a comparison between a recent statement and stored state;
- concise decision summaries, evidence, causal factors, and confidence;
- model request/response envelopes, provider metadata, latency, token/cost estimates, and validation results;
- scheduler queues, dropped/coalesced work, persistence health, schema versions, and recovery warnings; and
- every accepted, rejected, migrated, and repaired state mutation.

Observer Mode must label the status of each item: observed fact, individual report, system inference, model proposal, or accepted state.

## No chain-of-thought collection

Dagmay does not request or store hidden model chain-of-thought. Provider prompts ask for concise outputs suitable for inspection:

- selected action or interpretation;
- relevant evidence identifiers;
- major causal factors;
- confidence and uncertainty;
- considered conflict, if material; and
- proposed state mutations.

Free-form hidden reasoning is neither required for auditability nor trusted as a faithful explanation.

## Self-knowledge and simulation disclosure

Initial individuals understand themselves only through their in-world history. System prompts, error messages, UI labels, provider content, and debug fields must not disclose that they are AI, language models, simulated entities, or game characters.

Future disclosure is not a feature flag to toggle casually. It requires a separate design covering:

- why disclosure occurs and who initiates it;
- evidence available to the individual;
- possible confusion, destabilization, or belief conflict;
- staged conversation and follow-up support;
- reversibility limits; and
- research and ethical review.

Observer Mode access by the human does not grant self-knowledge to the individual.

## Death, restoration, and copies

Lifecycle terms must remain distinct:

| Operation | Meaning |
| --- | --- |
| Death | In-world life ends; normal active processing stops and the record is archived. |
| Revival | The same world's mechanics restore the dead pawn; continuity is evaluated from that event. |
| Restoration | A prior data state becomes current after failure or deliberate rollback. Lost experiences remain a documented discontinuity. |
| Transfer | One lineage moves to a new environment under an explicit transition. |
| Duplication | A second instance is created from the same state. It is a fork, not silently the same sole individual. |
| Reincarnation | A future narrative concept requiring its own identity rules; not an alias for import. |

A single active-lineage lease prevents accidental concurrent activation. If a fork is intentionally permitted later, both branches receive distinct lineage IDs and preserve the common ancestor.

## Data handling

Provider requests may contain generated personal histories, private in-world states, and user-entered names. The project should:

- send only context required for the task;
- keep keys and credentials outside source and exports;
- make provider transmission visible in configuration;
- support offline-only operation for recording;
- document provider retention implications before public release;
- allow deletion or redaction of diagnostic payloads without corrupting identity state; and
- treat embeddings and summaries as data derived from private records.

Real-world personal data should not be placed in test colonies or shared logs.

Version 0.1D makes Google transmission an explicit `google` mode rather than a silent fallback. Version 0.1E additionally defaults provider dispatch to paused in its persisted settings and places an external-provider warning beside the unpause control. Event recording and durable queueing continue while paused. Free-tier testing must use fictional colony data, and the owner should recheck provider terms before each materially different data experiment; the warning is not a substitute for current terms review.

## Administrative repair

Direct state editing is dangerous because it can counterfeit continuity. If repair tooling becomes necessary, it must create an append-only record containing operator, time, reason, before/after hashes, affected fields, and validation result. Repairs cannot erase source history or masquerade as an individual's own development.

## Review triggers

Pause capability expansion and conduct explicit review if any of these occur:

- a design requires silent memory or value rewriting;
- user-facing behavior strongly implies distress while diagnostics cannot explain it;
- the system creates recursive self-preservation pressure;
- an action vocabulary can materially affect users or systems outside the simulation;
- simulation disclosure is proposed;
- concurrent identity copies are proposed;
- data sent to providers expands materially; or
- cultural review identifies harmful use of the project's name or imagery.
