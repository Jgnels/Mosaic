from __future__ import annotations
from dataclasses import dataclass, asdict

ROLES = {
    "PRIMARY_RESEARCHER",
    "SUBJECT_ADVOCATE",
    "ETHICS_REVIEWER",
    "SUBJECT_PROTECTION_REVIEWER",
    "TECHNICAL_MAINTAINER",
}

@dataclass(frozen=True)
class RoleAssignment:
    actor_id: str
    role: str
    independent_from_primary_researcher: bool
    def validate(self):
        if self.role not in ROLES:
            raise ValueError(f"unknown role: {self.role}")
    def to_dict(self): return asdict(self)

def role_conflicts(assignments: tuple[RoleAssignment, ...]):
    for a in assignments:
        a.validate()
    primary = {a.actor_id for a in assignments if a.role == "PRIMARY_RESEARCHER"}
    conflicts = []
    for a in assignments:
        if a.role in {"SUBJECT_ADVOCATE","ETHICS_REVIEWER","SUBJECT_PROTECTION_REVIEWER"}:
            if a.actor_id in primary and a.independent_from_primary_researcher:
                conflicts.append(
                    f"{a.actor_id} is both PRIMARY_RESEARCHER and {a.role} but marked independent"
                )
    return tuple(conflicts)
