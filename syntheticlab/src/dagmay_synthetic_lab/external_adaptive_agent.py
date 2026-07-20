from __future__ import annotations

from collections import defaultdict
import copy

from .adaptive_trace_agent import RecencyEstimate
from .core import canonical_hash, stable_unit_float
from .minigrid_adapter import ExternalObservation


class ExternalAdaptiveAgent:
    """Environment-neutral recency-weighted learner over opaque observation tokens.

    This deliberately does not parse MiniGrid mission language.
    """

    def __init__(
        self,
        agent_id: str,
        seed: int,
        action_count: int = 7,
        alpha: float = .22,
        exploration_rate: float = .18,
    ):
        self.agent_id = agent_id
        self.seed = seed
        self.action_count = action_count
        self.alpha = alpha
        self.exploration_rate = (
            exploration_rate
        )
        self.values = defaultdict(
            RecencyEstimate
        )
        self.action_counts = defaultdict(
            int
        )
        self.transition_count = 0

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self):
        return canonical_hash(
            self.to_dict()
        )

    def _key(
        self,
        obs: ExternalObservation,
        action_index: int,
    ):
        return (
            obs.context_token(),
            str(action_index),
        )

    def choose_action(
        self,
        obs: ExternalObservation,
    ) -> int:
        # Deterministic exploration schedule.
        explore = stable_unit_float(
            "external-explore",
            self.seed,
            self.transition_count,
            obs.context_token(),
        ) < self.exploration_rate

        if explore:
            action = int(
                stable_unit_float(
                    "external-action",
                    self.seed,
                    self.transition_count,
                )
                * self.action_count
            )
            action = min(
                action,
                self.action_count - 1,
            )
        else:
            action = max(
                range(self.action_count),
                key=lambda a: (
                    self.values[
                        self._key(obs, a)
                    ].value
                    + .16
                    * self.values[
                        self._key(obs, a)
                    ].uncertainty()
                    + 1e-9
                    * stable_unit_float(
                        "external-tie",
                        self.seed,
                        self.transition_count,
                        a,
                    )
                ),
            )

        self.action_counts[action] += 1
        return action

    def observe_transition(
        self,
        obs: ExternalObservation,
        action_index: int,
        reward: float,
        terminated: bool,
        truncated: bool,
    ) -> None:
        # Convert sparse external reward into bounded learning outcome.
        # Completion is strongly positive; neutral steps remain weakly negative
        # so shorter successful paths can eventually dominate.
        if terminated and reward > 0:
            outcome = 1.0
        elif truncated:
            outcome = 0.0
        else:
            outcome = max(
                0.0,
                min(
                    1.0,
                    .48 + .50 * float(reward),
                ),
            )

        estimate = self.values[
            self._key(
                obs,
                action_index,
            )
        ]

        # RecencyEstimate expects binary observations, so use a deterministic
        # Bernoulli realization from the bounded outcome. This preserves the
        # same order-sensitive update mechanism used by AdaptiveTrace.
        binary = int(
            stable_unit_float(
                "external-outcome",
                self.seed,
                self.transition_count,
                obs.context_token(),
                action_index,
            )
            < outcome
        )
        estimate.observe(
            binary,
            self.alpha,
        )
        self.transition_count += 1

    def to_dict(self):
        return {
            "agent_id": self.agent_id,
            "seed": self.seed,
            "action_count": (
                self.action_count
            ),
            "alpha": self.alpha,
            "exploration_rate": (
                self.exploration_rate
            ),
            "values": {
                "|".join(k): v.to_dict()
                for k, v in sorted(
                    self.values.items()
                )
            },
            "action_counts": {
                str(k): v
                for k, v in sorted(
                    self.action_counts.items()
                )
            },
            "transition_count": (
                self.transition_count
            ),
        }
