import json
from pathlib import Path

from dagmay_synthetic_lab.retrieval_attention_experiments import (
    run_retrieval_attention_experiment,
)

ROOT = Path(__file__).resolve().parent

longitudinal = json.loads(
    (
        ROOT
        / "artifacts"
        / "real_reflection_evidence"
        / "SL-LONGITUDINAL-REFLECTION-ANALYSIS-001.json"
    ).read_text(encoding="utf-8")
)

result = run_retrieval_attention_experiment(
    longitudinal_analysis=longitudinal,
    seed_count=128,
    history_steps=650,
    retrieval_cycles=80,
)

(
    ROOT
    / "artifacts"
    / "retrieval-attention-feedback-latest.json"
).write_text(
    json.dumps(
        result,
        indent=2,
        sort_keys=True,
    ),
    encoding="utf-8",
)

summary = {
    key: value
    for key, value
    in result.items()
    if key != "rows"
}

print(json.dumps(summary, indent=2))
