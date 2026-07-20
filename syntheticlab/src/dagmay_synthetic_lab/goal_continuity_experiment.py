from __future__ import annotations

import random
from statistics import mean

from .bounded_game_goals import GoalCandidate
from .goal_continuity import GoalContinuityState, choose_goal_with_continuity
from .persistent_character_boundary import assert_persistent_character_objective


def _ordinary_candidates(rng: random.Random) -> tuple[GoalCandidate, GoalCandidate]:
    return (
        GoalCandidate("G-BUILD", "BUILD", ("EV-BUILD",), True, 0.35, 0.70 + rng.uniform(-0.12, 0.12), 0.20, 0.05, "Wall-12"),
        GoalCandidate("G-CRAFT", "CRAFT", ("EV-CRAFT",), True, 0.35, 0.70 + rng.uniform(-0.12, 0.12), 0.10, 0.05, "Bench-4"),
    )


def _naive_score(candidate: GoalCandidate) -> float:
    return 4.0 * candidate.urgency + 2.0 * candidate.utility + 0.5 * candidate.personality_fit - candidate.risk


def _trial(seed: int, ticks: int = 200) -> dict[str, float]:
    rng = random.Random(seed)
    state = GoalContinuityState()
    naive_previous: str | None = None
    naive_switches = 0
    continuity_regret = 0.0
    emergency_hits = 0
    emergency_total = 0
    resumed_commitment = 0
    expected_resume: str | None = None

    for tick in range(ticks):
        ordinary = _ordinary_candidates(rng)
        emergency = tick in {50, 100, 150}
        candidates: tuple[GoalCandidate, ...] = ordinary
        evidence = ["EV-BUILD", "EV-CRAFT"]
        if emergency:
            candidates += (GoalCandidate("G-EAT", "EAT", ("EV-STARVING",), True, 0.95, 0.4, -1.0),)
            evidence.append("EV-STARVING")
            expected_resume = state.committed_goal_id

        decision, state = choose_goal_with_continuity(candidates, allowed_evidence_ids=evidence, state=state)
        if emergency:
            emergency_total += 1
            emergency_hits += int(decision.goal_id == "G-EAT")
        elif tick in {51, 101, 151}:
            resumed_commitment += int(decision.goal_id == expected_resume)

        naive_pool = [item for item in candidates if (not emergency or item.urgency >= 0.90)]
        naive = max(naive_pool, key=lambda item: (_naive_score(item), item.goal_id))
        if naive_previous is not None and naive.goal_id != naive_previous:
            naive_switches += 1
        naive_previous = naive.goal_id

        if not emergency:
            best_utility = max(item.utility for item in ordinary)
            chosen = next(item for item in ordinary if item.goal_id == decision.goal_id)
            continuity_regret += best_utility - chosen.utility

    return {
        "continuity_switches": float(state.switch_count),
        "naive_switches": float(naive_switches),
        "switch_reduction": 1.0 - state.switch_count / max(naive_switches, 1),
        "mean_utility_regret": continuity_regret / (ticks - emergency_total),
        "emergency_compliance": emergency_hits / emergency_total,
        "post_emergency_resume": resumed_commitment / emergency_total,
    }


def run(seeds: int = 256) -> dict[str, object]:
    assert_persistent_character_objective(
        purpose="Reduce game-goal thrashing while preserving emergency response and useful work.",
        independent_value_areas=["bounded_goals", "personality", "causal_coherence", "player_value", "reliability"],
        design="Compare commitment-aware selection with a memoryless bounded baseline across fluctuating work utilities and periodic critical needs.",
    )
    trials = [_trial(seed) for seed in range(seeds)]
    return {
        "experiment_id": "MOSAIC-GOAL-CONTINUITY-001",
        "classification": "PERSISTENT_CHARACTER_ENGINEERING",
        "provider_calls": 0,
        "canonical_mutation": False,
        "seeds": seeds,
        "ticks_per_seed": 200,
        "summary": {key: mean(trial[key] for trial in trials) for key in trials[0]},
    }


if __name__ == "__main__":
    import json
    print(json.dumps(run(), indent=2, sort_keys=True))
