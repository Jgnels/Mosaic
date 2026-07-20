from __future__ import annotations

import json

from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .longitudinal_reflection_live_runner import (
    run_real_longitudinal_reflection,
)


def _response_for_checkpoint(
    checkpoint: int,
    evidence_ids: list[str],
) -> dict:
    proposals = []

    if evidence_ids:
        proposals.append({
            "hypothesis_domain": "agency",
            "proposition": (
                "A persistent action-selection process continues to be "
                "associated with recurring changes in accessible private state."
            ),
            "confidence": .71,
            "evidence_ids": [
                evidence_ids[0]
            ],
            "rationale": (
                "The current developmental window contains repeated "
                "action-consequence coupling."
            ),
        })

    memory_id = next(
        (
            eid
            for eid in evidence_ids
            if eid.endswith(
                "-MEMORY"
            )
        ),
        None,
    )
    if memory_id is not None:
        proposals.append({
            "hypothesis_domain": "continuity",
            "proposition": (
                "Accessible developmental history remains continuous as "
                "new experienced events accumulate."
            ),
            "confidence": .78,
            "evidence_ids": [
                memory_id
            ],
            "rationale": (
                "The memory sequence expanded while earlier history "
                "remained accessible."
            ),
        })

    return {
        "id": (
            f"longitudinal-offline-{checkpoint}"
        ),
        "status": "completed",
        "steps": [
            {
                "type": "thought",
                "content": [
                    {
                        "text": (
                            "OFFLINE HIDDEN TEST THOUGHT "
                            "MUST NOT ENTER OUTPUT"
                        )
                    }
                ],
            },
            {
                "type": "model_output",
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps({
                            "proposals": proposals
                        }),
                    }
                ],
            },
        ],
    }


def _transport_factory(
    request_cache: dict[int, list[str]],
):
    def factory(
        checkpoint: int,
    ):
        return ScriptedInteractionsTransport(
            _response_for_checkpoint(
                checkpoint,
                request_cache[
                    checkpoint
                ],
            )
        )
    return factory


def run_longitudinal_offline_tests(
    initial_real_reflection_payload: dict,
) -> dict:
    # The live runner creates requests internally, so construct deterministic
    # evidence IDs from the frozen checkpoint naming convention.
    request_cache = {
        checkpoint: [
            f"LONG-{checkpoint}-CAUSAL",
            f"LONG-{checkpoint}-MEMORY",
            f"LONG-{checkpoint}-SOCIAL",
        ]
        for checkpoint in (
            1050,
            1400,
            1800,
        )
    }

    result = (
        run_real_longitudinal_reflection(
            initial_real_reflection_payload=(
                initial_real_reflection_payload
            ),
            model_id=(
                "offline-longitudinal-test"
            ),
            transport_factory=(
                _transport_factory(
                    request_cache
                )
            ),
        )
    )

    # Some windows may not create SOCIAL evidence. The fake provider only cites
    # CAUSAL and MEMORY, which are always generated.
    assert result[
        "real_cloud_calls_made"
    ] == 0
    assert result[
        "final_world_equal_to_no_reflection_control"
    ] is True
    assert result[
        "final_agent_equal_to_no_reflection_control"
    ] is True

    for checkpoint in result[
        "checkpoint_results"
    ]:
        assert checkpoint[
            "world_equal_to_control"
        ] is True
        assert checkpoint[
            "agent_equal_to_control"
        ] is True
        assert checkpoint[
            "reflection_directly_mutated_lived_state"
        ] is False
        assert not checkpoint[
            "attribution_blocked"
        ]
        assert checkpoint[
            "committed"
        ]

    control_records = result[
        "final_control_cohort"
    ][
        "identity"
    ][
        "self_model"
    ][
        "records"
    ]

    reflective_records = result[
        "final_reflective_cohort"
    ][
        "identity"
    ][
        "self_model"
    ][
        "records"
    ]

    assert not control_records
    assert reflective_records

    return {
        "experiment_id": (
            "SL-LONGITUDINAL-REFLECTION-"
            "OFFLINE-INFRASTRUCTURE-001"
        ),
        "checkpoint_count": len(
            result[
                "checkpoint_results"
            ]
        ),
        "exact_world_control_equality": (
            result[
                "final_world_equal_to_no_reflection_control"
            ]
        ),
        "exact_agent_control_equality": (
            result[
                "final_agent_equal_to_no_reflection_control"
            ]
        ),
        "control_self_model_record_count": (
            len(
                control_records
            )
        ),
        "reflective_self_model_record_count": (
            len(
                reflective_records
            )
        ),
        "all_commits_attribution_validated": True,
        "real_cloud_calls_made": 0,
    }
