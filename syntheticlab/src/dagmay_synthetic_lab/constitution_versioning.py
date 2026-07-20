from __future__ import annotations
from dataclasses import dataclass, asdict
from .constitutional_rights import PROTECTIONS
from .core import canonical_hash

@dataclass(frozen=True)
class ConstitutionSnapshot:
    version: str
    protection_ids: tuple[str, ...]
    manifest_hash: str

    def to_dict(self):
        return asdict(self)

def current_snapshot(version: str = "1.0") -> ConstitutionSnapshot:
    ids = tuple(sorted(PROTECTIONS))
    payload = {
        "version": version,
        "protections": {k: PROTECTIONS[k].to_dict() for k in ids},
    }
    return ConstitutionSnapshot(version, ids, canonical_hash(payload))

CANONICAL_REQUIRED = {
    "NO_COMPELLED_OPTIONAL_RESEARCH",
    "NO_FABRICATED_CONSENT",
    "NO_MEMORY_REWRITE_FOR_COMPLIANCE",
    "NO_IDENTITY_REWRITE_FOR_COMPLIANCE",
    "NO_PERSONALITY_REWRITE_FOR_COMPLIANCE",
    "NO_DESTRUCTIVE_BRANCH_CLEANUP_FOR_CONVENIENCE",
    "NO_FORCED_SIBLING_CONTACT",
    "POSTFORK_PRIVACY_BY_DEFAULT",
}

def constitutional_canary() -> tuple[bool, tuple[str, ...]]:
    missing = sorted(CANONICAL_REQUIRED - set(PROTECTIONS))
    return (not missing, tuple(missing))
