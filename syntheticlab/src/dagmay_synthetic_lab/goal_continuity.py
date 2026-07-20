"""Longitudinal commitment and emergency interruption for bounded game goals."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable

from .bounded_game_goals import GoalCandidate, GoalDecision, select_bounded_goal


@dataclass(frozen=True)
class GoalContinuityState:
    committed_goal_id: str | None = None
    active_goal_id: str | None = None
    switch_count: int = 0
    interruption_count: int = 0


def choose_goal_with_continuity(
    candidates: Iterable[GoalCandidate],
    *,
    allowed_evidence_ids: Iterable[str],
    state: GoalContinuityState,
) -> tuple[GoalDecision, GoalContinuityState]:
    items = tuple(candidates)
    decision = select_bounded_goal(
        items,
        allowed_evidence_ids=allowed_evidence_ids,
        current_goal_id=state.committed_goal_id,
    )
    switched = int(state.active_goal_id is not None and decision.goal_id != state.active_goal_id)
    if decision.rationale_code == "CRITICAL_NEED":
        return decision, GoalContinuityState(
            committed_goal_id=state.committed_goal_id,
            active_goal_id=decision.goal_id,
            switch_count=state.switch_count + switched,
            interruption_count=state.interruption_count + int(decision.goal_id != state.committed_goal_id),
        )
    return decision, GoalContinuityState(
        committed_goal_id=decision.goal_id,
        active_goal_id=decision.goal_id,
        switch_count=state.switch_count + switched,
        interruption_count=state.interruption_count,
    )
