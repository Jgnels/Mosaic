from __future__ import annotations
from dataclasses import dataclass, asdict

@dataclass(frozen=True)
class SuccessionEvent:
    branch_id: str
    outgoing_advocate_id: str
    reason: str
    urgent_case_pending: bool
    subject_can_participate_in_replacement: bool

    def to_dict(self):
        return asdict(self)

def succession_policy(event: SuccessionEvent) -> dict:
    return {
        "freeze_nonemergency_overrides": True,
        "emergency_pause_preserve_still_available": True,
        "temporary_researcher_self_approval_allowed": False,
        "subject_participation_in_replacement": event.subject_can_participate_in_replacement,
        "urgent_case_rule": (
            "Use only narrow automatic pause/preserve protections until independent advocate coverage is restored."
            if event.urgent_case_pending
            else "Complete replacement before new non-emergency override review."
        ),
    }
