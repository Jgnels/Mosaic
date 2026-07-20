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

from dagmay_synthetic_lab.attention_competition_pilot import (
    run_attention_competition_real_pilot,
    analyze_attention_competition_pilot,
)


CONFIRMATION = (
    "I_APPROVE_TWENTY_FOUR_ATTENTION_COMPETITION_CALLS"
)


def main():
    ap = argparse.ArgumentParser()

    ap.add_argument(
        "--execute-real",
        action="store_true",
    )

    ap.add_argument(
        "--confirmation",
        default="",
    )

    ap.add_argument(
        "--model",
        default=os.environ.get(
            "DAGMAY_GEMINI_MODEL",
            "gemini-3.1-flash-lite",
        ),
    )

    args = ap.parse_args()

    if not args.execute_real:
        raise SystemExit(
            "Live attention-competition pilot blocked: "
            "--execute-real is required."
        )

    if args.confirmation != CONFIRMATION:
        raise SystemExit(
            "Live attention-competition pilot blocked. Supply "
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
            "Set DAGMAY_GEMINI_API_KEY "
            "or GEMINI_API_KEY first."
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

    progress = (
        ROOT
        / "artifacts"
        / "attention-competition-pilot-progress.json"
    )

    final_path = (
        ROOT
        / "artifacts"
        / "attention-competition-pilot-real-latest.json"
    )

    analysis_path = (
        ROOT
        / "artifacts"
        / "attention-competition-pilot-analysis-latest.json"
    )

    result = (
        run_attention_competition_real_pilot(
            checkpoint_path=(
                progress
            ),
            longitudinal_analysis=(
                longitudinal
            ),
            model_id=(
                args.model
            ),
        )
    )

    if result[
        "status"
    ] != "COMPLETE":
        raise SystemExit(
            "Pilot stopped before completion. "
            "Progress is checkpointed; rerun the same command."
        )

    analysis = (
        analyze_attention_competition_pilot(
            result
        )
    )

    final_path.write_text(
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
        "Completed 24 selective attention-competition calls."
    )
    print(
        "All conditions retained all four evidence channels."
    )
    print(
        "The model was limited to two proposals per call."
    )
    print(
        "No output was committed to a continuing SelfModel."
    )
    print(
        final_path
    )
    print(
        analysis_path
    )


if __name__ == "__main__":
    main()
