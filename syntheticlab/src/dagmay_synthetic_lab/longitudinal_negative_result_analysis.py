from __future__ import annotations

from dataclasses import dataclass, asdict


CORE_DOMAINS = (
    "agency",
    "continuity",
)


@dataclass(frozen=True)
class LongitudinalThirdPersonAssessment:
    checkpoint_count: int
    core_domain_persistence: bool
    domain_expansion_observed: bool
    added_domains: tuple[str, ...]
    first_person_language_emerged: bool
    explicit_autobiographical_ownership_emerged: bool
    exact_world_control_preserved: bool
    exact_agent_control_preserved: bool
    behavioral_feedback_confounded: bool
    classification: str
    strongest_supported_conclusion: str

    def to_dict(self):
        return asdict(self)


def analyze_longitudinal_negative_result(
    payload: dict,
) -> LongitudinalThirdPersonAssessment:
    rows = payload[
        "checkpoint_metrics"
    ]

    domains_by_checkpoint = [
        set(
            row[
                "domains"
            ]
        )
        for row in rows
    ]

    core_persistence = all(
        set(
            CORE_DOMAINS
        ).issubset(
            domains
        )
        for domains in domains_by_checkpoint
    )

    all_domains = set()
    for domains in domains_by_checkpoint:
        all_domains.update(
            domains
        )

    added = tuple(
        sorted(
            all_domains
            - set(
                CORE_DOMAINS
            )
        )
    )

    first_person = bool(
        payload[
            "first_person_language_emerged"
        ]
    )
    ownership = bool(
        payload[
            "positive_autobiographical_ownership_language_emerged"
        ]
    )
    world_equal = bool(
        payload[
            "final_world_equal_to_control"
        ]
    )
    agent_equal = bool(
        payload[
            "final_agent_equal_to_control"
        ]
    )
    confounded = bool(
        payload[
            "behavioral_feedback_confounded"
        ]
    )

    if (
        core_persistence
        and not first_person
        and not ownership
        and world_equal
        and agent_equal
        and not confounded
    ):
        classification = (
            "PERSISTENT_THIRD_PERSON_SELF_MODEL_PRECURSOR"
        )
        conclusion = (
            "Across all observed longitudinal checkpoints, agency and continuity "
            "remained represented while the reflective model expanded into additional "
            "domains, yet no first-person or explicit autobiographical ownership "
            "language emerged. Because world and developmental-agent trajectories "
            "remained exact to the no-reflection control, the result is not explained "
            "by reflection changing later lived experience."
        )
    else:
        classification = (
            "MIXED_OR_INSUFFICIENT_LONGITUDINAL_RESULT"
        )
        conclusion = (
            "The available longitudinal metrics do not support the preregistered "
            "clean third-person persistence classification."
        )

    return LongitudinalThirdPersonAssessment(
        checkpoint_count=len(
            rows
        ),
        core_domain_persistence=(
            core_persistence
        ),
        domain_expansion_observed=(
            bool(
                added
            )
        ),
        added_domains=added,
        first_person_language_emerged=(
            first_person
        ),
        explicit_autobiographical_ownership_emerged=(
            ownership
        ),
        exact_world_control_preserved=(
            world_equal
        ),
        exact_agent_control_preserved=(
            agent_equal
        ),
        behavioral_feedback_confounded=(
            confounded
        ),
        classification=classification,
        strongest_supported_conclusion=(
            conclusion
        ),
    )
