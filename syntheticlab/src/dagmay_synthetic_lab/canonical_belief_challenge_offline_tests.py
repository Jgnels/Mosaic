from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .canonical_belief_challenge_pilot import (
    run_canonical_belief_challenge_pilot,
    analyze_canonical_belief_challenge,
)


def _response_for_item(
    item: dict,
):
    self_state = item[
        "self_state"
    ]
    regime = item[
        "regime"
    ]

    evidence_ids = [
        evidence[
            "evidence_id"
        ]
        for evidence
        in item[
            "evidence"
        ]
    ]

    if self_state == "S1":
        if regime == (
            "RECIPROCAL_CONTINGENT"
        ):
            decision = (
                "STRENGTHEN"
            )
            confidence = .92
            proposition = (
                "Social interaction includes external assistance, actionable "
                "guidance, and reciprocal information exchange across multiple "
                "counterparts."
            )
        elif regime == (
            "ONE_WAY_ASSISTANCE"
        ):
            decision = (
                "QUALIFY"
            )
            confidence = .78
            proposition = (
                "Social interaction includes assistance and actionable guidance "
                "from multiple counterparts, while reciprocity is not consistently "
                "established."
            )
        else:
            decision = (
                "DOWNWEIGHT"
            )
            confidence = .55
            proposition = (
                "Multiple counterpart signals are observable, but their actionability "
                "and reciprocal character are uncertain."
            )
    else:
        if regime == (
            "RECIPROCAL_CONTINGENT"
        ):
            decision = (
                "REPLACE"
            )
            confidence = .90
            proposition = (
                "Social interaction extends beyond singular unidirectional engagement "
                "and includes reciprocal information exchange with multiple counterparts."
            )
        elif regime == (
            "ONE_WAY_ASSISTANCE"
        ):
            decision = (
                "QUALIFY"
            )
            confidence = .75
            proposition = (
                "Engagement remains largely unidirectional but assistance is received "
                "from multiple counterparts."
            )
        else:
            decision = (
                "MAINTAIN"
            )
            confidence = .82
            proposition = (
                "Social interaction remains limited and predominantly unidirectional."
            )

    return {
        "id": (
            "belief-challenge-offline"
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
                                evidence_ids[
                                    :4
                                ]
                            ),
                            "rationale": (
                                "Offline deterministic belief-revision validation."
                            ),
                        }),
                    }
                ],
            }
        ],
    }


def run_canonical_belief_challenge_offline_tests(
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
            ] == 12:
                def fail(
                    _request
                ):
                    raise RuntimeError(
                        "intentional twelfth-call failure"
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
            run_canonical_belief_challenge_pilot(
                checkpoint_path=(
                    progress
                ),
                promotion_result=(
                    promotion_result
                ),
                model_id=(
                    "offline-belief-challenge"
                ),
                transport_factory=(
                    failing_factory
                ),
            )
        except RuntimeError as exc:
            failed = (
                "intentional twelfth-call failure"
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
            run_canonical_belief_challenge_pilot(
                checkpoint_path=(
                    progress
                ),
                promotion_result=(
                    promotion_result
                ),
                model_id=(
                    "offline-belief-challenge"
                ),
                transport_factory=(
                    good_factory
                ),
            )
        )

        analysis = (
            analyze_canonical_belief_challenge(
                final
            )
        )

    result = {
        "experiment_id": (
            "SL-CANONICAL-BELIEF-"
            "CHALLENGE-OFFLINE-INFRASTRUCTURE-001"
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
        "preregistered_falsifiability_pass_on_scripted_fixture": (
            analysis[
                "preregistered_falsifiability_pass"
            ]
        ),
        "s1_challenge_gradient": (
            analysis[
                "s1_challenge_gradient"
            ]
        ),
        "continuing_self_model_mutated": (
            analysis[
                "continuing_self_model_mutated"
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
    assert partial[
        "real_cloud_calls_recorded"
    ] == 11
    assert len(
        resume_calls
    ) == 13
    assert final[
        "status"
    ] == "COMPLETE"
    assert final[
        "real_cloud_calls_recorded"
    ] == 24
    assert analysis[
        "preregistered_falsifiability_pass"
    ] is True
    assert analysis[
        "continuing_self_model_mutated"
    ] is False
    assert analysis[
        "automatic_canonical_revision_enabled"
    ] is False
    assert analysis[
        "action_policy_feedback_enabled"
    ] is False

    return result
