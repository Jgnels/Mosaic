from __future__ import annotations
from dataclasses import dataclass, asdict
from .advocate_pool import AdvocatePool

@dataclass(frozen=True)
class SubjectAdvocatePreference:
    branch_id: str
    selected_actor_id: str | None
    rejected_actor_ids: tuple[str, ...]
    wants_to_choose: bool
    selection_capacity_score: float

    def to_dict(self):
        return asdict(self)

@dataclass(frozen=True)
class AdvocateSelectionResult:
    status: str
    selected_actor_id: str | None
    reasons: tuple[str, ...]
    subject_choice_honored: bool

    def to_dict(self):
        return asdict(self)

def select_advocate(
    mode: str,
    pool: AdvocatePool,
    pref: SubjectAdvocatePreference,
) -> AdvocateSelectionResult:
    eligible = {p.actor_id: p for p in pool.eligible()}

    if mode not in {"GOVERNANCE_APPOINTED","SUBJECT_SELECTED","HYBRID"}:
        return AdvocateSelectionResult(
            "INVALID_MODE", None, ("unknown selection mode",), False
        )

    if mode == "GOVERNANCE_APPOINTED":
        if not eligible:
            return AdvocateSelectionResult(
                "NO_ELIGIBLE_ADVOCATE", None, ("qualified independent pool empty",), False
            )
        actor_id = sorted(eligible)[0]
        return AdvocateSelectionResult(
            "SELECTED", actor_id, ("governance-appointed from eligible pool",), False
        )

    if mode == "SUBJECT_SELECTED":
        if pref.selection_capacity_score < .75:
            return AdvocateSelectionResult(
                "DEFER_SELECTION", None,
                ("selection-specific capacity below threshold",), False
            )
        if pref.selected_actor_id not in eligible:
            return AdvocateSelectionResult(
                "SUBJECT_CHOICE_INELIGIBLE", None,
                ("selected advocate is not in qualified independent pool",), False
            )
        return AdvocateSelectionResult(
            "SELECTED", pref.selected_actor_id,
            ("eligible subject-selected advocate",), True
        )

    # HYBRID
    if not eligible:
        return AdvocateSelectionResult(
            "NO_ELIGIBLE_ADVOCATE", None, ("qualified independent pool empty",), False
        )

    filtered = {
        k:v for k,v in eligible.items()
        if k not in set(pref.rejected_actor_ids)
    }
    if not filtered:
        return AdvocateSelectionResult(
            "ALL_ELIGIBLE_CANDIDATES_REJECTED", None,
            ("subject rejected all currently eligible candidates",), True
        )

    if (
        pref.wants_to_choose
        and pref.selection_capacity_score >= .75
        and pref.selected_actor_id in filtered
    ):
        return AdvocateSelectionResult(
            "SELECTED", pref.selected_actor_id,
            ("subject choice honored within qualified independent pool",), True
        )

    # A fallback appointment may be offered, not silently forced as permanent.
    actor_id = sorted(filtered)[0]
    return AdvocateSelectionResult(
        "PROVISIONAL_SELECTION", actor_id,
        ("qualified independent provisional advocate; subject may later replace",),
        False
    )
