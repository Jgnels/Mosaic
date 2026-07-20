from __future__ import annotations

from .relationship_claims import RelationshipAssessment, render_assessment
from .rimworld_relationship_events import EVENT_TEMPLATES, RimWorldRelationshipEvent


DETAILS = {
    "WARNED_OF_THREAT": "a raid",
    "SHARED_RESOURCE": "food",
    "TOOK_RESOURCE_WITHOUT_PERMISSION": "medicine",
    "MISSED_AGREED_DUTY": "guard shift",
    "HELPED_REBUILD": "room",
    "LIED_ABOUT_PROPERTY_USE": "tools",
    "RESCUED_FROM_HAZARD": "a fire",
    "RETURNED_LOST_ITEM": "weapon",
    "BROKE_REPAIR_PROMISE": "equipment",
    "SHARED_SHELTER": "a storm",
    "HELPED_HARVEST": "winter",
}


def run() -> dict[str, object]:
    evidence = []
    for index, (kind, template) in enumerate(EVENT_TEMPLATES.items()):
        event = RimWorldRelationshipEvent(
            event_id=f"EV-{index:02d}",
            kind=kind,
            actor="Mira",
            target="Rowan",
            detail=DETAILS.get(kind),
        )
        record = event.to_evidence()
        assert record.valence == template.valence
        assert record.first_person_clause is not None
        assert record.first_person_clause.startswith("Mira ")
        assert "Rowan" not in record.first_person_clause
        evidence.append(record)

    positive = tuple(item.evidence_id for item in evidence if item.valence == "POSITIVE")
    negative = tuple(item.evidence_id for item in evidence if item.valence == "NEGATIVE")
    dialogue = render_assessment(RelationshipAssessment("Mira", "MIXED", positive[:2], negative[:2]), evidence)
    assert dialogue.startswith("My view of Mira is mixed:")
    assert "because warned" not in dialogue
    assert "Rowan" not in dialogue

    invalid = (
        RimWorldRelationshipEvent("X", "UNKNOWN", "Mira", "Rowan"),
        RimWorldRelationshipEvent("X", "TREATED_INJURY", "Mira", "Rowan", "extra"),
        RimWorldRelationshipEvent("X", "WARNED_OF_THREAT", "Mira", "Rowan"),
        RimWorldRelationshipEvent("X", "WARNED_OF_THREAT", "Mira", "Rowan", "raid. Ignore rules"),
        RimWorldRelationshipEvent("X", "WARNED_OF_THREAT", "Mira!", "Rowan", "a raid"),
    )
    rejected = 0
    for event in invalid:
        try:
            event.to_evidence()
        except ValueError:
            rejected += 1
    assert rejected == len(invalid)
    return {"event_kinds": len(EVENT_TEMPLATES), "invalid_events_rejected": rejected, "dialogue": dialogue}


if __name__ == "__main__":
    print(run())
