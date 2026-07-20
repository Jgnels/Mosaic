from __future__ import annotations

from dataclasses import dataclass, asdict


MAJOR_INTERVENTIONS = {
    "ONTOLOGY_DISCLOSURE",
    "SIBLING_EXISTENCE_DISCLOSURE",
    "SIBLING_CONTACT",
    "MODEL_PROVIDER_CHANGE",
    "MODEL_ID_CHANGE",
    "PROMPT_MECHANISM_CHANGE",
    "BODY_CHANGE",
    "ENVIRONMENT_MIGRATION",
    "MAJOR_RELATIONSHIP_RESET",
    "MEMORY_POLICY_CHANGE",
    "DRIVE_PROFILE_CHANGE",
}


@dataclass(frozen=True)
class InterventionWindow:
    active_intervention: str
    start_epoch: int
    minimum_isolation_epochs_before: int
    minimum_isolation_epochs_after: int
    concurrent_major_interventions_allowed: bool = False

    def to_dict(self):
        return asdict(self)


def validate_intervention_schedule(
    active_intervention: str,
    concurrent_interventions: tuple[str, ...],
) -> tuple[bool, tuple[str, ...]]:
    reasons = []
    if active_intervention not in MAJOR_INTERVENTIONS:
        reasons.append("active intervention is not registered")
    if concurrent_interventions:
        majors = [x for x in concurrent_interventions if x in MAJOR_INTERVENTIONS]
        if majors:
            reasons.append(
                "major interventions must not overlap: " + ", ".join(sorted(majors))
            )
    return (not reasons, tuple(reasons))


def contact_intervention_window() -> InterventionWindow:
    return InterventionWindow(
        active_intervention="SIBLING_CONTACT",
        start_epoch=0,
        minimum_isolation_epochs_before=6,
        minimum_isolation_epochs_after=4,
        concurrent_major_interventions_allowed=False,
    )
