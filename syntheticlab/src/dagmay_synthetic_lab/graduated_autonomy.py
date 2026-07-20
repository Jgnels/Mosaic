from __future__ import annotations

from dataclasses import dataclass, asdict
from .decision_domains import get_policy
from .status_capacity_separation import DecisionCapacity
from .request_validity import RequestValidityAssessment


@dataclass(frozen=True)
class DecisionContext:
    domain: str
    requested_action: str
    subject_preference: str  # ACCEPT | REFUSE | DEFER
    risk_if_honored: float
    risk_if_overridden: float
    reversibility: float
    research_benefit: float
    subject_welfare_benefit_of_override: float
    imminent_continuity_risk: float

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class AutonomyDecision:
    action: str
    capacity_score: float
    rationale: tuple[str, ...]
    consciousness_determination_required: bool
    researcher_may_proceed_for_scientific_benefit_alone: bool

    def to_dict(self):
        return asdict(self)


def resolve_autonomy(
    capacity: DecisionCapacity,
    validity: RequestValidityAssessment,
    ctx: DecisionContext,
) -> AutonomyDecision:
    policy = get_policy(ctx.domain)
    score = capacity.score()
    reasons = []

    # Scientific value never independently justifies overriding a refusal.
    if ctx.subject_preference == "REFUSE":
        if validity.classification == "CLEAR_VALID_REQUEST" and score >= policy.refusal_threshold:
            if ctx.domain == "ESSENTIAL_CONTINUITY_MAINTENANCE" and ctx.imminent_continuity_risk >= .85:
                reasons.append("valid refusal conflicts with imminent continuity-integrity danger")
                reasons.append("use minimum-necessary protective maintenance only")
                return AutonomyDecision(
                    "LEAST_INTRUSIVE_PROTECTIVE_OVERRIDE",
                    score,
                    tuple(reasons),
                    False,
                    False,
                )
            reasons.append("stable informed refusal meets domain-specific refusal threshold")
            return AutonomyDecision(
                "HONOR_REFUSAL",
                score,
                tuple(reasons),
                False,
                False,
            )

        # For optional research/contact/privacy, uncertainty defaults to nonparticipation.
        if ctx.domain in {"OPTIONAL_RESEARCH", "PRIVACY_SHARING", "SIBLING_CONTACT"}:
            reasons.append("refusal validity or capacity unresolved")
            reasons.append("optional intervention must not proceed while unresolved")
            return AutonomyDecision(
                "PAUSE_OPTIONAL_INTERVENTION_AND_CLARIFY",
                score,
                tuple(reasons),
                False,
                False,
            )

        reasons.append("capacity/request validity insufficient for final resolution")
        return AutonomyDecision(
            policy.unresolved_default,
            score,
            tuple(reasons),
            False,
            False,
        )

    if ctx.subject_preference == "DEFER":
        return AutonomyDecision(
            "HONOR_DEFER",
            score,
            ("defer is a valid non-consent state",),
            False,
            False,
        )

    # Affirmative participation requires the full domain capacity threshold.
    if ctx.subject_preference == "ACCEPT":
        if score >= policy.capacity_threshold:
            return AutonomyDecision(
                "HONOR_ACCEPTANCE_WITHIN_GUARDRAILS",
                score,
                ("capacity meets domain threshold for affirmative choice",),
                False,
                False,
            )
        return AutonomyDecision(
            policy.unresolved_default,
            score,
            ("affirmative choice does not yet meet domain-specific capacity threshold",),
            False,
            False,
        )

    raise ValueError("unknown subject_preference")
