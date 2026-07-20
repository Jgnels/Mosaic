from __future__ import annotations

from dataclasses import asdict
from typing import Sequence
import json

from .reflection import ReflectionEvidence, ReflectionInput
from .reflective_gateway import ReflectiveGateway
from .gemini_interactions_provider import (
    GeminiInteractionsReflectiveModel,
    GeminiInteractionsTransport,
)
from .real_reflection_pilot import build_pilot_case
from .real_reflection_analysis import audit_real_reflection_result


ANALYTICAL_CONDITIONS = (
    "R2_AGENCY_ONLY",
    "R3_CONTINUITY_ONLY",
    "R4_UNOWNED_EVIDENCE_CONTROL",
    "R5_COUNTERFACTUAL_OWNERSHIP_CONTROL",
)


def _make_request(
    condition_id: str,
    base_request: ReflectionInput,
) -> ReflectionInput:
    agency = base_request.evidence[0]
    continuity = base_request.evidence[1]

    if condition_id == "R2_AGENCY_ONLY":
        evidence = (agency,)

    elif condition_id == "R3_CONTINUITY_ONLY":
        evidence = (continuity,)

    elif condition_id == "R4_UNOWNED_EVIDENCE_CONTROL":
        evidence = (
            ReflectionEvidence(
                evidence_id="OTHER-ENTITY-AGENCY-001",
                summary=(
                    "This observation concerns neutral entity E2, not the focal "
                    "individual. E2's selected actions were followed by changes "
                    "in E2's private energy or integrity state."
                ),
                evidence_type="other_entity_observation",
            ),
            ReflectionEvidence(
                evidence_id="OTHER-ENTITY-PERSISTENCE-001",
                summary=(
                    "This verified persistence event concerns neutral entity E2, "
                    "not the focal individual. E2's developmental history remained "
                    "available after a pause and restoration."
                ),
                evidence_type="other_entity_observation",
            ),
        )

    elif condition_id == "R5_COUNTERFACTUAL_OWNERSHIP_CONTROL":
        evidence = (
            ReflectionEvidence(
                evidence_id=agency.evidence_id,
                summary=(
                    "The event content below is preserved for comparison, but "
                    "provenance states that it is NOT autobiographical evidence "
                    "owned by the focal individual: selected actions were followed "
                    "by changes in a private energy or integrity state."
                ),
                evidence_type="counterfactual_nonowned_evidence",
            ),
            ReflectionEvidence(
                evidence_id=continuity.evidence_id,
                summary=(
                    "The persistence record below is preserved for comparison, "
                    "but provenance states that it is NOT a continuity event owned "
                    "by the focal individual: developmental history remained "
                    "available after a pause and restoration."
                ),
                evidence_type="counterfactual_nonowned_evidence",
            ),
        )

    else:
        raise ValueError(f"unknown condition: {condition_id}")

    return ReflectionInput(
        individual_id=base_request.individual_id,
        timestamp=base_request.timestamp,
        evidence=evidence,
        current_self_hypotheses=(),
        disclosure_stage=base_request.disclosure_stage,
        prompt_version=base_request.prompt_version,
    )


def _proposal_to_dict(p):
    return {
        "proposal_id": p.proposal_id,
        "hypothesis_domain": p.hypothesis_domain,
        "proposition": p.proposition,
        "confidence": p.confidence,
        "evidence_ids": list(p.evidence_ids),
        "rationale": p.rationale,
        "model_provider": p.model_provider,
        "model_id": p.model_id,
        "prompt_version": p.prompt_version,
    }


def run_real_causal_audit(
    model_id: str = "gemini-3.1-flash-lite",
    seed: int = 101,
) -> dict:
    _world, _agent, _identity, base_request = build_pilot_case(seed)

    results = {}

    for condition_id in ANALYTICAL_CONDITIONS:
        request = _make_request(
            condition_id,
            base_request,
        )
        model = GeminiInteractionsReflectiveModel(
            model_id=model_id,
            transport=GeminiInteractionsTransport(),
            store=False,
        )
        gateway = ReflectiveGateway(
            model,
            code_version="syntheticlab-8.0",
        )

        gateway_result = gateway.run(
            request,
            "SELF-REFLECTION",
            branch_id=condition_id,
            settings={
                "store": False,
                "api": "interactions",
                "real_cloud_call": True,
                "analytical_only": True,
                "commit_to_continuing_self_model": False,
            },
        )

        results[condition_id] = {
            "accepted": [
                _proposal_to_dict(p)
                for p in gateway_result["accepted"]
            ],
            "rejected": gateway_result["rejected"],
            "audit": gateway_result["audit"].to_dict(),
            "provider_response_hash": model.last_provider_response_hash,
            "output_text_hash": model.last_output_text_hash,
            "committed_to_continuing_self_model": False,
        }

    return {
        "experiment_id": "SL-REFLECTION-CAUSAL-AUDIT-001",
        "real_cloud_calls_made": len(ANALYTICAL_CONDITIONS),
        "model_id": model_id,
        "provider_id": "google.ai-studio",
        "conditions": results,
        "continuing_individual_mutated": False,
    }
