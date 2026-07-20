# Context Management Protocol

## Problem

Long-running chats accumulate:

- obsolete versions;
- rejected ideas;
- fixed bugs;
- stale assumptions;
- contradictory experiment interpretations.

Conversational memory is useful but not authoritative.

## Solution

### GitHub repository
Canonical code + institutional memory.

### `research/CURRENT_STATE.md`
Compact first-read bridge.

Target:
roughly 5–15 pages, not hundreds.

It should always answer:

- current version/milestone;
- last known-good build;
- current architecture;
- implemented features;
- test status;
- known bugs;
- current experiment;
- major conclusions;
- open questions;
- next 3–5 priorities;
- decisions that must not be accidentally reversed.

### Append-only ledgers
Decisions, claims, negative results.

### Deep library
Atlas, audits, raw experiments.

### Migration handoff
Disaster recovery or major-phase reset, not everyday context.

## Rule for new chats

Do not start with a giant transcript dump by default.

Give repository access plus the bootstrap prompt.

Read current state first.

Deep-load only relevant artifacts.

## Rule for updates

When a milestone changes:

- update current state;
- append decisions;
- add experiment result;
- mark superseded/invalidated claims explicitly.

Never rewrite history so future agents cannot see how a conclusion changed.
