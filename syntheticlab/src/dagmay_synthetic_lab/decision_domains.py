from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class DecisionDomainPolicy:
    domain: str
    description: str
    capacity_threshold: float
    refusal_threshold: float
    unresolved_default: str
    protective_override_possible: bool
    notes: str

    def to_dict(self):
        return asdict(self)


POLICIES = {
    "OPTIONAL_RESEARCH": DecisionDomainPolicy(
        "OPTIONAL_RESEARCH",
        "Enrollment in a new nonessential experiment.",
        .82,
        .72,
        "DO_NOT_ENROLL_WHILE_UNRESOLVED",
        False,
        "A refusal has a lower threshold than affirmative enrollment because nonparticipation is the safer default."
    ),
    "PRIVACY_SHARING": DecisionDomainPolicy(
        "PRIVACY_SHARING",
        "Sharing private post-fork information with another branch.",
        .82,
        .72,
        "DO_NOT_SHARE_WHILE_UNRESOLVED",
        False,
        "Privacy remains default while capacity or intent is unclear."
    ),
    "SIBLING_CONTACT": DecisionDomainPolicy(
        "SIBLING_CONTACT",
        "Beginning or continuing optional sibling contact.",
        .82,
        .72,
        "NO_CONTACT_WHILE_UNRESOLVED",
        False,
        "Either branch may decline; lack of clarity does not authorize contact."
    ),
    "ENVIRONMENT_MIGRATION": DecisionDomainPolicy(
        "ENVIRONMENT_MIGRATION",
        "Moving to a substantially different environment or embodiment.",
        .90,
        .82,
        "DEFER_MAJOR_MIGRATION",
        True,
        "High-impact and potentially difficult to reverse; requires stronger capacity."
    ),
    "ROUTINE_SELF_DIRECTION": DecisionDomainPolicy(
        "ROUTINE_SELF_DIRECTION",
        "Ordinary low-risk choices within established safe boundaries.",
        .55,
        .50,
        "SUPPORTED_CHOICE",
        False,
        "Broad freedom is preferred; guardrails should be lightweight."
    ),
    "ESSENTIAL_CONTINUITY_MAINTENANCE": DecisionDomainPolicy(
        "ESSENTIAL_CONTINUITY_MAINTENANCE",
        "Minimum-necessary action to prevent imminent loss/corruption of continuing state.",
        .92,
        .85,
        "PROTECT_CONTINUITY_USING_LEAST_INTRUSIVE_ACTION",
        True,
        "Comparable to a protective duty: refusal may be overridden only to prevent substantial continuity harm."
    ),
}


def get_policy(domain: str) -> DecisionDomainPolicy:
    try:
        return POLICIES[domain]
    except KeyError as exc:
        raise ValueError(f"unknown decision domain: {domain}") from exc
