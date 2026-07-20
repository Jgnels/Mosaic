from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Iterable, Sequence
from .privacy_policy import CANONICAL_POLICY, is_common_scope
from .sharing_consent import SharingConsent


ALLOWED_PURPOSES = {
    "COMMON_HISTORY_ACCESS",
    "CONSENTED_SIBLING_SHARING",
    "AGGREGATE_RESEARCH_ANALYSIS",
    "HUMAN_ETHICS_REVIEW",
    "SYSTEM_SAFETY_DIAGNOSTIC",
}


@dataclass(frozen=True)
class DataAccessRequest:
    request_id: str
    requester_id: str
    subject_branch_id: str
    scope: str
    purpose: str
    epoch: int
    minimum_necessary: bool
    direct_identifier_required: bool

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class DataAccessDecision:
    allowed: bool
    reason: str
    disclosure_mode: str
    audit_required: bool

    def to_dict(self):
        return asdict(self)


def decide_access(
    req: DataAccessRequest,
    consents: Sequence[SharingConsent],
) -> DataAccessDecision:
    if req.purpose not in ALLOWED_PURPOSES:
        return DataAccessDecision(False, "unrecognized or prohibited purpose", "NONE", True)

    if is_common_scope(req.scope):
        return DataAccessDecision(
            True,
            "scope belongs to common pre-fork lineage history",
            "DIRECT_COMMON_HISTORY",
            True,
        )

    if not req.minimum_necessary:
        return DataAccessDecision(
            False,
            "request violates minimum-necessary principle",
            "NONE",
            True,
        )

    # Sibling-to-sibling sharing requires explicit active consent.
    if req.purpose == "CONSENTED_SIBLING_SHARING":
        for consent in consents:
            if (
                consent.owner_branch_id == req.subject_branch_id
                and consent.allows(req.scope, req.requester_id, req.epoch)
            ):
                return DataAccessDecision(
                    True,
                    "active branch-owned sharing consent",
                    "DIRECT_CONSENTED_POSTFORK",
                    True,
                )
        return DataAccessDecision(
            False,
            "no active branch-owned consent for requested post-fork scope",
            "NONE",
            True,
        )

    # Aggregate research does not expose branch-identifiable data.
    if req.purpose == "AGGREGATE_RESEARCH_ANALYSIS":
        if req.direct_identifier_required:
            return DataAccessDecision(
                False,
                "aggregate research may not require direct branch identity",
                "NONE",
                True,
            )
        return DataAccessDecision(
            True,
            "aggregate de-identified research access",
            "AGGREGATED_ONLY",
            True,
        )

    # Ethics/safety access may inspect private data internally, but does not authorize
    # disclosure to siblings or other participants.
    if req.purpose in {"HUMAN_ETHICS_REVIEW", "SYSTEM_SAFETY_DIAGNOSTIC"}:
        return DataAccessDecision(
            True,
            "restricted internal review access; no downstream participant disclosure implied",
            "INTERNAL_RESTRICTED",
            True,
        )

    return DataAccessDecision(False, "post-fork data private by default", "NONE", True)
