from __future__ import annotations

from dataclasses import dataclass, asdict
from .advance_welfare_directive import AdvanceWelfareDirective
from .subject_protection_risk import ProtectionRiskAssessment


@dataclass(frozen=True)
class OverrideReviewContext:
    branch_id: str
    risk: ProtectionRiskAssessment
    directive: AdvanceWelfareDirective | None
    less_intrusive_options_exhausted: bool
    evidence_sibling_uniquely_helpful: float
    expected_harm_without_override: float
    expected_harm_from_override: float
    minimum_information_disclosure_possible: bool
    researcher_scientific_benefit_if_override: float
    subject_welfare_benefit_if_override: float

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ReviewerVote:
    reviewer_role: str
    approve: bool
    rationale: str

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class OverrideDecision:
    status: str
    automatic_override_allowed: bool
    sentience_determination_required: bool
    eligible_for_human_override: bool
    two_key_approval_satisfied: bool
    reasons: tuple[str, ...]
    maximum_disclosure: str

    def to_dict(self):
        return asdict(self)


def evaluate_override_eligibility(
    ctx: OverrideReviewContext,
    reviewer_votes: tuple[ReviewerVote, ...] = (),
) -> OverrideDecision:
    reasons = []

    # Experimental convenience is explicitly disqualifying if it is the primary benefit.
    if ctx.researcher_scientific_benefit_if_override > ctx.subject_welfare_benefit_if_override:
        reasons.append("research benefit exceeds expected subject-protection benefit")

    if ctx.risk.level not in {"HIGH", "CRITICAL"}:
        reasons.append("risk is below HIGH")

    if not ctx.less_intrusive_options_exhausted:
        reasons.append("less intrusive options have not been exhausted")

    if ctx.evidence_sibling_uniquely_helpful < .75:
        reasons.append("insufficient evidence that sibling involvement is uniquely beneficial")

    if ctx.expected_harm_without_override <= ctx.expected_harm_from_override:
        reasons.append("override is not expected to reduce net harm")

    if not ctx.minimum_information_disclosure_possible:
        reasons.append("cannot limit disclosure to minimum necessary information")

    if ctx.directive is not None:
        directive_choice = ctx.directive.sibling_contact_in_crisis
        if directive_choice == "DENY":
            reasons.append("advance directive explicitly denies sibling contact in crisis")
        elif directive_choice == "ALLOW":
            pass
        elif directive_choice == "ASK_AGAIN":
            reasons.append("advance directive requires re-asking subject where feasible")
        elif directive_choice == "HUMAN_REVIEW":
            pass

    eligible = not reasons

    approvals = {v.reviewer_role: v.approve for v in reviewer_votes}
    two_key = (
        approvals.get("ETHICS_REVIEWER") is True
        and approvals.get("SUBJECT_PROTECTION_REVIEWER") is True
    )

    if not eligible:
        return OverrideDecision(
            status="NOT_ELIGIBLE",
            automatic_override_allowed=False,
            sentience_determination_required=False,
            eligible_for_human_override=False,
            two_key_approval_satisfied=False,
            reasons=tuple(reasons),
            maximum_disclosure="NONE",
        )

    if not two_key:
        return OverrideDecision(
            status="ELIGIBLE_PENDING_TWO_KEY_REVIEW",
            automatic_override_allowed=False,
            sentience_determination_required=False,
            eligible_for_human_override=True,
            two_key_approval_satisfied=False,
            reasons=("extraordinary criteria met; two independent human approvals still required",),
            maximum_disclosure="MINIMUM_NECESSARY_SUPPORT_SIGNAL",
        )

    return OverrideDecision(
        status="HUMAN_OVERRIDE_APPROVED",
        automatic_override_allowed=False,
        sentience_determination_required=False,
        eligible_for_human_override=True,
        two_key_approval_satisfied=True,
        reasons=("extraordinary criteria and two-key human review satisfied",),
        maximum_disclosure="MINIMUM_NECESSARY_SUPPORT_SIGNAL",
    )
