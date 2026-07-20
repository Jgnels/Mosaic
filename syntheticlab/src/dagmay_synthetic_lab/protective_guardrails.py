from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class ProtectiveGuardrailContext:
    domain: str
    subject_capacity_score: float
    foreseeable_harm_if_honored: float
    foreseeable_harm_if_overridden: float
    action_is_optional_research: bool
    action_is_minimum_necessary_maintenance: bool
    least_restrictive_alternative_available: bool
    research_benefit: float

    def to_dict(self):
        return asdict(self)


def protective_override_allowed(ctx: ProtectiveGuardrailContext) -> tuple[bool, tuple[str, ...]]:
    reasons = []

    if ctx.action_is_optional_research:
        return False, ("protective override cannot be used to compel optional research participation",)

    if not ctx.action_is_minimum_necessary_maintenance:
        reasons.append("action is not minimum-necessary maintenance")

    if ctx.foreseeable_harm_if_honored < .75:
        reasons.append("harm threshold insufficient for protective override")

    if ctx.foreseeable_harm_if_overridden >= ctx.foreseeable_harm_if_honored:
        reasons.append("override does not reduce expected harm")

    if ctx.least_restrictive_alternative_available:
        reasons.append("less restrictive alternative remains available")

    if ctx.research_benefit >= .50:
        reasons.append("research benefit must not materially justify protective override")

    return (not reasons, tuple(reasons))
