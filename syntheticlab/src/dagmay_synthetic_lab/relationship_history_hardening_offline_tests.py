from __future__ import annotations

from .relationship_claims import DETERMINISTIC_CLAUSE_SOURCE, RelationshipEvidence
from .relationship_history_selection import RelationshipHistoryItem, assessment_from_selected_evidence, select_relationship_evidence


def evidence(event_id: str, valence: str, clause: str) -> RelationshipEvidence:
    return RelationshipEvidence(event_id, "Mira", clause, valence, clause, DETERMINISTIC_CLAUSE_SOURCE)


def run() -> dict[str, object]:
    positive = RelationshipHistoryItem(evidence("P1", "POSITIVE", "Mira treated my wound"), 2, 0.9)
    false_negative = RelationshipHistoryItem(evidence("N1", "NEGATIVE", "Mira took my medicine"), 3, 1.0)
    correction = RelationshipHistoryItem(
        evidence("C1", "POSITIVE", "Mira corrected the medicine record"),
        20,
        1.0,
        retracts_evidence_id="N1",
    )
    selected = select_relationship_evidence((positive, false_negative, correction), counterpart="Mira")
    assert {item.evidence_id for item in selected} == {"P1"}
    assert assessment_from_selected_evidence("Mira", selected).disposition == "TRUST"

    duplicates = [
        RelationshipHistoryItem(evidence(f"D{i}", "POSITIVE", "Mira shared food with me"), 30 + i, 0.9)
        for i in range(10)
    ]
    distinct = RelationshipHistoryItem(evidence("P2", "POSITIVE", "Mira warned me before a raid"), 4, 0.85)
    selected = select_relationship_evidence((*duplicates, distinct), counterpart="Mira")
    assert len(selected) == 2
    assert len({item.summary for item in selected}) == 2
    assert "P2" in {item.evidence_id for item in selected}

    weak_correction = RelationshipHistoryItem(
        evidence("C2", "POSITIVE", "Mira disputed the medicine report"),
        21,
        1.0,
        evidence_quality=0.2,
        source_kind="RUMOR",
        retracts_evidence_id="N1",
    )
    selected = select_relationship_evidence((positive, false_negative, weak_correction), counterpart="Mira")
    assert "N1" in {item.evidence_id for item in selected}
    assert assessment_from_selected_evidence("Mira", selected).disposition == "MIXED"

    try:
        RelationshipHistoryItem(positive.evidence, 3, 0.9, retracts_evidence_id="P1")
    except ValueError:
        self_retraction_rejected = True
    else:
        self_retraction_rejected = False
    assert self_retraction_rejected
    return {
        "verified_retraction_applied": True,
        "duplicate_crowding_prevented": True,
        "weak_rumor_retraction_rejected": True,
        "self_retraction_rejected": True,
    }


if __name__ == "__main__":
    print(run())
