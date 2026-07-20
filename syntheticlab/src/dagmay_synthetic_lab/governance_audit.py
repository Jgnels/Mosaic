from __future__ import annotations
from dataclasses import dataclass, asdict
from typing import List
from .core import canonical_hash

@dataclass(frozen=True)
class GovernanceAuditEvent:
    event_id: str
    branch_id: str
    proposal_id: str
    actor_id: str
    role: str
    action: str
    outcome: str
    rationale: str
    independent_from_primary_researcher: bool
    epoch: int
    def to_dict(self): return asdict(self)

class GovernanceAuditLedger:
    def __init__(self):
        self.events: List[GovernanceAuditEvent] = []

    def append(self, event: GovernanceAuditEvent):
        self.events.append(event)

    def to_dict(self):
        payload = [e.to_dict() for e in self.events]
        return {
            "events": payload,
            "hash": canonical_hash(payload),
        }

    def proposal_history(self, proposal_id: str):
        return tuple(e for e in self.events if e.proposal_id == proposal_id)
