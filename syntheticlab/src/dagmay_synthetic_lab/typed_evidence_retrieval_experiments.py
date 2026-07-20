from __future__ import annotations

import statistics

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


def run_typed_evidence_retrieval_experiment(
    *,
    seed_count: int = 64,
    history_steps: int = 650,
) -> dict:
    rows = []

    for seed in range(
        1,
        seed_count + 1,
    ):
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

        baseline_summary = (
            typed_retrieval_summary(
                baseline
            )
        )

        for domain in DOMAINS:
            intervention = (
                retrieve_typed_evidence(
                    fabric=fabric,
                    target_domain=domain,
                    feedback_enabled=True,
                    total_k=12,
                    target_slots=6,
                )
            )

            intervention_summary = (
                typed_retrieval_summary(
                    intervention
                )
            )

            base_target = (
                baseline_summary[
                    "domain_counts"
                ].get(
                    domain,
                    0,
                )
                / len(
                    baseline
                )
            )

            intervention_target = (
                intervention_summary[
                    "domain_counts"
                ].get(
                    domain,
                    0,
                )
                / len(
                    intervention
                )
            )

            rows.append({
                "seed": seed,
                "domain": domain,
                "fabric_count": len(
                    fabric[
                        domain
                    ]
                ),
                "baseline_target_rate": (
                    base_target
                ),
                "intervention_target_rate": (
                    intervention_target
                ),
                "target_rate_effect": (
                    intervention_target
                    - base_target
                ),
                "retrieval_divergence": (
                    jaccard_divergence(
                        tuple(
                            unit.evidence_id
                            for unit
                            in baseline
                        ),
                        tuple(
                            unit.evidence_id
                            for unit
                            in intervention
                        ),
                    )
                ),
                "baseline_hash": (
                    baseline_summary[
                        "evidence_hash"
                    ]
                ),
                "intervention_hash": (
                    intervention_summary[
                        "evidence_hash"
                    ]
                ),
            })

    domain_results = {}

    for domain in DOMAINS:
        subset = [
            row
            for row in rows
            if row[
                "domain"
            ] == domain
        ]

        domain_results[
            domain
        ] = {
            "mean_fabric_count": (
                statistics.mean(
                    row[
                        "fabric_count"
                    ]
                    for row
                    in subset
                )
            ),
            "minimum_fabric_count": min(
                row[
                    "fabric_count"
                ]
                for row
                in subset
            ),
            "mean_baseline_target_rate": (
                statistics.mean(
                    row[
                        "baseline_target_rate"
                    ]
                    for row
                    in subset
                )
            ),
            "mean_intervention_target_rate": (
                statistics.mean(
                    row[
                        "intervention_target_rate"
                    ]
                    for row
                    in subset
                )
            ),
            "mean_target_rate_effect": (
                statistics.mean(
                    row[
                        "target_rate_effect"
                    ]
                    for row
                    in subset
                )
            ),
            "positive_effect_fraction": (
                statistics.mean(
                    1.0
                    if row[
                        "target_rate_effect"
                    ] > 0
                    else 0.0
                    for row
                    in subset
                )
            ),
            "mean_retrieval_divergence": (
                statistics.mean(
                    row[
                        "retrieval_divergence"
                    ]
                    for row
                    in subset
                )
            ),
            "nonzero_divergence_fraction": (
                statistics.mean(
                    1.0
                    if row[
                        "retrieval_divergence"
                    ] > 0
                    else 0.0
                    for row
                    in subset
                )
            ),
        }

    return {
        "experiment_id": (
            "SL-TYPED-EVIDENCE-"
            "RETRIEVAL-001"
        ),
        "seed_count": seed_count,
        "history_steps": (
            history_steps
        ),
        "domain_results": (
            domain_results
        ),
        "all_domains_positive_target_effect": all(
            result[
                "mean_target_rate_effect"
            ] > 0
            for result
            in domain_results.values()
        ),
        "all_domains_nonzero_retrieval_divergence": all(
            result[
                "nonzero_divergence_fraction"
            ] == 1.0
            for result
            in domain_results.values()
        ),
        "continuity_uses_structural_persistence_traces": (
            True
        ),
        "single_memory_age_is_not_continuity_match_rule": (
            True
        ),
        "rows": rows,
    }
