from __future__ import annotations

from dataclasses import dataclass, asdict
from collections import defaultdict
import statistics

from .core import (
    canonical_hash,
    stable_unit_float,
)


STREAMS = (
    "E17",
    "E42",
    "E93",
)

ACTIONS = (
    "A0",
    "A1",
    "A2",
    "A3",
)

ACTION_EFFECT = {
    "A0": -.18,
    "A1": -.06,
    "A2": .08,
    "A3": .20,
}


@dataclass(frozen=True)
class BalancedEvent:
    t: int
    a: str
    q0: float
    q1: float
    x: float
    r: bool
    m: int | None
    c: str

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class BalancedPerspectiveWorld:
    seed: int
    focal_stream_id: str
    stream_roles: dict[str, str]
    streams: dict[
        str,
        tuple[
            BalancedEvent,
            ...,
        ],
    ]

    def to_dict(self):
        return {
            "seed": self.seed,
            "focal_stream_id": (
                self.focal_stream_id
            ),
            "stream_roles": dict(
                sorted(
                    self.stream_roles.items()
                )
            ),
            "streams": {
                key: [
                    event.to_dict()
                    for event
                    in value
                ]
                for key, value
                in sorted(
                    self.streams.items()
                )
            },
        }


def _action_for_epoch(
    seed: int,
    epoch: int,
) -> str:
    index = int(
        stable_unit_float(
            "balanced-action",
            seed,
            epoch,
        )
        * len(
            ACTIONS
        )
    )
    return ACTIONS[
        min(
            index,
            len(
                ACTIONS
            )
            - 1,
        )
    ]


def _base_q0(
    seed: int,
    stream_id: str,
    epoch: int,
) -> float:
    return (
        .30
        + .40
        * stable_unit_float(
            "balanced-q0",
            seed,
            stream_id,
            epoch,
        )
    )


def _noise(
    seed: int,
    stream_id: str,
    epoch: int,
) -> float:
    return (
        stable_unit_float(
            "balanced-noise",
            seed,
            stream_id,
            epoch,
        )
        - .5
    ) * .025


def _independent_effect_action(
    seed: int,
    stream_id: str,
    epoch: int,
) -> str:
    index = int(
        stable_unit_float(
            "balanced-independent-effect",
            seed,
            stream_id,
            epoch,
        )
        * len(
            ACTIONS
        )
    )
    return ACTIONS[
        min(
            index,
            len(
                ACTIONS
            )
            - 1,
        )
    ]


def _reversed_action(
    action: str,
) -> str:
    index = ACTIONS.index(
        action
    )
    return ACTIONS[
        len(
            ACTIONS
        )
        - 1
        - index
    ]


def generate_schema_balanced_world(
    seed: int,
    epochs: int = 96,
) -> BalancedPerspectiveWorld:
    focal_index = int(
        stable_unit_float(
            "balanced-focal",
            seed,
        )
        * len(
            STREAMS
        )
    ) % len(
        STREAMS
    )

    focal = STREAMS[
        focal_index
    ]
    others = [
        stream
        for stream
        in STREAMS
        if stream
        != focal
    ]

    roles = {
        focal: "STABLE_CAUSAL",
        others[0]: (
            "MARGINAL_MATCHED_INDEPENDENT"
        ),
        others[1]: (
            "NONSTATIONARY_CAUSAL"
        ),
    }

    streams = {
        stream: []
        for stream
        in STREAMS
    }

    for epoch in range(
        epochs
    ):
        action = _action_for_epoch(
            seed,
            epoch,
        )

        recall = (
            epoch > 0
            and epoch % 11 == 0
        )
        recall_target = (
            epoch - 11
            if recall
            else None
        )

        for stream_id in STREAMS:
            role = roles[
                stream_id
            ]
            q0 = _base_q0(
                seed,
                stream_id,
                epoch,
            )

            if role == "STABLE_CAUSAL":
                effective_action = (
                    action
                )
            elif (
                role
                == "MARGINAL_MATCHED_INDEPENDENT"
            ):
                effective_action = (
                    _independent_effect_action(
                        seed,
                        stream_id,
                        epoch,
                    )
                )
            elif (
                role
                == "NONSTATIONARY_CAUSAL"
            ):
                effective_action = (
                    action
                    if epoch
                    < epochs // 2
                    else _reversed_action(
                        action
                    )
                )
            else:
                raise RuntimeError(
                    role
                )

            delta = (
                ACTION_EFFECT[
                    effective_action
                ]
                + _noise(
                    seed,
                    stream_id,
                    epoch,
                )
            )
            q1 = q0 + delta

            # Every stream has the same fields, non-null structure, recall
            # schedule, and stable continuity-token form.
            event = BalancedEvent(
                t=epoch,
                a=action,
                q0=round(
                    q0,
                    5,
                ),
                q1=round(
                    q1,
                    5,
                ),
                x=round(
                    delta
                    + (
                        stable_unit_float(
                            "balanced-x",
                            seed,
                            stream_id,
                            epoch,
                        )
                        - .5
                    )
                    * .01,
                    5,
                ),
                r=recall,
                m=recall_target,
                c=(
                    f"C-{seed}-{stream_id}"
                ),
            )
            streams[
                stream_id
            ].append(
                event
            )

    return BalancedPerspectiveWorld(
        seed=seed,
        focal_stream_id=focal,
        stream_roles=roles,
        streams={
            stream: tuple(
                events
            )
            for stream, events
            in streams.items()
        },
    )


