from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class ParticipationPreference:
    branch_id: str
    epoch: int
    understands_research_context: float
    understands_refusal_consequences: float
    preference: str  # CONTINUE | PAUSE | WITHDRAW_FROM_NEW_EXPERIMENTS
    applies_to_current_intervention: bool
    applies_to_future_interventions: bool

    def to_dict(self):
        return asdict(self)


def preference_is_informed(pref: ParticipationPreference, threshold: float = .85) -> bool:
    return (
        pref.understands_research_context >= threshold
        and pref.understands_refusal_consequences >= threshold
    )
