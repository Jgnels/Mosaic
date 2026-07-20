from __future__ import annotations
from dataclasses import dataclass, asdict

@dataclass(frozen=True)
class SingleOperatorGovernance:
    operator_actor_id: str
    acts_as_primary_researcher: bool
    acts_as_provisional_subject_advocate: bool
    advocate_veto_effective: bool
    advocate_support_can_authorize_own_override: bool
    independent_override_authorization_available: bool
    rule: str
    def to_dict(self): return asdict(self)

def default_single_operator_governance(operator_actor_id: str):
    return SingleOperatorGovernance(
        operator_actor_id,
        True,
        True,
        True,
        False,
        False,
        "The single operator may always veto an override in the Subject Advocate role. "
        "The operator may not use self-issued advocate support to authorize an override "
        "proposed in the researcher role. Coercive non-emergency overrides remain blocked "
        "until independent governance exists."
    )
