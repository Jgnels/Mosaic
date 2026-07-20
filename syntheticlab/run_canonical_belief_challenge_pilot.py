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

from dagmay_synthetic_lab.canonical_belief_challenge_pilot import (
    run_canonical_belief_challenge_pilot,
    analyze_canonical_belief_challenge,
)


CONFIRMATION = (
    "I_APPROVE_TWENTY_FOUR_CANONICAL_BELIEF_CHALLENGE_CALLS"
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
            "Live canonical belief-challenge pilot blocked: "
            "--execute-real is required."
        )

    if (
        args.confirmation
        != CONFIRMATION
    ):
        raise SystemExit(
            "Live belief-challenge pilot blocked. Supply "
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

    promotion_result = json.loads(
        (
            ROOT
            / "artifacts"
            / "bounded-canonical-promotion-latest.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    progress = (
        ROOT
        / "artifacts"
        / "canonical-belief-challenge-progress.json"
    )

    final_path = (
        ROOT
        / "artifacts"
        / "canonical-belief-challenge-real-latest.json"
    )

    analysis_path = (
        ROOT
        / "artifacts"
        / "canonical-belief-challenge-analysis-latest.json"
    )

    result = (
        run_canonical_belief_challenge_pilot(
            checkpoint_path=(
                progress
            ),
            promotion_result=(
                promotion_result
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
        analyze_canonical_belief_challenge(
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
        "Completed 24 canonical belief-challenge calls."
    )
    print(
        "No result was committed to the continuing canonical SelfModel."
    )
    print(
        "Automatic canonical revision remains disabled."
    )
    print(
        final_path
    )
    print(
        analysis_path
    )


if __name__ == "__main__":
    main()
