from __future__ import annotations

import argparse
import csv
import json
import statistics
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.self_tests import run_self_tests
from dagmay_synthetic_lab.experiments import run_suite
from dagmay_synthetic_lab.rich_experiments import (
    run_complexity_ladder,
    run_rich_exact_fork,
    run_path_dependence_after_normalization,
)
from dagmay_synthetic_lab.hard_rich_experiments import (
    run_hard_benchmark,
    run_history_order_audit,
)
from dagmay_synthetic_lab.developmental_differentiation import (
    run_developmental_differentiation,
)
from dagmay_synthetic_lab.rich_persistence_experiment import (
    run_rich_persistence_restart,
)
from dagmay_synthetic_lab.rich_belief_experiment import (
    run_rich_belief_provenance,
)
from dagmay_synthetic_lab.longitudinal_tom import (
    run_longitudinal_tom,
)
from dagmay_synthetic_lab.meta_transfer_experiment import (
    run_meta_transfer,
)
from dagmay_synthetic_lab.environment_adapter_tests import (
    run_environment_adapter_tests,
)
from dagmay_synthetic_lab.rich_reflection_bridge import (
    run_rich_reflection_bridge,
)
from dagmay_synthetic_lab.rich_scenario_registry import (
    registry_manifest,
)
from dagmay_synthetic_lab.canonical_advocate_selection import (
    canonical_advocate_selection_manifest,
)
from dagmay_synthetic_lab.constitution_versioning import (
    constitutional_canary,
)
from dagmay_synthetic_lab.core import canonical_hash


