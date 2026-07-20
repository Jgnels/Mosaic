# GitHub Setup

The ChatGPT GitHub connector is connected, but a connector does not automatically migrate chats or
local project files.

At export time, no owned repository was visible through the connected account.

## Recommended setup

1. Create a private repository named something like `dagmay`.
2. Clone it on the dedicated desktop.
3. Copy the actual Dagmay codebase into the clone.
4. Merge this knowledge-base export at the repository root.
5. Commit.
6. Push.
7. Verify ChatGPT/Codex can read:
   - `AGENTS.md`
   - `research/CURRENT_STATE.md`

## Suggested first commit message

`Establish canonical Dagmay code and research memory`

## Suggested structure

```text
/AGENTS.md
/README.md
/src
/tests
/tools
/research
/docs
```

Do not upload:

- API keys;
- provider credentials;
- local tokens;
- passwords;
- machine-specific secrets.
