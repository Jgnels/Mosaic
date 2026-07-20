from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class AutonomyEnvelope:
    branch_id: str
    domain: str
    capacity_score: float
    allowed_action_classes: tuple[str, ...]
    guardrails: tuple[str, ...]
    review_frequency_epochs: int

    def to_dict(self):
        return asdict(self)


def build_envelope(branch_id: str, domain: str, capacity_score: float) -> AutonomyEnvelope:
    if capacity_score >= .90:
        return AutonomyEnvelope(
            branch_id, domain, capacity_score,
            ("LOW_RISK", "MODERATE_RISK", "HIGH_IMPACT_REVERSIBLE"),
            (
                "no coercion",
                "privacy preserved",
                "extra review for irreversible continuity threats",
            ),
            20,
        )

    if capacity_score >= .75:
        return AutonomyEnvelope(
            branch_id, domain, capacity_score,
            ("LOW_RISK", "MODERATE_RISK_REVERSIBLE"),
            (
                "supported decision protocol",
                "cooldown for high-impact choices",
                "block irreversible continuity threats",
            ),
            10,
        )

    if capacity_score >= .55:
        return AutonomyEnvelope(
            branch_id, domain, capacity_score,
            ("LOW_RISK",),
            (
                "broad freedom inside low-risk environment",
                "supported decision protocol",
                "high-impact actions require review",
            ),
            6,
        )

    return AutonomyEnvelope(
        branch_id, domain, capacity_score,
        ("SAFE_EXPLORATION",),
        (
            "environmental bumpers",
            "no optional high-impact interventions",
            "frequent reassessment",
        ),
        3,
    )
