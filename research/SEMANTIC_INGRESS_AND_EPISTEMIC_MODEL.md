# Mosaic Semantic Ingress and Epistemic Model

**Status:** Accepted direction; experiment-gated
**Date:** 2026-08-01

This decision authorizes measurement, not new cognition, provider use, pawn/job authority, or
durable affect promotion.

## Direction

The highest-probability live bottleneck is **opportunity**, not retrieval sophistication. The
accepted v41-v44 chain remains offline/certified and outside the reachable RimWorld dialogue path.
Do not wire it merely because it exists. v42-v44 do not earn live runtime stages, durable appraisal
remains dormant/reference-only, and the dialogue outbox and reflection persistence remain intact.

Measure semantic ingress first. Preserve v41 for one equal-budget retrieval ablation only if live
telemetry shows repeated same-pair opportunities with rich prior histories; do not automatically
wire or delete it.

## Epistemic model

A global fact is not automatically part of every character's mind. Keep distinct whether an event:

1. happened;
2. involved the character;
3. was directly perceived;
4. was learned through a grounded source;
5. is currently known or remembered.

Prefer RimWorld's semantic structures over inference from raw scalars:

- factual episodes: HistoryEvent, PlayLog, Battle/BattleLog, and lifecycle transitions;
- character-relative evidence: participation, PawnObserver, and Thought memories;
- salience/long-horizon evidence: Tales, Records, guest/prisoner transitions, mental states,
  growth/life-stage changes, and WorldPawns continuity;
- durable autobiography/knowledge: provenance-bearing Mosaic state admitted under its own rules;
- ephemeral context: quests, conditions, threats, location, jobs, weather, current Thoughts, and
  similar present state without automatic autobiographical persistence.

Thoughts may merge, renew, stack, or replace, so they are subjective/attitudinal evidence rather
than unique factual IDs. Tales are positive salience signals, not a complete record. Reuse vanilla
battle clustering before inventing another combat episode mechanism. Distinct conceptual lanes do
not require distinct physical stores.

## Experiment 0A

Observe only current Mosaic social triggers, pair-linked Thought memories, pair-linked PlayLog
social interactions, Tales involving enrolled pawns, dialogue prepare/presentation outcomes, unique
and repeated pairs, and prior same-pair history depth.

Use bounded counters and a small recent-correlation buffer. Retain raw ticks and stable participant
IDs sufficient for later overlap analysis, but no raw dialogue, prompts, journal payloads, or private
memories. Do not count multiple channels from one plausible episode as independent opportunities.
Use a diagnostic +/-600-tick default and preserve enough timing information for 300/600/1200
sensitivity checks.

The telemetry causes no canonical writes, dialogue changes, experience-journal admission,
provider/model calls, pawn/job authority, HistoryEvent hook, or v41-v44 wiring. It establishes a
baseline and invents no product threshold.

If opportunity is sparse, add only the single highest-yield semantic source. If repeats are common
but histories shallow, improve coverage/episode formation. Only if repeat opportunities often have
at least four useful prior items may one equal-budget current/simple/v41 retrieval ablation run.
Experiment 0B remains separately gated on exact RimWorld 1.6 compatibility, 0A review, and expected
unique HistoryEvent coverage. v45 remains frozen.
