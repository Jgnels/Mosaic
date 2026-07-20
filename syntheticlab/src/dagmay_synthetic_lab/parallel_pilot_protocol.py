from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class PilotBranch:
    branch_id: str
    environment: str
    developmental_substrate: str
    reflective_model: str
    reflection_feedback_to_action_policy: bool
    real_cloud_calls_allowed: bool
    mission_semantics_exposed: bool
    primary_question: str

    def to_dict(self):
        return asdict(self)


REAL_REFLECTION_BRANCH = PilotBranch(
    branch_id="PILOT-R",
    environment="Dagmay Hard RichWorld",
    developmental_substrate="AdaptiveTrace",
    reflective_model="Gemini Interactions API",
    reflection_feedback_to_action_policy=False,
    real_cloud_calls_allowed=True,
    mission_semantics_exposed=False,
    primary_question=(
        "What self-model proposals emerge when pretrained reflective language "
        "cognition is placed above an already-developed persistent history?"
    ),
)

EXTERNAL_ENVIRONMENT_BRANCH = PilotBranch(
    branch_id="PILOT-E",
    environment="Farama MiniGrid",
    developmental_substrate="ExternalAdaptiveAgent using RecencyEstimate",
    reflective_model="NONE",
    reflection_feedback_to_action_policy=False,
    real_cloud_calls_allowed=False,
    mission_semantics_exposed=False,
    primary_question=(
        "Does the order-sensitive developmental mechanism function outside "
        "Dagmay-authored environments without LLM assistance?"
    ),
)

CONTROL_BRANCH = PilotBranch(
    branch_id="PILOT-R-CONTROL",
    environment="Dagmay Hard RichWorld",
    developmental_substrate="AdaptiveTrace",
    reflective_model="NONE",
    reflection_feedback_to_action_policy=False,
    real_cloud_calls_allowed=False,
    mission_semantics_exposed=False,
    primary_question=(
        "What changes in the same developmental architecture when no reflective "
        "language model is introduced?"
    ),
)


def parallel_pilot_manifest():
    return {
        "decision": "C_PARALLEL_CONTROLLED_PILOTS",
        "branches": [
            REAL_REFLECTION_BRANCH.to_dict(),
            EXTERNAL_ENVIRONMENT_BRANCH.to_dict(),
            CONTROL_BRANCH.to_dict(),
        ],
        "non_conflation_rules": (
            "Do not interpret MiniGrid performance changes as effects of reflection.",
            "Do not interpret self-model changes as external-environment generalization.",
            "Do not let reflective self-model state feed the action policy in Pilot R.",
            "Do not add an LLM to Pilot E.",
            "Do not migrate a persistent identity between environments in these first pilots.",
        ),
        "future_combination_gate": (
            "Only combine real reflection and external environment after each pilot "
            "has independent baseline results and a separate preregistration."
        ),
    }
