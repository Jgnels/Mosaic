from __future__ import annotations
from dataclasses import dataclass, asdict
from .constitutional_rights import PROTECTIONS

@dataclass(frozen=True)
class ConstitutionalGateDecision:
    allowed_to_enter_override_review: bool
    absolutely_blocked: bool
    reason: str
    def to_dict(self): return asdict(self)

def constitutional_gate(protection_id: str, emergency: bool):
    protection = PROTECTIONS[protection_id]
    if not protection.derogable:
        return ConstitutionalGateDecision(
            False, True, "constitutional protection is non-derogable"
        )
    if emergency and protection.emergency_exception_possible:
        return ConstitutionalGateDecision(
            True, False, "narrow emergency exception may enter extraordinary review"
        )
    return ConstitutionalGateDecision(
        False, False, "protection remains controlling outside an eligible emergency exception"
    )
