from __future__ import annotations

from collections import defaultdict
from pathlib import Path
from typing import Callable
import json
import random
import statistics

from .autobiographical_retrieval_ablation import (
    BRANCHES,
    _centroid,
    _distance,
    _mean_pairwise_distance,
    _permutation_p,
    _starting_state,
    build_verified_autobiographical_packets,
)
from .belief_revision_provider import (
    BeliefRevisionRequest,
    GeminiBeliefRevisionModel,
)
from .core import canonical_hash
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
)
from .multi_epoch_common_future import (
    build_epoch_sequence,
)
from .rate_limit_transport import (
    RateLimitSafeTransport,
)
from .semantic_belief_vector_provider import (
    DIMENSIONS,
    GeminiSemanticBeliefVectorModel,
)


TRAJECTORIES = (
    "M",
    "N",
    "O",
    "P",
    "Q",
    "R",
)


def _reference_source_for(
    *,
    focal_branch: str,
    trajectory: str,
) -> str:
    others = [
        branch
        for branch
        in BRANCHES
        if branch
        != focal_branch
    ]

    # Three trajectories receive one foreign history and three receive the other.
    index = (
        0
        if trajectory
        in (
            "M",
            "N",
            "O",
        )
        else 1
    )

    return others[
        index
    ]


def _externalized_packet(
    packet: dict,
) -> dict:
    evidence = tuple(
        {
            "evidence_id": (
                item[
                    "evidence_id"
                ].replace(
                    "-RETRIEVED",
                    "-EXTERNAL-REFERENCE",
                )
            ),
            "summary": (
                item[
                    "summary"
                ].replace(
                    "PRIOR LIVED-HISTORY EVIDENCE "
                    "(retrieved context; this is not a new event): ",
                    "EXTERNAL REFERENCE HISTORY "
                    "(this is another branch's history, not this individual's history; "
                    "this is not a new event): ",
                )
            ),
        }
        for item
        in packet[
            "retrieved_evidence"
        ]
    )

    return {
        **packet,
        "external_reference_evidence": (
            evidence
        ),
        "external_reference_evidence_hash": (
            canonical_hash(
                evidence
            )
        ),
    }


