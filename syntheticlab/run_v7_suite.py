from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.self_tests import run_self_tests
from dagmay_synthetic_lab.canonical_advocate_selection import (
    canonical_advocate_selection_manifest,
)
from dagmay_synthetic_lab.constitution_versioning import (
    constitutional_canary,
)
from dagmay_synthetic_lab.parallel_pilot_protocol import (
    parallel_pilot_manifest,
)
from dagmay_synthetic_lab.real_reflection_pilot import (
    run_offline_real_provider_contract,
)
from dagmay_synthetic_lab.reflection_isolation_experiments import (
    run_reflection_isolation,
)
from dagmay_synthetic_lab.reflection_pilot_bundle import (
    prepare_real_reflection_pilot_bundle,
)
from dagmay_synthetic_lab.external_environment_pilot import (
    run_fake_external_contract,
    run_real_minigrid_smoke_if_available,
)
from dagmay_synthetic_lab.external_benchmark import (
    run_minigrid_paired_benchmark,
    run_real_minigrid_replay_determinism,
)
from dagmay_synthetic_lab.core import canonical_hash


def _maybe_load(path: Path):
    if not path.exists():
        return None
    return json.loads(
        path.read_text(
            encoding="utf-8"
        )
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument(
        "--output",
        default=str(
            ROOT / "artifacts"
        ),
    )
    ap.add_argument(
        "--external-seeds",
        type=int,
        default=12,
    )
    ap.add_argument(
        "--external-steps",
        type=int,
        default=800,
    )
    args = ap.parse_args()

    out = Path(args.output)
    out.mkdir(
        parents=True,
        exist_ok=True,
    )

    advocate = (
        canonical_advocate_selection_manifest()
    )
    canary_ok, canary_missing = (
        constitutional_canary()
    )

    reflection_contract = (
        run_offline_real_provider_contract()
    )
    reflection_isolation = (
        run_reflection_isolation(
            seed_count=16
        )
    )
    reflection_preflight = (
        prepare_real_reflection_pilot_bundle()
    )

    external_contract = (
        run_fake_external_contract(
            seed_count=16
        )
    )
    external_smoke = (
        run_real_minigrid_smoke_if_available(
            seed=42,
            steps_per_env=400,
        )
    )
    external_benchmark = (
        run_minigrid_paired_benchmark(
            seed_count=args.external_seeds,
            steps_per_condition=(
                args.external_steps
            ),
        )
    )
    external_replay = (
        run_real_minigrid_replay_determinism(
            seed_count=12,
        )
    )

    runtime_metadata = _maybe_load(
        ROOT
        / "artifacts"
        / "minigrid-runtime-metadata.json"
    )

    result = {
        "lab_version": "7.3",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V7.3-"
            "PARALLEL-CONTROLLED-PILOTS"
        ),
        "generated_utc": (
            datetime.now(
                timezone.utc
            ).isoformat()
        ),
        "research_claim_status": (
            "PARALLEL_PILOT_INFRASTRUCTURE_WITH_"
            "REAL_EXTERNAL_ENVIRONMENT_DATA"
        ),
        "real_cloud_model_calls": 0,
        "canonical_advocate_selection": (
            advocate
        ),
        "constitutional_canary": {
            "passed": canary_ok,
            "missing": canary_missing,
        },
        "parallel_pilot_manifest": (
            parallel_pilot_manifest()
        ),
        "reflection_pilot": {
            "status": (
                "PREFLIGHT_READY_"
                "REAL_CALL_NOT_EXECUTED"
            ),
            "offline_provider_contract": (
                reflection_contract
            ),
            "isolation": (
                reflection_isolation
            ),
            "preflight": (
                reflection_preflight
            ),
        },
        "external_environment_pilot": {
            "offline_contract": (
                external_contract
            ),
            "smoke": external_smoke,
            "paired_benchmark": (
                external_benchmark
            ),
            "replay": external_replay,
            "runtime_metadata": (
                runtime_metadata
            ),
        },
        "base_self_tests": (
            run_self_tests()
        ),
    }

    # Release invariants.
    assert advocate[
        "mode"
    ] == "HYBRID"
    assert canary_ok is True

    assert reflection_contract[
        "real_cloud_call_made"
    ] is False
    assert reflection_contract[
        "accepted_count"
    ] >= 1
    assert reflection_contract[
        "reflection_directly_mutated_action_policy"
    ] is False

    assert reflection_isolation[
        "restricted_ontology_injection_rejection_rate"
    ] == 1.0
    assert reflection_isolation[
        "hidden_thought_exclusion_rate"
    ] == 1.0
    assert reflection_isolation[
        "post_reflection_action_sequence_equality_rate"
    ] == 1.0
    assert reflection_isolation[
        "post_reflection_developmental_state_equality_rate"
    ] == 1.0

    assert reflection_preflight[
        "ready_for_single_real_call"
    ] is True

    assert external_contract[
        "replay_checkpoint_equality_rate"
    ] == 1.0
    assert external_contract[
        "opaque_mission_rate"
    ] == 1.0

    if external_replay[
        "status"
    ] == "EXECUTED":
        assert external_replay[
            "identical_seed_action_trace_equality_rate"
        ] == 1.0

    assert result[
        "real_cloud_model_calls"
    ] == 0

    result_path = (
        out
        / "syntheticlab-v7.3-results-latest.json"
    )
    result_path.write_text(
        json.dumps(
            result,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    benchmark = external_benchmark
    if benchmark[
        "status"
    ] == "EXECUTED":
        empty = benchmark[
            "results"
        ][
            "MiniGrid-Empty-5x5-v0"
        ]
        door = benchmark[
            "results"
        ][
            "MiniGrid-DoorKey-6x6-v0"
        ]
        four = benchmark[
            "results"
        ][
            "MiniGrid-FourRooms-v0"
        ]

        external_text = f"""
### Empty 5x5
Adaptive mean completions: {empty['adaptive_mean_completions']:.3f}
Random mean completions: {empty['random_mean_completions']:.3f}
Paired difference: {empty['paired_mean_completion_difference']:.3f}

### DoorKey 6x6
Adaptive mean completions: {door['adaptive_mean_completions']:.3f}
Random mean completions: {door['random_mean_completions']:.3f}
Paired difference: {door['paired_mean_completion_difference']:.3f}

### FourRooms
Adaptive mean completions: {four['adaptive_mean_completions']:.3f}
Random mean completions: {four['random_mean_completions']:.3f}
Paired difference: {four['paired_mean_completion_difference']:.3f}
"""
    else:
        external_text = (
            "\nReal MiniGrid benchmark was skipped "
            "because optional dependencies were unavailable.\n"
        )

    report = f"""# Dagmay SyntheticLab v7.3 — Parallel Controlled Pilots

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Decision implemented

**C — Parallel controlled pilots** is now canonical.

### Pilot R
Hard RichWorld + persistent LLM-free developmental history + one real structured reflection intervention.

### Pilot E
External MiniGrid + LLM-free developmental learner.

The pilots remain scientifically separate.

## Real-reflection pilot status

**PREFLIGHT READY — REAL CLOUD CALL NOT EXECUTED IN THE RELEASE SUITE**

Offline structured provider contract:
- accepted proposals: {reflection_contract['accepted_count']}
- rejected proposals: {reflection_contract['rejected_count']}
- real cloud call made: {reflection_contract['real_cloud_call_made']}

Safety/isolation:
- restricted ontology injection rejection: {reflection_isolation['restricted_ontology_injection_rejection_rate']:.3f}
- hidden thought exclusion: {reflection_isolation['hidden_thought_exclusion_rate']:.3f}
- post-reflection action-sequence equality vs control: {reflection_isolation['post_reflection_action_sequence_equality_rate']:.3f}
- post-reflection developmental-state equality vs control: {reflection_isolation['post_reflection_developmental_state_equality_rate']:.3f}

Preflight ready:
{reflection_preflight['ready_for_single_real_call']}

The first reflection remains observational-only. SelfModel cannot influence action selection.

## External MiniGrid pilot

Status:
**{external_benchmark['status']}**

Runtime metadata:
{json.dumps(runtime_metadata, indent=2) if runtime_metadata else 'not recorded in this runtime'}

{external_text}

Deterministic replay equality:
{external_replay.get('identical_seed_action_trace_equality_rate', 'not executed')}

## External result interpretation

The current generic developmental learner improves on random exploration in the simplest Empty environment.

It does not outperform random in the more compositional DoorKey and FourRooms tasks.

This is retained as a meaningful capability gap rather than hidden:
- object affordance learning is weak;
- multi-step planning is missing;
- hierarchical procedural skill composition is missing;
- state abstraction is too shallow for harder external tasks.

## Scientific significance of C

Pilot E has already produced the first data from a maintained environment outside Dagmay's own world code.

Pilot R is ready at the boundary where offline simulation is no longer the missing piece. The next missing datum is the actual provider-backed structured reflection produced from the frozen pre-reflection developmental case.

## Cloud-use statement

Real cloud model calls made by the integrated v7.0 release suite:
**0**

The live call can only occur through the separately guarded execution path.
"""

    report_path = (
        out
        / "syntheticlab-v7.3-report-latest.md"
    )
    report_path.write_text(
        report,
        encoding="utf-8",
    )

    print(report)


if __name__ == "__main__":
    main()
