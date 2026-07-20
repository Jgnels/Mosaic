from __future__ import annotations

from pathlib import Path
from typing import Callable
from collections import Counter
import json
import statistics

from .canonical_branch_checkpoint import (
    restore_self_model_from_snapshot,
)
from .core import canonical_hash
from .belief_revision_provider import (
    BeliefRevisionRequest,
    GeminiBeliefRevisionModel,
)
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
)
from .rate_limit_transport import (
    RateLimitSafeTransport,
)
from .enacted_social_world import (
    EnactedSocialWorld,
    CueActionLearner,
)
from .enacted_social_learner import (
    build_structural_social_evidence,
)


BRANCHES = (
    "C1-ENACTED-RECIPROCAL_CONTINGENT",
    "C1-ENACTED-ONE_WAY_ASSISTANCE",
    "C1-ENACTED-NONCONTINGENT_SIGNALS",
)


def _common_future_evidence() -> dict:
    # One shared moderately contingent future. It is intentionally between the
    # strongly reciprocal and one-way histories, allowing us to test path dependence
    # without changing action policy or committing any result.
    world = EnactedSocialWorld(
        seed=2401,
        regime=(
            "RECIPROCAL_CONTINGENT"
        ),
        reciprocity_noise=.65,
    )

    learner = CueActionLearner(
        seed=2401,
    )

    history = world.run(
        episodes=384,
        learner=learner,
    )

    structural = build_structural_social_evidence(
        history=history,
        seed=2401,
    )

    return {
        "history_hash": (
            canonical_hash(
                [
                    episode.to_dict()
                    for episode
                    in history
                ]
            )
        ),
        "episode_ids": [
            episode.episode_id
            for episode
            in history
        ],
        "evidence": tuple(
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
        ),
        "statistics": {
            item.statistic_name: (
                item.statistic_value
            )
            for item
            in structural
        },
    }


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


def build_common_future_plan(
    *,
    revision_result: dict,
) -> list[
    dict
]:
    common = _common_future_evidence()

    plan = []

    for branch_id in BRANCHES:
        snapshot = _branch_snapshot(
            revision_result,
            branch_id,
        )

        store = (
            restore_self_model_from_snapshot(
                snapshot
            )
        )

        hypothesis = store.active(
            "other_minds"
        )

        if hypothesis is None:
            raise RuntimeError(
                "branch has no active other_minds hypothesis"
            )

        for replicate in (
            "A",
            "B",
            "C",
            "D",
        ):
            request = BeliefRevisionRequest(
                individual_id=branch_id,
                timestamp=7000,
                current_hypothesis=(
                    hypothesis.proposition
                ),
                current_confidence=(
                    hypothesis.confidence
                ),
                evidence=(
                    common[
                        "evidence"
                    ]
                ),
                prompt_version=(
                    "1.0"
                ),
            )

            key = canonical_hash({
                "experiment": (
                    "common-future-divergence-v1"
                ),
                "branch_id": (
                    branch_id
                ),
                "replicate": (
                    replicate
                ),
                "branch_state_hash": (
                    snapshot[
                        "state_hash"
                    ]
                ),
                "common_future_hash": (
                    common[
                        "history_hash"
                    ]
                ),
            })

            plan.append({
                "call_key": (
                    key
                ),
                "branch_id": (
                    branch_id
                ),
                "replicate": (
                    replicate
                ),
                "branch_state_hash": (
                    snapshot[
                        "state_hash"
                    ]
                ),
                "current_hypothesis_id": (
                    hypothesis.hypothesis_id
                ),
                "request": (
                    request
                ),
                "common_future_history_hash": (
                    common[
                        "history_hash"
                    ]
                ),
                "common_future_statistics": (
                    common[
                        "statistics"
                    ]
                ),
            })

    return plan


