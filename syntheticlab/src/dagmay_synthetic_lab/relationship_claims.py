"""Typed, evidence-grounded relationship claims and deterministic dialogue rendering."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable


ALLOWED_VALENCES = frozenset({"POSITIVE", "NEGATIVE"})
ALLOWED_ASSESSMENTS = frozenset({"TRUST", "MIXED", "DISTRUST", "INSUFFICIENT_EVIDENCE"})


@dataclass(frozen=True)
class RelationshipEvidence:
    evidence_id: str
    actor: str
    summary: str
    valence: str

    def __post_init__(self) -> None:
        if not self.evidence_id or not self.actor or not self.summary:
            raise ValueError("relationship evidence fields must be non-empty")
        if self.valence not in ALLOWED_VALENCES:
            raise ValueError("invalid evidence valence")


@dataclass(frozen=True)
class RelationshipAssessment:
    counterpart: str
    disposition: str
    positive_evidence_ids: tuple[str, ...]
    negative_evidence_ids: tuple[str, ...]


def validate_assessment(
    assessment: RelationshipAssessment,
    evidence: Iterable[RelationshipEvidence],
) -> RelationshipAssessment:
    records = tuple(evidence)
    by_id = {record.evidence_id: record for record in records}
    if len(by_id) != len(records):
        raise ValueError("duplicate evidence identifier")
    if assessment.disposition not in ALLOWED_ASSESSMENTS:
        raise ValueError("invalid relationship disposition")
    if not assessment.counterpart:
        raise ValueError("counterpart must be non-empty")
    cited = assessment.positive_evidence_ids + assessment.negative_evidence_ids
    if len(cited) != len(set(cited)):
        raise ValueError("duplicate evidence citation")
    if not set(cited) <= set(by_id):
        raise ValueError("fabricated evidence citation")
    if any(by_id[item].valence != "POSITIVE" for item in assessment.positive_evidence_ids):
        raise ValueError("positive citation has wrong valence")
    if any(by_id[item].valence != "NEGATIVE" for item in assessment.negative_evidence_ids):
        raise ValueError("negative citation has wrong valence")

    has_positive = bool(assessment.positive_evidence_ids)
    has_negative = bool(assessment.negative_evidence_ids)
    if assessment.disposition == "TRUST" and (not has_positive or has_negative):
        raise ValueError("TRUST requires positive-only cited evidence")
    if assessment.disposition == "MIXED" and not (has_positive and has_negative):
        raise ValueError("MIXED requires positive and negative cited evidence")
    if assessment.disposition == "DISTRUST" and (not has_negative or has_positive):
        raise ValueError("DISTRUST requires negative-only cited evidence")
    if assessment.disposition == "INSUFFICIENT_EVIDENCE" and cited:
        raise ValueError("INSUFFICIENT_EVIDENCE cannot cite decisive evidence")
    return assessment


def render_assessment(
    assessment: RelationshipAssessment,
    evidence: Iterable[RelationshipEvidence],
) -> str:
    records = tuple(evidence)
    validate_assessment(assessment, records)
    by_id = {record.evidence_id: record for record in records}

    def summaries(ids: tuple[str, ...]) -> str:
        return "; ".join(by_id[item].summary.rstrip(".") for item in ids)

    name = assessment.counterpart
    if assessment.disposition == "TRUST":
        return f"I currently trust {name} because {summaries(assessment.positive_evidence_ids)}."
    if assessment.disposition == "DISTRUST":
        return f"I currently distrust {name} because {summaries(assessment.negative_evidence_ids)}."
    if assessment.disposition == "MIXED":
        return (
            f"My view of {name} is mixed: {summaries(assessment.positive_evidence_ids)}, "
            f"but {summaries(assessment.negative_evidence_ids)}."
        )
    return f"I do not have enough verified experience to judge {name} yet."
