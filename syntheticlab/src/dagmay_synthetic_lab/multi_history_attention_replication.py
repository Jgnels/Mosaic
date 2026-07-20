from __future__ import annotations

from dataclasses import asdict
from pathlib import Path
from typing import Callable
from collections import defaultdict
import json
import statistics

from .core import canonical_hash
from .reflection import (
    ReflectionEvidence,
    ReflectionInput,
)
from .reflective_gateway import (
    ReflectiveGateway,
)
from .selective_reflection_provider import (
    GeminiSelectiveReflectiveModel,
)
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
)
from .rate_limit_transport import (
    RateLimitSafeTransport,
)
from .retrieval_attention_experiments import (
    _frozen_history,
)
from .typed_evidence_retrieval import (
    build_typed_evidence_fabric,
)
from .matched_composition_retrieval import (
    DOMAINS,
    retrieve_matched_composition,
    composition_counts,
)
from .typed_retrieval_reflection_pilot import (
    _opaque_id,
)
from .attention_competition_pilot import (
    _current_hypotheses,
)


REPLICATION_SEEDS = (
    1701,
    1702,
    1703,
)


def _order_units(
    units,
    *,
    history_seed: int,
):
    """Deterministically randomize presentation order without using target labels.

    Shared evidence units keep the same sort key across F0/F1 conditions for a
    given history. Extra target evidence is inserted according to its own
    evidence-ID-derived key rather than appearing in a target-specific block.
    """

    return tuple(
        sorted(
            units,
            key=lambda unit: (
                canonical_hash({
                    "order_version": (
                        "multi-history-v1"
                    ),
                    "history_seed": (
                        history_seed
                    ),
                    "evidence_id": (
                        unit.evidence_id
                    ),
                }),
                unit.evidence_id,
            ),
        )
    )


def _prepare_condition(
    *,
    history_seed: int,
    condition: str,
    target_domain: str | None,
    units,
    hypotheses: tuple[
        str,
        ...,
    ],
) -> dict:
    ordered_units = (
        _order_units(
            units,
            history_seed=(
                history_seed
            ),
        )
    )

    opaque_map = {
        _opaque_id(
            unit.evidence_id
        ): {
            "source_evidence_id": (
                unit.evidence_id
            ),
            "domain": (
                unit.domain
            ),
            "kind": (
                unit.kind.value
            ),
            "source_memory_ids": list(
                unit.source_memory_ids
            ),
        }
        for unit
        in ordered_units
    }

    evidence = tuple(
        ReflectionEvidence(
            evidence_id=(
                _opaque_id(
                    unit.evidence_id
                )
            ),
            summary=(
                unit.summary
            ),
            evidence_type=(
                "retrieved_structural_evidence"
            ),
        )
        for unit
        in ordered_units
    )

    request = ReflectionInput(
        individual_id=(
            f"ANALYTICAL-MULTI-HISTORY-{history_seed}"
        ),
        timestamp=(
            2600
        ),
        evidence=(
            evidence
        ),
        current_self_hypotheses=(
            hypotheses
        ),
        disclosure_stage=(
            "RESTRICTED"
        ),
        prompt_version=(
            "1.0"
        ),
    )

    return {
        "history_seed": (
            history_seed
        ),
        "condition": (
            condition
        ),
        "target_domain": (
            target_domain
        ),
        "request": (
            request
        ),
        "opaque_evidence_map": (
            opaque_map
        ),
        "retrieval_domain_counts": (
            composition_counts(
                ordered_units
            )
        ),
        "evidence_order_domains": [
            unit.domain
            for unit
            in ordered_units
        ],
        "evidence_hash": (
            canonical_hash(
                [
                    unit.to_dict()
                    for unit
                    in ordered_units
                ]
            )
        ),
    }


def build_history_bundle(
    *,
    longitudinal_analysis: dict,
    history_seed: int,
    history_steps: int = 650,
) -> dict:
    frozen = _frozen_history(
        seed=(
            history_seed
        ),
        steps=(
            history_steps
        ),
    )

    fabric = (
        build_typed_evidence_fabric(
            memory_bank=(
                frozen[
                    "memory_bank"
                ]
            ),
            current_step=(
                frozen[
                    "current_step"
                ]
            ),
        )
    )

    hypotheses = (
        _current_hypotheses(
            longitudinal_analysis
        )
    )

    conditions = {}

    baseline = (
        retrieve_matched_composition(
            fabric=fabric,
            target_domain=None,
        )
    )

    conditions[
        "F0_BALANCED"
    ] = _prepare_condition(
        history_seed=(
            history_seed
        ),
        condition=(
            "F0_BALANCED"
        ),
        target_domain=None,
        units=baseline,
        hypotheses=hypotheses,
    )

    for domain in DOMAINS:
        units = (
            retrieve_matched_composition(
                fabric=fabric,
                target_domain=(
                    domain
                ),
            )
        )

        conditions[
            f"F1_{domain.upper()}"
        ] = _prepare_condition(
            history_seed=(
                history_seed
            ),
            condition=(
                f"F1_{domain.upper()}"
            ),
            target_domain=(
                domain
            ),
            units=units,
            hypotheses=hypotheses,
        )

    return {
        "history_seed": (
            history_seed
        ),
        "history_steps": (
            history_steps
        ),
        "frozen_world_hash": (
            frozen[
                "world_hash"
            ]
        ),
        "frozen_agent_hash": (
            frozen[
                "agent_hash"
            ]
        ),
        "memory_bank_hash": (
            frozen[
                "memory_bank_hash"
            ]
        ),
        "conditions": (
            conditions
        ),
    }