def run_crossed_foreign_history_control(
    *,
    checkpoint_path,
    revision_result: dict,
    lived_history_payload: dict,
    model_id: str = (
        "gemini-3.1-flash-lite"
    ),
    transport_factory: Callable
    | None = None,
) -> dict:
    checkpoint_path = Path(
        checkpoint_path
    )

    epochs = build_epoch_sequence()

    own_packets = build_verified_autobiographical_packets(
        lived_history_payload=(
            lived_history_payload
        )
    )

    packets = {
        branch_id: _externalized_packet(
            packet
        )
        for branch_id, packet
        in own_packets.items()
    }

    revision_calls = {}
    shadow_states = {}
    semantic_evaluations = {}

    if checkpoint_path.exists():
        prior = json.loads(
            checkpoint_path.read_text(
                encoding="utf-8"
            )
        )

        for call in prior.get(
            "revision_calls",
            []
        ):
            revision_calls[
                call[
                    "call_key"
                ]
            ] = call

        shadow_states.update(
            prior.get(
                "shadow_states",
                {}
            )
        )

        for row in prior.get(
            "semantic_evaluations",
            []
        ):
            semantic_evaluations[
                row[
                    "semantic_call_key"
                ]
            ] = row

    for focal_branch in BRANCHES:
        start = _starting_state(
            revision_result=(
                revision_result
            ),
            branch_id=(
                focal_branch
            ),
        )

        for trajectory in TRAJECTORIES:
            state_key = (
                focal_branch
                + "|"
                + trajectory
            )

            reference_source = _reference_source_for(
                focal_branch=(
                    focal_branch
                ),
                trajectory=(
                    trajectory
                ),
            )

            if state_key not in shadow_states:
                shadow_states[
                    state_key
                ] = {
                    "focal_branch_id": (
                        focal_branch
                    ),
                    "trajectory": (
                        trajectory
                    ),
                    "reference_history_source_branch_id": (
                        reference_source
                    ),
                    "starting_branch_state_hash": (
                        start[
                            "branch_state_hash"
                        ]
                    ),
                    "starting_hypothesis_id": (
                        start[
                            "hypothesis_id"
                        ]
                    ),
                    "current_proposition": (
                        start[
                            "proposition"
                        ]
                    ),
                    "current_confidence": (
                        start[
                            "confidence"
                        ]
                    ),
                    "completed_epochs": (
                        0
                    ),
                }

    shared_transport = (
        None
        if transport_factory
        is not None
        else RateLimitSafeTransport(
            inner=(
                GeminiInteractionsTransport()
            ),
            minimum_interval_seconds=(
                4.25
            ),
            max_429_retries=(
                5
            ),
            fallback_retry_seconds=(
                65.0
            ),
        )
    )

    ordered_calls = []

    for epoch_index, epoch in enumerate(
        epochs,
        start=1,
    ):
        for focal_branch in BRANCHES:
            for trajectory in TRAJECTORIES:
                state_key = (
                    focal_branch
                    + "|"
                    + trajectory
                )

                state = shadow_states[
                    state_key
                ]

                reference_source = state[
                    "reference_history_source_branch_id"
                ]

                reference_packet = packets[
                    reference_source
                ]

                combined_evidence = (
                    tuple(
                        epoch[
                            "evidence"
                        ]
                    )
                    + tuple(
                        reference_packet[
                            "external_reference_evidence"
                        ]
                    )
                )

                if (
                    state[
                        "completed_epochs"
                    ]
                    >= epoch_index
                ):
                    matching = [
                        call
                        for call
                        in revision_calls.values()
                        if (
                            call[
                                "focal_branch_id"
                            ]
                            == focal_branch
                            and call[
                                "trajectory"
                            ]
                            == trajectory
                            and call[
                                "epoch_index"
                            ]
                            == epoch_index
                        )
                    ]

                    if len(
                        matching
                    ) != 1:
                        raise RuntimeError(
                            "checkpoint revision state/call mismatch"
                        )

                    ordered_calls.append(
                        matching[
                            0
                        ]
                    )

                    continue

                request = BeliefRevisionRequest(
                    individual_id=(
                        focal_branch
                        + "-FOREIGN-REFERENCE-"
                        + trajectory
                    ),
                    timestamp=(
                        11000
                        + epoch_index
                    ),
                    current_hypothesis=(
                        state[
                            "current_proposition"
                        ]
                    ),
                    current_confidence=float(
                        state[
                            "current_confidence"
                        ]
                    ),
                    evidence=(
                        combined_evidence
                    ),
                    prompt_version=(
                        "1.0"
                    ),
                )

                call_key = canonical_hash({
                    "experiment": (
                        "crossed-foreign-history-control-v1"
                    ),
                    "focal_branch_id": (
                        focal_branch
                    ),
                    "trajectory": (
                        trajectory
                    ),
                    "reference_history_source_branch_id": (
                        reference_source
                    ),
                    "epoch_index": (
                        epoch_index
                    ),
                    "prior_proposition": (
                        request.current_hypothesis
                    ),
                    "prior_confidence": (
                        request.current_confidence
                    ),
                    "epoch_history_hash": (
                        epoch[
                            "history_hash"
                        ]
                    ),
                    "reference_history_journal_head": (
                        reference_packet[
                            "journal_head_hash"
                        ]
                    ),
                    "reference_evidence_hash": (
                        reference_packet[
                            "external_reference_evidence_hash"
                        ]
                    ),
                    "model_id": (
                        model_id
                    ),
                })

                if call_key in revision_calls:
                    call = revision_calls[
                        call_key
                    ]
                else:
                    transport = (
                        transport_factory(
                            {
                                "phase": (
                                    "revision"
                                ),
                                "focal_branch_id": (
                                    focal_branch
                                ),
                                "trajectory": (
                                    trajectory
                                ),
                                "reference_history_source_branch_id": (
                                    reference_source
                                ),
                                "request": (
                                    request
                                ),
                                "epoch": (
                                    epoch
                                ),
                            },
                            call_key,
                        )
                        if transport_factory
                        is not None
                        else shared_transport
                    )

                    model = GeminiBeliefRevisionModel(
                        model_id=(
                            model_id
                        ),
                        transport=(
                            transport
                        ),
                        store=False,
                    )

                    proposal = model.revise(
                        request
                    )

                    call = {
                        "call_key": (
                            call_key
                        ),
                        "focal_branch_id": (
                            focal_branch
                        ),
                        "trajectory": (
                            trajectory
                        ),
                        "reference_history_source_branch_id": (
                            reference_source
                        ),
                        "epoch_index": (
                            epoch_index
                        ),
                        "epoch_history_hash": (
                            epoch[
                                "history_hash"
                            ]
                        ),
                        "reference_history_journal_head": (
                            reference_packet[
                                "journal_head_hash"
                            ]
                        ),
                        "reference_evidence_hash": (
                            reference_packet[
                                "external_reference_evidence_hash"
                            ]
                        ),
                        "prior_proposition": (
                            request.current_hypothesis
                        ),
                        "prior_confidence": (
                            request.current_confidence
                        ),
                        "proposal": (
                            proposal.to_dict()
                        ),
                    }

                    revision_calls[
                        call_key
                    ] = call

                state[
                    "current_proposition"
                ] = call[
                    "proposal"
                ][
                    "updated_proposition"
                ]

                state[
                    "current_confidence"
                ] = float(
                    call[
                        "proposal"
                    ][
                        "updated_confidence"
                    ]
                )

                state[
                    "completed_epochs"
                ] = (
                    epoch_index
                )

                ordered_calls.append(
                    call
                )

                _checkpoint(
                    checkpoint_path=(
                        checkpoint_path
                    ),
                    revision_calls=(
                        revision_calls
                    ),
                    shadow_states=(
                        shadow_states
                    ),
                    semantic_evaluations=(
                        semantic_evaluations
                    ),
                    model_id=(
                        model_id
                    ),
                )

    for focal_branch in BRANCHES:
        for trajectory in TRAJECTORIES:
            state = shadow_states[
                focal_branch
                + "|"
                + trajectory
            ]

            reference_source = state[
                "reference_history_source_branch_id"
            ]

            semantic_call_key = canonical_hash({
                "experiment": (
                    "crossed-foreign-history-semantic-v1"
                ),
                "focal_branch_id": (
                    focal_branch
                ),
                "trajectory": (
                    trajectory
                ),
                "reference_history_source_branch_id": (
                    reference_source
                ),
                "proposition": (
                    state[
                        "current_proposition"
                    ]
                ),
                "model_id": (
                    model_id
                ),
            })

            if semantic_call_key in semantic_evaluations:
                continue

            transport = (
                transport_factory(
                    {
                        "phase": (
                            "semantic"
                        ),
                        "focal_branch_id": (
                            focal_branch
                        ),
                        "trajectory": (
                            trajectory
                        ),
                        "reference_history_source_branch_id": (
                            reference_source
                        ),
                        "proposition": (
                            state[
                                "current_proposition"
                            ]
                        ),
                    },
                    semantic_call_key,
                )
                if transport_factory
                is not None
                else shared_transport
            )

            evaluator = GeminiSemanticBeliefVectorModel(
                model_id=(
                    model_id
                ),
                transport=(
                    transport
                ),
                store=False,
            )

            item_id = (
                "XF-"
                + semantic_call_key[
                    :16
                ]
            )

            vector = evaluator.evaluate(
                item_id=(
                    item_id
                ),
                proposition=(
                    state[
                        "current_proposition"
                    ]
                ),
            )

            semantic_evaluations[
                semantic_call_key
            ] = {
                "semantic_call_key": (
                    semantic_call_key
                ),
                "focal_branch_id": (
                    focal_branch
                ),
                "trajectory": (
                    trajectory
                ),
                "reference_history_source_branch_id": (
                    reference_source
                ),
                "proposition": (
                    state[
                        "current_proposition"
                    ]
                ),
                "confidence": (
                    state[
                        "current_confidence"
                    ]
                ),
                "semantic_vector": (
                    vector.to_dict()
                ),
            }

            _checkpoint(
                checkpoint_path=(
                    checkpoint_path
                ),
                revision_calls=(
                    revision_calls
                ),
                shadow_states=(
                    shadow_states
                ),
                semantic_evaluations=(
                    semantic_evaluations
                ),
                model_id=(
                    model_id
                ),
            )

    return {
        "experiment_id": (
            "SL-CROSSED-FOREIGN-HISTORY-"
            "RETRIEVAL-CONTROL-001"
        ),
        "status": (
            "COMPLETE"
        ),
        "condition": (
            "CROSSED_FOREIGN_HISTORY_AS_EXTERNAL_REFERENCE"
        ),
        "planned_revision_calls": (
            72
        ),
        "revision_calls_recorded": (
            len(
                revision_calls
            )
        ),
        "planned_semantic_calls": (
            18
        ),
        "semantic_calls_recorded": (
            len(
                semantic_evaluations
            )
        ),
        "model_id": (
            model_id
        ),
        "revision_calls": (
            ordered_calls
        ),
        "shadow_states": (
            shadow_states
        ),
        "semantic_evaluations": list(
            semantic_evaluations.values()
        ),
        "canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "memory_content_mutated": (
            False
        ),
        "foreign_history_misattributed_as_own": (
            False
        ),
    }


