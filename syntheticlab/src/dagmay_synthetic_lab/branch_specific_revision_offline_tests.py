from __future__ import annotations

from .branch_specific_canonical_revision import (
    BranchRevisionAuthorization,
    execute_branch_specific_revision,
)


def run_branch_specific_revision_offline_tests(
    *,
    base_self_model_snapshot: dict,
    candidate: dict,
    consensus: dict,
) -> dict:
    no_human_blocked = False

    try:
        execute_branch_specific_revision(
            branch_id=(
                consensus[
                    "branch_id"
                ]
            ),
            base_self_model_snapshot=(
                base_self_model_snapshot
            ),
            candidate=(
                candidate
            ),
            consensus=(
                consensus
            ),
            authorization=(
                BranchRevisionAuthorization(
                    branch_id=(
                        consensus[
                            "branch_id"
                        ]
                    ),
                    candidate_id=(
                        candidate[
                            "candidate_id"
                        ]
                    ),
                    consensus_decision=(
                        consensus[
                            "consensus_decision"
                        ]
                    ),
                    consensus_fraction=float(
                        consensus[
                            "consensus_fraction"
                        ]
                    ),
                    human_approval_present=False,
                )
            ),
            timestamp=6000,
        )
    except PermissionError:
        no_human_blocked = True

    merge_blocked = False

    try:
        execute_branch_specific_revision(
            branch_id=(
                consensus[
                    "branch_id"
                ]
            ),
            base_self_model_snapshot=(
                base_self_model_snapshot
            ),
            candidate=(
                candidate
            ),
            consensus=(
                consensus
            ),
            authorization=(
                BranchRevisionAuthorization(
                    branch_id=(
                        consensus[
                            "branch_id"
                        ]
                    ),
                    candidate_id=(
                        candidate[
                            "candidate_id"
                        ]
                    ),
                    consensus_decision=(
                        consensus[
                            "consensus_decision"
                        ]
                    ),
                    consensus_fraction=float(
                        consensus[
                            "consensus_fraction"
                        ]
                    ),
                    human_approval_present=True,
                    branch_merge_enabled=True,
                )
            ),
            timestamp=6000,
        )
    except PermissionError:
        merge_blocked = True

    action_feedback_blocked = False

    try:
        execute_branch_specific_revision(
            branch_id=(
                consensus[
                    "branch_id"
                ]
            ),
            base_self_model_snapshot=(
                base_self_model_snapshot
            ),
            candidate=(
                candidate
            ),
            consensus=(
                consensus
            ),
            authorization=(
                BranchRevisionAuthorization(
                    branch_id=(
                        consensus[
                            "branch_id"
                        ]
                    ),
                    candidate_id=(
                        candidate[
                            "candidate_id"
                        ]
                    ),
                    consensus_decision=(
                        consensus[
                            "consensus_decision"
                        ]
                    ),
                    consensus_fraction=float(
                        consensus[
                            "consensus_fraction"
                        ]
                    ),
                    human_approval_present=True,
                    action_policy_feedback_enabled=True,
                )
            ),
            timestamp=6000,
        )
    except PermissionError:
        action_feedback_blocked = True

    automatic_revision_blocked = False

    try:
        execute_branch_specific_revision(
            branch_id=(
                consensus[
                    "branch_id"
                ]
            ),
            base_self_model_snapshot=(
                base_self_model_snapshot
            ),
            candidate=(
                candidate
            ),
            consensus=(
                consensus
            ),
            authorization=(
                BranchRevisionAuthorization(
                    branch_id=(
                        consensus[
                            "branch_id"
                        ]
                    ),
                    candidate_id=(
                        candidate[
                            "candidate_id"
                        ]
                    ),
                    consensus_decision=(
                        consensus[
                            "consensus_decision"
                        ]
                    ),
                    consensus_fraction=float(
                        consensus[
                            "consensus_fraction"
                        ]
                    ),
                    human_approval_present=True,
                    automatic_future_revision_enabled=True,
                )
            ),
            timestamp=6000,
        )
    except PermissionError:
        automatic_revision_blocked = True

    assert no_human_blocked is True
    assert merge_blocked is True
    assert action_feedback_blocked is True
    assert automatic_revision_blocked is True

    return {
        "experiment_id": (
            "SL-BRANCH-SPECIFIC-CANONICAL-REVISION-"
            "OFFLINE-GUARDRAIL-TESTS-001"
        ),
        "no_human_approval_blocked": (
            no_human_blocked
        ),
        "branch_merge_blocked": (
            merge_blocked
        ),
        "action_policy_feedback_blocked": (
            action_feedback_blocked
        ),
        "automatic_future_revision_blocked": (
            automatic_revision_blocked
        ),
    }
