from __future__ import annotations

from .goal_continuity_experiment import run


def test() -> dict[str, float]:
    result = run(256)
    summary = result["summary"]
    assert result["provider_calls"] == 0
    assert result["canonical_mutation"] is False
    assert summary["switch_reduction"] >= 0.70
    assert summary["mean_utility_regret"] <= 0.08
    assert summary["emergency_compliance"] == 1.0
    assert summary["post_emergency_resume"] == 1.0
    return summary


if __name__ == "__main__":
    print(test())
