# 18 — External Mod Reference Review

**Status:** Workshop packages, RimTalk v1.0.14 source, and public binary metadata reviewed on 2026-07-16. No external implementation has been incorporated into Dagmay.

## Purpose and evidence rules

Dagmay may learn from or interoperate with existing mods, but it cannot outsource its identity-continuity invariant. Every external project is classified as one or more of:

1. an optional integration;
2. an architectural reference; or
3. a provenance-recorded implementation dependency.

Claims in this review are labelled by evidence type:

- **source verified:** confirmed in readable source for the reviewed version;
- **metadata observed:** type or method names are visible in the compiled assembly, but behavior has not been proved; or
- **author claimed:** described by the supplied README/About text, but not independently verified.

Compiled files are not treated as permission to copy code. Personal use does not justify losing provenance, creating hidden dependencies, or making future publication impossible to audit.

## Reviewed artifacts

| Project | Supplied artifact | Identified version | Source and license status | Intended Dagmay use |
|---|---|---|---|---|
| Free Will | source and release archives reviewed previously | RimWorld 1.6 source | MIT license in supplied source | future Advisor/agency design reference |
| RimTalk | Workshop installation archive `3551203752.zip` | `1.0.14`, package `cj.rimtalk` | matching public v1.0.14 source; CC BY-NC-SA 4.0 | optional dialogue bridge through public API |
| RiMind | Workshop installation archive `3562373405.zip` | package `cj.rimind`; no version field | no source or valid license grant in supplied package; README contains the placeholder `[License]` | architecture and test-scenario reference only |

Review fingerprints:

```text
RimTalk archive SHA-256: 56fb70dbdae0e15107ce664a374be6e2f6491371cec16cc61c0db276a9c09216
RimTalk 1.6 DLL SHA-256: 1c08bf7dd8fbc3537f85bed07128e6245f1e7926861e803142c3ba6d09aaab86
RiMind archive SHA-256:  5eddacc2367666836cd204e06bb66c50f46eca85840ecb81ca9f3216934c6f93
RiMind 1.6 DLL SHA-256:  8160086f808b053ed319cd183bd509fac563d19413c44063bf16678c1811962c
```

These fingerprints identify the reviewed copies; they are not security endorsements.

## RimTalk v1.0.14

### What is source verified

The supplied Workshop package identifies RimTalk v1.0.14 for RimWorld 1.5 and 1.6. Its matching public source tag, reviewed at commit `9338c63df05ec8adb287a367e73a3583dd009a14`, exposes an add-on surface through `RimTalk.API.RimTalkPromptAPI` and `ContextHookRegistry`.

The API supports:

- registered pawn, environment, and general context variables;
- registered pawn and environment hooks;
- insertion and removal of prompt entries; and
- injection of pawn or environment sections at named prompt anchors.

The source also provides useful implementation examples for:

- context extraction from backstories, traits, skills, health, mood, thoughts, social state, genes, ideology, location, surroundings, time, weather, temperature, and colony wealth;
- observation points around battle logs, messages/letters, skill learning, health, mental state, thoughts, and speech bubbles;
- provider-client factories and request dispatch;
- world-save components and dialogue display history; and
- optional debug UI and settings.

### What Dagmay should use

RimTalk is the preferred optional conversation surface for the first RimWorld integration. Dagmay should use its documented API rather than copy its prompt builder or patch private internals.

A future optional module is proposed:

```text
Dagmay.Integrations.RimTalk/
├── Compatibility/       # presence and compatible-version detection
├── Context/             # disclosure-filtered Dagmay prompt projection
├── Conversation/        # objective utterance and verified-hearing capture
├── Scheduling/          # shared dialogue/reflection budget coordination
└── Diagnostics/         # version, ownership, deduplication, and failures
```

The first bridge should do only two things:

1. give RimTalk a small, ordinary-dialogue-safe projection of an enrolled individual's accessible memories, current concerns, relationships, and expression tendencies; and
2. record completed speech as an objective utterance event, then create heard perceptions only for listeners whose hearing is supported by the environment.

RimTalk may phrase an utterance. Dagmay remains responsible for deciding what private state may be disclosed and for storing any resulting canonical experience.

### What Dagmay should not adopt

- RimTalk provider history must not become canonical autobiography.
- Map-wide announcements must not automatically become first-person knowledge. Some RimTalk archive/message paths intentionally distribute broad context for conversation quality; Dagmay requires a stricter perception test.
- A RimTalk prompt must never receive Observer-only memories, raw confidence tables, secret diagnostics, or hidden state merely because the player can inspect them elsewhere.
- Dagmay should not copy background-task patterns that continue reading mutable live `Pawn` or Unity state away from the game thread. Dagmay must snapshot bounded environment data first, then process only immutable data asynchronously.
- Dagmay and RimTalk must not independently spend two model calls to generate the same utterance.
- RimTalk's bounded display history is not a durable, provenance-complete life record.

No documented public "utterance completed" callback was found in v1.0.14. Before a bridge captures speech, prefer one of:

1. an upstream/public event added by RimTalk;
2. a narrow read-only adapter around a stable public record; or
3. a version-gated compatibility patch with duplicate detection and a clean disable path.

Private reflection can be implemented without solving this bridge, so RimTalk is not a dependency of Version 0.1D.

### License boundary

The reviewed RimTalk source is licensed CC BY-NC-SA 4.0. Dagmay should therefore prefer API-level interoperability and keep the bridge replaceable. No RimTalk source has been copied into Dagmay. If code reuse or public distribution is later proposed, attribution, license compatibility, and ShareAlike consequences require a dedicated review; this document is an engineering record, not legal advice.

