"""Bounded, provenance-aware relationship evidence selection over long histories."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable

from .relationship_claims import MAX_EVIDENCE_PER_VALENCE, RelationshipAssessment, RelationshipEvidence


@dataclass(frozen=True)
class RelationshipHistoryItem:
    evidence: RelationshipEvidence
    tick: int
    importance: float
    evidence_quality: float = 1.0
    source_kind: str = "DIRECT"
    retracts_evidence_id: str | None = None

    def __post_init__(self) -> None:
        if self.tick < 0:
            raise ValueError("tick must be non-negative")
        if not 0.0 <= self.importance <= 1.0:
            raise ValueError("importance must be between zero and one")
        if not 0.0 <= self.evidence_quality <= 1.0:
            raise ValueError("evidence quality must be between zero and one")
        if self.source_kind not in {"DIRECT", "RUMOR", "INFERRED"}:
            raise ValueError("unsupported source kind")
        if self.retracts_evidence_id == self.evidence.evidence_id:
            raise ValueError("an event cannot retract itself")


def _score(item: RelationshipHistoryItem, newest_tick: int) -> float:
    recency = item.tick / max(newest_tick, 1)
    source_weight = {"DIRECT": 3.0, "INFERRED": 0.5, "RUMOR": -4.0}[item.source_kind]
    return 4.0 * item.evidence_quality + 2.0 * item.importance + source_weight + 0.25 * recency


def select_relationship_evidence(
    history: Iterable[RelationshipHistoryItem],
    *,
    counterpart: str,
) -> tuple[RelationshipEvidence, ...]:
    items = tuple(history)
    newest = max((item.tick for item in items), default=1)
    known_ids = {item.evidence.evidence_id for item in items}
    retracted = {
        item.retracts_evidence_id
        for item in items
        if item.retracts_evidence_id in known_ids
        and item.source_kind == "DIRECT"
        and item.evidence_quality >= 0.75
    }
    selected: list[RelationshipHistoryItem] = []
    for valence in ("POSITIVE", "NEGATIVE"):
        candidates = [
            item for item in items
            if item.evidence.actor == counterpart
            and item.evidence.valence == valence
            and item.evidence.evidence_id not in retracted
            and item.retracts_evidence_id is None
            and item.source_kind == "DIRECT"
            and item.evidence_quality >= 0.75
        ]
        candidates.sort(key=lambda item: (_score(item, newest), item.tick, item.evidence.evidence_id), reverse=True)
        unique: list[RelationshipHistoryItem] = []
        seen_summaries: set[str] = set()
        for item in candidates:
            signature = item.evidence.summary.strip().casefold()
            if signature in seen_summaries:
                continue
            seen_summaries.add(signature)
            unique.append(item)
            if len(unique) == MAX_EVIDENCE_PER_VALENCE:
                break
        selected.extend(unique)
    return tuple(item.evidence for item in selected)


def assessment_from_selected_evidence(
    counterpart: str,
    evidence: Iterable[RelationshipEvidence],
) -> RelationshipAssessment:
    records = tuple(evidence)
    positive = tuple(item.evidence_id for item in records if item.valence == "POSITIVE")
    negative = tuple(item.evidence_id for item in records if item.valence == "NEGATIVE")
    if positive and negative:
        disposition = "MIXED"
    elif positive:
        disposition = "TRUST"
    elif negative:
        disposition = "DISTRUST"
    else:
        disposition = "INSUFFICIENT_EVIDENCE"
    return RelationshipAssessment(counterpart, disposition, positive, negative)
