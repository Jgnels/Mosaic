from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .attention_competition_pilot import (
    run_attention_competition_real_pilot,
    analyze_attention_competition_pilot,
)


def _response_for_item(
    item: dict,
):
    target = item[
        "target_domain"
    ]

    evidence_ids = [
        evidence.evidence_id
        for evidence
        in item[
            "request"
        ].evidence
    ]

    if target is None:
        domains = (
            "agency",
            "continuity",
        )
    else:
        # Deterministic offline success: target plus one comparator.
        comparator = (
            "continuity"
            if target
            != "continuity"
            else "agency"
        )
        domains = (
            target,
            comparator,
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
                f"Selective offline update for {domain}."
            ),
            "confidence": (
                .80
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
                "Offline deterministic attention-competition validation."
            ),
        })

    return {
        "id": (
            "attention-competition-offline"
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


def run_attention_competition_offline_tests(
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
            run_attention_competition_real_pilot(
                checkpoint_path=(
                    progress
                ),
                longitudinal_analysis=(
                    longitudinal_analysis
                ),
                model_id=(
                    "offline-attention-competition"
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
            run_attention_competition_real_pilot(
                checkpoint_path=(
                    progress
                ),
                longitudinal_analysis=(
                    longitudinal_analysis
                ),
                model_id=(
                    "offline-attention-competition"
                ),
                transport_factory=(
                    good_factory
                ),
            )
        )

        analysis = (
            analyze_attention_competition_pilot(
                final
            )
        )

    result = {
        "experiment_id": (
            "SL-ATTENTION-COMPETITION-"
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
        "all_conditions_keep_all_four_evidence_channels": (
            final[
                "all_conditions_keep_all_four_evidence_channels"
            ]
        ),
        "response_budget_max_proposals": (
            final[
                "response_budget_max_proposals"
            ]
        ),
        "domains_with_positive_selection_effect": (
            analysis[
                "domains_with_positive_selection_effect"
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
    ] == 9
    assert len(
        resume_calls
    ) == 15
    assert final[
        "status"
    ] == "COMPLETE"
    assert final[
        "real_cloud_calls_recorded"
    ] == 24
    assert final[
        "all_conditions_keep_all_four_evidence_channels"
    ] is True
    assert final[
        "response_budget_max_proposals"
    ] == 2
    assert analysis[
        "domains_with_positive_selection_effect"
    ] >= 2
    assert final[
        "continuing_individual_mutated"
    ] is False
    assert final[
        "action_policy_feedback_enabled"
    ] is False

    return result
