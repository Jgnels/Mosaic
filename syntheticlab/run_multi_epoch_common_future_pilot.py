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

from dagmay_synthetic_lab.multi_epoch_common_future import (
    run_multi_epoch_common_future_pilot,
    analyze_multi_epoch_common_future,
)


CONFIRMATION = (
    "I_APPROVE_TWENTY_FOUR_MULTI_EPOCH_COMMON_FUTURE_CALLS"
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
            "Live multi-epoch common-future pilot blocked: "
            "--execute-real is required."
        )

    if (
        args.confirmation
        != CONFIRMATION
    ):
        raise SystemExit(
            "Live pilot blocked. Supply "
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
        / "multi-epoch-common-future-progress.json"
    )

    result = (
        run_multi_epoch_common_future_pilot(
            checkpoint_path=(
                progress
            ),
            revision_result=(
                revision_result
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
            "Pilot stopped before completion. Progress is checkpointed; "
            "rerun the same command."
        )

    analysis = (
        analyze_multi_epoch_common_future(
            result
        )
    )

    real_path = (
        ROOT
        / "artifacts"
        / "multi-epoch-common-future-real-latest.json"
    )

    analysis_path = (
        ROOT
        / "artifacts"
        / "multi-epoch-common-future-analysis-latest.json"
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
        "Completed 24 multi-epoch common-future calls."
    )
    print(
        "All updates remained shadow-only; canonical branch states are unchanged."
    )
    print(
        real_path
    )
    print(
        analysis_path
    )


if __name__ == "__main__":
    main()
