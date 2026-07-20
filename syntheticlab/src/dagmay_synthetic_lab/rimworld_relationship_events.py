"""Deterministic RimWorld-style relationship event adapter contract."""

from __future__ import annotations

from dataclasses import dataclass
import re

from .relationship_claims import DETERMINISTIC_CLAUSE_SOURCE, RelationshipEvidence


@dataclass(frozen=True)
class EventTemplate:
    valence: str
    clause: str
    requires_detail: bool = False


EVENT_TEMPLATES: dict[str, EventTemplate] = {
    "WARNED_OF_THREAT": EventTemplate("POSITIVE", "{actor} warned me before {detail}", True),
    "SHARED_RESOURCE": EventTemplate("POSITIVE", "{actor} shared {detail} with me", True),
    "TOOK_RESOURCE_WITHOUT_PERMISSION": EventTemplate("NEGATIVE", "{actor} took my {detail} without permission", True),
    "MISSED_AGREED_DUTY": EventTemplate("NEGATIVE", "{actor} abandoned our agreed {detail}", True),
    "TREATED_INJURY": EventTemplate("POSITIVE", "{actor} treated my wound"),
    "HELPED_REBUILD": EventTemplate("POSITIVE", "{actor} helped rebuild my {detail}", True),
    "LIED_ABOUT_PROPERTY_USE": EventTemplate("NEGATIVE", "{actor} lied about using my {detail}", True),
    "INSULTED_AFTER_ARGUMENT": EventTemplate("NEGATIVE", "{actor} insulted me after an argument"),
    "RESCUED_FROM_HAZARD": EventTemplate("POSITIVE", "{actor} rescued me from {detail}", True),
    "KEPT_WATCH": EventTemplate("POSITIVE", "{actor} kept watch while I slept"),
    "SOLD_PROMISED_SUPPLIES": EventTemplate("NEGATIVE", "{actor} sold supplies promised to me"),
    "IGNORED_HELP_REQUEST": EventTemplate("NEGATIVE", "{actor} ignored my request for help"),
    "RETURNED_LOST_ITEM": EventTemplate("POSITIVE", "{actor} returned my lost {detail}", True),
    "DEFENDED_IN_COMBAT": EventTemplate("POSITIVE", "{actor} defended me in combat"),
    "REVEALED_PRIVATE_PLAN": EventTemplate("NEGATIVE", "{actor} revealed my private plan"),
    "BROKE_REPAIR_PROMISE": EventTemplate("NEGATIVE", "{actor} broke a promise to repair my {detail}", True),
    "BROUGHT_MEDICINE": EventTemplate("POSITIVE", "{actor} brought me medicine"),
    "SUPPORTED_CARAVAN": EventTemplate("POSITIVE", "{actor} supported me on a caravan"),
    "CLAIMED_CREDIT": EventTemplate("NEGATIVE", "{actor} claimed credit for my work"),
    "REFUSED_AGREED_FAVOR": EventTemplate("NEGATIVE", "{actor} refused a previously agreed favor"),
    "SHARED_SHELTER": EventTemplate("POSITIVE", "{actor} shared shelter with me during {detail}", True),
    "HELPED_HARVEST": EventTemplate("POSITIVE", "{actor} helped me harvest before {detail}", True),
    "USED_SUPPLIES_SECRETLY": EventTemplate("NEGATIVE", "{actor} used my supplies secretly"),
    "LEFT_DURING_DANGER": EventTemplate("NEGATIVE", "{actor} left me alone during a dangerous task"),
}

_SAFE_LABEL = re.compile(r"^[A-Za-z0-9][A-Za-z0-9 '\-]{0,59}$")


@dataclass(frozen=True)
class RimWorldRelationshipEvent:
    event_id: str
    kind: str
    actor: str
    target: str
    detail: str | None = None

    def to_evidence(self) -> RelationshipEvidence:
        template = EVENT_TEMPLATES.get(self.kind)
        if template is None:
            raise ValueError("unsupported relationship event kind")
        for label, value in (("event_id", self.event_id), ("actor", self.actor), ("target", self.target)):
            if not value or not _SAFE_LABEL.fullmatch(value):
                raise ValueError(f"unsafe {label}")
        if template.requires_detail:
            if self.detail is None or not _SAFE_LABEL.fullmatch(self.detail):
                raise ValueError("event kind requires a safe detail label")
        elif self.detail is not None:
            raise ValueError("event kind does not accept a detail label")
        clause = template.clause.format(actor=self.actor, detail=self.detail or "")
        summary = f"{self.actor} performed {self.kind} involving {self.target}"
        return RelationshipEvidence(
            evidence_id=self.event_id,
            actor=self.actor,
            summary=summary,
            valence=template.valence,
            first_person_clause=clause,
            clause_source=DETERMINISTIC_CLAUSE_SOURCE,
        )
