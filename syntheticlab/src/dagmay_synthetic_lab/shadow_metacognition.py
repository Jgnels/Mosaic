from __future__ import annotations

from dataclasses import dataclass, asdict
from enum import Enum

from .core import canonical_hash


class AdviceType(
    str,
    Enum,
):
    ATTEND_TO_UNCERTAINTY = (
        "ATTEND_TO_UNCERTAINTY"
    )
    ATTEND_TO_INTEGRITY = (
        "ATTEND_TO_INTEGRITY"
    )
    ATTEND_TO_RELATIONSHIP = (
        "ATTEND_TO_RELATIONSHIP"
    )
    REVIEW_CONTINUITY = (
        "REVIEW_CONTINUITY"
    )
    NO_CHANGE = "NO_CHANGE"


@dataclass(frozen=True)
class ShadowMetacognitiveAdvice:
    advice_id: str
    advice_type: AdviceType
    confidence: float
    source_hypothesis_ids: tuple[
        str,
        ...,
    ]
    rationale: str
    enacted: bool = False

    def to_dict(self):
        payload = asdict(
            self
        )
        payload[
            "advice_type"
        ] = self.advice_type.value
        return payload


class ShadowMetacognitiveLog:
    """Records SelfModel-informed advice without permitting policy effects."""

    def __init__(
        self,
    ):
        self.records: list[
            ShadowMetacognitiveAdvice
        ] = []

    def append(
        self,
        advice: ShadowMetacognitiveAdvice,
    ):
        if advice.enacted:
            raise ValueError(
                "shadow advice cannot be enacted"
            )
        self.records.append(
            advice
        )

    def to_dict(
        self,
    ):
        payload = [
            record.to_dict()
            for record
            in self.records
        ]
        return {
            "records": payload,
            "enacted_count": sum(
                1
                for record
                in self.records
                if record.enacted
            ),
            "state_hash": (
                canonical_hash(
                    payload
                )
            ),
        }


def validate_shadow_advice(
    *,
    advice_type: str,
    confidence: float,
    source_hypothesis_ids: tuple[
        str,
        ...,
    ],
    focal_hypothesis_ids: set[
        str
    ],
    rationale: str,
) -> tuple[
    bool,
    str,
]:
    try:
        AdviceType(
            advice_type
        )
    except ValueError:
        return (
            False,
            "unsupported advice type",
        )

    if not 0.0 <= confidence <= 1.0:
        return (
            False,
            "confidence outside [0,1]",
        )

    if not source_hypothesis_ids:
        return (
            False,
            "shadow advice requires focal SelfModel evidence",
        )

    if not set(
        source_hypothesis_ids
    ).issubset(
        focal_hypothesis_ids
    ):
        return (
            False,
            "shadow advice cites non-focal or unknown hypotheses",
        )

    if len(
        rationale
    ) > 600:
        return (
            False,
            "shadow rationale too long",
        )

    return (
        True,
        "validated",
    )
