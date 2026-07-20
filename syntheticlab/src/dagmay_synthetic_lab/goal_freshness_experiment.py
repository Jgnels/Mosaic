from __future__ import annotations

from statistics import mean

from .bounded_game_goals import GoalCandidate
from .goal_continuity import GoalContinuityState, choose_goal_with_continuity
from .goal_freshness import TimedGoalCandidate, filter_fresh_candidates
from .persistent_character_boundary import assert_persistent_character_objective


def _timed(candidate: GoalCandidate, tick: int, revision: int, *, observed: int | None = None, expires: int | None = None) -> TimedGoalCandidate:
    return TimedGoalCandidate(candidate, tick if observed is None else observed, tick + 1 if expires is None else expires, revision)


def _trial() -> dict[str, float]:
    state = GoalContinuityState()
    switch_tick: int | None = None
    stale_emergency_rejected = 0
    fresh_emergency_selected = 0
    resumed_after_emergency = 0

    for tick in range(100):
        revision = 0 if tick < 50 else 1
        before_shift = tick < 50
        build = GoalCandidate("G-BUILD", "BUILD", ("EV-BUILD",), True, 0.35, 0.85 if before_shift else 0.20, 0.2, 0.05, "Wall-12")
        craft = GoalCandidate("G-CRAFT", "CRAFT", ("EV-CRAFT",), True, 0.35, 0.45 if before_shift else 0.95, 0.1, 0.05, "Bench-4")
        timed = [_timed(build, tick, revision), _timed(craft, tick, revision)]
        evidence = ["EV-BUILD", "EV-CRAFT"]

        if tick == 40:
            stale = GoalCandidate("G-EAT-STALE", "EAT", ("EV-STARVING-OLD",), True, 1.0, 1.0)
            timed.append(_timed(stale, tick, revision, observed=30, expires=35))
            evidence.append("EV-STARVING-OLD")
        if tick == 70:
            emergency = GoalCandidate("G-EAT", "EAT", ("EV-STARVING",), True, 0.95, 0.5)
            timed.append(_timed(emergency, tick, revision))
            evidence.append("EV-STARVING")

        fresh, audit = filter_fresh_candidates(timed, current_tick=tick, current_world_revision=revision)
        decision, state = choose_goal_with_continuity(fresh, allowed_evidence_ids=evidence, state=state)
        if tick == 40:
            stale_emergency_rejected = int(audit.stale_rejected == 1 and decision.goal_id != "G-EAT-STALE")
        if tick == 70:
            fresh_emergency_selected = int(decision.goal_id == "G-EAT")
        if tick == 71:
            resumed_after_emergency = int(decision.goal_id == "G-CRAFT")
        if tick >= 50 and decision.goal_id == "G-CRAFT" and switch_tick is None:
            switch_tick = tick

    return {
        "regime_switch_latency": float((switch_tick or 100) - 50),
        "stale_emergency_rejected": float(stale_emergency_rejected),
        "fresh_emergency_selected": float(fresh_emergency_selected),
        "post_emergency_resume": float(resumed_after_emergency),
    }


def run(replicates: int = 128) -> dict[str, object]:
    assert_persistent_character_objective(
        purpose="Reject stale game goals and adapt bounded commitments after world regime changes.",
        independent_value_areas=["bounded_goals", "causal_coherence", "failure_recovery", "player_value", "reliability"],
        design="Exercise freshness/revision gates, a permanent work-utility shift, and stale versus fresh emergency signals.",
    )
    trials = [_trial() for _ in range(replicates)]
    return {
        "experiment_id": "MOSAIC-GOAL-FRESHNESS-001",
        "classification": "PERSISTENT_CHARACTER_ENGINEERING",
        "provider_calls": 0,
        "canonical_mutation": False,
        "replicates": replicates,
        "summary": {key: mean(item[key] for item in trials) for key in trials[0]},
    }


if __name__ == "__main__":
    import json
    print(json.dumps(run(), indent=2, sort_keys=True))
