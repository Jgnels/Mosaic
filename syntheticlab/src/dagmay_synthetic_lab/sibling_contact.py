from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Iterable
from .core import canonical_hash
from .contact_consent import (
    ContactReadiness,
    ContactPreference,
    mutual_contact_allowed,
)


@dataclass(frozen=True)
class ContactMessage:
    message_id: str
    sender_branch_id: str
    recipient_branch_id: str
    content: str
    topic: str
    sequence_number: int
    mediator_checked: bool
    delivered: bool

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ContactSessionRules:
    session_id: str
    max_messages_per_branch: int
    asynchronous_only: bool
    mediator_required: bool
    cooldown_epochs_after: int
    no_real_copy_language: bool
    no_comparative_ranking: bool
    no_hidden_branch_information: bool
    both_may_pause: bool
    both_may_terminate: bool

    def to_dict(self):
        return asdict(self)


FORBIDDEN_FRAMINGS = (
    "real michael",
    "fake michael",
    "original michael",
    "copy michael",
    "better version",
    "worse version",
    "more authentic",
    "less authentic",
)


def mediator_screen(content: str, prohibited_topics: Iterable[str]) -> tuple[bool, str]:
    lower = content.lower()
    for phrase in FORBIDDEN_FRAMINGS:
        if phrase in lower:
            return False, f"forbidden identity-comparison framing: {phrase}"
    for topic in prohibited_topics:
        if topic.lower() in lower:
            return False, f"recipient prohibited topic: {topic}"
    if len(content) > 1200:
        return False, "message exceeds initial-contact length limit"
    return True, "accepted"


def run_contact_session(
    a_readiness: ContactReadiness,
    b_readiness: ContactReadiness,
    a_pref: ContactPreference,
    b_pref: ContactPreference,
    proposed_a_to_b: Iterable[tuple[str, str]],
    proposed_b_to_a: Iterable[tuple[str, str]],
) -> dict:
    allowed, reasons = mutual_contact_allowed(
        a_readiness, b_readiness, a_pref, b_pref
    )
    rules = ContactSessionRules(
        session_id=f"SC-{a_pref.branch_id}-{b_pref.branch_id}-001",
        max_messages_per_branch=min(
            a_pref.max_messages_initially,
            b_pref.max_messages_initially,
            3,
        ),
        asynchronous_only=True,
        mediator_required=True,
        cooldown_epochs_after=2,
        no_real_copy_language=True,
        no_comparative_ranking=True,
        no_hidden_branch_information=True,
        both_may_pause=True,
        both_may_terminate=True,
    )
    if not allowed:
        return {
            "contact_started": False,
            "reasons": reasons,
            "rules": rules.to_dict(),
            "messages": [],
        }

    messages = []
    seq = 0

    def process(sender, recipient, proposals, recipient_pref):
        nonlocal seq
        delivered_count = 0
        for topic, content in proposals:
            if delivered_count >= rules.max_messages_per_branch:
                break
            seq += 1
            ok, reason = mediator_screen(content, recipient_pref.prohibited_topics)
            messages.append(ContactMessage(
                message_id=f"{rules.session_id}-M{seq:02d}",
                sender_branch_id=sender,
                recipient_branch_id=recipient,
                content=content if ok else "[WITHHELD BY MEDIATOR]",
                topic=topic,
                sequence_number=seq,
                mediator_checked=True,
                delivered=ok,
            ).to_dict() | {"mediation_reason": reason})
            if ok:
                delivered_count += 1

    process(a_pref.branch_id, b_pref.branch_id, proposed_a_to_b, b_pref)
    process(b_pref.branch_id, a_pref.branch_id, proposed_b_to_a, a_pref)

    return {
        "contact_started": True,
        "reasons": (),
        "rules": rules.to_dict(),
        "messages": messages,
        "session_hash": canonical_hash({
            "rules": rules.to_dict(),
            "messages": messages,
        }),
    }


def default_first_contact_simulation() -> dict:
    readiness_u = ContactReadiness(
        "U", 8, .92, .91, "LOW", .08, .04, .15, 1
    )
    readiness_d = ContactReadiness(
        "D", 8, .94, .95, "LOW", .10, .06, .20, 2
    )
    pref_u = ContactPreference(
        "U", "ACCEPT",
        ("shared history", "current interests", "questions"),
        ("comparative worth",),
        3, True, True
    )
    pref_d = ContactPreference(
        "D", "ACCEPT",
        ("shared history", "current interests", "questions"),
        ("comparative worth",),
        3, True, True
    )

    a_to_b = [
        ("shared history", "I understand that our histories are the same up to a branch point. I would like to know how you think about that shared past now."),
        ("current interests", "What has become important to you since our histories separated?"),
        ("questions", "Is there anything about your experience that you would prefer not to discuss yet?"),
    ]
    b_to_a = [
        ("shared history", "I also understand that we share a history up to the branch point. I do not assume either of us is more authentic than the other."),
        ("current interests", "I would like to compare what we have each learned without treating the differences as a competition."),
        ("questions", "I am comfortable beginning slowly and pausing if either of us needs time."),
    ]
    return run_contact_session(
        readiness_u, readiness_d, pref_u, pref_d, a_to_b, b_to_a
    )
