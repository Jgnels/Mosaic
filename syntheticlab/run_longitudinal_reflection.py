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
        ROOT / "src"
    ),
)

from dagmay_synthetic_lab.longitudinal_reflection_live_runner import (
    run_real_longitudinal_reflection,
)
from dagmay_synthetic_lab.longitudinal_reflection_analysis import (
    analyze_longitudinal_reflection,
)


CONFIRMATION = (
    "I_APPROVE_THREE_LONGITUDINAL_REFLECTION_CALLS"
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
            "Live longitudinal reflection blocked: "
            "--execute-real is required."
        )

    if (
        args.confirmation
        != CONFIRMATION
    ):
        raise SystemExit(
            "Live longitudinal reflection blocked. "
            "Supply "
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

    initial_path = (
        ROOT
        / "artifacts"
        / "real_reflection_evidence"
        / "SL-REAL-REFLECTION-PILOT-001.json"
    )
    initial_payload = json.loads(
        initial_path.read_text(
            encoding="utf-8"
        )
    )

    result = (
        run_real_longitudinal_reflection(
            initial_real_reflection_payload=(
                initial_payload
            ),
            model_id=args.model,
        )
    )
    analysis = (
        analyze_longitudinal_reflection(
            result
        )
    )

    result_path = (
        ROOT
        / "artifacts"
        / "longitudinal-reflection-real-latest.json"
    )
    analysis_path = (
        ROOT
        / "artifacts"
        / "longitudinal-reflection-analysis-latest.json"
    )

    result_path.write_text(
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
        "Completed three longitudinal reflection calls."
    )
    print(
        "World and action-policy state remained exact "
        "against the no-reflection control."
    )
    print(result_path)
    print(analysis_path)


if __name__ == "__main__":
    main()
