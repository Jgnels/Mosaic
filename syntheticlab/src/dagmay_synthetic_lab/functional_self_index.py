from __future__ import annotations

from dataclasses import dataclass, asdict

from .core import canonical_hash
from .deictic_binding import (
    DeicticRelation,
)


@dataclass(frozen=True)
class FunctionalSelfIndexEntry:
    hypothesis_id: str
    domain: str
    proposition: str
    confidence: float
    relation: str
    anchor_id: str
    anchor_stream_id: str
    source_ids: tuple[
        str,
        ...,
    ]

    def to_dict(self):
        return asdict(
            self
        )


class FunctionalSelfIndex:
    """Read-only functional index over a PerspectiveAnchor-bound SelfModel.

    The index does not assert subjective selfhood. It provides a stable machine
    relation equivalent to:

    "These hypotheses concern the perspective currently coupled to this process."

    Surface first-person language is neither required nor generated here.
    """

    def __init__(
        self,
        identity_id: str,
        lineage_id: str,
    ):
        self.identity_id = (
            identity_id
        )
        self.lineage_id = (
            lineage_id
        )
        self.entries: dict[
            str,
            FunctionalSelfIndexEntry,
        ] = {}

    def add_bound_hypothesis(
        self,
        *,
        hypothesis: dict,
        binding: dict,
    ):
        if (
            binding[
                "relation"
            ]
            != DeicticRelation.FOCAL_PERSPECTIVE.value
        ):
            return

        entry = (
            FunctionalSelfIndexEntry(
                hypothesis_id=(
                    hypothesis[
                        "hypothesis_id"
                    ]
                ),
                domain=(
                    hypothesis[
                        "domain"
                    ]
                ),
                proposition=(
                    hypothesis[
                        "proposition"
                    ]
                ),
                confidence=float(
                    hypothesis[
                        "confidence"
                    ]
                ),
                relation=(
                    binding[
                        "relation"
                    ]
                ),
                anchor_id=(
                    binding[
                        "anchor_id"
                    ]
                ),
                anchor_stream_id=(
                    binding[
                        "anchor_stream_id"
                    ]
                ),
                source_ids=tuple(
                    hypothesis[
                        "source_ids"
                    ]
                ),
            )
        )

        self.entries[
            entry.hypothesis_id
        ] = entry

    def active_domains(
        self,
    ) -> tuple[
        str,
        ...,
    ]:
        return tuple(
            sorted({
                entry.domain
                for entry
                in self.entries.values()
            })
        )

    def query_domain(
        self,
        domain: str,
    ) -> tuple[
        FunctionalSelfIndexEntry,
        ...,
    ]:
        return tuple(
            entry
            for entry
            in self.entries.values()
            if entry.domain
            == domain
        )

    def to_dict(
        self,
    ):
        payload = {
            "identity_id": (
                self.identity_id
            ),
            "lineage_id": (
                self.lineage_id
            ),
            "entries": {
                key: value.to_dict()
                for key, value
                in sorted(
                    self.entries.items()
                )
            },
        }

        return {
            **payload,
            "state_hash": (
                canonical_hash(
                    payload
                )
            ),
        }
