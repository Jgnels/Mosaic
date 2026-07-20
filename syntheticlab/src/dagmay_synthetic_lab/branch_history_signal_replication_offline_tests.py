from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .branch_history_signal_replication import (
    run_branch_history_signal_replication,
    analyze_branch_history_signal_replication,
)


def _revision_response(
    item: dict,
) -> dict:
    evidence_ids = [
        evidence[
            "evidence_id"
        ]
        for evidence
        in item[
            "request"
        ].evidence
    ]

    return {
        "id": (
            "branch-signal-revision-offline"
        ),
        "status": (
            "completed"
        ),
        "steps": [
            {
                "type": (
                    "model_output"
                ),
                "content": [
                    {
                        "type": (
                            "text"
                        ),
                        "text": json.dumps({
                            "decision": (
                                "QUALIFY"
                            ),
                            "updated_proposition": (
                                item[
                                    "request"
                                ].current_hypothesis
                            ),
                            "updated_confidence": (
                                item[
                                    "request"
                                ].current_confidence
                            ),
                            "evidence_ids": (
                                evidence_ids
                            ),
                            "rationale": (
                                "Offline deterministic branch-signal replication fixture."
                            ),
                        }),
                    }
                ],
            }
        ],
    }


def _semantic_response(
    item: dict,
) -> dict:
    branch = item[
        "branch_id"
    ]

    if branch.endswith(
        "RECIPROCAL_CONTINGENT"
    ):
        reciprocal = .9
        contingency = 1.0
    elif branch.endswith(
        "ONE_WAY_ASSISTANCE"
    ):
        reciprocal = .5
        contingency = .8
    else:
        reciprocal = .1
        contingency = .5

    return {
        "id": (
            "branch-signal-semantic-offline"
        ),
        "status": (
            "completed"
        ),
        "steps": [
            {
                "type": (
                    "model_output"
                ),
                "content": [
                    {
                        "type": (
                            "text"
                        ),
                        "text": json.dumps({
                            "item_id": (
                                "BSR-"
                                + item[
                                    "semantic_call_key"
                                ][
                                    :16
                                ]
                            ),
                            "scores": {
                                "cue_actionability": (
                                    1.0
                                ),
                                "contingent_exchange": (
                                    contingency
                                ),
                                "reciprocal_influence": (
                                    reciprocal
                                ),
                                "adaptive_learning": (
                                    .8
                                ),
                                "temporal_improvement": (
                                    .5
                                ),
                                "cross_counterpart_generalization": (
                                    .7
                                ),
                            },
                            "rationale": (
                                "Offline deterministic semantic branch-signal fixture."
                            ),
                        }),
                    }
                ],
            }
        ],
    }


def run_branch_history_signal_replication_offline_tests(
    *,
    revision_result: dict,
    multi_epoch_payload: dict,
    v27_analysis: dict,
) -> dict:
    with TemporaryDirectory() as tmp:
        checkpoint = (
            Path(tmp)
            / "progress.json"
        )

        calls = {
            "n": 0
        }

        def factory(
            item,
            key,
        ):
            calls[
                "n"
            ] += 1

            if item[
                "phase"
            ] == "revision":
                return (
                    ScriptedInteractionsTransport(
                        _revision_response(
                            item
                        )
                    )
                )

            semantic_item = {
                **item,
                "semantic_call_key": (
                    key
                ),
            }

            return (
                ScriptedInteractionsTransport(
                    _semantic_response(
                        semantic_item
                    )
                )
            )

        result = run_branch_history_signal_replication(
            checkpoint_path=(
                checkpoint
            ),
            revision_result=(
                revision_result
            ),
            multi_epoch_payload=(
                multi_epoch_payload
            ),
            model_id=(
                "offline-branch-signal"
            ),
            transport_factory=(
                factory
            ),
        )

        analysis = analyze_branch_history_signal_replication(
            payload=(
                result
            ),
            v27_analysis=(
                v27_analysis
            ),
            permutations=(
                1000
            ),
            seed=(
                2801
            ),
        )

    assert result[
        "revision_calls_recorded"
    ] == 48

    assert result[
        "semantic_calls_recorded"
    ] == 18

    assert calls[
        "n"
    ] == 66

    assert analysis[
        "trajectory_count_per_branch"
    ] == 6

    assert analysis[
        "total_final_trajectories"
    ] == 18

    assert result[
        "canonical_self_models_mutated"
    ] is False

    assert result[
        "action_policy_feedback_enabled"
    ] is False

    return {
        "experiment_id": (
            "SL-BRANCH-HISTORY-SIGNAL-"
            "REPLICATION-OFFLINE-INFRASTRUCTURE-001"
        ),
        "total_scripted_calls": (
            calls[
                "n"
            ]
        ),
        "revision_calls": (
            result[
                "revision_calls_recorded"
            ]
        ),
        "semantic_calls": (
            result[
                "semantic_calls_recorded"
            ]
        ),
        "trajectory_count_per_branch": (
            analysis[
                "trajectory_count_per_branch"
            ]
        ),
        "canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
    }
