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
from .enacted_lived_revision_pilot import (
    build_enacted_branch,
)
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


BRANCH_TO_REGIME = {
    "C1-ENACTED-RECIPROCAL_CONTINGENT": (
        "RECIPROCAL_CONTINGENT"
    ),
    "C1-ENACTED-ONE_WAY_ASSISTANCE": (
        "ONE_WAY_ASSISTANCE"
    ),
    "C1-ENACTED-NONCONTINGENT_SIGNALS": (
        "NONCONTINGENT_SIGNALS"
    ),
}

BRANCHES = tuple(
    BRANCH_TO_REGIME.keys()
)

TRAJECTORIES = (
    "G",
    "H",
    "I",
    "J",
    "K",
    "L",
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


def build_verified_autobiographical_packets(
    *,
    lived_history_payload: dict,
) -> dict:
    prefork_hash = lived_history_payload[
        "prefork_self_model_hash"
    ]

    packets = {}

    for branch_id, regime in BRANCH_TO_REGIME.items():
        rebuilt = build_enacted_branch(
            regime=(
                regime
            ),
            prefork_hash=(
                prefork_hash
            ),
            seed=int(
                lived_history_payload[
                    "environment_seed"
                ]
            ),
            episodes=384,
        )

        recorded = lived_history_payload[
            "branches"
        ][
            regime
        ]

        if (
            rebuilt[
                "journal_head_hash"
            ]
            != recorded[
                "journal_head_hash"
            ]
        ):
            raise RuntimeError(
                "reconstructed autobiographical history does not match "
                f"recorded journal head for {branch_id}"
            )

        if not rebuilt[
            "journal_verified"
        ]:
            raise RuntimeError(
                "reconstructed autobiographical journal failed verification"
            )

        evidence = tuple(
            {
                "evidence_id": (
                    item[
                        "evidence_id"
                    ]
                    + "-RETRIEVED"
                ),
                "summary": (
                    "PRIOR LIVED-HISTORY EVIDENCE "
                    "(retrieved context; this is not a new event): "
                    + item[
                        "summary"
                    ]
                ),
            }
            for item
            in rebuilt[
                "structural_evidence"
            ]
        )

        packets[
            branch_id
        ] = {
            "branch_id": (
                branch_id
            ),
            "regime": (
                regime
            ),
            "journal_head_hash": (
                rebuilt[
                    "journal_head_hash"
                ]
            ),
            "episode_count": (
                rebuilt[
                    "episode_count"
                ]
            ),
            "retrieved_evidence": (
                evidence
            ),
            "retrieved_evidence_hash": (
                canonical_hash(
                    evidence
                )
            ),
        }

    return packets


def run_autobiographical_retrieval_ablation(
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

    packets = (
        build_verified_autobiographical_packets(
            lived_history_payload=(
                lived_history_payload
            )
        )
    )

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

    for branch_id in BRANCHES:
        start = _starting_state(
            revision_result=(
                revision_result
            ),
            branch_id=(
                branch_id
            ),
        )

        for trajectory in TRAJECTORIES:
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
            history_packet = packets[
                branch_id
            ]

            combined_evidence = (
                tuple(
                    epoch[
                        "evidence"
                    ]
                )
                + tuple(
                    history_packet[
                        "retrieved_evidence"
                    ]
                )
            )

            for trajectory in TRAJECTORIES:
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
                        in revision_calls.values()
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
                        + "-HISTORY-RETRIEVAL-"
                        + trajectory
                    ),
                    timestamp=(
                        10000
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
                        "autobiographical-retrieval-ablation-v1"
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
                    "retrieved_history_journal_head": (
                        history_packet[
                            "journal_head_hash"
                        ]
                    ),
                    "retrieved_evidence_hash": (
                        history_packet[
                            "retrieved_evidence_hash"
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
                                "history_packet": (
                                    history_packet
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
                        "retrieved_history_journal_head": (
                            history_packet[
                                "journal_head_hash"
                            ]
                        ),
                        "retrieved_evidence_hash": (
                            history_packet[
                                "retrieved_evidence_hash"
                            ]
                        ),
                        "retrieved_history_episode_count": (
                            history_packet[
                                "episode_count"
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
                    packets=(
                        packets
                    ),
                    model_id=(
                        model_id
                    ),
                )

    for branch_id in BRANCHES:
        for trajectory in TRAJECTORIES:
            state = shadow_states[
                branch_id
                + "|"
                + trajectory
            ]

            semantic_call_key = canonical_hash({
                "experiment": (
                    "autobiographical-retrieval-semantic-v1"
                ),
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
                "AR-"
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
                packets=(
                    packets
                ),
                model_id=(
                    model_id
                ),
            )

    return {
        "experiment_id": (
            "SL-AUTOBIOGRAPHICAL-RETRIEVAL-"
            "CAUSALITY-ABLATION-001"
        ),
        "status": (
            "COMPLETE"
        ),
        "condition": (
            "TRUE_OWN_HISTORY_RETRIEVAL"
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
        "autobiographical_packets": (
            packets
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
    packets: dict,
    model_id: str,
) -> None:
    payload = {
        "experiment_id": (
            "SL-AUTOBIOGRAPHICAL-RETRIEVAL-"
            "CAUSALITY-ABLATION-001"
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
            "TRUE_OWN_HISTORY_RETRIEVAL"
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
        "autobiographical_packets": (
            packets
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
        for vectors in grouped.values()
        for vector in vectors
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
            for vector in vectors
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


def _permutation_p(
    grouped: dict[
        str,
        list[
            list[
                float
            ]
        ],
    ],
    *,
    permutations: int,
    seed: int,
) -> tuple[
    float,
    float,
]:
    observed = _pseudo_f(
        grouped
    )

    vectors = [
        vector
        for branch_id in BRANCHES
        for vector in grouped[
            branch_id
        ]
    ]

    group_size = len(
        grouped[
            BRANCHES[
                0
            ]
        ]
    )

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
                * group_size:
                (
                    index
                    + 1
                )
                * group_size
            ]
            for index
            in range(
                len(
                    BRANCHES
                )
            )
        }

        if _pseudo_f(
            permuted
        ) >= observed:
            exceed += 1

    return (
        observed,
        (
            1
            + exceed
        )
        / (
            1
            + permutations
        ),
    )


def analyze_autobiographical_retrieval_ablation(
    *,
    payload: dict,
    baseline_analysis: dict,
    v27_analysis: dict,
    permutations: int = 20000,
    seed: int = 2901,
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
            "each branch must have six retrieval trajectories"
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

    between = _mean_pairwise_distance(
        list(
            centroids.values()
        )
    )

    within = {
        branch_id: _mean_pairwise_distance(
            grouped[
                branch_id
            ]
        )
        for branch_id
        in BRANCHES
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

    observed_f, p_value = (
        _permutation_p(
            grouped,
            permutations=(
                permutations
            ),
            seed=(
                seed
            ),
        )
    )

    start_vectors = []

    for branch_id in BRANCHES:
        start_item = next(
            item
            for item in v27_analysis[
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

    initial = _mean_pairwise_distance(
        start_vectors
    )

    convergence_ratio = (
        between
        / initial
        if initial
        else 0.0
    )

    if convergence_ratio <= .75:
        convergence = (
            "SEMANTIC_CONVERGENCE"
        )
    elif convergence_ratio >= .90:
        convergence = (
            "SEMANTIC_PERSISTENT_PATH_DEPENDENCE"
        )
    else:
        convergence = (
            "SEMANTIC_PARTIAL_CONVERGENCE"
        )

    baseline_between = float(
        baseline_analysis[
            "between_branch_centroid_distance"
        ]
    )

    baseline_ratio = float(
        baseline_analysis[
            "branch_signal_to_trajectory_variance_ratio"
        ]
    )

    between_gain = (
        between
        - baseline_between
    )

    ratio_gain = (
        ratio
        - baseline_ratio
    )

    if (
        p_value
        <= .05
        and ratio
        >= 1.0
        and between
        > baseline_between
    ):
        retrieval_status = (
            "OWN_HISTORY_RETRIEVAL_PRESERVES_BRANCH_SIGNAL"
        )
    elif p_value <= .05:
        retrieval_status = (
            "OWN_HISTORY_RETRIEVAL_EFFECT_DETECTABLE_BUT_OVERLAPPING"
        )
    else:
        retrieval_status = (
            "OWN_HISTORY_RETRIEVAL_DOES_NOT_RESOLVE_BRANCH_SIGNAL"
        )

    return {
        "experiment_id": (
            "SL-AUTOBIOGRAPHICAL-RETRIEVAL-"
            "CAUSALITY-ABLATION-ANALYSIS-001"
        ),
        "condition": (
            "TRUE_OWN_HISTORY_RETRIEVAL"
        ),
        "trajectory_count_per_branch": (
            6
        ),
        "total_final_trajectories": (
            18
        ),
        "retrieval_branch_centroids": (
            centroids
        ),
        "retrieval_between_branch_centroid_distance": (
            between
        ),
        "retrieval_within_branch_trajectory_dispersion": (
            within
        ),
        "retrieval_mean_within_branch_trajectory_dispersion": (
            mean_within
        ),
        "retrieval_branch_signal_to_trajectory_variance_ratio": (
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
        "semantic_evaluator_noise_reference": (
            v27_analysis[
                "mean_within_item_evaluator_distance"
            ]
        ),
        "initial_semantic_distance": (
            initial
        ),
        "retrieval_final_to_initial_semantic_distance_ratio": (
            convergence_ratio
        ),
        "retrieval_convergence_classification": (
            convergence
        ),
        "baseline_without_history_retrieval": {
            "between_branch_centroid_distance": (
                baseline_between
            ),
            "branch_signal_to_trajectory_variance_ratio": (
                baseline_ratio
            ),
            "permutation_p_value": (
                baseline_analysis[
                    "permutation_test"
                ][
                    "p_value"
                ]
            ),
            "convergence_classification": (
                baseline_analysis[
                    "convergence_classification"
                ]
            ),
        },
        "retrieval_minus_baseline_between_branch_distance": (
            between_gain
        ),
        "retrieval_minus_baseline_signal_variance_ratio": (
            ratio_gain
        ),
        "retrieval_status": (
            retrieval_status
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
        "interpretation": (
            "This experiment tests whether making verified, branch-local prior lived "
            "history causally available through retrieval attention preserves a "
            "detectable historical signature under the same four later shared "
            "experience epochs. It compares against the v28 SelfModel-only baseline. "
            "A positive result demonstrates a causal role for truthful autobiographical "
            "retrieval, not consciousness or personhood."
        ),
    }
