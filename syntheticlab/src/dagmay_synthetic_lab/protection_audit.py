from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import List
from .core import canonical_hash


@dataclass(frozen=True)
class ProtectionAuditEvent:
    event_id: str
    branch_id: str
    epoch: int
    event_type: str
    risk_level: str
    action: str
    sentience_determination_used: bool
    human_review_required: bool
    evidence_ids: tuple[str, ...]
    rationale: str

    def to_dict(self):
        return asdict(self)


class ProtectionAuditLedger:
    def __init__(self):
        self.events: List[ProtectionAuditEvent] = []

    def append(self, event: ProtectionAuditEvent):
        if event.sentience_determination_used:
            raise ValueError(
                "Subject-protection actions may not depend on a sentience determination."
            )
        self.events.append(event)

    def to_dict(self):
        payload = [e.to_dict() for e in self.events]
        return {
            "events": payload,
            "hash": canonical_hash(payload),
        }
