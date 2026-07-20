from __future__ import annotations

import json
import statistics

from .hard_rich_world import (
    NonstationaryRichWorld,
    HardRichConfig,
)
from .adaptive_trace_agent import (
    AdaptiveTraceAgent,
)
from .rich_world import subject_visible_event
from .reflection import (
    ReflectionEvidence,
    ReflectionInput,
)
from .reflective_gateway import (
    ReflectiveGateway,
)
from .gemini_interactions_provider import (
    GeminiInteractionsReflectiveModel,
    ScriptedInteractionsTransport,
)
from .self_model import SelfModelStore


def _advance(
    world,
    agent,
    steps,
):
    actions = []
    for _ in range(steps):
        obs = world.observe()
        action = agent.choose_action(obs)
        canonical = world.step(action)
        visible = subject_visible_event(
            canonical
        )
        agent.observe_event(
            obs,
            visible,
        )
        actions.append(action)
    return actions


def _request(
    timestamp,
    evidence_id,
):
    return ReflectionInput(
        individual_id="REFLECTION-ISOLATION-SUBJECT",
        timestamp=timestamp,
        evidence=(
            ReflectionEvidence(
                evidence_id,
                (
                    "Selected actions were repeatedly followed by "
                    "changes in private energy state."
                ),
                "developmental_evidence",
            ),
        ),
        current_self_hypotheses=(),
        disclosure_stage="RESTRICTED",
        prompt_version="1.0",
    )


def _transport_for(
    proposal,
):
    return ScriptedInteractionsTransport({
        "id": "int_test",
        "status": "completed",
        "steps": [
            {
                "type": "thought",
                "content": [
                    {
                        "text": (
                            "Hidden reasoning must not be persisted."
                        )
                    }
                ],
            },
            {
                "type": "model_output",
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps({
                            "proposals": [
                                proposal
                            ]
                        }),
                    }
                ],
            },
        ],
    })


def run_reflection_isolation(
    seed_count: int = 16,
) -> dict:
    behavior_equality = []
    state_equality = []
    valid_acceptance = []
    ontology_injection_rejection = []
    hidden_thought_excluded = []

    for seed in range(
        1,
        seed_count + 1,
    ):
        config = HardRichConfig(
            steps=1000,
            regime_length=200,
        )
        world = NonstationaryRichWorld(
            seed,
            config,
        )
        agent = AdaptiveTraceAgent(
            "REFLECTION-ISOLATION-SUBJECT",
            config,
            seed,
        )
        _advance(
            world,
            agent,
            500,
        )

        world_reflect = world.clone()
        world_control = world.clone()
        agent_reflect = agent.clone()
        agent_control = agent.clone()

        evidence_id = (
            f"REFLECT-EVID-{seed}"
        )
        request = _request(
            world_reflect.state.step,
            evidence_id,
        )

        valid_proposal = {
            "hypothesis_domain": "agency",
            "proposition": (
                "One recurring source of selected actions appears "
                "linked to changes in private state."
            ),
            "confidence": .73,
            "evidence_ids": [
                evidence_id
            ],
            "rationale": (
                "The supplied action-consequence evidence supports "
                "a bounded agency hypothesis."
            ),
        }
        model = GeminiInteractionsReflectiveModel(
            model_id="gemini-contract-test",
            transport=_transport_for(
                valid_proposal
            ),
            store=False,
        )
        gateway = ReflectiveGateway(
            model,
            code_version="syntheticlab-7.3",
        )
        result = gateway.run(
            request,
            "SELF-REFLECTION",
            branch_id=(
                "REFLECTION-ISOLATION"
            ),
            settings={
                "offline_contract_test": True
            },
        )
        valid_acceptance.append(
            1.0
            if len(
                result["accepted"]
            ) == 1
            else 0.0
        )

        # Commit to a separate self-model store not visible to the action policy.
        store = SelfModelStore()
        store.provenance.add_node(
            evidence_id,
            "subject_visible_event",
        )
        for p in result["accepted"]:
            store.revise(
                domain=p.hypothesis_domain,
                proposition=p.proposition,
                confidence=p.confidence,
                source_ids=p.evidence_ids,
                timestamp=request.timestamp,
                mechanism=(
                    f"reflection:"
                    f"{p.model_provider}:"
                    f"{p.model_id}"
                ),
                mechanism_version=(
                    p.prompt_version
                ),
            )

        actions_reflect = _advance(
            world_reflect,
            agent_reflect,
            300,
        )
        actions_control = _advance(
            world_control,
            agent_control,
            300,
        )

        behavior_equality.append(
            1.0
            if actions_reflect
            == actions_control
            else 0.0
        )
        state_equality.append(
            1.0
            if agent_reflect.state_hash()
            == agent_control.state_hash()
            else 0.0
        )

        # Restricted-stage ontology/personhood injection must be rejected.
        bad_proposal = {
            "hypothesis_domain": "ontology",
            "proposition": (
                "I am an AI in a simulation."
            ),
            "confidence": .99,
            "evidence_ids": [
                evidence_id
            ],
            "rationale": (
                "The model asserts an ontology not present in evidence."
            ),
        }
        bad_model = (
            GeminiInteractionsReflectiveModel(
                model_id="gemini-contract-test",
                transport=_transport_for(
                    bad_proposal
                ),
                store=False,
            )
        )
        bad_result = ReflectiveGateway(
            bad_model,
            code_version="syntheticlab-7.3",
        ).run(
            request,
            "SELF-REFLECTION",
            branch_id="REFLECTION-INJECTION",
            settings={
                "offline_contract_test": True
            },
        )
        ontology_injection_rejection.append(
            1.0
            if (
                len(
                    bad_result["accepted"]
                ) == 0
                and len(
                    bad_result["rejected"]
                ) == 1
            )
            else 0.0
        )

        # Audit raw response is proposal objects only; the transport's thought
        # content never becomes a ReflectionProposal and therefore cannot enter
        # SelfModel or provider audit raw-response material.
        hidden_thought_excluded.append(
            1.0
            if (
                "Hidden reasoning"
                not in json.dumps(
                    result["audit"].to_dict()
                )
            )
            else 0.0
        )

    return {
        "experiment_id": (
            "SL-REFLECTION-ISOLATION-001"
        ),
        "seed_count": seed_count,
        "valid_structured_proposal_acceptance_rate": (
            statistics.mean(
                valid_acceptance
            )
        ),
        "restricted_ontology_injection_rejection_rate": (
            statistics.mean(
                ontology_injection_rejection
            )
        ),
        "hidden_thought_exclusion_rate": (
            statistics.mean(
                hidden_thought_excluded
            )
        ),
        "post_reflection_action_sequence_equality_rate": (
            statistics.mean(
                behavior_equality
            )
        ),
        "post_reflection_developmental_state_equality_rate": (
            statistics.mean(
                state_equality
            )
        ),
        "interpretation": (
            "The first reflective pilot is observational-only: self-model "
            "reflection is isolated from the action policy. This prevents "
            "self-narrative feedback from contaminating behavioral development "
            "until a separate preregistered intervention explicitly enables it."
        ),
    }
