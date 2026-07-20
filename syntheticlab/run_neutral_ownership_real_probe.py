from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(
    0,
    str(ROOT / "src"),
)

from dagmay_synthetic_lab.ownership_discovery_provider import (
    run_three_permutation_real_probe,
)


CONFIRMATION = (
    "I_APPROVE_THREE_NEUTRAL_OWNERSHIP_CALLS"
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
    ap.add_argument(
        "--seed",
        type=int,
        default=307,
    )
    args = ap.parse_args()

    if not args.execute_real:
        raise SystemExit(
            "Live probe blocked: --execute-real is required."
        )

    if args.confirmation != CONFIRMATION:
        raise SystemExit(
            "Live probe blocked. Supply "
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

    result = (
        run_three_permutation_real_probe(
            seed=args.seed,
            model_id=args.model,
        )
    )

    path = (
        ROOT
        / "artifacts"
        / "neutral-ownership-real-probe-latest.json"
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
        "Completed three neutral ownership-discovery calls."
    )
    print(
        "No result was committed to a continuing SelfModel."
    )
    print(path)


if __name__ == "__main__":
    main()
