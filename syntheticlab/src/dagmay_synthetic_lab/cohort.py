from __future__ import annotations

from dataclasses import dataclass
from itertools import product
from typing import Dict, Iterable, List, Tuple
import statistics

from .causal_world import NovelCausalWorld, OPAQUE_CONTEXTS, OPAQUE_ACTIONS
from .core import DeterministicRng, stable_unit_float, canonical_hash
from .learners import ContextualCausalLearner
from .blinding import build_blind_map


@dataclass(frozen=True)
class CohortCondition:
    learning: str       # developmental | memory_query
    history: str        # chronological | shuffled
    reflection: str     # none | grounded
    knowledge: str      # lived_only | supplied_summary

    @property
    def condition_id(self) -> str:
        return (
            f"L={self.learning};H={self.history};"
            f"R={self.reflection};K={self.knowledge}"
        )


class MemoryQueryAgent:
    """Does not incrementally update a learned model.

    At probe time it estimates success probabilities directly from retained
    episodic records. This is intentionally a memory/retrieval control.
    """
    def __init__(self, contexts, actions):
        self.contexts = tuple(contexts)
        self.actions = tuple(actions)
        self.memories = []

    def observe(self, exp):
        self.memories.append(exp)

    def choose(self, context):
        scores = {}
        for action in self.actions:
            relevant = [
                e.outcome for e in self.memories
                if e.context == context and e.action == action
            ]
            scores[action] = (
                (sum(relevant) + 1) / (len(relevant) + 2)
                if relevant else 0.5
            )
        return max(sorted(self.actions), key=lambda a: scores[a])


class SuppliedSummaryAgent:
    """Positive/contamination control: receives a researcher-derived summary.

    It is not evidence of developmental learning.
    """
    def __init__(self, mapping):
        self.mapping = dict(mapping)

    def observe(self, exp):
        del exp

    def choose(self, context):
        return self.mapping[context]


def all_conditions() -> List[CohortCondition]:
    return [
        CohortCondition(*values)
        for values in product(
            ("developmental", "memory_query"),
            ("chronological", "shuffled"),
            ("none", "grounded"),
            ("lived_only", "supplied_summary"),
        )
    ]


def _run_condition(seed: int, condition: CohortCondition, episodes: int = 320) -> dict:
    world = NovelCausalWorld(seed=seed, noise=0.10)
    history = world.generate_exploration_history(
        episodes=episodes,
        policy_seed=seed ^ 0x5150AA,
    )

    if condition.history == "shuffled":
        history = list(history)
        rng = DeterministicRng(seed ^ 0x777777)
        rng.shuffle(history)

    if condition.knowledge == "supplied_summary":
        agent = SuppliedSummaryAgent(world.rules.optimal_action_by_context)
    elif condition.learning == "developmental":
        agent = ContextualCausalLearner(OPAQUE_CONTEXTS, OPAQUE_ACTIONS)
    else:
        agent = MemoryQueryAgent(OPAQUE_CONTEXTS, OPAQUE_ACTIONS)

    for exp in history:
        agent.observe(exp)

    # Grounded reflection in v0.8 is strictly read-only at probe time.
    # It cannot write back into learning state, preventing the reflection
    # confound from contaminating this cohort baseline.
    reflection_summary = None
    if condition.reflection == "grounded":
        reflection_summary = {
            context: agent.choose(context)
            for context in OPAQUE_CONTEXTS
        }

    accuracy = statistics.mean(
        1.0 if agent.choose(c) == world.rules.optimal_action_by_context[c] else 0.0
        for c in OPAQUE_CONTEXTS
    )
    return {
        "condition_id": condition.condition_id,
        "seed": seed,
        "accuracy": accuracy,
        "reflection_summary": reflection_summary,
        "world_rules_hash": canonical_hash(world.rules.to_dict()),
    }


def run_passive_factorial_cohort(seed_count: int = 64) -> dict:
    conditions = all_conditions()
    blind_map = build_blind_map(
        [c.condition_id for c in conditions],
        blinding_salt="SYNTHETICLAB-V0.8-FROZEN",
    )

    results = {}
    for condition in conditions:
        rows = [
            _run_condition(seed, condition)
            for seed in range(1, seed_count + 1)
        ]
        results[condition.condition_id] = {
            "blinded_id": blind_map[condition.condition_id],
            "mean_accuracy": statistics.mean(r["accuracy"] for r in rows),
            "rows": rows,
        }

    blind_export = {
        data["blinded_id"]: {
            "mean_accuracy": data["mean_accuracy"],
            "rows": [
                {"seed": r["seed"], "accuracy": r["accuracy"]}
                for r in data["rows"]
            ],
        }
        for condition_id, data in results.items()
    }

    return {
        "experiment_id": "SL-PASSIVE-COHORT-001",
        "status": "EXPLORATORY_INFRASTRUCTURE_BASELINE",
        "seed_count": seed_count,
        "conditions": results,
        "blind_export": blind_export,
        "blind_map_sealed_for_researcher": blind_map,
        "interpretation_warning": (
            "This is a passive experience-processing cohort. It intentionally does not "
            "test endogenous action selection or seed drives. Supplied-summary conditions "
            "are contamination/positive controls, not developmental conditions."
        ),
    }
