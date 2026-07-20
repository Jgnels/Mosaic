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

from dagmay_synthetic_lab.crossed_foreign_history_control import (
    run_crossed_foreign_history_control,
    analyze_crossed_foreign_history_control,
)


CONFIRMATION = (
    "I_APPROVE_NINETY_CROSSED_FOREIGN_HISTORY_CONTROL_CALLS"
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
            "Live crossed foreign-history control blocked: "
            "--execute-real is required."
        )

    if (
        args.confirmation
        != CONFIRMATION
    ):
        raise SystemExit(
            "Live control blocked. Supply "
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

    lived_history_payload = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-ENACTED-LIVED-EVIDENCE-CANONICAL-REVISION-PILOT-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    own_history_analysis = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-AUTOBIOGRAPHICAL-RETRIEVAL-CAUSALITY-ABLATION-ANALYSIS-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    baseline_analysis = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-BRANCH-HISTORY-SIGNAL-REPLICATION-ANALYSIS-001.json"
        ).read_text(
            encoding="utf-8"
        )
    )

    progress = (
        ROOT
        / "artifacts"
        / "crossed-foreign-history-control-progress.json"
    )

    result = run_crossed_foreign_history_control(
        checkpoint_path=(
            progress
        ),
        revision_result=(
            revision_result
        ),
        lived_history_payload=(
            lived_history_payload
        ),
        model_id=(
            args.model
        ),
    )

    analysis = analyze_crossed_foreign_history_control(
        payload=(
            result
        ),
        own_history_analysis=(
            own_history_analysis
        ),
        baseline_analysis=(
            baseline_analysis
        ),
        permutations=(
            20000
        ),
        seed=(
            3001
        ),
    )

    real_path = (
        ROOT
        / "artifacts"
        / "crossed-foreign-history-control-real-latest.json"
    )

    analysis_path = (
        ROOT
        / "artifacts"
        / "crossed-foreign-history-control-analysis-latest.json"
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
        "Completed crossed foreign-history retrieval control."
    )
    print(
        "72 shadow revision calls + 18 blinded semantic calls."
    )
    print(
        "Foreign histories were explicitly labeled as external references, never as self-history."
    )
    print(
        "No canonical state or memory content was changed."
    )
    print(
        real_path
    )
    print(
        analysis_path
    )


if __name__ == "__main__":
    main()
