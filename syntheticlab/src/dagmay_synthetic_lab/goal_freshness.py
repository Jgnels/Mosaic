"""Freshness and world-revision gating for bounded game goal candidates."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable

from .bounded_game_goals import GoalCandidate


@dataclass(frozen=True)
class TimedGoalCandidate:
    candidate: GoalCandidate
    observed_tick: int
    valid_until_tick: int
    world_revision: int

    def __post_init__(self) -> None:
        if self.observed_tick < 0 or self.valid_until_tick < self.observed_tick:
            raise ValueError("invalid goal observation interval")
        if self.world_revision < 0:
            raise ValueError("invalid world revision")


@dataclass(frozen=True)
class FreshnessAudit:
    accepted: int
    stale_rejected: int
    future_rejected: int
    revision_rejected: int


def filter_fresh_candidates(
    candidates: Iterable[TimedGoalCandidate],
    *,
    current_tick: int,
    current_world_revision: int,
) -> tuple[tuple[GoalCandidate, ...], FreshnessAudit]:
    if current_tick < 0 or current_world_revision < 0:
        raise ValueError("invalid current game state marker")
    accepted: list[GoalCandidate] = []
    stale = future = revision = 0
    for item in candidates:
        if item.world_revision != current_world_revision:
            revision += 1
        elif item.observed_tick > current_tick:
            future += 1
        elif item.valid_until_tick < current_tick:
            stale += 1
        else:
            accepted.append(item.candidate)
    return tuple(accepted), FreshnessAudit(len(accepted), stale, future, revision)
