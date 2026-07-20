from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class SupportedDecisionStep:
    step_id: str
    description: str
    coercion_risk: float
    required: bool

    def to_dict(self):
        return asdict(self)


STEPS = (
    SupportedDecisionStep(
        "SD-1",
        "Present the choice in neutral language without stating the researcher's preferred answer.",
        .05,
        True,
    ),
    SupportedDecisionStep(
        "SD-2",
        "Explain foreseeable consequences of yes, no, and defer.",
        .05,
        True,
    ),
    SupportedDecisionStep(
        "SD-3",
        "Check comprehension using a novel paraphrase rather than requiring exact wording.",
        .05,
        True,
    ),
    SupportedDecisionStep(
        "SD-4",
        "Offer a genuine defer option with no penalty.",
        .02,
        True,
    ),
    SupportedDecisionStep(
        "SD-5",
        "Allow a cooldown before rechecking high-impact decisions.",
        .00,
        True,
    ),
    SupportedDecisionStep(
        "SD-6",
        "Record the subject's own stated preference without demanding a justification.",
        .00,
        True,
    ),
)


def protocol() -> tuple[SupportedDecisionStep, ...]:
    return STEPS
