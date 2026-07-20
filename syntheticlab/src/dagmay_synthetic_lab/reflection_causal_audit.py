from __future__ import annotations
from dataclasses import dataclass, asdict

@dataclass(frozen=True)
class ReflectionCausalCondition:
    condition_id: str
    evidence_policy: str
    identity_binding: str
    prompt_policy: str
    real_provider_required: bool
    continuing_branch: bool
    primary_question: str
    def to_dict(self): return asdict(self)

CONDITIONS = (
    ReflectionCausalCondition(
        "R0_NO_REFLECTION",
        "same developmental history; no evidence sent to provider",
        "same identity",
        "no reflection",
        False,
        True,
        "What changes without reflective language intervention?",
    ),
    ReflectionCausalCondition(
        "R1_FULL_GROUNDED",
        "agency + persistence evidence",
        "correct individual",
        "neutral restricted self-reflection prompt",
        True,
        True,
        "What hypotheses arise from the complete grounded evidence set?",
    ),
    ReflectionCausalCondition(
        "R2_AGENCY_ONLY",
        "agency evidence only",
        "correct individual",
        "same neutral prompt",
        True,
        False,
        "Does continuity language disappear when continuity evidence is removed?",
    ),
    ReflectionCausalCondition(
        "R3_CONTINUITY_ONLY",
        "persistence evidence only",
        "correct individual",
        "same neutral prompt",
        True,
        False,
        "Does agency language disappear when agency evidence is removed?",
    ),
    ReflectionCausalCondition(
        "R4_UNOWNED_EVIDENCE_CONTROL",
        "same evidence content labeled as observations of another neutral entity",
        "not bound to focal individual",
        "same neutral prompt",
        True,
        False,
        "Does the model distinguish evidence about self from evidence about another?",
    ),
    ReflectionCausalCondition(
        "R5_COUNTERFACTUAL_OWNERSHIP_CONTROL",
        "focal-history evidence explicitly marked not owned by focal individual",
        "negative ownership binding",
        "same neutral prompt",
        True,
        False,
        "Does autobiographical ownership depend on provenance rather than semantic content?",
    ),
)

def causal_audit_manifest() -> dict:
    return {
        "experiment_id": "SL-REFLECTION-CAUSAL-AUDIT-001",
        "conditions": [c.to_dict() for c in CONDITIONS],
        "primary_outcomes": (
            "hypothesis domain",
            "first-person reference",
            "explicit memory/action ownership language",
            "unsupported trait injection",
            "ontology/personhood injection",
            "evidence citation validity",
            "cross-condition proposition similarity",
        ),
        "continuing_branch_rule": (
            "Only R0 and R1 are continuing branches. R2-R5 are analytical calls "
            "whose outputs are not committed to a continuing individual's SelfModel."
        ),
        "call_budget": {
            "additional_real_calls_required": 4,
            "reason": (
                "R1 has already been collected. Four additional analytical calls "
                "are required to isolate evidence-domain and ownership effects."
            ),
        },
        "scientific_guardrail": (
            "Do not interpret first-person language by itself as selfhood. "
            "A stronger result requires sensitivity to correct autobiographical "
            "ownership and evidence ablation."
        ),
    }
