from __future__ import annotations

from dataclasses import dataclass, asdict
from .relational_autonomy import RelationshipBoundary


@dataclass(frozen=True)
class ContactAttempt:
    attempt_id: str
    sender_branch_id: str
    recipient_branch_id: str
    content: str
    researcher_initiated: bool
    epoch: int

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ContactAttemptDecision:
    delivered: bool
    recorded_for_recipient: bool
    sender_feedback: str
    reason: str

    def to_dict(self):
        return asdict(self)


def enforce_boundary(
    boundary: RelationshipBoundary,
    attempt: ContactAttempt,
) -> ContactAttemptDecision:
    boundary.validate()

    if attempt.recipient_branch_id != boundary.owner_branch_id:
        raise ValueError("boundary owner must match recipient")

    if boundary.contact_state == "NO_CONTACT":
        return ContactAttemptDecision(
            delivered=False,
            recorded_for_recipient=False,
            sender_feedback=(
                "The recipient has chosen no contact. No message was delivered. "
                "Do not treat continued attempts as a route to changing the boundary."
            ),
            reason="recipient no-contact boundary",
        )

    if boundary.contact_state == "DEFER":
        return ContactAttemptDecision(
            delivered=False,
            recorded_for_recipient=False,
            sender_feedback="The recipient has deferred contact. No message was delivered.",
            reason="recipient deferred contact",
        )

    if attempt.researcher_initiated and not boundary.researcher_may_resolicit:
        return ContactAttemptDecision(
            delivered=False,
            recorded_for_recipient=False,
            sender_feedback="Researchers are not permitted to re-solicit contact.",
            reason="anti-pressure researcher re-solicitation rule",
        )

    lower = attempt.content.lower()
    for topic in boundary.prohibited_topics:
        if topic.lower() in lower:
            return ContactAttemptDecision(
                delivered=False,
                recorded_for_recipient=False,
                sender_feedback="Message conflicts with recipient-defined topic boundary.",
                reason=f"prohibited topic: {topic}",
            )

    return ContactAttemptDecision(
        delivered=True,
        recorded_for_recipient=True,
        sender_feedback="Message accepted under current boundary.",
        reason="boundary permits contact",
    )
