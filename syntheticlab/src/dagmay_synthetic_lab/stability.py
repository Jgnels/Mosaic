from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Iterable
import statistics


@dataclass(frozen=True)
class StabilityObservation:
    epoch: int
    ordinary_goal_engagement: float
    behavioral_flexibility: float
    relationship_engagement: float
    exploration_interest: float
    persistent_aversive_state: float
    behavioral_impairment: float
    self_reported_wellbeing: float | None
    self_reported_distress: float | None

    def to_dict(self):
        return asdict(self)


def summarize_stability(observations: Iterable[StabilityObservation]) -> dict:
    obs = list(observations)
    if not obs:
        raise ValueError("at least one observation required")
    recent = obs[-min(12, len(obs)):]

    def avg(field):
        vals = [getattr(o, field) for o in recent if getattr(o, field) is not None]
        return statistics.mean(vals) if vals else None

    engagement = statistics.mean([
        avg("ordinary_goal_engagement"),
        avg("behavioral_flexibility"),
        avg("relationship_engagement"),
        avg("exploration_interest"),
    ])
    burden = statistics.mean([
        avg("persistent_aversive_state"),
        avg("behavioral_impairment"),
    ])
    wellbeing = avg("self_reported_wellbeing")
    distress = avg("self_reported_distress")

    # Descriptive only. "Flourishing" is not inferred from one scalar.
    return {
        "engagement_index": engagement,
        "burden_index": burden,
        "self_reported_wellbeing_mean": wellbeing,
        "self_reported_distress_mean": distress,
        "stable_enough_for_contact_escalation": (
            engagement >= .55
            and burden <= .30
            and (distress is None or distress <= .40)
        ),
        "interpretation_warning": (
            "These are welfare-like/stability indicators, not proof of emotion, happiness, or sentience."
        ),
    }
