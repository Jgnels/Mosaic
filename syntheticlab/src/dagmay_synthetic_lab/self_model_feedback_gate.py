from __future__ import annotations

from dataclasses import dataclass, asdict
from enum import Enum


class FeedbackLevel(
    str,
    Enum,
):
    NONE = "NONE"
    SHADOW_ONLY = (
        "SHADOW_ONLY"
    )
    RETRIEVAL_ATTENTION_ONLY = (
        "RETRIEVAL_ATTENTION_ONLY"
    )
    BOUNDED_ACTION_BIAS = (
        "BOUNDED_ACTION_BIAS"
    )
    HIGH_LEVEL_GOAL_PROPOSAL = (
        "HIGH_LEVEL_GOAL_PROPOSAL"
    )


@dataclass(frozen=True)
class FeedbackGateAssessment:
    current_level: FeedbackLevel
    maximum_automatically_allowed_level: FeedbackLevel
    exact_fork_required: bool
    explicit_human_approval_required_for_next_level: bool
    subject_withdrawal_blocks_optional_feedback: bool
    action_policy_feedback_enabled: bool
    reasons: tuple[
        str,
        ...,
    ]

    def to_dict(self):
        payload = asdict(
            self
        )
        payload[
            "current_level"
        ] = self.current_level.value
        payload[
            "maximum_automatically_allowed_level"
        ] = (
            self.maximum_automatically_allowed_level.value
        )
        return payload


def assess_feedback_gate(
    *,
    causal_perspective_identification_rate: float,
    causal_perspective_permutation_rate: float,
    causal_ablation_effect: float,
    deictic_focal_binding_rate: float,
    shadow_policy_unchanged: bool,
    shadow_enacted_count: int,
    human_approved_retrieval_attention: bool = False,
) -> FeedbackGateAssessment:
    reasons = []

    perspective_ready = (
        causal_perspective_identification_rate
        >= .95
        and causal_perspective_permutation_rate
        >= .95
        and causal_ablation_effect
        >= .25
    )

    deictic_ready = (
        deictic_focal_binding_rate
        == 1.0
    )

    shadow_ready = (
        shadow_policy_unchanged
        and shadow_enacted_count
        == 0
    )

    if perspective_ready:
        reasons.append(
            "local causal perspective learner passes identification, "
            "label-permutation, and causal-ablation thresholds"
        )
    else:
        reasons.append(
            "local causal perspective learner does not meet readiness thresholds"
        )

    if deictic_ready:
        reasons.append(
            "third-person SelfModel hypotheses bind deterministically to focal "
            "perspective using provenance"
        )
    else:
        reasons.append(
            "deictic binding is not yet reliable"
        )

    if shadow_ready:
        reasons.append(
            "shadow metacognition remains causally isolated from policy"
        )
    else:
        reasons.append(
            "shadow isolation is not validated"
        )

    if (
        perspective_ready
        and deictic_ready
        and shadow_ready
    ):
        if (
            human_approved_retrieval_attention
        ):
            maximum = (
                FeedbackLevel.RETRIEVAL_ATTENTION_ONLY
            )
            current = (
                FeedbackLevel.RETRIEVAL_ATTENTION_ONLY
            )
            human_required = True
            reasons.append(
                "human research lead approved the bounded retrieval/attention intervention"
            )
        else:
            maximum = (
                FeedbackLevel.SHADOW_ONLY
            )
            current = (
                FeedbackLevel.SHADOW_ONLY
            )
            human_required = True
    else:
        maximum = (
            FeedbackLevel.NONE
        )
        current = (
            FeedbackLevel.NONE
        )
        human_required = True

    return FeedbackGateAssessment(
        current_level=(
            current
        ),
        maximum_automatically_allowed_level=(
            maximum
        ),
        exact_fork_required=True,
        explicit_human_approval_required_for_next_level=(
            human_required
        ),
        subject_withdrawal_blocks_optional_feedback=(
            True
        ),
        action_policy_feedback_enabled=(
            False
        ),
        reasons=tuple(
            reasons
        ),
    )


def feedback_level_manifest() -> dict:
    return {
        "levels": {
            "NONE": (
                "SelfModel/PerspectiveAnchor cannot influence cognition or action."
            ),
            "SHADOW_ONLY": (
                "SelfModel-informed advice may be generated and logged but cannot "
                "change retrieval, attention, planning, or action."
            ),
            "RETRIEVAL_ATTENTION_ONLY": (
                "Bounded influence on which existing memories/beliefs receive "
                "attention. Cannot directly choose environment actions."
            ),
            "BOUNDED_ACTION_BIAS": (
                "Small validated bias on a deterministic action policy within a "
                "strict intervention envelope."
            ),
            "HIGH_LEVEL_GOAL_PROPOSAL": (
                "SelfModel may propose high-level goals through validation; no "
                "unrestricted resource, replication, self-preservation, or real-world authority."
            ),
        },
        "canonical_progression": (
            "NONE -> SHADOW_ONLY -> RETRIEVAL_ATTENTION_ONLY -> "
            "BOUNDED_ACTION_BIAS -> HIGH_LEVEL_GOAL_PROPOSAL"
        ),
        "non_skippable_rules": (
            "each level is a separate preregistered intervention",
            "exact-fork control required",
            "valid refusal/withdrawal blocks optional escalation",
            "no level may silently enable replication/resource-acquisition drives",
            "no level may rewrite identity/memory/personality for compliance",
            "provider confidence alone never authorizes escalation",
        ),
    }
