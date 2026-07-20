from __future__ import annotations
from dataclasses import dataclass, asdict
from collections import defaultdict, deque
import copy

from .core import canonical_hash, stable_unit_float
from .minigrid_adapter import ExternalObservation

@dataclass
class AffordanceEvidence:
    attempts: int = 0
    changed_observation: int = 0
    positive_reward: int = 0
    terminated_success: int = 0
    def observe(self, changed: bool, reward: float, terminated: bool):
        self.attempts += 1
        self.changed_observation += int(changed)
        self.positive_reward += int(reward > 0)
        self.terminated_success += int(terminated and reward > 0)
    @property
    def transition_probability(self) -> float:
        return (self.changed_observation + 1) / (self.attempts + 2)
    @property
    def terminal_value(self) -> float:
        return (self.terminated_success + 1) / (self.attempts + 8)
    def to_dict(self): return asdict(self)

class OpaqueStructuralAgent:
    """Learns an opaque state-transition graph and bounded procedural traces.

    No MiniGrid semantic object names are exposed. The model learns only from:
    - subject-visible opaque observation features;
    - chosen action;
    - whether the observation changed;
    - reward/termination consequences.
    """

    def __init__(
        self,
        agent_id: str,
        seed: int,
        action_count: int = 7,
        exploration_rate: float = .18,
        max_skill_length: int = 48,
    ):
        self.agent_id = agent_id
        self.seed = seed
        self.action_count = action_count
        self.exploration_rate = exploration_rate
        self.max_skill_length = max_skill_length

        self.affordances = defaultdict(AffordanceEvidence)
        self.transitions = defaultdict(lambda: defaultdict(int))
        self.reverse_transitions = defaultdict(set)
        self.reward_states = defaultdict(float)
        self.successful_skills: list[tuple[int, ...]] = []
        self.current_episode: list[tuple[str, int]] = []
        self.action_counts = defaultdict(int)
        self.step_count = 0
        self.last_state: str | None = None

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self):
        return canonical_hash(self.to_dict())

    @staticmethod
    def state_token(obs: ExternalObservation) -> str:
        return canonical_hash({
            "direction": obs.direction,
            "features": obs.opaque_features,
            "mission": obs.mission_token,
        })

    def _affordance_score(self, state: str, action: int) -> float:
        evidence = self.affordances[(state, action)]
        novelty = 1.0 / (evidence.attempts + 1.0)
        return (
            .55 * evidence.transition_probability
            + .30 * evidence.terminal_value
            + .15 * novelty
            + 1e-9 * stable_unit_float(
                "opaque-structural-tie",
                self.seed,
                self.step_count,
                state,
                action,
            )
        )

    def _known_reward_path_action(self, state: str) -> int | None:
        reward_targets = [
            s for s, value in self.reward_states.items()
            if value > 0
        ]
        if not reward_targets:
            return None

        queue = deque(reward_targets)
        seen = set(reward_targets)
        parent = {}

        while queue:
            current = queue.popleft()
            if current == state:
                break
            for predecessor in sorted(
                self.reverse_transitions.get(current, set())
            ):
                if predecessor in seen:
                    continue
                seen.add(predecessor)
                parent[predecessor] = current
                queue.append(predecessor)

        if state not in seen:
            return None

        next_state = parent.get(state)
        if next_state is None:
            return None

        candidates = []
        for action in range(self.action_count):
            count = self.transitions[(state, action)].get(next_state, 0)
            if count > 0:
                candidates.append((count, action))
        return max(candidates)[1] if candidates else None

    def _skill_action(self, state: str) -> int | None:
        for skill in reversed(self.successful_skills[-12:]):
            if not skill:
                continue
            action = skill[0]
            evidence = self.affordances[(state, action)]
            if (
                evidence.attempts >= 2
                and evidence.transition_probability >= .55
            ):
                return action
        return None

    def choose_action(self, obs: ExternalObservation) -> int:
        state = self.state_token(obs)

        path_action = self._known_reward_path_action(state)
        if path_action is not None:
            action = path_action
        else:
            skill_action = self._skill_action(state)
            if skill_action is not None:
                action = skill_action
            else:
                explore = stable_unit_float(
                    "opaque-structural-explore",
                    self.seed,
                    self.step_count,
                    state,
                ) < self.exploration_rate

                if explore:
                    action = int(
                        stable_unit_float(
                            "opaque-structural-action",
                            self.seed,
                            self.step_count,
                        ) * self.action_count
                    )
                    action = min(action, self.action_count - 1)
                else:
                    action = max(
                        range(self.action_count),
                        key=lambda a: self._affordance_score(state, a),
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
        next_obs: ExternalObservation,
    ):
        state = self.state_token(obs)
        next_state = self.state_token(next_obs)
        changed = state != next_state

        self.affordances[(state, int(action))].observe(
            changed=changed,
            reward=reward,
            terminated=terminated,
        )
        self.transitions[(state, int(action))][next_state] += 1
        self.reverse_transitions[next_state].add(state)

        if reward > 0:
            self.reward_states[next_state] += float(reward)

        self.current_episode.append((state, int(action)))

        if terminated and reward > 0:
            skill = tuple(
                action_index
                for _, action_index
                in self.current_episode[-self.max_skill_length:]
            )
            if skill:
                self.successful_skills.append(skill)
            self.current_episode.clear()
        elif truncated:
            self.current_episode.clear()

        self.step_count += 1
        self.last_state = next_state

    def to_dict(self):
        return {
            "agent_id": self.agent_id,
            "seed": self.seed,
            "action_count": self.action_count,
            "exploration_rate": self.exploration_rate,
            "max_skill_length": self.max_skill_length,
            "affordances": {
                f"{state}|{action}": value.to_dict()
                for (state, action), value in sorted(self.affordances.items())
            },
            "transitions": {
                f"{state}|{action}": dict(sorted(nexts.items()))
                for (state, action), nexts in sorted(self.transitions.items())
            },
            "reward_states": dict(sorted(self.reward_states.items())),
            "successful_skills": [list(skill) for skill in self.successful_skills],
            "current_episode": [
                [state, action] for state, action in self.current_episode
            ],
            "action_counts": {
                str(k): v for k, v in sorted(self.action_counts.items())
            },
            "step_count": self.step_count,
            "last_state": self.last_state,
        }
