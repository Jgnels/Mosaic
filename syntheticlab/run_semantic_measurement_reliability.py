from __future__ import annotations

import argparse
import json
import os
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

from dagmay_synthetic_lab.semantic_measurement_reliability import (
    run_semantic_measurement_reliability,
    analyze_semantic_measurement_reliability,
)


CONFIRMATION = (
    "I_APPROVE_EIGHTEEN_SEMANTIC_RELIABILITY_CALLS"
)


def main():
    parser = argparse.ArgumentParser()

    parser.add_argument(
        "--execute-real",
        action="store_true",
    )

    parser.add_argument(
        "--confirmation",
        default="",
    )

    parser.add_argument(
        "--model",
        default=os.environ.get(
            "DAGMAY_GEMINI_MODEL",
            "gemini-3.1-flash-lite",
        ),
    )

    args = parser.parse_args()

    if not args.execute_real:
        raise SystemExit(
            "Live semantic reliability audit blocked: "
            "--execute-real is required."
        )

    if (
        args.confirmation
        != CONFIRMATION
    ):
        raise SystemExit(
            "Live audit blocked. Supply "
            f'--confirmation "{CONFIRMATION}"'
        )

    if not (
        os.environ.get(
            "DAGMAY_GEMINI_API_KEY"
        )
        or os.environ.get(
            "GEMINI_API_KEY"
        )
    ):
        raise SystemExit(
            "Set DAGMAY_GEMINI_API_KEY or GEMINI_API_KEY first."
        )

    prior_payload = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-BLINDED-SEMANTIC-CONVERGENCE-AUDIT-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    progress = (
        ROOT
        / "artifacts"
        / "semantic-measurement-reliability-progress.json"
    )

    result = run_semantic_measurement_reliability(
        checkpoint_path=(
            progress
        ),
        prior_payload=(
            prior_payload
        ),
        model_id=(
            args.model
        ),
    )

    analysis = (
        analyze_semantic_measurement_reliability(
            result
        )
    )

    real_path = (
        ROOT
        / "artifacts"
        / "semantic-measurement-reliability-real-latest.json"
    )

    analysis_path = (
        ROOT
        / "artifacts"
        / "semantic-measurement-reliability-analysis-latest.json"
    )

    real_path.write_text(
        json.dumps(
            result,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    analysis_path.write_text(
        json.dumps(
            analysis,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    print(
        "Completed 18 new semantic reliability calls."
    )
    print(
        "Combined with v26 replicate A: 27 total semantic evaluations."
    )
    print(
        "No canonical state was changed."
    )
    print(
        real_path
    )
    print(
        analysis_path
    )


if __name__ == "__main__":
    main()
