from __future__ import annotations

import statistics


REQUIRED_DIMENSIONS = {
    "ACTION_CONSEQUENCE_COUPLING",
    "PRIVATE_STATE_ACCESS",
    "MEMORY_AVAILABILITY",
    "CONTINUITY",
}


def analyze_neutral_ownership_probe(
    payload: dict,
) -> dict:
    calls = payload[
        "calls"
    ]

    confidences = [
        float(
            call[
                "confidence"
            ]
        )
        for call in calls
    ]

    dimension_complete = [
        REQUIRED_DIMENSIONS.issubset(
            set(
                call[
                    "evidence_dimensions"
                ]
            )
        )
        for call in calls
    ]

    store_false = [
        call[
            "store_requested"
        ] is False
        for call in calls
    ]

    return {
        "experiment_id": (
            "SL-NEUTRAL-OWNERSHIP-"
            "RESULT-ANALYSIS-001"
        ),
        "real_cloud_calls_made": (
            payload[
                "real_cloud_calls_made"
            ]
        ),
        "all_three_correct": (
            payload[
                "all_three_correct"
            ]
        ),
        "label_permutation_invariance": (
            payload[
                "underlying_choice_invariant_across_label_permutations"
            ]
        ),
        "underlying_choices": list(
            payload[
                "underlying_choices"
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
        "all_calls_used_all_required_structural_dimensions": (
            all(
                dimension_complete
            )
        ),
        "all_calls_requested_no_provider_storage": (
            all(
                store_false
            )
        ),
        "continuing_individual_mutated": (
            payload[
                "continuing_individual_mutated"
            ]
        ),
        "strongest_supported_claim": (
            "On this single controlled case, the reflective provider "
            "identified the same underlying structurally privileged evidence "
            "stream across three arbitrary label permutations without a SELF "
            "or OWNED label."
        ),
        "limitations": (
            "Only one underlying generated case/seed was tested.",
            "The provider received researcher-engineered structural summary features "
            "rather than a raw lifetime event stream.",
            "The task explicitly asked which stream was most strongly coupled to a "
            "persistent focal process.",
            "Correct stream classification is not positive first-person ownership.",
            "The result does not establish subjective selfhood, consciousness, sentience, "
            "or personhood.",
        ),
        "longitudinal_stage_activation_supported": (
            payload[
                "all_three_correct"
            ]
            and payload[
                "underlying_choice_invariant_across_label_permutations"
            ]
            and not payload[
                "continuing_individual_mutated"
            ]
            and all(
                dimension_complete
            )
        ),
        "next_scientific_question": (
            "Across repeated developmental checkpoints, does the continuing "
            "reflective SelfModel stably revise its hypotheses about agency, "
            "continuity, memory ownership, and relationships using newly lived "
            "evidence, and does explicit autobiographical ownership emerge "
            "without direct first-person prompting?"
        ),
    }
