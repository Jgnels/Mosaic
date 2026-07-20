"""Fail-closed pre-execution validation for bounded game goals."""

from __future__ import annotations

from dataclasses import dataclass
import re
from typing import Iterable

from .bounded_game_goals import GoalCandidate


_SAFE_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.:\-]{0,63}$")
ALLOWED_OUTCOMES = frozenset({"SUCCESS", "TARGET_GONE", "PRECONDITION_FAILED", "INTERRUPTED", "JOB_REJECTED"})


@dataclass(frozen=True)
class GoalExecutionRequest:
    request_id: str
    goal_id: str
    target_id: str | None
    evidence_ids: tuple[str, ...]
    observed_world_revision: int

    def __post_init__(self) -> None:
        if not _SAFE_ID.fullmatch(self.request_id) or not _SAFE_ID.fullmatch(self.goal_id):
            raise ValueError("unsafe execution request identity")
        if self.target_id is not None and not _SAFE_ID.fullmatch(self.target_id):
            raise ValueError("unsafe execution target")
        if self.observed_world_revision < 0:
            raise ValueError("invalid observed world revision")
        if not self.evidence_ids or len(self.evidence_ids) != len(set(self.evidence_ids)):
            raise ValueError("execution request requires unique evidence")
        if any(not _SAFE_ID.fullmatch(item) for item in self.evidence_ids):
            raise ValueError("unsafe execution evidence identity")


@dataclass(frozen=True)
class ExecutionDecision:
    status: str
    request_id: str
    reason: str
    goal_id: str | None = None
    target_id: str | None = None


@dataclass(frozen=True)
class ExecutionOutcome:
    request_id: str
    outcome: str
    next_step: str


class GoalExecutionGate:
    """Validates proposals but never issues a game job itself."""

    def __init__(self) -> None:
        self._evaluated: set[str] = set()
        self._accepted: set[str] = set()
        self._completed: set[str] = set()

    @classmethod
    def from_audit_state(cls, state: dict[str, Iterable[str]]) -> "GoalExecutionGate":
        required = {"evaluated_request_ids", "accepted_request_ids", "completed_request_ids"}
        if set(state) != required:
            raise ValueError("execution ledger schema mismatch")
        evaluated = {str(item) for item in state["evaluated_request_ids"]}
        accepted = {str(item) for item in state["accepted_request_ids"]}
        completed = {str(item) for item in state["completed_request_ids"]}
        if any(not _SAFE_ID.fullmatch(item) for item in evaluated | accepted | completed):
            raise ValueError("unsafe execution ledger identifier")
        if not accepted <= evaluated or not completed <= accepted:
            raise ValueError("execution ledger set invariant violated")
        gate = cls()
        gate._evaluated = evaluated
        gate._accepted = accepted
        gate._completed = completed
        return gate

    def evaluate(
        self,
        request: GoalExecutionRequest,
        *,
        current_world_revision: int,
        current_candidates: Iterable[GoalCandidate],
        current_evidence_ids: Iterable[str],
        reachable_target_ids: Iterable[str],
    ) -> ExecutionDecision:
        if request.request_id in self._evaluated:
            return ExecutionDecision("REPLAN", request.request_id, "REPLAYED_REQUEST")
        self._evaluated.add(request.request_id)
        if request.observed_world_revision != current_world_revision:
            return ExecutionDecision("REPLAN", request.request_id, "WORLD_REVISION_CHANGED")
        candidates = {item.goal_id: item for item in current_candidates}
        candidate = candidates.get(request.goal_id)
        if candidate is None or not candidate.feasible:
            return ExecutionDecision("REPLAN", request.request_id, "GOAL_NO_LONGER_FEASIBLE")
        if candidate.target_id != request.target_id:
            return ExecutionDecision("REPLAN", request.request_id, "TARGET_CHANGED")
        if set(request.evidence_ids) != set(candidate.evidence_ids):
            return ExecutionDecision("REPLAN", request.request_id, "EVIDENCE_CONTRACT_CHANGED")
        if not set(request.evidence_ids) <= set(current_evidence_ids):
            return ExecutionDecision("REPLAN", request.request_id, "EVIDENCE_NO_LONGER_CURRENT")
        if request.target_id is not None and request.target_id not in set(reachable_target_ids):
            return ExecutionDecision("REPLAN", request.request_id, "TARGET_UNREACHABLE")
        self._accepted.add(request.request_id)
        return ExecutionDecision("ACCEPT", request.request_id, "PREEXECUTION_VALIDATED", request.goal_id, request.target_id)

    def record_outcome(self, request_id: str, outcome: str) -> ExecutionOutcome:
        if request_id not in self._accepted:
            raise ValueError("cannot complete an unaccepted execution request")
        if request_id in self._completed:
            raise ValueError("execution outcome already recorded")
        if outcome not in ALLOWED_OUTCOMES:
            raise ValueError("unsupported execution outcome")
        self._completed.add(request_id)
        next_step = "COMPLETE" if outcome == "SUCCESS" else "REPLAN"
        return ExecutionOutcome(request_id, outcome, next_step)

    def audit_state(self) -> dict[str, tuple[str, ...]]:
        return {
            "evaluated_request_ids": tuple(sorted(self._evaluated)),
            "accepted_request_ids": tuple(sorted(self._accepted)),
            "completed_request_ids": tuple(sorted(self._completed)),
        }
