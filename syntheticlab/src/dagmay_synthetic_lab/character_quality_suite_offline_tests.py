from __future__ import annotations

from .character_quality_suite import (
    run_adversarial_memory_experiment,
    run_memory_relevance_experiment,
)


def run() -> dict[str, float]:
    result = run_memory_relevance_experiment(32)
    summary = result["summary"]
    assert result["provider_calls"] == 0
    assert result["canonical_mutation"] is False
    assert summary["mosaic_recall"] >= 0.95
    assert summary["mosaic_causal_pair"] >= 0.95
    assert summary["recall_gain_over_recency"] >= 0.75
    adversarial = run_adversarial_memory_experiment(32)["summary"]
    assert adversarial["mosaic_recall"] >= 0.95
    assert adversarial["mosaic_causal_pair"] >= 0.95
    assert adversarial["mosaic_rumor_rate"] == 0.0
    assert adversarial["surface_rumor_rate"] >= 0.95
    return {**summary, **{f"adversarial_{k}": v for k, v in adversarial.items()}}


if __name__ == "__main__":
    print(run())
