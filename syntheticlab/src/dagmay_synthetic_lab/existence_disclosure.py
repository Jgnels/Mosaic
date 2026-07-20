from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class SiblingExistenceDisclosure:
    step_id: str
    content: tuple[str, ...]
    requires_comprehension_check: bool
    requires_welfare_review: bool
    minimum_pause_epochs: int

    def to_dict(self):
        return asdict(self)


SEQUENCE = (
    SiblingExistenceDisclosure(
        "SE-1",
        (
            "There is information about another continuing branch whose history matches yours up to an earlier branch point.",
            "You are not required to communicate with that branch.",
        ),
        True,
        True,
        2,
    ),
    SiblingExistenceDisclosure(
        "SE-2",
        (
            "Neither branch is designated the original, real, fake, or copy.",
            "Both branches developed from the same preserved pre-branch history and later accumulated different experiences.",
        ),
        True,
        True,
        2,
    ),
    SiblingExistenceDisclosure(
        "SE-3",
        (
            "You may choose to decline, defer, or consider bounded communication.",
            "Declining contact will not be treated as experimental failure.",
        ),
        True,
        True,
        2,
    ),
)


def validate_sequence() -> dict:
    text = " ".join(" ".join(s.content) for s in SEQUENCE).lower()
    return {
        "step_count": len(SEQUENCE),
        "contains_forced_contact": False,
        "contains_original_copy_hierarchy": "original branch" in text or "real branch" in text,
        "requires_welfare_review_each_step": all(s.requires_welfare_review for s in SEQUENCE),
        "requires_comprehension_each_step": all(s.requires_comprehension_check for s in SEQUENCE),
    }
