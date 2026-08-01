# Mosaic Research Executive Summary

## Objective

Mosaic is a persistent-character architecture for simulated worlds, with RimWorld as the primary environment. Its central claim is architectural rather than metaphysical: **a character should remain the same individual because its durable identity and causal history persist, not because the same language model continues generating text.**

The project does not pursue or claim consciousness or sentience. The binding `PERSISTENT_CHARACTER_BOUNDARY.md` rejects optimization for deceptive pseudo-consciousness and treats an unexpected credible moral-status signal as a pause-and-preserve condition rather than a success metric.

## Core architecture

Mosaic separates:

- environmental fact/event evidence;
- perception/admission provenance;
- autobiographical and social history;
- retrieved context;
- appraisal/interpretation;
- bounded provisional state;
- durable canonical character state;
- generated language/presentation.

Language models, where used, are replaceable and subordinate. Model output is an untrusted proposal. Durable mutation requires separately validated authority and provenance.

## What has been demonstrated

The project has substantial offline and owner-operated live evidence across its earlier RimWorld line, including identity/persistence recovery, observer operation, dialogue presentation/admission, bounded replies, save/reload non-replay, cross-encounter continuity, and read-only conversation history.

The newest accepted research line (v41–v44R) adds progressively grounded contextual interpretation:

- **v41:** contextual developmental retrieval;
- **v42:** grounded context materialization;
- **v43:** grounded compound appraisal;
- **v44R / 0.3I:** bounded session-local contextual provisional reaction admission.

v44R is accepted at commit `89be3f89fdd3802abd92f0b2821dd7ec28cfc0a1`. Its repaired test suite and integration scenario are substantive rather than name/count-only gates. See `CANONICAL_CHECKPOINT.md` for exact evidence identities.

## Important limitations

The project has deliberately strong contract, provenance, persistence, determinism, and adversarial verification. That rigor has caught real defects, including experimental payload leakage, recovery/checkpoint inconsistencies, unsupported claim language, and a misleading v44 test translation.

However, the active project also has significant scope debt:

- duplicated frozen/current RimWorld trees;
- a large SyntheticLab surface containing both useful references and historical/pre-pivot research;
- stale root/bootstrap documentation;
- many offline architectural layers whose newest end-to-end player value has not yet been demonstrated in the game;
- test-count and documentation volume that can create false confidence or excessive AI context cost if treated as success metrics.

## Current direction

The project has adopted an austerity/value rule: **do not add architecture merely for completeness.** New work should eliminate a demonstrated risk, enable an observable persistent-character capability, or satisfy a necessary reproducibility requirement more simply than the alternatives.

The immediate roadmap is:

1. consolidate the repository without adding cognition;
2. obtain external source-level red-team review before major pruning;
3. build a live 0.3 shadow vertical slice from a real RimWorld event through v41 → v42 → v43 → v44;
4. measure player-visible continuity, contextual appropriateness, distinctiveness, naturalness, repetition, memorability, and enjoyment;
5. reassess whether later architecture such as v45 is justified.

The long-term target is not “the most elaborate research architecture.” It is a durable character system that remains trustworthy internally **and** produces clearly perceptible, enjoyable character continuity during long-running simulation play.
