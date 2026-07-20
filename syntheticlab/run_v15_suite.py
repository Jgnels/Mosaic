from __future__ import annotations

import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.retrieval_reflection_mediation_analysis import (
    analyze_retrieval_reflection_mediation,
)
from dagmay_synthetic_lab.typed_evidence_retrieval_experiments import (
    run_typed_evidence_retrieval_experiment,
)
from dagmay_synthetic_lab.typed_retrieval_reflection_offline_tests import (
    run_typed_retrieval_reflection_offline_tests,
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
            / "SL-RETRIEVAL-ATTENTION-REAL-REFLECTION-PILOT-001.json"
        ).read_text(encoding="utf-8")
    )

    longitudinal = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-LONGITUDINAL-REFLECTION-ANALYSIS-001.json"
        ).read_text(encoding="utf-8")
    )

    mediation = (
        analyze_retrieval_reflection_mediation(
            real
        )
    )

    typed_smoke = (
        run_typed_evidence_retrieval_experiment(
            seed_count=8,
            history_steps=450,
        )
    )

    offline = (
        run_typed_retrieval_reflection_offline_tests(
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
        "lab_version": "15.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V15.0-"
            "RETRIEVAL-REFLECTION-MEDIATION-AND-TYPED-EVIDENCE"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "real_pilot_mediation": (
            mediation
        ),
        "typed_retrieval_smoke": {
            key: value
            for key, value
            in typed_smoke.items()
            if key != "rows"
        },
        "typed_real_pilot_offline_validation": (
            offline
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

    assert mediation[
        "perfect_observed_binary_alignment"
    ] is True

    assert mediation[
        "target_domain_gained_count"
    ] == 1

    assert mediation[
        "target_domain_lost_count"
    ] == 1

    assert typed_smoke[
        "all_domains_positive_target_effect"
    ] is True

    assert typed_smoke[
        "all_domains_nonzero_retrieval_divergence"
    ] is True

    assert offline[
        "failure_triggered"
    ] is True

    assert offline[
        "checkpointed_before_failure"
    ] == 5

    assert offline[
        "resume_transport_invocations"
    ] == 7

    assert offline[
        "final_call_count"
    ] == 12

    assert offline[
        "provider_facing_evidence_ids_are_opaque"
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
        / "syntheticlab-v15.0-results-latest.json"
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
        "SyntheticLab v15.0 integrated suite PASS"
    )
    print(
        "Observed retrieval/reflection binary alignment:",
        mediation[
            "perfect_observed_binary_alignment"
        ],
    )
    print(
        "Target domains gained/lost:",
        mediation[
            "target_domain_gained_count"
        ],
        mediation[
            "target_domain_lost_count"
        ],
    )
    print(
        "Typed retrieval all-domain positive effect:",
        typed_smoke[
            "all_domains_positive_target_effect"
        ],
    )
    print(
        "Typed pilot opaque provider IDs:",
        offline[
            "provider_facing_evidence_ids_are_opaque"
        ],
    )


if __name__ == "__main__":
    main()
