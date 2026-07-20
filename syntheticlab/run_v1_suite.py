from __future__ import annotations
import argparse,json,csv,sys
from pathlib import Path
from datetime import datetime,timezone
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/"src"))
from dagmay_synthetic_lab.self_tests import run_self_tests
from dagmay_synthetic_lab.experiments import run_suite
from dagmay_synthetic_lab.advanced_experiments import run_advanced_suite
from dagmay_synthetic_lab.beliefs import run_rumor_retraction_scenario
from dagmay_synthetic_lab.cohort import run_passive_factorial_cohort
from dagmay_synthetic_lab.active_development import run_active_cohort
from dagmay_synthetic_lab.appraisal_lab import run_appraisal_lab
from dagmay_synthetic_lab.relationships import run_relationship_lab
from dagmay_synthetic_lab.segmentation import run_segmentation_lab
from dagmay_synthetic_lab.replay_lab import run_replay_lab
from dagmay_synthetic_lab.skills import run_skill_store_lab
from dagmay_synthetic_lab.drives import PROFILES
from dagmay_synthetic_lab.core import canonical_hash

def main():
    ap=argparse.ArgumentParser();ap.add_argument("--seeds",type=int,default=64);ap.add_argument("--output",default=str(ROOT/"artifacts"));args=ap.parse_args()
    out=Path(args.output);out.mkdir(parents=True,exist_ok=True)
    result={
        "lab_version":"1.0","suite_id":"DAGMAY-SYNTHETICLAB-V1.0",
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "research_claim_status":"ACTIVE_DEVELOPMENT_AND_METHOD_COMPARISON_BASELINE",
        "self_tests":run_self_tests(),
        "baseline":run_suite(args.seeds),
        "advanced":run_advanced_suite(args.seeds),
        "temporal_belief":run_rumor_retraction_scenario(),
        "passive_cohort":run_passive_factorial_cohort(args.seeds),
        "active_cohort":run_active_cohort(args.seeds),
        "appraisal_lab":run_appraisal_lab(),
        "relationship_lab":run_relationship_lab(),
        "segmentation_lab":run_segmentation_lab(args.seeds),
        "replay_lab":run_replay_lab(args.seeds),
        "skill_store_lab":run_skill_store_lab(),
        "drive_profiles":{k:v.to_dict() for k,v in PROFILES.items()},
        "canonical_drive_profile":"BALANCED_MINIMAL",
        "canonical_drive_profile_user_approved":True,
    }
    (out/"syntheticlab-v1.0-results-latest.json").write_text(json.dumps(result,indent=2,sort_keys=True),encoding="utf-8")
    rows=[]
    for profile,metrics in result["active_cohort"]["summary"].items():
        for metric,value in metrics.items():rows.append(["active_cohort",profile,metric,value])
    for method,value in result["segmentation_lab"]["mean_boundary_f1"].items():rows.append(["segmentation",method,"boundary_f1_mean",value])
    for policy,metrics in result["replay_lab"]["summary"].items():
        for metric,value in metrics.items():rows.append(["replay",policy,metric,value])
    with (out/"syntheticlab-v1.0-summary-latest.csv").open("w",newline="",encoding="utf-8") as f:
        w=csv.writer(f);w.writerow(["group","condition","metric","value"]);w.writerows(rows)
    ac=result["active_cohort"]["summary"];seg=result["segmentation_lab"]["mean_boundary_f1"]
    report=f"""# Dagmay SyntheticLab v1.0 — Integrated Research Report

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Active developmental cohort

Canonical: **BALANCED_MINIMAL**  
Ablations: MINIMAL_REGULATION, EPISTEMIC_MINIMAL, NONE

| Profile | Causal model accuracy | Mean need | Success rate | Action entropy |
|---|---:|---:|---:|---:|
| Balanced Minimal | {ac['BALANCED_MINIMAL']['causal_model_accuracy_mean']:.3f} | {ac['BALANCED_MINIMAL']['mean_need_mean']:.3f} | {ac['BALANCED_MINIMAL']['success_rate_mean']:.3f} | {ac['BALANCED_MINIMAL']['action_entropy_mean']:.3f} |
| Minimal Regulation | {ac['MINIMAL_REGULATION']['causal_model_accuracy_mean']:.3f} | {ac['MINIMAL_REGULATION']['mean_need_mean']:.3f} | {ac['MINIMAL_REGULATION']['success_rate_mean']:.3f} | {ac['MINIMAL_REGULATION']['action_entropy_mean']:.3f} |
| Epistemic Minimal | {ac['EPISTEMIC_MINIMAL']['causal_model_accuracy_mean']:.3f} | {ac['EPISTEMIC_MINIMAL']['mean_need_mean']:.3f} | {ac['EPISTEMIC_MINIMAL']['success_rate_mean']:.3f} | {ac['EPISTEMIC_MINIMAL']['action_entropy_mean']:.3f} |
| None | {ac['NONE']['causal_model_accuracy_mean']:.3f} | {ac['NONE']['mean_need_mean']:.3f} | {ac['NONE']['success_rate_mean']:.3f} | {ac['NONE']['action_entropy_mean']:.3f} |

These are toy-world engineering baselines, not an optimal psychology result.

## Appraisal Lab
Pure, evidence-linked comparison engines:
- Dagmay baseline
- GAMYGDALA-inspired
- FAtiMA-inspired
- Psi-inspired

No winner is selected. Behavioral validation is required.

## Directed Relationship Lab
Implemented Trust, Affection, Fear, Resentment, Familiarity; directional asymmetry; rumor/retraction; recompute-from-evidence semantics; repeated help vs single rescue.

## Event Segmentation Lab
- fixed window F1: {seg['fixed_window']:.3f}
- context rule F1: {seg['context_rule']:.3f}
- prediction-error F1: {seg['prediction_error']:.3f}
- hybrid F1: {seg['hybrid']:.3f}

No production winner is selected from synthetic ground truth alone.

## Replay Lab
Policies: none, recent, significant, reservoir, dual. The lab preserves the stability/plasticity tradeoff rather than declaring one universally correct.

## Procedural Skill Store
Voyager-inspired separation between autobiographical evidence and validated competence. Stable IDs, versions, source episodes, pre/postconditions, validated executor bindings, and no arbitrary generated-code execution.

## Next creative/ethical gate
Before adding a real reflective LLM to persistent SyntheticLab individuals:

**What should the first language-enabled individuals initially know about themselves?**

A. Explicit disclosure that they are artificial research agents.  
B. Restricted self-knowledge; no AI/simulation/personhood labels.  
C. Hybrid staged disclosure: begin restricted, then disclose at a preregistered threshold.

Technical recommendation: **C**.
"""
    (out/"syntheticlab-v1.0-report-latest.md").write_text(report,encoding="utf-8")
    print(report)
if __name__=="__main__":main()
