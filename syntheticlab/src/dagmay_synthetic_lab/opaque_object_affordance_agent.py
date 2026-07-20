from __future__ import annotations

from collections import defaultdict, Counter
from dataclasses import dataclass, asdict
import copy

from .core import canonical_hash, stable_unit_float
from .minigrid_adapter import ExternalObservation


@dataclass
class EffectStats:
    attempts: int = 0
    observation_changed: int = 0
    reward_events: int = 0

    def observe(
        self,
        changed: bool,
        reward: float,
    ):
        self.attempts += 1
        self.observation_changed += int(changed)
        self.reward_events += int(reward > 0)

    @property
    def change_rate(self) -> float:
        return (
            self.observation_changed + 1
        ) / (
            self.attempts + 2
        )

    @property
    def reward_rate(self) -> float:
        return (
            self.reward_events + 1
        ) / (
            self.attempts + 12
        )

    def to_dict(self):
        return asdict(self)


class OpaqueObjectAffordanceAgent:
    """Object-relative affordance discovery without semantic object labels.

    The learner treats each opaque cell encoding as an uninterpreted token.
    It learns:
    - token frequency / novelty;
    - which actions change observations when a token occupies a relative cell;
    - which token/action combinations have ever participated in rewarded paths;
    - successful abstract token/action traces.

    The agent is not told which token means key, door, wall, or goal.
    """

    def __init__(
        self,
        agent_id: str,
        seed: int,
        action_count: int = 7,
        exploration_rate: float = .20,
        max_success_trace: int = 64,
    ):
        self.agent_id = agent_id
        self.seed = seed
        self.action_count = action_count
        self.exploration_rate = exploration_rate
        self.max_success_trace = max_success_trace

        self.token_counts = Counter()
        self.effect_stats = defaultdict(EffectStats)
        self.reward_assoc = defaultdict(float)
        self.current_trace: list[
            tuple[
                tuple[str, ...],
                int,
            ]
        ] = []
        self.success_templates: list[
            tuple[
                tuple[
                    tuple[str, ...],
                    int,
                ],
                ...
            ]
        ] = []
        self.step_count = 0
        self.action_counts = Counter()

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self):
        return canonical_hash(
            self.to_dict()
        )

    @staticmethod
    def relative_tokens(
        obs: ExternalObservation,
    ) -> tuple[str, ...]:
        tokens = []
        for feature in obs.opaque_features:
            if feature.startswith("CELL:"):
                tokens.append(feature)
        return tuple(sorted(tokens))

    @staticmethod
    def token_identity(
        cell_feature: str,
    ) -> str:
        # CELL:row:col:opaque-encoded-triple
        parts = cell_feature.split(
            ":",
            3,
        )
        if len(parts) != 4:
            return cell_feature
        return parts[3]

    @staticmethod
    def relative_signature(
        obs: ExternalObservation,
    ) -> tuple[str, ...]:
        # Keep relative position + opaque identity, but never semantic labels.
        return tuple(
            sorted(
                feature
                for feature
                in obs.opaque_features
                if feature.startswith("CELL:")
            )
        )

    def _interesting_features(
        self,
        obs: ExternalObservation,
    ) -> tuple[str, ...]:
        cells = self.relative_tokens(obs)
        if not cells:
            return ()

        # Prefer rare opaque identities. This is an objectness heuristic only.
        scored = []
        for cell in cells:
            identity = self.token_identity(
                cell
            )
            novelty = 1.0 / (
                self.token_counts[
                    identity
                ] + 1.0
            )
            scored.append(
                (
                    novelty,
                    cell,
                )
            )

        scored.sort(
            reverse=True
        )
        return tuple(
            cell
            for _, cell
            in scored[:6]
        )

    def _action_score(
        self,
        obs: ExternalObservation,
        action: int,
    ) -> float:
        interesting = (
            self._interesting_features(
                obs
            )
        )
        if not interesting:
            return 0.0

        scores = []
        for feature in interesting:
            identity = self.token_identity(
                feature
            )
            stats = self.effect_stats[
                (
                    feature,
                    action,
                )
            ]
            reward = self.reward_assoc[
                (
                    identity,
                    action,
                )
            ]
            novelty = 1.0 / (
                stats.attempts + 1.0
            )
            scores.append(
                .42
                * stats.change_rate
                + .28
                * stats.reward_rate
                + .18
                * reward
                + .12
                * novelty
            )

        return (
            sum(scores)
            / len(scores)
        )

    def _template_action(
        self,
        obs: ExternalObservation,
    ) -> int | None:
        current_ids = {
            self.token_identity(
                feature
            )
            for feature
            in self._interesting_features(
                obs
            )
        }

        for template in reversed(
            self.success_templates[-16:]
        ):
            if not template:
                continue
            features, action = template[0]
            template_ids = {
                self.token_identity(
                    feature
                )
                for feature
                in features
            }
            overlap = len(
                current_ids
                & template_ids
            )
            if overlap >= 2:
                return action
        return None

    def choose_action(
        self,
        obs: ExternalObservation,
    ) -> int:
        template_action = (
            self._template_action(
                obs
            )
        )

        if template_action is not None:
            action = template_action
        else:
            explore = stable_unit_float(
                "object-affordance-explore",
                self.seed,
                self.step_count,
                obs.context_token(),
            ) < self.exploration_rate

            if explore:
                action = int(
                    stable_unit_float(
                        "object-affordance-action",
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
                    range(
                        self.action_count
                    ),
                    key=lambda a: (
                        self._action_score(
                            obs,
                            a,
                        )
                        + 1e-9
                        * stable_unit_float(
                            "object-affordance-tie",
                            self.seed,
                            self.step_count,
                            a,
                        )
                    ),
                )

        self.action_counts[
            action
        ] += 1
        return action

    def observe_transition(
        self,
        obs: ExternalObservation,
        action: int,
        reward: float,
        terminated: bool,
        truncated: bool,
        next_obs: ExternalObservation,
    ):
        before = self.relative_signature(
            obs
        )
        after = self.relative_signature(
            next_obs
        )
        changed = before != after

        interesting = (
            self._interesting_features(
                obs
            )
        )

        for feature in interesting:
            identity = self.token_identity(
                feature
            )
            self.token_counts[
                identity
            ] += 1

            self.effect_stats[
                (
                    feature,
                    int(action),
                )
            ].observe(
                changed,
                reward,
            )

            if reward > 0:
                self.reward_assoc[
                    (
                        identity,
                        int(action),
                    )
                ] += 1.0

        self.current_trace.append(
            (
                interesting,
                int(action),
            )
        )

        if (
            terminated
            and reward > 0
        ):
            trace = tuple(
                self.current_trace[
                    -self.max_success_trace:
                ]
            )
            if trace:
                self.success_templates.append(
                    trace
                )
            self.current_trace.clear()
        elif truncated:
            self.current_trace.clear()

        self.step_count += 1

    def to_dict(self):
        return {
            "agent_id": self.agent_id,
            "seed": self.seed,
            "action_count": (
                self.action_count
            ),
            "exploration_rate": (
                self.exploration_rate
            ),
            "max_success_trace": (
                self.max_success_trace
            ),
            "token_counts": dict(
                sorted(
                    self.token_counts.items()
                )
            ),
            "effect_stats": {
                f"{feature}|{action}": stats.to_dict()
                for (
                    feature,
                    action
                ), stats
                in sorted(
                    self.effect_stats.items()
                )
            },
            "reward_assoc": {
                f"{identity}|{action}": value
                for (
                    identity,
                    action
                ), value
                in sorted(
                    self.reward_assoc.items()
                )
            },
            "success_template_count": len(
                self.success_templates
            ),
            "step_count": (
                self.step_count
            ),
            "action_counts": {
                str(k): v
                for k, v
                in sorted(
                    self.action_counts.items()
                )
            },
        }