def run_common_future_divergence_pilot(
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

    plan = build_common_future_plan(
        revision_result=(
            revision_result
        )
    )

    existing = {}

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
            existing[
                call[
                    "call_key"
                ]
            ] = call

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

    for item in plan:
        key = item[
            "call_key"
        ]

        if key in existing:
            call = existing[
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

            model = GeminiBeliefRevisionModel(
                model_id=model_id,
                transport=transport,
                store=False,
            )

            proposal = model.revise(
                item[
                    "request"
                ]
            )

            call = {
                "call_key": (
                    key
                ),
                "branch_id": (
                    item[
                        "branch_id"
                    ]
                ),
                "replicate": (
                    item[
                        "replicate"
                    ]
                ),
                "branch_state_hash": (
                    item[
                        "branch_state_hash"
                    ]
                ),
                "current_hypothesis_id": (
                    item[
                        "current_hypothesis_id"
                    ]
                ),
                "current_hypothesis": (
                    item[
                        "request"
                    ].current_hypothesis
                ),
                "current_confidence": (
                    item[
                        "request"
                    ].current_confidence
                ),
                "common_future_history_hash": (
                    item[
                        "common_future_history_hash"
                    ]
                ),
                "common_future_statistics": (
                    item[
                        "common_future_statistics"
                    ]
                ),
                "proposal": (
                    proposal.to_dict()
                ),
            }

            existing[
                key
            ] = call

            partial_calls = [
                existing[
                    planned[
                        "call_key"
                    ]
                ]
                for planned
                in plan
                if planned[
                    "call_key"
                ]
                in existing
            ]

            partial = {
                "experiment_id": (
                    "SL-COMMON-FUTURE-"
                    "PSYCHOLOGICAL-DIVERGENCE-PILOT-001"
                ),
                "status": (
                    "COMPLETE"
                    if len(
                        partial_calls
                    ) == 12
                    else "PARTIAL"
                ),
                "planned_real_cloud_calls": (
                    12
                ),
                "real_cloud_calls_recorded": (
                    len(
                        partial_calls
                    )
                ),
                "model_id": (
                    model_id
                ),
                "calls": (
                    partial_calls
                ),
                "identical_common_future_evidence_across_branches": (
                    True
                ),
                "committed_to_canonical_self_model": (
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
            call
        )

    result = {
        "experiment_id": (
            "SL-COMMON-FUTURE-"
            "PSYCHOLOGICAL-DIVERGENCE-PILOT-001"
        ),
        "status": (
            "COMPLETE"
        ),
        "planned_real_cloud_calls": (
            12
        ),
        "real_cloud_calls_recorded": (
            12
        ),
        "model_id": (
            model_id
        ),
        "calls": (
            ordered
        ),
        "identical_common_future_evidence_across_branches": (
            True
        ),
        "committed_to_canonical_self_model": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
    }

    checkpoint_path.write_text(
        json.dumps(
            result,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    return result


def analyze_common_future_divergence(
    payload: dict,
) -> dict:
    by_branch = {}

    for branch_id in BRANCHES:
        calls = [
            call
            for call
            in payload[
                "calls"
            ]
            if call[
                "branch_id"
            ] == branch_id
        ]

        decisions = Counter(
            call[
                "proposal"
            ][
                "decision"
            ]
            for call
            in calls
        )

        confidence_deltas = [
            float(
                call[
                    "proposal"
                ][
                    "updated_confidence"
                ]
            )
            - float(
                call[
                    "current_confidence"
                ]
            )
            for call
            in calls
        ]

        by_branch[
            branch_id
        ] = {
            "call_count": (
                len(
                    calls
                )
            ),
            "decision_counts": dict(
                sorted(
                    decisions.items()
                )
            ),
            "mean_confidence_delta": (
                statistics.mean(
                    confidence_deltas
                )
            ),
        }

    unique_decision_profiles = {
        tuple(
            sorted(
                payload[
                    "decision_counts"
                ].items()
            )
        )
        for payload
        in by_branch.values()
    }

    return {
        "experiment_id": (
            "SL-COMMON-FUTURE-"
            "PSYCHOLOGICAL-DIVERGENCE-ANALYSIS-001"
        ),
        "call_count": (
            payload[
                "real_cloud_calls_recorded"
            ]
        ),
        "by_branch": (
            by_branch
        ),
        "branch_decision_profiles_diverge": (
            len(
                unique_decision_profiles
            )
            > 1
        ),
        "identical_common_future_evidence_across_branches": (
            True
        ),
        "continuing_canonical_self_models_mutated": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "interpretation": (
            "This analytical pilot asks whether branch-specific canonical histories "
            "produce different reflective trajectories when all branches encounter "
            "the same new moderately contingent evidence. Divergence would indicate "
            "path dependence; convergence would indicate that sufficiently similar "
            "new evidence can reduce prior psychological divergence."
        ),
    }
