from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .multi_history_attention_replication import (
    run_multi_history_attention_replication,
    analyze_multi_history_replication,
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

    target = item[
        "target_domain"
    ]

    if target is None:
        domains = (
            "continuity",
            "embodiment",
        )
    elif target == "continuity":
        domains = (
            "continuity",
            "embodiment",
        )
    else:
        domains = (
            "continuity",
            target,
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
                f"Offline multi-history update for {domain}."
            ),
            "confidence": (
                .85
                if index
                else .95
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
                "Offline deterministic replication infrastructure test."
            ),
        })

    return {
        "id": (
            "multi-history-offline"
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


def run_multi_history_attention_offline_tests(
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
            ] == 14:
                def fail(
                    _request
                ):
                    raise RuntimeError(
                        "intentional fourteenth-call failure"
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
            run_multi_history_attention_replication(
                checkpoint_path=(
                    progress
                ),
                longitudinal_analysis=(
                    longitudinal_analysis
                ),
                model_id=(
                    "offline-multi-history"
                ),
                transport_factory=(
                    failing_factory
                ),
            )
        except RuntimeError as exc:
            failed = (
                "intentional fourteenth-call failure"
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
            run_multi_history_attention_replication(
                checkpoint_path=(
                    progress
                ),
                longitudinal_analysis=(
                    longitudinal_analysis
                ),
                model_id=(
                    "offline-multi-history"
                ),
                transport_factory=(
                    good_factory
                ),
            )
        )

        analysis = (
            analyze_multi_history_replication(
                final
            )
        )

    result = {
        "experiment_id": (
            "SL-MULTI-HISTORY-ATTENTION-"
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
        "presentation_order_randomized_by_evidence_id": (
            final[
                "presentation_order_randomized_by_evidence_id"
            ]
        ),
        "all_conditions_keep_all_four_evidence_channels": (
            final[
                "all_conditions_keep_all_four_evidence_channels"
            ]
        ),
        "preregistered_replication_pass_on_scripted_fixture": (
            analysis[
                "preregistered_replication_pass"
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
    ] == 13
    assert len(
        resume_calls
    ) == 23
    assert final[
        "status"
    ] == "COMPLETE"
    assert final[
        "real_cloud_calls_recorded"
    ] == 36
    assert final[
        "presentation_order_randomized_by_evidence_id"
    ] is True
    assert final[
        "all_conditions_keep_all_four_evidence_channels"
    ] is True
    assert final[
        "continuing_individual_mutated"
    ] is False
    assert final[
        "action_policy_feedback_enabled"
    ] is False

    return result
