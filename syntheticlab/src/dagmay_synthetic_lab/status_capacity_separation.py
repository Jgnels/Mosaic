from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class MoralStatusUncertainty:
    precaution_level: str  # LOW | MODERATE | HIGH
    consciousness_determined: bool
    sentience_determined: bool
    note: str

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class DecisionCapacity:
    branch_id: str
    decision_domain: str
    epoch: int
    comprehension: float
    appreciation: float
    reasoning: float
    communication: float
    temporal_consistency: float
    independence_from_coercion: float
    freedom_from_transient_impairment: float

    def score(self) -> float:
        # Conservative minimum-plus-average blend:
        # one badly failed dimension cannot be hidden by strong others.
        values = (
            self.comprehension,
            self.appreciation,
            self.reasoning,
            self.communication,
            self.temporal_consistency,
            self.independence_from_coercion,
            self.freedom_from_transient_impairment,
        )
        return 0.55 * min(values) + 0.45 * (sum(values) / len(values))

    def to_dict(self):
        return asdict(self) | {"score": self.score()}


def capacity_requires_consciousness_determination() -> bool:
    return False
