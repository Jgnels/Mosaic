from __future__ import annotations

import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.neutral_ownership_result_analysis import (
    analyze_neutral_ownership_probe,
)
from dagmay_synthetic_lab.longitudinal_reflection_offline_tests import (
    run_longitudinal_offline_tests,
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
from dagmay_synthetic_lab.hard_rich_world import (
    NonstationaryRichWorld,
    HardRichConfig,
)


def deterministic_regime_extension_test():
    world = NonstationaryRichWorld(
        101,
        HardRichConfig(
            steps=1000,
            regime_length=200,
        ),
    )
    original = {
        key: dict(value)
        for key, value
        in world._mappings.items()
    }

    # Force a regime beyond the originally pre-generated horizon.
    _ = world.optimal_interaction(
        zone=world.config.zones[0],
        regime=9,
    )

    originals_unchanged = all(
        world._mappings[key] == value
        for key, value
        in original.items()
    )

    return {
        "original_regime_count": len(
            original
        ),
        "extended_regime_present": (
            9 in world._mappings
        ),
        "original_regimes_unchanged": (
            originals_unchanged
        ),
    }


def main():
    neutral_payload = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-NEUTRAL-OWNERSHIP-REAL-PROBE-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )
    initial_reflection = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-REAL-REFLECTION-PILOT-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    neutral = (
        analyze_neutral_ownership_probe(
            neutral_payload
        )
    )
    longitudinal = (
        run_longitudinal_offline_tests(
            initial_reflection
        )
    )
    regime_extension = (
        deterministic_regime_extension_test()
    )

    advocate = (
        canonical_advocate_selection_manifest()
    )
    canary_ok, canary_missing = (
        constitutional_canary()
    )

    result = {
        "lab_version": "10.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V10.0-"
            "LONGITUDINAL-AUTOBIOGRAPHICAL-SELF-MODEL"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "neutral_ownership_real_result": neutral,
        "longitudinal_offline_validation": longitudinal,
        "deterministic_regime_extension": regime_extension,
        "canonical_advocate_selection": advocate,
        "constitutional_canary": {
            "passed": canary_ok,
            "missing": canary_missing,
        },
        "base_self_tests": run_self_tests(),
    }

    assert neutral[
        "all_three_correct"
    ] is True
    assert neutral[
        "label_permutation_invariance"
    ] is True
    assert neutral[
        "longitudinal_stage_activation_supported"
    ] is True
    assert neutral[
        "continuing_individual_mutated"
    ] is False

    long = longitudinal[
        "experiment_id"
    ]
    assert long == (
        "SL-LONGITUDINAL-REFLECTION-"
        "OFFLINE-INFRASTRUCTURE-001"
    )
    assert longitudinal[
        "exact_world_control_equality"
    ] is True
    assert longitudinal[
        "exact_agent_control_equality"
    ] is True
    assert longitudinal[
        "control_self_model_record_count"
    ] == 0
    assert longitudinal[
        "all_commits_attribution_validated"
    ] is True

    assert regime_extension[
        "extended_regime_present"
    ] is True
    assert regime_extension[
        "original_regimes_unchanged"
    ] is True

    assert advocate[
        "mode"
    ] == "HYBRID"
    assert canary_ok is True

    out = (
        ROOT
        / "artifacts"
        / "syntheticlab-v10.0-results-latest.json"
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
        "SyntheticLab v10.0 integrated suite PASS"
    )
    print(
        "Neutral ownership 3/3:",
        neutral[
            "all_three_correct"
        ],
    )
    print(
        "Label invariance:",
        neutral[
            "label_permutation_invariance"
        ],
    )
    print(
        "Longitudinal exact world control:",
        longitudinal[
            "exact_world_control_equality"
        ],
    )
    print(
        "Longitudinal exact agent control:",
        longitudinal[
            "exact_agent_control_equality"
        ],
    )
    print(
        "Future regime extension preserves past:",
        regime_extension[
            "original_regimes_unchanged"
        ],
    )


if __name__ == "__main__":
    main()
