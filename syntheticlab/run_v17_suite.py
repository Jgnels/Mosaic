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

from dagmay_synthetic_lab.attention_competition_result_analysis_v2 import (
    analyze_attention_competition_result_v2,
)
from dagmay_synthetic_lab.multi_history_attention_offline_tests import (
    run_multi_history_attention_offline_tests,
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
    real = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-ATTENTION-COMPETITION-REAL-REFLECTION-PILOT-001.json"
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

    result_analysis = (
        analyze_attention_competition_result_v2(
            real
        )
    )

    offline = (
        run_multi_history_attention_offline_tests(
            longitudinal
        )
    )

    advocate = (
        canonical_advocate_selection_manifest()
    )

    canary_ok, canary_missing = (
        constitutional_canary()
    )

    result = {
        "lab_version": "17.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V17.0-"
            "MULTI-HISTORY-ATTENTION-REPLICATION"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "v16_real_result_analysis_v2": (
            result_analysis
        ),
        "multi_history_offline_validation": (
            offline
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

    assert result_analysis[
        "all_unsaturated_domains_positive"
    ] is True

    assert result_analysis[
        "by_domain"
    ][
        "agency"
    ][
        "baseline_selected_count"
    ] == 0

    assert result_analysis[
        "by_domain"
    ][
        "agency"
    ][
        "targeted_selected_count"
    ] == 4

    assert result_analysis[
        "continuity_behaves_as_dominant_anchor"
    ] is True

    assert offline[
        "failure_triggered"
    ] is True

    assert offline[
        "checkpointed_before_failure"
    ] == 13

    assert offline[
        "resume_transport_invocations"
    ] == 23

    assert offline[
        "final_call_count"
    ] == 36

    assert offline[
        "presentation_order_randomized_by_evidence_id"
    ] is True

    assert offline[
        "all_conditions_keep_all_four_evidence_channels"
    ] is True

    assert offline[
        "continuing_individual_mutated"
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
        / "syntheticlab-v17.0-results-latest.json"
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
        "SyntheticLab v17.0 integrated suite PASS"
    )
    print(
        "All unsaturated v16 domains positive:",
        result_analysis[
            "all_unsaturated_domains_positive"
        ],
    )
    print(
        "Agency exact effect:",
        result_analysis[
            "by_domain"
        ][
            "agency"
        ][
            "selection_rate_effect"
        ],
    )
    print(
        "Multi-history offline calls:",
        offline[
            "final_call_count"
        ],
    )
    print(
        "Order randomization:",
        offline[
            "presentation_order_randomized_by_evidence_id"
        ],
    )


if __name__ == "__main__":
    main()
