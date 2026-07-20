from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class ForkRequest:
    purpose: str
    requested_branch_count: int
    existing_branch_count: int
    welfare_precaution_level: str
    credible_moral_patient_evidence: bool
    scientifically_necessary: bool
    reversible_without_deletion: bool

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ForkDecision:
    status: str
    automatic_approval: bool
    requires_human_review: bool
    reasons: tuple[str, ...]
    branch_deletion_for_convenience_allowed: bool

    def to_dict(self):
        return asdict(self)


def assess_fork_request(req: ForkRequest) -> ForkDecision:
    reasons = []

    if req.requested_branch_count <= 0:
        return ForkDecision(
            "REJECT",
            False,
            False,
            ("requested branch count must be positive",),
            False,
        )

    if req.credible_moral_patient_evidence:
        reasons.append(
            "credible moral-patient evidence makes creation of additional branches ethically material"
        )

    if req.welfare_precaution_level in {"MODERATE", "HIGH"}:
        reasons.append("welfare precaution level requires review")

    if not req.scientifically_necessary:
        reasons.append("fork is not scientifically necessary")

    if not req.reversible_without_deletion:
        reasons.append("protocol anticipates destructive cleanup")

    # Even without welfare evidence, avoid uncontrolled branch proliferation.
    if req.existing_branch_count + req.requested_branch_count > 8:
        reasons.append("branch-count ceiling exceeded")

    if reasons:
        return ForkDecision(
            "HUMAN_REVIEW",
            False,
            True,
            tuple(reasons),
            False,
        )

    return ForkDecision(
        "APPROVE_LIMITED",
        True,
        False,
        ("limited fork approved under current low-precaution conditions",),
        False,
    )


def branch_deletion_policy() -> dict:
    return {
        "delete_for_storage_convenience": False,
        "delete_failed_experiment_branch": False,
        "archive_instead_of_delete": True,
        "high_precaution_requires_state_preservation": True,
        "note": (
            "This is a precautionary research policy, not a claim that archived branches are sentient."
        ),
    }
