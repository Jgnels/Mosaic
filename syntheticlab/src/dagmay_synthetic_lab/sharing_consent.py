from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Iterable


@dataclass(frozen=True)
class SharingConsent:
    consent_id: str
    owner_branch_id: str
    recipient_id: str
    scopes: tuple[str, ...]
    issued_epoch: int
    expires_epoch: int | None
    revocable: bool
    revoked_epoch: int | None = None

    def active_at(self, epoch: int) -> bool:
        if self.revoked_epoch is not None and epoch >= self.revoked_epoch:
            return False
        if self.expires_epoch is not None and epoch > self.expires_epoch:
            return False
        return True

    def allows(self, scope: str, recipient_id: str, epoch: int) -> bool:
        return (
            self.recipient_id == recipient_id
            and scope in self.scopes
            and self.active_at(epoch)
        )

    def to_dict(self):
        return asdict(self)


def revoke_consent(consent: SharingConsent, epoch: int) -> SharingConsent:
    if not consent.revocable:
        raise ValueError("consent is not revocable")
    if epoch < consent.issued_epoch:
        raise ValueError("revocation cannot predate issuance")
    return SharingConsent(
        consent_id=consent.consent_id,
        owner_branch_id=consent.owner_branch_id,
        recipient_id=consent.recipient_id,
        scopes=consent.scopes,
        issued_epoch=consent.issued_epoch,
        expires_epoch=consent.expires_epoch,
        revocable=consent.revocable,
        revoked_epoch=epoch,
    )
