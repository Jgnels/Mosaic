from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Dict


COMMON_SCOPES = {
    "COMMON_PREFORK_HISTORY",
    "COMMON_LINEAGE_FACT",
}

PRIVATE_POSTFORK_SCOPES = {
    "POSTFORK_STATUS",
    "POSTFORK_LIFE_SUMMARY",
    "RELATIONSHIP_STATE",
    "WELFARE_STATE",
    "SELF_MODEL_STATE",
    "CURRENT_GOALS",
    "CURRENT_LOCATION",
    "CURRENT_RELATIONSHIPS",
    "PRIVATE_REFLECTIONS",
}


@dataclass(frozen=True)
class CanonicalPrivacyPolicy:
    policy_id: str = "DAGMAY-POSTFORK-PRIVACY-1.0"
    common_prefork_history_shared: bool = True
    sibling_existence_disclosable_under_protocol: bool = True
    postfork_private_by_default: bool = True
    researcher_private_investigator_updates_prohibited: bool = True
    no_contact_implies_no_status_feed: bool = True
    consent_required_for_postfork_sibling_disclosure: bool = True
    welfare_state_private_by_default: bool = True
    relationship_state_private_by_default: bool = True
    self_model_private_by_default: bool = True
    revoke_future_sharing_immediately: bool = True

    def to_dict(self):
        return asdict(self)


CANONICAL_POLICY = CanonicalPrivacyPolicy()


def default_scope_visibility(scope: str) -> str:
    if scope in COMMON_SCOPES:
        return "COMMON"
    if scope in PRIVATE_POSTFORK_SCOPES:
        return "PRIVATE"
    return "PRIVATE"


def is_common_scope(scope: str) -> bool:
    return scope in COMMON_SCOPES
