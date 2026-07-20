from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.reflection_causal_live_runner import (
    run_real_causal_audit,
)


CONFIRMATION = "I_APPROVE_FOUR_ANALYTICAL_REFLECTION_CALLS"


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
            "This runner is live-call only. Use the integrated v8.0 suite "
            "for offline validation."
        )

    if args.confirmation != CONFIRMATION:
        raise SystemExit(
            "Four-call causal audit blocked. Supply "
            f'--confirmation "{CONFIRMATION}"'
        )

    if not (
        os.environ.get("DAGMAY_GEMINI_API_KEY")
        or os.environ.get("GEMINI_API_KEY")
    ):
        raise SystemExit(
            "Set DAGMAY_GEMINI_API_KEY or GEMINI_API_KEY first."
        )

    result = run_real_causal_audit(
        model_id=args.model,
    )

    path = (
        ROOT
        / "artifacts"
        / "reflection-causal-audit-real-latest.json"
    )
    path.write_text(
        json.dumps(
            result,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )
    print(
        "Completed four analytical reflection calls. "
        "No result was committed to a continuing SelfModel."
    )
    print(path)


if __name__ == "__main__":
    main()
