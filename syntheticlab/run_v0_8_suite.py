from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path
import sys
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.self_tests import run_self_tests
from dagmay_synthetic_lab.experiments import run_suite
from dagmay_synthetic_lab.advanced_experiments import run_advanced_suite
from dagmay_synthetic_lab.beliefs import run_rumor_retraction_scenario
from dagmay_synthetic_lab.cohort import run_passive_factorial_cohort
from dagmay_synthetic_lab.drives import PROFILES
from dagmay_synthetic_lab.core import canonical_hash


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", type=int, default=64)
    parser.add_argument("--output", default=str(ROOT / "artifacts"))
    args = parser.parse_args()

    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)

    result = {
        "lab_version": "0.8",
        "suite_id": "DAGMAY-SYNTHETICLAB-V0.8",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "research_claim_status": "METHODOLOGY_AND_ARCHITECTURE_BASELINE",
        "self_tests": run_self_tests(),
        "baseline": run_suite(args.seeds),
        "advanced": run_advanced_suite(args.seeds),
        "temporal_belief": run_rumor_retraction_scenario(),
        "passive_factorial_cohort": run_passive_factorial_cohort(args.seeds),
        "available_drive_profiles": {
            k: v.to_dict() for k, v in PROFILES.items()
        },
        "canonical_drive_profile_selected": None,
    }

    (output / "syntheticlab-v0.8-results-latest.json").write_text(
        json.dumps(result, indent=2, sort_keys=True),
        encoding="utf-8",
    )

    cohort = result["passive_factorial_cohort"]
    with (output / "syntheticlab-v0.8-blinded-cohort.csv").open(
        "w", newline="", encoding="utf-8"
    ) as f:
        w = csv.writer(f)
        w.writerow(["BlindedCondition","Seed","Accuracy"])
        for blinded_id, data in sorted(cohort["blind_export"].items()):
            for row in data["rows"]:
                w.writerow([blinded_id, row["seed"], row["accuracy"]])

    (output / "syntheticlab-v0.8-blind-key-RESEARCHER-ONLY.json").write_text(
        json.dumps(cohort["blind_map_sealed_for_researcher"], indent=2, sort_keys=True),
        encoding="utf-8",
    )

    belief = result["temporal_belief"]
    report = f"""# Dagmay SyntheticLab v0.8 — Expansion Report

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## New in v0.8

### Native temporal belief graph
Rumor/retraction scenario:
- old beliefs preserved: {belief['old_beliefs_preserved']}
- final active belief reconciled to YES: {belief['active_is_yes']}
- explicit supersession chain valid: {belief['supersession_chain_valid']}

The graph now supports:
- perspective IDs;
- confidence;
- source evidence IDs;
- valid-from / valid-to;
- non-destructive supersession;
- mechanism/version metadata;
- provenance ancestry.

### Passive 16-condition factorial cohort
Factors:
- developmental learner vs memory-query control;
- chronological vs shuffled history;
- no reflection vs grounded read-only reflection;
- lived-only vs supplied-summary positive/contamination control.

This cohort intentionally does not use seed drives or endogenous action selection yet.

### Blinding
A blinded CSV is emitted separately from the researcher-only condition key.

### Seed drives
A configurable interface now exists, but no canonical drive profile has been selected.

Available profiles:
- NONE
- MINIMAL_REGULATION
- EPISTEMIC_MINIMAL
- BALANCED_MINIMAL
- HIGH_RISK_EXPERIMENTAL (requires explicit human approval)

## Human decision gate

The first *active* developmental cohort requires a decision about which seed drives are allowed.

The code deliberately refuses to silently choose the canonical profile.
"""
    (output / "syntheticlab-v0.8-report-latest.md").write_text(report, encoding="utf-8")

    print(report)


if __name__ == "__main__":
    main()
