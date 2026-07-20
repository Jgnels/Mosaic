from __future__ import annotations

from dataclasses import dataclass
from typing import Dict, List

from .core import DeterministicRng, Experience, stable_unit_float


OPAQUE_CONTEXTS = ("VOR", "KEP", "NUL", "SAV")
OPAQUE_ACTIONS = ("TAL", "PIM", "ZEF", "RUD")


@dataclass(frozen=True)
class HiddenRuleSet:
    seed: int
    optimal_action_by_context: Dict[str, str]

    def to_dict(self) -> dict:
        return {
            "seed": self.seed,
            "optimal_action_by_context": dict(sorted(self.optimal_action_by_context.items())),
        }


class NovelCausalWorld:
    """A world whose useful causal rules are represented only by opaque tokens.

    The learner is never told that an action is "rescue", "food", "friend",
    or any other semantically loaded concept. It must infer which action works
    in which context from its own ordered experience.
    """

    def __init__(self, seed: int, noise: float = 0.08):
        if not 0.0 <= noise < 0.5:
            raise ValueError("noise must be in [0, 0.5)")
        self.seed = seed
        self.noise = noise

        rng = DeterministicRng(seed)
        actions = list(OPAQUE_ACTIONS)
        mapping: Dict[str, str] = {}
        # Each context gets one optimal action. The action list is reshuffled
        # so the mapping is not lexically predictable.
        for context in OPAQUE_CONTEXTS:
            rng.shuffle(actions)
            mapping[context] = actions[0]
        self.rules = HiddenRuleSet(seed=seed, optimal_action_by_context=mapping)

    def outcome(self, context: str, action: str, event_index: int) -> int:
        is_optimal = action == self.rules.optimal_action_by_context[context]
        p_success = (1.0 - self.noise) if is_optimal else self.noise
        draw = stable_unit_float("causal", self.seed, context, action, event_index)
        return 1 if draw < p_success else 0

    def generate_exploration_history(self, episodes: int, policy_seed: int) -> List[Experience]:
        rng = DeterministicRng(policy_seed)
        history: List[Experience] = []
        for i in range(episodes):
            context = OPAQUE_CONTEXTS[rng.randrange(len(OPAQUE_CONTEXTS))]
            action = OPAQUE_ACTIONS[rng.randrange(len(OPAQUE_ACTIONS))]
            outcome = self.outcome(context, action, i)
            history.append(
                Experience(
                    event_id=f"NC-{self.seed:08X}-{i:06d}",
                    timestep=i,
                    context=context,
                    action=action,
                    outcome=outcome,
                )
            )
        return history