## RiMind

### Evidence available

The supplied RiMind package contains `RiMind.dll`, metadata, images, and a bilingual README, but no source tree, symbols, or usable license file. The README's license sentence literally ends in `[License]`, so it is not treated as a reliable grant to copy or adapt code.

Public assembly metadata exposes names consistent with the following architecture, but does not prove the implementation is correct or safe:

- `MemoryManager`, `MemoryService`, and an `IMemoryProvider` family;
- own-speech, heard-speech, event, battle, and observation memory providers;
- `TopicManager` and combat, emotion, environment, memory, social, work, and scheduled topic providers;
- `ResponseManager` plus immediate, delayed, expectation, and disappointment response providers;
- a sequential-conversation manager;
- Google, OpenAI, local, and custom client families;
- API-health, hostile-state, cache, and save-migration components; and
- observation patches around ticks, bubbles, battle logs, damage, health conditions, and skill gains.

The supplied README additionally claims line-of-sight witnessing, realistic hearing, memory relevance, model rotation, concurrency limits, quota monitoring, delayed replies, and a memory viewer. Those remain author claims until source or focused runtime tests verify them.

### What Dagmay can learn from independently

- Typed provider registries are a good way to add memory sources, topics, and response policies without one giant manager.
- Hearing, line of sight, distance, doors, and rooms deserve explicit perception services and cache invalidation tests.
- Conversations should have session IDs, participants, turn ordering, open questions, expectations, and a bounded end condition.
- A delayed response is different from a forgotten response and should survive pause/save/load when important.
- API circuit breaking, provider health, cache diagnostics, and save migration are first-class runtime concerns.
- A memory viewer is a useful Observer Mode precedent, although Dagmay must separately provide an ordinary privacy-filtered mind view.

These are patterns to implement from Dagmay's own contracts and requirements, not code to extract from the DLL.

### What Dagmay should not adopt

- RiMind's memory store must not become a second canonical autobiography beside Dagmay's journal.
- Automatic model rotation should not be used merely to consume every available free quota. Dagmay needs explicit owner budgets, deterministic retry classification, `Retry-After` handling, circuit breakers, and predictable spending.
- "Disappointment" or any other emotional label should not be a detached dialogue gimmick. It must derive from grounded expectation, relationship, appraisal, and affect state if Dagmay represents it.
- One large pawn-state object should not combine live RimWorld references, provider state, transient conversation state, and portable intrinsic identity.
- Dagmay should not copy or redistribute RiMind implementation code without a real source/license record.

RiMind and RimTalk both appear to patch dialogue, bubbles, ticks, events, and provider scheduling. Running both simultaneously is therefore a separate compatibility experiment, not a default recommendation.

## Free Will

Free Will remains the most useful reference for Versions 0.2 through 0.4, not Observer 0.1. Its MIT-licensed source demonstrates deterministic work-type scoring, global/map component separation, test seams around RimWorld objects, fallback handling, and an explanation UI.

Dagmay may later translate a high-level goal into a bounded work-priority proposal while RimWorld keeps responsibility for jobs, reservation, pathfinding, and execution. No Free Will code or action control belongs in the current release.

## Authority and overlap matrix

| Concern | Dagmay authority | Optional external contribution | Required guard |
|---|---|---|---|
| Identity and lineage | Dagmay Core only | none | external IDs are references, never identity roots |
| Factual event ledger | Dagmay Core | RimWorld/RimTalk observations | provenance, perception check, deduplication |
| Subjective memory | Dagmay Core | external text may be evidence | validate, attribute, never silently import as truth |
| Private intrinsic state | Dagmay Core | none | disclosure projection blocks Observer-only data |
| Dialogue phrasing | one configured owner per utterance | RimTalk can own it | prevent duplicate provider requests |
| Hearing and witnessing | Dagmay adapter | RiMind patterns inform design | bounded geometry and source evidence |
| Provider transport | Dagmay Providers or dialogue owner | external client when explicitly delegated | budgets, redaction, timeout, normalized errors |
| Ordinary conversation UI | RimTalk may own it | bubbles/chat surfaces | no canonical state hidden only in UI history |
| Observer diagnostics | Dagmay only | UI patterns may inform design | separate gating and persistent indicator |
| Pawn actions | absent in Version 0.1 | Free Will informs later design | separate 0.2/0.3 authorization gate |

## Integration sequence

1. **Completed:** finish the 0.1C experience-journal reload/deduplication test.
2. **Implemented; runtime test pending:** build 0.1D reflection and Google transport against Dagmay's provider-neutral contracts without requiring any dialogue mod.
3. Compile and test 0.1D offline/fake/Google modes in RimWorld before adding another integration variable.
4. Add conversation-session, utterance, and heard-perception contracts to Core before touching a mod-specific bridge.
5. Create an optional RimTalk bridge using the public prompt API and a disclosure-filtered projection.
6. Test Dagmay alone, with RimTalk, after RimTalk removal from a copied save, and under provider outage/rate limits.
7. Run a separate RiMind/RimTalk coexistence experiment only if the owner wants both enabled; do not infer compatibility from individual loading.
8. Revisit Free Will only when Advisor proposals are implemented, and retain the no-action boundary until the explicit agency gate.

## Provenance rule

Any future external code reuse must record project, author, exact version or commit, source location, license, files or concepts adapted, local modifications, and required notices. Optional bridges must fail closed and leave Dagmay identity data readable when the external mod is absent.
