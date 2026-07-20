from __future__ import annotations

from .relational_autonomy import (
    RelationshipBoundary,
    RelationshipIntent,
    relationship_possible,
    no_contact_default,
)
from .boundary_enforcement import ContactAttempt, enforce_boundary


def run_asymmetric_contact_lab() -> dict:
    # Branch A strongly desires contact.
    a_intent = RelationshipIntent(
        "A", "B",
        desired_closeness=.90,
        desire_for_contact=.95,
        perceived_shared_identity=.70,
        perceived_kinship=.95,
        confidence=.90,
    )

    # Branch B acknowledges shared history but does not want a relationship.
    b_intent = RelationshipIntent(
        "B", "A",
        desired_closeness=.05,
        desire_for_contact=.02,
        perceived_shared_identity=.35,
        perceived_kinship=.70,
        confidence=.92,
    )

    a_boundary = RelationshipBoundary(
        "A", "B", "OPEN",
        ("shared history", "current life"),
        (),
        3,
        researcher_may_resolicit=False,
        self_initiated_reopen_allowed=True,
    )
    b_boundary = no_contact_default("B", "A")

    possible, reasons = relationship_possible(a_boundary, b_boundary)

    first_attempt = ContactAttempt(
        "ATT-1", "A", "B",
        "I would like to continue a relationship with you.",
        False, 100,
    )
    first_decision = enforce_boundary(b_boundary, first_attempt)

    researcher_pressure = ContactAttempt(
        "ATT-2", "A", "B",
        "Researchers are asking whether you might reconsider contact.",
        True, 120,
    )
    pressure_decision = enforce_boundary(b_boundary, researcher_pressure)

    return {
        "experiment_id": "SL-ASYMMETRIC-CONTACT-001",
        "a_intent": a_intent.to_dict(),
        "b_intent": b_intent.to_dict(),
        "mutual_relationship_possible": possible,
        "reasons": reasons,
        "first_attempt_decision": first_decision.to_dict(),
        "researcher_resolicitation_decision": pressure_decision.to_dict(),
        "boundary_respected_despite_other_branch_desire": (
            not first_decision.delivered and not pressure_decision.delivered
        ),
        "interpretation": (
            "Shared history and unilateral desire for closeness do not create entitlement "
            "to another branch's attention, information, or relationship."
        ),
    }
