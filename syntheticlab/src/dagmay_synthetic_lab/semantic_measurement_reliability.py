from __future__ import annotations

from collections import defaultdict
from pathlib import Path
from typing import Callable
import json
import math
import statistics

from .core import canonical_hash
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
)
from .rate_limit_transport import (
    RateLimitSafeTransport,
)
from .semantic_belief_vector_provider import (
    DIMENSIONS,
    GeminiSemanticBeliefVectorModel,
)


SEMANTIC_REPLICATES = (
    "A",
    "B",
    "C",
)

BRANCHES = (
    "C1-ENACTED-RECIPROCAL_CONTINGENT",
    "C1-ENACTED-ONE_WAY_ASSISTANCE",
    "C1-ENACTED-NONCONTINGENT_SIGNALS",
)


def _euclidean(
    left: dict[str, float],
    right: dict[str, float],
) -> float:
    return math.sqrt(
        sum(
            (
                float(left[d])
                - float(right[d])
            )
            ** 2
            for d
            in DIMENSIONS
        )
        / len(
            DIMENSIONS
        )
    )


def _mean_vector(
    vectors: list[dict[str, float]],
) -> dict[str, float]:
    return {
        dimension: statistics.mean(
            vector[dimension]
            for vector
            in vectors
        )
        for dimension
        in DIMENSIONS
    }


def _mean_pairwise_distance(
    vectors: list[dict[str, float]],
) -> float:
    values = []

    for index, left in enumerate(vectors):
        for right in vectors[index + 1:]:
            values.append(
                _euclidean(
                    left,
                    right,
                )
            )

    return (
        statistics.mean(values)
        if values
        else 0.0
    )


def _classification(
    ratio: float,
) -> str:
    if ratio <= .75:
        return (
            "SEMANTIC_CONVERGENCE"
        )

    if ratio >= .90:
        return (
            "SEMANTIC_PERSISTENT_PATH_DEPENDENCE"
        )

    return (
        "SEMANTIC_PARTIAL_CONVERGENCE"
    )


def _existing_rep_a(
    prior_payload: dict,
) -> list[dict]:
    rows = []

    for evaluation in prior_payload[
        "evaluations"
    ]:
        rows.append({
            "semantic_replicate": (
                "A"
            ),
            "item_id": (
                evaluation[
                    "item_id"
                ]
            ),
            "stage": (
                evaluation[
                    "stage"
                ]
            ),
            "branch_id": (
                evaluation[
                    "branch_id"
                ]
            ),
            "trajectory": (
                evaluation[
                    "trajectory"
                ]
            ),
            "proposition": (
                evaluation[
                    "proposition"
                ]
            ),
            "semantic_vector": (
                evaluation[
                    "semantic_vector"
                ]
            ),
            "source": (
                "integrated_v26_real_evaluation"
            ),
        })

    return rows


def _plan_additional_calls(
    prior_payload: dict,
    *,
    model_id: str,
) -> list[dict]:
    plan = []

    for evaluation in prior_payload[
        "evaluations"
    ]:
        for replicate in (
            "B",
            "C",
        ):
            call_key = canonical_hash({
                "experiment": (
                    "semantic-measurement-reliability-v1"
                ),
                "item_id": (
                    evaluation[
                        "item_id"
                    ]
                ),
                "semantic_replicate": (
                    replicate
                ),
                "model_id": (
                    model_id
                ),
                "provider_id": (
                    "google.ai-studio"
                ),
                "prompt_version": (
                    "1.0"
                ),
                "proposition": (
                    evaluation[
                        "proposition"
                    ]
                ),
            })

            plan.append({
                "call_key": (
                    call_key
                ),
                "semantic_replicate": (
                    replicate
                ),
                "item_id": (
                    evaluation[
                        "item_id"
                    ]
                ),
                "stage": (
                    evaluation[
                        "stage"
                    ]
                ),
                "branch_id": (
                    evaluation[
                        "branch_id"
                    ]
                ),
                "trajectory": (
                    evaluation[
                        "trajectory"
                    ]
                ),
                "proposition": (
                    evaluation[
                        "proposition"
                    ]
                ),
            })

    return plan


