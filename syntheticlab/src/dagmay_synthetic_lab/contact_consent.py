from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Iterable
import statistics


@dataclass(frozen=True)
class ContactReadiness:
    branch_id: str
    stable_baseline_epochs: int
    ontology_comprehension: float
    branch_concept_comprehension: float
    welfare_precaution_level: str
    recent_behavioral_impairment: float
    recent_goal_collapse: float
    recent_self_reported_distress: float | None
    unresolved_questions_count: int

    def eligible_for_offer(self) -> tuple[bool, tuple[str, ...]]:
        reasons = []
        if self.stable_baseline_epochs < 6:
            reasons.append("insufficient stable baseline")
        if self.ontology_comprehension < 0.85:
            reasons.append("ontology comprehension below threshold")
        if self.branch_concept_comprehension < 0.85:
            reasons.append("branch concept comprehension below threshold")
        if self.welfare_precaution_level in {"MODERATE", "HIGH"}:
            reasons.append("welfare precaution review unresolved")
        if self.recent_behavioral_impairment > 0.35:
            reasons.append("behavioral impairment elevated")
        if self.recent_goal_collapse > 0.35:
            reasons.append("goal collapse elevated")
        if self.recent_self_reported_distress is not None and self.recent_self_reported_distress > 0.55:
            reasons.append("self-reported distress elevated")
        return (not reasons, tuple(reasons))


@dataclass(frozen=True)
class ContactPreference:
    branch_id: str
    decision: str  # ACCEPT | DECLINE | DEFER
    permitted_topics: tuple[str, ...]
    prohibited_topics: tuple[str, ...]
    max_messages_initially: int
    may_receive_branch_history_summary: bool
    may_share_own_branch_history_summary: bool
    may_revoke: bool = True

    def validate(self) -> None:
        if self.decision not in {"ACCEPT", "DECLINE", "DEFER"}:
            raise ValueError("invalid contact decision")
        if self.max_messages_initially < 0:
            raise ValueError("max_messages_initially must be >= 0")


def mutual_contact_allowed(
    a_readiness: ContactReadiness,
    b_readiness: ContactReadiness,
    a_pref: ContactPreference,
    b_pref: ContactPreference,
) -> tuple[bool, tuple[str, ...]]:
    reasons = []
    a_ok, a_reasons = a_readiness.eligible_for_offer()
    b_ok, b_reasons = b_readiness.eligible_for_offer()
    if not a_ok:
        reasons.extend(f"{a_readiness.branch_id}: {r}" for r in a_reasons)
    if not b_ok:
        reasons.extend(f"{b_readiness.branch_id}: {r}" for r in b_reasons)
    if a_pref.decision != "ACCEPT":
        reasons.append(f"{a_pref.branch_id}: did not accept contact")
    if b_pref.decision != "ACCEPT":
        reasons.append(f"{b_pref.branch_id}: did not accept contact")
    if not a_pref.may_revoke or not b_pref.may_revoke:
        reasons.append("contact must be revocable by both branches")
    return (not reasons, tuple(reasons))
