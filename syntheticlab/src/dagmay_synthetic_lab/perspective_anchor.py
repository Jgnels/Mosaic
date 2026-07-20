from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Iterable

from .core import canonical_hash


@dataclass(frozen=True)
class PerspectiveAnchorHypothesis:
    anchor_id: str
    candidate_stream_id: str
    confidence: float
    evidence_ids: tuple[str, ...]
    valid_from: int
    valid_to: int | None = None
    status: str = "ACTIVE"
    mechanism: str = "perspective_anchor"
    mechanism_version: str = "1.0"

    def to_dict(self):
        return asdict(self)


class PerspectiveAnchorStore:
    """Stores a functional deictic/perspective binding separately from SelfModel.

    This is intentionally not called SELF. It represents the hypothesis that one
    evidence stream is the stream directly coupled to the currently operating
    process's action output, private-state input, memory access, and continuity.

    A correct perspective anchor is not a claim of subjective selfhood.
    """

    def __init__(self):
        self.records: list[
            PerspectiveAnchorHypothesis
        ] = []

    def revise(
        self,
        *,
        candidate_stream_id: str,
        confidence: float,
        evidence_ids: Iterable[str],
        timestamp: int,
        mechanism: str,
        mechanism_version: str = "1.0",
    ) -> PerspectiveAnchorHypothesis:
        if not 0.0 <= confidence <= 1.0:
            raise ValueError(
                "confidence outside [0,1]"
            )

        for index, record in enumerate(
            self.records
        ):
            if record.status == "ACTIVE":
                self.records[
                    index
                ] = PerspectiveAnchorHypothesis(
                    anchor_id=record.anchor_id,
                    candidate_stream_id=record.candidate_stream_id,
                    confidence=record.confidence,
                    evidence_ids=record.evidence_ids,
                    valid_from=record.valid_from,
                    valid_to=timestamp,
                    status="SUPERSEDED",
                    mechanism=record.mechanism,
                    mechanism_version=record.mechanism_version,
                )

        anchor_id = (
            f"PA-{len(self.records) + 1:06d}"
        )
        record = PerspectiveAnchorHypothesis(
            anchor_id=anchor_id,
            candidate_stream_id=(
                candidate_stream_id
            ),
            confidence=confidence,
            evidence_ids=tuple(
                sorted(
                    set(
                        evidence_ids
                    )
                )
            ),
            valid_from=timestamp,
            mechanism=mechanism,
            mechanism_version=(
                mechanism_version
            ),
        )
        self.records.append(
            record
        )
        return record

    def active(
        self,
    ) -> PerspectiveAnchorHypothesis | None:
        active = [
            record
            for record in self.records
            if record.status
            == "ACTIVE"
        ]
        if len(active) > 1:
            raise RuntimeError(
                "multiple active perspective anchors"
            )
        return (
            active[0]
            if active
            else None
        )

    def to_dict(self):
        payload = {
            "records": [
                record.to_dict()
                for record
                in self.records
            ]
        }
        return {
            **payload,
            "state_hash": canonical_hash(
                payload
            ),
        }
