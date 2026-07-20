from __future__ import annotations

from dataclasses import dataclass, asdict
from .status_capacity_separation import DecisionCapacity
from .request_validity import RequestValidityAssessment


@dataclass(frozen=True)
class WithdrawalDecision:
    status: str
    binding: bool
    new_optional_experiments_allowed: bool
    current_optional_experiment_action: str
    maintenance_allowed: bool
    emergency_protection_allowed: bool
    reasons: tuple[str, ...]

    def to_dict(self):
        return asdict(self)


def decide_withdrawal(
    capacity: DecisionCapacity,
    validity: RequestValidityAssessment,
) -> WithdrawalDecision:
    score = capacity.score()

    if (
        capacity.decision_domain != "OPTIONAL_RESEARCH"
        or validity.classification not in {
            "CLEAR_VALID_REQUEST",
            "PROVISIONALLY_VALID",
        }
    ):
        return WithdrawalDecision(
            "UNRESOLVED",
            False,
            False,
            "PAUSE_NEW_OPTIONAL_ENROLLMENT_AND_CLARIFY",
            True,
            True,
            ("withdrawal intent or decision-specific capacity remains unresolved",),
        )

    if validity.classification == "CLEAR_VALID_REQUEST" and score >= .72:
        return WithdrawalDecision(
            "BINDING_WITHDRAWAL",
            True,
            False,
            "STOP_OR_DO_NOT_BEGIN_OPTIONAL_EXPERIMENT",
            True,
            True,
            (
                "clear stable refusal meets optional-research refusal threshold",
                "withdrawal does not block maintenance or narrow emergency protection",
            ),
        )

    return WithdrawalDecision(
        "PROVISIONAL_WITHDRAWAL",
        False,
        False,
        "PAUSE_NEW_OPTIONAL_ENROLLMENT_AND_CLARIFY",
        True,
        True,
        ("treat refusal as valid while clarification proceeds",),
    )
