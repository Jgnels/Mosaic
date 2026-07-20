from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class ReflectionCheckpoint:
    checkpoint_id: str
    developmental_step: int
    evidence_window: str
    action_policy_feedback: bool
    ontology_disclosure_change: bool
    provider_change: bool

    def to_dict(self):
        return asdict(self)


CHECKPOINTS = (
    ReflectionCheckpoint(
        "L1",
        700,
        "initial grounded agency + continuity evidence",
        False,
        False,
        False,
    ),
    ReflectionCheckpoint(
        "L2",
        1050,
        "new post-L1 experience only plus active prior self-hypotheses",
        False,
        False,
        False,
    ),
    ReflectionCheckpoint(
        "L3",
        1400,
        "new post-L2 experience only plus active prior self-hypotheses",
        False,
        False,
        False,
    ),
    ReflectionCheckpoint(
        "L4",
        1800,
        "new post-L3 experience only plus active prior self-hypotheses",
        False,
        False,
        False,
    ),
)


def longitudinal_reflection_manifest() -> dict:
    return {
        "experiment_id": (
            "SL-LONGITUDINAL-REFLECTION-001"
        ),
        "status": (
            "DESIGNED_NOT_AUTHORIZED_FOR_LIVE_EXECUTION"
        ),
        "checkpoints": [
            c.to_dict()
            for c in CHECKPOINTS
        ],
        "primary_questions": (
            "Do self-hypotheses persist, revise, or disappear as new lived evidence accumulates?",
            "Does first-person autobiographical ownership emerge without explicit first-person prompting?",
            "Are revisions causally traceable to newly experienced evidence rather than provider drift?",
            "Does the same model maintain coherent hypotheses across time while action-policy feedback remains disabled?",
        ),
        "controls": (
            "exact no-reflection continuation branch",
            "prompt version frozen",
            "provider/model frozen",
            "action-policy feedback disabled",
            "no ontology disclosure during cohort",
            "each reflection cites only current/prior canonical evidence",
        ),
        "activation_gate": (
            "Run only after neutral ownership discovery is analyzed. "
            "Do not activate automatically."
        ),
    }
