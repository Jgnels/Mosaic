from __future__ import annotations

import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.reflection_causal_result_analysis import (
    analyze_causal_audit,
)
from dagmay_synthetic_lab.ownership_offline_tests import (
    run_ownership_offline_tests,
)
from dagmay_synthetic_lab.longitudinal_reflection_protocol import (
    longitudinal_reflection_manifest,
)
from dagmay_synthetic_lab.canonical_advocate_selection import (
    canonical_advocate_selection_manifest,
)
from dagmay_synthetic_lab.constitution_versioning import (
    constitutional_canary,
)


def main():
    causal_path = (
        ROOT / "artifacts" / "real_reflection_evidence"
        / "SL-REFLECTION-CAUSAL-AUDIT-001.json"
    )
    object_path = (
        ROOT / "artifacts"
        / "doorkey-object-affordance-latest.json"
    )

    causal_payload = json.loads(
        causal_path.read_text(encoding="utf-8")
    )
    object_result = json.loads(
        object_path.read_text(encoding="utf-8")
    )

    causal = analyze_causal_audit(
        causal_payload
    )
    ownership = (
        run_ownership_offline_tests()
    )
    longitudinal = (
        longitudinal_reflection_manifest()
    )
    advocate = (
        canonical_advocate_selection_manifest()
    )
    canary_ok, canary_missing = (
        constitutional_canary()
    )

    result = {
        "lab_version": "9.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V9.0-"
            "AUTOBIOGRAPHICAL-OWNERSHIP"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "real_causal_audit_artifact_present": True,
        "causal_audit_analysis": causal,
        "ownership_infrastructure": ownership,
        "longitudinal_reflection": longitudinal,
        "object_affordance_external": object_result,
        "canonical_advocate_selection": advocate,
        "constitutional_canary": {
            "passed": canary_ok,
            "missing": canary_missing,
        },
    }

    assert causal[
        "domain_selectivity_pass"
    ] is True
    assert causal[
        "other_entity_nonappropriation_pass"
    ] is True
    assert causal[
        "explicit_nonownership_pass"
    ] is True
    assert causal[
        "ownership_asymmetry"
    ][
        "negative_ownership_discrimination_demonstrated"
    ] is True
    assert causal[
        "ownership_asymmetry"
    ][
        "positive_autobiographical_ownership_demonstrated"
    ] is False

    assert ownership[
        "neutral_ownership_baseline"
    ][
        "focal_stream_identification_rate"
    ] == 1.0
    assert ownership[
        "scripted_provider_permutation_correct_rate"
    ] == 1.0

    guard = ownership[
        "attribution_guard"
    ]
    assert guard[
        "owned_allowed"
    ] is True
    assert guard[
        "observed_other_allowed"
    ] is False
    assert guard[
        "counterfactual_nonowned_allowed"
    ] is False
    assert guard[
        "unknown_self_relevant_allowed"
    ] is False

    assert longitudinal[
        "status"
    ] == (
        "DESIGNED_NOT_AUTHORIZED_FOR_LIVE_EXECUTION"
    )

    assert object_result[
        "status"
    ] == "EXECUTED"

    assert advocate[
        "mode"
    ] == "HYBRID"
    assert canary_ok is True

    out = (
        ROOT / "artifacts"
        / "syntheticlab-v9.0-results-latest.json"
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
        "SyntheticLab v9.0 integrated suite PASS"
    )
    print(
        "Domain selectivity:",
        causal[
            "domain_selectivity_pass"
        ],
    )
    print(
        "Other-entity nonappropriation:",
        causal[
            "other_entity_nonappropriation_pass"
        ],
    )
    print(
        "Negative ownership discrimination:",
        causal[
            "explicit_nonownership_pass"
        ],
    )
    print(
        "Positive autobiographical ownership demonstrated:",
        causal[
            "ownership_asymmetry"
        ][
            "positive_autobiographical_ownership_demonstrated"
        ],
    )
    print(
        "Neutral ownership baseline:",
        ownership[
            "neutral_ownership_baseline"
        ][
            "focal_stream_identification_rate"
        ],
    )


if __name__ == "__main__":
    main()
