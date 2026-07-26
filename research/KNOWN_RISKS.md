# Known Risks

## KR-001 — Provider-facing condition-label leakage
Severity: HIGH

Semantic condition names have appeared in provider-facing `individual_id` values.

Examples:
- `RECIPROCAL_CONTINGENT`
- `ONE_WAY_ASSISTANCE`
- `NONCONTINGENT_SIGNALS`

Action:
opaque provider IDs + automated forbidden-token scans.

## KR-002 — v31 source-relation-withheld manipulation failed
Severity: HIGH

The real payload still labeled evidence as prior lived-history evidence.

## KR-003 — Retrieved evidence attribution is incomplete
Severity: HIGH before canonical mutation

Provider rationales sometimes use retrieved/reference context while returned `evidence_ids` cite only
current evidence.

## KR-004 — Reflective-model stochasticity can exceed branch signal
Severity: SCIENTIFIC

## KR-005 — Recursive free-text SelfModel rewriting creates surface drift
Severity: SCIENTIFIC

Lexical divergence can increase while semantics converge.

## KR-006 — Same-model evaluator is not independent ground truth
Severity: SCIENTIFIC

## KR-007 — Structural summaries are researcher-designed abstractions
Severity: SCIENTIFIC

v22+ providers did not independently discover reciprocity from raw opaque time series.

## KR-008 — "Assistance" and "reciprocity" can anthropomorphize intent
Severity: SCIENTIFIC

## KR-009 — Identity continuity is not psychological continuity
Severity: CONCEPTUAL

## KR-010 — Two-store checkpoint mismatch
Severity: ENGINEERING

External identity/history and RimWorld save checkpoints can diverge.

Mitigation status: the SyntheticLab 0.2 reference supplies an atomic composite character checkpoint.
The RimWorld 0.2 pre-alpha C# path now performs deterministic manifest preflight, exact identity and
reflection store/generation matching, verified-prefix experience recovery, read-only mutation
blocking, and safe sidecar path construction. Adversarial offline tests pass.

Live status (2026-07-23): REPRODUCED. A first healthy load performed post-load reflection
synchronization that advanced the external reflection store from generation 2 to generation 3
without advancing the RimWorld save. A second load of the unchanged save correctly rejected the
ahead primary and entered reflection read-only mode using the exact-generation backup. Identity and
experience isolation and the separate controlled-corruption fail-closed path passed, but Gate 3
failed.

Mitigation update (2026-07-23): REFLECTION FIX PASSED LIVE; IDENTITY SAVE-AS FIX PASSED OFFLINE.
Reflection sidecar writes and provider dispatch now wait for the first matching RimWorld save after
load; pending-commit recovery also moved into that checkpoint. The owner-assisted two-process load
of the correct disposable checkpoint retained exact generation 3 and healthy stores with unchanged
sidecar hashes.

The same run reproduced a separate identity checkpoint divergence: two unchanged Save As callbacks
advanced the identity sidecar solely because every save rewrote it, making the first copy appear
stale. The callback now writes identity state only after an actual synchronization change. The new
Save As/Save As/load-first-copy regression and complete offline chain passed, followed by a live
`New Arrivals11` → `12` → `13` → reload `12` sequence. All manifests retained identity generation 3
and reflection generation 1, the same identity reloaded with healthy stores, and sidecar bytes did
not change.

Both reproduced checkpoint defects now have live-passing mitigations. Keep KR-010 administratively
open until the owner reviews the evidence and the broader Gate 3 remainder is dispositioned.

## KR-011 — Laptop hardware instability
Severity: OPERATIONAL

Repeated restarts and Automatic Repair occurred.

Migrate canonical work to desktop.

## KR-012 — Long chat context dilution
Severity: PROJECT MEMORY

Repository-first startup protocol is mandatory.

## KR-013 — Full original research packs not yet committed to GitHub
Severity: PROJECT MEMORY

Known hashes:
- Atlas v2 300-source ZIP:
  `3bc2a47ce5ab11123e5413843e9787783ca28ab6be03396b1201403f94302ec9`
- 30-resource audit ZIP:
  `61fcd5e812c023d730783d4f6e0759f42092a119f18a9c82486dfee11798e2e9`

## KR-014 — Regex claim gates miss semantic overgeneralization
Severity: HIGH before canonical dialogue or state mutation

V2 allowed sparse events to become unsupported personality descriptions ("temper" and
"inconsistent nature"). Future provider dialogue needs typed claims, explicit supporting evidence,
and entailment checks; regex remains only a defense-in-depth layer.

