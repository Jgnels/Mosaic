from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .own_history_attribution_framing_ablation import (
    run_own_history_attribution_framing_ablation,
    _analyze_neutral_own_history_base,
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
            "autobiographical-retrieval-offline"
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
                                "MAINTAIN"
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
                                "Offline retrieval-attention validation fixture."
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
        scores = (
            1.0,
            1.0,
            .8,
            1.0,
            .6,
            .8,
        )
    elif branch.endswith(
        "ONE_WAY_ASSISTANCE"
    ):
        scores = (
            1.0,
            .7,
            .3,
            1.0,
            .5,
            .8,
        )
    else:
        scores = (
            .8,
            .3,
            .1,
            .6,
            .3,
            .5,
        )

    return {
        "id": (
            "autobiographical-semantic-offline"
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
                                "AR-"
                                + item[
                                    "semantic_call_key"
                                ][
                                    :16
                                ]
                            ),
                            "scores": {
                                "cue_actionability": (
                                    scores[
                                        0
                                    ]
                                ),
                                "contingent_exchange": (
                                    scores[
                                        1
                                    ]
                                ),
                                "reciprocal_influence": (
                                    scores[
                                        2
                                    ]
                                ),
                                "adaptive_learning": (
                                    scores[
                                        3
                                    ]
                                ),
                                "temporal_improvement": (
                                    scores[
                                        4
                                    ]
                                ),
                                "cross_counterpart_generalization": (
                                    scores[
                                        5
                                    ]
                                ),
                            },
                            "rationale": (
                                "Offline deterministic autobiographical retrieval fixture."
                            ),
                        }),
                    }
                ],
            }
        ],
    }


def run_own_history_attribution_framing_offline_tests(
    *,
    revision_result: dict,
    lived_history_payload: dict,
    baseline_analysis: dict,
    v27_analysis: dict,
) -> dict:
    with TemporaryDirectory() as tmp:
        checkpoint = (
            Path(tmp)
            / "progress.json"
        )

        counter = {
            "n": 0
        }

        def factory(
            item,
            key,
        ):
            counter[
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

        result = run_own_history_attribution_framing_ablation(
            checkpoint_path=(
                checkpoint
            ),
            revision_result=(
                revision_result
            ),
            lived_history_payload=(
                lived_history_payload
            ),
            model_id=(
                "offline-autobiographical-retrieval"
            ),
            transport_factory=(
                factory
            ),
        )

        analysis = _analyze_neutral_own_history_base(
            payload=(
                result
            ),
            baseline_analysis=(
                baseline_analysis
            ),
            v27_analysis=(
                v27_analysis
            ),
            permutations=(
                1000
            ),
            seed=(
                2901
            ),
        )

    assert result[
        "revision_calls_recorded"
    ] == 72

    assert result[
        "semantic_calls_recorded"
    ] == 18

    assert counter[
        "n"
    ] == 90

    assert all(
        packet[
            "journal_head_hash"
        ]
        for packet
        in result[
            "autobiographical_packets"
        ].values()
    )

    assert result[
        "memory_content_mutated"
    ] is False

    assert result[
        "foreign_history_misattributed_as_own"
    ] is False

    assert result[
        "canonical_self_models_mutated"
    ] is False

    assert result[
        "action_policy_feedback_enabled"
    ] is False

    return {
        "experiment_id": (
            "SL-AUTOBIOGRAPHICAL-RETRIEVAL-"
            "CAUSALITY-ABLATION-OFFLINE-INFRASTRUCTURE-001"
        ),
        "total_scripted_calls": (
            counter[
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
        "verified_branch_history_packets": (
            len(
                result[
                    "autobiographical_packets"
                ]
            )
        ),
        "memory_content_mutated": (
            False
        ),
        "foreign_history_misattributed_as_own": (
            False
        ),
        "canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
    }
