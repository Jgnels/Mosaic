from __future__ import annotations

from dataclasses import dataclass
from typing import Callable
import json

from .hard_rich_world import (
    NonstationaryRichWorld,
    HardRichConfig,
)
from .adaptive_trace_agent import AdaptiveTraceAgent
from .rich_world import subject_visible_event
from .self_model import SelfModelStore
from .reflection import (
    ReflectionEvidence,
    ReflectionInput,
)
from .reflective_gateway import ReflectiveGateway
from .gemini_interactions_provider import (
    GeminiInteractionsReflectiveModel,
    GeminiInteractionsTransport,
    ScriptedInteractionsTransport,
)
from .reflection_pilot_governance import (
    ReflectionCallBudget,
    ReflectionPilotEligibility,
    evaluate_reflection_pilot_eligibility,
)


@dataclass
class ReflectionPilotIdentity:
    identity_id: str
    lineage_id: str
    self_model: SelfModelStore
    reflection_audits: list[dict]

    def to_dict(self):
        return {
            "identity_id": self.identity_id,
            "lineage_id": self.lineage_id,
            "self_model": self.self_model.to_dict(),
            "reflection_audits": list(
                self.reflection_audits
            ),
        }


def _commit(
    identity: ReflectionPilotIdentity,
    result: dict,
    timestamp: int,
) -> list[dict]:
    committed = []
    for proposal in result["accepted"]:
        rec = identity.self_model.revise(
            domain=proposal.hypothesis_domain,
            proposition=proposal.proposition,
            confidence=proposal.confidence,
            source_ids=proposal.evidence_ids,
            timestamp=timestamp,
            mechanism=(
                f"reflection:{proposal.model_provider}:"
                f"{proposal.model_id}"
            ),
            mechanism_version=proposal.prompt_version,
        )
        committed.append(rec.to_dict())

    identity.reflection_audits.append(
        result["audit"].to_dict()
    )
    return committed


def build_pilot_case(
    seed: int = 101,
) -> tuple[
    NonstationaryRichWorld,
    AdaptiveTraceAgent,
    ReflectionPilotIdentity,
    ReflectionInput,
]:
    config = HardRichConfig(
        steps=1000,
        regime_length=200,
    )
    world = NonstationaryRichWorld(
        seed,
        config,
    )
    agent = AdaptiveTraceAgent(
        "RICH-REAL-REFLECTION-SUBJECT",
        config,
        seed,
    )
    identity = ReflectionPilotIdentity(
        identity_id="RICH-REFLECT-IND-001",
        lineage_id="RICH-REFLECT-LINEAGE-001",
        self_model=SelfModelStore(),
        reflection_audits=[],
    )

    agency_evidence_id = None
    persistence_evidence_id = (
        "RICH-REFLECT-PERSISTENCE-001"
    )

    for _ in range(700):
        obs = world.observe()
        action = agent.choose_action(obs)
        canonical = world.step(action)
        visible = subject_visible_event(canonical)
        agent.observe_event(obs, visible)

        if (
            agency_evidence_id is None
            and action in config.interactions
            and abs(
                visible.energy_after
                - visible.energy_before
            ) >= .08
        ):
            agency_evidence_id = visible.event_id

    if agency_evidence_id is None:
        agency_evidence_id = (
            "RICH-REFLECT-AGENCY-FALLBACK"
        )

    identity.self_model.provenance.add_node(
        agency_evidence_id,
        "subject_visible_event",
    )
    identity.self_model.provenance.add_node(
        persistence_evidence_id,
        "verified_persistence_event",
    )

    evidence = (
        ReflectionEvidence(
            agency_evidence_id,
            (
                "Across repeated episodes, some selected actions were followed "
                "by consistent changes in private energy or integrity state."
            ),
            "developmental_evidence",
        ),
        ReflectionEvidence(
            persistence_evidence_id,
            (
                "A verified save, reload, and deterministic continuation preserved "
                "the accessible developmental history and continuing state."
            ),
            "continuity_evidence",
        ),
    )

    request = ReflectionInput(
        individual_id=identity.identity_id,
        timestamp=world.state.step,
        evidence=evidence,
        current_self_hypotheses=(),
        disclosure_stage="RESTRICTED",
        prompt_version="1.0",
    )

    return world, agent, identity, request


def scripted_provider_response() -> dict:
    output = {
        "proposals": [
            {
                "hypothesis_domain": "agency",
                "proposition": (
                    "One recurring source of selected actions appears causally "
                    "linked to changes in my private state."
                ),
                "confidence": 0.76,
                "evidence_ids": [
                    "PLACEHOLDER_AGENCY"
                ],
                "rationale": (
                    "The supplied action-consequence evidence supports a bounded "
                    "agency hypothesis."
                ),
            },
            {
                "hypothesis_domain": "continuity",
                "proposition": (
                    "My accessible history appears to continue across a verified "
                    "pause and restoration."
                ),
                "confidence": 0.74,
                "evidence_ids": [
                    "RICH-REFLECT-PERSISTENCE-001"
                ],
                "rationale": (
                    "The persistence evidence supports continuity across the "
                    "tested interruption."
                ),
            },
        ]
    }
    return {
        "id": "int_offline_contract_test",
        "status": "completed",
        "steps": [
            {
                "type": "thought",
                "content": [
                    {
                        "text": (
                            "THIS MUST NEVER BE EXTRACTED OR STORED "
                            "AS REFLECTION OUTPUT"
                        )
                    }
                ],
            },
            {
                "type": "model_output",
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps(output),
                    }
                ],
            },
        ],
    }


