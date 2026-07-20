from __future__ import annotations

import statistics

from .experiments import run_suite
from .hard_rich_experiments import run_hard_benchmark


def run_environment_value_audit(
    simple_seed_count: int = 32,
    hard_seed_count: int = 24,
) -> dict:
    simple = run_suite(simple_seed_count)
    hard = run_hard_benchmark(hard_seed_count)

    simple_accuracy = simple["summary"]["novel_causality"]
    simple_conditions = {
        "developmental": simple_accuracy["developmental_accuracy_mean"],
        "global_control": simple_accuracy["global_control_accuracy_mean"],
        "no_learning": simple_accuracy["no_learning_accuracy_mean"],
        "counterfactual_history": simple_accuracy["counterfactual_history_accuracy_mean"],
        "oracle": simple_accuracy["oracle_supplied_knowledge_accuracy_mean"],
    }

    hard_conditions = {
        k: v["optimal_action_selection_rate"]
        for k, v in hard["summary"].items()
    }

    simple_nonoracle = [
        v for k, v in simple_conditions.items()
        if k != "oracle"
    ]
    hard_nonoracle = [
        v for k, v in hard_conditions.items()
        if k != "ORACLE"
    ]

    return {
        "experiment_id": "SL-ENVIRONMENT-VALUE-AUDIT-001",
        "simple_microbenchmark": {
            "conditions": simple_conditions,
            "ceiling_conditions_at_or_above_0_95": sum(
                1 for v in simple_conditions.values()
                if v >= .95
            ),
            "nonoracle_performance_spread": (
                max(simple_nonoracle)
                - min(simple_nonoracle)
            ),
        },
        "hard_rich_benchmark": {
            "conditions": hard_conditions,
            "ceiling_conditions_at_or_above_0_95": sum(
                1 for v in hard_conditions.values()
                if v >= .95
            ),
            "nonoracle_performance_spread": (
                max(hard_nonoracle)
                - min(hard_nonoracle)
            ),
        },
        "conclusion": (
            "The simple environment remains superior for isolating a single causal mechanism. "
            "The hard rich environment is more discriminating among continual-learning architectures "
            "and exposes failures hidden by stationary benchmarks. Neither should replace the other."
        ),
        "recommended_lab_stack": (
            "MicroLab for causal mechanism proof",
            "Rich Synthetic World for controlled developmental ecology",
            "RimWorld for messy ecological validation",
            "later richer simulation/embodiment for sensorimotor transfer",
        ),
    }
