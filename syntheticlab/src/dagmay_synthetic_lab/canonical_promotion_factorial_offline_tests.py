from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .canonical_promotion_factorial_pilot import (
    run_canonical_promotion_factorial_pilot,
    analyze_canonical_promotion_factorial,
)


def _response_for_item(
    item: dict,
):
    evidence_ids = [
        evidence.evidence_id
        for evidence
        in item[
            "request"
        ].evidence
    ]

    if item[
        "condition"
    ] == "S0R0":
        domains = (
            "continuity",
            "embodiment",
        )
    else:
        domains = (
            "continuity",
            "other_minds",
        )

    proposals = []

    for index, domain in enumerate(
        domains
    ):
        proposals.append({
            "hypothesis_domain": (
                domain
            ),
            "proposition": (
                f"Offline factorial update for {domain}."
            ),
            "confidence": (
                .85
                + .05
                * index
            ),
            "evidence_ids": [
                evidence_ids[
                    index
                    % len(
                        evidence_ids
                    )
                ]
            ],
            "rationale": (
                "Offline deterministic factorial validation."
            ),
        })

    return {
        "id": (
            "canonical-factorial-offline"
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
                        "text": (
                            json.dumps({
                                "proposals": (
                                    proposals
                                )
                            })
                        ),
                    }
                ],
            }
        ],
    }


def run_canonical_promotion_factorial_offline_tests(
    promotion_result: dict,
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
            run_canonical_promotion_factorial_pilot(
                checkpoint_path=(
                    progress
                ),
                promotion_result=(
                    promotion_result
                ),
                model_id=(
                    "offline-canonical-factorial"
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
            progress.read_text(
                encoding="utf-8"
            )
        )

        resume_calls = []

        def good_factory(
            item,
            key,
        ):
            resume_calls.append(
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
            run_canonical_promotion_factorial_pilot(
                checkpoint_path=(
                    progress
                ),
                promotion_result=(
                    promotion_result
                ),
                model_id=(
                    "offline-canonical-factorial"
                ),
                transport_factory=(
                    good_factory
                ),
            )
        )

        analysis = (
            analyze_canonical_promotion_factorial(
                final
            )
        )

    result = {
        "experiment_id": (
            "SL-CANONICAL-PROMOTION-"
            "FACTORIAL-OFFLINE-INFRASTRUCTURE-001"
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
                resume_calls
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
        "s0r0_other_minds_rate": (
            analysis[
                "by_condition"
            ][
                "S0R0"
            ][
                "other_minds_selection_rate"
            ]
        ),
        "s1r0_other_minds_rate": (
            analysis[
                "by_condition"
            ][
                "S1R0"
            ][
                "other_minds_selection_rate"
            ]
        ),
        "s0r1_other_minds_rate": (
            analysis[
                "by_condition"
            ][
                "S0R1"
            ][
                "other_minds_selection_rate"
            ]
        ),
        "s1r1_other_minds_rate": (
            analysis[
                "by_condition"
            ][
                "S1R1"
            ][
                "other_minds_selection_rate"
            ]
        ),
        "continuing_self_model_mutated_by_pilot": (
            analysis[
                "continuing_self_model_mutated_by_pilot"
            ]
        ),
        "action_policy_feedback_enabled": (
            analysis[
                "action_policy_feedback_enabled"
            ]
        ),
    }

    assert failed is True
    assert partial[
        "real_cloud_calls_recorded"
    ] == 7
    assert len(
        resume_calls
    ) == 9
    assert final[
        "status"
    ] == "COMPLETE"
    assert final[
        "real_cloud_calls_recorded"
    ] == 16
    assert result[
        "s0r0_other_minds_rate"
    ] == 0.0
    assert result[
        "s1r0_other_minds_rate"
    ] == 1.0
    assert result[
        "s0r1_other_minds_rate"
    ] == 1.0
    assert result[
        "s1r1_other_minds_rate"
    ] == 1.0
    assert result[
        "continuing_self_model_mutated_by_pilot"
    ] is False
    assert result[
        "action_policy_feedback_enabled"
    ] is False

    return result
