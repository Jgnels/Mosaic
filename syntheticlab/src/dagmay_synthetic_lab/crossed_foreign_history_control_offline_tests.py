from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .crossed_foreign_history_control import (
    run_crossed_foreign_history_control,
    analyze_crossed_foreign_history_control,
)
from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
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
        "id": "crossed-foreign-revision-offline",
        "status": "completed",
        "steps": [
            {
                "type": "model_output",
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps({
                            "decision": "MAINTAIN",
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
                            "evidence_ids": evidence_ids,
                            "rationale": (
                                "Offline crossed foreign-history control fixture."
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
    source = item[
        "reference_history_source_branch_id"
    ]

    if source.endswith(
        "RECIPROCAL_CONTINGENT"
    ):
        scores = (
            1.0,
            1.0,
            .9,
            1.0,
            .7,
            .8,
        )
    elif source.endswith(
        "ONE_WAY_ASSISTANCE"
    ):
        scores = (
            1.0,
            .8,
            .3,
            1.0,
            .6,
            .8,
        )
    else:
        scores = (
            .7,
            .3,
            .1,
            .6,
            .3,
            .5,
        )

    return {
        "id": "crossed-foreign-semantic-offline",
        "status": "completed",
        "steps": [
            {
                "type": "model_output",
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps({
                            "item_id": (
                                "XF-"
                                + item[
                                    "semantic_call_key"
                                ][
                                    :16
                                ]
                            ),
                            "scores": {
                                "cue_actionability": scores[0],
                                "contingent_exchange": scores[1],
                                "reciprocal_influence": scores[2],
                                "adaptive_learning": scores[3],
                                "temporal_improvement": scores[4],
                                "cross_counterpart_generalization": scores[5],
                            },
                            "rationale": (
                                "Offline deterministic crossed-history semantic fixture."
                            ),
                        }),
                    }
                ],
            }
        ],
    }


def run_crossed_foreign_history_control_offline_tests(
    *,
    revision_result: dict,
    lived_history_payload: dict,
    own_history_analysis: dict,
    baseline_analysis: dict,
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

        result = run_crossed_foreign_history_control(
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
                "offline-crossed-foreign-history"
            ),
            transport_factory=(
                factory
            ),
        )

        analysis = analyze_crossed_foreign_history_control(
            payload=(
                result
            ),
            own_history_analysis=(
                own_history_analysis
            ),
            baseline_analysis=(
                baseline_analysis
            ),
            permutations=(
                1000
            ),
            seed=(
                3001
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

    source_counts = {}

    for row in result[
        "semantic_evaluations"
    ]:
        source = row[
            "reference_history_source_branch_id"
        ]
        source_counts[
            source
        ] = source_counts.get(
            source,
            0,
        ) + 1

        assert (
            row[
                "reference_history_source_branch_id"
            ]
            != row[
                "focal_branch_id"
            ]
        )

    assert set(
        source_counts.values()
    ) == {
        6
    }

    assert result[
        "foreign_history_misattributed_as_own"
    ] is False

    assert result[
        "canonical_self_models_mutated"
    ] is False

    return {
        "experiment_id": (
            "SL-CROSSED-FOREIGN-HISTORY-"
            "RETRIEVAL-CONTROL-OFFLINE-INFRASTRUCTURE-001"
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
        "balanced_reference_source_counts": (
            source_counts
        ),
        "control_status_fixture": (
            analysis[
                "control_status"
            ]
        ),
        "foreign_history_misattributed_as_own": (
            False
        ),
        "canonical_self_models_mutated": (
            False
        ),
    }