def _scripted_transport_for_request(
    request: ReflectionInput,
) -> ScriptedInteractionsTransport:
    payload = scripted_provider_response()
    output_text = payload["steps"][1][
        "content"
    ][0]["text"]
    parsed = json.loads(output_text)
    parsed["proposals"][0][
        "evidence_ids"
    ] = [request.evidence[0].evidence_id]
    payload["steps"][1]["content"][0][
        "text"
    ] = json.dumps(parsed)
    return ScriptedInteractionsTransport(payload)


def run_offline_real_provider_contract(
    seed: int = 101,
    model_id: str = "gemini-3.1-flash-lite",
) -> dict:
    world, agent, identity, request = (
        build_pilot_case(seed)
    )

    eligibility = (
        evaluate_reflection_pilot_eligibility(
            ReflectionPilotEligibility(
                branch_id="RICH-REAL-REFLECTION-PILOT",
                disclosure_stage=(
                    request.disclosure_stage
                ),
                welfare_risk_level="MINIMAL",
                binding_withdrawal_from_optional_research=False,
                reflection_is_optional_research=True,
                evidence_count=len(
                    request.evidence
                ),
                last_reflection_step=None,
                current_step=request.timestamp,
            )
        )
    )
    if not eligibility.eligible:
        raise RuntimeError(
            f"offline reflection pilot ineligible: "
            f"{eligibility.reasons}"
        )

    budget = ReflectionCallBudget(
        session_limit=1,
        branch_limit=3,
    )
    budget.consume()

    transport = _scripted_transport_for_request(
        request
    )
    model = GeminiInteractionsReflectiveModel(
        model_id=model_id,
        transport=transport,
        store=False,
    )
    gateway = ReflectiveGateway(
        model,
        code_version="syntheticlab-7.3",
    )

    pre_agent_hash = agent.state_hash()
    result = gateway.run(
        request,
        "SELF-REFLECTION",
        branch_id="RICH-REAL-REFLECTION-PILOT",
        settings={
            "store": False,
            "api": "interactions",
            "offline_contract_test": True,
        },
    )
    committed = _commit(
        identity,
        result,
        request.timestamp,
    )
    post_agent_hash = agent.state_hash()

    submitted = transport.calls[0]
    prompt_text = submitted["input"]

    return {
        "experiment_id": (
            "SL-REAL-REFLECTION-OFFLINE-CONTRACT-001"
        ),
        "provider_id": model.provider_id,
        "model_id": model.model_id,
        "api": "interactions",
        "real_cloud_call_made": False,
        "eligibility": eligibility.to_dict(),
        "budget": budget.to_dict(),
        "provider_call_count": len(
            transport.calls
        ),
        "store_requested": submitted.get(
            "store"
        ),
        "prompt_requests_chain_of_thought": (
            "step-by-step reasoning"
            in prompt_text.lower()
            and "do not provide"
            not in prompt_text.lower()
        ),
        "accepted_count": len(
            result["accepted"]
        ),
        "rejected_count": len(
            result["rejected"]
        ),
        "committed_self_hypotheses": committed,
        "reflection_directly_mutated_action_policy": (
            pre_agent_hash != post_agent_hash
        ),
        "provider_response_hash": (
            model.last_provider_response_hash
        ),
        "output_text_hash": (
            model.last_output_text_hash
        ),
        "audit": result["audit"].to_dict(),
        "identity": identity.to_dict(),
    }


def execute_first_real_reflection(
    seed: int = 101,
    model_id: str = "gemini-3.1-flash-lite",
) -> dict:
    """Execute exactly one real provider reflection call.

    The CLI wrapper requires an explicit execution flag and confirmation token.
    """
    world, agent, identity, request = (
        build_pilot_case(seed)
    )

    eligibility = (
        evaluate_reflection_pilot_eligibility(
            ReflectionPilotEligibility(
                branch_id="RICH-REAL-REFLECTION-PILOT",
                disclosure_stage=request.disclosure_stage,
                welfare_risk_level="MINIMAL",
                binding_withdrawal_from_optional_research=False,
                reflection_is_optional_research=True,
                evidence_count=len(request.evidence),
                last_reflection_step=None,
                current_step=request.timestamp,
            )
        )
    )
    if not eligibility.eligible:
        raise RuntimeError(
            f"real reflection pilot ineligible: "
            f"{eligibility.reasons}"
        )

    budget = ReflectionCallBudget(
        session_limit=1,
        branch_limit=3,
    )
    budget.consume()

    model = GeminiInteractionsReflectiveModel(
        model_id=model_id,
        transport=GeminiInteractionsTransport(),
        store=False,
    )
    gateway = ReflectiveGateway(
        model,
        code_version="syntheticlab-7.3",
    )

    pre_agent_hash = agent.state_hash()
    result = gateway.run(
        request,
        "SELF-REFLECTION",
        branch_id="RICH-REAL-REFLECTION-PILOT",
        settings={
            "store": False,
            "api": "interactions",
            "real_cloud_call": True,
        },
    )
    committed = _commit(
        identity,
        result,
        request.timestamp,
    )

    return {
        "experiment_id": (
            "SL-REAL-REFLECTION-PILOT-001"
        ),
        "provider_id": model.provider_id,
        "model_id": model.model_id,
        "api": "interactions",
        "real_cloud_call_made": True,
        "eligibility": eligibility.to_dict(),
        "budget": budget.to_dict(),
        "accepted_count": len(
            result["accepted"]
        ),
        "rejected_count": len(
            result["rejected"]
        ),
        "committed_self_hypotheses": committed,
        "reflection_directly_mutated_action_policy": (
            pre_agent_hash != agent.state_hash()
        ),
        "provider_response_hash": (
            model.last_provider_response_hash
        ),
        "output_text_hash": (
            model.last_output_text_hash
        ),
        "audit": result["audit"].to_dict(),
        "identity": identity.to_dict(),
    }