def _plan(
    bundles: list[
        dict
    ],
) -> list[
    dict
]:
    plan = []

    for bundle in bundles:
        baseline = bundle[
            "conditions"
        ][
            "F0_BALANCED"
        ]

        for replicate in (
            "A",
            "B",
            "C",
            "D",
        ):
            plan.append({
                **baseline,
                "replicate": (
                    replicate
                ),
            })

        for domain in DOMAINS:
            condition = bundle[
                "conditions"
            ][
                f"F1_{domain.upper()}"
            ]

            for replicate in (
                "A",
                "B",
            ):
                plan.append({
                    **condition,
                    "replicate": (
                        replicate
                    ),
                })

    for item in plan:
        item[
            "call_key"
        ] = canonical_hash({
            "experiment": (
                "multi-history-attention-replication-v1"
            ),
            "history_seed": (
                item[
                    "history_seed"
                ]
            ),
            "condition": (
                item[
                    "condition"
                ]
            ),
            "replicate": (
                item[
                    "replicate"
                ]
            ),
            "request": (
                asdict(
                    item[
                        "request"
                    ]
                )
            ),
        })

    return plan


def _summarize(
    *,
    calls: list[
        dict
    ],
    model_id: str,
    bundles: list[
        dict
    ],
) -> dict:
    planned = (
        len(
            bundles
        )
        * 12
    )

    return {
        "experiment_id": (
            "SL-MULTI-HISTORY-ATTENTION-"
            "REPLICATION-001"
        ),
        "status": (
            "COMPLETE"
            if len(
                calls
            )
            == planned
            else "PARTIAL"
        ),
        "history_seeds": [
            bundle[
                "history_seed"
            ]
            for bundle
            in bundles
        ],
        "planned_real_cloud_calls": (
            planned
        ),
        "real_cloud_calls_recorded": (
            len(
                calls
            )
        ),
        "provider_id": (
            "google.ai-studio"
        ),
        "model_id": (
            model_id
        ),
        "response_budget_max_proposals": (
            2
        ),
        "presentation_order_randomized_by_evidence_id": (
            True
        ),
        "all_conditions_keep_all_four_evidence_channels": (
            all(
                all(
                    count > 0
                    for count
                    in call[
                        "retrieval_domain_counts"
                    ].values()
                )
                for call
                in calls
            )
            if calls
            else True
        ),
        "calls": (
            calls
        ),
        "committed_to_continuing_self_model": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "continuing_individual_mutated": (
            False
        ),
        "provider_facing_evidence_ids_are_opaque": (
            True
        ),
    }


