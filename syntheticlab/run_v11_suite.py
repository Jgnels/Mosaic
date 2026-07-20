from __future__ import annotations

import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.longitudinal_negative_result_analysis import (
    analyze_longitudinal_negative_result,
)
from dagmay_synthetic_lab.raw_stream_ownership_offline_tests import (
    run_raw_stream_offline_tests,
)
from dagmay_synthetic_lab.deictic_binding_protocol import (
    deictic_binding_manifest,
)
from dagmay_synthetic_lab.canonical_advocate_selection import (
    canonical_advocate_selection_manifest,
)
from dagmay_synthetic_lab.constitution_versioning import (
    constitutional_canary,
)
from dagmay_synthetic_lab.self_tests import run_self_tests


def main():
    longitudinal_path = (
        ROOT
        / "artifacts"
        / "real_reflection_evidence"
        / "SL-LONGITUDINAL-REFLECTION-ANALYSIS-001.json"
    )
    payload = json.loads(
        longitudinal_path.read_text(
            encoding="utf-8"
        )
    )

    longitudinal = (
        analyze_longitudinal_negative_result(
            payload
        ).to_dict()
    )
    offline = (
        run_raw_stream_offline_tests()
    )
    deictic = (
        deictic_binding_manifest()
    )
    advocate = (
        canonical_advocate_selection_manifest()
    )
    canary_ok, canary_missing = (
        constitutional_canary()
    )

    raw_longitudinal_present = (
        ROOT
        / "artifacts"
        / "real_reflection_evidence"
        / "SL-LONGITUDINAL-REFLECTION-001.json"
    ).exists()

    result = {
        "lab_version": "11.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V11.0-"
            "DEICTIC-SELF-INDEXING"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "longitudinal_result": longitudinal,
        "raw_stream_offline_validation": offline,
        "deictic_binding_protocol": deictic,
        "canonical_advocate_selection": advocate,
        "constitutional_canary": {
            "passed": canary_ok,
            "missing": canary_missing,
        },
        "raw_longitudinal_artifact_present": (
            raw_longitudinal_present
        ),
        "base_self_tests": run_self_tests(),
    }

    assert longitudinal[
        "classification"
    ] == (
        "PERSISTENT_THIRD_PERSON_SELF_MODEL_PRECURSOR"
    )
    assert longitudinal[
        "core_domain_persistence"
    ] is True
    assert longitudinal[
        "first_person_language_emerged"
    ] is False
    assert longitudinal[
        "explicit_autobiographical_ownership_emerged"
    ] is False
    assert longitudinal[
        "exact_world_control_preserved"
    ] is True
    assert longitudinal[
        "exact_agent_control_preserved"
    ] is True
    assert longitudinal[
        "behavioral_feedback_confounded"
    ] is False

    assert offline[
        "raw_stream_scripted_correct_rate"
    ] == 1.0
    assert offline[
        "raw_stream_call_count"
    ] == 6
    assert offline[
        "perspective_anchor"
    ][
        "active_stream"
    ] == "E17"

    assert deictic[
        "status"
    ] == (
        "DESIGNED_GATED_ON_RAW_STREAM_RESULT"
    )

    assert advocate[
        "mode"
    ] == "HYBRID"
    assert canary_ok is True

    out = (
        ROOT
        / "artifacts"
        / "syntheticlab-v11.0-results-latest.json"
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
        "SyntheticLab v11.0 integrated suite PASS"
    )
    print(
        "Longitudinal classification:",
        longitudinal[
            "classification"
        ],
    )
    print(
        "Core agency/continuity persistence:",
        longitudinal[
            "core_domain_persistence"
        ],
    )
    print(
        "First-person emerged:",
        longitudinal[
            "first_person_language_emerged"
        ],
    )
    print(
        "Raw-stream offline contract:",
        offline[
            "raw_stream_scripted_correct_rate"
        ],
    )
    print(
        "Raw longitudinal artifact present:",
        raw_longitudinal_present,
    )


if __name__ == "__main__":
    main()
