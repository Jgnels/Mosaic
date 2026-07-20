from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.external_environment_pilot import (
    run_fake_external_contract,
    run_real_minigrid_smoke_if_available,
)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument(
        "--steps-per-env",
        type=int,
        default=600,
    )
    ap.add_argument(
        "--seed",
        type=int,
        default=42,
    )
    args = ap.parse_args()

    out = ROOT / "artifacts"
    out.mkdir(exist_ok=True)

    contract = run_fake_external_contract(
        seed_count=16
    )
    real = run_real_minigrid_smoke_if_available(
        seed=args.seed,
        steps_per_env=args.steps_per_env,
    )

    result = {
        "offline_adapter_contract": contract,
        "real_minigrid": real,
    }
    path = out / (
        "external-minigrid-pilot-latest.json"
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
        json.dumps(
            result,
            indent=2,
        )
    )
    print(path)


if __name__ == "__main__":
    main()