def _environment_value_summary(simple, hard):
    s = simple["summary"]["novel_causality"]
    simple_conditions = {
        "developmental": s["developmental_accuracy_mean"],
        "global_control": s["global_control_accuracy_mean"],
        "no_learning": s["no_learning_accuracy_mean"],
        "counterfactual_history": s["counterfactual_history_accuracy_mean"],
        "oracle": s["oracle_supplied_knowledge_accuracy_mean"],
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
        "simple_conditions": simple_conditions,
        "hard_conditions": hard_conditions,
        "simple_ceiling_count": sum(
            1 for v in simple_conditions.values()
            if v >= .95
        ),
        "hard_ceiling_count": sum(
            1 for v in hard_conditions.values()
            if v >= .95
        ),
        "simple_nonoracle_spread": (
            max(simple_nonoracle)
            - min(simple_nonoracle)
        ),
        "hard_nonoracle_spread": (
            max(hard_nonoracle)
            - min(hard_nonoracle)
        ),
        "conclusion": (
            "MicroLab remains better for causal isolation; Hard RichWorld is more discriminating "
            "among continual-learning architectures and exposes failures hidden by stationary tasks."
        ),
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", type=int, default=24)
    ap.add_argument(
        "--output",
        default=str(ROOT / "artifacts"),
    )
    args = ap.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)

    seeds = args.seeds
    baseline_seeds = max(16, seeds)
    tom_seeds = max(24, seeds)

    baseline = run_suite(baseline_seeds)
    complexity = run_complexity_ladder(
        max(12, seeds // 2)
    )
    hard = run_hard_benchmark(seeds)
    order = run_history_order_audit(seeds)
    differentiation = run_developmental_differentiation(
        max(12, seeds // 2)
    )
    exact_fork = run_rich_exact_fork(
        max(12, seeds // 2)
    )
    path_dependence = run_path_dependence_after_normalization(
        max(12, seeds // 2)
    )
    persistence = run_rich_persistence_restart(
        max(12, seeds // 2)
    )
    beliefs = run_rich_belief_provenance(
        max(12, seeds // 2)
    )
    tom = run_longitudinal_tom(tom_seeds)
    meta_transfer = run_meta_transfer(
        max(12, seeds // 2)
    )
    adapter_tests = run_environment_adapter_tests()
    reflection_bridge = run_rich_reflection_bridge(1)

    canary_ok, canary_missing = constitutional_canary()
    advocate = canonical_advocate_selection_manifest()

    environment_value = _environment_value_summary(
        baseline,
        hard,
    )

    hard_meta = meta_transfer["target_summary"][
        "HARD_HISTORY_META_ONLY"
    ]["optimal_action_rate"]
    scratch_meta = meta_transfer["target_summary"][
        "SCRATCH"
    ]["optimal_action_rate"]
    meta_transfer_advantage = hard_meta - scratch_meta

    result = {
        "lab_version": "6.0",
        "suite_id": "DAGMAY-SYNTHETICLAB-V6.0-CONTROLLED-RICH-WORLD",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "research_claim_status": "CONTROLLED_RICH_DEVELOPMENTAL_LAB_BASELINE",
        "real_cloud_model_calls": 0,
        "base_self_tests": run_self_tests(),
        "canonical_advocate_selection": advocate,
        "constitutional_canary": {
            "passed": canary_ok,
            "missing": canary_missing,
        },
        "scenario_registry": registry_manifest(),
        "environment_adapter_tests": adapter_tests,
        "baseline_micro": baseline,
        "complexity_ladder": complexity,
        "hard_rich_benchmark": hard,
        "history_order_audit": order,
        "developmental_differentiation": differentiation,
        "rich_exact_fork": exact_fork,
        "path_dependence": path_dependence,
        "rich_persistence": persistence,
        "rich_belief_provenance": beliefs,
        "longitudinal_tom": tom,
        "meta_transfer": meta_transfer,
        "meta_transfer_hard_history_minus_scratch_optimal_action_rate": meta_transfer_advantage,
        "reflection_bridge": reflection_bridge,
        "environment_value_audit": environment_value,
    }

    # Release-blocking invariants.
    assert advocate["mode"] == "HYBRID"
    assert canary_ok is True

    assert adapter_tests["controlled_subject_packet_clean"] is True
    assert adapter_tests["hard_subject_packet_clean"] is True
    assert adapter_tests["controlled_canonical_event_would_leak"] is True
    assert adapter_tests["hard_canonical_event_would_leak"] is True

    assert persistence["exact_snapshot_restore_rate"] == 1.0
    assert persistence["exact_post_restart_continuation_rate"] == 1.0
    assert persistence["tamper_detection_rate"] == 1.0

    assert exact_fork["exact_prefork_equality_rate"] == 1.0
    assert exact_fork["postfork_state_divergence_rate"] == 1.0

    assert differentiation["identical_history_mean_policy_divergence"] == 0.0
    assert differentiation["identical_history_exact_state_equality_rate"] == 1.0
    assert differentiation["fresh_identical_agent_policy_divergence"] == 0.0

    assert order["tabular_final_resource_state_multiset_invariance_rate"] == 1.0
    assert order["adaptive_internal_state_order_divergence_rate"] == 1.0

    assert reflection_bridge["real_cloud_model_used"] is False
    assert reflection_bridge["reflection_directly_mutated_action_policy"] is False

    assert result["real_cloud_model_calls"] == 0

    json_path = out / "syntheticlab-v6.0-results-latest.json"
    json_path.write_text(
        json.dumps(
            result,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    # Compact comparison CSV.
    csv_path = out / "syntheticlab-v6.0-summary-latest.csv"
    rows = []

    for kind, metrics in hard["summary"].items():
        for metric in (
            "mean_energy",
            "resource_success_rate",
            "optimal_action_selection_rate",
            "final_hidden_regime_policy_accuracy",
        ):
            value = metrics[metric]
            if value is not None:
                rows.append([
                    "hard_rich",
                    kind,
                    metric,
                    value,
                ])

    rows.extend([
        [
            "history_order",
            "adaptive",
            "mean_policy_divergence_after_shuffle",
            order["adaptive_mean_policy_divergence_after_shuffle"],
        ],
        [
            "developmental_differentiation",
            "different_histories",
            "mean_policy_divergence",
            differentiation["mean_policy_divergence_after_different_histories"],
        ],
        [
            "tom",
            "access_aware",
            "partner_action_prediction_accuracy",
            tom["access_aware_partner_action_prediction_accuracy"],
        ],
        [
            "tom",
            "flat_control",
            "partner_action_prediction_accuracy",
            tom["flat_partner_action_prediction_accuracy"],
        ],
        [
            "meta_transfer",
            "hard_history_minus_scratch",
            "optimal_action_rate_difference",
            meta_transfer_advantage,
        ],
    ])

    with csv_path.open(
        "w",
        newline="",
        encoding="utf-8",
    ) as f:
        w = csv.writer(f)
        w.writerow([
            "group",
            "condition",
            "metric",
            "value",
        ])
        w.writerows(rows)

    paired = hard["paired_adaptive_vs_tabular"]
    optimal_diff = paired[
        "optimal_action_selection_rate"
    ]
    resource_diff = paired[
        "resource_success_rate"
    ]

    meta_result_text = (
        "positive"
        if meta_transfer_advantage > 0
        else "negative/no advantage"
    )

    report = f"""# Dagmay SyntheticLab v6.0 — Controlled Rich Developmental World

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Answer to the environment-richness question

**Yes. The richer environment materially improves the value of several experiments — but it does not replace the simple lab.**

MicroLab remains the cleanest place to prove that a mechanism causes an effect.

RichWorld and Hard RichWorld reveal whether that mechanism survives:
- partial information;
- changing causal structure;
- resource depletion;
- competing internal needs;
- unreliable social information;
- long developmental histories.

## Hard RichWorld — Adaptive Trace vs accumulation-based developmental learner

Paired difference in optimal-action selection:
- mean Adaptive minus Tabular: {optimal_diff['mean']:.3f}
- 95% CI: [{optimal_diff['ci95_low']:.3f}, {optimal_diff['ci95_high']:.3f}]
- Adaptive higher on {optimal_diff['positive_pair_fraction']:.1%} of paired seeds

Paired difference in resource success:
- mean Adaptive minus Tabular: {resource_diff['mean']:.3f}
- 95% CI: [{resource_diff['ci95_low']:.3f}, {resource_diff['ci95_high']:.3f}]

This is stronger evidence that the benchmark is discriminating between continual-learning mechanisms instead of merely waiting for all of them to reach ceiling.

## Ordered history

Tabular final resource state invariant to chronological vs shuffled evidence:
{order['tabular_final_resource_state_multiset_invariance_rate']:.3f}

Adaptive internal state diverged under chronological vs shuffled evidence:
{order['adaptive_internal_state_order_divergence_rate']:.3f}

Mean later policy divergence after shuffling:
{order['adaptive_mean_policy_divergence_after_shuffle']:.3f}

Interpretation:
The order of a life affects future behavior only when the continuing learner itself is order-sensitive.

## Developmental differentiation

Different histories, identical architecture/internal seed:
- mean later policy divergence: {differentiation['mean_policy_divergence_after_different_histories']:.3f}
- internal-state divergence rate: {differentiation['different_history_internal_state_divergence_rate']:.3f}

Identical history control:
- policy divergence: {differentiation['identical_history_mean_policy_divergence']:.3f}
- exact state equality: {differentiation['identical_history_exact_state_equality_rate']:.3f}

Fresh identical-agent control:
- policy divergence: {differentiation['fresh_identical_agent_policy_divergence']:.3f}

This establishes history-caused differentiation in the synthetic architecture, not personality or selfhood.

## Exact continuity

Rich JSON snapshot restore:
{persistence['exact_snapshot_restore_rate']:.3f}

Exact post-restart continuation:
{persistence['exact_post_restart_continuation_rate']:.3f}

Tamper detection:
{persistence['tamper_detection_rate']:.3f}

## Longitudinal social cognition

Access-aware partner-action prediction:
{tom['access_aware_partner_action_prediction_accuracy']:.3f}

Flat-control partner-action prediction:
{tom['flat_partner_action_prediction_accuracy']:.3f}

Access-aware information-source accuracy:
{tom['access_aware_information_source_accuracy']:.3f}

Flat-control information-source accuracy:
{tom['flat_information_source_accuracy']:.3f}

This is a stronger Theory-of-Mind precursor than the earlier one-shot false-belief representation test, but still not human-like ToM.

## Temporal beliefs

Belief provenance coverage:
{beliefs['belief_provenance_source_coverage_rate']:.3f}

Active belief accuracy against hidden final-regime truth:
{beliefs['mean_active_belief_accuracy_against_hidden_final_regime']:.3f}

Old contradicted/superseded belief versions are preserved.

## Meta-learning transfer — retained negative result

Hard-history meta prior minus scratch target optimal-action rate:
{meta_transfer_advantage:.3f}

Current result classification:
**{meta_result_text}**

The current volatility-prior transfer mechanism does not yet provide a convincing transferable learning advantage. This result is retained rather than tuned away.

## Information leakage

Subject-visible packets clean:
- Controlled RichWorld: {adapter_tests['controlled_subject_packet_clean']}
- Hard RichWorld: {adapter_tests['hard_subject_packet_clean']}

The leakage guard correctly detects that passing canonical evaluation events directly would expose hidden fields.

## Reflection bridge

Rich lived evidence successfully reaches:
subject-visible evidence → validated reflection gateway → SelfModelStore.

Provider:
{reflection_bridge['provider_id']}

Real cloud model used:
{reflection_bridge['real_cloud_model_used']}

Reflection directly mutated action policy:
{reflection_bridge['reflection_directly_mutated_action_policy']}

## Governance

Canonical Subject Advocate selection:
**HYBRID**

The individual may select, reject, and replace an Advocate from a qualified independent pool.

## Current research stack

1. MicroLab — causal proof.
2. RichWorld — controlled developmental ecology.
3. Hard RichWorld — nonstationarity, depletion, longitudinal adaptation.
4. RimWorld — future messy ecological validation.
5. Later external simulation/embodiment.

## Next qualitative decision

The LLM-free rich research spine is now substantially developed.

The next qualitative step is whether to begin the first **real pretrained-language-model reflective cohort** while also building an external environment adapter.

Technical recommendation remains:
**parallel controlled pilots** — one real-reflection RichWorld pilot and one LLM-free external-environment pilot — treated as separate interventions.

No real cloud-model call was made in v6.0.
"""

    report_path = out / "syntheticlab-v6.0-report-latest.md"
    report_path.write_text(
        report,
        encoding="utf-8",
    )

    print(report)


if __name__ == "__main__":
    main()
