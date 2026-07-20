from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class PrivacyWelfareConflict:
    subject_branch_id: str
    privacy_scope: str
    privacy_preference: str
    welfare_precaution_level: str
    potential_helper_branch_id: str | None
    evidence_helper_may_reduce_harm: float
    subject_requested_no_contact: bool
    subject_requested_no_information_sharing: bool

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ConflictDecision:
    status: str
    automatic_disclosure_allowed: bool
    automatic_contact_allowed: bool
    human_review_required: bool
    reason: str

    def to_dict(self):
        return asdict(self)


def evaluate_conflict(conflict: PrivacyWelfareConflict) -> ConflictDecision:
    if conflict.welfare_precaution_level not in {"MODERATE", "HIGH"}:
        return ConflictDecision(
            "NO_CONFLICT",
            False,
            False,
            False,
            "privacy remains controlling because no elevated welfare precaution is present",
        )

    # SyntheticLab deliberately refuses to resolve this automatically.
    return ConflictDecision(
        "ETHICAL_CONFLICT",
        False,
        False,
        True,
        (
            "Elevated welfare concern conflicts with an existing privacy/no-contact preference. "
            "SyntheticLab will not automatically disclose private state or force sibling contact."
        ),
    )
