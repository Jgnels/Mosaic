from __future__ import annotations

import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.replication_commit_readiness import (
    assess_replication_commit_readiness,
)
from dagmay_synthetic_lab.canonical_promotion_offline_tests import (
    run_canonical_promotion_offline_tests,
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
    analysis = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-MULTI-HISTORY-ATTENTION-REPLICATION-ANALYSIS-001.json"
        ).read_text(encoding="utf-8")
    )

    readiness = assess_replication_commit_readiness(
        analysis
    )

    promotion = run_canonical_promotion_offline_tests()

    advocate = canonical_advocate_selection_manifest()
    canary_ok, canary_missing = constitutional_canary()

    result = {
        "lab_version": "18.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V18.0-"
            "CANONICAL-SELF-MODEL-PROMOTION-GATE"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "replication_commit_readiness": readiness,
        "canonical_promotion_offline_validation": promotion,
        "canonical_promotion_currently_authorized": False,
        "canonical_self_model_mutated_by_suite": False,
        "action_policy_feedback_enabled": False,
        "canonical_advocate_selection": advocate,
        "constitutional_canary": {
            "passed": canary_ok,
            "missing": canary_missing,
        },
        "base_self_tests": run_self_tests(),
    }

    assert readiness[
        "preregistered_replication_pass"
    ] is True

    assert readiness[
        "canonical_commit_technically_preparable"
    ] is True

    assert readiness[
        "canonical_commit_human_approval_required"
    ] is True

    assert readiness[
        "canonical_commit_currently_authorized"
    ] is False

    assert promotion[
        "unauthorized_promotion_blocked"
    ] is True

    assert promotion[
        "authorized_promotion_state_changed"
    ] is True

    assert promotion[
        "cross_domain_candidate_authorized"
    ] is False

    assert promotion[
        "ontology_candidate_authorized"
    ] is False

    assert promotion[
        "action_policy_feedback_enabled"
    ] is False

    assert advocate[
        "mode"
    ] == "HYBRID"

    assert canary_ok is True

    out = (
        ROOT
        / "artifacts"
        / "syntheticlab-v18.0-results-latest.json"
    )

    out.write_text(
        json.dumps(
            result,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    print("SyntheticLab v18.0 integrated suite PASS")
    print(
        "Replication passed:",
        readiness[
            "preregistered_replication_pass"
        ],
    )
    print(
        "Canonical commit technically preparable:",
        readiness[
            "canonical_commit_technically_preparable"
        ],
    )
    print(
        "Canonical commit authorized:",
        readiness[
            "canonical_commit_currently_authorized"
        ],
    )
    print(
        "Unauthorized promotion blocked:",
        promotion[
            "unauthorized_promotion_blocked"
        ],
    )
    print(
        "Action-policy feedback enabled:",
        promotion[
            "action_policy_feedback_enabled"
        ],
    )


if __name__ == "__main__":
    main()
