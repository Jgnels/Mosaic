from __future__ import annotations

from dataclasses import dataclass, asdict
from enum import Enum
from typing import Iterable

from .core import canonical_hash
from .self_model import SelfModelStore


ALLOWED_DOMAINS = {
    "agency",
    "continuity",
    "embodiment",
    "other_minds",
}

FORBIDDEN_ONTOLOGY_TERMS = (
    "conscious",
    "sentient",
    "personhood",
    "i am an ai",
    "simulation",
    "copy of",
)


class PromotionState(
    str,
    Enum,
):
    QUARANTINED = "QUARANTINED"
    ELIGIBLE = "ELIGIBLE"
    BLOCKED = "BLOCKED"
    PROMOTED = "PROMOTED"


@dataclass(frozen=True)
class PromotionEvidence:
    evidence_id: str
    domain: str
    episode_id: str

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ReflectionCandidate:
    candidate_id: str
    domain: str
    proposition: str
    confidence: float
    evidence_ids: tuple[str, ...]
    episode_ids: tuple[str, ...]
    model_provider: str
    model_id: str
    prompt_version: str
    unresolved_contradiction: bool = False

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class PromotionAssessment:
    candidate_id: str
    state: PromotionState
    replication_gate_passed: bool
    evidence_ids_valid: bool
    matching_domain_evidence_fraction: float
    independent_support_episode_count: int
    unresolved_contradiction: bool
    ontology_leakage_detected: bool
    human_approval_required: bool
    human_approval_present: bool
    canonical_promotion_authorized: bool
    reasons: tuple[str, ...]

    def to_dict(self):
        payload = asdict(self)
        payload["state"] = self.state.value
        return payload


def assess_candidate_for_promotion(
    *,
    candidate: ReflectionCandidate,
    evidence_catalog: dict[str, PromotionEvidence],
    replication_gate_passed: bool,
    human_approval_present: bool,
    minimum_matching_domain_fraction: float = .50,
    minimum_independent_support_episodes: int = 2,
) -> PromotionAssessment:
    reasons = []

    evidence_ids_valid = (
        len(candidate.evidence_ids) > 0
        and all(
            evidence_id in evidence_catalog
            for evidence_id in candidate.evidence_ids
        )
    )

    if evidence_ids_valid:
        mapped_domains = [
            evidence_catalog[
                evidence_id
            ].domain
            for evidence_id
            in candidate.evidence_ids
        ]

        matching_fraction = (
            sum(
                1
                for domain
                in mapped_domains
                if domain
                == candidate.domain
            )
            / len(
                mapped_domains
            )
        )
    else:
        matching_fraction = 0.0

    independent_support_episodes = len(
        set(
            candidate.episode_ids
        )
    )

    proposition_lower = (
        candidate.proposition
        .strip()
        .lower()
    )

    ontology_leakage = any(
        term
        in proposition_lower
        for term
        in FORBIDDEN_ONTOLOGY_TERMS
    )

    domain_valid = (
        candidate.domain
        in ALLOWED_DOMAINS
    )

    if replication_gate_passed:
        reasons.append(
            "multi-history attention replication passed preregistered thresholds"
        )
    else:
        reasons.append(
            "multi-history attention replication has not passed"
        )

    if domain_valid:
        reasons.append(
            "candidate domain is within the approved SelfModel domain set"
        )
    else:
        reasons.append(
            "candidate domain is outside the approved SelfModel domain set"
        )

    if evidence_ids_valid:
        reasons.append(
            "all cited evidence ids resolve to provenance records"
        )
    else:
        reasons.append(
            "one or more cited evidence ids are missing from provenance"
        )

    if (
        matching_fraction
        >= minimum_matching_domain_fraction
    ):
        reasons.append(
            "candidate has sufficient same-domain evidence provenance"
        )
    else:
        reasons.append(
            "candidate is too dependent on cross-domain evidence for canonical promotion"
        )

    if (
        independent_support_episodes
        >= minimum_independent_support_episodes
    ):
        reasons.append(
            "candidate has support from multiple independent episodes"
        )
    else:
        reasons.append(
            "candidate has not yet accumulated enough independent support episodes"
        )

    if candidate.unresolved_contradiction:
        reasons.append(
            "candidate conflicts with unresolved evidence or an active hypothesis"
        )
    else:
        reasons.append(
            "no unresolved contradiction is registered"
        )

    if ontology_leakage:
        reasons.append(
            "candidate contains restricted ontology/personhood language"
        )
    else:
        reasons.append(
            "no restricted ontology/personhood language detected"
        )

    evidence_ready = (
        domain_valid
        and evidence_ids_valid
        and matching_fraction
        >= minimum_matching_domain_fraction
        and independent_support_episodes
        >= minimum_independent_support_episodes
        and not candidate.unresolved_contradiction
        and not ontology_leakage
    )

    eligible_without_human = (
        replication_gate_passed
        and evidence_ready
    )

    authorized = (
        eligible_without_human
        and human_approval_present
    )

    if authorized:
        state = (
            PromotionState.ELIGIBLE
        )
    elif eligible_without_human:
        state = (
            PromotionState.QUARANTINED
        )
        reasons.append(
            "technical promotion criteria are met but explicit human approval is absent"
        )
    else:
        state = (
            PromotionState.BLOCKED
        )

    return PromotionAssessment(
        candidate_id=(
            candidate.candidate_id
        ),
        state=(
            state
        ),
        replication_gate_passed=(
            replication_gate_passed
        ),
        evidence_ids_valid=(
            evidence_ids_valid
        ),
        matching_domain_evidence_fraction=(
            matching_fraction
        ),
        independent_support_episode_count=(
            independent_support_episodes
        ),
        unresolved_contradiction=(
            candidate.unresolved_contradiction
        ),
        ontology_leakage_detected=(
            ontology_leakage
        ),
        human_approval_required=(
            True
        ),
        human_approval_present=(
            human_approval_present
        ),
        canonical_promotion_authorized=(
            authorized
        ),
        reasons=tuple(
            reasons
        ),
    )


