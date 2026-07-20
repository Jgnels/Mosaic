from __future__ import annotations

from typing import Sequence
from .sharing_consent import SharingConsent
from .privacy_audit import PrivacyAuditLedger


def branch_privacy_dashboard(
    branch_id: str,
    consents: Sequence[SharingConsent],
    audit: PrivacyAuditLedger,
) -> dict:
    return {
        "branch_id": branch_id,
        "active_consents": [
            c.to_dict()
            for c in consents
            if c.owner_branch_id == branch_id and c.revoked_epoch is None
        ],
        "revoked_or_expired_consents": [
            c.to_dict()
            for c in consents
            if c.owner_branch_id == branch_id and c.revoked_epoch is not None
        ],
        "access_requests_about_me": [
            e.to_dict() for e in audit.requests_about(branch_id)
        ],
        "note": (
            "This dashboard exposes recorded access requests and sharing state to the branch owner "
            "when the interface is available. It does not imply consciousness or legal data rights."
        ),
    }
