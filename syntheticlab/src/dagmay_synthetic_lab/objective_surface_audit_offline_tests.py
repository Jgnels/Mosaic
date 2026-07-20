from __future__ import annotations

from pathlib import Path

from .objective_surface_audit import run_audit


def run() -> dict[str, object]:
    repo = Path(__file__).resolve().parents[3]
    result = run_audit(repo)
    assert result["status"] == "PASS"
    assert not result["failed_checks"]
    return result


if __name__ == "__main__":
    print(run())

