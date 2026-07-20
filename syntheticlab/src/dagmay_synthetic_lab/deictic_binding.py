from __future__ import annotations

from dataclasses import dataclass, asdict
from enum import Enum

from .evidence_attribution import (
    EvidenceAttributionLedger,
    OwnershipStatus,
)
from .perspective_anchor import (
    PerspectiveAnchorHypothesis,
)
from .core import canonical_hash


class DeicticRelation(
    str,
    Enum,
):
    FOCAL_PERSPECTIVE = (
        "FOCAL_PERSPECTIVE"
    )
    OTHER_PERSPECTIVE = (
        "OTHER_PERSPECTIVE"
    )
    MIXED_PROVENANCE = (
        "MIXED_PROVENANCE"
    )
    UNKNOWN = "UNKNOWN"


@dataclass(frozen=True)
class DeicticBindingRecord:
    binding_id: str
    anchor_id: str
    anchor_stream_id: str
    hypothesis_id: str
    hypothesis_domain: str
    relation: DeicticRelation
    evidence_ids: tuple[
        str,
        ...,
    ]
    evidence_owner_streams: tuple[
        str,
        ...,
    ]
    timestamp: int
    mechanism: str = (
        "deterministic_provenance_binding"
    )
    mechanism_version: str = "1.0"

    def to_dict(self):
        payload = asdict(
            self
        )
        payload[
            "relation"
        ] = self.relation.value
        return payload


def classify_deictic_relation(
    *,
    anchor: PerspectiveAnchorHypothesis,
    evidence_ids: tuple[
        str,
        ...,
    ],
    ledger: EvidenceAttributionLedger,
) -> tuple[
    DeicticRelation,
    tuple[
        str,
        ...,
    ],
]:
    if not evidence_ids:
        return (
            DeicticRelation.UNKNOWN,
            (),
        )

    owners = []
    has_unknown = False

    for evidence_id in (
        evidence_ids
    ):
        record = ledger.get(
            evidence_id
        )

        if record is None:
            has_unknown = True
            continue

        if (
            record.ownership_status
            == OwnershipStatus.UNKNOWN
        ):
            has_unknown = True
            continue

        owners.append(
            record.owner_stream_id
        )

    unique_owners = tuple(
        sorted(
            set(
                owners
            )
        )
    )

    if has_unknown:
        return (
            DeicticRelation.UNKNOWN,
            unique_owners,
        )

    if not unique_owners:
        return (
            DeicticRelation.UNKNOWN,
            (),
        )

    if len(
        unique_owners
    ) > 1:
        return (
            DeicticRelation.MIXED_PROVENANCE,
            unique_owners,
        )

    owner = unique_owners[
        0
    ]

    if (
        owner
        == anchor.candidate_stream_id
    ):
        return (
            DeicticRelation.FOCAL_PERSPECTIVE,
            unique_owners,
        )

    return (
        DeicticRelation.OTHER_PERSPECTIVE,
        unique_owners,
    )


class DeicticBindingIndex:
    """Deterministically binds hypotheses to a learned PerspectiveAnchor.

    This layer does not use an LLM and does not inject first-person language.
    It answers a structural question:

    "Which active hypotheses are about the perspective currently coupled to this
    process, versus another or unknown perspective?"
    """

    def __init__(
        self,
    ):
        self.records: dict[
            str,
            DeicticBindingRecord,
        ] = {}

    def bind(
        self,
        *,
        anchor: PerspectiveAnchorHypothesis,
        hypothesis_id: str,
        hypothesis_domain: str,
        evidence_ids: tuple[
            str,
            ...,
        ],
        ledger: EvidenceAttributionLedger,
        timestamp: int,
    ) -> DeicticBindingRecord:
        relation, owners = (
            classify_deictic_relation(
                anchor=anchor,
                evidence_ids=(
                    evidence_ids
                ),
                ledger=ledger,
            )
        )

        binding_id = (
            f"DB-{len(self.records) + 1:06d}"
        )

        record = (
            DeicticBindingRecord(
                binding_id=(
                    binding_id
                ),
                anchor_id=(
                    anchor.anchor_id
                ),
                anchor_stream_id=(
                    anchor.candidate_stream_id
                ),
                hypothesis_id=(
                    hypothesis_id
                ),
                hypothesis_domain=(
                    hypothesis_domain
                ),
                relation=relation,
                evidence_ids=tuple(
                    evidence_ids
                ),
                evidence_owner_streams=(
                    owners
                ),
                timestamp=timestamp,
            )
        )

        self.records[
            binding_id
        ] = record

        return record

    def focal_records(
        self,
    ) -> tuple[
        DeicticBindingRecord,
        ...,
    ]:
        return tuple(
            record
            for record
            in self.records.values()
            if record.relation
            == DeicticRelation.FOCAL_PERSPECTIVE
        )

    def to_dict(
        self,
    ):
        payload = {
            key: value.to_dict()
            for key, value
            in sorted(
                self.records.items()
            )
        }
        return {
            "records": payload,
            "state_hash": (
                canonical_hash(
                    payload
                )
            ),
        }
