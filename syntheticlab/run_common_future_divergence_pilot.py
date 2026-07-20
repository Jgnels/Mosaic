from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.common_future_divergence_pilot import (
    run_common_future_divergence_pilot,
    analyze_common_future_divergence,
)


CONFIRMATION = (
    "I_APPROVE_TWELVE_COMMON_FUTURE_DIVERGENCE_CALLS"
)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--execute-real", action="store_true")
    parser.add_argument("--confirmation", default="")
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
            "Live common-future divergence pilot blocked: --execute-real is required."
        )

    if args.confirmation != CONFIRMATION:
        raise SystemExit(
            f'Live pilot blocked. Supply --confirmation "{CONFIRMATION}"'
        )

    revision_result = json.loads(
        (
            ROOT
            / "artifacts"
            / "approved-divergent-branch-canonical-revision-latest.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    progress = (
        ROOT
        / "artifacts"
        / "common-future-divergence-progress.json"
    )

    result = run_common_future_divergence_pilot(
        checkpoint_path=progress,
        revision_result=revision_result,
        model_id=args.model,
    )

    analysis = analyze_common_future_divergence(
        result
    )

    real_path = (
        ROOT
        / "artifacts"
        / "common-future-divergence-real-latest.json"
    )

    analysis_path = (
        ROOT
        / "artifacts"
        / "common-future-divergence-analysis-latest.json"
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

    print("Completed 12 common-future divergence calls.")
    print("No canonical state was changed by this pilot.")
    print(real_path)
    print(analysis_path)


if __name__ == "__main__":
    main()
