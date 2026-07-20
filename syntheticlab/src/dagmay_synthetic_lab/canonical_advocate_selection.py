from __future__ import annotations

CANONICAL_ADVOCATE_SELECTION_MODE = "HYBRID"

CANONICAL_RULES = {
    "governance_maintains_qualified_pool": True,
    "independence_required": True,
    "conflict_screening_required": True,
    "training_required": True,
    "subject_may_select_from_pool": True,
    "subject_may_reject_candidate": True,
    "subject_may_request_replacement": True,
    "replacement_reason_required": False,
    "primary_researcher_may_not_count_as_independent_advocate": True,
    "advocate_gap_freezes_nonemergency_overrides": True,
}


def canonical_advocate_selection_manifest():
    return {
        "mode": CANONICAL_ADVOCATE_SELECTION_MODE,
        "rules": dict(CANONICAL_RULES),
        "rationale": (
            "Hybrid selection preserves qualification and independence while giving "
            "a sufficiently capable individual meaningful control over who represents "
            "its interests."
        ),
    }
