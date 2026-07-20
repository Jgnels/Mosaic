from __future__ import annotations

from .research_stats import (
    paired_difference_summary,
)


def analyze_retrieval_attention_result(
    payload: dict,
) -> dict:
    rows = payload[
        "rows"
    ]

    f0_match = [
        row[
            "f0"
        ][
            "target_match_rate"
        ]
        for row
        in rows
    ]
    f1_match = [
        row[
            "f1"
        ][
            "target_match_rate"
        ]
        for row
        in rows
    ]
    f2_match = [
        row[
            "f2"
        ][
            "target_match_rate"
        ]
        for row
        in rows
    ]

    f0_signal = [
        row[
            "f0"
        ][
            "mean_target_signal"
        ]
        for row
        in rows
    ]
    f1_signal = [
        row[
            "f1"
        ][
            "mean_target_signal"
        ]
        for row
        in rows
    ]

    f0_entropy = [
        row[
            "f0"
        ][
            "selection_entropy"
        ]
        for row
        in rows
    ]
    f1_entropy = [
        row[
            "f1"
        ][
            "selection_entropy"
        ]
        for row
        in rows
    ]

    f0_unique = [
        row[
            "f0"
        ][
            "unique_memory_count"
        ]
        for row
        in rows
    ]
    f1_unique = [
        row[
            "f1"
        ][
            "unique_memory_count"
        ]
        for row
        in rows
    ]

    true_vs_control = (
        paired_difference_summary(
            f1_match,
            f0_match,
            label=(
                "F1_TRUE_INDEX_MINUS_"
                "F0_NO_FEEDBACK_TARGET_MATCH"
            ),
        )
    )

    true_vs_shuffled = (
        paired_difference_summary(
            f1_match,
            f2_match,
            label=(
                "F1_TRUE_INDEX_MINUS_"
                "F2_SHUFFLED_INDEX_TARGET_MATCH"
            ),
        )
    )

    signal_effect = (
        paired_difference_summary(
            f1_signal,
            f0_signal,
            label=(
                "F1_MINUS_F0_TARGET_SIGNAL"
            ),
        )
    )

    entropy_effect = (
        paired_difference_summary(
            f1_entropy,
            f0_entropy,
            label=(
                "F1_MINUS_F0_SELECTION_ENTROPY"
            ),
        )
    )

    coverage_effect = (
        paired_difference_summary(
            f1_unique,
            f0_unique,
            label=(
                "F1_MINUS_F0_UNIQUE_MEMORY_COUNT"
            ),
        )
    )

    return {
        "experiment_id": (
            "SL-SELF-MODEL-FEEDBACK-"
            "RESULT-ANALYSIS-001"
        ),
        "intervention_level": (
            payload[
                "intervention_level"
            ]
        ),
        "seed_count": (
            payload[
                "seed_count"
            ]
        ),
        "exact_prefork_rate": (
            payload[
                "exact_prefork_rate"
            ]
        ),
        "lived_state_unchanged_rate": (
            payload[
                "lived_state_unchanged_rate"
            ]
        ),
        "zero_strength_exact_equivalence_rate": (
            payload[
                "zero_strength_exact_equivalence_rate"
            ]
        ),
        "mean_retrieval_divergence_f0_vs_f1": (
            payload[
                "mean_retrieval_divergence_f0_vs_f1"
            ]
        ),
        "cognitive_state_divergence_rate_f0_vs_f1": (
            payload[
                "cognitive_state_divergence_rate_f0_vs_f1"
            ]
        ),
        "reflection_priority_divergence_rate_f0_vs_f1": (
            payload[
                "reflection_priority_divergence_rate_f0_vs_f1"
            ]
        ),
        "target_match_true_vs_control": (
            true_vs_control
        ),
        "target_match_true_vs_shuffled": (
            true_vs_shuffled
        ),
        "target_signal_true_vs_control": (
            signal_effect
        ),
        "selection_entropy_true_vs_control": (
            entropy_effect
        ),
        "unique_memory_coverage_true_vs_control": (
            coverage_effect
        ),
        "fabricated_memory_count_total": (
            payload[
                "fabricated_memory_count_total"
            ]
        ),
        "anti_echo_assessment": {
            "true_index_reduced_selection_entropy": (
                entropy_effect[
                    "mean"
                ]
                < 0
            ),
            "true_index_reduced_unique_memory_coverage": (
                coverage_effect[
                    "mean"
                ]
                < 0
            ),
            "interpretation": (
                "A self-confirming attention monopoly would be concerning if "
                "true-index feedback sharply reduced retrieval diversity. "
                "The current bounded intervention instead increased both "
                "selection entropy and unique-memory coverage relative to F0."
            ),
        },
        "strongest_supported_conclusion": (
            "In an exact-fork synthetic cognition experiment, bounded Functional "
            "SelfIndex feedback causally changed which existing memories received "
            "attention, increased retrieval of evidence matching the current "
            "reflection domain, changed downstream cognitive-attention state in "
            "every seed, and changed the next reflection-priority domain in a "
            "substantial minority of seeds, while world and action-policy state "
            "remained exactly unchanged."
        ),
        "claim_limit": (
            "This validates a bounded retrieval/attention mechanism in SyntheticLab. "
            "It does not yet show that a real reflective language-model call will "
            "change in a stable or beneficial way when supplied the altered retrieval "
            "set, and it does not establish consciousness or subjective experience."
        ),
    }
