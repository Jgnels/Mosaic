from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .multi_epoch_common_future import (
    run_multi_epoch_common_future_pilot,
    analyze_multi_epoch_common_future,
)


def _response_for_item(
    item: dict,
) -> dict:
    epoch = item[
        "epoch_index"
    ]

    evidence_ids = [
        evidence[
            "evidence_id"
        ]
        for evidence
        in item[
            "request"
        ].evidence
    ]

    if epoch < 4:
        proposition = (
            item[
                "request"
            ].current_hypothesis
        )
        decision = (
            "MAINTAIN"
        )
        confidence = float(
            item[
                "request"
            ].current_confidence
        )
    else:
        proposition = (
            "Shared later experience supports adaptive cue-conditioned interaction "
            "with moderate contingent information exchange across multiple counterparts."
        )
        decision = (
            "QUALIFY"
        )
        confidence = .80

    return {
        "id": (
            "multi-epoch-offline"
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
                                decision
                            ),
                            "updated_proposition": (
                                proposition
                            ),
                            "updated_confidence": (
                                confidence
                            ),
                            "evidence_ids": (
                                evidence_ids
                            ),
                            "rationale": (
                                "Offline deterministic sequential convergence validation."
                            ),
                        }),
                    }
                ],
            }
        ],
    }


def run_multi_epoch_common_future_offline_tests(
    revision_result: dict,
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
            ] == 10:
                def fail(
                    _request
                ):
                    raise RuntimeError(
                        "intentional tenth-call failure"
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
            run_multi_epoch_common_future_pilot(
                checkpoint_path=(
                    checkpoint
                ),
                revision_result=(
                    revision_result
                ),
                model_id=(
                    "offline-multi-epoch"
                ),
                transport_factory=(
                    failing_factory
                ),
            )
        except RuntimeError as exc:
            failed = (
                "intentional tenth-call failure"
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

        final = (
            run_multi_epoch_common_future_pilot(
                checkpoint_path=(
                    checkpoint
                ),
                revision_result=(
                    revision_result
                ),
                model_id=(
                    "offline-multi-epoch"
                ),
                transport_factory=(
                    good_factory
                ),
            )
        )

        analysis = (
            analyze_multi_epoch_common_future(
                final
            )
        )

    result = {
        "experiment_id": (
            "SL-MULTI-EPOCH-COMMON-FUTURE-"
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
        "final_status": (
            final[
                "status"
            ]
        ),
        "classification_on_scripted_fixture": (
            analysis[
                "classification"
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
    ] == 9
    assert result[
        "resume_transport_invocations"
    ] == 15
    assert result[
        "final_call_count"
    ] == 24
    assert result[
        "final_status"
    ] == "COMPLETE"
    assert result[
        "classification_on_scripted_fixture"
    ] == "CONVERGENCE"
    assert result[
        "canonical_self_models_mutated"
    ] is False
    assert result[
        "action_policy_feedback_enabled"
    ] is False

    return result
