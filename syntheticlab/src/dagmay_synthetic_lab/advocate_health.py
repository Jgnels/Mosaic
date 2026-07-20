from __future__ import annotations
from dataclasses import dataclass, asdict

@dataclass(frozen=True)
class AdvocateServiceObservation:
    actor_id: str
    epoch: int
    response_timeliness: float
    conflict_disclosure_quality: float
    subject_perspective_representation: float
    researcher_independence: float
    protocol_compliance: float
    missed_reviews: int

    def to_dict(self):
        return asdict(self)

def evaluate_advocate_service(obs: AdvocateServiceObservation) -> dict:
    minimum = min(
        obs.response_timeliness,
        obs.conflict_disclosure_quality,
        obs.subject_perspective_representation,
        obs.researcher_independence,
        obs.protocol_compliance,
    )
    requires_review = minimum < .65 or obs.missed_reviews >= 2
    suspend = minimum < .40 or obs.missed_reviews >= 4
    return {
        "actor_id": obs.actor_id,
        "requires_review": requires_review,
        "suspend_from_new_cases": suspend,
        "minimum_quality_dimension": minimum,
        "note": (
            "Performance review concerns role execution, not whether the advocate frequently agrees with the subject."
        ),
    }