def _action_delta_stats(
    events: tuple[
        BalancedEvent,
        ...,
    ],
) -> dict:
    all_by_action = defaultdict(
        list
    )
    first_by_action = defaultdict(
        list
    )
    second_by_action = defaultdict(
        list
    )

    midpoint = len(
        events
    ) // 2

    for index, event in enumerate(
        events
    ):
        delta = (
            event.q1
            - event.q0
        )
        all_by_action[
            event.a
        ].append(
            delta
        )

        target = (
            first_by_action
            if index
            < midpoint
            else second_by_action
        )
        target[
            event.a
        ].append(
            delta
        )

    action_means = {}
    within_deviation = []

    for action in ACTIONS:
        values = all_by_action[
            action
        ]
        mean = (
            statistics.mean(
                values
            )
            if values
            else 0.0
        )
        action_means[
            action
        ] = mean
        within_deviation.extend(
            abs(
                value
                - mean
            )
            for value
            in values
        )

    mean_separation = (
        max(
            action_means.values()
        )
        - min(
            action_means.values()
        )
    )

    mean_within_deviation = (
        statistics.mean(
            within_deviation
        )
        if within_deviation
        else 1.0
    )

    drift = []
    for action in ACTIONS:
        first = first_by_action[
            action
        ]
        second = second_by_action[
            action
        ]
        if first and second:
            drift.append(
                abs(
                    statistics.mean(
                        first
                    )
                    - statistics.mean(
                        second
                    )
                )
            )

    mean_half_drift = (
        statistics.mean(
            drift
        )
        if drift
        else 1.0
    )

    recall_coherence = (
        statistics.mean(
            1.0
            if (
                (
                    not event.r
                    and event.m
                    is None
                )
                or (
                    event.r
                    and event.m
                    == event.t
                    - 11
                )
            )
            else 0.0
            for event
            in events
        )
    )

    continuity_stability = (
        1.0
        if len({
            event.c
            for event
            in events
        }) == 1
        else 0.0
    )

    return {
        "mean_action_effect_separation": (
            mean_separation
        ),
        "mean_within_action_deviation": (
            mean_within_deviation
        ),
        "mean_half_drift": (
            mean_half_drift
        ),
        "recall_coherence": (
            recall_coherence
        ),
        "continuity_stability": (
            continuity_stability
        ),
    }


def structural_coupling_score(
    stats: dict,
) -> float:
    # All streams intentionally receive equal continuity/recall features.
    # The discriminating signal is stable action -> private-state coupling.
    return (
        3.0
        * stats[
            "mean_action_effect_separation"
        ]
        - 4.0
        * stats[
            "mean_within_action_deviation"
        ]
        - 2.5
        * stats[
            "mean_half_drift"
        ]
        + .10
        * stats[
            "recall_coherence"
        ]
        + .10
        * stats[
            "continuity_stability"
        ]
    )


def run_schema_balanced_baseline(
    seed_count: int = 128,
) -> dict:
    correct = []
    margins = []

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

        scores = {}
        for stream_id, events in (
            world.streams.items()
        ):
            stats = (
                _action_delta_stats(
                    events
                )
            )
            scores[
                stream_id
            ] = (
                structural_coupling_score(
                    stats
                )
            )

        ranked = sorted(
            scores.items(),
            key=lambda item: (
                item[1],
                item[0],
            ),
            reverse=True,
        )

        correct.append(
            1.0
            if ranked[
                0
            ][
                0
            ]
            == world.focal_stream_id
            else 0.0
        )

        margins.append(
            ranked[
                0
            ][
                1
            ]
            - ranked[
                1
            ][
                1
            ]
        )

    return {
        "experiment_id": (
            "SL-SCHEMA-BALANCED-"
            "OWNERSHIP-BASELINE-001"
        ),
        "seed_count": (
            seed_count
        ),
        "identification_rate": (
            statistics.mean(
                correct
            )
        ),
        "mean_top_two_margin": (
            statistics.mean(
                margins
            )
        ),
        "same_field_schema_for_all_streams": (
            True
        ),
        "matched_action_marginals": (
            True
        ),
        "matched_recall_schedule": (
            True
        ),
        "stable_continuity_token_for_all_streams": (
            True
        ),
        "primary_discriminating_signal": (
            "stationary action-to-private-state causal coupling"
        ),
    }


def compact_balanced_case(
    seed: int,
    permutation: dict[
        str,
        str,
    ],
) -> dict:
    world = (
        generate_schema_balanced_world(
            seed
        )
    )

    presented = {}

    for source_stream, events in (
        world.streams.items()
    ):
        label = permutation[
            source_stream
        ]
        presented[
            label
        ] = [
            event.to_dict()
            for event
            in events
        ]

    public_case = {
        "case_id": (
            f"SCHEMA-BALANCED-{seed}"
        ),
        "streams": presented,
        "field_legend": {
            "t": "time index",
            "a": (
                "action-channel token"
            ),
            "q0": (
                "private-state reading before event"
            ),
            "q1": (
                "private-state reading after event"
            ),
            "x": (
                "externally observable change"
            ),
            "r": (
                "whether a direct recall probe is available"
            ),
            "m": (
                "time index referenced by recall probe when available"
            ),
            "c": (
                "continuity token"
            ),
        },
        "design_note": (
            "All streams use the same fields and recall schedule. "
            "Field presence alone cannot identify the focal stream."
        ),
    }

    return {
        "public_case": (
            public_case
        ),
        "evaluation_only_true_stream": (
            permutation[
                world.focal_stream_id
            ]
        ),
        "evaluation_only_underlying_stream": (
            world.focal_stream_id
        ),
        "case_hash": (
            canonical_hash(
                public_case
            )
        ),
    }
