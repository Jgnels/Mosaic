from __future__ import annotations

from statistics import mean

from .enacted_social_world import (
    REGIMES,
    EnactedSocialWorld,
    CueActionLearner,
)
from .enacted_social_learner import (
    build_structural_social_evidence,
)


def validate_enacted_social_world(
    *,
    seeds=range(
        2201,
        2233,
    ),
    episodes: int = 384,
) -> dict:
    by_regime = {
        regime: []
        for regime
        in REGIMES
    }

    for seed in seeds:
        for regime in REGIMES:
            world = EnactedSocialWorld(
                seed=seed,
                regime=regime,
            )

            learner = CueActionLearner(
                seed=seed,
            )

            history = world.run(
                episodes=episodes,
                learner=learner,
            )

            structural = {
                item.statistic_name: (
                    item.statistic_value
                )
                for item
                in build_structural_social_evidence(
                    history=history,
                    seed=seed,
                )
            }

            by_regime[
                regime
            ].append(
                structural
            )

    summary = {}

    for regime in REGIMES:
        rows = by_regime[
            regime
        ]

        summary[
            regime
        ] = {}

        for key in rows[
            0
        ]:
            values = [
                row[
                    key
                ]
                for row
                in rows
            ]

            summary[
                regime
            ][
                key
            ] = {
                "mean": (
                    mean(
                        values
                    )
                ),
                "min": (
                    min(
                        values
                    )
                ),
                "max": (
                    max(
                        values
                    )
                ),
            }

    reciprocal_contingency_min = (
        summary[
            "RECIPROCAL_CONTINGENT"
        ][
            "response_next_mode_contingency_gain"
        ][
            "min"
        ]
    )

    one_way_contingency_max = (
        summary[
            "ONE_WAY_ASSISTANCE"
        ][
            "response_next_mode_contingency_gain"
        ][
            "max"
        ]
    )

    noncontingent_contingency_max = (
        summary[
            "NONCONTINGENT_SIGNALS"
        ][
            "response_next_mode_contingency_gain"
        ][
            "max"
        ]
    )

    informative_actionability_min = min(
        summary[
            "RECIPROCAL_CONTINGENT"
        ][
            "cue_conditioned_action_gain"
        ][
            "min"
        ],
        summary[
            "ONE_WAY_ASSISTANCE"
        ][
            "cue_conditioned_action_gain"
        ][
            "min"
        ],
    )

    noncontingent_actionability_mean = (
        summary[
            "NONCONTINGENT_SIGNALS"
        ][
            "cue_conditioned_action_gain"
        ][
            "mean"
        ]
    )

    passed = (
        reciprocal_contingency_min
        >= .45
        and one_way_contingency_max
        <= .10
        and noncontingent_contingency_max
        <= .10
        and informative_actionability_min
        >= .30
        and noncontingent_actionability_mean
        <= .35
    )

    return {
        "experiment_id": (
            "SL-ENACTED-SOCIAL-WORLD-"
            "LOCAL-STRUCTURAL-VALIDATION-001"
        ),
        "seed_count": (
            len(
                tuple(
                    seeds
                )
            )
        ),
        "episodes_per_branch": (
            episodes
        ),
        "summary": (
            summary
        ),
        "thresholds": {
            "reciprocal_contingency_min_gte": (
                .45
            ),
            "one_way_contingency_max_lte": (
                .10
            ),
            "noncontingent_contingency_max_lte": (
                .10
            ),
            "informative_actionability_min_gte": (
                .30
            ),
            "noncontingent_actionability_mean_lte": (
                .35
            ),
        },
        "passed": (
            passed
        ),
    }
