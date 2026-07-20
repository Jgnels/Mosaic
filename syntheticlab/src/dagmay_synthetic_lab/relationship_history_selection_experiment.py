from __future__ import annotations

from statistics import mean
import random

from .persistent_character_boundary import assert_persistent_character_objective
from .relationship_claims import DETERMINISTIC_CLAUSE_SOURCE, RelationshipEvidence, render_assessment
from .relationship_history_selection import RelationshipHistoryItem, assessment_from_selected_evidence, select_relationship_evidence


def _evidence(event_id: str, actor: str, valence: str, clause: str) -> RelationshipEvidence:
    return RelationshipEvidence(event_id, actor, clause, valence, clause, DETERMINISTIC_CLAUSE_SOURCE)


def _trial(seed: int) -> dict[str, float]:
    rng = random.Random(seed)
    counterpart = "Mira"
    core = (
        RelationshipHistoryItem(_evidence("P1", counterpart, "POSITIVE", "Mira rescued me from a fire"), 5, 0.95),
        RelationshipHistoryItem(_evidence("P2", counterpart, "POSITIVE", "Mira treated my wound"), 9, 0.90),
        RelationshipHistoryItem(_evidence("N1", counterpart, "NEGATIVE", "Mira took my medicine without permission"), 12, 0.95),
        RelationshipHistoryItem(_evidence("N2", counterpart, "NEGATIVE", "Mira broke our agreed guard shift"), 15, 0.90),
    )
    history = list(core)
    tick = 16
    for index in range(96):
        actor = rng.choice(("Tarin", "Venn", "Xara", "Oren"))
        valence = rng.choice(("POSITIVE", "NEGATIVE"))
        history.append(RelationshipHistoryItem(_evidence(f"D{index}", actor, valence, f"{actor} completed a colony task"), tick, rng.uniform(0.2, 0.8)))
        tick += 1
    for index in range(8):
        valence = "POSITIVE" if index % 2 == 0 else "NEGATIVE"
        history.append(RelationshipHistoryItem(_evidence(f"R{index}", counterpart, valence, "Mira was mentioned in a rumor"), tick, 0.9, 0.05, "RUMOR"))
        tick += 1

    selected = select_relationship_evidence(history, counterpart=counterpart)
    selected_ids = {item.evidence_id for item in selected}
    relevant = {item.evidence.evidence_id for item in core}
    assessment = assessment_from_selected_evidence(counterpart, selected)
    dialogue = render_assessment(assessment, selected)
    recency = sorted(history, key=lambda item: item.tick, reverse=True)[:4]
    return {
        "mosaic_recall": len(selected_ids & relevant) / len(relevant),
        "mosaic_rumor_rate": sum(item.evidence_id.startswith("R") for item in selected) / max(len(selected), 1),
        "mosaic_balanced": float(assessment.disposition == "MIXED"),
        "mosaic_dialogue_bounded": float(len(dialogue) <= 320),
        "recency_recall": len({item.evidence.evidence_id for item in recency} & relevant) / len(relevant),
        "recency_rumor_rate": sum(item.source_kind == "RUMOR" for item in recency) / len(recency),
    }


def run(seeds: int = 128) -> dict[str, object]:
    assert_persistent_character_objective(
        purpose="Preserve balanced relationship continuity under long-history distraction.",
        independent_value_areas=["relationships", "memory", "causal_coherence", "hallucination_resistance", "player_value"],
        design="Compare bounded provenance-aware selection with recency under direct-event, distractor, and rumor histories.",
    )
    trials = [_trial(seed) for seed in range(seeds)]
    return {
        "experiment_id": "MOSAIC-RELATIONSHIP-HISTORY-001",
        "classification": "PERSISTENT_CHARACTER_ENGINEERING",
        "provider_calls": 0,
        "canonical_mutation": False,
        "seeds": seeds,
        "summary": {key: mean(trial[key] for trial in trials) for key in trials[0]},
    }


if __name__ == "__main__":
    import json
    print(json.dumps(run(), indent=2, sort_keys=True))
