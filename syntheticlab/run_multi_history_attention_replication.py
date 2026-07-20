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

from dagmay_synthetic_lab.multi_history_attention_replication import (
    run_multi_history_attention_replication,
    analyze_multi_history_replication,
)


CONFIRMATION = (
    "I_APPROVE_THIRTY_SIX_MULTI_HISTORY_ATTENTION_CALLS"
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
            "Live multi-history replication blocked: "
            "--execute-real is required."
        )

    if args.confirmation != CONFIRMATION:
        raise SystemExit(
            "Live multi-history replication blocked. Supply "
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
        / "multi-history-attention-replication-progress.json"
    )

    final_path = (
        ROOT
        / "artifacts"
        / "multi-history-attention-replication-real-latest.json"
    )

    analysis_path = (
        ROOT
        / "artifacts"
        / "multi-history-attention-replication-analysis-latest.json"
    )

    result = (
        run_multi_history_attention_replication(
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
            "Replication stopped before completion. "
            "Progress is checkpointed; rerun the same command."
        )

    analysis = (
        analyze_multi_history_replication(
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
        "Completed 36 multi-history attention-replication calls."
    )
    print(
        "Three independent histories were tested with deterministic "
        "evidence-order randomization."
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
