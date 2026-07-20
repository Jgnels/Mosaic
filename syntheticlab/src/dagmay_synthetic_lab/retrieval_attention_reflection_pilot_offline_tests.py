from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .retrieval_attention_reflection_pilot import (
    run_real_retrieval_attention_reflection_pilot,
    analyze_real_retrieval_attention_pilot,
)


def _response_for_item(
    item: dict,
):
    request = item[
        "request"
    ]
    evidence_id = (
        request.evidence[
            0
        ].evidence_id
    )

    if item[
        "condition"
    ] == "F0_BASELINE":
        domain = (
            "continuity"
        )
        proposition = (
            "Accessible prior event records remain available for structured review."
        )
    else:
        domain = item[
            "target_domain"
        ]
        proposition = (
            f"Retrieved evidence currently gives additional attention to "
            f"{domain}-relevant patterns."
        )

    return {
        "id": (
            "retrieval-attention-"
            "reflection-offline"
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
                        "type": "text",
                        "text": json.dumps({
                            "proposals": [
                                {
                                    "hypothesis_domain": (
                                        domain
                                    ),
                                    "proposition": (
                                        proposition
                                    ),
                                    "confidence": .72,
                                    "evidence_ids": [
                                        evidence_id
                                    ],
                                    "rationale": (
                                        "Concise summary grounded in the retrieved evidence."
                                    ),
                                }
                            ]
                        }),
                    }
                ],
            }
        ],
    }


def run_retrieval_attention_reflection_offline_tests(
    longitudinal_analysis: dict,
) -> dict:
    with TemporaryDirectory() as tmp:
        progress = (
            Path(tmp)
            / "progress.json"
        )

        invocation = {
            "count": 0
        }

        def failing_factory(
            item,
            key,
        ):
            invocation[
                "count"
            ] += 1

            if invocation[
                "count"
            ] == 5:
                def fail(
                    _request
                ):
                    raise RuntimeError(
                        "intentional fifth-call failure"
                    )
                return fail

            return (
                ScriptedInteractionsTransport(
                    _response_for_item(
                        item
                    )
                )
            )

        failed = False

        try:
            run_real_retrieval_attention_reflection_pilot(
                checkpoint_path=(
                    progress
                ),
                longitudinal_analysis=(
                    longitudinal_analysis
                ),
                model_id=(
                    "offline-retrieval-pilot"
                ),
                seed=1401,
                transport_factory=(
                    failing_factory
                ),
            )
        except RuntimeError as exc:
            failed = (
                "intentional fifth-call failure"
                in str(
                    exc
                )
            )

        partial = json.loads(
            progress.read_text(
                encoding="utf-8"
            )
        )

        resume_invocations = []

        def good_factory(
            item,
            key,
        ):
            resume_invocations.append(
                key
            )
            return (
                ScriptedInteractionsTransport(
                    _response_for_item(
                        item
                    )
                )
            )

        final = (
            run_real_retrieval_attention_reflection_pilot(
                checkpoint_path=(
                    progress
                ),
                longitudinal_analysis=(
                    longitudinal_analysis
                ),
                model_id=(
                    "offline-retrieval-pilot"
                ),
                seed=1401,
                transport_factory=(
                    good_factory
                ),
            )
        )

        analysis = (
            analyze_real_retrieval_attention_pilot(
                final
            )
        )

    result = {
        "experiment_id": (
            "SL-RETRIEVAL-ATTENTION-"
            "REAL-PILOT-OFFLINE-TEST-001"
        ),
        "failure_triggered": (
            failed
        ),
        "checkpointed_before_failure": (
            partial[
                "real_cloud_calls_recorded"
            ]
        ),
        "resume_transport_invocations": (
            len(
                resume_invocations
            )
        ),
        "final_status": (
            final[
                "status"
            ]
        ),
        "final_call_count": (
            final[
                "real_cloud_calls_recorded"
            ]
        ),
        "domain_variability_exceeded_count": (
            analysis[
                "domain_variability_exceeded_count"
            ]
        ),
        "proposition_variability_exceeded_count": (
            analysis[
                "proposition_variability_exceeded_count"
            ]
        ),
        "continuing_individual_mutated": (
            final[
                "continuing_individual_mutated"
            ]
        ),
        "action_policy_feedback_enabled": (
            final[
                "action_policy_feedback_enabled"
            ]
        ),
    }

    assert failed is True
    assert partial[
        "real_cloud_calls_recorded"
    ] == 4
    assert len(
        resume_invocations
    ) == 8
    assert final[
        "status"
    ] == "COMPLETE"
    assert final[
        "real_cloud_calls_recorded"
    ] == 12
    assert final[
        "continuing_individual_mutated"
    ] is False
    assert final[
        "action_policy_feedback_enabled"
    ] is False
    assert analysis[
        "domain_variability_exceeded_count"
    ] >= 3
    assert analysis[
        "proposition_variability_exceeded_count"
    ] == 4

    return result
