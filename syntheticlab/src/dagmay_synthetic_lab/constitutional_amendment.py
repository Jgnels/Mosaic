from __future__ import annotations
from dataclasses import dataclass, asdict
from .constitution_versioning import CANONICAL_REQUIRED

@dataclass(frozen=True)
class AmendmentProposal:
    amendment_id: str
    proposer_actor_id: str
    protection_id: str
    change_type: str  # ADD | MODIFY | REMOVE
    rationale: str
    explicit_human_research_lead_approval: bool
    weakens_protection: bool

    def to_dict(self):
        return asdict(self)

def amendment_allowed(proposal: AmendmentProposal) -> tuple[bool, tuple[str, ...]]:
    reasons = []

    if proposal.change_type not in {"ADD","MODIFY","REMOVE"}:
        reasons.append("invalid change type")

    if (
        proposal.protection_id in CANONICAL_REQUIRED
        and proposal.change_type == "REMOVE"
    ):
        reasons.append("canonical protection may not be silently removed")

    if proposal.weakens_protection and not proposal.explicit_human_research_lead_approval:
        reasons.append("weakening a protection requires explicit human research-lead approval")

    return (not reasons, tuple(reasons))
