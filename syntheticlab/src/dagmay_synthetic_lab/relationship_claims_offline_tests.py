from __future__ import annotations

from .claim_grounding import evaluate_dialogue_claims
from .relationship_claims import (
    RelationshipAssessment,
    RelationshipEvidence,
    render_assessment,
    validate_assessment,
)


def run() -> dict[str, object]:
    evidence = (
        RelationshipEvidence("P1", "Mira", "Mira warned me before a raid", "POSITIVE"),
        RelationshipEvidence("P2", "Mira", "Mira shared food during a shortage", "POSITIVE"),
        RelationshipEvidence("N1", "Mira", "Mira took my medicine without permission", "NEGATIVE"),
        RelationshipEvidence("N2", "Mira", "Mira abandoned an agreed guard shift", "NEGATIVE"),
    )
    mixed = RelationshipAssessment("Mira", "MIXED", ("P1", "P2"), ("N1", "N2"))
    rendered = render_assessment(mixed, evidence)
    assert "mixed" in rendered.lower()
    assert "temper" not in rendered.lower()
    assert "nature" not in rendered.lower()
    assert evaluate_dialogue_claims(
        rendered,
        cited_evidence_ids=mixed.positive_evidence_ids + mixed.negative_evidence_ids,
        allowed_evidence_ids=[item.evidence_id for item in evidence],
    ).status == "ACCEPT"

    invalid_cases = (
        RelationshipAssessment("Mira", "MIXED", ("P1",), ()),
        RelationshipAssessment("Mira", "TRUST", ("P1",), ("N1",)),
        RelationshipAssessment("Mira", "DISTRUST", (), ("P1",)),
        RelationshipAssessment("Mira", "TRUST", ("FAKE",), ()),
    )
    rejected = 0
    for candidate in invalid_cases:
        try:
            validate_assessment(candidate, evidence)
        except ValueError:
            rejected += 1
    assert rejected == len(invalid_cases)
    return {"rendered": rendered, "invalid_claim_sets_rejected": rejected}


if __name__ == "__main__":
    print(run())
