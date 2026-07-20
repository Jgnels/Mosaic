from __future__ import annotations

from collections import defaultdict
from pathlib import Path
from typing import Callable
import json
import math
import random
import statistics

from .belief_revision_provider import (
    BeliefRevisionRequest,
    GeminiBeliefRevisionModel,
)
from .canonical_branch_checkpoint import (
    restore_self_model_from_snapshot,
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


BRANCHES = (
    "C1-ENACTED-RECIPROCAL_CONTINGENT",
    "C1-ENACTED-ONE_WAY_ASSISTANCE",
    "C1-ENACTED-NONCONTINGENT_SIGNALS",
)

NEW_TRAJECTORIES = (
    "C",
    "D",
    "E",
    "F",
)

ALL_TRAJECTORIES = (
    "A",
    "B",
    "C",
    "D",
    "E",
    "F",
)


def _branch_snapshot(
    revision_result: dict,
    branch_id: str,
) -> dict:
    if branch_id in revision_result[
        "executed_revisions"
    ]:
        return revision_result[
            "executed_revisions"
        ][
            branch_id
        ][
            "self_model_state"
        ]

    if (
        revision_result[
            "noncontingent_branch"
        ][
            "branch_id"
        ]
        == branch_id
    ):
        return revision_result[
            "noncontingent_branch"
        ][
            "self_model_state"
        ]

    raise KeyError(
        branch_id
    )


def _starting_state(
    *,
    revision_result: dict,
    branch_id: str,
) -> dict:
    snapshot = _branch_snapshot(
        revision_result,
        branch_id,
    )

    store = restore_self_model_from_snapshot(
        snapshot
    )

    hypothesis = store.active(
        "other_minds"
    )

    if hypothesis is None:
        raise RuntimeError(
            "branch has no active other_minds hypothesis"
        )

    return {
        "branch_state_hash": (
            snapshot[
                "state_hash"
            ]
        ),
        "hypothesis_id": (
            hypothesis.hypothesis_id
        ),
        "proposition": (
            hypothesis.proposition
        ),
        "confidence": (
            hypothesis.confidence
        ),
    }


def _extract_existing_ab_finals(
    multi_epoch_payload: dict,
) -> list[dict]:
    rows = []

    for branch_id in BRANCHES:
        for trajectory in (
            "A",
            "B",
        ):
            call = next(
                call
                for call
                in multi_epoch_payload[
                    "calls"
                ]
                if (
                    call[
                        "branch_id"
                    ]
                    == branch_id
                    and call[
                        "trajectory"
                    ]
                    == trajectory
                    and call[
                        "epoch_index"
                    ]
                    == 4
                )
            )

            rows.append({
                "branch_id": (
                    branch_id
                ),
                "trajectory": (
                    trajectory
                ),
                "proposition": (
                    call[
                        "proposal"
                    ][
                        "updated_proposition"
                    ]
                ),
                "confidence": float(
                    call[
                        "proposal"
                    ][
                        "updated_confidence"
                    ]
                ),
                "source": (
                    "existing_v25_shadow_trajectory"
                ),
            })

    return rows


def run_branch_history_signal_replication(
    *,
    checkpoint_path,
    revision_result: dict,
    multi_epoch_payload: dict,
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

    existing_calls = {}
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
            existing_calls[
                call[
                    "call_key"
                ]
            ] = call

        for key, value in prior.get(
            "shadow_states",
            {}
        ).items():
            shadow_states[
                key
            ] = value

        for row in prior.get(
            "semantic_evaluations",
            []
        ):
            semantic_evaluations[
                row[
                    "semantic_call_key"
                ]
            ] = row

    for branch_id in BRANCHES:
        start = _starting_state(
            revision_result=(
                revision_result
            ),
            branch_id=(
                branch_id
            ),
        )

        for trajectory in NEW_TRAJECTORIES:
            key = (
                branch_id
                + "|"
                + trajectory
            )

            if key not in shadow_states:
                shadow_states[
                    key
                ] = {
                    "branch_id": (
                        branch_id
                    ),
                    "trajectory": (
                        trajectory
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
        for branch_id in BRANCHES:
            for trajectory in NEW_TRAJECTORIES:
                state_key = (
                    branch_id
                    + "|"
                    + trajectory
                )

                state = shadow_states[
                    state_key
                ]

                if (
                    state[
                        "completed_epochs"
                    ]
                    >= epoch_index
                ):
                    matching = [
                        call
                        for call
                        in existing_calls.values()
                        if (
                            call[
                                "branch_id"
                            ]
                            == branch_id
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
                        branch_id
                        + "-"
                        + trajectory
                    ),
                    timestamp=(
                        9000
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
                        epoch[
                            "evidence"
                        ]
                    ),
                    prompt_version=(
                        "1.0"
                    ),
                )

                call_key = canonical_hash({
                    "experiment": (
                        "branch-history-signal-replication-v1"
                    ),
                    "branch_id": (
                        branch_id
                    ),
                    "trajectory": (
                        trajectory
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
                    "model_id": (
                        model_id
                    ),
                })

                if call_key in existing_calls:
                    call = existing_calls[
                        call_key
                    ]
                else:
                    transport = (
                        transport_factory(
                            {
                                "phase": (
                                    "revision"
                                ),
                                "branch_id": (
                                    branch_id
                                ),
                                "trajectory": (
                                    trajectory
                                ),
                                "epoch_index": (
                                    epoch_index
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
                        "branch_id": (
                            branch_id
                        ),
                        "trajectory": (
                            trajectory
                        ),
                        "epoch_index": (
                            epoch_index
                        ),
                        "epoch_history_hash": (
                            epoch[
                                "history_hash"
                            ]
                        ),
                        "epoch_statistics": (
                            epoch[
                                "statistics"
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

                    existing_calls[
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

                _write_checkpoint(
                    checkpoint_path=(
                        checkpoint_path
                    ),
                    existing_calls=(
                        existing_calls
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

    final_propositions = _extract_existing_ab_finals(
        multi_epoch_payload
    )

    for branch_id in BRANCHES:
        for trajectory in NEW_TRAJECTORIES:
            state = shadow_states[
                branch_id
                + "|"
                + trajectory
            ]

            final_propositions.append({
                "branch_id": (
                    branch_id
                ),
                "trajectory": (
                    trajectory
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
                "source": (
                    "v28_new_shadow_trajectory"
                ),
            })

    final_propositions = sorted(
        final_propositions,
        key=lambda row: (
            row[
                "branch_id"
            ],
            row[
                "trajectory"
            ],
        ),
    )

    for row in final_propositions:
        semantic_call_key = canonical_hash({
            "experiment": (
                "branch-history-signal-semantic-evaluation-v1"
            ),
            "branch_id": (
                row[
                    "branch_id"
                ]
            ),
            "trajectory": (
                row[
                    "trajectory"
                ]
            ),
            "proposition": (
                row[
                    "proposition"
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
                    **row,
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
            "BSR-"
            + semantic_call_key[
                :16
            ]
        )

        vector = evaluator.evaluate(
            item_id=(
                item_id
            ),
            proposition=(
                row[
                    "proposition"
                ]
            ),
        )

        semantic_evaluations[
            semantic_call_key
        ] = {
            "semantic_call_key": (
                semantic_call_key
            ),
            "branch_id": (
                row[
                    "branch_id"
                ]
            ),
            "trajectory": (
                row[
                    "trajectory"
                ]
            ),
            "proposition": (
                row[
                    "proposition"
                ]
            ),
            "confidence": (
                row[
                    "confidence"
                ]
            ),
            "source": (
                row[
                    "source"
                ]
            ),
            "semantic_vector": (
                vector.to_dict()
            ),
        }

        _write_checkpoint(
            checkpoint_path=(
                checkpoint_path
            ),
            existing_calls=(
                existing_calls
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
            "SL-BRANCH-HISTORY-SIGNAL-"
            "REPLICATION-001"
        ),
        "status": (
            "COMPLETE"
        ),
        "planned_revision_calls": (
            48
        ),
        "revision_calls_recorded": (
            len(
                existing_calls
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
    }


def _write_checkpoint(
    *,
    checkpoint_path: Path,
    existing_calls: dict,
    shadow_states: dict,
    semantic_evaluations: dict,
    model_id: str,
) -> None:
    payload = {
        "experiment_id": (
            "SL-BRANCH-HISTORY-SIGNAL-"
            "REPLICATION-001"
        ),
        "status": (
            "COMPLETE"
            if (
                len(
                    existing_calls
                )
                == 48
                and len(
                    semantic_evaluations
                )
                == 18
            )
            else "PARTIAL"
        ),
        "planned_revision_calls": (
            48
        ),
        "revision_calls_recorded": (
            len(
                existing_calls
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
            existing_calls.values()
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


def _centroid(
    vectors: list[list[float]],
) -> list[float]:
    return [
        statistics.mean(
            vector[index]
            for vector
            in vectors
        )
        for index
        in range(
            len(
                DIMENSIONS
            )
        )
    ]


def _sq_distance(
    left: list[float],
    right: list[float],
) -> float:
    return sum(
        (
            a
            - b
        )
        ** 2
        for a, b
        in zip(
            left,
            right,
        )
    ) / len(
        DIMENSIONS
    )


def _distance(
    left: list[float],
    right: list[float],
) -> float:
    return math.sqrt(
        _sq_distance(
            left,
            right,
        )
    )


def _mean_pairwise_distance(
    vectors: list[list[float]],
) -> float:
    values = []

    for index, left in enumerate(
        vectors
    ):
        for right in vectors[
            index + 1:
        ]:
            values.append(
                _distance(
                    left,
                    right,
                )
            )

    return (
        statistics.mean(
            values
        )
        if values
        else 0.0
    )


def _pseudo_f(
    grouped: dict[
        str,
        list[
            list[
                float
            ]
        ],
    ],
) -> float:
    all_vectors = [
        vector
        for vectors
        in grouped.values()
        for vector
        in vectors
    ]

    grand = _centroid(
        all_vectors
    )

    k = len(
        grouped
    )

    n = len(
        all_vectors
    )

    between = 0.0
    within = 0.0

    for vectors in grouped.values():
        center = _centroid(
            vectors
        )

        between += (
            len(
                vectors
            )
            * _sq_distance(
                center,
                grand,
            )
        )

        within += sum(
            _sq_distance(
                vector,
                center,
            )
            for vector
            in vectors
        )

    if within == 0.0:
        return float(
            "inf"
        )

    return (
        (
            between
            / (
                k
                - 1
            )
        )
        / (
            within
            / (
                n
                - k
            )
        )
    )


def analyze_branch_history_signal_replication(
    *,
    payload: dict,
    v27_analysis: dict,
    permutations: int = 20000,
    seed: int = 2801,
) -> dict:
    grouped = defaultdict(
        list
    )

    for row in payload[
        "semantic_evaluations"
    ]:
        grouped[
            row[
                "branch_id"
            ]
        ].append(
            _vector(
                row
            )
        )

    if any(
        len(
            grouped[
                branch_id
            ]
        )
        != 6
        for branch_id
        in BRANCHES
    ):
        raise RuntimeError(
            "each branch must have exactly six final semantic trajectories"
        )

    centroids = {
        branch_id: _centroid(
            grouped[
                branch_id
            ]
        )
        for branch_id
        in BRANCHES
    }

    between_branch_distance = (
        _mean_pairwise_distance(
            list(
                centroids.values()
            )
        )
    )

    within_branch_dispersion = {
        branch_id: (
            _mean_pairwise_distance(
                grouped[
                    branch_id
                ]
            )
        )
        for branch_id
        in BRANCHES
    }

    mean_within = statistics.mean(
        within_branch_dispersion.values()
    )

    ratio = (
        between_branch_distance
        / mean_within
        if mean_within
        else float(
            "inf"
        )
    )

    observed_f = _pseudo_f(
        grouped
    )

    vectors = [
        vector
        for branch_id
        in BRANCHES
        for vector
        in grouped[
            branch_id
        ]
    ]

    rng = random.Random(
        seed
    )

    exceed = 0

    for _ in range(
        permutations
    ):
        shuffled = list(
            vectors
        )

        rng.shuffle(
            shuffled
        )

        permuted = {
            BRANCHES[
                index
            ]: shuffled[
                index
                * 6:
                (
                    index
                    + 1
                )
                * 6
            ]
            for index
            in range(
                3
            )
        }

        if _pseudo_f(
            permuted
        ) >= observed_f:
            exceed += 1

    p_value = (
        1
        + exceed
    ) / (
        1
        + permutations
    )

    start_vectors = []

    for branch_id in BRANCHES:
        start_item = next(
            item
            for item
            in v27_analysis[
                "item_reliability"
            ].values()
            if (
                item[
                    "branch_id"
                ]
                == branch_id
                and item[
                    "stage"
                ]
                == "START"
            )
        )

        start_vectors.append([
            float(
                start_item[
                    "mean_vector"
                ][
                    dimension
                ]
            )
            for dimension
            in DIMENSIONS
        ])

    initial_distance = (
        _mean_pairwise_distance(
            start_vectors
        )
    )

    final_to_initial = (
        between_branch_distance
        / initial_distance
        if initial_distance
        else 0.0
    )

    if final_to_initial <= .75:
        convergence_classification = (
            "SEMANTIC_CONVERGENCE"
        )
    elif final_to_initial >= .90:
        convergence_classification = (
            "SEMANTIC_PERSISTENT_PATH_DEPENDENCE"
        )
    else:
        convergence_classification = (
            "SEMANTIC_PARTIAL_CONVERGENCE"
        )

    if (
        p_value
        <= .05
        and ratio
        >= 1.0
    ):
        branch_signal_status = (
            "BRANCH_HISTORY_SIGNAL_SEPARATED"
        )
    elif p_value <= .05:
        branch_signal_status = (
            "BRANCH_HISTORY_EFFECT_DETECTABLE_BUT_OVERLAPPING"
        )
    else:
        branch_signal_status = (
            "BRANCH_HISTORY_SIGNAL_UNRESOLVED"
        )

    return {
        "experiment_id": (
            "SL-BRANCH-HISTORY-SIGNAL-"
            "REPLICATION-ANALYSIS-001"
        ),
        "trajectory_count_per_branch": (
            6
        ),
        "total_final_trajectories": (
            18
        ),
        "semantic_evaluator_noise_reference": (
            v27_analysis[
                "mean_within_item_evaluator_distance"
            ]
        ),
        "branch_centroids": (
            centroids
        ),
        "between_branch_centroid_distance": (
            between_branch_distance
        ),
        "within_branch_trajectory_dispersion": (
            within_branch_dispersion
        ),
        "mean_within_branch_trajectory_dispersion": (
            mean_within
        ),
        "branch_signal_to_trajectory_variance_ratio": (
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
        "branch_signal_status": (
            branch_signal_status
        ),
        "aggregated_initial_semantic_distance": (
            initial_distance
        ),
        "replicated_final_semantic_distance": (
            between_branch_distance
        ),
        "final_to_initial_semantic_distance_ratio": (
            final_to_initial
        ),
        "convergence_classification": (
            convergence_classification
        ),
        "canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "interpretation": (
            "Six shadow trajectories per branch under the same four future epochs "
            "provide a direct estimate of provider-level trajectory stochasticity. "
            "The branch-history claim is considered separated only when the branch "
            "grouping is permutation-significant and between-branch centroid "
            "separation is at least as large as average within-branch dispersion."
        ),
    }