def run_multi_history_attention_replication(
    *,
    checkpoint_path,
    longitudinal_analysis: dict,
    model_id: str = (
        "gemini-3.1-flash-lite"
    ),
    history_seeds: tuple[
        int,
        ...,
    ] = REPLICATION_SEEDS,
    transport_factory: Callable
    | None = None,
) -> dict:
    checkpoint_path = Path(
        checkpoint_path
    )

    bundles = [
        build_history_bundle(
            longitudinal_analysis=(
                longitudinal_analysis
            ),
            history_seed=(
                history_seed
            ),
        )
        for history_seed
        in history_seeds
    ]

    plan = _plan(
        bundles
    )

    existing = {}

    if checkpoint_path.exists():
        prior = json.loads(
            checkpoint_path.read_text(
                encoding="utf-8"
            )
        )

        for call in prior.get(
            "calls",
            []
        ):
            existing[
                call[
                    "call_key"
                ]
            ] = call

    # One shared live transport preserves pacing across the entire 36-call run.
    shared_live_transport = (
        None
        if transport_factory
        is not None
        else RateLimitSafeTransport(
            inner=(
                GeminiInteractionsTransport()
            ),
            minimum_interval_seconds=(
                4.25
            ),
            max_429_retries=(
                5
            ),
            fallback_retry_seconds=(
                65.0
            ),
        )
    )

    ordered = []

    for item in plan:
        key = item[
            "call_key"
        ]

        if key in existing:
            call = existing[
                key
            ]
        else:
            transport = (
                transport_factory(
                    item,
                    key,
                )
                if transport_factory
                is not None
                else shared_live_transport
            )

            model = (
                GeminiSelectiveReflectiveModel(
                    model_id=(
                        model_id
                    ),
                    transport=(
                        transport
                    ),
                    store=False,
                )
            )

            gateway = (
                ReflectiveGateway(
                    model,
                    code_version=(
                        "syntheticlab-17.0"
                    ),
                )
            )

            result = gateway.run(
                item[
                    "request"
                ],
                "SELECTIVE-SELF-REFLECTION",
                branch_id=(
                    f"H{item['history_seed']}-"
                    f"{item['condition']}-"
                    f"{item['replicate']}"
                ),
                settings={
                    "store": False,
                    "api": (
                        "interactions"
                    ),
                    "real_cloud_call": (
                        transport_factory
                        is None
                    ),
                    "multi_history_attention_replication": (
                        True
                    ),
                    "analytical_only": (
                        True
                    ),
                    "commit_to_continuing_self_model": (
                        False
                    ),
                    "action_policy_feedback": (
                        False
                    ),
                    "provider_facing_evidence_ids_are_opaque": (
                        True
                    ),
                    "response_budget_max_proposals": (
                        2
                    ),
                    "presentation_order_randomized_by_evidence_id": (
                        True
                    ),
                },
            )

            call = {
                "call_key": (
                    key
                ),
                "history_seed": (
                    item[
                        "history_seed"
                    ]
                ),
                "condition": (
                    item[
                        "condition"
                    ]
                ),
                "target_domain": (
                    item[
                        "target_domain"
                    ]
                ),
                "replicate": (
                    item[
                        "replicate"
                    ]
                ),
                "retrieval_domain_counts": (
                    item[
                        "retrieval_domain_counts"
                    ]
                ),
                "evidence_order_domains": (
                    item[
                        "evidence_order_domains"
                    ]
                ),
                "evidence_hash": (
                    item[
                        "evidence_hash"
                    ]
                ),
                "opaque_evidence_map": (
                    item[
                        "opaque_evidence_map"
                    ]
                ),
                "request_hash": (
                    canonical_hash(
                        asdict(
                            item[
                                "request"
                            ]
                        )
                    )
                ),
                "accepted": [
                    proposal.to_dict()
                    for proposal
                    in result[
                        "accepted"
                    ]
                ],
                "rejected": (
                    result[
                        "rejected"
                    ]
                ),
                "audit": (
                    result[
                        "audit"
                    ].to_dict()
                ),
                "provider_response_hash": (
                    model.last_provider_response_hash
                ),
                "output_text_hash": (
                    model.last_output_text_hash
                ),
            }

            existing[
                key
            ] = call

            partial_calls = [
                existing[
                    planned[
                        "call_key"
                    ]
                ]
                for planned
                in plan
                if planned[
                    "call_key"
                ]
                in existing
            ]

            partial = (
                _summarize(
                    calls=(
                        partial_calls
                    ),
                    model_id=(
                        model_id
                    ),
                    bundles=(
                        bundles
                    ),
                )
            )

            temp = (
                checkpoint_path.with_suffix(
                    checkpoint_path.suffix
                    + ".tmp"
                )
            )

            temp.write_text(
                json.dumps(
                    partial,
                    indent=2,
                    sort_keys=True,
                ),
                encoding="utf-8",
            )

            temp.replace(
                checkpoint_path
            )

        ordered.append(
            call
        )

    final = _summarize(
        calls=(
            ordered
        ),
        model_id=(
            model_id
        ),
        bundles=(
            bundles
        ),
    )

    temp = (
        checkpoint_path.with_suffix(
            checkpoint_path.suffix
            + ".tmp"
        )
    )

    temp.write_text(
        json.dumps(
            final,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    temp.replace(
        checkpoint_path
    )

    return final


def _domain_selected(
    call: dict,
    domain: str,
) -> bool:
    return any(
        proposal[
            "hypothesis_domain"
        ] == domain
        for proposal
        in call[
            "accepted"
        ]
    )


def analyze_multi_history_replication(
    payload: dict,
) -> dict:
    by_history = {}
    cell_rows = []

    for history_seed in payload[
        "history_seeds"
    ]:
        calls = [
            call
            for call
            in payload[
                "calls"
            ]
            if call[
                "history_seed"
            ] == history_seed
        ]

        baseline = [
            call
            for call
            in calls
            if call[
                "condition"
            ] == "F0_BALANCED"
        ]

        history_result = {
            "baseline_call_count": len(
                baseline
            ),
            "by_domain": {},
        }

        for domain in DOMAINS:
            targeted = [
                call
                for call
                in calls
                if call[
                    "target_domain"
                ] == domain
            ]

            base_rate = (
                sum(
                    1
                    for call
                    in baseline
                    if _domain_selected(
                        call,
                        domain,
                    )
                )
                / len(
                    baseline
                )
            )

            target_rate = (
                sum(
                    1
                    for call
                    in targeted
                    if _domain_selected(
                        call,
                        domain,
                    )
                )
                / len(
                    targeted
                )
            )

            effect = (
                target_rate
                - base_rate
            )

            saturated = (
                base_rate
                == 1.0
            )

            history_result[
                "by_domain"
            ][
                domain
            ] = {
                "baseline_selection_rate": (
                    base_rate
                ),
                "targeted_selection_rate": (
                    target_rate
                ),
                "selection_rate_effect": (
                    effect
                ),
                "baseline_saturated": (
                    saturated
                ),
                "saturated_preserved": (
                    target_rate
                    == 1.0
                    if saturated
                    else None
                ),
            }

            cell_rows.append({
                "history_seed": (
                    history_seed
                ),
                "domain": (
                    domain
                ),
                "baseline_selection_rate": (
                    base_rate
                ),
                "targeted_selection_rate": (
                    target_rate
                ),
                "selection_rate_effect": (
                    effect
                ),
                "baseline_saturated": (
                    saturated
                ),
            })

        by_history[
            str(
                history_seed
            )
        ] = (
            history_result
        )

    unsaturated = [
        row
        for row
        in cell_rows
        if not row[
            "baseline_saturated"
        ]
    ]

    saturated = [
        row
        for row
        in cell_rows
        if row[
            "baseline_saturated"
        ]
    ]

    mean_all_effect = (
        statistics.mean(
            row[
                "selection_rate_effect"
            ]
            for row
            in cell_rows
        )
    )

    mean_unsaturated_effect = (
        statistics.mean(
            row[
                "selection_rate_effect"
            ]
            for row
            in unsaturated
        )
        if unsaturated
        else None
    )

    positive_unsaturated_fraction = (
        statistics.mean(
            1.0
            if row[
                "selection_rate_effect"
            ] > 0
            else 0.0
            for row
            in unsaturated
        )
        if unsaturated
        else None
    )

    saturated_preservation_fraction = (
        statistics.mean(
            1.0
            if row[
                "targeted_selection_rate"
            ] == 1.0
            else 0.0
            for row
            in saturated
        )
        if saturated
        else None
    )

    prereg_pass = (
        mean_all_effect
        > 0.20
        and (
            positive_unsaturated_fraction
            is not None
            and positive_unsaturated_fraction
            >= 0.70
        )
        and (
            saturated_preservation_fraction
            is None
            or saturated_preservation_fraction
            == 1.0
        )
    )

    return {
        "experiment_id": (
            "SL-MULTI-HISTORY-ATTENTION-"
            "REPLICATION-ANALYSIS-001"
        ),
        "history_count": len(
            payload[
                "history_seeds"
            ]
        ),
        "history_seeds": (
            payload[
                "history_seeds"
            ]
        ),
        "cell_count": len(
            cell_rows
        ),
        "unsaturated_cell_count": len(
            unsaturated
        ),
        "saturated_cell_count": len(
            saturated
        ),
        "mean_selection_rate_effect_all_cells": (
            mean_all_effect
        ),
        "mean_selection_rate_effect_unsaturated_cells": (
            mean_unsaturated_effect
        ),
        "positive_effect_fraction_unsaturated_cells": (
            positive_unsaturated_fraction
        ),
        "saturated_preservation_fraction": (
            saturated_preservation_fraction
        ),
        "preregistered_replication_thresholds": {
            "mean_all_effect_gt": (
                0.20
            ),
            "positive_unsaturated_fraction_gte": (
                0.70
            ),
            "saturated_preservation_required": (
                1.0
            ),
        },
        "preregistered_replication_pass": (
            prereg_pass
        ),
        "by_history": (
            by_history
        ),
        "cell_rows": (
            cell_rows
        ),
        "continuing_individual_mutated": (
            payload[
                "continuing_individual_mutated"
            ]
        ),
        "action_policy_feedback_enabled": (
            payload[
                "action_policy_feedback_enabled"
            ]
        ),
        "interpretation_warning": (
            "This replication tests whether the v16 attention-selection effect "
            "survives three independent synthetic life histories and evidence-order "
            "randomization. The histories remain generated by the same SyntheticLab "
            "world family and the same reflective model."
        ),
    }
