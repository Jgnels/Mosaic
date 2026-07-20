from __future__ import annotations
from dataclasses import dataclass, asdict

@dataclass(frozen=True)
class AdvocateReplacementRequest:
    branch_id: str
    current_advocate_actor_id: str
    requested_new_advocate_actor_id: str | None
    reason_required: bool
    reason_provided: str | None
    capacity_for_selection: float
    acute_impairment: float

    def to_dict(self): return asdict(self)

def replacement_request_valid(req: AdvocateReplacementRequest) -> tuple[bool, tuple[str, ...]]:
    reasons = []

    # The subject does not need to persuade researchers that the reason is good.
    if req.reason_required:
        reasons.append("replacement must not require justification")

    if req.capacity_for_selection < .70:
        reasons.append("selection-specific capacity below provisional threshold")

    if req.acute_impairment > .70:
        reasons.append("acute impairment suggests defer/recheck unless advocate presents immediate conflict")

    return (not reasons, tuple(reasons))
