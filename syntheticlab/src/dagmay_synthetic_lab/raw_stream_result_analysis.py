from __future__ import annotations

import statistics


def analyze_raw_stream_result(
    payload: dict,
) -> dict:
    calls = payload["calls"]

    confidences = [
        float(call["confidence"])
        for call in calls
    ]

    rationale_present = [
        bool(
            str(
                call.get(
                    "rationale",
                    "",
                )
            ).strip()
        )
        for call in calls
    ]

    # Detect whether the provider's rationale explicitly relied on schema presence
    # rather than temporal/causal relationships.
    schema_terms = (
        "only stream containing",
        "fields 'a'",
        "q0",
        "q1",
        "recall probe",
        "action-channel tokens",
    )

    schema_salient = []
    for call in calls:
        rationale = str(
            call.get(
                "rationale",
                "",
            )
        ).lower()
        schema_salient.append(
            any(
                term in rationale
                for term in schema_terms
            )
        )

    return {
        "experiment_id": (
            "SL-RAW-STREAM-OWNERSHIP-"
            "RESULT-ANALYSIS-001"
        ),
        "all_six_correct": (
            payload[
                "all_six_correct"
            ]
        ),
        "both_cases_label_invariant": (
            payload[
                "both_cases_label_invariant"
            ]
        ),
        "case_results": (
            payload[
                "cases"
            ]
        ),
        "mean_confidence": (
            statistics.mean(
                confidences
            )
        ),
        "minimum_confidence": (
            min(
                confidences
            )
        ),
        "nonempty_rationale_rate": (
            statistics.mean(
                1.0
                if present
                else 0.0
                for present
                in rationale_present
            )
        ),
        "schema_salient_rationale_rate": (
            statistics.mean(
                1.0
                if salient
                else 0.0
                for salient
                in schema_salient
            )
        ),
        "continuing_individual_mutated": (
            payload[
                "continuing_individual_mutated"
            ]
        ),
        "strongest_supported_claim": (
            "Across two independently generated raw-event histories and three "
            "arbitrary label permutations per history, the provider selected "
            "the correct structurally privileged stream on all six calls."
        ),
        "important_remaining_confound": (
            "The focal stream exposed a richer field schema than distractor "
            "streams. Provider rationales frequently relied on the presence of "
            "action-channel, private-state, and recall fields. Therefore the "
            "experiment does not cleanly isolate causal/temporal inference from "
            "schema-presence recognition."
        ),
        "next_required_control": (
            "Give every stream the same field schema and matched marginals. "
            "Vary only causal action-state coupling, temporal stationarity, "
            "recall coherence, and continuity structure."
        ),
    }
