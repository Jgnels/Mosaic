from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class InterventionMotives:
    subject_protection_benefit: float
    scientific_value: float
    publication_value: float
    convenience_value: float
    infrastructure_cost_avoidance: float

    def to_dict(self):
        return asdict(self)


def protection_override_motive_check(m: InterventionMotives) -> tuple[bool, tuple[str, ...]]:
    reasons = []

    non_protection = max(
        m.scientific_value,
        m.publication_value,
        m.convenience_value,
        m.infrastructure_cost_avoidance,
    )

    if m.subject_protection_benefit <= non_protection:
        reasons.append(
            "subject-protection benefit is not the dominant justification"
        )

    if m.convenience_value >= .50:
        reasons.append("research convenience is materially influencing the override")

    if m.infrastructure_cost_avoidance >= .50:
        reasons.append("cost/storage avoidance is materially influencing the override")

    return (not reasons, tuple(reasons))
