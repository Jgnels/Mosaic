from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Iterable

from .canonical_branch_checkpoint import (
    restore_self_model_from_snapshot,
)
from .self_model import SelfModelStore


@dataclass(frozen=True)
class BranchRevisionAuthorization:
    branch_id: str
    candidate_id: str
    consensus_decision: str
    consensus_fraction: float
    human_approval_present: bool
    branch_merge_enabled: bool = False
    action_policy_feedback_enabled: bool = False
    automatic_future_revision_enabled: bool = False

    def to_dict(self):
        return asdict(self)


def execute_branch_specific_revision(
    *,
    branch_id: str,
    base_self_model_snapshot: dict,
    candidate: dict,
    consensus: dict,
    authorization: BranchRevisionAuthorization,
    timestamp: int,
) -> dict:
    if branch_id != authorization.branch_id:
        raise PermissionError(
            "authorization branch mismatch"
        )

    if candidate[
        "candidate_id"
    ] != authorization.candidate_id:
        raise PermissionError(
            "authorization candidate mismatch"
        )

    if not authorization.human_approval_present:
        raise PermissionError(
            "explicit human approval is required"
        )

    if authorization.branch_merge_enabled:
        raise PermissionError(
            "branch merge is prohibited"
        )

    if authorization.action_policy_feedback_enabled:
        raise PermissionError(
            "action-policy feedback is prohibited"
        )

    if authorization.automatic_future_revision_enabled:
        raise PermissionError(
            "automatic future revision is prohibited"
        )

    if not consensus[
        "technically_eligible_for_human_gate"
    ]:
        raise PermissionError(
            "branch did not pass the technical consensus gate"
        )

    if consensus[
        "branch_id"
    ] != branch_id:
        raise PermissionError(
            "consensus branch mismatch"
        )

    if consensus[
        "representative_candidate_id"
    ] != candidate[
        "candidate_id"
    ]:
        raise PermissionError(
            "candidate is not the deterministic representative"
        )

    if consensus[
        "consensus_decision"
    ] != authorization.consensus_decision:
        raise PermissionError(
            "consensus decision mismatch"
        )

    if float(
        consensus[
            "consensus_fraction"
        ]
    ) != float(
        authorization.consensus_fraction
    ):
        raise PermissionError(
            "consensus fraction mismatch"
        )

    if candidate[
        "provenance_class"
    ] != "LIVED_BRANCH_HISTORY":
        raise PermissionError(
            "canonical revision requires lived branch history provenance"
        )

    if candidate[
        "prior_hypothesis_id"
    ] != consensus[
        "prior_hypothesis_id"
    ]:
        raise PermissionError(
            "candidate prior hypothesis mismatch"
        )

    self_model = restore_self_model_from_snapshot(
        base_self_model_snapshot
    )

    prior = self_model.active(
        candidate[
            "domain"
        ]
    )

    if prior is None:
        raise RuntimeError(
            "no active prior hypothesis exists in revision domain"
        )

    if prior.hypothesis_id != candidate[
        "prior_hypothesis_id"
    ]:
        raise PermissionError(
            "branch base state does not contain the expected prior hypothesis"
        )

    before = self_model.to_dict()

    record = self_model.revise(
        domain=(
            candidate[
                "domain"
            ]
        ),
        proposition=(
            candidate[
                "updated_proposition"
            ]
        ),
        confidence=float(
            candidate[
                "updated_confidence"
            ]
        ),
        source_ids=tuple(
            list(
                candidate[
                    "evidence_ids"
                ]
            )
            + [
                "branch_journal:"
                + consensus[
                    "journal_head_hash"
                ],
            ]
        ),
        timestamp=(
            timestamp
        ),
        mechanism=(
            "canonical_lived_history_revision"
        ),
        mechanism_version=(
            "1.0"
        ),
    )

    after = self_model.to_dict()

    return {
        "branch_id": (
            branch_id
        ),
        "candidate_id": (
            candidate[
                "candidate_id"
            ]
        ),
        "consensus_decision": (
            authorization.consensus_decision
        ),
        "consensus_fraction": (
            authorization.consensus_fraction
        ),
        "human_approval_present": (
            authorization.human_approval_present
        ),
        "before_state_hash": (
            before[
                "state_hash"
            ]
        ),
        "after_state_hash": (
            after[
                "state_hash"
            ]
        ),
        "state_changed": (
            before[
                "state_hash"
            ]
            != after[
                "state_hash"
            ]
        ),
        "prior_hypothesis": (
            prior.to_dict()
        ),
        "revised_hypothesis": (
            record.to_dict()
        ),
        "self_model_state": (
            after
        ),
        "branch_merge_enabled": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "automatic_future_revision_enabled": (
            False
        ),
        "automatic_second_promotion_enabled": (
            False
        ),
        "goal_generation_enabled": (
            False
        ),
        "drive_modification_enabled": (
            False
        ),
        "memory_rewrite_enabled": (
            False
        ),
        "identity_rewrite_enabled": (
            False
        ),
    }
