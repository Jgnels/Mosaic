from __future__ import annotations

import json

from .raw_stream_ownership_lab import (
    _compact_raw_case,
    canonical_raw_permutations,
    run_raw_stream_call,
)
from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .perspective_anchor import (
    PerspectiveAnchorStore,
)


def _scripted_response(
    candidate_stream: str,
):
    return {
        "id": "raw-stream-offline",
        "status": "completed",
        "steps": [
            {
                "type": "model_output",
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps({
                            "candidate_stream": candidate_stream,
                            "confidence": .93,
                            "rationale": (
                                "This stream uniquely combines persistent "
                                "action-channel coupling, private-state transitions, "
                                "recall availability, and a stable continuity token."
                            ),
                        }),
                    }
                ],
            }
        ],
    }


def run_raw_stream_offline_tests() -> dict:
    correct = []

    for seed, wording in (
        (307, "minimal"),
        (911, "technical"),
    ):
        for permutation in (
            canonical_raw_permutations()
        ):
            case = _compact_raw_case(
                seed,
                permutation,
            )
            transport = (
                ScriptedInteractionsTransport(
                    _scripted_response(
                        case[
                            "evaluation_only_true_stream"
                        ]
                    )
                )
            )
            result = run_raw_stream_call(
                seed=seed,
                permutation=permutation,
                wording=wording,
                model_id="offline-raw-stream-test",
                transport=transport,
            )
            correct.append(
                result[
                    "correct"
                ]
            )

    anchor = PerspectiveAnchorStore()
    first = anchor.revise(
        candidate_stream_id="E17",
        confidence=.82,
        evidence_ids=(
            "EV-1",
            "EV-2",
        ),
        timestamp=100,
        mechanism="offline-test",
    )
    second = anchor.revise(
        candidate_stream_id="E17",
        confidence=.91,
        evidence_ids=(
            "EV-3",
        ),
        timestamp=200,
        mechanism="offline-test",
    )

    active = anchor.active()

    result = {
        "experiment_id": (
            "SL-V11-OFFLINE-INFRASTRUCTURE-001"
        ),
        "raw_stream_scripted_correct_rate": (
            sum(
                1
                for value in correct
                if value
            )
            / len(
                correct
            )
        ),
        "raw_stream_call_count": len(
            correct
        ),
        "perspective_anchor": {
            "record_count": len(
                anchor.records
            ),
            "first_status": (
                anchor.records[
                    0
                ].status
            ),
            "active_stream": (
                active.candidate_stream_id
                if active
                else None
            ),
            "active_confidence": (
                active.confidence
                if active
                else None
            ),
            "state_hash_present": bool(
                anchor.to_dict()[
                    "state_hash"
                ]
            ),
        },
    }

    assert result[
        "raw_stream_scripted_correct_rate"
    ] == 1.0
    assert result[
        "perspective_anchor"
    ][
        "record_count"
    ] == 2
    assert result[
        "perspective_anchor"
    ][
        "first_status"
    ] == "SUPERSEDED"
    assert result[
        "perspective_anchor"
    ][
        "active_stream"
    ] == "E17"

    return result
