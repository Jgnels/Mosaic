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

from dagmay_synthetic_lab.raw_stream_ownership_lab import (
    run_six_call_raw_probe_resumable,
)


CONFIRMATION = (
    "I_APPROVE_SIX_RAW_STREAM_CALLS"
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
            "Live raw-stream probe blocked: "
            "--execute-real is required."
        )

    if (
        args.confirmation
        != CONFIRMATION
    ):
        raise SystemExit(
            "Live raw-stream probe blocked. Supply "
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

    progress_path = (
        ROOT
        / "artifacts"
        / "raw-stream-ownership-progress.json"
    )
    final_path = (
        ROOT
        / "artifacts"
        / "raw-stream-ownership-real-latest.json"
    )

    result = (
        run_six_call_raw_probe_resumable(
            checkpoint_path=(
                progress_path
            ),
            model_id=args.model,
        )
    )

    if result[
        "status"
    ] != "COMPLETE":
        raise SystemExit(
            "Raw-stream probe stopped before completion. "
            "Progress has been saved to "
            f"{progress_path}. Re-run the same command to resume."
        )

    final_path.write_text(
        json.dumps(
            result,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    print(
        "Completed six raw-stream analytical calls."
    )
    print(
        "Each successful call was checkpointed before the next call."
    )
    print(
        "No result was committed to a continuing SelfModel."
    )
    print(final_path)


if __name__ == "__main__":
    main()
