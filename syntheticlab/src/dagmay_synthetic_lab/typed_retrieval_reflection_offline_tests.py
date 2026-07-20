from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .typed_retrieval_reflection_pilot import (
    run_typed_retrieval_real_reflection_pilot,
    analyze_typed_retrieval_real_pilot,
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
    ] == "F0_BASELINE":
        domains = (
            "agency",
            "continuity",
            "embodiment",
        )
    else:
        domains = (
            item[
                "target_domain"
            ],
            "agency",
        )

    proposals = []

    for index, domain in enumerate(
        domains
    ):
        proposals.append({
            "hypothesis_domain": domain,
            "proposition": (
                f"Offline structured proposal concerning {domain}."
            ),
            "confidence": .75,
            "evidence_ids": [
                evidence_ids[
                    index
                    % len(
                        evidence_ids
                    )
                ]
            ],
            "rationale": (
                "Offline deterministic pilot validation."
            ),
        })

    return {
        "id": (
            "typed-retrieval-offline"
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
                            "proposals": (
                                proposals
                            )
                        }),
                    }
                ],
            }
        ],
    }


def run_typed_retrieval_reflection_offline_tests(
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
            ] == 6:
                def fail(
                    _request
                ):
                    raise RuntimeError(
                        "intentional sixth-call failure"
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
            run_typed_retrieval_real_reflection_pilot(
                checkpoint_path=(
                    progress
                ),
                longitudinal_analysis=(
                    longitudinal_analysis
                ),
                model_id=(
                    "offline-typed-retrieval"
                ),
                transport_factory=(
                    failing_factory
                ),
            )
        except RuntimeError as exc:
            failed = (
                "intentional sixth-call failure"
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
            run_typed_retrieval_real_reflection_pilot(
                checkpoint_path=(
                    progress
                ),
                longitudinal_analysis=(
                    longitudinal_analysis
                ),
                model_id=(
                    "offline-typed-retrieval"
                ),
                transport_factory=(
                    good_factory
                ),
            )
        )

        analysis = (
            analyze_typed_retrieval_real_pilot(
                final
            )
        )

    result = {
        "experiment_id": (
            "SL-TYPED-RETRIEVAL-"
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
        "provider_facing_evidence_ids_are_opaque": (
            final[
                "provider_facing_evidence_ids_are_opaque"
            ]
        ),
        "domains_with_positive_target_prevalence_effect": (
            analysis[
                "domains_with_positive_target_prevalence_effect"
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
    ] == 5
    assert len(
        resume_calls
    ) == 7
    assert final[
        "status"
    ] == "COMPLETE"
    assert final[
        "real_cloud_calls_recorded"
    ] == 12
    assert final[
        "provider_facing_evidence_ids_are_opaque"
    ] is True
    assert final[
        "continuing_individual_mutated"
    ] is False
    assert final[
        "action_policy_feedback_enabled"
    ] is False
    assert analysis[
        "domains_with_positive_target_prevalence_effect"
    ] >= 1

    return result
