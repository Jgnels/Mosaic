# Mosaic 0.2 Dialogue Runtime Retest - 2026-07-26

- **Status:** LIVE PASS for the isolated dialogue/thread-affinity path
- **Test operator:** project owner
- **Tested commit:** `c49da815bd9271d132f557cec40745f50a3b9962`
- **Mosaic version:** `0.2-prealpha`
- **RimWorld version:** `1.6.4871`
- **Package SHA-256:** `de6c8b6652297f10ac3ed2f6b53975f376b1c8b962221876923f41bae3bf32aa`
- **Active mods:** RimWorld Core and Mosaic only
- **Provider mode:** offline; no provider dispatch

## Evidence scope and sanitization

This repository record is a sanitized summary of an owner-operated live test.
The raw `Player.log`, saves, sidecars, identities, journals, configuration, and
machine-specific evidence remain outside Git.

Sanitization applied:

- the Windows username and absolute paths were removed;
- pawn display names and full runtime identifiers were replaced with stable
  placeholders in excerpts;
- Unity allocator/memory-statistics output and unrelated machine information
  were omitted; and
- substantive Mosaic message text, event kinds, counts, checkpoint generation,
  presentation channel, and error-absence results were preserved.

Stable placeholders used below are `<COLONIST>`, `<EVENT_ID>`, `<MEMORY_ID>`,
`<UTTERANCE_ID>`, and `<MOSAIC_RUNTIME_PATH>`.

## Procedure

1. The owner used RimWorld 1.6.4871 with only Core and Mosaic enabled.
2. Provider dispatch remained offline.
3. Automatic enrollment was enabled in the disposable test game.
4. The owner allowed qualifying opinion and direct-relationship changes to
   occur and observed the deterministic fake-dialogue path.
5. The owner confirmed visible speech-bubble presentation.
6. The disposable game was saved and reloaded.
7. Identity continuity, checkpoint generation, persisted event/memory counts,
   reflection queue state, storage health, duplicate presentation, and bounded
   error absence were inspected after reload.

## Owner-observed result before save

- Three colonists enrolled successfully.
- `rimworld.social.opinion_changed` and
  `rimworld.relationship.direct_changed` events were detected.
- Four bounded experiences and four memories were created.
- Four dialogue admissions received actual `channel=Bubble` receipts.
- The expected deterministic sentence appeared in speech bubbles.
- `RimWorld dialogue presentation is main-thread-only` did not recur.
- No runtime-thread validation or presentation-disable warning appeared.
- No exception flood or noticeable performance degradation occurred.

## Sanitized pre-save log excerpts

The owner reported four bounded-experience messages using the fixed Mosaic
template, covering the two qualifying social event kinds:

```text
[Dagmay] 0.2-prealpha recorded bounded experience; name=<COLONIST>; kind=rimworld.social.opinion_changed; EventId=<EVENT_ID>; MemoryId=<MEMORY_ID>.
[Dagmay] 0.2-prealpha recorded bounded experience; name=<COLONIST>; kind=rimworld.relationship.direct_changed; EventId=<EVENT_ID>; MemoryId=<MEMORY_ID>.
```

The owner reported four actual Bubble-channel admission receipts:

```text
[Dagmay] 0.2-prealpha queued verified dialogue admission; UtteranceId=<UTTERANCE_ID>; channel=Bubble.
```

The owner observed the deterministic fake-provider sentence rendered in the
speech bubbles. Dialogue text is not reproduced here because the presentation
channel and factual receipt are the evidence relevant to this test.

## Owner-observed result after save and reload

- All three identities retained their `IndividualId` and `LineageId`.
- Checkpoint generation advanced to 2.
- Identity, experience, and reflection storage remained healthy.
- State restored with `Events=8; Memories=4`.
- The reflection queue restored with two meaningful-event items.
- Previously displayed dialogue was not presented again.
- No thread-affinity, presentation, or other Mosaic exception appeared.

## Sanitized post-reload log excerpts

The restored-state message retained these relevant fields; private paths and
identifiers are replaced or omitted:

