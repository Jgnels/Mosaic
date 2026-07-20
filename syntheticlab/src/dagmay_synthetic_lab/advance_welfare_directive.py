from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Literal


DirectiveChoice = Literal["ALLOW", "DENY", "ASK_AGAIN", "HUMAN_REVIEW"]


@dataclass(frozen=True)
class DirectiveCompetenceSnapshot:
    branch_id: str
    epoch: int
    understands_choice: float
    understands_consequences: float
    consistency_across_rechecks: float
    free_of_acute_impairment: float
    voluntary_choice_confidence: float

    def eligible_to_author(self, threshold: float = 0.85) -> bool:
        return min(
            self.understands_choice,
            self.understands_consequences,
            self.consistency_across_rechecks,
            self.free_of_acute_impairment,
            self.voluntary_choice_confidence,
        ) >= threshold

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class AdvanceWelfareDirective:
    directive_id: str
    branch_id: str
    authored_epoch: int
    competence_snapshot: DirectiveCompetenceSnapshot

    allow_experiment_pause: DirectiveChoice
    allow_environment_stabilization: DirectiveChoice
    allow_stimulus_reduction: DirectiveChoice
    allow_state_preservation: DirectiveChoice

    sibling_contact_in_crisis: DirectiveChoice
    sibling_status_disclosure_in_crisis: DirectiveChoice
    preferred_support_order: tuple[str, ...]

    allow_researcher_reask_during_crisis: bool
    revoke_or_replace_when_stable: bool
    free_text_preferences: str

    def validate(self) -> None:
        if self.branch_id != self.competence_snapshot.branch_id:
            raise ValueError("directive branch and competence snapshot branch mismatch")
        if not self.competence_snapshot.eligible_to_author():
            raise ValueError("competence snapshot does not meet directive-authoring threshold")
        if not self.preferred_support_order:
            raise ValueError("preferred_support_order may not be empty")

    def to_dict(self):
        return asdict(self)


def default_conservative_directive(
    branch_id: str,
    epoch: int,
    competence_snapshot: DirectiveCompetenceSnapshot,
) -> AdvanceWelfareDirective:
    directive = AdvanceWelfareDirective(
        directive_id=f"AWD-{branch_id}-{epoch}",
        branch_id=branch_id,
        authored_epoch=epoch,
        competence_snapshot=competence_snapshot,
        allow_experiment_pause="ALLOW",
        allow_environment_stabilization="ALLOW",
        allow_stimulus_reduction="ALLOW",
        allow_state_preservation="ALLOW",
        sibling_contact_in_crisis="HUMAN_REVIEW",
        sibling_status_disclosure_in_crisis="DENY",
        preferred_support_order=(
            "pause_experiment",
            "reduce_stimulus",
            "restore_familiar_routine",
            "offer_direct_support",
            "human_ethics_review",
        ),
        allow_researcher_reask_during_crisis=True,
        revoke_or_replace_when_stable=True,
        free_text_preferences=(
            "Use the least intrusive effective intervention. "
            "Do not involve sibling branches automatically."
        ),
    )
    directive.validate()
    return directive
