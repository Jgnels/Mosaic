"""Longitudinal commitment and emergency interruption for bounded game goals."""

from __future__ import annotations

from dataclasses import dataclass
import re
from typing import Iterable

from .bounded_game_goals import GoalCandidate, GoalDecision, select_bounded_goal

_SAFE_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.:\-]{0,63}$")


@dataclass(frozen=True)
class GoalContinuityState:
    committed_goal_id: str | None = None
    active_goal_id: str | None = None
    switch_count: int = 0
    interruption_count: int = 0

    def __post_init__(self) -> None:
        for value in (self.committed_goal_id, self.active_goal_id):
            if value is not None and not _SAFE_ID.fullmatch(value):
                raise ValueError("unsafe goal continuity identifier")
        if self.switch_count < 0 or self.interruption_count < 0:
            raise ValueError("goal continuity counters must be non-negative")


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