```text
[Dagmay] 0.2-prealpha observer service ready after load. Individuals=3; Generation=2; Events=8; Memories=4; ReflectionMode=offline; ReflectionQueue=2; ... IdentityStorage=healthy; ExperienceStorage=healthy; ReflectionStorage=healthy; IdentityPath=<MOSAIC_RUNTIME_PATH>; ExperiencePath=<MOSAIC_RUNTIME_PATH>; ReflectionPath=<MOSAIC_RUNTIME_PATH>
```

The exact certification result reported:

```text
SOCIAL PATH CERTIFICATION PASS; events=4; memories=4; reflectionEligible=2; postLoad=True.
```

## Pass/fail matrix

| Area | Result | Evidence |
|---|---|---|
| Exact tested commit and package | PASS | Commit and SHA-256 above |
| Minimal Core + Mosaic load | PASS | Owner observation and bounded log review |
| Enrollment | PASS | Three stable identities |
| Opinion-change detection | PASS | `rimworld.social.opinion_changed` experience |
| Direct-relationship detection | PASS | `rimworld.relationship.direct_changed` experience |
| Bounded experience and memory creation | PASS | Four experiences and four memories |
| Deterministic fake-dialogue path | PASS | Four accepted deterministic presentations |
| Bubble-channel presentation | PASS | Four `channel=Bubble` receipts and visible bubbles |
| Runtime-thread repair | PASS | No recurrence or validation-disable warning |
| Save checkpoint | PASS | Checkpoint generation advanced to 2 |
| Reload persistence | PASS | Stable identities; `Events=8; Memories=4`; healthy stores |
| Reflection-queue reload | PASS | Two meaningful-event items restored |
| Duplicate-presentation prevention | PASS | Previously displayed dialogue did not reappear |
| Exception/performance containment | PASS | No exception flood or noticeable degradation |
| Play-log fallback | UNVERIFIED | Bubble path succeeded; fallback was not exercised |
| Full normal mod stack | UNVERIFIED | Isolated Core + Mosaic only |
| DLC combinations | UNVERIFIED | Not exercised as a compatibility matrix |
| RimTalk coexistence | UNVERIFIED | RimTalk was not active |
| Long-duration soak | UNVERIFIED | This was a targeted functional retest |
| Real provider behavior | UNVERIFIED | Provider dispatch remained offline |

## Interpretation and remaining scope

This is OBSERVED live evidence that the `c49da81` runtime-thread correction
works in the isolated Core + Mosaic configuration and that the tested
enrollment, social capture, bounded experience/memory, deterministic fake
dialogue, Bubble receipt, save/reload, storage-health, and duplicate-
presentation paths operated together.

It does not establish full normal-mod-stack compatibility, DLC-combination
compatibility, RimTalk coexistence, play-log fallback, long-duration soak
stability, broader visual/gameplay tuning, or real-provider behavior.

## Offline evidence-commit verification

The evidence-only commit changes documentation, current-state, risk, and
verification records. Git comparison confirms that its complete runtime,
test, integration-harness, build-tool, and package-tool source trees are
identical to tested implementation commit `c49da81`.

Complete Release verification of that unchanged source state passed:

- static verification: 129 C# files;
- contract tests: 153 executed and zero failed;
- integration harness: seven scenarios, 631 assertions, zero failed;
- Core, Providers, and RimWorld 1.6 Release builds: zero warnings/errors;
- package firewall fixtures: one valid accepted and 17 adversarial rejected;
- package firewall: eight declared entries and zero direct adaptations; and
- frozen `rimworld/0.1-closure-candidate/`: unchanged.

Because portable PDB SourceLink metadata truthfully names the checked-out Git
revision, package certification remains anchored to the exact owner-tested
implementation commit rather than the later evidence-only commit. Two fresh
standard Windows worktrees at `c49da81` independently produced byte-identical
packages with SHA-256:

`de6c8b6652297f10ac3ed2f6b53975f376b1c8b962221876923f41bae3bf32aa`

Non-installing preflight passed on that exact regenerated package. Runtime
source and distributable bytes are therefore unchanged by this evidence
update; no package built from a documentation-only revision is substituted for
the owner-tested artifact.
