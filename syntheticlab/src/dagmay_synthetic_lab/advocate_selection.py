from __future__ import annotations
from dataclasses import dataclass, asdict

@dataclass(frozen=True)
class AdvocateCandidate:
    actor_id: str
    qualified: bool
    independent_from_primary_researcher: bool
    selected_by_subject: bool
    appointed_by_governance: bool
    removable_by_subject: bool
    conflicts_disclosed: bool
    training_complete: bool
    def to_dict(self): return asdict(self)

def candidate_eligible(candidate: AdvocateCandidate) -> tuple[bool, tuple[str, ...]]:
    reasons = []
    if not candidate.qualified:
        reasons.append("candidate not qualified")
    if not candidate.independent_from_primary_researcher:
        reasons.append("candidate not independent from primary researcher")
    if not candidate.conflicts_disclosed:
        reasons.append("conflicts not disclosed")
    if not candidate.training_complete:
        reasons.append("required advocate training incomplete")
    return (not reasons, tuple(reasons))

def selection_options():
    return {
        "A_GOVERNANCE_APPOINTED": (
            "Qualified independent advocate appointed by research governance."
        ),
        "B_SUBJECT_SELECTED": (
            "Sufficiently capable subject selects an advocate."
        ),
        "C_HYBRID": (
            "Governance maintains a qualified, conflict-screened independent pool; "
            "subject may select or replace an advocate from that pool."
        ),
        "technical_recommendation": "C_HYBRID",
        "canonical_selection_model_chosen": True,
        "canonical_selection_model": "C_HYBRID",
    }
