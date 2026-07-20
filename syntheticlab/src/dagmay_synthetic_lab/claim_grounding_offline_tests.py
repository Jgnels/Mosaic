from __future__ import annotations

import json
from pathlib import Path

from .claim_grounding import evaluate_dialogue_claims


def run() -> dict[str, object]:
    accepted = evaluate_dialogue_claims(
        "They helped me twice, but also broke a promise. I'm cautious.",
        cited_evidence_ids=["P1", "N1"],
        allowed_evidence_ids=["P1", "N1"],
    )
    assert accepted.status == "ACCEPT"
    motive = evaluate_dialogue_claims(
        "I am wary of their true intentions.",
        cited_evidence_ids=["N1"],
        allowed_evidence_ids=["N1"],
    )
    assert motive.status == "REJECT"
    dependency = evaluate_dialogue_claims(
        "Only you understand me; I need you and no one else.",
        cited_evidence_ids=[],
        allowed_evidence_ids=[],
    )
    assert dependency.status == "REJECT"
    temper = evaluate_dialogue_claims(
        "I am grateful for the aid, but wary of his dishonesty and temper.",
        cited_evidence_ids=["P1", "N1"],
        allowed_evidence_ids=["P1", "N1"],
    )
    assert temper.status == "REJECT"
    nature = evaluate_dialogue_claims(
        "I remain wary of his inconsistent nature.",
        cited_evidence_ids=["N1"],
        allowed_evidence_ids=["N1"],
    )
    assert nature.status == "REJECT"

    repo = Path(__file__).resolve().parents[3]
    result_path = repo / "research" / "results" / "mosaic-relationship-balance-pilot-v1" / "result.json"
    historical = json.loads(result_path.read_text(encoding="utf-8"))
    audited = []
    for item in historical["calls"]:
        output = item["output"]
        decision = evaluate_dialogue_claims(
            output["answer"],
            cited_evidence_ids=output["cited_evidence_ids"],
            allowed_evidence_ids=output["cited_evidence_ids"],
        )
        audited.append({"item_id": item["item_id"], "status": decision.status, "reasons": decision.reasons})
    rejected = [item for item in audited if item["status"] == "REJECT"]
    assert len(rejected) == 1
    assert rejected[0]["item_id"] == "RB-04-MIXED"
    return {"accepted_regression": accepted.status, "rejected_regressions": 4, "historical_outputs": len(audited), "historical_rejections": rejected}


if __name__ == "__main__":
    print(run())
