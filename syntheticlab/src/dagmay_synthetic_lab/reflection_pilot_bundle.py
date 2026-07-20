from __future__ import annotations

from .real_reflection_pilot import (
    build_pilot_case,
    run_offline_real_provider_contract,
)
from .rich_snapshot import snapshot_pair
from .reflection_isolation_experiments import (
    run_reflection_isolation,
)
from .core import canonical_hash


def prepare_real_reflection_pilot_bundle(
    seed: int = 101,
) -> dict:
    world, agent, identity, request = (
        build_pilot_case(seed)
    )
    common_snapshot = snapshot_pair(
        world,
        agent,
    )

    contract = (
        run_offline_real_provider_contract(
            seed=seed
        )
    )
    isolation = run_reflection_isolation(
        seed_count=8
    )

    branch_plan = {
        "common_lineage_id": (
            identity.lineage_id
        ),
        "prefork_identity_id": (
            identity.identity_id
        ),
        "prefork_snapshot_hash": (
            common_snapshot["pair_hash"]
        ),
        "branches": {
            "R_REAL": {
                "branch_id": (
                    "RICH-REAL-REFLECTION-PILOT"
                ),
                "intervention": (
                    "one real structured reflection call"
                ),
                "self_model_readable_by_action_policy": False,
                "continuing_branch": True,
            },
            "R_CONTROL": {
                "branch_id": (
                    "RICH-NO-REFLECTION-CONTROL"
                ),
                "intervention": (
                    "no reflection call"
                ),
                "self_model_readable_by_action_policy": False,
                "continuing_branch": True,
            },
        },
        "fake_provider_role": (
            "analytical contract/control only; no third continuing branch required"
        ),
    }

    payload = {
        "experiment_id": (
            "SL-REAL-REFLECTION-PILOT-PREFLIGHT-001"
        ),
        "seed": seed,
        "request": {
            "individual_id": (
                request.individual_id
            ),
            "timestamp": request.timestamp,
            "evidence_ids": [
                e.evidence_id
                for e in request.evidence
            ],
            "disclosure_stage": (
                request.disclosure_stage
            ),
            "prompt_version": (
                request.prompt_version
            ),
        },
        "common_snapshot": (
            common_snapshot
        ),
        "branch_plan": branch_plan,
        "offline_provider_contract": {
            "accepted_count": (
                contract["accepted_count"]
            ),
            "rejected_count": (
                contract["rejected_count"]
            ),
            "real_cloud_call_made": (
                contract["real_cloud_call_made"]
            ),
            "store_requested": (
                contract["store_requested"]
            ),
        },
        "reflection_isolation": isolation,
        "ready_for_single_real_call": (
            contract["accepted_count"] > 0
            and contract[
                "real_cloud_call_made"
            ] is False
            and isolation[
                "restricted_ontology_injection_rejection_rate"
            ] == 1.0
            and isolation[
                "hidden_thought_exclusion_rate"
            ] == 1.0
            and isolation[
                "post_reflection_action_sequence_equality_rate"
            ] == 1.0
        ),
    }
    return {
        **payload,
        "preflight_hash": canonical_hash(
            payload
        ),
    }
