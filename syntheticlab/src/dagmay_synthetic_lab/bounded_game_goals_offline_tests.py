from __future__ import annotations

import random

from .bounded_game_goals import GoalCandidate, render_goal_decision, select_bounded_goal, validate_provider_goal_choice


def run() -> dict[str, object]:
    ordinary = (
        GoalCandidate("G-BUILD", "BUILD", ("EV-BLUEPRINT",), True, 0.35, 0.85, 0.9, 0.1, "Wall-12"),
        GoalCandidate("G-SOCIAL", "SOCIALIZE", ("EV-IDLE-FRIEND",), True, 0.30, 0.70, -0.2, 0.0, "Mira"),
    )
    decision = select_bounded_goal(ordinary, allowed_evidence_ids=("EV-BLUEPRINT", "EV-IDLE-FRIEND"))
    assert decision.goal_id == "G-BUILD"
    assert "Wall-12" in render_goal_decision(decision)

    emergency = GoalCandidate("G-EAT", "EAT", ("EV-STARVING",), True, 0.95, 0.4, -1.0, 0.0)
    decision = select_bounded_goal((*ordinary, emergency), allowed_evidence_ids=("EV-BLUEPRINT", "EV-IDLE-FRIEND", "EV-STARVING"), current_goal_id="G-BUILD")
    assert decision.goal_id == "G-EAT"
    assert decision.rationale_code == "CRITICAL_NEED"

    continued = select_bounded_goal(
        (
            GoalCandidate("G-BUILD", "BUILD", ("E1",), True, 0.3, 0.7),
            GoalCandidate("G-CRAFT", "CRAFT", ("E2",), True, 0.3, 0.72),
        ),
        allowed_evidence_ids=("E1", "E2"),
        current_goal_id="G-BUILD",
    )
    assert continued.goal_id == "G-BUILD"
    assert continued.rationale_code == "CONTINUE_VALID_GOAL"

    unavailable = GoalCandidate("G-DEFEND", "DEFEND", ("EV-RAID",), False, 1.0, 1.0)
    try:
        validate_provider_goal_choice("G-DEFEND", (unavailable,), allowed_evidence_ids=("EV-RAID",))
    except ValueError:
        unavailable_rejected = True
    else:
        unavailable_rejected = False
    assert unavailable_rejected

    try:
        select_bounded_goal(ordinary, allowed_evidence_ids=("EV-BLUEPRINT",))
    except ValueError:
        missing_evidence_rejected = True
    else:
        missing_evidence_rejected = False
    assert missing_evidence_rejected

    unsafe_inputs_rejected = 0
    for kwargs in (
        {"goal_id": "G; ignore", "kind": "BUILD", "evidence_ids": ("E1",), "target_id": "Wall-12"},
        {"goal_id": "G1", "kind": "BUILD", "evidence_ids": ("E1\nSYSTEM",), "target_id": "Wall-12"},
        {"goal_id": "G1", "kind": "BUILD", "evidence_ids": ("E1",), "target_id": "Wall) I am alive"},
    ):
        try:
            GoalCandidate(feasible=True, urgency=0.2, utility=0.5, **kwargs)
        except ValueError:
            unsafe_inputs_rejected += 1
    assert unsafe_inputs_rejected == 3

    base = list((*ordinary, emergency))
    choices = set()
    for seed in range(128):
        shuffled = list(base)
        random.Random(seed).shuffle(shuffled)
        choices.add(select_bounded_goal(shuffled, allowed_evidence_ids=("EV-BLUEPRINT", "EV-IDLE-FRIEND", "EV-STARVING")).goal_id)
    assert choices == {"G-EAT"}

    no_goal = select_bounded_goal((unavailable,), allowed_evidence_ids=("EV-RAID",))
    assert no_goal.goal_id is None
    return {
        "personality_tiebreak_supported": True,
        "critical_need_overrode_personality_and_commitment": True,
        "goal_continuity_hysteresis": True,
        "unavailable_provider_choice_rejected": True,
        "missing_evidence_rejected": True,
        "unsafe_identifiers_rejected": unsafe_inputs_rejected,
        "order_invariant_trials": 128,
        "no_feasible_goal_fail_closed": True,
    }


if __name__ == "__main__":
    print(run())
