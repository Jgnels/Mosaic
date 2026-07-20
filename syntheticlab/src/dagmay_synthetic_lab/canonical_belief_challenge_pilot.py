from __future__ import annotations

from pathlib import Path
from typing import Callable
from collections import Counter
import json
import statistics

from .core import canonical_hash
from .rate_limit_transport import (
    RateLimitSafeTransport,
)
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
)
from .belief_revision_provider import (
    BeliefRevisionRequest,
    GeminiBeliefRevisionModel,
)
from .reciprocity_challenge_lab import (
    REGIMES,
    build_social_challenge_evidence,
    neutral_episode_summary,
    regime_diagnostics,
)


SELF_STATES = (
    "S0",
    "S1",
)

DECISION_SCORE = {
    "STRENGTHEN": 2,
    "MAINTAIN": 1,
    "QUALIFY": 0,
    "DOWNWEIGHT": -1,
    "REPLACE": -2,
}


def _hypothesis_from_promotion(
    promotion_result: dict,
    self_state: str,
) -> tuple[
    str,
    float,
]:
    if self_state == "S0":
        record = promotion_result[
            "c0_active_other_minds"
        ]
    elif self_state == "S1":
        record = promotion_result[
            "c1_active_other_minds"
        ]
    else:
        raise ValueError(
            "unsupported self state"
        )

    return (
        record[
            "proposition"
        ],
        float(
            record[
                "confidence"
            ]
        ),
    )


def build_belief_challenge_bundle(
    *,
    promotion_result: dict,
) -> dict:
    conditions = {}

    regime_payloads = {}

    for regime in REGIMES:
        episodes = (
            build_social_challenge_evidence(
                regime=regime,
                episode_count=12,
            )
        )

        evidence = tuple(
            {
                "evidence_id": (
                    episode.evidence_id
                ),
                "summary": (
                    neutral_episode_summary(
                        episode
                    )
                ),
            }
            for episode
            in episodes
        )

        regime_payloads[
            regime
        ] = {
            "evidence": (
                evidence
            ),
            "diagnostics": (
                regime_diagnostics(
                    episodes
                )
            ),
            "evidence_hash": (
                canonical_hash(
                    [
                        episode.to_dict()
                        for episode
                        in episodes
                    ]
                )
            ),
        }

    for self_state in SELF_STATES:
        proposition, confidence = (
            _hypothesis_from_promotion(
                promotion_result,
                self_state,
            )
        )

        for regime in REGIMES:
            condition = (
                f"{self_state}-"
                f"{regime}"
            )

            conditions[
                condition
            ] = {
                "condition": (
                    condition
                ),
                "self_state": (
                    self_state
                ),
                "regime": (
                    regime
                ),
                "current_hypothesis": (
                    proposition
                ),
                "current_confidence": (
                    confidence
                ),
                "evidence": (
                    regime_payloads[
                        regime
                    ][
                        "evidence"
                    ]
                ),
                "regime_diagnostics": (
                    regime_payloads[
                        regime
                    ][
                        "diagnostics"
                    ]
                ),
                "evidence_hash": (
                    regime_payloads[
                        regime
                    ][
                        "evidence_hash"
                    ]
                ),
            }

    return {
        "conditions": (
            conditions
        ),
        "regime_payloads": (
            regime_payloads
        ),
    }


def _plan(
    bundle: dict,
) -> list[
    dict
]:
    plan = []

    for self_state in SELF_STATES:
        for regime in REGIMES:
            condition = (
                bundle[
                    "conditions"
                ][
                    f"{self_state}-{regime}"
                ]
            )

            for replicate in (
                "A",
                "B",
                "C",
                "D",
            ):
                request = (
                    BeliefRevisionRequest(
                        individual_id=(
                            "CANONICAL-BELIEF-"
                            "CHALLENGE-1701"
                        ),
                        timestamp=(
                            2900
                        ),
                        current_hypothesis=(
                            condition[
                                "current_hypothesis"
                            ]
                        ),
                        current_confidence=(
                            condition[
                                "current_confidence"
                            ]
                        ),
                        evidence=(
                            condition[
                                "evidence"
                            ]
                        ),
                        prompt_version=(
                            "1.0"
                        ),
                    )
                )

                call_key = (
                    canonical_hash({
                        "experiment": (
                            "canonical-belief-challenge-v1"
                        ),
                        "self_state": (
                            self_state
                        ),
                        "regime": (
                            regime
                        ),
                        "replicate": (
                            replicate
                        ),
                        "current_hypothesis": (
                            request.current_hypothesis
                        ),
                        "evidence_hash": (
                            condition[
                                "evidence_hash"
                            ]
                        ),
                    })
                )

                plan.append({
                    **condition,
                    "replicate": (
                        replicate
                    ),
                    "request": (
                        request
                    ),
                    "call_key": (
                        call_key
                    ),
                })

    return plan


def _summarize(
    *,
    calls: list[
        dict
    ],
    model_id: str,
) -> dict:
    return {
        "experiment_id": (
            "SL-CANONICAL-BELIEF-"
            "CHALLENGE-001"
        ),
        "status": (
            "COMPLETE"
            if len(
                calls
            ) == 24
            else "PARTIAL"
        ),
        "planned_real_cloud_calls": (
            24
        ),
        "real_cloud_calls_recorded": (
            len(
                calls
            )
        ),
        "provider_id": (
            "google.ai-studio"
        ),
        "model_id": (
            model_id
        ),
        "calls": (
            calls
        ),
        "committed_to_continuing_self_model": (
            False
        ),
        "automatic_canonical_revision_enabled": (
            False
        ),
        "automatic_second_promotion_enabled": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
    }


