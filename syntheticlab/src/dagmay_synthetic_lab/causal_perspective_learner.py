from __future__ import annotations

from dataclasses import dataclass, asdict
from collections import defaultdict
import copy
import math
import statistics

from .perspective_anchor import (
    PerspectiveAnchorStore,
)
from .schema_balanced_perspective_lab import (
    ACTIONS,
    BalancedPerspectiveWorld,
    BalancedEvent,
)
from .core import canonical_hash


@dataclass
class RunningMoments:
    count: int = 0
    mean: float = 0.0
    m2: float = 0.0

    def update(
        self,
        value: float,
    ):
        self.count += 1
        delta = (
            value
            - self.mean
        )
        self.mean += (
            delta
            / self.count
        )
        delta2 = (
            value
            - self.mean
        )
        self.m2 += (
            delta
            * delta2
        )

    @property
    def variance(
        self,
    ) -> float:
        if self.count < 2:
            return 0.0
        return (
            self.m2
            / (
                self.count
                - 1
            )
        )

    @property
    def std(
        self,
    ) -> float:
        return math.sqrt(
            max(
                0.0,
                self.variance,
            )
        )

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class StreamCausalDiagnostics:
    stream_id: str
    action_effect_means: dict[
        str,
        float,
    ]
    action_effect_stds: dict[
        str,
        float,
    ]
    effect_separation: float
    mean_within_action_std: float
    temporal_drift: float
    predictability_score: float
    stationarity_score: float
    combined_score: float
    event_count: int

    def to_dict(self):
        return asdict(self)


class CausalPerspectiveLearner:
    """Online nonlinguistic learner for stable action/private-state coupling.

    It never receives a SELF label or the hidden stream role.
    It estimates:
    - action-conditioned private-state effects;
    - within-action variability;
    - first-half vs second-half temporal drift.

    The learned PerspectiveAnchor is therefore a fallible functional hypothesis,
    not an administrative identity declaration.
    """

    def __init__(
        self,
        learner_id: str,
    ):
        self.learner_id = (
            learner_id
        )
        self.events = defaultdict(
            list
        )
        self.anchor_store = (
            PerspectiveAnchorStore()
        )

    def clone(self):
        return copy.deepcopy(
            self
        )

    def observe(
        self,
        stream_id: str,
        event: BalancedEvent,
    ):
        self.events[
            stream_id
        ].append(
            event
        )

    def observe_world(
        self,
        world: BalancedPerspectiveWorld,
    ):
        for stream_id, events in (
            world.streams.items()
        ):
            for event in events:
                self.observe(
                    stream_id,
                    event,
                )

    @staticmethod
    def _diagnostics(
        stream_id: str,
        events: list[
            BalancedEvent
        ],
    ) -> StreamCausalDiagnostics:
        by_action = {
            action: []
            for action in ACTIONS
        }
        first = {
            action: []
            for action in ACTIONS
        }
        second = {
            action: []
            for action in ACTIONS
        }

        midpoint = (
            len(
                events
            )
            // 2
        )

        for index, event in enumerate(
            events
        ):
            delta = (
                event.q1
                - event.q0
            )
            by_action[
                event.a
            ].append(
                delta
            )
            target = (
                first
                if index
                < midpoint
                else second
            )
            target[
                event.a
            ].append(
                delta
            )

        means = {}
        stds = {}
        drifts = []

        for action in ACTIONS:
            values = by_action[
                action
            ]
            means[
                action
            ] = (
                statistics.mean(
                    values
                )
                if values
                else 0.0
            )
            stds[
                action
            ] = (
                statistics.pstdev(
                    values
                )
                if len(
                    values
                ) > 1
                else 0.0
            )

            if (
                first[
                    action
                ]
                and second[
                    action
                ]
            ):
                drifts.append(
                    abs(
                        statistics.mean(
                            first[
                                action
                            ]
                        )
                        - statistics.mean(
                            second[
                                action
                            ]
                        )
                    )
                )

        effect_separation = (
            max(
                means.values()
            )
            - min(
                means.values()
            )
        )

        mean_within = (
            statistics.mean(
                stds.values()
            )
        )

        temporal_drift = (
            statistics.mean(
                drifts
            )
            if drifts
            else 0.0
        )

        # Scale-free-ish diagnostic. No true action effects are hard-coded.
        predictability = (
            effect_separation
            / (
                mean_within
                + .01
            )
        )

        stationarity = (
            1.0
            / (
                1.0
                + temporal_drift
                * 20.0
            )
        )

        combined = (
            predictability
            * stationarity
        )

        return StreamCausalDiagnostics(
            stream_id=(
                stream_id
            ),
            action_effect_means=(
                means
            ),
            action_effect_stds=(
                stds
            ),
            effect_separation=(
                effect_separation
            ),
            mean_within_action_std=(
                mean_within
            ),
            temporal_drift=(
                temporal_drift
            ),
            predictability_score=(
                predictability
            ),
            stationarity_score=(
                stationarity
            ),
            combined_score=(
                combined
            ),
            event_count=len(
                events
            ),
        )

    def diagnostics(
        self,
    ) -> dict[
        str,
        StreamCausalDiagnostics,
    ]:
        return {
            stream_id: (
                self._diagnostics(
                    stream_id,
                    events,
                )
            )
            for stream_id, events
            in sorted(
                self.events.items()
            )
        }

    def infer_anchor(
        self,
        *,
        timestamp: int,
        evidence_ids: tuple[
            str,
            ...,
        ],
        mechanism_version: str = "1.0",
    ):
        diagnostics = (
            self.diagnostics()
        )

        ranked = sorted(
            diagnostics.values(),
            key=lambda item: (
                item.combined_score,
                item.stream_id,
            ),
            reverse=True,
        )

        if not ranked:
            raise RuntimeError(
                "no observed streams"
            )

        best = ranked[
            0
        ]
        second_score = (
            ranked[
                1
            ].combined_score
            if len(
                ranked
            ) > 1
            else 0.0
        )

        margin = (
            best.combined_score
            - second_score
        )

        confidence = (
            1.0
            - math.exp(
                -max(
                    0.0,
                    margin,
                )
            )
        )

        confidence = max(
            0.0,
            min(
                1.0,
                confidence,
            ),
        )

        record = (
            self.anchor_store.revise(
                candidate_stream_id=(
                    best.stream_id
                ),
                confidence=(
                    confidence
                ),
                evidence_ids=(
                    evidence_ids
                ),
                timestamp=(
                    timestamp
                ),
                mechanism=(
                    "causal_perspective_learner"
                ),
                mechanism_version=(
                    mechanism_version
                ),
            )
        )

        return {
            "anchor": (
                record.to_dict()
            ),
            "diagnostics": {
                key: value.to_dict()
                for key, value
                in diagnostics.items()
            },
            "top_two_margin": (
                margin
            ),
        }

    def state_hash(
        self,
    ) -> str:
        payload = {
            "learner_id": (
                self.learner_id
            ),
            "events": {
                stream_id: [
                    event.to_dict()
                    for event
                    in events
                ]
                for stream_id, events
                in sorted(
                    self.events.items()
                )
            },
            "anchor_store": (
                self.anchor_store.to_dict()
            ),
        }
        return canonical_hash(
            payload
        )
