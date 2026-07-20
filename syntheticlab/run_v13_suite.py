from __future__ import annotations

import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(
    0,
    str(
        ROOT
        / "src"
    ),
)

from dagmay_synthetic_lab.schema_balanced_result_analysis import (
    analyze_schema_balanced_result,
)
from dagmay_synthetic_lab.causal_perspective_experiments import (
    run_causal_perspective_experiments,
)
from dagmay_synthetic_lab.deictic_binding_experiments import (
    run_deictic_binding_experiments,
)
from dagmay_synthetic_lab.functional_self_index_experiments import (
    run_functional_self_index_experiments,
)
from dagmay_synthetic_lab.provider_reliability_experiments import (
    run_provider_reliability_experiment,
)
from dagmay_synthetic_lab.self_model_feedback_gate import (
    assess_feedback_gate,
)
from dagmay_synthetic_lab.canonical_advocate_selection import (
    canonical_advocate_selection_manifest,
)
from dagmay_synthetic_lab.constitution_versioning import (
    constitutional_canary,
)
from dagmay_synthetic_lab.self_tests import (
    run_self_tests,
)


def main():
    schema_payload = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-SCHEMA-BALANCED-OWNERSHIP-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    longitudinal = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-LONGITUDINAL-REFLECTION-ANALYSIS-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    schema = (
        analyze_schema_balanced_result(
            schema_payload
        )
    )

    causal = (
        run_causal_perspective_experiments(
            seed_count=512
        )
    )

    deictic = (
        run_deictic_binding_experiments(
            longitudinal
        )
    )

    functional = (
        run_functional_self_index_experiments(
            longitudinal_analysis=(
                longitudinal
            ),
            deictic_binding_result=(
                deictic
            ),
        )
    )

    reliability = (
        run_provider_reliability_experiment(
            schema_payload
        )
    )

    feedback = assess_feedback_gate(
        causal_perspective_identification_rate=(
            causal[
                "identification_rate"
            ]
        ),
        causal_perspective_permutation_rate=(
            causal[
                "label_permutation_invariance_rate"
            ]
        ),
        causal_ablation_effect=(
            causal[
                "causal_ablation_effect"
            ]
        ),
        deictic_focal_binding_rate=(
            deictic[
                "focal_binding_rate"
            ]
        ),
        shadow_policy_unchanged=(
            functional[
                "policy_state_unchanged"
            ]
        ),
        shadow_enacted_count=(
            functional[
                "shadow_enacted_count"
            ]
        ),
    )

    advocate = (
        canonical_advocate_selection_manifest()
    )
    canary_ok, canary_missing = (
        constitutional_canary()
    )

    result = {
        "lab_version": "13.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V13.0-"
            "LEARNED-CAUSAL-PERSPECTIVE-AND-DEICTIC-BINDING"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "schema_balanced_real_result": (
            schema
        ),
        "causal_perspective_learner": (
            causal
        ),
        "deictic_binding": (
            deictic
        ),
        "functional_self_index": (
            functional
        ),
        "provider_reliability": (
            reliability
        ),
        "feedback_gate": (
            feedback.to_dict()
        ),
        "canonical_advocate_selection": (
            advocate
        ),
        "constitutional_canary": {
            "passed": canary_ok,
            "missing": (
                canary_missing
            ),
        },
        "base_self_tests": (
            run_self_tests()
        ),
    }

    assert schema[
        "correct_count"
    ] == 3
    assert schema[
        "both_cases_label_invariant"
    ] is False
    assert schema[
        "dominant_visible_label"
    ] == "P1"
    assert schema[
        "dominant_visible_label_count"
    ] == 5

    assert causal[
        "identification_rate"
    ] == 1.0
    assert causal[
        "label_permutation_invariance_rate"
    ] == 1.0
    assert causal[
        "causal_ablation_effect"
    ] >= .25

    assert deictic[
        "focal_binding_rate"
    ] == 1.0
    assert deictic[
        "anchor_swap_converts_original_focal_to_other_rate"
    ] == 1.0

    assert functional[
        "indexed_hypothesis_count"
    ] == 4
    assert functional[
        "shadow_enacted_count"
    ] == 0
    assert functional[
        "policy_state_unchanged"
    ] is True

    assert reliability[
        "empirical_accuracy"
    ] == .5
    assert reliability[
        "mean_provider_confidence"
    ] >= .95

    assert feedback.current_level.value == (
        "SHADOW_ONLY"
    )
    assert (
        feedback.maximum_automatically_allowed_level.value
        == "SHADOW_ONLY"
    )
    assert feedback.action_policy_feedback_enabled is False
    assert (
        feedback.explicit_human_approval_required_for_next_level
        is True
    )

    assert advocate[
        "mode"
    ] == "HYBRID"
    assert canary_ok is True

    out = (
        ROOT
        / "artifacts"
        / "syntheticlab-v13.0-results-latest.json"
    )

    out.write_text(
        json.dumps(
            result,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    print(
        "SyntheticLab v13.0 integrated suite PASS"
    )
    print(
        "Schema-balanced provider accuracy:",
        schema[
            "accuracy"
        ],
    )
    print(
        "Causal Perspective Learner identification:",
        causal[
            "identification_rate"
        ],
    )
    print(
        "Causal Perspective label invariance:",
        causal[
            "label_permutation_invariance_rate"
        ],
    )
    print(
        "Functional SelfIndex hypotheses:",
        functional[
            "indexed_hypothesis_count"
        ],
    )
    print(
        "Current feedback level:",
        feedback.current_level.value,
    )
    print(
        "Human approval required for next level:",
        feedback.explicit_human_approval_required_for_next_level,
    )


if __name__ == "__main__":
    main()
