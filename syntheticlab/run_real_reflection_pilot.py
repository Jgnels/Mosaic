from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.real_reflection_pilot import (
    run_offline_real_provider_contract,
    execute_first_real_reflection,
)


CONFIRMATION = "I_APPROVE_ONE_REAL_REFLECTION"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument(
        "--execute-real",
        action="store_true",
        help="Perform exactly one real Gemini reflection call.",
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
        default=101,
    )
    args = ap.parse_args()

    out = ROOT / "artifacts"
    out.mkdir(exist_ok=True)

    if not args.execute_real:
        result = run_offline_real_provider_contract(
            seed=args.seed,
            model_id=args.model,
        )
        path = out / (
            "real-reflection-offline-contract-latest.json"
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
            "Offline provider-contract test completed. "
            "No cloud call was made."
        )
        print(path)
        return

    if args.confirmation != CONFIRMATION:
        raise SystemExit(
            "Real call blocked. Supply "
            f'--confirmation "{CONFIRMATION}"'
        )

    if not (
        os.environ.get("DAGMAY_GEMINI_API_KEY")
        or os.environ.get("GEMINI_API_KEY")
    ):
        raise SystemExit(
            "Real call blocked. Set DAGMAY_GEMINI_API_KEY "
            "or GEMINI_API_KEY in the environment."
        )

    result = execute_first_real_reflection(
        seed=args.seed,
        model_id=args.model,
    )
    path = out / (
        "real-reflection-pilot-latest.json"
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
        "Exactly one real reflection call completed."
    )
    print(path)


if __name__ == "__main__":
    main()
