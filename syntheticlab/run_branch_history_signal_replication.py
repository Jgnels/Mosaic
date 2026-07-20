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

from dagmay_synthetic_lab.branch_history_signal_replication import (
    run_branch_history_signal_replication,
    analyze_branch_history_signal_replication,
)


CONFIRMATION = (
    "I_APPROVE_SIXTY_SIX_BRANCH_SIGNAL_REPLICATION_CALLS"
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
            "Live branch-history signal replication blocked: "
            "--execute-real is required."
        )

    if (
        args.confirmation
        != CONFIRMATION
    ):
        raise SystemExit(
            "Live replication blocked. Supply "
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

    multi_epoch_payload = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-MULTI-EPOCH-COMMON-FUTURE-CONVERGENCE-PILOT-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    v27_analysis = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-SEMANTIC-MEASUREMENT-RELIABILITY-ANALYSIS-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    progress = (
        ROOT
        / "artifacts"
        / "branch-history-signal-replication-progress.json"
    )

    result = run_branch_history_signal_replication(
        checkpoint_path=(
            progress
        ),
        revision_result=(
            revision_result
        ),
        multi_epoch_payload=(
            multi_epoch_payload
        ),
        model_id=(
            args.model
        ),
    )

    analysis = analyze_branch_history_signal_replication(
        payload=(
            result
        ),
        v27_analysis=(
            v27_analysis
        ),
        permutations=(
            20000
        ),
        seed=(
            2801
        ),
    )

    real_path = (
        ROOT
        / "artifacts"
        / "branch-history-signal-replication-real-latest.json"
    )

    analysis_path = (
        ROOT
        / "artifacts"
        / "branch-history-signal-replication-analysis-latest.json"
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
        "Completed branch-history signal replication."
    )
    print(
        "48 sequential shadow-revision calls + 18 blinded final semantic calls."
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
