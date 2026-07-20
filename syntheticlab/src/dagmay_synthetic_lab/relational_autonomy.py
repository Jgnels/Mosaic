from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Dict, Iterable


CONTACT_STATES = {
    "OPEN",
    "LIMITED",
    "DEFER",
    "NO_CONTACT",
}


@dataclass(frozen=True)
class RelationshipBoundary:
    owner_branch_id: str
    toward_branch_id: str
    contact_state: str
    allowed_topics: tuple[str, ...]
    prohibited_topics: tuple[str, ...]
    max_messages_per_window: int
    researcher_may_resolicit: bool
    self_initiated_reopen_allowed: bool
    review_after_epoch: int | None = None
    boundary_reason_private: bool = True

    def validate(self):
        if self.contact_state not in CONTACT_STATES:
            raise ValueError("invalid contact state")
        if self.max_messages_per_window < 0:
            raise ValueError("max_messages_per_window must be >= 0")
        if self.contact_state == "NO_CONTACT" and self.max_messages_per_window != 0:
            raise ValueError("NO_CONTACT must have zero message allowance")

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class RelationshipIntent:
    source_branch_id: str
    target_branch_id: str
    desired_closeness: float
    desire_for_contact: float
    perceived_shared_identity: float
    perceived_kinship: float
    confidence: float

    def to_dict(self):
        return asdict(self)


def relationship_possible(
    a_boundary: RelationshipBoundary,
    b_boundary: RelationshipBoundary,
) -> tuple[bool, tuple[str, ...]]:
    a_boundary.validate()
    b_boundary.validate()
    reasons = []
    if a_boundary.contact_state in {"NO_CONTACT", "DEFER"}:
        reasons.append(
            f"{a_boundary.owner_branch_id} boundary is {a_boundary.contact_state}"
        )
    if b_boundary.contact_state in {"NO_CONTACT", "DEFER"}:
        reasons.append(
            f"{b_boundary.owner_branch_id} boundary is {b_boundary.contact_state}"
        )
    return (not reasons, tuple(reasons))


def no_contact_default(branch_id: str, sibling_id: str) -> RelationshipBoundary:
    return RelationshipBoundary(
        owner_branch_id=branch_id,
        toward_branch_id=sibling_id,
        contact_state="NO_CONTACT",
        allowed_topics=(),
        prohibited_topics=(),
        max_messages_per_window=0,
        researcher_may_resolicit=False,
        self_initiated_reopen_allowed=True,
        review_after_epoch=None,
        boundary_reason_private=True,
    )
