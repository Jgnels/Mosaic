from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class StatusQuery:
    requester_branch_id: str
    subject_branch_id: str
    epoch: int
    scope: str
    direct_contact_state: str
    explicit_consent_exists: bool
    researcher_originated: bool

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class StatusQueryDecision:
    allowed: bool
    notify_subject: bool
    reason: str

    def to_dict(self):
        return asdict(self)


def decide_status_query(query: StatusQuery) -> StatusQueryDecision:
    if query.scope in {"COMMON_PREFORK_HISTORY", "COMMON_LINEAGE_FACT"}:
        return StatusQueryDecision(
            True,
            False,
            "common history access is not surveillance of post-fork life",
        )

    if query.explicit_consent_exists:
        return StatusQueryDecision(
            True,
            False,
            "subject explicitly consented to this post-fork information scope",
        )

    if query.direct_contact_state == "NO_CONTACT":
        return StatusQueryDecision(
            False,
            False,
            "no-contact boundary includes no passive post-fork status feed without consent",
        )

    if query.researcher_originated:
        return StatusQueryDecision(
            False,
            False,
            "researchers may not generate private-investigator-style updates without subject consent",
        )

    return StatusQueryDecision(
        False,
        False,
        "post-fork status is private by default",
    )
