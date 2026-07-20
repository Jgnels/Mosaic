from __future__ import annotations

from .bounded_game_goals import GoalCandidate
from .goal_freshness import TimedGoalCandidate, filter_fresh_candidates
from .goal_freshness_experiment import run


def test() -> dict[str, float]:
    result = run(128)
    summary = result["summary"]
    assert summary["regime_switch_latency"] <= 1.0
    assert summary["stale_emergency_rejected"] == 1.0
    assert summary["fresh_emergency_selected"] == 1.0
    assert summary["post_emergency_resume"] == 1.0

    goal = GoalCandidate("G1", "REST", ("E1",), True, 0.5, 0.5)
    candidates = (
        TimedGoalCandidate(goal, 12, 13, 2),
        TimedGoalCandidate(GoalCandidate("G2", "EAT", ("E2",), True, 1.0, 1.0), 10, 20, 1),
    )
    fresh, audit = filter_fresh_candidates(candidates, current_tick=11, current_world_revision=2)
    assert not fresh
    assert audit.future_rejected == 1
    assert audit.revision_rejected == 1
    return summary


if __name__ == "__main__":
    print(test())
