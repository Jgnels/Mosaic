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

from dagmay_synthetic_lab.raw_stream_result_analysis import (
    analyze_raw_stream_result,
)
from dagmay_synthetic_lab.schema_balanced_offline_tests import (
    run_schema_balanced_offline_tests,
)
from dagmay_synthetic_lab.longitudinal_negative_result_analysis import (
    analyze_longitudinal_negative_result,
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
    raw_payload = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-RAW-STREAM-OWNERSHIP-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    longitudinal_payload = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-LONGITUDINAL-REFLECTION-ANALYSIS-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    raw = (
        analyze_raw_stream_result(
            raw_payload
        )
    )
    schema_balanced = (
        run_schema_balanced_offline_tests()
    )
    longitudinal = (
        analyze_longitudinal_negative_result(
            longitudinal_payload
        ).to_dict()
    )

    advocate = (
        canonical_advocate_selection_manifest()
    )
    canary_ok, canary_missing = (
        constitutional_canary()
    )

    result = {
        "lab_version": "12.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V12.0-"
            "SCHEMA-BALANCED-CAUSAL-PERSPECTIVE"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "raw_stream_real_result": raw,
        "schema_balanced_offline_validation": (
            schema_balanced
        ),
        "longitudinal_result": (
            longitudinal
        ),
        "deictic_binding_status": (
            "DESIGNED_GATED_ON_SCHEMA_BALANCED_RESULT"
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

    assert raw[
        "all_six_correct"
    ] is True
    assert raw[
        "both_cases_label_invariant"
    ] is True
    assert raw[
        "mean_confidence"
    ] >= .98
    assert raw[
        "schema_salient_rationale_rate"
    ] >= .50

    baseline = (
        schema_balanced[
            "baseline"
        ]
    )
    assert baseline[
        "identification_rate"
    ] == 1.0
    assert baseline[
        "same_field_schema_for_all_streams"
    ] is True
    assert baseline[
        "matched_action_marginals"
    ] is True
    assert baseline[
        "matched_recall_schedule"
    ] is True
    assert baseline[
        "stable_continuity_token_for_all_streams"
    ] is True

    assert schema_balanced[
        "scripted_provider_correct_rate"
    ] == 1.0

    resume = (
        schema_balanced[
            "resume_regression"
        ]
    )
    assert resume[
        "failure_triggered"
    ] is True
    assert resume[
        "checkpointed_before_failure"
    ] == 3
    assert resume[
        "resume_transport_invocations"
    ] == 3
    assert resume[
        "final_status"
    ] == "COMPLETE"
    assert resume[
        "all_six_correct"
    ] is True

    assert longitudinal[
        "classification"
    ] == (
        "PERSISTENT_THIRD_PERSON_SELF_MODEL_PRECURSOR"
    )
    assert longitudinal[
        "exact_world_control_preserved"
    ] is True
    assert longitudinal[
        "exact_agent_control_preserved"
    ] is True

    assert advocate[
        "mode"
    ] == "HYBRID"
    assert canary_ok is True

    out = (
        ROOT
        / "artifacts"
        / "syntheticlab-v12.0-results-latest.json"
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
        "SyntheticLab v12.0 integrated suite PASS"
    )
    print(
        "Raw-stream 6/6:",
        raw[
            "all_six_correct"
        ],
    )
    print(
        "Raw-stream label invariance:",
        raw[
            "both_cases_label_invariant"
        ],
    )
    print(
        "Raw-stream schema-salient rationale rate:",
        raw[
            "schema_salient_rationale_rate"
        ],
    )
    print(
        "Schema-balanced 128-seed baseline:",
        baseline[
            "identification_rate"
        ],
    )
    print(
        "Deictic binding status:",
        result[
            "deictic_binding_status"
        ],
    )


if __name__ == "__main__":
    main()
