from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path
import sys
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parent
SRC = ROOT / "src"
sys.path.insert(0, str(SRC))

from dagmay_synthetic_lab.experiments import run_suite
from dagmay_synthetic_lab.self_tests import run_self_tests
from dagmay_synthetic_lab.core import canonical_hash


def write_outputs(result: dict, output_dir: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)

    result_path = output_dir / "syntheticlab-results-latest.json"
    result_path.write_text(json.dumps(result, indent=2, sort_keys=True), encoding="utf-8")

    summary = result["summary"]
    rows = []
    for group, metrics in summary.items():
        for metric, value in metrics.items():
            if metric == "seeds":
                continue
            rows.append({"experiment_group": group, "metric": metric, "value": value})

    with (output_dir / "syntheticlab-summary-latest.csv").open(
        "w", newline="", encoding="utf-8"
    ) as f:
        writer = csv.DictWriter(f, fieldnames=["experiment_group", "metric", "value"])
        writer.writeheader()
        writer.writerows(rows)

    report = f"""# Dagmay SyntheticLab v0.1 — Baseline Run

Generated: {datetime.now(timezone.utc).isoformat()}
Result SHA-256: `{canonical_hash(result)}`

## Novel causal learning
- Developmental contextual learner mean accuracy: {summary['novel_causality']['developmental_accuracy_mean']:.3f}
- Context-free frequency control: {summary['novel_causality']['global_control_accuracy_mean']:.3f}
- No-learning control: {summary['novel_causality']['no_learning_accuracy_mean']:.3f}
- Counterfactual-history control: {summary['novel_causality']['counterfactual_history_accuracy_mean']:.3f}
- Oracle/supplied-knowledge positive control: {summary['novel_causality']['oracle_supplied_knowledge_accuracy_mean']:.3f}

## Exact fork divergence
- Exact fork equality rate: {summary['fork_divergence']['exact_fork_equality_rate']:.3f}
- Post-history state divergence rate: {summary['fork_divergence']['state_divergence_rate']:.3f}
- Post-history preference divergence rate: {summary['fork_divergence']['preference_divergence_rate']:.3f}

## Pre-self agency coupling
- Causally privileged entity identification accuracy: {summary['agency_coupling']['identification_accuracy']:.3f}

## Interpretation

These are engineering-baseline experiments, not evidence of personhood or consciousness.

The important capabilities established by the lab are:
1. opaque causal rules can be learned from individual-specific experience;
2. equal starting states can be forked exactly and diverge under controlled histories;
3. a learner can infer which entity has privileged causal coupling to an internal signal without receiving a `SELF` label;
4. supplied knowledge can be compared explicitly against learned knowledge.

The next scientific step is to add stronger controls and preregistered experiments before making claims from results.
"""
    (output_dir / "syntheticlab-report-latest.md").write_text(report, encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", type=int, default=64)
    parser.add_argument("--output", default=str(ROOT / "artifacts"))
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        passed = run_self_tests()
        print("PASS SyntheticLab self-tests:")
        for test in passed:
            print(f"  - {test}")

    result = run_suite(seed_count=args.seeds)
    write_outputs(result, Path(args.output))

    print(json.dumps(result["summary"], indent=2))
    print(f"Artifacts written to: {Path(args.output).resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
