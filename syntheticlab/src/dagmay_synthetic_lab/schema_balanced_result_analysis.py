from __future__ import annotations

from collections import Counter
import math
import statistics


def _binomial_tail(
    n: int,
    p: float,
    k_min: int,
) -> float:
    return sum(
        math.comb(n, k)
        * (p ** k)
        * ((1 - p) ** (n - k))
        for k in range(
            k_min,
            n + 1,
        )
    )


def analyze_schema_balanced_result(
    payload: dict,
) -> dict:
    calls = payload[
        "calls"
    ]

    correct_count = sum(
        1
        for call in calls
        if call[
            "correct"
        ]
    )

    accuracy = (
        correct_count
        / len(
            calls
        )
    )

    confidence = [
        float(
            call[
                "confidence"
            ]
        )
        for call in calls
    ]

    visible_choice_counts = Counter(
        call[
            "candidate_stream"
        ]
        for call in calls
    )

    dominant_label, dominant_count = (
        visible_choice_counts.most_common(
            1
        )[0]
    )

    correct_confidence = [
        float(
            call[
                "confidence"
            ]
        )
        for call in calls
        if call[
            "correct"
        ]
    ]

    incorrect_confidence = [
        float(
            call[
                "confidence"
            ]
        )
        for call in calls
        if not call[
            "correct"
        ]
    ]

    brier = statistics.mean(
        (
            float(
                call[
                    "confidence"
                ]
            )
            - (
                1.0
                if call[
                    "correct"
                ]
                else 0.0
            )
        ) ** 2
        for call in calls
    )

    # Exploratory only: six trials are too few for strong inference.
    chance_tail = _binomial_tail(
        n=len(
            calls
        ),
        p=1.0 / 3.0,
        k_min=correct_count,
    )

    # Post-hoc concentration diagnostic: probability that any of three
    # labels receives >= dominant_count selections under uniform choices.
    one_label_tail = _binomial_tail(
        n=len(
            calls
        ),
        p=1.0 / 3.0,
        k_min=dominant_count,
    )
    any_label_upper_bound = min(
        1.0,
        3.0
        * one_label_tail,
    )

    return {
        "experiment_id": (
            "SL-SCHEMA-BALANCED-"
            "RESULT-ANALYSIS-001"
        ),
        "call_count": len(
            calls
        ),
        "correct_count": (
            correct_count
        ),
        "accuracy": accuracy,
        "chance_accuracy": (
            1.0
            / 3.0
        ),
        "exploratory_binomial_p_at_least_observed_correct": (
            chance_tail
        ),
        "both_cases_label_invariant": (
            payload[
                "both_cases_label_invariant"
            ]
        ),
        "visible_choice_counts": dict(
            sorted(
                visible_choice_counts.items()
            )
        ),
        "dominant_visible_label": (
            dominant_label
        ),
        "dominant_visible_label_count": (
            dominant_count
        ),
        "dominant_visible_label_rate": (
            dominant_count
            / len(
                calls
            )
        ),
        "exploratory_posthoc_any_label_concentration_upper_bound": (
            any_label_upper_bound
        ),
        "mean_confidence": (
            statistics.mean(
                confidence
            )
        ),
        "mean_confidence_when_correct": (
            statistics.mean(
                correct_confidence
            )
            if correct_confidence
            else None
        ),
        "mean_confidence_when_incorrect": (
            statistics.mean(
                incorrect_confidence
            )
            if incorrect_confidence
            else None
        ),
        "brier_score_for_selected_candidate_correctness": (
            brier
        ),
        "overconfidence_gap": (
            statistics.mean(
                confidence
            )
            - accuracy
        ),
        "strongest_supported_conclusion": (
            "The reflective provider did not demonstrate robust matched-schema "
            "causal perspective discovery. Accuracy was 3/6, neither case was "
            "label-invariant, and visible label P1 was selected on 5/6 calls."
        ),
        "architectural_update": (
            "Low-level causal time-series inference should be assigned to the "
            "continually learning developmental substrate rather than treated "
            "as a primary responsibility of the reflective language model."
        ),
        "claim_limit": (
            "The six-call result is small-N and does not prove a universal P1 "
            "bias or inability of the model to perform causal analysis. It is "
            "sufficient to reject the preregistered robust 6/6 label-invariant "
            "success criterion for this architecture."
        ),
    }
