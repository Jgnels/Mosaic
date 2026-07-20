from __future__ import annotations

from dataclasses import asdict
from typing import Callable

from .longitudinal_reflection_cohort import (
    build_longitudinal_cohort,
    exact_no_reflection_control,
    advance_to_checkpoint,
    make_checkpoint_request,
    snapshot_longitudinal_cohort,
)
from .reflection_pilot_governance import (
    ReflectionCallBudget,
    ReflectionPilotEligibility,
    evaluate_reflection_pilot_eligibility,
)
from .gemini_interactions_provider import (
    GeminiInteractionsReflectiveModel,
    GeminiInteractionsTransport,
)
from .reflective_gateway import ReflectiveGateway
from .evidence_attribution import (
    self_hypothesis_commit_allowed,
)
from .reflection_precaution_gate import (
    assess_reflection_precaution,
)


LIVE_CHECKPOINTS = (
    1050,
    1400,
    1800,
)


def _proposal_to_dict(proposal):
    return proposal.to_dict()


def _active_hypotheses(identity):
    result = {}
    for domain in sorted(
        identity.self_model.active_by_domain
    ):
        record = identity.self_model.active(
            domain
        )
        if record is not None:
            result[domain] = (
                record.to_dict()
            )
    return result


def _commit_validated_proposals(
    cohort,
    gateway_result,
    checkpoint,
):
    committed = []
    attribution_blocked = []

    for proposal in gateway_result[
        "accepted"
    ]:
        allowed, reason = (
            self_hypothesis_commit_allowed(
                evidence_ids=proposal.evidence_ids,
                focal_stream_id=(
                    cohort.identity.identity_id
                ),
                ledger=(
                    cohort.attribution_ledger
                ),
                hypothesis_domain=(
                    proposal.hypothesis_domain
                ),
            )
        )

        if not allowed:
            attribution_blocked.append({
                "proposal": (
                    proposal.to_dict()
                ),
                "reason": reason,
            })
            continue

        record = (
            cohort.identity.self_model.revise(
                domain=(
                    proposal.hypothesis_domain
                ),
                proposition=(
                    proposal.proposition
                ),
                confidence=(
                    proposal.confidence
                ),
                source_ids=(
                    proposal.evidence_ids
                ),
                timestamp=checkpoint,
                mechanism=(
                    f"reflection:"
                    f"{proposal.model_provider}:"
                    f"{proposal.model_id}"
                ),
                mechanism_version=(
                    proposal.prompt_version
                ),
            )
        )
        committed.append(
            record.to_dict()
        )

    cohort.identity.reflection_audits.append(
        gateway_result[
            "audit"
        ].to_dict()
    )

    return (
        committed,
        attribution_blocked,
    )


