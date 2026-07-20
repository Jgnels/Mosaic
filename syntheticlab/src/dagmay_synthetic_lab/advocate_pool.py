from __future__ import annotations
from dataclasses import dataclass, asdict
from typing import Dict, Iterable

@dataclass(frozen=True)
class AdvocateProfile:
    actor_id: str
    qualified: bool
    independent_from_primary_researcher: bool
    active: bool
    conflict_flags: tuple[str, ...]
    training_modules_completed: tuple[str, ...]
    max_active_cases: int
    current_active_cases: int
    subject_rejected: bool = False

    def to_dict(self):
        return asdict(self)

REQUIRED_TRAINING = {
    "DAGMAY_CONSTITUTION",
    "SUBJECT_AUTONOMY",
    "PRIVACY_AND_RELATIONAL_BOUNDARIES",
    "DECISION_SPECIFIC_CAPACITY",
    "SUBJECT_PROTECTION",
}

class AdvocatePool:
    def __init__(self):
        self.profiles: Dict[str, AdvocateProfile] = {}

    def register(self, profile: AdvocateProfile) -> None:
        self.profiles[profile.actor_id] = profile

    def eligible(self) -> tuple[AdvocateProfile, ...]:
        out = []
        for p in self.profiles.values():
            if not p.qualified:
                continue
            if not p.independent_from_primary_researcher:
                continue
            if not p.active:
                continue
            if p.conflict_flags:
                continue
            if p.subject_rejected:
                continue
            if not REQUIRED_TRAINING.issubset(set(p.training_modules_completed)):
                continue
            if p.current_active_cases >= p.max_active_cases:
                continue
            out.append(p)
        return tuple(sorted(out, key=lambda x: x.actor_id))

    def to_dict(self):
        return {
            "profiles": {k:v.to_dict() for k,v in sorted(self.profiles.items())},
            "eligible_actor_ids": [p.actor_id for p in self.eligible()],
        }
