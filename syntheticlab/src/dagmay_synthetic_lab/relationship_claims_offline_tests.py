from __future__ import annotations

from .claim_grounding import evaluate_dialogue_claims
from .relationship_claims import (
    DETERMINISTIC_CLAUSE_SOURCE,
    RelationshipAssessment,
    RelationshipEvidence,
    render_assessment,
    validate_assessment,
)


def run() -> dict[str, object]:
    evidence = (
        RelationshipEvidence("P1", "Mira", "warned Rowan before a raid", "POSITIVE", "Mira warned me before a raid", DETERMINISTIC_CLAUSE_SOURCE),
        RelationshipEvidence("P2", "Mira", "shared food during a shortage", "POSITIVE", "Mira shared food with me during a shortage", DETERMINISTIC_CLAUSE_SOURCE),
        RelationshipEvidence("N1", "Mira", "took Rowan's medicine without permission", "NEGATIVE", "Mira took my medicine without permission", DETERMINISTIC_CLAUSE_SOURCE),
        RelationshipEvidence("N2", "Mira", "abandoned an agreed guard shift", "NEGATIVE", "Mira abandoned our agreed guard shift", DETERMINISTIC_CLAUSE_SOURCE),
    )
    mixed = RelationshipAssessment("Mira", "MIXED", ("P1", "P2"), ("N1", "N2"))
    rendered = render_assessment(mixed, evidence)
    assert "mixed" in rendered.lower()
    assert "temper" not in rendered.lower()
    assert "nature" not in rendered.lower()
    assert "Rowan" not in rendered
    assert "Mira warned me" in rendered
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
    invalid_clauses = (
        ("", "empty"),
        ("She warned me", "wrong actor"),
        ("Mira warned me.", "terminal punctuation"),
    )
    clause_rejections = 0
    for clause, _ in invalid_clauses:
        try:
            RelationshipEvidence("X", "Mira", "summary", "POSITIVE", clause, DETERMINISTIC_CLAUSE_SOURCE)
        except ValueError:
            clause_rejections += 1
    assert clause_rejections == len(invalid_clauses)
    provenance_rejections = 0
    for clause, source in (("Mira warned me", None), (None, DETERMINISTIC_CLAUSE_SOURCE), ("Mira warned me", "MODEL_OUTPUT")):
        try:
            RelationshipEvidence("X", "Mira", "summary", "POSITIVE", clause, source)
        except ValueError:
            provenance_rejections += 1
    assert provenance_rejections == 3
    return {"rendered": rendered, "invalid_claim_sets_rejected": rejected, "invalid_clauses_rejected": clause_rejections, "invalid_provenance_rejected": provenance_rejections}


if __name__ == "__main__":
    print(run())
