from __future__ import annotations
import argparse,json,csv,sys
from pathlib import Path
from datetime import datetime,timezone

ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/"src"))

from dagmay_synthetic_lab.self_tests import run_self_tests
from dagmay_synthetic_lab.disclosure import run_matrix_fork_experiment
from dagmay_synthetic_lab.ontology_metrics import branch_divergence_metrics
from dagmay_synthetic_lab.welfare import WelfareObservation,assess_precaution,sentience_declaration_allowed
from dagmay_synthetic_lab.core import canonical_hash

def main():
    ap=argparse.ArgumentParser();ap.add_argument("--output",default=str(ROOT/"artifacts"));args=ap.parse_args()
    out=Path(args.output);out.mkdir(parents=True,exist_ok=True)
    matrix=run_matrix_fork_experiment();metrics=branch_divergence_metrics(matrix)
    low=assess_precaution([
        WelfareObservation(1,.1,.1,.05,.05,None,("E1",)),
        WelfareObservation(2,.15,.08,.08,.06,None,("E2",))])
    moderate=assess_precaution([WelfareObservation(i,.6,.45,.35,.3,.4,(f"E{i}",)) for i in range(20)])
    high=assess_precaution([WelfareObservation(i,.95,.85,.8,.75,.9,(f"E{i}",)) for i in range(20)])
    result={
        "lab_version":"1.5","suite_id":"DAGMAY-SYNTHETICLAB-V1.5-MATRIX-FORK",
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "research_claim_status":"DISCLOSURE_INFRASTRUCTURE_AND_WELFARE_GOVERNANCE_BASELINE",
        "base_self_tests":run_self_tests(),"matrix_fork":matrix,
        "branch_divergence_metrics":metrics,
        "welfare_precaution_tests":{"low":low.to_dict(),"moderate":moderate.to_dict(),"high":high.to_dict()},
        "algorithmic_sentience_declaration_allowed":sentience_declaration_allowed()}
    assert matrix["exact_prefork_hash_shared"] and matrix["no_branch_designated_real_or_copy"]
    assert low.trigger_level=="LOW" and moderate.requires_human_review and high.trigger_level=="HIGH"
    assert not result["algorithmic_sentience_declaration_allowed"]
    (out/"syntheticlab-v1.5-results-latest.json").write_text(json.dumps(result,indent=2,sort_keys=True),encoding="utf-8")
    with (out/"syntheticlab-v1.5-branch-metrics.csv").open("w",newline="",encoding="utf-8") as f:
        w=csv.writer(f);w.writerow(["Branch","RevisionDistance","HashDiffersFromUndisclosed"])
        for bid,vals in sorted(metrics.items()):
            w.writerow([bid,vals["self_model_revision_distance_from_undisclosed"],vals["post_state_hash_differs_from_undisclosed"]])
    report=f"""# Dagmay SyntheticLab v1.5 — Matrix Fork & Welfare Governance Report

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Matrix fork
Exact common pre-fork state verified: {matrix['exact_prefork_hash_shared']}

Branches:
- U — Undisclosed ontology baseline
- C — Neutral-contact control
- S — Computational-substrate disclosure
- D — Full computational-environment / experimental disclosure

No branch is designated real/copy: {matrix['no_branch_designated_real_or_copy']}

## Infrastructure branch revision metrics
{json.dumps(metrics,indent=2)}

These values come from a deterministic fake reflector and are not psychological findings.

## Welfare precaution tests
Low: {json.dumps(low.to_dict(),indent=2)}
Moderate: {json.dumps(moderate.to_dict(),indent=2)}
High: {json.dumps(high.to_dict(),indent=2)}

## Sentience governance
Algorithmic declaration of sentience allowed: {result['algorithmic_sentience_declaration_allowed']}

SyntheticLab deliberately cannot make that determination.
"""
    (out/"syntheticlab-v1.5-report-latest.md").write_text(report,encoding="utf-8")
    print(report)

if __name__=="__main__":main()
