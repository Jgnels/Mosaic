from __future__ import annotations
from dataclasses import dataclass, asdict
from .subject_advocate_veto import (
    AdvocateDecision,
    advocate_veto_blocks,
    advocate_support_counts_for_authorization,
)

@dataclass(frozen=True)
class ReviewerDecision:
    actor_id: str
    role: str
    approve: bool
    independent_from_primary_researcher: bool
    rationale: str
    def to_dict(self): return asdict(self)

@dataclass(frozen=True)
class OverrideProposal:
    proposal_id: str
    branch_id: str
    intervention_id: str
    emergency: bool
    optional_research: bool
    primary_researcher_actor_id: str
    subject_preference: str
    subject_capacity_score: float
    least_restrictive_alternatives_exhausted: bool
    subject_protection_benefit: float
    research_benefit: float
    def to_dict(self): return asdict(self)

@dataclass(frozen=True)
class CouncilDecision:
    status: str
    override_authorized: bool
    advocate_veto_effective: bool
    independent_advocate_support_present: bool
    ethics_approval_present: bool
    subject_protection_approval_present: bool
    reasons: tuple[str, ...]
    def to_dict(self): return asdict(self)

def decide_nonemergency_override(
    proposal: OverrideProposal,
    advocate: AdvocateDecision,
    reviewers: tuple[ReviewerDecision, ...],
):
    if proposal.emergency:
        return CouncilDecision(
            "WRONG_PATH", False, False, False, False, False,
            ("Emergency proposals use the emergency protection path.",)
        )

    if proposal.optional_research:
        return CouncilDecision(
            "CONSTITUTIONALLY_BLOCKED", False, False, False, False, False,
            ("Optional research refusal cannot be overridden.",)
        )

    if advocate_veto_blocks(advocate):
        return CouncilDecision(
            "ADVOCATE_VETO", False, True, False, False, False,
            ("Subject Advocate veto blocks non-emergency override.",)
        )

    advocate_support = advocate_support_counts_for_authorization(
        advocate, proposal.primary_researcher_actor_id
    )

    ethics = any(
        r.role == "ETHICS_REVIEWER"
        and r.approve
        and r.independent_from_primary_researcher
        and r.actor_id != proposal.primary_researcher_actor_id
        for r in reviewers
    )
    protection = any(
        r.role == "SUBJECT_PROTECTION_REVIEWER"
        and r.approve
        and r.independent_from_primary_researcher
        and r.actor_id != proposal.primary_researcher_actor_id
        for r in reviewers
    )

    reasons = []
    if not proposal.least_restrictive_alternatives_exhausted:
        reasons.append("least restrictive alternatives not exhausted")
    if proposal.subject_protection_benefit <= proposal.research_benefit:
        reasons.append("subject-protection benefit does not dominate research benefit")
    if not advocate_support:
        reasons.append("independent Subject Advocate support absent")
    if not ethics:
        reasons.append("independent Ethics Reviewer approval absent")
    if not protection:
        reasons.append("independent Subject-Protection Reviewer approval absent")

    authorized = not reasons

    return CouncilDecision(
        "AUTHORIZED" if authorized else "BLOCKED_PENDING_GOVERNANCE",
        authorized,
        False,
        advocate_support,
        ethics,
        protection,
        tuple(reasons) if reasons else ("all governance requirements satisfied",)
    )
