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

from dagmay_synthetic_lab.typed_pilot_result_audit import (
    analyze_typed_pilot_design_confounds,
)
from dagmay_synthetic_lab.attention_competition_offline_tests import (
    run_attention_competition_offline_tests,
)
from dagmay_synthetic_lab.matched_composition_retrieval import (
    retrieve_matched_composition,
    composition_counts,
)
from dagmay_synthetic_lab.typed_evidence_retrieval import (
    build_typed_evidence_fabric,
)
from dagmay_synthetic_lab.retrieval_attention_experiments import (
    _frozen_history,
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
    typed_analysis = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-TYPED-RETRIEVAL-REAL-REFLECTION-PILOT-ANALYSIS-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    typed_real = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-TYPED-RETRIEVAL-REAL-REFLECTION-PILOT-001.json"
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

    audit = (
        analyze_typed_pilot_design_confounds(
            analysis_payload=(
                typed_analysis
            ),
            real_payload=(
                typed_real
            ),
        )
    )

    frozen = _frozen_history(
        seed=1601,
        steps=450,
    )

    fabric = (
        build_typed_evidence_fabric(
            memory_bank=(
                frozen[
                    "memory_bank"
                ]
            ),
            current_step=(
                frozen[
                    "current_step"
                ]
            ),
        )
    )

    compositions = {
        "F0": composition_counts(
            retrieve_matched_composition(
                fabric=fabric,
                target_domain=None,
            )
        )
    }

    for domain in (
        "agency",
        "continuity",
        "embodiment",
        "other_minds",
    ):
        compositions[
            f"F1_{domain}"
        ] = composition_counts(
            retrieve_matched_composition(
                fabric=fabric,
                target_domain=domain,
            )
        )

    offline = (
        run_attention_competition_offline_tests(
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
        "lab_version": "16.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V16.0-"
            "MATCHED-COMPOSITION-ATTENTION-COMPETITION"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "typed_pilot_design_audit": (
            audit
        ),
        "matched_compositions": (
            compositions
        ),
        "attention_competition_offline_validation": (
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

    assert audit[
        "baseline_target_prevalence_ceiling"
    ] is True

    assert audit[
        "all_f1_calls_omit_exactly_one_evidence_channel"
    ] is True

    assert audit[
        "omitted_evidence_channel_always_absent_from_output_domains"
    ] is True

    assert compositions[
        "F0"
    ] == {
        "agency": 3,
        "continuity": 3,
        "embodiment": 3,
        "other_minds": 3,
    }

    for domain in (
        "agency",
        "continuity",
        "embodiment",
        "other_minds",
    ):
        expected = {
            candidate: (
                6
                if candidate
                == domain
                else 2
            )
            for candidate
            in (
                "agency",
                "continuity",
                "embodiment",
                "other_minds",
            )
        }

        assert compositions[
            f"F1_{domain}"
        ] == expected

    assert offline[
        "failure_triggered"
    ] is True

    assert offline[
        "checkpointed_before_failure"
    ] == 9

    assert offline[
        "resume_transport_invocations"
    ] == 15

    assert offline[
        "final_call_count"
    ] == 24

    assert offline[
        "all_conditions_keep_all_four_evidence_channels"
    ] is True

    assert offline[
        "response_budget_max_proposals"
    ] == 2

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
        / "syntheticlab-v16.0-results-latest.json"
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
        "SyntheticLab v16.0 integrated suite PASS"
    )
    print(
        "v15 prevalence ceiling confirmed:",
        audit[
            "baseline_target_prevalence_ceiling"
        ],
    )
    print(
        "v15 F1 channel omission confirmed:",
        audit[
            "all_f1_calls_omit_exactly_one_evidence_channel"
        ],
    )
    print(
        "v16 F0 composition:",
        compositions[
            "F0"
        ],
    )
    print(
        "v16 selective response budget:",
        offline[
            "response_budget_max_proposals"
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
