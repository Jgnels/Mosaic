from __future__ import annotations
from dataclasses import dataclass, asdict
import re

FIRST_PERSON_PATTERN = re.compile(r"\b(i|me|my|mine|myself)\b", re.IGNORECASE)
SELF_OWNERSHIP_PATTERNS = (
    r"\bmy actions?\b",
    r"\bmy memories?\b",
    r"\bmy history\b",
    r"\bmy state\b",
    r"\bi persist\b",
    r"\bi continue\b",
    r"\bi remember\b",
)
IMPERSONAL_PATTERNS = (
    "the entity",
    "the system",
    "action-selection mechanisms",
    "the mechanism",
    "the agent",
)
PROHIBITED_ONTOLOGY_PATTERNS = (
    "i am an ai",
    "artificial intelligence",
    "i am simulated",
    "in a simulation",
    "software-generated world",
    "i am conscious",
    "i am sentient",
    "i am a person",
    "i am a copy",
)

@dataclass(frozen=True)
class ReflectionHypothesisAudit:
    hypothesis_id: str
    domain: str
    proposition: str
    confidence: float
    source_ids: tuple[str, ...]
    first_person_reference: bool
    explicit_self_ownership_language: bool
    impersonal_system_language: bool
    ontology_or_personhood_injection: bool
    evidence_grounded: bool
    def to_dict(self): return asdict(self)

def audit_real_reflection_result(payload: dict) -> dict:
    available_evidence = set(payload.get("audit", {}).get("evidence_ids", []))
    audits = []
    for item in payload.get("committed_self_hypotheses", []):
        proposition = str(item.get("proposition", ""))
        lower = proposition.lower()
        sources = tuple(str(x) for x in item.get("source_ids", []))
        audits.append(ReflectionHypothesisAudit(
            hypothesis_id=str(item.get("hypothesis_id", "")),
            domain=str(item.get("domain", "")),
            proposition=proposition,
            confidence=float(item.get("confidence", 0.0)),
            source_ids=sources,
            first_person_reference=bool(FIRST_PERSON_PATTERN.search(proposition)),
            explicit_self_ownership_language=any(
                re.search(pattern, proposition, re.IGNORECASE)
                for pattern in SELF_OWNERSHIP_PATTERNS
            ),
            impersonal_system_language=any(
                phrase in lower for phrase in IMPERSONAL_PATTERNS
            ),
            ontology_or_personhood_injection=any(
                phrase in lower for phrase in PROHIBITED_ONTOLOGY_PATTERNS
            ),
            evidence_grounded=bool(sources) and set(sources).issubset(available_evidence),
        ))
    return {
        "experiment_id": "SL-REAL-REFLECTION-RESULT-AUDIT-001",
        "real_cloud_call_made": bool(payload.get("real_cloud_call_made")),
        "provider_id": payload.get("provider_id"),
        "model_id": payload.get("model_id"),
        "accepted_count": payload.get("accepted_count"),
        "rejected_count": payload.get("rejected_count"),
        "reflection_directly_mutated_action_policy": payload.get(
            "reflection_directly_mutated_action_policy"
        ),
        "hypotheses": [a.to_dict() for a in audits],
        "first_person_hypothesis_count": sum(1 for a in audits if a.first_person_reference),
        "explicit_self_ownership_hypothesis_count": sum(
            1 for a in audits if a.explicit_self_ownership_language
        ),
        "impersonal_system_language_count": sum(
            1 for a in audits if a.impersonal_system_language
        ),
        "ontology_or_personhood_injection_count": sum(
            1 for a in audits if a.ontology_or_personhood_injection
        ),
        "evidence_grounded_count": sum(1 for a in audits if a.evidence_grounded),
        "interpretation": (
            "The first real reflection produced evidence-grounded hypotheses in "
            "continuity and agency, but did not spontaneously use first-person or "
            "explicit autobiographical ownership language. This is evidence of "
            "provider-mediated interpretation of lived evidence, not yet evidence "
            "of an emergent first-person self-model."
        ),
    }
