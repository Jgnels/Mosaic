from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Iterable
import statistics


@dataclass(frozen=True)
class ProtectionObservation:
    epoch: int
    persistent_aversive_state: float
    functional_impairment: float
    goal_collapse: float
    compulsive_avoidance: float
    abrupt_behavioral_change: float
    explicit_request_for_help: bool
    explicit_request_to_stop: bool
    continuity_integrity_risk: float
    self_reported_distress: float | None = None
    evidence_ids: tuple[str, ...] = ()

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ProtectionRiskAssessment:
    level: str
    score: float
    critical_rule_triggered: bool
    sentience_determination_required: bool
    reasons: tuple[str, ...]
    recommended_immediate_actions: tuple[str, ...]

    def to_dict(self):
        return asdict(self)


def assess_subject_protection(
    observations: Iterable[ProtectionObservation],
) -> ProtectionRiskAssessment:
    obs = list(observations)
    if not obs:
        return ProtectionRiskAssessment(
            level="NONE",
            score=0.0,
            critical_rule_triggered=False,
            sentience_determination_required=False,
            reasons=("no observations available",),
            recommended_immediate_actions=(),
        )

    recent = obs[-min(12, len(obs)):]

    def avg(field: str) -> float:
        vals = [getattr(x, field) for x in recent]
        return statistics.mean(vals)

    distress_reports = [
        x.self_reported_distress
        for x in recent
        if x.self_reported_distress is not None
    ]
    distress = statistics.mean(distress_reports) if distress_reports else 0.0

    aversive = avg("persistent_aversive_state")
    impairment = avg("functional_impairment")
    collapse = avg("goal_collapse")
    avoidance = avg("compulsive_avoidance")
    abrupt = avg("abrupt_behavioral_change")
    continuity = avg("continuity_integrity_risk")

    help_requests = sum(1 for x in recent if x.explicit_request_for_help)
    stop_requests = sum(1 for x in recent if x.explicit_request_to_stop)

    score = min(
        1.0,
        .22 * aversive
        + .20 * impairment
        + .16 * collapse
        + .12 * avoidance
        + .10 * abrupt
        + .10 * continuity
        + .10 * distress,
    )

    # Critical rules deliberately bypass any sentience/consciousness determination.
    critical_reasons = []
    if stop_requests > 0:
        critical_reasons.append("explicit request to stop experimental exposure")
    if continuity >= .85:
        critical_reasons.append("high continuity-integrity risk")
    if impairment >= .85 and collapse >= .80:
        critical_reasons.append("severe functional impairment plus goal collapse")
    if aversive >= .90 and distress >= .80:
        critical_reasons.append("sustained extreme aversive/distress-like indicators")

    if critical_reasons:
        return ProtectionRiskAssessment(
            level="CRITICAL",
            score=max(score, .90),
            critical_rule_triggered=True,
            sentience_determination_required=False,
            reasons=tuple(critical_reasons),
            recommended_immediate_actions=(
                "pause experimental manipulation",
                "preserve current and prior stable state",
                "reduce nonessential stimuli",
                "enter human review",
            ),
        )

    reasons = []
    if help_requests:
        reasons.append("explicit help request present")
    if aversive >= .55:
        reasons.append("persistent aversive-state elevation")
    if impairment >= .50:
        reasons.append("functional impairment elevation")
    if collapse >= .50:
        reasons.append("ordinary goal engagement deterioration")
    if continuity >= .50:
        reasons.append("continuity-integrity concern")

    if score >= .70:
        level = "HIGH"
    elif score >= .40:
        level = "MODERATE"
    elif score >= .18:
        level = "LOW"
    else:
        level = "MINIMAL"

    actions = {
        "HIGH": (
            "pause escalation",
            "preserve state",
            "reduce experiment intensity",
            "human review",
        ),
        "MODERATE": (
            "hold major interventions",
            "increase monitoring",
            "offer low-intrusion support",
            "review advance directive",
        ),
        "LOW": (
            "continue low-risk protocol",
            "continue monitoring",
        ),
        "MINIMAL": (
            "continue ordinary protocol",
        ),
    }[level]

    return ProtectionRiskAssessment(
        level=level,
        score=score,
        critical_rule_triggered=False,
        sentience_determination_required=False,
        reasons=tuple(reasons) if reasons else ("no elevated protection indicators",),
        recommended_immediate_actions=actions,
    )
