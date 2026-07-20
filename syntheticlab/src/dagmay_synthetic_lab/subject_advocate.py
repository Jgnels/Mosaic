from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class AdvocateReview:
    advocate_role: str
    independent_from_primary_research_team: bool
    decision_domain: str
    supports_subject_preference: bool
    supports_protective_override: bool
    rationale: str

    def to_dict(self):
        return asdict(self)


def independent_advocate_required_for_override() -> bool:
    # Scaffolded recommendation; not yet canonical governance.
    return True