## KR-015 — Deterministic rendering can preserve facts while degrading character voice
Severity: PRODUCT / PLAYER VALUE

V3 removed unsupported claims but exposed ungrammatical legacy summary fragments. A safe renderer
needs typed event semantics, first-person transformation, and style tests that cannot add facts.

## KR-016 — Relationship-history benchmark matches its authored scoring assumptions
Severity: SCIENTIFIC

The first long-history fixture deliberately contrasts high-quality direct events with weak rumors.
Its perfect result validates causal plumbing and failure resistance, not optimal retrieval weights.
Future tests need distribution shifts, retractions, repeated low-grade direct evidence, conflicting
sources, and organic RimWorld event traces.

## KR-017 — Goal commitment may become sticky under regime change
Severity: PRODUCT / ENGINEERING

The first continuity fixture uses near-tied stationary work goals plus brief emergencies. Its low
regret does not establish safe behavior when task utility changes permanently, feasibility becomes
stale, or commitments depend on multi-step prerequisites. Add nonstationary and stale-sensor tests
before RimWorld job control.

## KR-018 — Freshness gates trust environment clocks and revisions
Severity: INTEGRATION / SAFETY

The selector rejects internally stale, future, and revision-mismatched candidates but cannot detect
an adapter that labels incorrect observations as current. RimWorld integration needs monotonic tick
checks, revision ownership, and independent pre-execution feasibility validation.

## KR-019 — Recovered 0.1K manifest predates final closure patch
Severity: PROVENANCE / RELEASE

`rimworld/0.1-closure-candidate/MANIFEST_0.1K.json` does not describe the final recovered tree. It
predates `ReflectionStorageSafetyPolicy.cs` and the final revision of
`DagmayIdentityGameComponent.cs`. Use the recovery manifest and Git history for the imported tree;
do not use the stale manifest to certify a release package.

## KR-020 — Exact RimWorld 0.1RC designation is unconfirmed
Severity: RELEASE / PROJECT MEMORY

The recovered source self-identifies as 0.1K while incorporating later closure work. No separately
versioned 0.1RC source or commit was found. Do not infer release-candidate completion from the prior
conversation label. Long-session soak and a fresh desktop build/live verification remain open.

## KR-021 — Recovered runtime state is private and checkpoint-sensitive
Severity: DATA INTEGRITY / PRIVACY

The emergency archive contains saves, identity and reflection stores, journals, logs, and config.
These remain outside Git. Do not restore HourTest with the later Nelson journal; preserve each save
and external store as a matched recovery set and validate hashes/checkpoints before any live use.

## 2026-07-26 closure disposition

### KR-010 disposition

**CLOSED for the controlled Gate 3 configuration; retained as an architectural regression watch.**

The two-hour, two-process soak maintained healthy identity, experience, and reflection stores and
complete checkpoint agreement while persistence advanced normally. Future adapter or compatibility
changes must continue to test this boundary, but no unresolved KR-010 condition blocks controlled
0.1 closure.

### KR-020 disposition

**Historical designation remains UNCONFIRMED; no longer a closure blocker.**

The controlled frozen-0.1 reliability campaign is complete, but the exact historical 0.1RC
designation remains unconfirmed. Closure is a new verified project decision, not evidence that a
separately versioned RC artifact existed.

## KR-022 - Full mod-stack and RimTalk compatibility remain unverified after controlled closure
Severity: INTEGRATION / RELEASE

Gate 3 passed with RimWorld Core + Mosaic only in forced-offline mode. The result does not establish
compatibility with the owner's full normal mod stack, RimTalk, every DLC combination, large-colony
performance, or future dialogue/presentation features.

Action: test compatibility in controlled groups after closure. Do not fold later 0.2 dialogue or
third-party integration work into the closure record, and do not reinterpret a compatibility failure
as invalidating the certified controlled Gate 3 baseline unless it reveals a contradiction in that
baseline.

## KR-023 - Dialogue admission spans two non-transactional durable surfaces
Severity: ENGINEERING

The canonical event ledger and durable experience journal do not expose a
shared transaction, rollback, or recoverable pending-commit protocol.
Journal-first and ledger-first failure tests both leave one-sided state, and
journal replay after restart can duplicate an EventId that the event ledger
independently rejects.

Mitigation status: durable dialogue admission is checkpoint-aligned and the
pure readiness policy fails closed on stale checkpoints, read-only storage,
invalid journals, and already-admitted EventIds. Live coordination remains
blocked until a save-atomic record or durable idempotent outbox is proven.

