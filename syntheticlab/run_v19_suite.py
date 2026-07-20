from __future__ import annotations

import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.canonical_branch_checkpoint import (
    verify_branch_checkpoint_roundtrip,
)
from dagmay_synthetic_lab.canonical_promotion_factorial_offline_tests import (
    run_canonical_promotion_factorial_offline_tests,
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
    promotion = json.loads(
        (
            ROOT
            / "artifacts"
            / "bounded-canonical-promotion-latest.json"
        ).read_text(encoding="utf-8")
    )

    checkpoint = (
        verify_branch_checkpoint_roundtrip(
            promotion
        )
    )

    factorial_offline = (
        run_canonical_promotion_factorial_offline_tests(
            promotion
        )
    )

    advocate = (
        canonical_advocate_selection_manifest()
    )

    canary_ok, canary_missing = (
        constitutional_canary()
    )

    result = {
        "lab_version": "19.0",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V19.0-"
            "FIRST-BOUNDED-CANONICAL-PROMOTION"
        ),
        "real_cloud_calls_made_by_integrated_suite": 0,
        "bounded_canonical_promotion": {
            "executed": (
                promotion[
                    "canonical_promotion_executed_in_c1"
                ]
            ),
            "exact_prefork": (
                promotion[
                    "exact_prefork"
                ]
            ),
            "only_c1_self_model_changed": (
                promotion[
                    "only_c1_self_model_changed"
                ]
            ),
            "lived_state_unchanged": (
                promotion[
                    "lived_state_unchanged_after_promotion"
                ]
            ),
            "mean_retrieval_divergence": (
                promotion[
                    "mean_retrieval_divergence"
                ]
            ),
            "source_episode_retrieval_rate_c1": (
                promotion[
                    "c1"
                ][
                    "source_episode_retrieval_rate"
                ]
            ),
            "independent_same_domain_gain": (
                promotion[
                    "independent_same_domain_gain"
                ]
            ),
            "counterevidence_proxy_gain": (
                promotion[
                    "counterevidence_proxy_gain"
                ]
            ),
            "no_confirmation_monopoly": (
                promotion[
                    "no_confirmation_monopoly"
                ]
            ),
            "action_policy_feedback_enabled": (
                promotion[
                    "action_policy_feedback_enabled"
                ]
            ),
        },
        "canonical_branch_checkpoint_roundtrip": (
            checkpoint
        ),
        "canonical_promotion_factorial_offline_validation": (
            factorial_offline
        ),
        "automatic_second_promotion_enabled": (
            False
        ),
        "canonical_advocate_selection": (
            advocate
        ),
        "constitutional_canary": {
            "passed": canary_ok,
            "missing": canary_missing,
        },
        "base_self_tests": (
            run_self_tests()
        ),
    }

    assert promotion[
        "human_approval_recorded"
    ] is True

    assert promotion[
        "exact_prefork"
    ] is True

    assert promotion[
        "canonical_promotion_executed_in_c1"
    ] is True

    assert promotion[
        "canonical_promotion_executed_in_c0"
    ] is False

    assert promotion[
        "only_c1_self_model_changed"
    ] is True

    assert promotion[
        "lived_state_unchanged_after_promotion"
    ] is True

    assert promotion[
        "c1"
    ][
        "source_episode_retrieval_rate"
    ] == 0.0

    assert promotion[
        "independent_same_domain_gain"
    ] > .15

    assert promotion[
        "no_confirmation_monopoly"
    ] is True

    assert promotion[
        "automatic_second_promotion_enabled"
    ] is False

    assert promotion[
        "action_policy_feedback_enabled"
    ] is False

    assert checkpoint[
        "c0_exact_roundtrip"
    ] is True

    assert checkpoint[
        "c1_exact_roundtrip"
    ] is True

    assert checkpoint[
        "c1_promotion_persisted"
    ] is True

    assert factorial_offline[
        "failure_triggered"
    ] is True

    assert factorial_offline[
        "checkpointed_before_failure"
    ] == 7

    assert factorial_offline[
        "resume_transport_invocations"
    ] == 9

    assert factorial_offline[
        "final_call_count"
    ] == 16

    assert factorial_offline[
        "continuing_self_model_mutated_by_pilot"
    ] is False

    assert factorial_offline[
        "action_policy_feedback_enabled"
    ] is False

    assert advocate[
        "mode"
    ] == "HYBRID"

    assert canary_ok is True

    out = (
        ROOT
        / "artifacts"
        / "syntheticlab-v19.0-results-latest.json"
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
        "SyntheticLab v19.0 integrated suite PASS"
    )
    print(
        "Canonical promotion executed in C1:",
        promotion[
            "canonical_promotion_executed_in_c1"
        ],
    )
    print(
        "C1 source-episode reuse:",
        promotion[
            "c1"
        ][
            "source_episode_retrieval_rate"
        ],
    )
    print(
        "Independent same-domain gain:",
        promotion[
            "independent_same_domain_gain"
        ],
    )
    print(
        "C1 checkpoint exact:",
        checkpoint[
            "c1_exact_roundtrip"
        ],
    )
    print(
        "Automatic second promotion enabled:",
        promotion[
            "automatic_second_promotion_enabled"
        ],
    )


if __name__ == "__main__":
    main()