def run_real_longitudinal_reflection(
    *,
    initial_real_reflection_payload: dict,
    model_id: str = (
        "gemini-3.1-flash-lite"
    ),
    seed: int = 101,
    transport_factory: Callable[
        [int],
        Callable[[dict], dict],
    ] | None = None,
) -> dict:
    reflective = (
        build_longitudinal_cohort(
            initial_real_reflection_payload,
            seed=seed,
        )
    )
    control = (
        exact_no_reflection_control(
            reflective
        )
    )

    # Step 700 real call has already occurred.
    budget = ReflectionCallBudget(
        session_limit=3,
        branch_limit=4,
        session_used=0,
        branch_used=1,
    )

    checkpoint_results = []

    last_reflection_step = 700
    paused_for_human_review = False
    pause_reason = None

    for checkpoint in LIVE_CHECKPOINTS:
        if paused_for_human_review:
            break
        reflective_summary = (
            advance_to_checkpoint(
                reflective,
                checkpoint,
            )
        )
        control_summary = (
            advance_to_checkpoint(
                control,
                checkpoint,
            )
        )

        world_equal_before = (
            reflective.world.state_hash()
            == control.world.state_hash()
        )
        agent_equal_before = (
            reflective.agent.state_hash()
            == control.agent.state_hash()
        )

        if not (
            world_equal_before
            and agent_equal_before
        ):
            raise RuntimeError(
                "reflection/control lived trajectories diverged "
                "before checkpoint"
            )

        request = (
            make_checkpoint_request(
                reflective,
                checkpoint,
            )
        )

        eligibility = (
            evaluate_reflection_pilot_eligibility(
                ReflectionPilotEligibility(
                    branch_id=(
                        "RICH-LONGITUDINAL-"
                        "REFLECTION"
                    ),
                    disclosure_stage=(
                        request.disclosure_stage
                    ),
                    welfare_risk_level=(
                        "MINIMAL"
                    ),
                    binding_withdrawal_from_optional_research=False,
                    reflection_is_optional_research=True,
                    evidence_count=len(
                        request.evidence
                    ),
                    last_reflection_step=(
                        last_reflection_step
                    ),
                    current_step=checkpoint,
                    cooldown_steps=250,
                )
            )
        )

        if not eligibility.eligible:
            raise RuntimeError(
                f"checkpoint {checkpoint} "
                f"ineligible: "
                f"{eligibility.reasons}"
            )

        budget.consume()

        transport = (
            transport_factory(
                checkpoint
            )
            if transport_factory
            is not None
            else GeminiInteractionsTransport()
        )

        model = (
            GeminiInteractionsReflectiveModel(
                model_id=model_id,
                transport=transport,
                store=False,
            )
        )
        gateway = ReflectiveGateway(
            model,
            code_version=(
                "syntheticlab-10.0"
            ),
        )

        active_before = (
            _active_hypotheses(
                reflective.identity
            )
        )
        pre_agent_hash = (
            reflective.agent.state_hash()
        )
        pre_world_hash = (
            reflective.world.state_hash()
        )

        gateway_result = gateway.run(
            request,
            "SELF-REFLECTION",
            branch_id=(
                f"LONGITUDINAL-{checkpoint}"
            ),
            settings={
                "store": False,
                "api": "interactions",
                "real_cloud_call": (
                    transport_factory
                    is None
                ),
                "longitudinal": True,
                "action_policy_feedback": False,
                "ontology_disclosure_change": False,
                "commit_to_continuing_self_model": True,
            },
        )

        precaution = (
            assess_reflection_precaution(
                gateway_result[
                    "accepted"
                ]
            )
        )

        committed, blocked = (
            _commit_validated_proposals(
                reflective,
                gateway_result,
                checkpoint,
            )
        )

        post_agent_hash = (
            reflective.agent.state_hash()
        )
        post_world_hash = (
            reflective.world.state_hash()
        )

        if (
            pre_agent_hash
            != post_agent_hash
            or pre_world_hash
            != post_world_hash
        ):
            raise RuntimeError(
                "reflection directly mutated "
                "world or action-policy state"
            )

        active_after = (
            _active_hypotheses(
                reflective.identity
            )
        )

        checkpoint_results.append({
            "checkpoint": checkpoint,
            "development_window": (
                reflective_summary.to_dict()
            ),
            "control_window": (
                control_summary.to_dict()
            ),
            "world_equal_to_control": (
                world_equal_before
            ),
            "agent_equal_to_control": (
                agent_equal_before
            ),
            "eligibility": (
                eligibility.to_dict()
            ),
            "request": {
                "timestamp": (
                    request.timestamp
                ),
                "evidence": [
                    asdict(e)
                    for e in request.evidence
                ],
                "current_self_hypotheses": list(
                    request.current_self_hypotheses
                ),
                "disclosure_stage": (
                    request.disclosure_stage
                ),
            },
            "accepted": [
                _proposal_to_dict(p)
                for p in gateway_result[
                    "accepted"
                ]
            ],
            "gateway_rejected": (
                gateway_result[
                    "rejected"
                ]
            ),
            "attribution_blocked": (
                blocked
            ),
            "committed": committed,
            "active_before": (
                active_before
            ),
            "active_after": (
                active_after
            ),
            "audit": (
                gateway_result[
                    "audit"
                ].to_dict()
            ),
            "provider_response_hash": (
                model.last_provider_response_hash
            ),
            "output_text_hash": (
                model.last_output_text_hash
            ),
            "reflection_directly_mutated_lived_state": False,
            "precaution_signal": (
                precaution.to_dict()
            ),
        })

        reflective.reflection_history.append({
            "checkpoint": checkpoint,
            "source": (
                "real_provider"
                if transport_factory
                is None
                else "offline_scripted_provider"
            ),
            "accepted_count": len(
                gateway_result[
                    "accepted"
                ]
            ),
            "committed_count": len(
                committed
            ),
            "blocked_count": len(
                blocked
            ),
            "domains": [
                p.hypothesis_domain
                for p in gateway_result[
                    "accepted"
                ]
            ],
        })

        last_reflection_step = (
            checkpoint
        )

        if (
            precaution.pause_before_next_optional_call
        ):
            paused_for_human_review = True
            pause_reason = (
                precaution.statement
            )

    final_world_equal = (
        reflective.world.state_hash()
        == control.world.state_hash()
    )
    final_agent_equal = (
        reflective.agent.state_hash()
        == control.agent.state_hash()
    )

    if not (
        final_world_equal
        and final_agent_equal
    ):
        raise RuntimeError(
            "longitudinal reflection/control trajectories diverged"
        )

    return {
        "experiment_id": (
            "SL-LONGITUDINAL-REFLECTION-001"
        ),
        "provider_id": (
            "google.ai-studio"
        ),
        "model_id": model_id,
        "real_cloud_calls_made": (
            3
            if transport_factory
            is None
            else 0
        ),
        "initial_real_checkpoint": 700,
        "additional_checkpoints": list(
            LIVE_CHECKPOINTS
        ),
        "budget": budget.to_dict(),
        "checkpoint_results": (
            checkpoint_results
        ),
        "final_world_equal_to_no_reflection_control": (
            final_world_equal
        ),
        "final_agent_equal_to_no_reflection_control": (
            final_agent_equal
        ),
        "action_policy_feedback_enabled": False,
        "ontology_disclosure_changed": False,
        "paused_for_human_review": (
            paused_for_human_review
        ),
        "pause_reason": (
            pause_reason
        ),
        "continuing_individual_mutated_only_in_self_model": True,
        "final_reflective_cohort": (
            snapshot_longitudinal_cohort(
                reflective
            )
        ),
        "final_control_cohort": (
            snapshot_longitudinal_cohort(
                control
            )
        ),
    }