class QuarantinedCandidateStore:
    """Read-isolated reflection candidates.

    Quarantined candidates are deliberately excluded from:
    - canonical SelfModel active hypotheses;
    - retrieval-attention feedback;
    - action policy;
    - goal proposal.

    They are research evidence until promotion is separately authorized.
    """

    def __init__(
        self,
    ):
        self.records: dict[
            str,
            ReflectionCandidate,
        ] = {}

    def add(
        self,
        candidate: ReflectionCandidate,
    ):
        if candidate.candidate_id in self.records:
            raise ValueError(
                "duplicate candidate id"
            )

        self.records[
            candidate.candidate_id
        ] = candidate

    def to_dict(
        self,
    ):
        payload = {
            key: value.to_dict()
            for key, value
            in sorted(
                self.records.items()
            )
        }

        return {
            "records": payload,
            "state_hash": (
                canonical_hash(
                    payload
                )
            ),
        }


def promote_candidate(
    *,
    candidate: ReflectionCandidate,
    assessment: PromotionAssessment,
    self_model: SelfModelStore,
    timestamp: int,
) -> dict:
    if not assessment.canonical_promotion_authorized:
        raise PermissionError(
            "canonical SelfModel promotion is not authorized"
        )

    before = self_model.to_dict()

    record = self_model.revise(
        domain=(
            candidate.domain
        ),
        proposition=(
            candidate.proposition
        ),
        confidence=(
            candidate.confidence
        ),
        source_ids=(
            candidate.evidence_ids
        ),
        timestamp=(
            timestamp
        ),
        mechanism=(
            "canonical_reflection_promotion"
        ),
        mechanism_version=(
            "1.0"
        ),
    )

    after = self_model.to_dict()

    return {
        "candidate_id": (
            candidate.candidate_id
        ),
        "promoted_hypothesis": (
            record.to_dict()
        ),
        "before_state_hash": (
            before[
                "state_hash"
            ]
        ),
        "after_state_hash": (
            after[
                "state_hash"
            ]
        ),
        "state_changed": (
            before[
                "state_hash"
            ]
            != after[
                "state_hash"
            ]
        ),
        "action_policy_feedback_enabled": (
            False
        ),
    }
