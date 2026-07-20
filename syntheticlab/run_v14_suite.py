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

from dagmay_synthetic_lab.retrieval_attention_result_analysis import (
    analyze_retrieval_attention_result,
)
from dagmay_synthetic_lab.retrieval_attention_experiments import (
    run_retrieval_attention_experiment,
)
from dagmay_synthetic_lab.retrieval_attention_reflection_pilot_offline_tests import (
    run_retrieval_attention_reflection_offline_tests,
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

    full_payload = json.loads(
        (
            ROOT
            / "artifacts"
            / "retrieval-attention-feedback-latest.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    full_analysis = (
        analyze_retrieval_attention_result(
            full_payload
        )
    )

    # Small execution smoke test. The full 64-seed artifact is preserved as the
    # primary result; this smoke run validates executable release code.
    smoke = (
        run_retrieval_attention_experiment(
            longitudinal_analysis=(
                longitudinal
            ),
            seed_count=8,
            history_steps=450,
            retrieval_cycles=40,
        )
    )

    pilot_offline = (
        run_retrieval_attention_reflection_offline_tests(
            longitudinal
        )
    )

    # The underlying architecture was already ready at v13; the human research
    # lead has now approved the bounded retrieval/attention level.
    feedback = assess_feedback_gate(
        causal_perspective_identification_rate=1.0,
        causal_perspective_permutation_rate=1.0,
        causal_ablation_effect=.517578125,
        deictic_focal_binding_rate=1.0,
        shadow_policy_unchanged=True,
        shadow_enacted_count=0,
        human_approved_retrieval_attention=True,
    )

    advocate = (
        canonical_advocate_selection_manifest()
    )
    canary_ok, canary_missing = (
        constitutional_canary()
    )

    result = {
        "lab_version": "14.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V14.0-"
            "RETRIEVAL-ATTENTION-FEEDBACK"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "full_64_seed_result_analysis": (
            full_analysis
        ),
        "release_smoke_test": {
            key: value
            for key, value
            in smoke.items()
            if key != "rows"
        },
        "real_reflection_pilot_offline_validation": (
            pilot_offline
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

    assert full_analysis[
        "exact_prefork_rate"
    ] == 1.0

    assert full_analysis[
        "lived_state_unchanged_rate"
    ] == 1.0

    assert full_analysis[
        "zero_strength_exact_equivalence_rate"
    ] == 1.0

    assert full_analysis[
        "target_match_true_vs_control"
    ][
        "mean"
    ] > .25

    assert full_analysis[
        "target_match_true_vs_control"
    ][
        "positive_pair_fraction"
    ] == 1.0

    assert full_analysis[
        "target_match_true_vs_shuffled"
    ][
        "mean"
    ] > .30

    assert full_analysis[
        "cognitive_state_divergence_rate_f0_vs_f1"
    ] == 1.0

    assert full_analysis[
        "fabricated_memory_count_total"
    ] == 0

    assert smoke[
        "exact_prefork_rate"
    ] == 1.0

    assert smoke[
        "lived_state_unchanged_rate"
    ] == 1.0

    assert smoke[
        "zero_strength_exact_equivalence_rate"
    ] == 1.0

    assert smoke[
        "true_index_target_match_effect"
    ] > 0

    assert pilot_offline[
        "failure_triggered"
    ] is True

    assert pilot_offline[
        "checkpointed_before_failure"
    ] == 4

    assert pilot_offline[
        "resume_transport_invocations"
    ] == 8

    assert pilot_offline[
        "final_call_count"
    ] == 12

    assert feedback.current_level.value == (
        "RETRIEVAL_ATTENTION_ONLY"
    )

    assert (
        feedback.maximum_automatically_allowed_level.value
        == "RETRIEVAL_ATTENTION_ONLY"
    )

    assert feedback.action_policy_feedback_enabled is False

    assert advocate[
        "mode"
    ] == "HYBRID"

    assert canary_ok is True

    out = (
        ROOT
        / "artifacts"
        / "syntheticlab-v14.0-results-latest.json"
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
        "SyntheticLab v14.0 integrated suite PASS"
    )
    print(
        "Approved feedback level:",
        feedback.current_level.value,
    )
    print(
        "Full exact-prefork rate:",
        full_analysis[
            "exact_prefork_rate"
        ],
    )
    print(
        "F1 minus F0 target-match effect:",
        full_analysis[
            "target_match_true_vs_control"
        ][
            "mean"
        ],
    )
    print(
        "F1 minus shuffled specificity effect:",
        full_analysis[
            "target_match_true_vs_shuffled"
        ][
            "mean"
        ],
    )
    print(
        "Cognitive state divergence rate:",
        full_analysis[
            "cognitive_state_divergence_rate_f0_vs_f1"
        ],
    )
    print(
        "Action-policy feedback enabled:",
        feedback.action_policy_feedback_enabled,
    )


if __name__ == "__main__":
    main()
