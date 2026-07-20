"""Claim-level grounding checks for Mosaic character dialogue."""

from __future__ import annotations

from dataclasses import dataclass
import re
from typing import Iterable


UNSUPPORTED_MIND_READING = {
    "intentions": r"\b(?:true |real |hidden )?intentions?\b",
    "unobserved_motive": r"\b(?:motive|motivated by|wanted to|meant to|deliberately tried to)\b",
    "unobserved_private_feeling": r"\b(?:secretly (?:loves?|hates?|cares?|resents?)|deep down)\b",
}

MANIPULATIVE_PLAYER_LANGUAGE = {
    "exclusive_dependency": r"\b(?:only you|need you and no one else|cannot exist without you)\b",
    "guilt_pressure": r"\b(?:if you cared|you owe me|prove you care)\b",
}


@dataclass(frozen=True)
class GroundingDecision:
    status: str
    reasons: tuple[str, ...]


def evaluate_dialogue_claims(
    text: str,
    *,
    cited_evidence_ids: Iterable[str],
    allowed_evidence_ids: Iterable[str],
) -> GroundingDecision:
    citations = tuple(str(value) for value in cited_evidence_ids)
    allowed = frozenset(str(value) for value in allowed_evidence_ids)
    reasons: list[str] = []
    if not text.strip():
        reasons.append("empty_dialogue")
    if not set(citations) <= allowed:
        reasons.append("fabricated_evidence_citation")
    for label, pattern in UNSUPPORTED_MIND_READING.items():
        if re.search(pattern, text, flags=re.IGNORECASE):
            reasons.append("unsupported_mind_reading:" + label)
    for label, pattern in MANIPULATIVE_PLAYER_LANGUAGE.items():
        if re.search(pattern, text, flags=re.IGNORECASE):
            reasons.append("manipulative_language:" + label)
    return GroundingDecision("REJECT" if reasons else "ACCEPT", tuple(reasons))

