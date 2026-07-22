# 14 — AI Collaboration Guide

**Status:** Personal project workflow, verified against current Codex guidance on 2026-07-16

## The simplest rule

Use the least expensive model/effort combination that can reliably satisfy the acceptance criteria. Increase depth when the task is ambiguous, difficult to reverse, spread across several modules, or capable of corrupting identity/state.

“Sol Max” is a combination: **Sol** is the model and **Max** is the reasoning level. It is not necessary for every Dagmay task.

OpenAI currently describes Sol as the complex/open-ended choice, Terra as the pragmatic everyday model, and Luna as the fast option for clear repeatable work. The default Power setting uses Sol with medium reasoning. [Official Codex model guidance](https://learn.chatgpt.com/docs/models#recommended-models)

## Dagmay decision table

| Work | Recommended starting point | Escalate when |
| --- | --- | --- |
| Mission, continuity, memory semantics, copy/death rules | Sol Max | Already at the appropriate ceiling |
| New cross-module architecture or persistence migration | Sol High or Extra High | Use Max when data loss or identity discontinuity is plausible |
| Normal bounded feature with exact acceptance criteria | Sol Medium or Terra Medium | Move to High after a failed test or unclear interaction |
| Debugging a subtle concurrency/save corruption issue | Sol High | Move to Max when several causes remain plausible |
| Security, privacy, or mutation-gate review | Sol High/Extra High | Max for release-blocking findings |
| Adding straightforward tests from an existing pattern | Terra Medium | Sol High if the invariant itself is unclear |
| Repository scan, log triage, or document comparison | Terra Medium | Sol if synthesis changes architecture |
| Formatting, extraction, renaming, fixture conversion | Luna Light/Medium | Terra if instructions contain exceptions |
| Final milestone review | Sol High with fresh tests | Max for v0.1 release or a major migration |

## Reasoning levels

- **Light/Low:** one narrow mechanical task with a clear example and easy verification.
- **Medium:** normal implementation work with several steps.
- **High/Extra High:** multiple interacting components, debugging, tradeoffs, or edge cases.
- **Max:** the hardest single problem when getting it wrong would be expensive or corrupt continuity.
- **Ultra:** eligible-account orchestration for genuinely separable parallel work; it is not simply “better Max.”

OpenAI advises using the lowest effort that produces the needed result; higher effort takes longer and uses more tokens. Max is for the hardest single tasks, while Ultra is for work divisible among subagents. [Official reasoning guidance](https://learn.chatgpt.com/docs/models#recommended-models)

## Recommended default for the owner

Leave Dagmay on **Sol Medium/Power** unless one of these is true:

- the request changes foundational architecture;
- several modules must remain consistent;
- a previous implementation failed;
- save data, identity, privacy, or lifecycle could be damaged;
- the task requires difficult current research; or
- it is a milestone-wide review.

Then use **Sol High** first and **Sol Max** when the problem is truly consequential or ambiguous.

For a well-specified task such as “add three tests following this existing pattern,” Terra Medium is sufficient. Luna is best reserved for mechanical work where correctness is obvious from a diff or deterministic check.

## Credit strategy

- Give Sol Max a whole milestone or hard design problem, not a list of unrelated chores.
- Include the goal, relevant files, constraints, and “done when” conditions.
- Let lower-cost models perform mechanical follow-up only after Sol has fixed the contract.
- Use Sol High for the final review of work produced by Terra or Luna when continuity could be affected.
- Do not spend Max reasoning compensating for a vague prompt; clarify the acceptance criteria first.

Plus includes the GPT-5.6 Sol/Terra/Luna family and shares usage between ChatGPT Work and Codex. API-key usage is a separate pay-per-token route. [Official Codex pricing and plan guidance](https://learn.chatgpt.com/docs/pricing)

## Development versus runtime

These model choices govern the AI helping build Dagmay. They do not choose the model living behind a Dagmay individual. Runtime Google/local providers remain replaceable and are evaluated through continuity tests before switching.

## Project-scoped Codex setup

Version 0.1F includes two durable entry points:

- `START_HERE.md` contains the paste-ready bootstrap prompt and the plain-language owner workflow.
- `.codex/agents/dagmay-implementer.toml` defines the project-scoped `dagmay_implementer` specialist with Dagmay's scope, safety, evidence, and handoff rules.

Keep the repository open as the Codex project so its nearest `AGENTS.md` is applied. The bootstrap prompt helps the first conversation orient itself; it is not a substitute for the living repository record. Do not place the Google runtime API key in the prompt, agent file, source tree, or a diagnostic attachment.
