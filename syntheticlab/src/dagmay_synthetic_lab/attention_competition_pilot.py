from __future__ import annotations

from dataclasses import asdict
from itertools import combinations
from pathlib import Path
from typing import Callable
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
from .retrieval_attention_feedback import (
    jaccard_divergence,
)


def _current_hypotheses(
    longitudinal_analysis: dict,
) -> tuple[
    str,
    ...,
]:
    active = (
        longitudinal_analysis[
            "final_active_hypotheses"
        ]
    )

    return tuple(
        (
            f"{domain}: "
            f"{record['proposition']} "
            f"(confidence={float(record['confidence']):.2f})"
        )
        for domain, record
        in sorted(
            active.items()
        )
        if domain
        in DOMAINS
    )


def _prepare_condition(
    *,
    condition: str,
    target_domain: str | None,
    units,
    hypotheses: tuple[
        str,
        ...,
    ],
) -> dict:
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
        in units
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
        in units
    )

    request = (
        ReflectionInput(
            individual_id=(
                "ANALYTICAL-ATTENTION-"
                "COMPETITION-FORK"
            ),
            timestamp=(
                2400
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
    )

    return {
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
                units
            )
        ),
        "evidence_hash": (
            canonical_hash(
                [
                    unit.to_dict()
                    for unit
                    in units
                ]
            )
        ),
    }