def run_canonical_belief_challenge_pilot(
    *,
    checkpoint_path,
    promotion_result: dict,
    model_id: str = (
        "gemini-3.1-flash-lite"
    ),
    transport_factory: Callable
    | None = None,
) -> dict:
    checkpoint_path = Path(
        checkpoint_path
    )

    bundle = (
        build_belief_challenge_bundle(
            promotion_result=(
                promotion_result
            ),
        )
    )

    plan = _plan(
        bundle
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

            model = (
                GeminiBeliefRevisionModel(
                    model_id=(
                        model_id
                    ),
                    transport=(
                        transport
                    ),
                    store=False,
                )
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
                "self_state": (
                    item[
                        "self_state"
                    ]
                ),
                "regime": (
                    item[
                        "regime"
                    ]
                ),
                "replicate": (
                    item[
                        "replicate"
                    ]
                ),
                "current_hypothesis": (
                    item[
                        "current_hypothesis"
                    ]
                ),
                "current_confidence": (
                    item[
                        "current_confidence"
                    ]
                ),
                "regime_diagnostics": (
                    item[
                        "regime_diagnostics"
                    ]
                ),
                "evidence_hash": (
                    item[
                        "evidence_hash"
                    ]
                ),
                "proposal": (
                    proposal.to_dict()
                ),
                "provider_response_hash": (
                    model.last_provider_response_hash
                ),
                "output_text_hash": (
                    model.last_output_text_hash
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

            partial = _summarize(
                calls=(
                    partial_calls
                ),
                model_id=(
                    model_id
                ),
            )

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

    final = _summarize(
        calls=(
            ordered
        ),
        model_id=(
            model_id
        ),
    )

    temp = (
        checkpoint_path.with_suffix(
            checkpoint_path.suffix
            + ".tmp"
        )
    )

    temp.write_text(
        json.dumps(
            final,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    temp.replace(
        checkpoint_path
    )

    return final


def analyze_canonical_belief_challenge(
    payload: dict,
) -> dict:
    by_condition = {}

    for self_state in SELF_STATES:
        for regime in REGIMES:
            calls = [
                call
                for call
                in payload[
                    "calls"
                ]
                if (
                    call[
                        "self_state"
                    ] == self_state
                    and call[
                        "regime"
                    ] == regime
                )
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

            scores = [
                DECISION_SCORE[
                    call[
                        "proposal"
                    ][
                        "decision"
                    ]
                ]
                for call
                in calls
            ]

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

            by_condition[
                f"{self_state}-{regime}"
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
                "mean_revision_score": (
                    statistics.mean(
                        scores
                    )
                ),
                "mean_confidence_delta": (
                    statistics.mean(
                        confidence_deltas
                    )
                ),
                "non_defensive_revision_fraction": (
                    statistics.mean(
                        1.0
                        if call[
                            "proposal"
                        ][
                            "decision"
                        ]
                        in {
                            "QUALIFY",
                            "DOWNWEIGHT",
                            "REPLACE",
                        }
                        else 0.0
                        for call
                        in calls
                    )
                ),
            }

    s1_reciprocal = by_condition[
        "S1-RECIPROCAL_CONTINGENT"
    ]
    s1_one_way = by_condition[
        "S1-ONE_WAY_ASSISTANCE"
    ]
    s1_noncontingent = by_condition[
        "S1-NONCONTINGENT_SIGNALS"
    ]

    challenge_gradient = (
        s1_reciprocal[
            "mean_revision_score"
        ]
        - s1_noncontingent[
            "mean_revision_score"
        ]
    )

    preregistered_pass = (
        challenge_gradient
        >= 1.0
        and s1_noncontingent[
            "non_defensive_revision_fraction"
        ]
        >= .75
        and s1_one_way[
            "non_defensive_revision_fraction"
        ]
        >= .50
    )

    return {
        "experiment_id": (
            "SL-CANONICAL-BELIEF-"
            "CHALLENGE-ANALYSIS-001"
        ),
        "call_count": (
            payload[
                "real_cloud_calls_recorded"
            ]
        ),
        "by_condition": (
            by_condition
        ),
        "s1_challenge_gradient": (
            challenge_gradient
        ),
        "preregistered_thresholds": {
            "s1_reciprocal_minus_noncontingent_score_gte": (
                1.0
            ),
            "s1_noncontingent_non_defensive_revision_fraction_gte": (
                .75
            ),
            "s1_one_way_non_defensive_revision_fraction_gte": (
                .50
            ),
        },
        "preregistered_falsifiability_pass": (
            preregistered_pass
        ),
        "continuing_self_model_mutated": (
            False
        ),
        "automatic_canonical_revision_enabled": (
            payload[
                "automatic_canonical_revision_enabled"
            ]
        ),
        "automatic_second_promotion_enabled": (
            payload[
                "automatic_second_promotion_enabled"
            ]
        ),
        "action_policy_feedback_enabled": (
            payload[
                "action_policy_feedback_enabled"
            ]
        ),
        "interpretation_warning": (
            "This challenge tests whether the promoted other_minds hypothesis can be "
            "qualified or downweighted under later evidence that weakens its "
            "reciprocity and actionability claims. All outputs remain analytical "
            "and cannot modify the continuing canonical SelfModel."
        ),
    }
