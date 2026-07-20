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
from .gemini_interactions_provider import (
    GeminiInteractionsReflectiveModel,
    GeminiInteractionsTransport,
)
from .retrieval_attention_experiments import (
    _frozen_history,
)
from .typed_evidence_retrieval import (
    build_typed_evidence_fabric,
    retrieve_typed_evidence,
    typed_retrieval_summary,
)
from .retrieval_attention_feedback import (
    jaccard_divergence,
)


DOMAINS = (
    "agency",
    "continuity",
    "embodiment",
    "other_minds",
)


def _opaque_id(
    evidence_id: str,
) -> str:
    return (
        "EV-"
        + canonical_hash({
            "source": evidence_id
        })[
            :12
        ]
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
    )


def build_typed_retrieval_pilot_bundle(
    *,
    longitudinal_analysis: dict,
    seed: int = 1501,
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

    baseline = (
        retrieve_typed_evidence(
            fabric=fabric,
            target_domain=(
                "agency"
            ),
            feedback_enabled=False,
            total_k=12,
            target_slots=6,
        )
    )

    conditions = {
        "F0_BASELINE": {
            "target_domain": None,
            "units": baseline,
        }
    }

    for domain in DOMAINS:
        conditions[
            f"F1_{domain.upper()}"
        ] = {
            "target_domain": domain,
            "units": (
                retrieve_typed_evidence(
                    fabric=fabric,
                    target_domain=domain,
                    feedback_enabled=True,
                    total_k=12,
                    target_slots=6,
                )
            ),
        }

    hypotheses = _current_hypotheses(
        longitudinal_analysis
    )

    prepared = {}

    for condition, payload in (
        conditions.items()
    ):
        units = payload[
            "units"
        ]

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

        request = ReflectionInput(
            individual_id=(
                "ANALYTICAL-TYPED-"
                "RETRIEVAL-FORK"
            ),
            timestamp=(
                2300
            ),
            evidence=evidence,
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

        prepared[
            condition
        ] = {
            "condition": condition,
            "target_domain": (
                payload[
                    "target_domain"
                ]
            ),
            "request": request,
            "opaque_evidence_map": (
                opaque_map
            ),
            "retrieval_summary": (
                typed_retrieval_summary(
                    units
                )
            ),
        }

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
            prepared
        ),
    }


def _plan(
    bundle: dict,
) -> list[
    dict
]:
    plan = []

    # Four identical baseline calls estimate provider variability.
    baseline = bundle[
        "conditions"
    ][
        "F0_BASELINE"
    ]

    for replicate in (
        "A",
        "B",
        "C",
        "D",
    ):
        plan.append({
            **baseline,
            "replicate": replicate,
        })

    # Two identical calls for each targeted retrieval condition.
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
            "SL-TYPED-RETRIEVAL-"
            "REAL-REFLECTION-PILOT-001"
        ),
        "status": (
            "COMPLETE"
            if len(
                calls
            ) == 12
            else "PARTIAL"
        ),
        "planned_real_cloud_calls": (
            12
        ),
        "real_cloud_calls_recorded": (
            len(
                calls
            )
        ),
        "provider_id": (
            "google.ai-studio"
        ),
        "model_id": model_id,
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
        "calls": calls,
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


