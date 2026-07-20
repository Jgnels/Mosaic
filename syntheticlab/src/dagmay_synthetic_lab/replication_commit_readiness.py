from __future__ import annotations


def assess_replication_commit_readiness(
    analysis: dict,
) -> dict:
    prereg_pass = bool(
        analysis[
            "preregistered_replication_pass"
        ]
    )

    by_history = analysis[
        "by_history"
    ]

    agency_effects = [
        by_history[
            history
        ][
            "by_domain"
        ][
            "agency"
        ][
            "selection_rate_effect"
        ]
        for history
        in sorted(
            by_history
        )
    ]

    other_minds_effects = [
        by_history[
            history
        ][
            "by_domain"
        ][
            "other_minds"
        ][
            "selection_rate_effect"
        ]
        for history
        in sorted(
            by_history
        )
    ]

    saturated_domains = {
        domain
        for history_payload
        in by_history.values()
        for domain, payload
        in history_payload[
            "by_domain"
        ].items()
        if payload[
            "baseline_saturated"
        ]
    }

    agency_heterogeneous = (
        min(
            agency_effects
        )
        <= 0.0
        and max(
            agency_effects
        )
        > 0.0
    )

    other_minds_consistent = all(
        effect > 0.0
        for effect
        in other_minds_effects
    )

    return {
        "experiment_id": (
            "SL-CANONICAL-COMMIT-"
            "READINESS-ASSESSMENT-001"
        ),
        "preregistered_replication_pass": (
            prereg_pass
        ),
        "mean_selection_rate_effect_all_cells": (
            analysis[
                "mean_selection_rate_effect_all_cells"
            ]
        ),
        "mean_selection_rate_effect_unsaturated_cells": (
            analysis[
                "mean_selection_rate_effect_unsaturated_cells"
            ]
        ),
        "positive_effect_fraction_unsaturated_cells": (
            analysis[
                "positive_effect_fraction_unsaturated_cells"
            ]
        ),
        "saturated_preservation_fraction": (
            analysis[
                "saturated_preservation_fraction"
            ]
        ),
        "agency_effects_by_history": (
            agency_effects
        ),
        "agency_effect_heterogeneous": (
            agency_heterogeneous
        ),
        "other_minds_effects_by_history": (
            other_minds_effects
        ),
        "other_minds_effect_consistent": (
            other_minds_consistent
        ),
        "domains_saturated_in_at_least_one_history": (
            sorted(
                saturated_domains
            )
        ),
        "mechanism_replication_ready": (
            prereg_pass
        ),
        "all_domain_specificity_established": (
            False
        ),
        "canonical_commit_technically_preparable": (
            prereg_pass
        ),
        "canonical_commit_human_approval_required": (
            True
        ),
        "canonical_commit_currently_authorized": (
            False
        ),
        "strongest_supported_conclusion": (
            "The scarce reflective-attention mechanism replicated across independent "
            "histories under the preregistered thresholds. Domain-specific behavior "
            "remains heterogeneous: other_minds was consistently responsive, agency "
            "failed in one history, and continuity/embodiment were saturated in all "
            "three new baselines. This is sufficient to prepare a bounded canonical "
            "promotion experiment, but not to claim uniform domain control."
        ),
    }
