from __future__ import annotations

from copy import deepcopy
import json
from pathlib import Path
import tempfile

from .relationship_claims import DETERMINISTIC_CLAUSE_SOURCE, RelationshipEvidence, render_assessment
from .relationship_history_selection import RelationshipHistoryItem, assessment_from_selected_evidence, select_relationship_evidence
from .relationship_history_snapshot import load_snapshot, restore_relationship_history, snapshot_relationship_history, write_snapshot_atomic


def ev(event_id: str, valence: str, clause: str) -> RelationshipEvidence:
    return RelationshipEvidence(event_id, "Mira", clause, valence, clause, DETERMINISTIC_CLAUSE_SOURCE)


def run() -> dict[str, object]:
    history = (
        RelationshipHistoryItem(ev("P1", "POSITIVE", "Mira treated my wound"), 3, 0.9),
        RelationshipHistoryItem(ev("N1", "NEGATIVE", "Mira took my medicine"), 5, 0.95),
        RelationshipHistoryItem(ev("C1", "POSITIVE", "Mira corrected the medicine record"), 8, 1.0, retracts_evidence_id="N1"),
        RelationshipHistoryItem(ev("N2", "NEGATIVE", "Mira broke our agreed guard shift"), 11, 0.9),
    )
    snapshot = snapshot_relationship_history(history, individual_id="IND-001", lineage_id="LIN-001")
    restored = restore_relationship_history(snapshot, expected_individual_id="IND-001", expected_lineage_id="LIN-001")

    before = select_relationship_evidence(history, counterpart="Mira")
    after = select_relationship_evidence(restored, counterpart="Mira")
    assert before == after
    before_dialogue = render_assessment(assessment_from_selected_evidence("Mira", before), before)
    after_dialogue = render_assessment(assessment_from_selected_evidence("Mira", after), after)
    assert before_dialogue == after_dialogue

    rejected = 0
    tampered = deepcopy(snapshot)
    tampered["history"][0]["importance"] = 0.0
    wrong_version = deepcopy(snapshot)
    wrong_version["snapshot_version"] = "UNKNOWN"
    extra_field = deepcopy(snapshot)
    extra_field["unexpected"] = True
    for candidate in (tampered, wrong_version, extra_field):
        try:
            restore_relationship_history(candidate)
        except ValueError:
            rejected += 1
    try:
        restore_relationship_history(snapshot, expected_individual_id="IND-WRONG")
    except ValueError:
        rejected += 1
    assert rejected == 4

    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / "relationship.json"
        write_snapshot_atomic(path, snapshot)
        assert load_snapshot(path, expected_individual_id="IND-001") == restored
        assert not path.with_name(path.name + ".tmp").exists()
        interrupted_temp = path.with_name(path.name + ".tmp")
        interrupted_temp.write_text('{"partial":', encoding="utf-8")
        assert load_snapshot(path, expected_individual_id="IND-001") == restored
        path.write_text('{"partial":', encoding="utf-8")
        try:
            load_snapshot(path)
        except ValueError:
            truncated_rejected = True
        else:
            truncated_rejected = False
        assert truncated_rejected

    return {
        "round_trip_exact": True,
        "dialogue_reproduced": True,
        "tamper_schema_identity_rejections": rejected,
        "interrupted_temp_preserved_last_good": True,
        "truncated_snapshot_rejected": True,
    }


if __name__ == "__main__":
    print(json.dumps(run(), indent=2, sort_keys=True))