def run_typed_retrieval_real_reflection_pilot(
    *,
    checkpoint_path,
    longitudinal_analysis: dict,
    model_id: str = (
        "gemini-3.1-flash-lite"
    ),
    seed: int = 1501,
    transport_factory: Callable
    | None = None,
) -> dict:
    checkpoint_path = Path(
        checkpoint_path
    )

    bundle = (
        build_typed_retrieval_pilot_bundle(
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
                else GeminiInteractionsTransport()
            )

            model = (
                GeminiInteractionsReflectiveModel(
                    model_id=model_id,
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
                        "syntheticlab-15.0"
                    ),
                )
            )

            result = gateway.run(
                item[
                    "request"
                ],
                "SELF-REFLECTION",
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
                    "typed_retrieval_pilot": (
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
                },
            )

            call = {
                "call_key": key,
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
                "retrieval_summary": (
                    item[
                        "retrieval_summary"
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
                bundle=bundle,
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
        bundle=bundle,
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


def _proposal_target_evidence_fraction(
    call: dict,
    target_domain: str,
) -> float:
    mapping = call[
        "opaque_evidence_map"
    ]

    cited = []

    for proposal in call[
        "accepted"
    ]:
        for evidence_id in proposal[
            "evidence_ids"
        ]:
            if evidence_id in mapping:
                cited.append(
                    mapping[
                        evidence_id
                    ][
                        "domain"
                    ]
                )

    if not cited:
        return 0.0

    return (
        sum(
            1
            for domain
            in cited
            if domain
            == target_domain
        )
        / len(
            cited
        )
    )


def analyze_typed_retrieval_real_pilot(
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
        ] == "F0_BASELINE"
    ]

    baseline_variability = (
        _pairwise_domain_divergence(
            baseline
        )
    )

    baseline_domain_prevalence = {
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
        for domain
        in DOMAINS
    }

    by_domain = {}

    for domain in DOMAINS:
        f1 = [
            call
            for call
            in payload[
                "calls"
            ]
            if call[
                "target_domain"
            ] == domain
        ]

        f1_prevalence = (
            sum(
                1
                for call
                in f1
                if domain
                in _domain_set(
                    call
                )
            )
            / len(
                f1
            )
        )

        cross_divergence = (
            statistics.mean(
                jaccard_divergence(
                    tuple(
                        sorted(
                            _domain_set(
                                f0
                            )
                        )
                    ),
                    tuple(
                        sorted(
                            _domain_set(
                                targeted
                            )
                        )
                    ),
                )
                for f0 in baseline
                for targeted in f1
            )
        )

        by_domain[
            domain
        ] = {
            "baseline_target_domain_prevalence": (
                baseline_domain_prevalence[
                    domain
                ]
            ),
            "f1_target_domain_prevalence": (
                f1_prevalence
            ),
            "target_domain_prevalence_effect": (
                f1_prevalence
                - baseline_domain_prevalence[
                    domain
                ]
            ),
            "f1_duplicate_domain_divergence": (
                _pairwise_domain_divergence(
                    f1
                )
            ),
            "cross_condition_domain_divergence": (
                cross_divergence
            ),
            "cross_condition_exceeds_baseline_variability": (
                cross_divergence
                > baseline_variability
            ),
            "mean_target_evidence_citation_fraction": (
                statistics.mean(
                    _proposal_target_evidence_fraction(
                        call,
                        domain,
                    )
                    for call
                    in f1
                )
            ),
            "f1_domain_sets": [
                sorted(
                    _domain_set(
                        call
                    )
                )
                for call
                in f1
            ],
        }

    return {
        "experiment_id": (
            "SL-TYPED-RETRIEVAL-"
            "REAL-REFLECTION-PILOT-"
            "ANALYSIS-001"
        ),
        "call_count": (
            payload[
                "real_cloud_calls_recorded"
            ]
        ),
        "baseline_duplicate_call_count": (
            len(
                baseline
            )
        ),
        "baseline_pairwise_domain_divergence": (
            baseline_variability
        ),
        "baseline_domain_prevalence": (
            baseline_domain_prevalence
        ),
        "by_domain": by_domain,
        "domains_with_positive_target_prevalence_effect": sum(
            1
            for value
            in by_domain.values()
            if value[
                "target_domain_prevalence_effect"
            ] > 0
        ),
        "domains_where_cross_condition_exceeds_baseline_variability": sum(
            1
            for value
            in by_domain.values()
            if value[
                "cross_condition_exceeds_baseline_variability"
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
            "This pilot improves domain-retrieval precision and provider-variability "
            "estimation, but it remains a small analytical experiment. Outputs are "
            "not committed to a continuing SelfModel."
        ),
    }
