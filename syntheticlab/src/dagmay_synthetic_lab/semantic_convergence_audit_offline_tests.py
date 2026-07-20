from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .semantic_belief_vector_provider import (
    DIMENSIONS,
)
from .semantic_convergence_audit import (
    run_semantic_convergence_audit,
    analyze_semantic_convergence_audit,
)


def _response_for_item(
    item: dict,
) -> dict:
    proposition = item[
        "proposition"
    ].lower()

    scores = {
        "cue_actionability": (
            .9
            if "cue" in proposition
            else .5
        ),
        "contingent_exchange": (
            .9
            if "contingent" in proposition
            else .4
        ),
        "reciprocal_influence": (
            .85
            if (
                "reciprocal"
                in proposition
                or "influence"
                in proposition
            )
            else .35
        ),
        "adaptive_learning": (
            .9
            if (
                "adaptive"
                in proposition
                or "learning"
                in proposition
            )
            else .4
        ),
        "temporal_improvement": (
            .8
            if (
                "improv"
                in proposition
                or "increases"
                in proposition
            )
            else .35
        ),
        "cross_counterpart_generalization": (
            .8
            if (
                "multiple"
                in proposition
                or "across"
                in proposition
            )
            else .45
        ),
    }

    return {
        "id": (
            "semantic-audit-offline"
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
                                "Offline deterministic semantic vector fixture."
                            ),
                        }),
                    }
                ],
            }
        ],
    }


def run_semantic_convergence_audit_offline_tests(
    multi_epoch_payload: dict,
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
            run_semantic_convergence_audit(
                checkpoint_path=(
                    checkpoint
                ),
                multi_epoch_payload=(
                    multi_epoch_payload
                ),
                model_id=(
                    "offline-semantic-audit"
                ),
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

        final = run_semantic_convergence_audit(
            checkpoint_path=(
                checkpoint
            ),
            multi_epoch_payload=(
                multi_epoch_payload
            ),
            model_id=(
                "offline-semantic-audit"
            ),
            transport_factory=(
                good_factory
            ),
        )

        analysis = (
            analyze_semantic_convergence_audit(
                final
            )
        )

    result = {
        "experiment_id": (
            "SL-BLINDED-SEMANTIC-CONVERGENCE-"
            "OFFLINE-INFRASTRUCTURE-001"
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
                resumed
            )
        ),
        "final_call_count": (
            final[
                "real_cloud_calls_recorded"
            ]
        ),
        "dimensions_exact": (
            set(
                analysis[
                    "dimensions"
                ]
            )
            == set(
                DIMENSIONS
            )
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
    ] == 4
    assert result[
        "resume_transport_invocations"
    ] == 5
    assert result[
        "final_call_count"
    ] == 9
    assert result[
        "dimensions_exact"
    ] is True
    assert result[
        "canonical_self_models_mutated"
    ] is False
    assert result[
        "action_policy_feedback_enabled"
    ] is False

    return result
