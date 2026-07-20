from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import List
from .core import canonical_hash


@dataclass(frozen=True)
class PrivacyAuditEntry:
    request_id: str
    requester_id: str
    subject_branch_id: str
    scope: str
    purpose: str
    epoch: int
    allowed: bool
    disclosure_mode: str
    reason: str

    def to_dict(self):
        return asdict(self)


class PrivacyAuditLedger:
    def __init__(self):
        self.entries: List[PrivacyAuditEntry] = []

    def append(self, entry: PrivacyAuditEntry) -> None:
        self.entries.append(entry)

    def to_dict(self) -> dict:
        payload = [e.to_dict() for e in self.entries]
        return {
            "entries": payload,
            "hash": canonical_hash(payload),
        }

    def requests_about(self, branch_id: str) -> list[PrivacyAuditEntry]:
        return [e for e in self.entries if e.subject_branch_id == branch_id]