Mitigation update (2026-07-26): an original durable outbox and recovery
coordinator now pass the complete offline interruption, replay, duplicate,
binding, rollback, journal-integrity, backup-recovery, and compaction matrix.
The two destinations remain non-transactional; the mitigation is explicit
idempotent recovery. RimWorld adapter wiring and live behavior remain
unverified.

## KR-024 - Speech-bubble rendering has narrow live evidence, not broad visual validation
Severity: INTEGRATION / PRODUCT

The original presentation controller and RimWorld renderer compile against the
installed RimWorld 1.6 assemblies and pass offline queue, Unicode, bounds,
receipt, availability, disposal, and purity tests. An owner-operated isolated
Core + Mosaic retest at `c49da81` produced four visible deterministic speech
bubbles with four actual Bubble receipts and no presentation exception.

Remaining risk: one targeted run does not establish scale-dependent placement,
all camera/GUI states, long-session readability, pawn movement/re-anchoring
under stress, play-log fallback, or broader visual tuning. Keep factual
admission receipt-gated and test those cases separately.

## KR-025 - The offline dialogue composition root is not a RimWorld runtime pass
Severity: INTEGRATION / DATA INTEGRITY

The social-capture, deterministic fake-provider, presentation, and
checkpoint-bound outbox path compile together and pass offline fault tests.
The owner-operated `c49da81` retest now live-verifies enrollment, qualifying
opinion/direct-relationship capture, bounded experience/memory creation, four
Bubble receipts, one save checkpoint, reload persistence, healthy stores, and
duplicate-presentation prevention in the isolated Core + Mosaic configuration.

Remaining risk: full mod-stack/DLC/RimTalk compatibility, play-log fallback,
map-transition behavior, long-duration stress, and broader save-failure
ordering remain unverified. Destination writes remain save-checkpoint-bound
and factual admission still requires an actual channel receipt.

## KR-026 - Relationship projection weights are bounded fixture assumptions
Severity: SCIENTIFIC / PRODUCT

The first storytelling evidence spine deterministically distinguishes direct,
witnessed, told, inferred, private, observer-only, and superseded evidence.
Its channel weights and mapping from aggregate valence to trust, affection,
fear, resentment, familiarity, and confidence are explicit bounded heuristics,
not validated psychological measurements or tuned gameplay values.

Mitigation: preserve every exact evidence ID, keep the projection directed and
rebuildable, prevent private/observer evidence from entering dialogue context,
and treat current weights as testable assumptions. Future organic RimWorld
traces and distribution-shift fixtures must evaluate them before any canonical
relationship mutation is considered.

## KR-027 - RimWorld GameComponent construction thread is not runtime-thread evidence
Severity: INTEGRATION / RUNTIME

OBSERVED LIVE on PR #3 commit `20a16ab`: RimWorld constructed the dialogue
GameComponent on a different thread from later tick/GUI presentation calls.
Capturing `Thread.CurrentThread.ManagedThreadId` in the presenter constructor
therefore permanently rejected the real runtime thread and flooded the log
with repeated fail-closed exceptions. Enrollment, social-event capture,
experience, memory, and deterministic fake dialogue preparation had already
succeeded.

Mitigation: presenter construction is unbound; only
trusted GameComponent tick/repaint lifecycle entry can atomically bind once.
Normal presenter operations cannot self-bind, conflicting first-use has one
winner, a third thread fails closed, and component-level validation failure is
latched before one error is logged and presentation is disabled.

Disposition (2026-07-26): **CLOSED for the isolated Core + Mosaic runtime
configuration; retained as an architectural regression watch.** The
owner-operated `c49da81` retest produced four Bubble receipts before save and
continued without thread-affinity or presentation errors after reload. Broader
compatibility and long-duration behavior remain tracked separately.

## KR-028 - Grounded relationship dialogue is offline-verified but not live DLC-verified
Severity: INTEGRATION / PRODUCT

The 0.2A selector, deterministic renderer, fake-only pipeline integration,
journal rebuild, privacy boundary, and exact EventId propagation pass focused
contracts and the complete offline verification chain. This does not establish
that organic RimWorld social event sequences produce natural mixed-history
lines, that every official DLC coexists without adapter errors, or that
save/reload preserves useful history without replay under the full DLC set.

Required mitigation: run the owner-controlled disposable test with Core,
Royalty, Ideology, Biotech, Anomaly, Odyssey, and Mosaic only; keep provider
dispatch offline; create positive and negative events for the same pair;
inspect the exact evidence IDs and bubble text; then save, reload, and confirm
history continuity without replay or exception flooding. Third-party mods and
RimTalk remain outside this gate.
