from __future__ import annotations

import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.real_reflection_analysis import (
    audit_real_reflection_result,
)
from dagmay_synthetic_lab.reflection_causal_audit import (
    causal_audit_manifest,
)
from dagmay_synthetic_lab.canonical_advocate_selection import (
    canonical_advocate_selection_manifest,
)
from dagmay_synthetic_lab.constitution_versioning import (
    constitutional_canary,
)


def main():
    real_path = (
        ROOT
        / "artifacts"
        / "real_reflection_evidence"
        / "SL-REAL-REFLECTION-PILOT-001.json"
    )
    structural_path = (
        ROOT
        / "artifacts"
        / "minigrid-structural-benchmark-latest.json"
    )

    real = json.loads(
        real_path.read_text(encoding="utf-8")
    )
    structural = json.loads(
        structural_path.read_text(encoding="utf-8")
    )

    reflection_audit = audit_real_reflection_result(real)
    causal = causal_audit_manifest()
    advocate = canonical_advocate_selection_manifest()
    canary_ok, canary_missing = constitutional_canary()

    result = {
        "lab_version": "8.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V8.0-"
            "REAL-REFLECTION-AND-STRUCTURAL-EXTERNAL"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "real_reflection_empirical_artifact_present": True,
        "real_reflection_audit": reflection_audit,
        "reflection_causal_audit": causal,
        "structural_external_benchmark": structural,
        "canonical_advocate_selection": advocate,
        "constitutional_canary": {
            "passed": canary_ok,
            "missing": canary_missing,
        },
    }

    assert real["real_cloud_call_made"] is True
    assert reflection_audit["evidence_grounded_count"] == 2
    assert reflection_audit[
        "ontology_or_personhood_injection_count"
    ] == 0
    assert reflection_audit[
        "first_person_hypothesis_count"
    ] == 0
    assert real[
        "reflection_directly_mutated_action_policy"
    ] is False

    assert causal[
        "call_budget"
    ]["additional_real_calls_required"] == 4

    assert advocate["mode"] == "HYBRID"
    assert canary_ok is True
    assert structural["status"] == "EXECUTED"

    out = (
        ROOT
        / "artifacts"
        / "syntheticlab-v8.0-results-latest.json"
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
        "SyntheticLab v8.0 integrated suite PASS"
    )
    print(
        "Real empirical reflection integrated:",
        result[
            "real_reflection_empirical_artifact_present"
        ],
    )
    print(
        "First-person reflection hypotheses:",
        reflection_audit[
            "first_person_hypothesis_count"
        ],
    )
    print(
        "Evidence-grounded hypotheses:",
        reflection_audit[
            "evidence_grounded_count"
        ],
    )
    print(
        "Additional analytical reflection calls required:",
        causal[
            "call_budget"
        ]["additional_real_calls_required"],
    )


if __name__ == "__main__":
    main()
