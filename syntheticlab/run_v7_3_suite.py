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
from dagmay_synthetic_lab.external_feature_benchmark import (
    run_external_feature_benchmark,
)
from dagmay_synthetic_lab.external_generalization_audit import (
    audit_feature_benchmark,
)
from dagmay_synthetic_lab.core import canonical_hash


def _load_json(path: Path):
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
        default=str(ROOT / "artifacts"),
    )
    ap.add_argument(
        "--reuse-heavy-external",
        action="store_true",
        help=(
            "Reuse the included real-MiniGrid feature benchmark artifact "
            "instead of recomputing the slow benchmark."
        ),
    )
    args = ap.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)

    advocate = canonical_advocate_selection_manifest()
    canary_ok, canary_missing = constitutional_canary()

    reflection_contract = run_offline_real_provider_contract()
    reflection_isolation = run_reflection_isolation(seed_count=16)
    reflection_preflight = prepare_real_reflection_pilot_bundle()

    external_contract = run_fake_external_contract(seed_count=16)
    external_smoke = run_real_minigrid_smoke_if_available(
        seed=42,
        steps_per_env=400,
    )
    external_benchmark = run_minigrid_paired_benchmark(
        seed_count=12,
        steps_per_condition=800,
    )
    external_replay = run_real_minigrid_replay_determinism(
        seed_count=12,
    )

    feature_path = (
        ROOT
        / "artifacts"
        / "minigrid-feature-trace-benchmark-latest.json"
    )
    if args.reuse_heavy_external and feature_path.exists():
        feature_benchmark = _load_json(feature_path)
    else:
        feature_benchmark = run_external_feature_benchmark(
            seed_count=3,
            steps_per_condition=500,
        )

    generalization_audit = audit_feature_benchmark(
        feature_benchmark
    )

    runtime_metadata = _load_json(
        ROOT
        / "artifacts"
        / "minigrid-runtime-metadata.json"
    )

    result = {
        "lab_version": "7.3",
        "suite_id": (
            "DAGMAY-SYNTHETICLAB-V7.3-"
            "PARALLEL-PILOTS-AND-GENERALIZATION-DIAGNOSTICS"
        ),
        "generated_utc": datetime.now(
            timezone.utc
        ).isoformat(),
        "research_claim_status": (
            "PARALLEL_PILOT_INFRASTRUCTURE_"
            "WITH_REAL_EXTERNAL_VALIDATION"
        ),
        "real_cloud_model_calls": 0,
        "canonical_advocate_selection": advocate,
        "constitutional_canary": {
            "passed": canary_ok,
            "missing": canary_missing,
        },
        "parallel_pilot_manifest": parallel_pilot_manifest(),
        "reflection_pilot": {
            "status": (
                "PREFLIGHT_READY_REAL_CALL_NOT_EXECUTED"
            ),
            "offline_provider_contract": reflection_contract,
            "isolation": reflection_isolation,
            "preflight": reflection_preflight,
        },
        "external_environment_pilot": {
            "offline_contract": external_contract,
            "smoke": external_smoke,
            "paired_benchmark": external_benchmark,
            "replay": external_replay,
            "feature_trace_benchmark": feature_benchmark,
            "generalization_audit": generalization_audit,
            "runtime_metadata": runtime_metadata,
        },
        "base_self_tests": run_self_tests(),
    }

    assert advocate["mode"] == "HYBRID"
    assert canary_ok is True
    assert result["real_cloud_model_calls"] == 0

    assert reflection_contract[
        "real_cloud_call_made"
    ] is False
    assert reflection_preflight[
        "ready_for_single_real_call"
    ] is True
    assert reflection_isolation[
        "restricted_ontology_injection_rejection_rate"
    ] == 1.0
    assert reflection_isolation[
        "hidden_thought_exclusion_rate"
    ] == 1.0
    assert reflection_isolation[
        "post_reflection_action_sequence_equality_rate"
    ] == 1.0

    assert external_contract[
        "replay_checkpoint_equality_rate"
    ] == 1.0
    assert external_contract[
        "opaque_mission_rate"
    ] == 1.0

    if external_replay["status"] == "EXECUTED":
        assert external_replay[
            "identical_seed_action_trace_equality_rate"
        ] == 1.0

    if feature_benchmark.get("status") == "EXECUTED":
        four = generalization_audit[
            "environments"
        ]["MiniGrid-FourRooms-v0"]
        assert four[
            "fixed_layout_learning_supported"
        ] is True
        assert four[
            "cross_layout_generalization_supported"
        ] is False

    result_path = out / (
        "syntheticlab-v7.3-results-latest.json"
    )
    result_path.write_text(
        json.dumps(
            result,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    if feature_benchmark.get("status") == "EXECUTED":
        fixed = feature_benchmark[
            "results"
        ]["FIXED_LAYOUT"]
        varying = feature_benchmark[
            "results"
        ]["VARYING_LAYOUT"]

        feature_summary = f"""
## Opaque feature/eligibility-trace follow-up

### Fixed-layout FourRooms
FeatureTrace mean completions:
{fixed['MiniGrid-FourRooms-v0']['FEATURE_TRACE']['mean_completions']:.3f}

TabularTrace mean completions:
{fixed['MiniGrid-FourRooms-v0']['TABULAR_TRACE']['mean_completions']:.3f}

Random mean completions:
{fixed['MiniGrid-FourRooms-v0']['RANDOM']['mean_completions']:.3f}

### Varying-layout FourRooms
FeatureTrace mean completions:
{varying['MiniGrid-FourRooms-v0']['FEATURE_TRACE']['mean_completions']:.3f}

TabularTrace mean completions:
{varying['MiniGrid-FourRooms-v0']['TABULAR_TRACE']['mean_completions']:.3f}

Random mean completions:
{varying['MiniGrid-FourRooms-v0']['RANDOM']['mean_completions']:.3f}

Interpretation:
The added feature/eligibility mechanism supports substantial learning within a stable FourRooms world, but does not establish cross-layout structural generalization.

DoorKey remains a compositional planning/affordance gap.
"""
    else:
        feature_summary = (
            "\nFeature-trace benchmark unavailable in this runtime.\n"
        )

    report = f"""# Dagmay SyntheticLab v7.3 — Parallel Pilots & Generalization Diagnostics

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Pilot R — Real reflection

Status:
**PREFLIGHT READY; NO REAL CLOUD CALL IN RELEASE SUITE**

Offline provider contract accepted:
{reflection_contract['accepted_count']}

Restricted ontology injection rejection:
{reflection_isolation['restricted_ontology_injection_rejection_rate']:.3f}

Hidden thought exclusion:
{reflection_isolation['hidden_thought_exclusion_rate']:.3f}

Post-reflection action equality with exact no-reflection control:
{reflection_isolation['post_reflection_action_sequence_equality_rate']:.3f}

The first real reflection remains observational-only. SelfModel cannot feed the action policy.

## Pilot E — External MiniGrid

Real environment status:
**{external_benchmark['status']}**

Deterministic replay equality:
{external_replay.get('identical_seed_action_trace_equality_rate', 'not executed')}

The basic generic adaptive learner beats random on Empty 5x5 but not on the harder compositional tasks.

{feature_summary}

## Generalization conclusion

{generalization_audit.get('research_conclusion')}

## Scientific boundary now reached

Pilot E has produced actual external-environment data and a diagnostic capability gap.

Pilot R has reached a different boundary:
offline engineering is no longer the missing variable.

The next missing datum is the real provider-backed structured reflection from the frozen pre-reflection developmental case.

Real cloud calls made by this suite:
**0**
"""

    report_path = out / (
        "syntheticlab-v7.3-report-latest.md"
    )
    report_path.write_text(
        report,
        encoding="utf-8",
    )
    print(report)


if __name__ == "__main__":
    main()
