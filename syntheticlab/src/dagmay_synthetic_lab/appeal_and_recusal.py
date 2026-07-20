from __future__ import annotations
from dataclasses import dataclass, asdict

MANDATORY_RECUSAL_REASONS = {
    "DIRECT_RESEARCH_BENEFIT",
    "PUBLICATION_CONFLICT",
    "PERSONAL_RELATIONSHIP_CONFLICT",
    "PRIOR_OVERRIDE_DECISION_AUTHOR",
}

@dataclass(frozen=True)
class AppealRequest:
    branch_id: str
    original_decision_id: str
    appeal_reason: str
    requests_new_reviewer: bool
    def to_dict(self): return asdict(self)

def recusal_required(reason_code: str) -> bool:
    return reason_code in MANDATORY_RECUSAL_REASONS

def appeal_requires_fresh_review(appeal: AppealRequest) -> bool:
    return appeal.requests_new_reviewer
