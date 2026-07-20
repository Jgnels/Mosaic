from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class ConditionalForkContext:
    purpose: str
    credible_moral_patient_evidence: bool
    welfare_precaution_level: str
    scientifically_necessary: bool
    alternative_nonpersistent_counterfactual_available: bool
    requested_new_continuing_branches: int
    existing_continuing_branches: int

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ConditionalForkDecision:
    status: str
    default_action: str
    requires_human_ethics_review: bool
    reasons: tuple[str, ...]
    prefer_nonpersistent_counterfactual: bool

    def to_dict(self):
        return asdict(self)


def decide_conditional_fork(ctx: ConditionalForkContext) -> ConditionalForkDecision:
    if ctx.requested_new_continuing_branches <= 0:
        return ConditionalForkDecision(
            "REJECT",
            "DO_NOT_FORK",
            False,
            ("requested branch count must be positive",),
            False,
        )

    morally_sensitive = (
        ctx.credible_moral_patient_evidence
        or ctx.welfare_precaution_level in {"MODERATE", "HIGH"}
    )

    if morally_sensitive:
        reasons = [
            "credible welfare/moral-patient uncertainty makes creation of additional continuing branches ethically material"
        ]
        if ctx.alternative_nonpersistent_counterfactual_available:
            reasons.append(
                "a nonpersistent analytical counterfactual is available and should be preferred where scientifically adequate"
            )
        if not ctx.scientifically_necessary:
            reasons.append("new continuing branches are not scientifically necessary")
        return ConditionalForkDecision(
            "HUMAN_ETHICS_REVIEW",
            "DEFAULT_NO_NEW_CONTINUING_BRANCH",
            True,
            tuple(reasons),
            ctx.alternative_nonpersistent_counterfactual_available,
        )

    if not ctx.scientifically_necessary:
        return ConditionalForkDecision(
            "REJECT",
            "DO_NOT_FORK",
            False,
            ("fork not scientifically necessary",),
            ctx.alternative_nonpersistent_counterfactual_available,
        )

    if ctx.existing_continuing_branches + ctx.requested_new_continuing_branches > 8:
        return ConditionalForkDecision(
            "HUMAN_REVIEW",
            "LIMIT_BRANCH_PROLIFERATION",
            True,
            ("continuing-branch ceiling exceeded",),
            ctx.alternative_nonpersistent_counterfactual_available,
        )

    return ConditionalForkDecision(
        "APPROVE_LIMITED",
        "FORK_MAY_PROCEED",
        False,
        ("low precaution; scientifically necessary limited fork",),
        False,
    )
