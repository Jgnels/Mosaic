"""Bounded, evidence-grounded game goal selection for Mosaic characters."""

from __future__ import annotations

from dataclasses import dataclass
import re
from typing import Iterable


ALLOWED_GOAL_KINDS = frozenset({
    "EAT", "REST", "SEEK_TREATMENT", "TEND_OTHER", "DEFEND", "FLEE",
    "HAUL", "BUILD", "CRAFT", "CLEAN", "RESEARCH", "RECREATE", "SOCIALIZE",
})
CRITICAL_URGENCY = 0.90
_SAFE_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.:\-]{0,63}$")


@dataclass(frozen=True)
class GoalCandidate:
    goal_id: str
    kind: str
    evidence_ids: tuple[str, ...]
    feasible: bool
    urgency: float
    utility: float
    personality_fit: float = 0.0
    risk: float = 0.0
    target_id: str | None = None

    def __post_init__(self) -> None:
        if not _SAFE_ID.fullmatch(self.goal_id) or self.kind not in ALLOWED_GOAL_KINDS:
            raise ValueError("invalid bounded goal identity or kind")
        if not self.evidence_ids or len(self.evidence_ids) != len(set(self.evidence_ids)):
            raise ValueError("goals require unique observed evidence")
        if any(not _SAFE_ID.fullmatch(item) for item in self.evidence_ids):
            raise ValueError("unsafe evidence identifier")
        if self.target_id is not None and not _SAFE_ID.fullmatch(self.target_id):
            raise ValueError("unsafe goal target identifier")
        for name, value in (("urgency", self.urgency), ("utility", self.utility), ("risk", self.risk)):
            if not 0.0 <= value <= 1.0:
                raise ValueError(f"{name} must be between zero and one")
        if not -1.0 <= self.personality_fit <= 1.0:
            raise ValueError("personality fit must be between negative and positive one")


@dataclass(frozen=True)
class GoalDecision:
    goal_id: str | None
    kind: str | None
    target_id: str | None
    cited_evidence_ids: tuple[str, ...]
    rationale_code: str
    score: float | None


def _score(candidate: GoalCandidate, current_goal_id: str | None) -> float:
    commitment = 0.75 if candidate.goal_id == current_goal_id else 0.0
    return (
        4.0 * candidate.urgency
        + 2.0 * candidate.utility
        + 0.5 * candidate.personality_fit
        - candidate.risk
        + commitment
    )


def select_bounded_goal(
    candidates: Iterable[GoalCandidate],
    *,
    allowed_evidence_ids: Iterable[str],
    current_goal_id: str | None = None,
) -> GoalDecision:
    items = tuple(candidates)
    if len({item.goal_id for item in items}) != len(items):
        raise ValueError("duplicate goal candidate identifier")
    allowed = frozenset(allowed_evidence_ids)
    for item in items:
        if not set(item.evidence_ids) <= allowed:
            raise ValueError("goal candidate cites unavailable evidence")
    feasible = [item for item in items if item.feasible]
    if not feasible:
        return GoalDecision(None, None, None, (), "NO_FEASIBLE_GROUNDED_GOAL", None)
    critical = [item for item in feasible if item.urgency >= CRITICAL_URGENCY]
    pool = critical or feasible
    chosen = max(pool, key=lambda item: (_score(item, current_goal_id), item.goal_id))
    rationale = "CRITICAL_NEED" if critical else ("CONTINUE_VALID_GOAL" if chosen.goal_id == current_goal_id else "BEST_BOUNDED_CANDIDATE")
    return GoalDecision(
        chosen.goal_id,
        chosen.kind,
        chosen.target_id,
        chosen.evidence_ids,
        rationale,
        round(_score(chosen, current_goal_id), 6),
    )


def validate_provider_goal_choice(
    proposed_goal_id: str,
    candidates: Iterable[GoalCandidate],
    *,
    allowed_evidence_ids: Iterable[str],
) -> GoalDecision:
    items = tuple(candidates)
    matching = [item for item in items if item.goal_id == proposed_goal_id]
    if len(matching) != 1 or not matching[0].feasible:
        raise ValueError("provider selected an unavailable goal")
    item = matching[0]
    if not set(item.evidence_ids) <= set(allowed_evidence_ids):
        raise ValueError("provider-selected goal lacks evidence")
    return GoalDecision(item.goal_id, item.kind, item.target_id, item.evidence_ids, "VALIDATED_BOUNDED_PROPOSAL", round(_score(item, None), 6))


def render_goal_decision(decision: GoalDecision) -> str:
    if decision.goal_id is None:
        return "I do not have a feasible task right now."
    target = f" ({decision.target_id})" if decision.target_id else ""
    labels = {
        "EAT": "get food", "REST": "rest", "SEEK_TREATMENT": "seek treatment",
        "TEND_OTHER": "tend the injured", "DEFEND": "defend the colony", "FLEE": "move to safety",
        "HAUL": "haul supplies", "BUILD": "build", "CRAFT": "craft", "CLEAN": "clean",
        "RESEARCH": "research", "RECREATE": "take recreation", "SOCIALIZE": "socialize",
    }
    return f"My current goal is to {labels[decision.kind]}{target}."
