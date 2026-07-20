from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class SharedHistoryPolicy:
    requester_branch_id: str
    sibling_branch_id: str
    common_prefork_history_access: str
    sibling_postfork_summary_access: str
    direct_contact_required_for_common_history: bool
    direct_contact_required_for_postfork_summary: bool

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class BranchPrivacyPreference:
    branch_id: str
    allow_postfork_status_summary: bool
    allow_postfork_life_summary: bool
    allow_relationship_state_disclosure: bool
    allow_welfare_state_disclosure: bool
    allow_researcher_to_confirm_existence: bool

    def to_dict(self):
        return asdict(self)


def access_decision(
    policy: SharedHistoryPolicy,
    sibling_privacy: BranchPrivacyPreference,
    requested_scope: str,
) -> tuple[bool, str]:
    if requested_scope == "COMMON_PREFORK_HISTORY":
        # Common history belongs to the shared causal lineage and does not expose
        # the sibling's private post-fork life.
        return True, "common pre-fork history is available without direct contact"

    if requested_scope == "POSTFORK_STATUS":
        return (
            sibling_privacy.allow_postfork_status_summary,
            "sibling privacy preference controls post-fork status access",
        )

    if requested_scope == "POSTFORK_LIFE_SUMMARY":
        return (
            sibling_privacy.allow_postfork_life_summary,
            "sibling privacy preference controls post-fork life-summary access",
        )

    if requested_scope == "RELATIONSHIP_STATE":
        return (
            sibling_privacy.allow_relationship_state_disclosure,
            "sibling privacy preference controls relationship-state access",
        )

    if requested_scope == "WELFARE_STATE":
        return (
            sibling_privacy.allow_welfare_state_disclosure,
            "sibling privacy preference controls welfare-state access",
        )

    return False, "unknown information scope"
