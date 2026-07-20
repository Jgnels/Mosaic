from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .schema_balanced_perspective_lab import (
    compact_balanced_case,
    run_schema_balanced_baseline,
)
from .schema_balanced_provider import (
    canonical_balanced_permutations,
    run_balanced_call,
    run_balanced_probe_resumable,
)
from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)


def _response(
    candidate_stream: str,
    rationale_length: int = 900,
):
    base = (
        "The selected stream shows stable action-conditioned private-state "
        "effects with low within-action variance and low temporal drift. "
    )
    rationale = (
        base
        * (
            rationale_length
            // len(base)
            + 1
        )
    )[:rationale_length]

    return {
        "id": (
            "schema-balanced-offline"
        ),
        "status": "completed",
        "steps": [
            {
                "type": "model_output",
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps({
                            "candidate_stream": (
                                candidate_stream
                            ),
                            "confidence": .93,
                            "causal_signature": [
                                "STABLE_ACTION_STATE_COUPLING",
                                "LOW_WITHIN_ACTION_VARIANCE",
                                "LOW_TEMPORAL_DRIFT",
                                "RECALL_COHERENCE",
                                "CONTINUITY_STABILITY",
                            ],
                            "rationale": rationale,
                        }),
                    }
                ],
            }
        ],
    }


def run_schema_balanced_offline_tests() -> dict:
    baseline = (
        run_schema_balanced_baseline(
            seed_count=128
        )
    )

    scripted_correct = []

    for seed, wording in (
        (1201, "minimal"),
        (2407, "causal"),
    ):
        for permutation in (
            canonical_balanced_permutations()
        ):
            case = compact_balanced_case(
                seed,
                permutation,
            )
            transport = (
                ScriptedInteractionsTransport(
                    _response(
                        case[
                            "evaluation_only_true_stream"
                        ]
                    )
                )
            )
            result = run_balanced_call(
                seed=seed,
                permutation=permutation,
                wording=wording,
                model_id=(
                    "offline-balanced-test"
                ),
                transport=transport,
            )
            scripted_correct.append(
                result[
                    "correct"
                ]
            )

    with TemporaryDirectory() as tmp:
        checkpoint = (
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
            ] == 4:
                def fail(
                    _request
                ):
                    raise RuntimeError(
                        "intentional fourth-call failure"
                    )
                return fail

            case = compact_balanced_case(
                item[
                    "seed"
                ],
                item[
                    "permutation"
                ],
            )
            return ScriptedInteractionsTransport(
                _response(
                    case[
                        "evaluation_only_true_stream"
                    ]
                )
            )

        failed = False
        try:
            run_balanced_probe_resumable(
                checkpoint_path=(
                    checkpoint
                ),
                model_id=(
                    "offline-balanced-resume"
                ),
                transport_factory=(
                    failing_factory
                ),
            )
        except RuntimeError as exc:
            failed = (
                "intentional fourth-call failure"
                in str(
                    exc
                )
            )

        partial = json.loads(
            checkpoint.read_text(
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
            case = compact_balanced_case(
                item[
                    "seed"
                ],
                item[
                    "permutation"
                ],
            )
            return ScriptedInteractionsTransport(
                _response(
                    case[
                        "evaluation_only_true_stream"
                    ],
                    rationale_length=6100,
                )
            )

        final = (
            run_balanced_probe_resumable(
                checkpoint_path=(
                    checkpoint
                ),
                model_id=(
                    "offline-balanced-resume"
                ),
                transport_factory=(
                    good_factory
                ),
            )
        )

    truncation_count = sum(
        1
        for call
        in final[
            "calls"
        ]
        if call[
            "rationale_was_truncated"
        ]
    )

    result = {
        "experiment_id": (
            "SL-SCHEMA-BALANCED-"
            "OFFLINE-INFRASTRUCTURE-001"
        ),
        "baseline": baseline,
        "scripted_provider_correct_rate": (
            sum(
                1
                for value
                in scripted_correct
                if value
            )
            / len(
                scripted_correct
            )
        ),
        "resume_regression": {
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
            "all_six_correct": (
                final[
                    "all_six_correct"
                ]
            ),
            "both_cases_label_invariant": (
                final[
                    "both_cases_label_invariant"
                ]
            ),
            "truncated_calls_after_resume": (
                truncation_count
            ),
        },
    }

    assert baseline[
        "identification_rate"
    ] == 1.0
    assert result[
        "scripted_provider_correct_rate"
    ] == 1.0
    assert failed is True
    assert partial[
        "real_cloud_calls_recorded"
    ] == 3
    assert len(
        resume_calls
    ) == 3
    assert final[
        "status"
    ] == "COMPLETE"
    assert final[
        "all_six_correct"
    ] is True
    assert final[
        "both_cases_label_invariant"
    ] is True
    assert truncation_count == 3

    return result
