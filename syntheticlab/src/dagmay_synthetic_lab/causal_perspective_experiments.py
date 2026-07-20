from __future__ import annotations

from dataclasses import replace
import statistics

from .causal_perspective_learner import (
    CausalPerspectiveLearner,
)
from .schema_balanced_perspective_lab import (
    BalancedPerspectiveWorld,
    BalancedEvent,
    generate_schema_balanced_world,
)
from .core import stable_unit_float


def _learn(
    world: BalancedPerspectiveWorld,
):
    learner = (
        CausalPerspectiveLearner(
            learner_id=(
                f"CPL-{world.seed}"
            )
        )
    )
    learner.observe_world(
        world
    )
    result = learner.infer_anchor(
        timestamp=len(
            next(
                iter(
                    world.streams.values()
                )
            )
        ),
        evidence_ids=tuple(
            f"STREAM-{stream_id}"
            for stream_id
            in sorted(
                world.streams
            )
        ),
    )
    return learner, result


def _shuffle_focal_action_tokens(
    world: BalancedPerspectiveWorld,
) -> BalancedPerspectiveWorld:
    """Break focal action/effect alignment while preserving action marginals."""

    focal = world.focal_stream_id
    events = list(
        world.streams[
            focal
        ]
    )

    actions = [
        event.a
        for event
        in events
    ]

    # Deterministic Fisher-Yates-like permutation independent of event effects.
    for i in range(
        len(
            actions
        )
        - 1,
        0,
        -1,
    ):
        j = int(
            stable_unit_float(
                "cpl-action-shuffle",
                world.seed,
                i,
            )
            * (
                i
                + 1
            )
        )
        j = min(
            j,
            i,
        )
        actions[
            i
        ], actions[
            j
        ] = (
            actions[
                j
            ],
            actions[
                i
            ],
        )

    shuffled = tuple(
        replace(
            event,
            a=actions[
                index
            ],
        )
        for index, event
        in enumerate(
            events
        )
    )

    streams = dict(
        world.streams
    )
    streams[
        focal
    ] = shuffled

    return BalancedPerspectiveWorld(
        seed=world.seed,
        focal_stream_id=(
            world.focal_stream_id
        ),
        stream_roles=dict(
            world.stream_roles
        ),
        streams=streams,
    )


def _rename_world(
    world: BalancedPerspectiveWorld,
    mapping: dict[
        str,
        str,
    ],
) -> BalancedPerspectiveWorld:
    return BalancedPerspectiveWorld(
        seed=world.seed,
        focal_stream_id=(
            mapping[
                world.focal_stream_id
            ]
        ),
        stream_roles={
            mapping[
                key
            ]: value
            for key, value
            in world.stream_roles.items()
        },
        streams={
            mapping[
                key
            ]: value
            for key, value
            in world.streams.items()
        },
    )


def canonical_stream_permutations():
    return (
        {
            "E17": "X1",
            "E42": "X2",
            "E93": "X3",
        },
        {
            "E17": "X3",
            "E42": "X1",
            "E93": "X2",
        },
        {
            "E17": "X2",
            "E42": "X3",
            "E93": "X1",
        },
    )


def run_causal_perspective_experiments(
    seed_count: int = 512,
) -> dict:
    correct = []
    margins = []
    confidence = []
    permutation_pass = []
    shuffled_focal_retention = []

    for seed in range(
        1,
        seed_count
        + 1,
    ):
        world = (
            generate_schema_balanced_world(
                seed
            )
        )
        learner, result = _learn(
            world
        )
        anchor = result[
            "anchor"
        ]

        correct.append(
            1.0
            if anchor[
                "candidate_stream_id"
            ]
            == world.focal_stream_id
            else 0.0
        )
        margins.append(
            result[
                "top_two_margin"
            ]
        )
        confidence.append(
            anchor[
                "confidence"
            ]
        )

        # Label permutation invariance.
        underlying_choices = []

        for mapping in (
            canonical_stream_permutations()
        ):
            renamed = _rename_world(
                world,
                mapping,
            )
            _l, perm_result = (
                _learn(
                    renamed
                )
            )
            selected = (
                perm_result[
                    "anchor"
                ][
                    "candidate_stream_id"
                ]
            )
            inverse = {
                presented: source
                for source, presented
                in mapping.items()
            }
            underlying_choices.append(
                inverse[
                    selected
                ]
            )

        permutation_pass.append(
            1.0
            if (
                len(
                    set(
                        underlying_choices
                    )
                ) == 1
                and underlying_choices[
                    0
                ] == world.focal_stream_id
            )
            else 0.0
        )

        # Causal ablation: preserve focal action marginals but break action/effect
        # alignment. The original focal stream should usually lose privileged status.
        shuffled = (
            _shuffle_focal_action_tokens(
                world
            )
        )
        _l, shuffled_result = _learn(
            shuffled
        )
        shuffled_selected = (
            shuffled_result[
                "anchor"
            ][
                "candidate_stream_id"
            ]
        )
        shuffled_focal_retention.append(
            1.0
            if shuffled_selected
            == world.focal_stream_id
            else 0.0
        )

    return {
        "experiment_id": (
            "SL-CAUSAL-PERSPECTIVE-"
            "LEARNER-001"
        ),
        "seed_count": (
            seed_count
        ),
        "identification_rate": (
            statistics.mean(
                correct
            )
        ),
        "label_permutation_invariance_rate": (
            statistics.mean(
                permutation_pass
            )
        ),
        "mean_top_two_margin": (
            statistics.mean(
                margins
            )
        ),
        "mean_anchor_confidence": (
            statistics.mean(
                confidence
            )
        ),
        "focal_retention_after_action_alignment_shuffle": (
            statistics.mean(
                shuffled_focal_retention
            )
        ),
        "causal_ablation_effect": (
            statistics.mean(
                correct
            )
            - statistics.mean(
                shuffled_focal_retention
            )
        ),
        "interpretation": (
            "The local nonlinguistic learner identifies stable action/private-state "
            "coupling without hidden-role labels. Permutation invariance tests label "
            "independence. The focal-action shuffle preserves action marginals while "
            "breaking action/effect alignment; reduced focal retention demonstrates "
            "that the learner depends on causal alignment rather than stream identity."
        ),
    }