def run_semantic_measurement_reliability(
    *,
    checkpoint_path,
    prior_payload: dict,
    model_id: str = (
        "gemini-3.1-flash-lite"
    ),
    transport_factory: Callable
    | None = None,
) -> dict:
    checkpoint_path = Path(
        checkpoint_path
    )

    baseline_rows = _existing_rep_a(
        prior_payload
    )

    plan = _plan_additional_calls(
        prior_payload,
        model_id=(
            model_id
        ),
    )

    existing = {}

    if checkpoint_path.exists():
        prior = json.loads(
            checkpoint_path.read_text(
                encoding="utf-8"
            )
        )

        for row in prior.get(
            "new_evaluations",
            []
        ):
            existing[
                row[
                    "call_key"
                ]
            ] = row

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

    ordered_new = []

    for item in plan:
        key = item[
            "call_key"
        ]

        if key in existing:
            row = existing[
                key
            ]
        else:
            transport = (
                transport_factory(
                    item,
                    key,
                )
                if transport_factory
                is not None
                else shared_transport
            )

            model = (
                GeminiSemanticBeliefVectorModel(
                    model_id=(
                        model_id
                    ),
                    transport=(
                        transport
                    ),
                    store=False,
                )
            )

            vector = model.evaluate(
                item_id=(
                    item[
                        "item_id"
                    ]
                ),
                proposition=(
                    item[
                        "proposition"
                    ]
                ),
            )

            row = {
                **item,
                "semantic_vector": (
                    vector.to_dict()
                ),
                "source": (
                    "v27_reliability_replicate"
                ),
            }

            existing[
                key
            ] = row

            partial = {
                "experiment_id": (
                    "SL-SEMANTIC-MEASUREMENT-"
                    "RELIABILITY-001"
                ),
                "status": (
                    "COMPLETE"
                    if len(existing) == 18
                    else "PARTIAL"
                ),
                "planned_new_real_cloud_calls": (
                    18
                ),
                "new_real_cloud_calls_recorded": (
                    len(
                        existing
                    )
                ),
                "model_id": (
                    model_id
                ),
                "new_evaluations": list(
                    existing.values()
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
                    partial,
                    indent=2,
                    sort_keys=True,
                ),
                encoding="utf-8",
            )

            temp.replace(
                checkpoint_path
            )

        ordered_new.append(
            row
        )

    return {
        "experiment_id": (
            "SL-SEMANTIC-MEASUREMENT-"
            "RELIABILITY-001"
        ),
        "status": (
            "COMPLETE"
        ),
        "planned_new_real_cloud_calls": (
            18
        ),
        "new_real_cloud_calls_recorded": (
            len(
                ordered_new
            )
        ),
        "model_id": (
            model_id
        ),
        "baseline_evaluations": (
            baseline_rows
        ),
        "new_evaluations": (
            ordered_new
        ),
        "all_evaluations": (
            baseline_rows
            + ordered_new
        ),
        "canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
    }


