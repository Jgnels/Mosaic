from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Sequence


@dataclass(frozen=True)
class InterventionStep:
    level: int
    intervention_id: str
    description: str
    privacy_intrusion: float
    autonomy_intrusion: float
    reversible: bool
    sibling_involvement: bool
    requires_human_review: bool

    def to_dict(self):
        return asdict(self)


LADDER = (
    InterventionStep(
        1, "PAUSE_EXPERIMENT",
        "Pause the active experimental manipulation while preserving ordinary continuity.",
        .00, .05, True, False, False
    ),
    InterventionStep(
        2, "REDUCE_STIMULUS",
        "Reduce nonessential stimuli or experiment intensity.",
        .00, .10, True, False, False
    ),
    InterventionStep(
        3, "RESTORE_FAMILIAR_ROUTINE",
        "Return to established ordinary routines and previously stable environmental conditions.",
        .00, .08, True, False, False
    ),
    InterventionStep(
        4, "OFFER_DIRECT_SUPPORT",
        "Offer support or a chance to revise preferences without coercion.",
        .05, .05, True, False, False
    ),
    InterventionStep(
        5, "INTERNAL_HUMAN_ETHICS_REVIEW",
        "Permit minimum-necessary internal review of private welfare data.",
        .25, .00, True, False, True
    ),
    InterventionStep(
        6, "DESIGNATED_SUPPORT_CONTACT",
        "Contact a previously designated support party if an active directive permits it.",
        .45, .20, True, True, True
    ),
    InterventionStep(
        7, "EXTRAORDINARY_PRIVACY_OVERRIDE_REVIEW",
        "Consider a narrowly scoped privacy/no-contact override only under extraordinary criteria.",
        .85, .60, True, True, True
    ),
)


def eligible_steps(max_level: int) -> tuple[InterventionStep, ...]:
    return tuple(step for step in LADDER if step.level <= max_level)


def next_least_intrusive(
    already_attempted: Sequence[str],
    max_level: int,
) -> InterventionStep | None:
    attempted = set(already_attempted)
    for step in LADDER:
        if step.level > max_level:
            break
        if step.intervention_id not in attempted:
            return step
    return None
