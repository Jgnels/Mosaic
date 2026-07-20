from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.own_history_attribution_framing_ablation import (
    run_own_history_attribution_framing_ablation,
    analyze_own_history_attribution_framing,
)


CONFIRMATION = (
    "I_APPROVE_NINETY_OWN_HISTORY_ATTRIBUTION_FRAMING_CALLS"
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
            "Live own-history attribution framing ablation blocked: "
            "--execute-real is required."
        )

    if args.confirmation != CONFIRMATION:
        raise SystemExit(
            "Live ablation blocked. Supply "
            f'--confirmation "{CONFIRMATION}"'
        )

    if not (
        os.environ.get("DAGMAY_GEMINI_API_KEY")
        or os.environ.get("GEMINI_API_KEY")
    ):
        raise SystemExit(
            "Set DAGMAY_GEMINI_API_KEY or GEMINI_API_KEY first."
        )

    revision_result = json.loads(
        (
            ROOT
            / "artifacts"
            / "approved-divergent-branch-canonical-revision-latest.json"
        ).read_text(encoding="utf-8")
    )

    lived_history_payload = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-ENACTED-LIVED-EVIDENCE-CANONICAL-REVISION-PILOT-001.json"
        ).read_text(encoding="utf-8")
    )

    baseline_analysis = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-BRANCH-HISTORY-SIGNAL-REPLICATION-ANALYSIS-001.json"
        ).read_text(encoding="utf-8")
    )

    v27_analysis = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-SEMANTIC-MEASUREMENT-RELIABILITY-ANALYSIS-001.json"
        ).read_text(encoding="utf-8")
    )

    own_history_analysis = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-AUTOBIOGRAPHICAL-RETRIEVAL-CAUSALITY-ABLATION-ANALYSIS-001.json"
        ).read_text(encoding="utf-8")
    )

    foreign_history_analysis = json.loads(
        (
            ROOT
            / "artifacts"
            / "real_reflection_evidence"
            / "SL-CROSSED-FOREIGN-HISTORY-RETRIEVAL-CONTROL-ANALYSIS-001.json"
        ).read_text(encoding="utf-8")
    )

    progress = (
        ROOT
        / "artifacts"
        / "own-history-attribution-framing-progress.json"
    )

    result = run_own_history_attribution_framing_ablation(
        checkpoint_path=progress,
        revision_result=revision_result,
        lived_history_payload=lived_history_payload,
        model_id=args.model,
    )

    analysis = analyze_own_history_attribution_framing(
        payload=result,
        baseline_analysis=baseline_analysis,
        v27_analysis=v27_analysis,
        own_history_analysis=own_history_analysis,
        foreign_history_analysis=foreign_history_analysis,
        permutations=20000,
        seed=3101,
    )

    real_path = (
        ROOT
        / "artifacts"
        / "own-history-attribution-framing-real-latest.json"
    )

    analysis_path = (
        ROOT
        / "artifacts"
        / "own-history-attribution-framing-analysis-latest.json"
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
        "Completed own-history attribution framing ablation."
    )
    print(
        "72 shadow revision calls + 18 blinded semantic calls."
    )
    print(
        "The provider received the focal branch's real history content, "
        "but its source relation to the focal individual was withheld."
    )
    print(
        "No canonical state or memory content was changed."
    )
    print(real_path)
    print(analysis_path)


if __name__ == "__main__":
    main()
