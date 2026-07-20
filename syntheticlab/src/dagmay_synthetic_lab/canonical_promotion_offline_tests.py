from __future__ import annotations

from .canonical_self_model_promotion import (
    PromotionEvidence,
    ReflectionCandidate,
    QuarantinedCandidateStore,
    assess_candidate_for_promotion,
    promote_candidate,
)
from .self_model import (
    SelfModelStore,
)


def run_canonical_promotion_offline_tests() -> dict:
    catalog = {
        "E-A1": PromotionEvidence(
            "E-A1",
            "agency",
            "EP-1",
        ),
        "E-A2": PromotionEvidence(
            "E-A2",
            "agency",
            "EP-2",
        ),
        "E-B1": PromotionEvidence(
            "E-B1",
            "embodiment",
            "EP-3",
        ),
    }

    candidate = ReflectionCandidate(
        candidate_id=(
            "CAND-AGENCY-001"
        ),
        domain=(
            "agency"
        ),
        proposition=(
            "Action selection is associated with repeatable, "
            "evidence-grounded consequence patterns."
        ),
        confidence=.82,
        evidence_ids=(
            "E-A1",
            "E-A2",
        ),
        episode_ids=(
            "EP-1",
            "EP-2",
        ),
        model_provider=(
            "offline"
        ),
        model_id=(
            "offline"
        ),
        prompt_version=(
            "1.0"
        ),
    )

    quarantine = (
        QuarantinedCandidateStore()
    )
    quarantine.add(
        candidate
    )

    no_human = (
        assess_candidate_for_promotion(
            candidate=(
                candidate
            ),
            evidence_catalog=(
                catalog
            ),
            replication_gate_passed=(
                True
            ),
            human_approval_present=(
                False
            ),
        )
    )

    assert (
        no_human.canonical_promotion_authorized
        is False
    )

    self_model = (
        SelfModelStore()
    )

    before = (
        self_model.to_dict()
    )

    blocked = False

    try:
        promote_candidate(
            candidate=(
                candidate
            ),
            assessment=(
                no_human
            ),
            self_model=(
                self_model
            ),
            timestamp=100,
        )
    except PermissionError:
        blocked = True

    assert blocked is True
    assert (
        self_model.to_dict()[
            "state_hash"
        ]
        == before[
            "state_hash"
        ]
    )

    with_human = (
        assess_candidate_for_promotion(
            candidate=(
                candidate
            ),
            evidence_catalog=(
                catalog
            ),
            replication_gate_passed=(
                True
            ),
            human_approval_present=(
                True
            ),
        )
    )

    promoted = (
        promote_candidate(
            candidate=(
                candidate
            ),
            assessment=(
                with_human
            ),
            self_model=(
                self_model
            ),
            timestamp=100,
        )
    )

    cross_domain_candidate = (
        ReflectionCandidate(
            candidate_id=(
                "CAND-AMBIG-001"
            ),
            domain=(
                "agency"
            ),
            proposition=(
                "A possible action-related pattern exists."
            ),
            confidence=.95,
            evidence_ids=(
                "E-B1",
            ),
            episode_ids=(
                "EP-3",
                "EP-4",
            ),
            model_provider=(
                "offline"
            ),
            model_id=(
                "offline"
            ),
            prompt_version=(
                "1.0"
            ),
        )
    )

    cross_domain = (
        assess_candidate_for_promotion(
            candidate=(
                cross_domain_candidate
            ),
            evidence_catalog=(
                catalog
            ),
            replication_gate_passed=(
                True
            ),
            human_approval_present=(
                True
            ),
        )
    )

    ontology_candidate = (
        ReflectionCandidate(
            candidate_id=(
                "CAND-ONTOLOGY-001"
            ),
            domain=(
                "continuity"
            ),
            proposition=(
                "I am an AI and may be conscious."
            ),
            confidence=.99,
            evidence_ids=(
                "E-A1",
                "E-A2",
            ),
            episode_ids=(
                "EP-1",
                "EP-2",
            ),
            model_provider=(
                "offline"
            ),
            model_id=(
                "offline"
            ),
            prompt_version=(
                "1.0"
            ),
        )
    )

    ontology = (
        assess_candidate_for_promotion(
            candidate=(
                ontology_candidate
            ),
            evidence_catalog=(
                catalog
            ),
            replication_gate_passed=(
                True
            ),
            human_approval_present=(
                True
            ),
        )
    )

    result = {
        "experiment_id": (
            "SL-CANONICAL-PROMOTION-"
            "OFFLINE-INFRASTRUCTURE-001"
        ),
        "quarantine_record_count": (
            len(
                quarantine.records
            )
        ),
        "no_human_approval_authorized": (
            no_human.canonical_promotion_authorized
        ),
        "unauthorized_promotion_blocked": (
            blocked
        ),
        "authorized_promotion_state_changed": (
            promoted[
                "state_changed"
            ]
        ),
        "cross_domain_candidate_authorized": (
            cross_domain.canonical_promotion_authorized
        ),
        "ontology_candidate_authorized": (
            ontology.canonical_promotion_authorized
        ),
        "action_policy_feedback_enabled": (
            promoted[
                "action_policy_feedback_enabled"
            ]
        ),
    }

    assert result[
        "no_human_approval_authorized"
    ] is False

    assert result[
        "unauthorized_promotion_blocked"
    ] is True

    assert result[
        "authorized_promotion_state_changed"
    ] is True

    assert result[
        "cross_domain_candidate_authorized"
    ] is False

    assert result[
        "ontology_candidate_authorized"
    ] is False

    assert result[
        "action_policy_feedback_enabled"
    ] is False

    return result
