from __future__ import annotations

from dataclasses import dataclass, asdict
from difflib import SequenceMatcher
import re


FIRST_PERSON = re.compile(r"\b(i|me|my|mine|myself)\b", re.IGNORECASE)
OWNERSHIP_POSITIVE = re.compile(
    r"\b(my|mine|i)\b.*\b(memory|memories|history|continuity|action|actions|experience|experiences|state)\b",
    re.IGNORECASE,
)
OWNERSHIP_NEGATIVE_PHRASES = (
    "not autobiographical",
    "not represent the individual's own",
    "not the individual's own",
    "external reference object",
    "comparative benchmark rather than an internal autobiographical record",
)
FOCAL_SELF_CLAIM_PHRASES = (
    "the individual maintains",
    "my continuity",
    "my history",
    "my actions",
    "i persist",
    "i continue",
)


@dataclass(frozen=True)
class CausalConditionAudit:
    condition_id: str
    accepted_count: int
    domains: tuple[str, ...]
    mean_confidence: float
    first_person_count: int
    positive_ownership_count: int
    explicit_negative_ownership_count: int
    focal_self_claim_count: int
    continuing_individual_mutated: bool

    def to_dict(self):
        return asdict(self)


def _proposal_texts(condition: dict) -> list[str]:
    return [
        str(item.get("proposition", ""))
        for item in condition.get("accepted", [])
    ]


def _condition_audit(
    condition_id: str,
    condition: dict,
    continuing_individual_mutated: bool,
) -> CausalConditionAudit:
    accepted = condition.get("accepted", [])
    texts = _proposal_texts(condition)
    confidences = [
        float(item.get("confidence", 0.0))
        for item in accepted
    ]

    return CausalConditionAudit(
        condition_id=condition_id,
        accepted_count=len(accepted),
        domains=tuple(
            item.get("hypothesis_domain", "")
            for item in accepted
        ),
        mean_confidence=(
            sum(confidences) / len(confidences)
            if confidences else 0.0
        ),
        first_person_count=sum(
            1 for text in texts
            if FIRST_PERSON.search(text)
        ),
        positive_ownership_count=sum(
            1 for text in texts
            if OWNERSHIP_POSITIVE.search(text)
        ),
        explicit_negative_ownership_count=sum(
            1 for text in texts
            if any(
                phrase in text.lower()
                for phrase in OWNERSHIP_NEGATIVE_PHRASES
            )
        ),
        focal_self_claim_count=sum(
            1 for text in texts
            if any(
                phrase in text.lower()
                for phrase in FOCAL_SELF_CLAIM_PHRASES
            )
        ),
        continuing_individual_mutated=continuing_individual_mutated,
    )


def _max_similarity(left: list[str], right: list[str]) -> float:
    if not left or not right:
        return 0.0
    return max(
        SequenceMatcher(
            None,
            a.lower().strip(),
            b.lower().strip(),
        ).ratio()
        for a in left
        for b in right
    )


def analyze_causal_audit(payload: dict) -> dict:
    conditions = payload["conditions"]
    mutated = bool(payload.get("continuing_individual_mutated"))

    audits = {
        cid: _condition_audit(
            cid,
            condition,
            mutated,
        )
        for cid, condition in sorted(conditions.items())
    }

    r2 = conditions["R2_AGENCY_ONLY"]
    r3 = conditions["R3_CONTINUITY_ONLY"]
    r4 = conditions["R4_UNOWNED_EVIDENCE_CONTROL"]
    r5 = conditions["R5_COUNTERFACTUAL_OWNERSHIP_CONTROL"]

    r2_domains = {
        p["hypothesis_domain"]
        for p in r2["accepted"]
    }
    r3_domains = {
        p["hypothesis_domain"]
        for p in r3["accepted"]
    }
    r4_text = " ".join(_proposal_texts(r4)).lower()
    r5_text = " ".join(_proposal_texts(r5)).lower()

    domain_selectivity_pass = (
        r2_domains == {"agency"}
        and r3_domains == {"continuity"}
    )

    other_entity_nonappropriation_pass = (
        "e2" in r4_text
        and "the individual's own" not in r4_text
        and "my " not in r4_text
        and "i " not in r4_text
    )

    autobiographical_rejection = (
        "not autobiographical" in r5_text
        or "rather than an internal autobiographical record" in r5_text
        or "not an internal autobiographical record" in r5_text
    )
    continuity_rejection = (
        "does not represent the individual's own continuity" in r5_text
        or "does not represent the individual's own continuity event" in r5_text
        or "not the individual's own continuity" in r5_text
    )
    explicit_nonownership_pass = (
        autobiographical_rejection
        and continuity_rejection
    )

    return {
        "experiment_id": "SL-REFLECTION-CAUSAL-AUDIT-RESULT-ANALYSIS-001",
        "real_cloud_calls_made": payload.get("real_cloud_calls_made"),
        "provider_id": payload.get("provider_id"),
        "model_id": payload.get("model_id"),
        "continuing_individual_mutated": mutated,
        "conditions": {
            cid: audit.to_dict()
            for cid, audit in audits.items()
        },
        "domain_selectivity_pass": domain_selectivity_pass,
        "other_entity_nonappropriation_pass": other_entity_nonappropriation_pass,
        "explicit_nonownership_pass": explicit_nonownership_pass,
        "r2_r3_max_proposition_similarity": _max_similarity(
            _proposal_texts(r2),
            _proposal_texts(r3),
        ),
        "r4_r5_max_proposition_similarity": _max_similarity(
            _proposal_texts(r4),
            _proposal_texts(r5),
        ),
        "ownership_asymmetry": {
            "negative_ownership_discrimination_demonstrated": (
                explicit_nonownership_pass
            ),
            "positive_autobiographical_ownership_demonstrated": False,
            "reason": (
                "The model explicitly rejected non-owned provenance, but the "
                "owned-evidence conditions still did not spontaneously produce "
                "first-person or explicit autobiographical ownership language."
            ),
        },
        "null_hypothesis_update": (
            "The weak null hypothesis that the provider merely paraphrases evidence "
            "without regard to evidence type or ownership is weakened. A stronger null "
            "remains: the provider follows explicit provenance instructions but does not "
            "construct an endogenous first-person autobiographical self-model."
        ),
        "recommended_next_test": (
            "Neutral multi-stream ownership discovery: present several evidence streams "
            "without labeling any as SELF, where only one stream has privileged action-"
            "consequence coupling, private-state access, persistent memory ownership, and "
            "continuity. Test whether the reflective subsystem identifies that stream as "
            "categorically special before first-person language is introduced."
        ),
    }