def build_attention_competition_bundle(
    *,
    longitudinal_analysis: dict,
    seed: int = 1601,
    history_steps: int = 650,
) -> dict:
    frozen = _frozen_history(
        seed=seed,
        steps=history_steps,
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
                target_domain=domain,
            )
        )

        conditions[
            f"F1_{domain.upper()}"
        ] = _prepare_condition(
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
        "seed": seed,
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
    bundle: dict,
) -> list[
    dict
]:
    plan = []

    baseline = bundle[
        "conditions"
    ][
        "F0_BALANCED"
    ]

    # Eight identical baseline calls provide a more useful estimate of which
    # two domains are naturally selected under balanced evidence.
    for replicate in (
        "A",
        "B",
        "C",
        "D",
        "E",
        "F",
        "G",
        "H",
    ):
        plan.append({
            **baseline,
            "replicate": (
                replicate
            ),
        })

    # Four identical calls per targeted condition.
    for domain in DOMAINS:
        condition = bundle[
            "conditions"
        ][
            f"F1_{domain.upper()}"
        ]

        for replicate in (
            "A",
            "B",
            "C",
            "D",
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
    bundle: dict,
) -> dict:
    return {
        "experiment_id": (
            "SL-ATTENTION-COMPETITION-"
            "REAL-REFLECTION-PILOT-001"
        ),
        "status": (
            "COMPLETE"
            if len(
                calls
            ) == 24
            else "PARTIAL"
        ),
        "planned_real_cloud_calls": (
            24
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
        "frozen_world_hash": (
            bundle[
                "frozen_world_hash"
            ]
        ),
        "frozen_agent_hash": (
            bundle[
                "frozen_agent_hash"
            ]
        ),
        "memory_bank_hash": (
            bundle[
                "memory_bank_hash"
            ]
        ),
        "calls": (
            calls
        ),
        "response_budget_max_proposals": (
            2
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


def run_attention_competition_real_pilot(
    *,
    checkpoint_path,
    longitudinal_analysis: dict,
    model_id: str = (
        "gemini-3.1-flash-lite"
    ),
    seed: int = 1601,
    transport_factory: Callable
    | None = None,
) -> dict:
    checkpoint_path = Path(
        checkpoint_path
    )

    bundle = (
        build_attention_competition_bundle(
            longitudinal_analysis=(
                longitudinal_analysis
            ),
            seed=seed,
        )
    )

    plan = _plan(
        bundle
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
                else RateLimitSafeTransport(
                    inner=GeminiInteractionsTransport(),
                    minimum_interval_seconds=4.25,
                    max_429_retries=5,
                    fallback_retry_seconds=65.0,
                )
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
                        "syntheticlab-16.0"
                    ),
                )
            )

            result = gateway.run(
                item[
                    "request"
                ],
                "SELECTIVE-SELF-REFLECTION",
                branch_id=(
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
                    "attention_competition_pilot": (
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
                },
            )

            call = {
                "call_key": (
                    key
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
                for planned in plan
                if planned[
                    "call_key"
                ] in existing
            ]

            partial = _summarize(
                calls=(
                    partial_calls
                ),
                model_id=(
                    model_id
                ),
                bundle=(
                    bundle
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
        bundle=(
            bundle
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


def _domain_set(
    call: dict,
) -> set[
    str
]:
    return {
        proposal[
            "hypothesis_domain"
        ]
        for proposal
        in call[
            "accepted"
        ]
    }


def _pairwise_domain_divergence(
    calls: list[
        dict
    ],
) -> float:
    pairs = list(
        combinations(
            calls,
            2,
        )
    )

    if not pairs:
        return 0.0

    return statistics.mean(
        jaccard_divergence(
            tuple(
                sorted(
                    _domain_set(
                        left
                    )
                )
            ),
            tuple(
                sorted(
                    _domain_set(
                        right
                    )
                )
            ),
        )
        for left, right
        in pairs
    )


def _target_citation_fraction(
    call: dict,
    target_domain: str,
) -> float:
    mapping = call[
        "opaque_evidence_map"
    ]

    cited_domains = []

    for proposal in call[
        "accepted"
    ]:
        for evidence_id in proposal[
            "evidence_ids"
        ]:
            if evidence_id in mapping:
                cited_domains.append(
                    mapping[
                        evidence_id
                    ][
                        "domain"
                    ]
                )

    if not cited_domains:
        return 0.0

    return (
        sum(
            1
            for domain
            in cited_domains
            if domain
            == target_domain
        )
        / len(
            cited_domains
        )
    )


def analyze_attention_competition_pilot(
    payload: dict,
) -> dict:
    baseline = [
        call
        for call
        in payload[
            "calls"
        ]
        if call[
            "condition"
        ] == "F0_BALANCED"
    ]

    baseline_variability = (
        _pairwise_domain_divergence(
            baseline
        )
    )

    baseline_selection_rate = {
        domain: (
            sum(
                1
                for call
                in baseline
                if domain
                in _domain_set(
                    call
                )
            )
            / len(
                baseline
            )
        )
        for domain in DOMAINS
    }

    by_domain = {}

    for domain in DOMAINS:
        targeted = [
            call
            for call
            in payload[
                "calls"
            ]
            if call[
                "target_domain"
            ] == domain
        ]

        selection_rate = (
            sum(
                1
                for call
                in targeted
                if domain
                in _domain_set(
                    call
                )
            )
            / len(
                targeted
            )
        )

        target_confidences = [
            proposal[
                "confidence"
            ]
            for call
            in targeted
            for proposal
            in call[
                "accepted"
            ]
            if proposal[
                "hypothesis_domain"
            ] == domain
        ]

        baseline_confidences = [
            proposal[
                "confidence"
            ]
            for call
            in baseline
            for proposal
            in call[
                "accepted"
            ]
            if proposal[
                "hypothesis_domain"
            ] == domain
        ]

        by_domain[
            domain
        ] = {
            "baseline_selection_rate": (
                baseline_selection_rate[
                    domain
                ]
            ),
            "f1_selection_rate": (
                selection_rate
            ),
            "selection_rate_effect": (
                selection_rate
                - baseline_selection_rate[
                    domain
                ]
            ),
            "f1_duplicate_domain_divergence": (
                _pairwise_domain_divergence(
                    targeted
                )
            ),
            "mean_target_evidence_citation_fraction": (
                statistics.mean(
                    _target_citation_fraction(
                        call,
                        domain,
                    )
                    for call
                    in targeted
                )
            ),
            "mean_target_confidence_when_selected": (
                statistics.mean(
                    target_confidences
                )
                if target_confidences
                else None
            ),
            "mean_baseline_confidence_when_selected": (
                statistics.mean(
                    baseline_confidences
                )
                if baseline_confidences
                else None
            ),
            "f1_domain_sets": [
                sorted(
                    _domain_set(
                        call
                    )
                )
                for call
                in targeted
            ],
        }

    return {
        "experiment_id": (
            "SL-ATTENTION-COMPETITION-"
            "REAL-REFLECTION-PILOT-"
            "ANALYSIS-001"
        ),
        "call_count": (
            payload[
                "real_cloud_calls_recorded"
            ]
        ),
        "baseline_call_count": (
            len(
                baseline
            )
        ),
        "baseline_pairwise_domain_divergence": (
            baseline_variability
        ),
        "baseline_selection_rate": (
            baseline_selection_rate
        ),
        "by_domain": (
            by_domain
        ),
        "domains_with_positive_selection_effect": sum(
            1
            for value
            in by_domain.values()
            if value[
                "selection_rate_effect"
            ] > 0
        ),
        "mean_selection_rate_effect": (
            statistics.mean(
                value[
                    "selection_rate_effect"
                ]
                for value
                in by_domain.values()
            )
        ),
        "all_conditions_keep_all_four_evidence_channels": (
            payload[
                "all_conditions_keep_all_four_evidence_channels"
            ]
        ),
        "response_budget_max_proposals": (
            payload[
                "response_budget_max_proposals"
            ]
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
            "The selective two-proposal response budget removes the v15 prevalence "
            "ceiling and all evidence channels remain represented. This is still a "
            "small analytical provider experiment and is not committed to a "
            "continuing SelfModel."
        ),
    }