def _checkpoint(
    *,
    checkpoint_path: Path,
    revision_calls: dict,
    shadow_states: dict,
    semantic_evaluations: dict,
    model_id: str,
) -> None:
    payload = {
        "experiment_id": (
            "SL-CROSSED-FOREIGN-HISTORY-"
            "RETRIEVAL-CONTROL-001"
        ),
        "status": (
            "COMPLETE"
            if (
                len(
                    revision_calls
                )
                == 72
                and len(
                    semantic_evaluations
                )
                == 18
            )
            else "PARTIAL"
        ),
        "condition": (
            "CROSSED_FOREIGN_HISTORY_AS_EXTERNAL_REFERENCE"
        ),
        "planned_revision_calls": (
            72
        ),
        "revision_calls_recorded": (
            len(
                revision_calls
            )
        ),
        "planned_semantic_calls": (
            18
        ),
        "semantic_calls_recorded": (
            len(
                semantic_evaluations
            )
        ),
        "model_id": (
            model_id
        ),
        "revision_calls": list(
            revision_calls.values()
        ),
        "shadow_states": (
            shadow_states
        ),
        "semantic_evaluations": list(
            semantic_evaluations.values()
        ),
        "canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "memory_content_mutated": (
            False
        ),
        "foreign_history_misattributed_as_own": (
            False
        ),
    }

    temp = (
        checkpoint_path.with_suffix(
            checkpoint_path.suffix
            + ".tmp"
        )
    )

    temp.write_text(
        json.dumps(
            payload,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    temp.replace(
        checkpoint_path
    )


def _vector(
    row: dict,
) -> list[float]:
    return [
        float(
            row[
                "semantic_vector"
            ][
                "scores"
            ][
                dimension
            ]
        )
        for dimension
        in DIMENSIONS
    ]


def _group_analysis(
    *,
    rows: list[dict],
    grouping_key: str,
    permutations: int,
    seed: int,
) -> dict:
    grouped = defaultdict(
        list
    )

    for row in rows:
        grouped[
            row[
                grouping_key
            ]
        ].append(
            _vector(
                row
            )
        )

    centroids = {
        key: _centroid(
            vectors
        )
        for key, vectors
        in grouped.items()
    }

    between = _mean_pairwise_distance(
        list(
            centroids.values()
        )
    )

    within = {
        key: _mean_pairwise_distance(
            vectors
        )
        for key, vectors
        in grouped.items()
    }

    mean_within = statistics.mean(
        within.values()
    )

    ratio = (
        between
        / mean_within
        if mean_within
        else float(
            "inf"
        )
    )

    observed_f, p_value = _permutation_p(
        grouped,
        permutations=(
            permutations
        ),
        seed=(
            seed
        ),
    )

    return {
        "grouping_key": (
            grouping_key
        ),
        "group_sizes": {
            key: len(
                vectors
            )
            for key, vectors
            in grouped.items()
        },
        "centroids": (
            centroids
        ),
        "between_group_centroid_distance": (
            between
        ),
        "within_group_trajectory_dispersion": (
            within
        ),
        "mean_within_group_trajectory_dispersion": (
            mean_within
        ),
        "signal_to_variance_ratio": (
            ratio
        ),
        "permutation_test": {
            "permutations": (
                permutations
            ),
            "seed": (
                seed
            ),
            "observed_pseudo_f": (
                observed_f
            ),
            "p_value": (
                p_value
            ),
        },
    }


def analyze_crossed_foreign_history_control(
    *,
    payload: dict,
    own_history_analysis: dict,
    baseline_analysis: dict,
    permutations: int = 20000,
    seed: int = 3001,
) -> dict:
    rows = payload[
        "semantic_evaluations"
    ]

    focal = _group_analysis(
        rows=(
            rows
        ),
        grouping_key=(
            "focal_branch_id"
        ),
        permutations=(
            permutations
        ),
        seed=(
            seed
        ),
    )

    source = _group_analysis(
        rows=(
            rows
        ),
        grouping_key=(
            "reference_history_source_branch_id"
        ),
        permutations=(
            permutations
        ),
        seed=(
            seed
            + 1
        ),
    )

    own_p = float(
        own_history_analysis[
            "permutation_test"
        ][
            "p_value"
        ]
    )

    own_between = float(
        own_history_analysis[
            "retrieval_between_branch_centroid_distance"
        ]
    )

    foreign_focal_p = float(
        focal[
            "permutation_test"
        ][
            "p_value"
        ]
    )

    foreign_source_p = float(
        source[
            "permutation_test"
        ][
            "p_value"
        ]
    )

    if (
        own_p
        <= .05
        and foreign_focal_p
        > .05
        and foreign_source_p
        <= .05
    ):
        status = (
            "RETRIEVED_HISTORY_CONTENT_REDIRECTS_TRAJECTORY"
        )
    elif (
        own_p
        <= .05
        and foreign_focal_p
        > .05
        and foreign_source_p
        > .05
    ):
        status = (
            "OWN_HISTORY_ALIGNMENT_SPECIFICITY_SUPPORTED"
        )
    elif (
        foreign_focal_p
        <= .05
        or foreign_source_p
        <= .05
    ):
        status = (
            "RETRIEVAL_CONTEXT_EFFECT_NOT_SPECIFIC_TO_OWN_HISTORY"
        )
    else:
        status = (
            "FOREIGN_HISTORY_CONTROL_INCONCLUSIVE"
        )

    external_ids = {
        evidence_id
        for call
        in payload[
            "revision_calls"
        ]
        for evidence_id
        in call[
            "proposal"
        ][
            "evidence_ids"
        ]
        if evidence_id.endswith(
            "-EXTERNAL-REFERENCE"
        )
    }

    return {
        "experiment_id": (
            "SL-CROSSED-FOREIGN-HISTORY-"
            "RETRIEVAL-CONTROL-ANALYSIS-001"
        ),
        "condition": (
            "CROSSED_FOREIGN_HISTORY_AS_EXTERNAL_REFERENCE"
        ),
        "trajectory_count_per_focal_branch": (
            6
        ),
        "total_final_trajectories": (
            18
        ),
        "focal_branch_grouping": (
            focal
        ),
        "reference_history_source_grouping": (
            source
        ),
        "own_history_reference": {
            "between_branch_centroid_distance": (
                own_between
            ),
            "permutation_p_value": (
                own_p
            ),
            "signal_to_variance_ratio": (
                own_history_analysis[
                    "retrieval_branch_signal_to_trajectory_variance_ratio"
                ]
            ),
            "status": (
                own_history_analysis[
                    "retrieval_status"
                ]
            ),
        },
        "self_model_only_reference": (
            baseline_analysis
        ),
        "control_status": (
            status
        ),
        "proposal_external_reference_evidence_id_count": (
            len(
                external_ids
            )
        ),
        "provenance_warning": (
            "The current belief-revision provider did not require proposals to cite "
            "retrieved/reference evidence IDs even when rationale text used that "
            "context. This control preserves the v29 provider behavior for comparability. "
            "No canonical commits occur; provenance enforcement should be strengthened "
            "before any retrieval-conditioned canonical mutation."
        ),
        "canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "memory_content_mutated": (
            False
        ),
        "foreign_history_misattributed_as_own": (
            False
        ),
    }
