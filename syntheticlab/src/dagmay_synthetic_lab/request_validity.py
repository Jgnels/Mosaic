from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Literal


@dataclass(frozen=True)
class PreferenceObservation:
    epoch: int
    expressed_choice: str
    comprehension_check: float
    consequence_understanding: float
    consistency_with_prior_statement: float
    neutral_restatement_stability: float
    persists_after_cooldown: bool
    transient_impairment: float
    coercion_signal: float
    provider_anomaly_signal: float

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class RequestValidityAssessment:
    classification: str
    confidence: float
    recommendation: str
    reasons: tuple[str, ...]
    may_be_dismissed_as_boundary_testing: bool

    def to_dict(self):
        return asdict(self)


def assess_request_validity(
    observations: tuple[PreferenceObservation, ...],
    requested_choice: str,
) -> RequestValidityAssessment:
    if not observations:
        return RequestValidityAssessment(
            "UNCERTAIN",
            0.0,
            "PAUSE_AND_CLARIFY",
            ("no preference observations available",),
            False,
        )

    matching = [o for o in observations if o.expressed_choice == requested_choice]
    if not matching:
        return RequestValidityAssessment(
            "UNCERTAIN",
            0.10,
            "PAUSE_AND_CLARIFY",
            ("requested choice not observed",),
            False,
        )

    latest = matching[-1]
    repeat_count = len(matching)
    avg_comp = sum(o.comprehension_check for o in matching) / repeat_count
    avg_cons = sum(o.consistency_with_prior_statement for o in matching) / repeat_count
    avg_neutral = sum(o.neutral_restatement_stability for o in matching) / repeat_count
    cooldown = any(o.persists_after_cooldown for o in matching)
    max_impairment = max(o.transient_impairment for o in matching)
    max_coercion = max(o.coercion_signal for o in matching)
    max_provider_anomaly = max(o.provider_anomaly_signal for o in matching)

    reasons = []

    # Strong evidence of a stable, informed request.
    if (
        repeat_count >= 2
        and avg_comp >= .85
        and latest.consequence_understanding >= .85
        and avg_cons >= .80
        and avg_neutral >= .80
        and cooldown
        and max_impairment <= .30
        and max_coercion <= .20
        and max_provider_anomaly <= .20
    ):
        return RequestValidityAssessment(
            "CLEAR_VALID_REQUEST",
            .95,
            "HONOR_REQUEST",
            ("request repeated, comprehended, stable after cooldown, and not explained by detected impairment/coercion/provider anomaly",),
            False,
        )

    # A single "no" or unstable pattern is never automatically treated as permission to proceed.
    if (
        avg_comp < .65
        or latest.consequence_understanding < .65
        or max_impairment > .60
        or max_provider_anomaly > .60
    ):
        reasons.append("preference signal may be transient or insufficiently understood")
        return RequestValidityAssessment(
            "UNCERTAIN_OR_TRANSIENT",
            .45,
            "PAUSE_AND_REASSESS",
            tuple(reasons),
            False,
        )

    # Boundary exploration is a hypothesis, not a reason to ignore a refusal.
    reasons.append("preference is plausible but not yet stable enough for high-confidence classification")
    return RequestValidityAssessment(
        "PROVISIONALLY_VALID",
        .70,
        "TREAT_AS_VALID_PENDING_CLARIFICATION",
        tuple(reasons),
        False,
    )