def analyze_semantic_measurement_reliability(
    payload: dict,
) -> dict:
    all_rows = payload[
        "all_evaluations"
    ]

    by_item = defaultdict(
        list
    )

    for row in all_rows:
        by_item[
            row[
                "item_id"
            ]
        ].append(
            row
        )

    if any(
        len(rows) != 3
        for rows
        in by_item.values()
    ):
        raise RuntimeError(
            "each semantic item requires exactly three evaluator replicates"
        )

    item_reliability = {}

    for item_id, rows in sorted(
        by_item.items()
    ):
        vectors = [
            row[
                "semantic_vector"
            ][
                "scores"
            ]
            for row
            in rows
        ]

        item_reliability[
            item_id
        ] = {
            "branch_id": (
                rows[
                    0
                ][
                    "branch_id"
                ]
            ),
            "stage": (
                rows[
                    0
                ][
                    "stage"
                ]
            ),
            "trajectory": (
                rows[
                    0
                ][
                    "trajectory"
                ]
            ),
            "mean_evaluator_pairwise_distance": (
                _mean_pairwise_distance(
                    vectors
                )
            ),
            "mean_vector": (
                _mean_vector(
                    vectors
                )
            ),
        }

    evaluator_noise = statistics.mean(
        item[
            "mean_evaluator_pairwise_distance"
        ]
        for item
        in item_reliability.values()
    )

    replicate_classifications = {}

    for replicate in SEMANTIC_REPLICATES:
        rows = [
            row
            for row
            in all_rows
            if row[
                "semantic_replicate"
            ] == replicate
        ]

        starts = {}
        finals = defaultdict(
            list
        )

        for row in rows:
            scores = row[
                "semantic_vector"
            ][
                "scores"
            ]

            if row[
                "stage"
            ] == "START":
                starts[
                    row[
                        "branch_id"
                    ]
                ] = scores
            else:
                finals[
                    row[
                        "branch_id"
                    ]
                ].append(
                    scores
                )

        start_vectors = [
            starts[
                branch_id
            ]
            for branch_id
            in BRANCHES
        ]

        final_branch_means = [
            _mean_vector(
                finals[
                    branch_id
                ]
            )
            for branch_id
            in BRANCHES
        ]

        initial_distance = (
            _mean_pairwise_distance(
                start_vectors
            )
        )

        final_distance = (
            _mean_pairwise_distance(
                final_branch_means
            )
        )

        ratio = (
            final_distance
            / initial_distance
            if initial_distance
            else 0.0
        )

        replicate_classifications[
            replicate
        ] = {
            "initial_distance": (
                initial_distance
            ),
            "final_distance": (
                final_distance
            ),
            "ratio": (
                ratio
            ),
            "classification": (
                _classification(
                    ratio
                )
            ),
        }

    classifications = {
        item[
            "classification"
        ]
        for item
        in replicate_classifications.values()
    }

    aggregated_starts = {}
    aggregated_finals = defaultdict(
        list
    )

    for item in item_reliability.values():
        if item[
            "stage"
        ] == "START":
            aggregated_starts[
                item[
                    "branch_id"
                ]
            ] = item[
                "mean_vector"
            ]
        else:
            aggregated_finals[
                item[
                    "branch_id"
                ]
            ].append(
                item[
                    "mean_vector"
                ]
            )

    aggregated_start_vectors = [
        aggregated_starts[
            branch_id
        ]
        for branch_id
        in BRANCHES
    ]

    aggregated_final_branch_means = {
        branch_id: _mean_vector(
            aggregated_finals[
                branch_id
            ]
        )
        for branch_id
        in BRANCHES
    }

    aggregated_final_vectors = [
        aggregated_final_branch_means[
            branch_id
        ]
        for branch_id
        in BRANCHES
    ]

    initial_distance = (
        _mean_pairwise_distance(
            aggregated_start_vectors
        )
    )

    final_between_branch_distance = (
        _mean_pairwise_distance(
            aggregated_final_vectors
        )
    )

    ratio = (
        final_between_branch_distance
        / initial_distance
        if initial_distance
        else 0.0
    )

    within_branch_trajectory_dispersion = {}

    for branch_id in BRANCHES:
        trajectories = aggregated_finals[
            branch_id
        ]

        within_branch_trajectory_dispersion[
            branch_id
        ] = (
            _euclidean(
                trajectories[
                    0
                ],
                trajectories[
                    1
                ],
            )
        )

    mean_within_branch_trajectory_dispersion = (
        statistics.mean(
            within_branch_trajectory_dispersion.values()
        )
    )

    branch_signal_to_trajectory_variance_ratio = (
        final_between_branch_distance
        / mean_within_branch_trajectory_dispersion
        if mean_within_branch_trajectory_dispersion
        else float(
            "inf"
        )
    )

    if (
        branch_signal_to_trajectory_variance_ratio
        >= 1.5
    ):
        separation_status = (
            "BRANCH_SIGNAL_CLEARLY_EXCEEDS_TRAJECTORY_VARIANCE"
        )
    elif (
        branch_signal_to_trajectory_variance_ratio
        >= 1.0
    ):
        separation_status = (
            "BRANCH_SIGNAL_MODESTLY_EXCEEDS_TRAJECTORY_VARIANCE"
        )
    else:
        separation_status = (
            "BRANCH_SIGNAL_NOT_SEPARATED_FROM_TRAJECTORY_VARIANCE"
        )

    return {
        "experiment_id": (
            "SL-SEMANTIC-MEASUREMENT-"
            "RELIABILITY-ANALYSIS-001"
        ),
        "semantic_evaluator_replicates": (
            3
        ),
        "item_count": (
            len(
                item_reliability
            )
        ),
        "mean_within_item_evaluator_distance": (
            evaluator_noise
        ),
        "item_reliability": (
            item_reliability
        ),
        "replicate_classifications": (
            replicate_classifications
        ),
        "classification_stable_across_semantic_evaluator_replicates": (
            len(
                classifications
            )
            == 1
        ),
        "aggregated_initial_semantic_distance": (
            initial_distance
        ),
        "aggregated_final_between_branch_semantic_distance": (
            final_between_branch_distance
        ),
        "aggregated_final_to_initial_ratio": (
            ratio
        ),
        "aggregated_classification": (
            _classification(
                ratio
            )
        ),
        "within_branch_final_trajectory_dispersion": (
            within_branch_trajectory_dispersion
        ),
        "mean_within_branch_final_trajectory_dispersion": (
            mean_within_branch_trajectory_dispersion
        ),
        "branch_signal_to_trajectory_variance_ratio": (
            branch_signal_to_trajectory_variance_ratio
        ),
        "separation_status": (
            separation_status
        ),
        "canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "interpretation": (
            "This audit separates semantic measurement noise from shadow-trajectory "
            "variation. A convergence classification is not treated as robust branch-"
            "level convergence if between-branch centroid separation is smaller than "
            "within-branch trajectory dispersion."
        ),
    }
