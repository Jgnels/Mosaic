from __future__ import annotations

from collections import defaultdict, deque
import copy

from .core import canonical_hash, stable_unit_float
from .minigrid_adapter import ExternalObservation


class ExternalFeatureTraceAgent:
    """Opaque feature-based learner with short eligibility traces.

    The agent is never given MiniGrid semantic labels. It receives only opaque
    positional feature tokens derived from the subject-visible partial image.

    Shared feature/action weights allow reuse across layouts. A bounded recent
    trace allows sparse terminal reward to update preceding action choices.
    """

    def __init__(
        self,
        agent_id: str,
        seed: int,
        action_count: int = 7,
        learning_rate: float = .12,
        exploration_rate: float = .22,
        trace_length: int = 28,
        trace_discount: float = .92,
    ):
        self.agent_id = agent_id
        self.seed = seed
        self.action_count = action_count
        self.learning_rate = learning_rate
        self.exploration_rate = exploration_rate
        self.trace_length = trace_length
        self.trace_discount = trace_discount

        self.weights = defaultdict(float)
        self.counts = defaultdict(int)
        self.trace = deque(
            maxlen=trace_length
        )
        self.step_count = 0
        self.action_counts = defaultdict(int)

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self):
        return canonical_hash(self.to_dict())

    def _score(
        self,
        obs: ExternalObservation,
        action: int,
    ) -> float:
        features = obs.opaque_features
        if not features:
            return 0.0

        values = [
            self.weights[(f, action)]
            for f in features
        ]
        mean_value = (
            sum(values) / len(values)
        )

        novelty = (
            sum(
                1.0
                / (
                    self.counts[(f, action)]
                    + 1.0
                )
                for f in features
            )
            / len(features)
        )

        return (
            mean_value
            + .08 * novelty
            + 1e-9
            * stable_unit_float(
                "feature-trace-tie",
                self.seed,
                self.step_count,
                action,
            )
        )

    def choose_action(
        self,
        obs: ExternalObservation,
    ) -> int:
        explore = stable_unit_float(
            "feature-trace-explore",
            self.seed,
            self.step_count,
            obs.context_token(),
        ) < self.exploration_rate

        if explore:
            action = int(
                stable_unit_float(
                    "feature-trace-action",
                    self.seed,
                    self.step_count,
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
                key=lambda a: self._score(
                    obs,
                    a,
                ),
            )

        self.action_counts[action] += 1
        return action

    def observe_transition(
        self,
        obs: ExternalObservation,
        action: int,
        reward: float,
        terminated: bool,
        truncated: bool,
    ) -> None:
        features = tuple(
            obs.opaque_features
        )
        self.trace.append(
            (
                features,
                int(action),
            )
        )

        for feature in features:
            self.counts[
                (feature, int(action))
            ] += 1

        # Small immediate step pressure encourages shorter paths without
        # exposing task semantics.
        immediate_target = (
            float(reward) - .005
        )
        self._update_features(
            features,
            int(action),
            immediate_target,
            scale=.25,
        )

        if terminated and reward > 0:
            self._credit_recent_trace(
                terminal_signal=1.0
            )
            self.trace.clear()
        elif truncated:
            self._credit_recent_trace(
                terminal_signal=-.10
            )
            self.trace.clear()

        self.step_count += 1

    def _update_features(
        self,
        features,
        action,
        target,
        scale=1.0,
    ):
        if not features:
            return
        prediction = (
            sum(
                self.weights[
                    (f, action)
                ]
                for f in features
            )
            / len(features)
        )
        error = target - prediction
        delta = (
            self.learning_rate
            * scale
            * error
        )
        for feature in features:
            self.weights[
                (feature, action)
            ] += delta

    def _credit_recent_trace(
        self,
        terminal_signal: float,
    ):
        reversed_trace = list(
            reversed(self.trace)
        )
        for distance, (
            features,
            action,
        ) in enumerate(
            reversed_trace
        ):
            discounted = (
                terminal_signal
                * (
                    self.trace_discount
                    ** distance
                )
            )
            self._update_features(
                features,
                action,
                discounted,
                scale=1.0,
            )

    def to_dict(self):
        return {
            "agent_id": self.agent_id,
            "seed": self.seed,
            "action_count": self.action_count,
            "learning_rate": (
                self.learning_rate
            ),
            "exploration_rate": (
                self.exploration_rate
            ),
            "trace_length": (
                self.trace_length
            ),
            "trace_discount": (
                self.trace_discount
            ),
            "weights": {
                f"{feature}|{action}": value
                for (
                    feature,
                    action
                ), value in sorted(
                    self.weights.items()
                )
            },
            "counts": {
                f"{feature}|{action}": value
                for (
                    feature,
                    action
                ), value in sorted(
                    self.counts.items()
                )
            },
            "trace": [
                {
                    "features": list(features),
                    "action": action,
                }
                for features, action
                in self.trace
            ],
            "step_count": self.step_count,
            "action_counts": {
                str(k): v
                for k, v in sorted(
                    self.action_counts.items()
                )
            },
        }
