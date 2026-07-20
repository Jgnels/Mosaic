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

from dagmay_synthetic_lab.autobiographical_retrieval_ablation import (
    run_autobiographical_retrieval_ablation,
    analyze_autobiographical_retrieval_ablation,
)


CONFIRMATION = (
    "I_APPROVE_NINETY_AUTOBIOGRAPHICAL_RETRIEVAL_CALLS"
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
            "Live autobiographical retrieval ablation blocked: "
            "--execute-real is required."
        )

    if (
        args.confirmation
        != CONFIRMATION
    ):
        raise SystemExit(
            "Live ablation blocked. Supply "
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
        / "autobiographical-retrieval-ablation-progress.json"
    )

    result = run_autobiographical_retrieval_ablation(
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

    analysis = analyze_autobiographical_retrieval_ablation(
        payload=(
            result
        ),
        baseline_analysis=(
            baseline_analysis
        ),
        v27_analysis=(
            v27_analysis
        ),
        permutations=(
            20000
        ),
        seed=(
            2901
        ),
    )

    real_path = (
        ROOT
        / "artifacts"
        / "autobiographical-retrieval-ablation-real-latest.json"
    )

    analysis_path = (
        ROOT
        / "artifacts"
        / "autobiographical-retrieval-ablation-analysis-latest.json"
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
        "Completed autobiographical retrieval causality ablation."
    )
    print(
        "72 shadow revision calls + 18 blinded semantic calls."
    )
    print(
        "Only verified own-history evidence was retrieved."
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
