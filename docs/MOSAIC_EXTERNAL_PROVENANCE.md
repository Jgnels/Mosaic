# Mosaic External Provenance

**Audit date:** 2026-07-24
**Scope:** Read-only architecture and license review for v0.2 planning
**Code incorporated:** None

This record supplements, rather than rewrites, the artifact-specific review in
`rimworld/0.2-prealpha/docs/18_External_Mod_Reference_Review.md`. Repository and license state can
change; reverify before any future reuse.

## RimTalk

- Repository: <https://github.com/craftingmod/RimTalk>
- Exact revision: [`9338c63df05ec8adb287a367e73a3583dd009a14`](https://github.com/craftingmod/RimTalk/commit/9338c63df05ec8adb287a367e73a3583dd009a14), identified in source as v1.0.14.
- License reviewed: [CC BY-NC-SA 4.0](https://github.com/craftingmod/RimTalk/blob/9338c63df05ec8adb287a367e73a3583dd009a14/LICENSE).
- Evidence examined: exact commit metadata/diff and license tonight; the prior local source audit
  examined `RimTalkPromptAPI`, `ContextHookRegistry`, prompt hooks, context extraction, provider
  dispatch, world-save components, and dialogue history.
- Useful concepts: a supported optional context-hook API, named prompt anchors, bounded
  disclosure-filtered context, and one model-call owner per utterance.
- Direct reuse: not approved. The license permits noncommercial adaptation only with attribution
  and ShareAlike obligations. Those terms need a dedicated compatibility review before source
  copying or distribution.
- Required attribution if reused: creator/copyright identification, license notice and link,
  source link where practicable, modification notice, and compatible ShareAlike terms.
- Risks: optional-mod API/version churn, duplicate provider calls, private-memory disclosure,
  background access to live game objects, and license compatibility.
- Mosaic use tonight: concepts and future interoperability boundary only; zero copied code.

## RiMind Core

- Repository: <https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Core>
- Exact revision: [`d1b2d95aac694fb9192b8d4555bbc0484c36652e`](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Core/commit/d1b2d95aac694fb9192b8d4555bbc0484c36652e).
- License reviewed: [MIT](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Core/blob/d1b2d95aac694fb9192b8d4555bbc0484c36652e/LICENSE).
- Evidence examined: `README.md` and `LICENSE` at the exact revision. No third-party script was run
  and no repository was cloned.
- Useful concepts: explicit module boundaries, asynchronous request queues, context-filter
  presets, provider-independent transport surfaces, and operational diagnostics.
- Direct reuse: MIT-covered code is generally reusable with its copyright and permission notice,
  but no reuse is approved from this high-level review. Individual files and dependencies require
  a source-level provenance and safety audit first.
- Required attribution if reused: retain the MIT copyright and permission notice in copies or
  substantial portions.
- Risks: the suite includes autonomous action modules that conflict with Mosaic's current
  observer-only boundary; its context, secrets, persistence, provider, and canonical-authority
  assumptions differ from Mosaic's; dependencies and file-level notices remain unreviewed.
- Mosaic use tonight: conceptual comparison only; zero copied code.

The 2026-07-16 supplied RiMind Workshop artifact had no source tree or usable license statement.
That observation remains valid for that exact artifact. The public MIT repository found on
2026-07-24 is new evidence and does not retroactively license or identify the earlier binary.

## Free Will

- Repository: <https://github.com/paul-freeman/rimworld-freewill>
- Exact revision: [`47ec7c9f3a05876b263c14f349a4c2ddda784701`](https://github.com/paul-freeman/rimworld-freewill/commit/47ec7c9f3a05876b263c14f349a4c2ddda784701).
- License reviewed: [MIT](https://github.com/paul-freeman/rimworld-freewill/blob/47ec7c9f3a05876b263c14f349a4c2ddda784701/LICENSE).
- Evidence examined: `README.md`, `docs/architecture.md`, and `LICENSE` at the exact revision.
- Useful concepts: separate world/map components, deterministic work-priority calculation,
  explanation UI, explicit application boundary, and testable strategy seams.
- Direct reuse: legally possible for MIT-covered code with notice, but technically rejected for
  the current stage. Free Will applies priorities and patches execution behavior; Mosaic Gate 3
  and current v0.2 work remain observer-only.
- Required attribution if reused: retain Paul Freeman's copyright and MIT permission notice.
- Risks: action authority, Harmony compatibility, RimWorld-version coupling, and accidental
  conflation of external execution with Mosaic-authored goals.
- Mosaic use tonight: future planning reference only; zero copied code.

## Provenance classification

| Source | Direct code | Adapted concept | Rejected/deferred |
| --- | --- | --- | --- |
| RimTalk v1.0.14 | None | Optional bounded context-hook boundary | Source copying and bridge work pending license/API review and Gate 4 |
| RiMind Core | None | Module/context-filter/diagnostic comparison | Code reuse and all autonomous-action patterns |
| Free Will | None | Component separation and explanation seams | Any action-control implementation before an explicit later gate |

All Mosaic changes in the overnight branch are original implementations derived from Mosaic's own
requirements and tests.
