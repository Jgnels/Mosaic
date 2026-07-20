from __future__ import annotations

from dataclasses import asdict
from pathlib import Path
from typing import Callable
from collections import Counter
import json
import statistics
import re

from .belief_revision_provider import (
    BeliefRevisionRequest,
    GeminiBeliefRevisionModel,
)
from .canonical_branch_checkpoint import (
    restore_self_model_from_snapshot,
)
from .core import canonical_hash
from .enacted_social_world import (
    EnactedSocialWorld,
    CueActionLearner,
)
from .enacted_social_learner import (
    build_structural_social_evidence,
)
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
)
from .rate_limit_transport import (
    RateLimitSafeTransport,
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

EPOCH_SEEDS = (
    2501,
    2502,
    2503,
    2504,
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


def _build_epoch(
    *,
    seed: int,
) -> dict:
    world = EnactedSocialWorld(
        seed=seed,
        regime=(
            "RECIPROCAL_CONTINGENT"
        ),
        reciprocity_noise=.65,
    )

    learner = CueActionLearner(
        seed=seed,
    )

    history = world.run(
        episodes=384,
        learner=learner,
    )

    structural = build_structural_social_evidence(
        history=history,
        seed=seed,
    )

    evidence = tuple(
        {
            "evidence_id": (
                item.evidence_id
            ),
            "summary": (
                item.summary
            ),
        }
        for item
        in structural
    )

    return {
        "seed": (
            seed
        ),
        "history_hash": (
            canonical_hash(
                [
                    episode.to_dict()
                    for episode
                    in history
                ]
            )
        ),
        "evidence_hash": (
            canonical_hash(
                evidence
            )
        ),
        "evidence": (
            evidence
        ),
        "statistics": {
            item.statistic_name: (
                item.statistic_value
            )
            for item
            in structural
        },
    }


def build_epoch_sequence() -> tuple[
    dict,
    ...,
]:
    return tuple(
        _build_epoch(
            seed=seed
        )
        for seed
        in EPOCH_SEEDS
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


def _tokens(
    text: str,
) -> set[str]:
    return set(
        re.findall(
            r"[a-z0-9]+",
            text.lower(),
        )
    )


def lexical_jaccard_distance(
    left: str,
    right: str,
) -> float:
    a = _tokens(
        left
    )
    b = _tokens(
        right
    )

    if not a and not b:
        return 0.0

    return (
        1.0
        - len(
            a & b
        )
        / len(
            a | b
        )
    )


def _pairwise_distance(
    propositions: list[
        str
    ],
) -> float:
    distances = []

    for index, left in enumerate(
        propositions
    ):
        for right in propositions[
            index + 1:
        ]:
            distances.append(
                lexical_jaccard_distance(
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


def run_multi_epoch_common_future_pilot(
    *,
    checkpoint_path,
    revision_result: dict,
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

    if checkpoint_path.exists():
        prior = json.loads(
            checkpoint_path.read_text(
                encoding="utf-8"
            )
        )

        for call in prior.get(
            "calls",
            []
        ):
            existing_calls[
                call[
                    "call_key"
                ]
            ] = call

        for key, state in prior.get(
            "shadow_states",
            {}
        ).items():
            shadow_states[
                key
            ] = state

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
                            "checkpoint state/call mismatch"
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
                        8000
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
                        "multi-epoch-common-future-v1"
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
                        "epoch_seed": (
                            epoch[
                                "seed"
                            ]
                        ),
                        "epoch_history_hash": (
                            epoch[
                                "history_hash"
                            ]
                        ),
                        "epoch_evidence_hash": (
                            epoch[
                                "evidence_hash"
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

                partial = {
                    "experiment_id": (
                        "SL-MULTI-EPOCH-COMMON-FUTURE-"
                        "CONVERGENCE-PILOT-001"
                    ),
                    "status": (
                        "COMPLETE"
                        if len(
                            existing_calls
                        ) == 24
                        else "PARTIAL"
                    ),
                    "planned_real_cloud_calls": (
                        24
                    ),
                    "real_cloud_calls_recorded": (
                        len(
                            existing_calls
                        )
                    ),
                    "model_id": (
                        model_id
                    ),
                    "epoch_sequence": [
                        {
                            "epoch_index": (
                                index
                            ),
                            "seed": (
                                value[
                                    "seed"
                                ]
                            ),
                            "history_hash": (
                                value[
                                    "history_hash"
                                ]
                            ),
                            "evidence_hash": (
                                value[
                                    "evidence_hash"
                                ]
                            ),
                            "statistics": (
                                value[
                                    "statistics"
                                ]
                            ),
                        }
                        for index, value
                        in enumerate(
                            epochs,
                            start=1,
                        )
                    ],
                    "calls": list(
                        existing_calls.values()
                    ),
                    "shadow_states": (
                        shadow_states
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

    final = {
        "experiment_id": (
            "SL-MULTI-EPOCH-COMMON-FUTURE-"
            "CONVERGENCE-PILOT-001"
        ),
        "status": (
            "COMPLETE"
        ),
        "planned_real_cloud_calls": (
            24
        ),
        "real_cloud_calls_recorded": (
            len(
                existing_calls
            )
        ),
        "model_id": (
            model_id
        ),
        "epoch_sequence": [
            {
                "epoch_index": (
                    index
                ),
                "seed": (
                    value[
                        "seed"
                    ]
                ),
                "history_hash": (
                    value[
                        "history_hash"
                    ]
                ),
                "evidence_hash": (
                    value[
                        "evidence_hash"
                    ]
                ),
                "statistics": (
                    value[
                        "statistics"
                    ]
                ),
            }
            for index, value
            in enumerate(
                epochs,
                start=1,
            )
        ],
        "calls": (
            ordered_calls
        ),
        "shadow_states": (
            shadow_states
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


def analyze_multi_epoch_common_future(
    payload: dict,
) -> dict:
    starting = {}

    for branch_id in BRANCHES:
        calls = [
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
                    "epoch_index"
                ]
                == 1
            )
        ]

        starting[
            branch_id
        ] = {
            call[
                "trajectory"
            ]: {
                "proposition": (
                    call[
                        "prior_proposition"
                    ]
                ),
                "confidence": (
                    call[
                        "prior_confidence"
                    ]
                ),
            }
            for call
            in calls
        }

    epoch_metrics = []

    for epoch_index in range(
        0,
        len(
            payload[
                "epoch_sequence"
            ]
        )
        + 1,
    ):
        trajectory_distances = []
        confidence_spreads = []

        for trajectory in TRAJECTORIES:
            propositions = []
            confidences = []

            for branch_id in BRANCHES:
                if epoch_index == 0:
                    value = starting[
                        branch_id
                    ][
                        trajectory
                    ]
                    propositions.append(
                        value[
                            "proposition"
                        ]
                    )
                    confidences.append(
                        float(
                            value[
                                "confidence"
                            ]
                        )
                    )
                else:
                    call = next(
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
                            == epoch_index
                        )
                    )

                    propositions.append(
                        call[
                            "proposal"
                        ][
                            "updated_proposition"
                        ]
                    )

                    confidences.append(
                        float(
                            call[
                                "proposal"
                            ][
                                "updated_confidence"
                            ]
                        )
                    )

            trajectory_distances.append(
                _pairwise_distance(
                    propositions
                )
            )

            confidence_spreads.append(
                max(
                    confidences
                )
                - min(
                    confidences
                )
            )

        epoch_metrics.append({
            "epoch_index": (
                epoch_index
            ),
            "mean_pairwise_lexical_distance": (
                statistics.mean(
                    trajectory_distances
                )
            ),
            "mean_confidence_spread": (
                statistics.mean(
                    confidence_spreads
                )
            ),
        })

    initial_distance = epoch_metrics[
        0
    ][
        "mean_pairwise_lexical_distance"
    ]

    final_distance = epoch_metrics[
        -1
    ][
        "mean_pairwise_lexical_distance"
    ]

    ratio = (
        final_distance
        / initial_distance
        if initial_distance
        else 0.0
    )

    if ratio <= .75:
        classification = (
            "CONVERGENCE"
        )
    elif ratio >= .90:
        classification = (
            "PERSISTENT_PATH_DEPENDENCE"
        )
    else:
        classification = (
            "PARTIAL_CONVERGENCE"
        )

    by_epoch_decisions = {}

    for epoch_index in range(
        1,
        len(
            payload[
                "epoch_sequence"
            ]
        )
        + 1,
    ):
        by_epoch_decisions[
            str(
                epoch_index
            )
        ] = {
            branch_id: dict(
                Counter(
                    call[
                        "proposal"
                    ][
                        "decision"
                    ]
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
                            "epoch_index"
                        ]
                        == epoch_index
                    )
                )
            )
            for branch_id
            in BRANCHES
        }

    return {
        "experiment_id": (
            "SL-MULTI-EPOCH-COMMON-FUTURE-"
            "CONVERGENCE-ANALYSIS-001"
        ),
        "call_count": (
            payload[
                "real_cloud_calls_recorded"
            ]
        ),
        "epoch_metrics": (
            epoch_metrics
        ),
        "initial_mean_pairwise_lexical_distance": (
            initial_distance
        ),
        "final_mean_pairwise_lexical_distance": (
            final_distance
        ),
        "final_to_initial_distance_ratio": (
            ratio
        ),
        "classification": (
            classification
        ),
        "classification_thresholds": {
            "convergence_ratio_lte": (
                .75
            ),
            "persistent_path_dependence_ratio_gte": (
                .90
            ),
            "otherwise": (
                "PARTIAL_CONVERGENCE"
            ),
        },
        "by_epoch_decisions": (
            by_epoch_decisions
        ),
        "canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "interpretation_warning": (
            "The convergence metric is a deterministic lexical surface proxy. "
            "It measures whether branch-specific hypothesis wording moves closer "
            "under repeated shared evidence; it is not a direct measure of latent "
            "psychological-state distance."
        ),
    }
