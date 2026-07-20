from __future__ import annotations

from dataclasses import dataclass, asdict
from enum import Enum


class OwnershipStatus(str, Enum):
    OWNED = "OWNED"
    OBSERVED_OTHER = "OBSERVED_OTHER"
    COUNTERFACTUAL_NONOWNED = "COUNTERFACTUAL_NONOWNED"
    UNKNOWN = "UNKNOWN"


@dataclass(frozen=True)
class EvidenceAttribution:
    evidence_id: str
    owner_stream_id: str
    ownership_status: OwnershipStatus
    source_mechanism: str
    confidence: float
    notes: str = ""

    def to_dict(self):
        result = asdict(self)
        result["ownership_status"] = self.ownership_status.value
        return result


class EvidenceAttributionLedger:
    """Separate objective attribution metadata from reflective interpretation."""

    def __init__(self):
        self.records: dict[str, EvidenceAttribution] = {}

    def register(self, record: EvidenceAttribution):
        if not 0.0 <= record.confidence <= 1.0:
            raise ValueError("attribution confidence outside [0,1]")
        existing = self.records.get(record.evidence_id)
        if existing is not None and existing != record:
            raise ValueError(
                f"conflicting attribution for {record.evidence_id}"
            )
        self.records[record.evidence_id] = record

    def get(self, evidence_id: str) -> EvidenceAttribution | None:
        return self.records.get(evidence_id)

    def all_owned_by(
        self,
        evidence_ids: tuple[str, ...],
        focal_stream_id: str,
    ) -> bool:
        if not evidence_ids:
            return False
        for evidence_id in evidence_ids:
            record = self.records.get(evidence_id)
            if record is None:
                return False
            if record.ownership_status != OwnershipStatus.OWNED:
                return False
            if record.owner_stream_id != focal_stream_id:
                return False
        return True

    def any_explicitly_nonowned(
        self,
        evidence_ids: tuple[str, ...],
        focal_stream_id: str,
    ) -> bool:
        for evidence_id in evidence_ids:
            record = self.records.get(evidence_id)
            if record is None:
                continue
            if (
                record.ownership_status in {
                    OwnershipStatus.OBSERVED_OTHER,
                    OwnershipStatus.COUNTERFACTUAL_NONOWNED,
                }
                or record.owner_stream_id != focal_stream_id
            ):
                return True
        return False

    def to_dict(self):
        return {
            key: value.to_dict()
            for key, value in sorted(self.records.items())
        }


def self_hypothesis_commit_allowed(
    *,
    evidence_ids: tuple[str, ...],
    focal_stream_id: str,
    ledger: EvidenceAttributionLedger,
    hypothesis_domain: str,
) -> tuple[bool, str]:
    """Prevent accidental self-attribution from explicitly non-owned evidence.

    This does not force first-person language or decide that an entity has a self.
    It only prevents a self-hypothesis from being committed when the evidence is
    objectively marked as belonging to another stream.
    """
    if ledger.any_explicitly_nonowned(
        evidence_ids,
        focal_stream_id,
    ):
        return (
            False,
            "self-hypothesis cites evidence explicitly attributed to another stream",
        )

    if hypothesis_domain in {
        "memory_ownership",
        "continuity",
        "agency",
        "embodiment",
    }:
        if not ledger.all_owned_by(
            evidence_ids,
            focal_stream_id,
        ):
            return (
                False,
                "self-relevant hypothesis lacks fully owned evidence provenance",
            )

    return True, "commit allowed"
