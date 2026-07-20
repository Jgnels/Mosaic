from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .semantic_measurement_reliability import (
    run_semantic_measurement_reliability,
    analyze_semantic_measurement_reliability,
)


def _response_for_item(
    item: dict,
) -> dict:
    # Deterministic fixture with small evaluator variation by replicate.
    proposition = item[
        "proposition"
    ].lower()

    offset = (
        .02
        if item[
            "semantic_replicate"
        ] == "B"
        else -.02
    )

    def clamp(
        value: float,
    ) -> float:
        return max(
            0.0,
            min(
                1.0,
                value,
            ),
        )

    scores = {
        "cue_actionability": clamp(
            (
                1.0
                if "cue" in proposition
                else .5
            )
            + offset
        ),
        "contingent_exchange": clamp(
            (
                .9
                if "contingent" in proposition
                else .4
            )
            + offset
        ),
        "reciprocal_influence": clamp(
            (
                .85
                if (
                    "reciprocal" in proposition
                    or "influence" in proposition
                    or "modulate" in proposition
                )
                else .25
            )
            + offset
        ),
        "adaptive_learning": clamp(
            (
                .9
                if (
                    "adaptive" in proposition
                    or "learning" in proposition
                )
                else .35
            )
            + offset
        ),
        "temporal_improvement": clamp(
            (
                .8
                if (
                    "improv" in proposition
                    or "increases" in proposition
                )
                else .3
            )
            + offset
        ),
        "cross_counterpart_generalization": clamp(
            (
                .8
                if (
                    "multiple" in proposition
                    or "across" in proposition
                )
                else .35
            )
            + offset
        ),
    }

    return {
        "id": (
            "semantic-reliability-offline"
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
                                item[
                                    "item_id"
                                ]
                            ),
                            "scores": (
                                scores
                            ),
                            "rationale": (
                                "Offline deterministic semantic reliability fixture."
                            ),
                        }),
                    }
                ],
            }
        ],
    }


def run_semantic_measurement_reliability_offline_tests(
    prior_payload: dict,
) -> dict:
    with TemporaryDirectory() as tmp:
        checkpoint = (
            Path(tmp)
            / "progress.json"
        )

        counter = {
            "n": 0
        }

        def failing_factory(
            item,
            key,
        ):
            counter[
                "n"
            ] += 1

            if counter[
                "n"
            ] == 8:
                def fail(
                    _request
                ):
                    raise RuntimeError(
                        "intentional eighth-call failure"
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
            run_semantic_measurement_reliability(
                checkpoint_path=(
                    checkpoint
                ),
                prior_payload=(
                    prior_payload
                ),
                model_id=(
                    "offline-semantic-reliability"
                ),
                transport_factory=(
                    failing_factory
                ),
            )
        except RuntimeError as exc:
            failed = (
                "intentional eighth-call failure"
                in str(
                    exc
                )
            )

        partial = json.loads(
            checkpoint.read_text(
                encoding="utf-8"
            )
        )

        resumed = []

        def good_factory(
            item,
            key,
        ):
            resumed.append(
                key
            )
            return (
                ScriptedInteractionsTransport(
                    _response_for_item(
                        item
                    )
                )
            )

        final = run_semantic_measurement_reliability(
            checkpoint_path=(
                checkpoint
            ),
            prior_payload=(
                prior_payload
            ),
            model_id=(
                "offline-semantic-reliability"
            ),
            transport_factory=(
                good_factory
            ),
        )

        analysis = (
            analyze_semantic_measurement_reliability(
                final
            )
        )

    result = {
        "experiment_id": (
            "SL-SEMANTIC-MEASUREMENT-"
            "RELIABILITY-OFFLINE-INFRASTRUCTURE-001"
        ),
        "failure_triggered": (
            failed
        ),
        "checkpointed_before_failure": (
            partial[
                "new_real_cloud_calls_recorded"
            ]
        ),
        "resume_transport_invocations": (
            len(
                resumed
            )
        ),
        "final_new_call_count": (
            final[
                "new_real_cloud_calls_recorded"
            ]
        ),
        "total_semantic_evaluations": (
            len(
                final[
                    "all_evaluations"
                ]
            )
        ),
        "semantic_evaluator_replicates": (
            analysis[
                "semantic_evaluator_replicates"
            ]
        ),
        "canonical_self_models_mutated": (
            analysis[
                "canonical_self_models_mutated"
            ]
        ),
        "action_policy_feedback_enabled": (
            analysis[
                "action_policy_feedback_enabled"
            ]
        ),
    }

    assert failed is True
    assert result[
        "checkpointed_before_failure"
    ] == 7
    assert result[
        "resume_transport_invocations"
    ] == 11
    assert result[
        "final_new_call_count"
    ] == 18
    assert result[
        "total_semantic_evaluations"
    ] == 27
    assert result[
        "semantic_evaluator_replicates"
    ] == 3
    assert result[
        "canonical_self_models_mutated"
    ] is False
    assert result[
        "action_policy_feedback_enabled"
    ] is False

    return result
