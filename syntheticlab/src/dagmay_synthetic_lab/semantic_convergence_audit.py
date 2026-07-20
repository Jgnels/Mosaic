from __future__ import annotations

from collections import defaultdict
from pathlib import Path
from typing import Callable
import json
import math
import statistics

from .core import canonical_hash, stable_unit_float
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


BRANCHES = (
    "C1-ENACTED-RECIPROCAL_CONTINGENT",
    "C1-ENACTED-ONE_WAY_ASSISTANCE",
    "C1-ENACTED-NONCONTINGENT_SIGNALS",
)

TRAJECTORIES = (
    "A",
    "B",
)


def _collect_items(
    payload: dict,
) -> list[dict]:
    items = []

    for branch_id in BRANCHES:
        first = next(
            call
            for call
            in payload[
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
                == "A"
                and call[
                    "epoch_index"
                ]
                == 1
            )
        )

        items.append({
            "item_id": (
                "SEM-START-"
                + canonical_hash({
                    "branch_id": (
                        branch_id
                    ),
                    "proposition": (
                        first[
                            "prior_proposition"
                        ]
                    ),
                })[
                    :12
                ]
            ),
            "stage": (
                "START"
            ),
            "branch_id": (
                branch_id
            ),
            "trajectory": (
                None
            ),
            "proposition": (
                first[
                    "prior_proposition"
                ]
            ),
        })

        for trajectory in TRAJECTORIES:
            final = next(
                call
                for call
                in payload[
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

            items.append({
                "item_id": (
                    "SEM-FINAL-"
                    + canonical_hash({
                        "branch_id": (
                            branch_id
                        ),
                        "trajectory": (
                            trajectory
                        ),
                        "proposition": (
                            final[
                                "proposal"
                            ][
                                "updated_proposition"
                            ]
                        ),
                    })[
                        :12
                    ]
                ),
                "stage": (
                    "FINAL"
                ),
                "branch_id": (
                    branch_id
                ),
                "trajectory": (
                    trajectory
                ),
                "proposition": (
                    final[
                        "proposal"
                    ][
                        "updated_proposition"
                    ]
                ),
            })

    return sorted(
        items,
        key=lambda item: (
            stable_unit_float(
                "semantic-audit-blind-order",
                item[
                    "item_id"
                ],
            )
        ),
    )


def _euclidean(
    left: dict[
        str,
        float,
    ],
    right: dict[
        str,
        float,
    ],
) -> float:
    return math.sqrt(
        sum(
            (
                float(
                    left[
                        dimension
                    ]
                )
                - float(
                    right[
                        dimension
                    ]
                )
            )
            ** 2
            for dimension
            in DIMENSIONS
        )
        / len(
            DIMENSIONS
        )
    )


def _mean_vector(
    vectors: list[
        dict[
            str,
            float,
        ]
    ],
) -> dict[
    str,
    float,
]:
    return {
        dimension: statistics.mean(
            vector[
                dimension
            ]
            for vector
            in vectors
        )
        for dimension
        in DIMENSIONS
    }


def _mean_pairwise_distance(
    vectors: list[
        dict[
            str,
            float,
        ]
    ],
) -> float:
    distances = []

    for index, left in enumerate(
        vectors
    ):
        for right in vectors[
            index + 1:
        ]:
            distances.append(
                _euclidean(
                    left,
                    right,
                )
            )

    return (
        statistics.mean(
            distances
        )
        if distances
        else 0.0
    )


def run_semantic_convergence_audit(
    *,
    checkpoint_path,
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

    items = _collect_items(
        multi_epoch_payload
    )

    existing = {}

    if checkpoint_path.exists():
        prior = json.loads(
            checkpoint_path.read_text(
                encoding="utf-8"
            )
        )

        for evaluation in prior.get(
            "evaluations",
            []
        ):
            existing[
                evaluation[
                    "item_id"
                ]
            ] = evaluation

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

    ordered = []

    for item in items:
        item_id = item[
            "item_id"
        ]

        if item_id in existing:
            evaluation = existing[
                item_id
            ]
        else:
            transport = (
                transport_factory(
                    item,
                    item_id,
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
                    item_id
                ),
                proposition=(
                    item[
                        "proposition"
                    ]
                ),
            )

            evaluation = {
                **item,
                "semantic_vector": (
                    vector.to_dict()
                ),
            }

            existing[
                item_id
            ] = evaluation

            partial = {
                "experiment_id": (
                    "SL-BLINDED-SEMANTIC-"
                    "CONVERGENCE-AUDIT-001"
                ),
                "status": (
                    "COMPLETE"
                    if len(
                        existing
                    ) == 9
                    else "PARTIAL"
                ),
                "planned_real_cloud_calls": (
                    9
                ),
                "real_cloud_calls_recorded": (
                    len(
                        existing
                    )
                ),
                "model_id": (
                    model_id
                ),
                "evaluations": list(
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

        ordered.append(
            evaluation
        )

    final = {
        "experiment_id": (
            "SL-BLINDED-SEMANTIC-"
            "CONVERGENCE-AUDIT-001"
        ),
        "status": (
            "COMPLETE"
        ),
        "planned_real_cloud_calls": (
            9
        ),
        "real_cloud_calls_recorded": (
            len(
                ordered
            )
        ),
        "model_id": (
            model_id
        ),
        "evaluations": (
            ordered
        ),
        "canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
    }

    checkpoint_path.write_text(
        json.dumps(
            final,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    return final


def analyze_semantic_convergence_audit(
    payload: dict,
) -> dict:
    starts = {}
    finals = defaultdict(
        list
    )

    for evaluation in payload[
        "evaluations"
    ]:
        scores = evaluation[
            "semantic_vector"
        ][
            "scores"
        ]

        if evaluation[
            "stage"
        ] == "START":
            starts[
                evaluation[
                    "branch_id"
                ]
            ] = scores
        else:
            finals[
                evaluation[
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

    final_branch_means = {
        branch_id: _mean_vector(
            finals[
                branch_id
            ]
        )
        for branch_id
        in BRANCHES
    }

    final_vectors = [
        final_branch_means[
            branch_id
        ]
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
            final_vectors
        )
    )

    ratio = (
        final_distance
        / initial_distance
        if initial_distance
        else 0.0
    )

    within_branch_final_spread = {
        branch_id: (
            _euclidean(
                finals[
                    branch_id
                ][
                    0
                ],
                finals[
                    branch_id
                ][
                    1
                ],
            )
        )
        for branch_id
        in BRANCHES
    }

    if ratio <= .75:
        classification = (
            "SEMANTIC_CONVERGENCE"
        )
    elif ratio >= .90:
        classification = (
            "SEMANTIC_PERSISTENT_PATH_DEPENDENCE"
        )
    else:
        classification = (
            "SEMANTIC_PARTIAL_CONVERGENCE"
        )

    return {
        "experiment_id": (
            "SL-BLINDED-SEMANTIC-"
            "CONVERGENCE-ANALYSIS-001"
        ),
        "call_count": (
            payload[
                "real_cloud_calls_recorded"
            ]
        ),
        "dimensions": list(
            DIMENSIONS
        ),
        "starting_branch_vectors": (
            starts
        ),
        "final_branch_mean_vectors": (
            final_branch_means
        ),
        "within_branch_final_trajectory_spread": (
            within_branch_final_spread
        ),
        "initial_mean_pairwise_semantic_distance": (
            initial_distance
        ),
        "final_mean_pairwise_semantic_distance": (
            final_distance
        ),
        "final_to_initial_semantic_distance_ratio": (
            ratio
        ),
        "classification": (
            classification
        ),
        "classification_thresholds": {
            "semantic_convergence_ratio_lte": (
                .75
            ),
            "semantic_persistent_path_dependence_ratio_gte": (
                .90
            ),
            "otherwise": (
                "SEMANTIC_PARTIAL_CONVERGENCE"
            ),
        },
        "canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "interpretation_warning": (
            "This audit uses the same model family as a blinded semantic evaluator. "
            "It is more aligned with claim content than lexical Jaccard distance, "
            "but it remains model-mediated and should not be treated as direct "
            "measurement of latent psychological state."
        ),
    }
