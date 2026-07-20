from __future__ import annotations

from .character_quality_suite import run_memory_relevance_experiment


def run() -> dict[str, float]:
    result = run_memory_relevance_experiment(32)
    summary = result["summary"]
    assert result["provider_calls"] == 0
    assert result["canonical_mutation"] is False
    assert summary["mosaic_recall"] >= 0.95
    assert summary["mosaic_causal_pair"] >= 0.95
    assert summary["recall_gain_over_recency"] >= 0.75
    return summary


if __name__ == "__main__":
    print(run())

