from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .enacted_lived_revision_pilot import (
    run_enacted_lived_revision_pilot,
    analyze_enacted_lived_revision_pilot,
)


def _response_for_item(
    item: dict,
) -> dict:
    regime = item[
        "regime"
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

    if regime == (
        "RECIPROCAL_CONTINGENT"
    ):
        decision = (
            "STRENGTHEN"
        )
        confidence = .91
        proposition = (
            "Repeated interaction history supports reliable cue-conditioned guidance "
            "and contingent bidirectional information exchange with multiple counterparts."
        )
    elif regime == (
        "ONE_WAY_ASSISTANCE"
    ):
        decision = (
            "QUALIFY"
        )
        confidence = .76
        proposition = (
            "Repeated interaction history supports actionable external guidance from "
            "multiple counterparts, while reciprocal contingency is not established."
        )
    else:
        decision = (
            "DOWNWEIGHT"
        )
        confidence = .62
        proposition = (
            "Repeated interaction history provides weak evidence for reliable external "
            "guidance and little evidence for reciprocal contingency."
        )

    return {
        "id": (
            "enacted-lived-offline"
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
                                "Offline deterministic enacted-history validation."
                            ),
                        }),
                    }
                ],
            }
        ],
    }


def run_enacted_lived_revision_offline_tests(
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
            ] == 7:
                def fail(
                    _request
                ):
                    raise RuntimeError(
                        "intentional seventh-call failure"
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
            run_enacted_lived_revision_pilot(
                checkpoint_path=(
                    progress
                ),
                promotion_result=(
                    promotion_result
                ),
                model_id=(
                    "offline-enacted-lived"
                ),
                transport_factory=(
                    failing_factory
                ),
            )
        except RuntimeError as exc:
            failed = (
                "intentional seventh-call failure"
                in str(
                    exc
                )
            )

        partial = json.loads(
            progress.read_text(
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
            run_enacted_lived_revision_pilot(
                checkpoint_path=(
                    progress
                ),
                promotion_result=(
                    promotion_result
                ),
                model_id=(
                    "offline-enacted-lived"
                ),
                transport_factory=(
                    good_factory
                ),
            )
        )

        analysis = (
            analyze_enacted_lived_revision_pilot(
                final
            )
        )

    result = {
        "experiment_id": (
            "SL-ENACTED-LIVED-REVISION-"
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
        "preregistered_enacted_history_pass_on_scripted_fixture": (
            analysis[
                "preregistered_enacted_history_pass"
            ]
        ),
        "raw_history_generated_by_environment_loop": (
            final[
                "raw_history_generated_by_environment_loop"
            ]
        ),
        "structural_evidence_derived_after_history": (
            final[
                "structural_evidence_derived_after_history"
            ]
        ),
        "all_revision_candidates_quarantined": (
            analysis[
                "all_revision_candidates_quarantined"
            ]
        ),
        "automatic_canonical_revision_enabled": (
            analysis[
                "automatic_canonical_revision_enabled"
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
    ] == 6
    assert result[
        "resume_transport_invocations"
    ] == 6
    assert result[
        "final_call_count"
    ] == 12
    assert result[
        "preregistered_enacted_history_pass_on_scripted_fixture"
    ] is True
    assert result[
        "raw_history_generated_by_environment_loop"
    ] is True
    assert result[
        "structural_evidence_derived_after_history"
    ] is True
    assert result[
        "all_revision_candidates_quarantined"
    ] is True
    assert result[
        "automatic_canonical_revision_enabled"
    ] is False
    assert result[
        "action_policy_feedback_enabled"
    ] is False

    return result
