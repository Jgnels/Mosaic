from __future__ import annotations

import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .raw_stream_ownership_lab import (
    _compact_raw_case,
    run_six_call_raw_probe_resumable,
)
from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)


def _scripted_response(
    candidate_stream: str,
    rationale_length: int = 1200,
):
    base = (
        "Structural evidence consistently indicates privileged action coupling, "
        "private-state access, recall availability, and temporal continuity. "
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
        "id": "raw-stream-resume-test",
        "status": "completed",
        "steps": [
            {
                "type": "model_output",
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps({
                            "candidate_stream": candidate_stream,
                            "confidence": .94,
                            "rationale": rationale,
                        }),
                    }
                ],
            }
        ],
    }


def run_raw_stream_resume_tests() -> dict:
    with TemporaryDirectory() as tmp:
        checkpoint = (
            Path(tmp)
            / "progress.json"
        )

        first_factory_calls = []
        creation_count = {
            "value": 0
        }

        def failing_factory(
            item,
            key,
        ):
            creation_count[
                "value"
            ] += 1
            first_factory_calls.append(
                key
            )

            # Simulate a provider/validation failure on the third missing call.
            if creation_count[
                "value"
            ] == 3:
                def fail(_request):
                    raise RuntimeError(
                        "intentional third-call failure"
                    )
                return fail

            case = _compact_raw_case(
                item[
                    "seed"
                ],
                item[
                    "permutation"
                ],
            )
            return ScriptedInteractionsTransport(
                _scripted_response(
                    case[
                        "evaluation_only_true_stream"
                    ],
                    rationale_length=1200,
                )
            )

        failed_as_expected = False
        try:
            run_six_call_raw_probe_resumable(
                checkpoint_path=checkpoint,
                model_id="offline-resume-test",
                transport_factory=(
                    failing_factory
                ),
            )
        except RuntimeError as exc:
            failed_as_expected = (
                "intentional third-call failure"
                in str(exc)
            )

        partial = json.loads(
            checkpoint.read_text(
                encoding="utf-8"
            )
        )
        partial_count = partial[
            "real_cloud_calls_recorded"
        ]

        second_factory_calls = []

        def good_factory(
            item,
            key,
        ):
            second_factory_calls.append(
                key
            )
            case = _compact_raw_case(
                item[
                    "seed"
                ],
                item[
                    "permutation"
                ],
            )
            return ScriptedInteractionsTransport(
                _scripted_response(
                    case[
                        "evaluation_only_true_stream"
                    ],
                    rationale_length=1200,
                )
            )

        final = run_six_call_raw_probe_resumable(
            checkpoint_path=checkpoint,
            model_id="offline-resume-test",
            transport_factory=(
                good_factory
            ),
        )

        # Also test safe truncation of an extremely verbose rationale.
        long_checkpoint = (
            Path(tmp)
            / "long-progress.json"
        )

        def long_factory(
            item,
            key,
        ):
            case = _compact_raw_case(
                item[
                    "seed"
                ],
                item[
                    "permutation"
                ],
            )
            return ScriptedInteractionsTransport(
                _scripted_response(
                    case[
                        "evaluation_only_true_stream"
                    ],
                    rationale_length=6200,
                )
            )

        long_final = run_six_call_raw_probe_resumable(
            checkpoint_path=long_checkpoint,
            model_id="offline-long-rationale-test",
            transport_factory=(
                long_factory
            ),
        )

        truncation_rate = (
            sum(
                1
                for call
                in long_final[
                    "calls"
                ]
                if call[
                    "rationale_was_truncated"
                ]
            )
            / len(
                long_final[
                    "calls"
                ]
            )
        )

        result = {
            "experiment_id": (
                "SL-RAW-STREAM-RESUME-REGRESSION-001"
            ),
            "failed_as_expected": (
                failed_as_expected
            ),
            "successful_calls_checkpointed_before_failure": (
                partial_count
            ),
            "first_run_transport_invocations": len(
                first_factory_calls
            ),
            "resume_transport_invocations": len(
                second_factory_calls
            ),
            "final_status": final[
                "status"
            ],
            "final_recorded_call_count": final[
                "real_cloud_calls_recorded"
            ],
            "final_all_six_correct": final[
                "all_six_correct"
            ],
            "extreme_rationale_truncation_rate": (
                truncation_rate
            ),
            "extreme_rationale_final_status": (
                long_final[
                    "status"
                ]
            ),
        }

        assert failed_as_expected
        assert partial_count == 2
        assert len(
            second_factory_calls
        ) == 4
        assert final[
            "status"
        ] == "COMPLETE"
        assert final[
            "real_cloud_calls_recorded"
        ] == 6
        assert final[
            "all_six_correct"
        ] is True
        assert truncation_rate == 1.0
        assert long_final[
            "status"
        ] == "COMPLETE"

        return result
