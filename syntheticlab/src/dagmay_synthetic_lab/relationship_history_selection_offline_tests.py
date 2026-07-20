from __future__ import annotations

from .relationship_history_selection_experiment import run


def test() -> dict[str, float]:
    result = run(128)
    summary = result["summary"]
    assert result["provider_calls"] == 0
    assert result["canonical_mutation"] is False
    assert summary["mosaic_recall"] == 1.0
    assert summary["mosaic_rumor_rate"] == 0.0
    assert summary["mosaic_balanced"] == 1.0
    assert summary["mosaic_dialogue_bounded"] == 1.0
    assert summary["recency_recall"] == 0.0
    assert summary["recency_rumor_rate"] == 1.0
    return summary


if __name__ == "__main__":
    print(test())
