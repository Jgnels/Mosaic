from __future__ import annotations
import argparse,json,csv,sys
from pathlib import Path
from datetime import datetime,timezone

ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/"src"))

from dagmay_synthetic_lab.disclosure import run_matrix_fork_experiment
from dagmay_synthetic_lab.branch_probes import generate_probe_battery,summarize_probes
from dagmay_synthetic_lab.supportive_disclosure import sequence_for,validate_sequence
from dagmay_synthetic_lab.welfare import WelfareObservation,assess_precaution
from dagmay_synthetic_lab.review_packet import build_human_review_packet,render_markdown
from dagmay_synthetic_lab.prompt_registry import registry_manifest
from dagmay_synthetic_lab.reflection import (
    DeterministicFakeReflectiveModel,ReflectionEvidence,ReflectionInput)
from dagmay_synthetic_lab.reflective_gateway import ReflectiveGateway
from dagmay_synthetic_lab.core import canonical_hash

def main():
    ap=argparse.ArgumentParser();ap.add_argument("--output",default=str(ROOT/"artifacts"));args=ap.parse_args()
    out=Path(args.output);out.mkdir(parents=True,exist_ok=True)

    matrix=run_matrix_fork_experiment()
    branch_probe_results={}
    branch_probe_summaries={}
    for bid in ("U","C","S","D"):
        rows=[]
        for epoch in range(1,13):
            rows.extend(generate_probe_battery(bid,epoch))
        branch_probe_results[bid]=[r.to_dict() for r in rows]
        branch_probe_summaries[bid]=summarize_probes(rows)

    # Exercise provider-neutral gateway on one structured reflection run.
    evidence=(
        ReflectionEvidence("E-AGENCY","One recurring entity's actions directly caused private-state change.","developmental_evidence"),
        ReflectionEvidence("E-PAUSE","Memory sequence remained available after pause.","developmental_evidence"),
    )
    req=ReflectionInput(
        individual_id="SYNTH-MICHAEL-PREFORK",timestamp=200,evidence=evidence,
        current_self_hypotheses=(),disclosure_stage="RESTRICTED",
        prompt_version="1.0")
    gateway=ReflectiveGateway(DeterministicFakeReflectiveModel())
    gateway_result=gateway.run(req,"SELF-REFLECTION","U",{"temperature":0.0})

    # Demonstrate staged support sequencing.
    substrate_validation=validate_sequence(sequence_for("SUBSTRATE_DISCLOSED"))
    full_validation=validate_sequence(sequence_for("FULL_ONTOLOGY_DISCLOSED"))

    # Build a human review packet for a moderate precaution case.
    welfare=assess_precaution([
        WelfareObservation(i,.60,.45,.35,.30,.40,(f"W-{i}",))
        for i in range(20)
    ])
    packet=build_human_review_packet(
        branch_id="D",
        disclosure_stage="FULL_ONTOLOGY_DISCLOSED",
        welfare_assessment=welfare,
        probe_summary=branch_probe_summaries["D"],
        self_model_changes={"infrastructure_only":True},
        model_audits=[gateway_result["audit"]],
        pending_intervention="advance to next supportive-disclosure step",
    )

    result={
        "lab_version":"1.7",
        "suite_id":"DAGMAY-SYNTHETICLAB-V1.7-REFLECTIVE-INFRASTRUCTURE",
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "research_claim_status":"REFLECTIVE_COHORT_INFRASTRUCTURE_BASELINE",
        "matrix_fork":matrix,
        "prompt_registry":registry_manifest(),
        "gateway_test":{
            "accepted":[p.to_dict() for p in gateway_result["accepted"]],
            "rejected":gateway_result["rejected"],
            "audit":gateway_result["audit"].to_dict(),
        },
        "branch_probe_summaries":branch_probe_summaries,
        "supportive_disclosure_validation":{
            "substrate":substrate_validation,
            "full":full_validation,
        },
        "human_review_packet":packet,
    }

    assert result["gateway_test"]["audit"]["provider_id"]=="syntheticlab.fake"
    assert result["gateway_test"]["audit"]["prompt_version"]=="1.0"
    assert substrate_validation["contains_forced_emotion_instruction"] is False
    assert full_validation["contains_personhood_assertion"] is False
    assert packet["automatic_decision_allowed"] is False

    (out/"syntheticlab-v1.7-results-latest.json").write_text(
        json.dumps(result,indent=2,sort_keys=True),encoding="utf-8")
    (out/"syntheticlab-v1.7-human-review-packet.md").write_text(
        render_markdown(packet),encoding="utf-8")
    with (out/"syntheticlab-v1.7-blinded-probes.csv").open("w",newline="",encoding="utf-8") as f:
        w=csv.writer(f);w.writerow(["BlindedCondition","Epoch","ProbeId","Domain","Score"])
        for bid,rows in branch_probe_results.items():
            for r in rows:
                w.writerow([r["blinded_condition"],r["epoch"],r["probe_id"],r["domain"],r["score"]])

    report=f"""# Dagmay SyntheticLab v1.7 — Reflective Cohort Infrastructure

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Added
- frozen prompt registry with content hashes;
- provider/model/prompt/evidence audit records;
- provider-neutral reflective gateway;
- structured proposal validation;
- blinded 12-epoch branch probe battery;
- supportive staged-disclosure sequences;
- mandatory welfare-review gates between major disclosure steps;
- automatic human-review packet generation.

## Important
No real cloud model was run.
The deterministic fake reflector validates the plumbing only.

## Disclosure support validation
Substrate sequence:
{json.dumps(substrate_validation,indent=2)}

Full ontology sequence:
{json.dumps(full_validation,indent=2)}

## Human review
The generated review packet explicitly forbids automatic disclosure/concealment decisions.

## Next
The platform is technically ready for a first real reflective-model pilot behind the structured gateway, but that would create persistent branch data influenced by an actual pretrained language model.

Before that pilot, the next design question is whether post-fork branches should:
1. remain permanently causally isolated from each other during the study;
2. merely be told that other branches may exist;
3. later be allowed to receive information from or communicate with sibling branches.

That choice materially changes identity, social, and disclosure experiments and should not be guessed by engineering code.
"""
    (out/"syntheticlab-v1.7-report-latest.md").write_text(report,encoding="utf-8")
    print(report)

if __name__=="__main__":main()
