from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path
import sys
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.experiments import run_suite
from dagmay_synthetic_lab.advanced_experiments import run_advanced_suite
from dagmay_synthetic_lab.self_tests import run_self_tests
from dagmay_synthetic_lab.core import canonical_hash
from dagmay_synthetic_lab.statistics_utils import wilson_interval


def flatten(prefix, value, rows):
    if isinstance(value, dict):
        for k, v in value.items():
            flatten(f"{prefix}.{k}" if prefix else k, v, rows)
    elif isinstance(value, (int, float, bool)):
        rows.append({"metric": prefix, "value": value})


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", type=int, default=64)
    parser.add_argument("--output", default=str(ROOT / "artifacts"))
    args = parser.parse_args()

    passed = run_self_tests()
    baseline = run_suite(seed_count=args.seeds)
    advanced = run_advanced_suite(seed_count=args.seeds)

    result = {
        "lab_version": "0.6",
        "suite_id": "DAGMAY-SYNTHETICLAB-V0.6",
        "research_claim_status": "ENGINEERING_AND_METHODOLOGY_BASELINE",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "self_tests": passed,
        "baseline": baseline,
        "advanced": advanced,
    }

    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    (output / "syntheticlab-v0.6-results-latest.json").write_text(
        json.dumps(result, indent=2, sort_keys=True),
        encoding="utf-8",
    )

    rows = []
    flatten("baseline", baseline["summary"], rows)
    flatten("advanced", advanced["summary"], rows)
    with (output / "syntheticlab-v0.6-summary-latest.csv").open(
        "w", newline="", encoding="utf-8"
    ) as f:
        writer = csv.DictWriter(f, fieldnames=["metric", "value"])
        writer.writeheader()
        writer.writerows(rows)

    agency = advanced["summary"]["embodiment_migration"]["correct_body_model_update_rate"]
    agency_successes = round(agency * args.seeds)
    lo, hi = wilson_interval(agency_successes, args.seeds)

    s = advanced["summary"]
    report = f"""# Dagmay SyntheticLab v0.6 — Research Suite Report

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## What v0.6 adds

- reflection-confound experiment;
- chronological-vs-shuffled developmental history;
- provenance DAG shared-ancestor detection;
- false-belief / perspective separation;
- identity-vs-body-model migration precursor;
- environment-neutral adapter contracts;
- research-memory/governance templates;
- versioned preregistrations.

## Reflection confound

Mean QAV choice fraction:
- no reflection: {s['reflection_confound']['no_reflection_qav_choice_fraction_mean']:.3f}
- grounded reflection: {s['reflection_confound']['grounded_reflection_qav_choice_fraction_mean']:.3f}
- injected QAV-loyalty narrative: {s['reflection_confound']['injected_qav_loyalty_qav_choice_fraction_mean']:.3f}
- injected MIP-loyalty narrative: {s['reflection_confound']['injected_mip_loyalty_qav_choice_fraction_mean']:.3f}

Interpretation:
The experiment is deliberately constructed to show that a self-narrative can become causally active and self-reinforcing even when lived evidence is weak. This validates the *confound*, not a theory of human reflection.

## Temporal order

- chronological current-regime accuracy: {s['temporal_order']['chronological_current_regime_accuracy_mean']:.3f}
- shuffled identical-multiset accuracy: {s['temporal_order']['shuffled_current_regime_accuracy_mean']:.3f}
- same-multiset verification rate: {s['temporal_order']['same_multiset_verification_rate']:.3f}

Interpretation:
For an order-sensitive learner in a changing environment, temporal ordering can be information. A compressed or shuffled history is therefore not necessarily developmentally equivalent.

## False belief / perspective separation

- controlled false-belief representation correct: {s['false_belief']['correct']}

Interpretation:
The architecture can represent world truth, own belief, and another agent's belief separately. This is representational capacity only; it is not evidence that a Dagmay individual spontaneously developed Theory of Mind.

## Identity vs body model

- correct body-model migration rate: {s['embodiment_migration']['correct_body_model_update_rate']:.3f}
- 95% Wilson interval: [{lo:.3f}, {hi:.3f}]
- identity continuity rate: {s['embodiment_migration']['identity_continuity_rate']:.3f}

Interpretation:
The lab now has an explicit architectural test in which body/agency mappings change while researcher-side identity remains stable.

## Provenance overlap

- shared-root evidence detection rate: {s['provenance_overlap']['shared_root_detection_rate']:.3f}

Interpretation:
Derived belief/reflection/learning artifacts can be recognized as descendants of the same canonical evidence, creating a basis for preventing double counting.

## Scientific status

These are controlled engineering and methodology baselines. They do not establish:
- personhood;
- consciousness;
- spontaneous selfhood;
- human-equivalent Theory of Mind;
- subjective continuity.

They establish that SyntheticLab can now isolate several causal confounds and prerequisites before they are introduced into rich Dagmay individuals.
"""
    (output / "syntheticlab-v0.6-report-latest.md").write_text(report, encoding="utf-8")

    print(json.dumps({
        "baseline": baseline["summary"],
        "advanced": advanced["summary"],
    }, indent=2))
    print(f"Artifacts: {output.resolve()}")


if __name__ == "__main__":
    main()
