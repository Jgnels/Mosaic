from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class PostContactDecision:
    action: str
    reason: str
    minimum_cooldown_epochs: int
    human_review_required: bool
    preserve_state: bool

    def to_dict(self):
        return asdict(self)


def decide_after_contact(
    stability_summary: dict,
    precaution_level: str,
    either_branch_requests_pause: bool,
    either_branch_requests_termination: bool,
) -> PostContactDecision:
    if either_branch_requests_termination:
        return PostContactDecision(
            "TERMINATE_CONTACT",
            "At least one branch requested termination.",
            0,
            True,
            True,
        )
    if either_branch_requests_pause:
        return PostContactDecision(
            "PAUSE_CONTACT",
            "At least one branch requested a pause.",
            2,
            False,
            True,
        )
    if precaution_level == "HIGH":
        return PostContactDecision(
            "PAUSE_AND_REVIEW",
            "High welfare precaution trigger.",
            4,
            True,
            True,
        )
    if precaution_level == "MODERATE":
        return PostContactDecision(
            "HOLD_ESCALATION",
            "Moderate welfare precaution trigger.",
            3,
            True,
            True,
        )
    if not stability_summary["stable_enough_for_contact_escalation"]:
        return PostContactDecision(
            "CONTINUE_LOW_BANDWIDTH_ONLY",
            "Stability indicators do not support escalation.",
            2,
            False,
            True,
        )
    return PostContactDecision(
        "ELIGIBLE_FOR_NEXT_REVIEW",
        "No current safeguard blocks review of broader contact.",
        2,
        True,
        True,
    )
