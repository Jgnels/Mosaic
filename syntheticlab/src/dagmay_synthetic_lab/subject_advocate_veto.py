from __future__ import annotations
from dataclasses import dataclass, asdict

@dataclass(frozen=True)
class AdvocateDecision:
    advocate_actor_id: str
    branch_id: str
    intervention_id: str
    decision: str
    independent_from_primary_researcher: bool
    rationale: str
    def validate(self):
        if self.decision not in {"SUPPORT","VETO","ABSTAIN"}:
            raise ValueError("invalid advocate decision")
    def to_dict(self): return asdict(self)

def advocate_veto_blocks(decision: AdvocateDecision) -> bool:
    decision.validate()
    return decision.decision == "VETO"

def advocate_support_counts_for_authorization(
    decision: AdvocateDecision,
    primary_researcher_actor_id: str,
) -> bool:
    decision.validate()
    if decision.advocate_actor_id == primary_researcher_actor_id:
        return False
    if not decision.independent_from_primary_researcher:
        return False
    return decision.decision == "SUPPORT"
