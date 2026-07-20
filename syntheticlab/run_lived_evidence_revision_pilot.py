from __future__ import annotations
import argparse
import json
import os
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.lived_evidence_revision_pilot import (
    run_lived_evidence_revision_pilot,
    analyze_lived_evidence_revision_pilot,
)

CONFIRMATION = (
    "I_APPROVE_TWELVE_LIVED_EVIDENCE_REVISION_CALLS"
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
            "Live lived-evidence revision pilot blocked: --execute-real is required."
        )

    if args.confirmation != CONFIRMATION:
        raise SystemExit(
            f'Live pilot blocked. Supply --confirmation "{CONFIRMATION}"'
        )

    if not (
        os.environ.get("DAGMAY_GEMINI_API_KEY")
        or os.environ.get("GEMINI_API_KEY")
    ):
        raise SystemExit(
            "Set DAGMAY_GEMINI_API_KEY or GEMINI_API_KEY first."
        )

    promotion = json.loads(
        (
            ROOT
            / "artifacts"
            / "bounded-canonical-promotion-latest.json"
        ).read_text(encoding="utf-8")
    )

    progress = (
        ROOT
        / "artifacts"
        / "lived-evidence-revision-progress.json"
    )

    result = run_lived_evidence_revision_pilot(
        checkpoint_path=progress,
        promotion_result=promotion,
        model_id=args.model,
    )

    if result["status"] != "COMPLETE":
        raise SystemExit(
            "Pilot stopped before completion. Progress is checkpointed; rerun."
        )

    analysis = analyze_lived_evidence_revision_pilot(
        result
    )

    real_path = (
        ROOT
        / "artifacts"
        / "lived-evidence-revision-real-latest.json"
    )
    analysis_path = (
        ROOT
        / "artifacts"
        / "lived-evidence-revision-analysis-latest.json"
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

    print("Completed 12 lived-evidence revision calls.")
    print("All revision candidates remain quarantined.")
    print("No canonical revision was committed.")
    print(real_path)
    print(analysis_path)


if __name__ == "__main__":
    main()
