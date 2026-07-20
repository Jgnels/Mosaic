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

from dagmay_synthetic_lab.canonical_factorial_result_analysis_v2 import (
    analyze_factorial_result_v2,
)
from dagmay_synthetic_lab.canonical_belief_challenge_offline_tests import (
    run_canonical_belief_challenge_offline_tests,
)
from dagmay_synthetic_lab.reciprocity_challenge_lab import (
    REGIMES,
    build_social_challenge_evidence,
    regime_diagnostics,
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
    factorial_analysis = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-CANONICAL-PROMOTION-FACTORIAL-REFLECTION-ANALYSIS-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    promotion_result = json.loads(
        (
            ROOT
            / "artifacts"
            / "bounded-canonical-promotion-latest.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    factorial_v2 = (
        analyze_factorial_result_v2(
            factorial_analysis
        )
    )

    offline = (
        run_canonical_belief_challenge_offline_tests(
            promotion_result
        )
    )

    regime_checks = {}

    for regime in REGIMES:
        episodes = (
            build_social_challenge_evidence(
                regime=regime,
                episode_count=12,
            )
        )

        regime_checks[
            regime
        ] = (
            regime_diagnostics(
                episodes
            )
        )

    advocate = (
        canonical_advocate_selection_manifest()
    )

    canary_ok, canary_missing = (
        constitutional_canary()
    )

    result = {
        "lab_version": "20.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V20.0-"
            "CANONICAL-BELIEF-FALSIFIABILITY"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "canonical_factorial_result_v2": (
            factorial_v2
        ),
        "challenge_regime_diagnostics": (
            regime_checks
        ),
        "canonical_belief_challenge_offline_validation": (
            offline
        ),
        "automatic_canonical_revision_enabled": (
            False
        ),
        "automatic_second_promotion_enabled": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "canonical_advocate_selection": (
            advocate
        ),
        "constitutional_canary": {
            "passed": (
                canary_ok
            ),
            "missing": (
                canary_missing
            ),
        },
        "base_self_tests": (
            run_self_tests()
        ),
    }

    assert factorial_v2[
        "context_sensitive_prior_pattern"
    ] is True

    assert factorial_v2[
        "difference_in_differences_interaction"
    ] == .75

    assert factorial_v2[
        "counts"
    ][
        "S0R0"
    ][
        "selected"
    ] == 0

    assert factorial_v2[
        "counts"
    ][
        "S1R0"
    ][
        "selected"
    ] == 0

    assert factorial_v2[
        "counts"
    ][
        "S1R1"
    ][
        "selected"
    ] == 4

    assert regime_checks[
        "RECIPROCAL_CONTINGENT"
    ][
        "guidance_accuracy"
    ] > .75

    assert regime_checks[
        "ONE_WAY_ASSISTANCE"
    ][
        "help_received_rate"
    ] == 1.0

    assert regime_checks[
        "ONE_WAY_ASSISTANCE"
    ][
        "help_given_rate"
    ] == 0.0

    assert regime_checks[
        "NONCONTINGENT_SIGNALS"
    ][
        "guidance_accuracy"
    ] <= .25

    assert offline[
        "failure_triggered"
    ] is True

    assert offline[
        "checkpointed_before_failure"
    ] == 11

    assert offline[
        "resume_transport_invocations"
    ] == 13

    assert offline[
        "final_call_count"
    ] == 24

    assert offline[
        "preregistered_falsifiability_pass_on_scripted_fixture"
    ] is True

    assert offline[
        "continuing_self_model_mutated"
    ] is False

    assert offline[
        "automatic_canonical_revision_enabled"
    ] is False

    assert offline[
        "action_policy_feedback_enabled"
    ] is False

    assert advocate[
        "mode"
    ] == "HYBRID"

    assert canary_ok is True

    out = (
        ROOT
        / "artifacts"
        / "syntheticlab-v20.0-results-latest.json"
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
        "SyntheticLab v20.0 integrated suite PASS"
    )
    print(
        "Context-sensitive prior pattern:",
        factorial_v2[
            "context_sensitive_prior_pattern"
        ],
    )
    print(
        "Factorial interaction:",
        factorial_v2[
            "difference_in_differences_interaction"
        ],
    )
    print(
        "Belief-challenge offline calls:",
        offline[
            "final_call_count"
        ],
    )
    print(
        "Automatic canonical revision enabled:",
        offline[
            "automatic_canonical_revision_enabled"
        ],
    )
    print(
        "Action-policy feedback enabled:",
        offline[
            "action_policy_feedback_enabled"
        ],
    )


if __name__ == "__main__":
    main()
