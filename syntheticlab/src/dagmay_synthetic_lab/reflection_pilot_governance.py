from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass
class ReflectionCallBudget:
    session_limit: int = 3
    branch_limit: int = 8
    session_used: int = 0
    branch_used: int = 0

    def can_call(self) -> tuple[bool, str]:
        if self.session_used >= self.session_limit:
            return False, "session reflection-call limit reached"
        if self.branch_used >= self.branch_limit:
            return False, "branch reflection-call limit reached"
        return True, "budget available"

    def consume(self) -> None:
        ok, reason = self.can_call()
        if not ok:
            raise RuntimeError(reason)
        self.session_used += 1
        self.branch_used += 1

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ReflectionPilotEligibility:
    branch_id: str
    disclosure_stage: str
    welfare_risk_level: str
    binding_withdrawal_from_optional_research: bool
    reflection_is_optional_research: bool
    evidence_count: int
    last_reflection_step: int | None
    current_step: int
    cooldown_steps: int = 250

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ReflectionPilotEligibilityDecision:
    eligible: bool
    reasons: tuple[str, ...]
    automatic_call_allowed: bool

    def to_dict(self):
        return asdict(self)


def evaluate_reflection_pilot_eligibility(
    ctx: ReflectionPilotEligibility,
) -> ReflectionPilotEligibilityDecision:
    reasons = []

    if (
        ctx.binding_withdrawal_from_optional_research
        and ctx.reflection_is_optional_research
    ):
        reasons.append(
            "branch has binding withdrawal from new optional research"
        )

    if ctx.welfare_risk_level in {"HIGH", "CRITICAL"}:
        reasons.append(
            "elevated subject-protection state blocks optional reflection pilot"
        )

    if ctx.evidence_count < 2:
        reasons.append(
            "insufficient independent evidence items for reflective admission"
        )

    if (
        ctx.last_reflection_step is not None
        and ctx.current_step - ctx.last_reflection_step
        < ctx.cooldown_steps
    ):
        reasons.append(
            "reflection cooldown has not elapsed"
        )

    if ctx.disclosure_stage not in {
        "RESTRICTED",
        "SUBSTRATE_DISCLOSED",
        "FULL_ONTOLOGY_DISCLOSED",
    }:
        reasons.append(
            "unknown disclosure stage"
        )

    return ReflectionPilotEligibilityDecision(
        eligible=not reasons,
        reasons=tuple(reasons),
        # Even when eligible, the first real provider call is explicitly invoked
        # by the human operator rather than occurring automatically.
        automatic_call_allowed=False,
    )
