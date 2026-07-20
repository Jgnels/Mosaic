from __future__ import annotations
import argparse,json,csv,sys,statistics,math
from pathlib import Path
from datetime import datetime,timezone

ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/"src"))

from dagmay_synthetic_lab.self_tests import run_self_tests
from dagmay_synthetic_lab.active_development import run_active_cohort
from dagmay_synthetic_lab.appraisal_lab import run_appraisal_lab
from dagmay_synthetic_lab.relationships import run_relationship_lab
from dagmay_synthetic_lab.segmentation import run_segmentation_lab
from dagmay_synthetic_lab.replay_lab import run_replay_lab
from dagmay_synthetic_lab.skills import run_skill_store_lab
from dagmay_synthetic_lab.beliefs import run_rumor_retraction_scenario
from dagmay_synthetic_lab.hardening import (
    run_hard_segmentation,run_hard_replay,run_learned_tom,
    run_relationship_behavior,run_cross_environment_transfer,run_curriculum)
from dagmay_synthetic_lab.persistence_lab import run_persistence_restart
from dagmay_synthetic_lab.core import canonical_hash

def main():
    ap=argparse.ArgumentParser();ap.add_argument("--seeds",type=int,default=128);ap.add_argument("--output",default=str(ROOT/"artifacts"));args=ap.parse_args()
    out=Path(args.output);out.mkdir(parents=True,exist_ok=True)
    result={
      "lab_version":"1.2",
      "suite_id":"DAGMAY-SYNTHETICLAB-V1.2-HARDENING",
      "generated_utc":datetime.now(timezone.utc).isoformat(),
      "research_claim_status":"SCIENTIFIC_HARDENING_BASELINE",
      "self_tests":run_self_tests(),
      "active_cohort":run_active_cohort(args.seeds),
      "appraisal_lab":run_appraisal_lab(),
      "relationship_lab":run_relationship_lab(),
      "temporal_belief":run_rumor_retraction_scenario(),
      "segmentation_original":run_segmentation_lab(args.seeds),
      "segmentation_hard":run_hard_segmentation(args.seeds),
      "replay_original":run_replay_lab(args.seeds),
      "replay_hard":run_hard_replay(args.seeds),
      "learned_tom":run_learned_tom(args.seeds),
      "relationship_behavior":run_relationship_behavior(args.seeds),
      "cross_environment_transfer":run_cross_environment_transfer(args.seeds),
      "curriculum":run_curriculum(args.seeds),
      "persistence_restart":run_persistence_restart(args.seeds),
      "next_human_gate":"reflective self-knowledge/disclosure policy",
    }
    (out/"syntheticlab-v1.2-results-latest.json").write_text(json.dumps(result,indent=2,sort_keys=True),encoding="utf-8")
    rows=[]
    for k,v in result["segmentation_hard"]["mean_f1"].items():rows.append(["hard_segmentation",k,"mean_f1",v])
    for p,metrics in result["replay_hard"]["summary"].items():
        for k,v in metrics.items():rows.append(["hard_replay",p,k,v])
    for key in ("false_belief_rule_inference_accuracy",):
        rows.append(["learned_tom","all",key,result["learned_tom"][key]])
    with (out/"syntheticlab-v1.2-summary-latest.csv").open("w",newline="",encoding="utf-8") as f:
        w=csv.writer(f);w.writerow(["group","condition","metric","value"]);w.writerows(rows)
    hs=result["segmentation_hard"];hr=result["replay_hard"]["summary"]
    report=f"""# Dagmay SyntheticLab v1.2 — Scientific Hardening Report

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Why v1.2 exists
v1.0 exposed ceiling/triviality problems. v1.2 intentionally makes several benchmarks harder instead of celebrating perfect toy scores.

## Hard segmentation
Mean boundary F1:
{json.dumps(hs['mean_f1'],indent=2)}

Ceiling detected: {hs['ceiling_detected']}

## Hard replay/interference
{json.dumps(hr,indent=2)}

This benchmark uses shared weights and ongoing interference so replay policies can actually change the stability/plasticity balance.

## Learned information-access rule
False-belief rule inference accuracy: {result['learned_tom']['false_belief_rule_inference_accuracy']:.3f}

This learns that observation access predicts belief updating; it does not establish human-like Theory of Mind.

## Relationship-caused behavior
History-consistent partner choice rate: {result['relationship_behavior']['history_consistent_partner_choice_rate']:.3f}

This validates causal plumbing from evidence → relationship state → behavior, while the update rules themselves remain authored experimental mechanisms.

## Cross-environment transfer
Mean steps with transferred exploration strategy: {result['cross_environment_transfer']['transfer_steps_mean']:.2f}
Mean steps from scratch: {result['cross_environment_transfer']['scratch_steps_mean']:.2f}
Transfer advantage: {result['cross_environment_transfer']['transfer_advantage_steps']:.2f} steps

This transfers a learning strategy, not semantic world facts.

## Learning-progress curriculum
Fixed curriculum mean competence: {result['curriculum']['fixed_mean_competence']:.3f}
Learning-progress curriculum mean competence: {result['curriculum']['learning_progress_curriculum_mean_competence']:.3f}

Curriculum generation is applied to challenges, never to selecting for “more person-like” individuals.

## Exact persistence/restart
Restart equivalence rate: {result['persistence_restart']['exact_restart_equivalence_rate']:.3f}
Identity preservation rate: {result['persistence_restart']['identity_preservation_rate']:.3f}

## Remaining human gate
No further non-language engineering decision currently blocks the lab.

The next qualitative change is adding a real reflective language model to persistent research individuals. That requires the self-knowledge/disclosure policy decision documented in `docs/NEXT_CREATIVE_DECISION_GATE.md`.
"""
    (out/"syntheticlab-v1.2-report-latest.md").write_text(report,encoding="utf-8")
    print(report)

if __name__=="__main__":main()
