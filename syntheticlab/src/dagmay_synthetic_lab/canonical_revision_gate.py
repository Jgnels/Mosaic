from __future__ import annotations

from dataclasses import dataclass, asdict
from enum import Enum


class EvidenceProvenanceClass(str, Enum):
    ANALYTICAL_FIXTURE = "ANALYTICAL_FIXTURE"
    LIVED_BRANCH_HISTORY = "LIVED_BRANCH_HISTORY"


@dataclass(frozen=True)
class CanonicalRevisionCandidate:
    candidate_id: str
    branch_id: str
    domain: str
    prior_hypothesis_id: str
    decision: str
    updated_proposition: str
    updated_confidence: float
    evidence_ids: tuple[str, ...]
    independent_episode_ids: tuple[str, ...]
    provenance_class: EvidenceProvenanceClass
    unresolved_contradiction: bool = False

    def to_dict(self):
        payload = asdict(self)
        payload["provenance_class"] = self.provenance_class.value
        return payload


@dataclass(frozen=True)
class CanonicalRevisionAssessment:
    candidate_id: str
    falsifiability_gate_passed: bool
    lived_history_required: bool
    lived_history_present: bool
    independent_episode_count: int
    unresolved_contradiction: bool
    human_approval_required: bool
    human_approval_present: bool
    authorized: bool
    reasons: tuple[str, ...]

    def to_dict(self):
        return asdict(self)


def assess_canonical_revision(
    *,
    candidate: CanonicalRevisionCandidate,
    falsifiability_gate_passed: bool,
    human_approval_present: bool,
    minimum_independent_episodes: int = 2,
) -> CanonicalRevisionAssessment:
    reasons = []

    lived = (
        candidate.provenance_class
        == EvidenceProvenanceClass.LIVED_BRANCH_HISTORY
    )

    episodes = len(
        set(candidate.independent_episode_ids)
    )

    if not falsifiability_gate_passed:
        reasons.append(
            "belief falsifiability gate has not passed"
        )
    else:
        reasons.append(
            "belief falsifiability gate passed"
        )

    if lived:
        reasons.append(
            "revision evidence belongs to lived branch history"
        )
    else:
        reasons.append(
            "analytical fixture evidence cannot directly rewrite canonical SelfModel"
        )

    if episodes < minimum_independent_episodes:
        reasons.append(
            "insufficient independent lived episodes"
        )
    else:
        reasons.append(
            "independent episode threshold satisfied"
        )

    if candidate.unresolved_contradiction:
        reasons.append(
            "unresolved contradiction blocks revision"
        )
    else:
        reasons.append(
            "no unresolved contradiction registered"
        )

    technically_ready = (
        falsifiability_gate_passed
        and lived
        and episodes >= minimum_independent_episodes
        and not candidate.unresolved_contradiction
    )

    authorized = (
        technically_ready
        and human_approval_present
    )

    if technically_ready and not human_approval_present:
        reasons.append(
            "technical criteria satisfied but explicit human approval is absent"
        )

    return CanonicalRevisionAssessment(
        candidate_id=candidate.candidate_id,
        falsifiability_gate_passed=falsifiability_gate_passed,
        lived_history_required=True,
        lived_history_present=lived,
        independent_episode_count=episodes,
        unresolved_contradiction=candidate.unresolved_contradiction,
        human_approval_required=True,
        human_approval_present=human_approval_present,
        authorized=authorized,
        reasons=tuple(reasons),
    )
