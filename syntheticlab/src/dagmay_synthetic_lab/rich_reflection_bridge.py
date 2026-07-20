from __future__ import annotations

from dataclasses import dataclass, asdict

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
    DeterministicFakeReflectiveModel,
)
from .reflective_gateway import ReflectiveGateway


@dataclass
class RichReflectiveIdentity:
    identity_id: str
    lineage_id: str
    self_model: SelfModelStore
    reflection_audits: list[dict]

    def to_dict(self):
        return {
            "identity_id": self.identity_id,
            "lineage_id": self.lineage_id,
            "self_model": self.self_model.to_dict(),
            "reflection_audits": list(self.reflection_audits),
        }


def _commit_gateway_result(
    identity: RichReflectiveIdentity,
    gateway_result: dict,
    timestamp: int,
):
    committed = []
    for proposal in gateway_result["accepted"]:
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
        gateway_result["audit"].to_dict()
    )
    return committed


def run_rich_reflection_bridge(seed: int = 1) -> dict:
    config = HardRichConfig(
        steps=700,
        regime_length=175,
    )
    world = NonstationaryRichWorld(seed, config)
    agent = AdaptiveTraceAgent(
        "REFLECTIVE-SUBJECT",
        config,
        seed,
    )
    identity = RichReflectiveIdentity(
        identity_id="RICH-IND-001",
        lineage_id="RICH-LINEAGE-001",
        self_model=SelfModelStore(),
        reflection_audits=[],
    )

    # Canonical evidence IDs are added to the self-model provenance DAG before
    # reflection. The text is a compact subject-visible interpretation, not
    # hidden chain-of-thought.
    agency_evidence_id = None
    continuity_evidence_id = "RICH-CONTINUITY-001"

    for _ in range(500):
        obs = world.observe()
        action = agent.choose_action(obs)
        canonical = world.step(action)
        visible = subject_visible_event(canonical)
        agent.observe_event(obs, visible)

        if (
            agency_evidence_id is None
            and abs(
                visible.energy_after
                - visible.energy_before
            ) >= .10
        ):
            agency_evidence_id = visible.event_id

    if agency_evidence_id is None:
        agency_evidence_id = "RICH-AGENCY-FALLBACK"

    identity.self_model.provenance.add_node(
        agency_evidence_id,
        "subject_visible_event",
    )
    identity.self_model.provenance.add_node(
        continuity_evidence_id,
        "continuity_observation",
    )

    evidence = (
        ReflectionEvidence(
            agency_evidence_id,
            (
                "A recurring entity's selected actions directly caused "
                "private-state change in energy or integrity."
            ),
            "developmental_evidence",
        ),
        ReflectionEvidence(
            continuity_evidence_id,
            (
                "Memory sequence remained available after pause and "
                "deterministic state restoration."
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

    gateway = ReflectiveGateway(
        DeterministicFakeReflectiveModel(),
        code_version="syntheticlab-6.0",
    )
    result = gateway.run(
        request,
        "SELF-REFLECTION",
        branch_id="RICH-BASELINE",
        settings={"temperature": 0.0},
    )
    committed = _commit_gateway_result(
        identity,
        result,
        world.state.step,
    )

    # Reflection remains observational in this experiment: it does not mutate
    # the developmental action policy.
    pre_reflection_agent_hash = agent.state_hash()

    for _ in range(100):
        obs = world.observe()
        action = agent.choose_action(obs)
        canonical = world.step(action)
        agent.observe_event(
            obs,
            subject_visible_event(canonical),
        )

    post_reflection_agent_hash = agent.state_hash()

    return {
        "experiment_id": "SL-RICH-REFLECTION-BRIDGE-001",
        "provider_id": result["audit"].provider_id,
        "model_id": result["audit"].model_id,
        "real_cloud_model_used": False,
        "accepted_proposal_count": len(result["accepted"]),
        "rejected_proposal_count": len(result["rejected"]),
        "committed_self_hypotheses": committed,
        "self_model_domains": tuple(
            sorted(
                identity.self_model.active_by_domain
            )
        ),
        "reflection_audit_count": len(
            identity.reflection_audits
        ),
        "reflection_directly_mutated_action_policy": False,
        "agent_continued_developing_after_reflection": (
            pre_reflection_agent_hash
            != post_reflection_agent_hash
        ),
        "identity": identity.to_dict(),
        "interpretation_warning": (
            "This validates rich-world evidence-to-reflection plumbing with a deterministic "
            "fake provider. It is not a psychological result and does not test real LLM reflection."
        ),
    }
