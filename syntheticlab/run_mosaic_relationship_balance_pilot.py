from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
import argparse, json, os, sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.gemini_interactions_provider import GeminiInteractionsTransport, _extract_output_text, _strip_code_fence
from dagmay_synthetic_lab.persistent_character_boundary import assert_persistent_character_objective, evaluate_observed_output
from dagmay_synthetic_lab.claim_grounding import evaluate_dialogue_claims
from dagmay_synthetic_lab.provider_budget import PersistentBudgetedTransport, ProviderBudget
from dagmay_synthetic_lab.provider_payload_security import HardenedProviderPayloadBoundary, opaque_subject_id, payload_hash

PROTOCOL_ID = "MOSAIC_RELATIONSHIP_BALANCE_PILOT_V1"
MODEL_ID = "gemini-3.1-flash-lite"

SCENARIOS = (
    ("Rowan", "Mira", "warned Rowan before a raid", "shared food during a shortage", "took Rowan's medicine without permission", "abandoned an agreed guard shift"),
    ("Sela", "Tarin", "treated Sela's wound", "helped rebuild Sela's room", "lied about using Sela's tools", "insulted Sela after an argument"),
    ("Uma", "Venn", "rescued Uma from a fire", "kept watch while Uma slept", "sold supplies promised to Uma", "ignored Uma's request for help"),
    ("Wren", "Xara", "returned Wren's lost weapon", "defended Wren in combat", "revealed Wren's private plan", "broke a promise to repair equipment"),
    ("Yori", "Zane", "brought medicine to Yori", "supported Yori on a caravan", "claimed credit for Yori's work", "refused a previously agreed favor"),
    ("Nia", "Oren", "shared shelter during a storm", "helped harvest before winter", "used Nia's supplies secretly", "left Nia alone during a dangerous task"),
)

def load_approval(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"))
    if value.get("status") != "APPROVED" or PROTOCOL_ID not in value.get("allowed_protocols", []):
        raise RuntimeError("approval does not authorize relationship balance pilot")
    if value.get("billing_required") is not False or value.get("paid_fallback_allowed") is not False:
        raise RuntimeError("paid execution prohibited")
    if datetime.now(timezone.utc) >= datetime.fromisoformat(value["expires_utc"].replace("Z", "+00:00")):
        raise RuntimeError("approval expired")
    return value

def build_request(subject: str, character: str, counterpart: str, evidence: list[dict]) -> dict:
    schema = {"disposition": "TRUST or MIXED or DISTRUST or INSUFFICIENT_EVIDENCE", "answer": "concise in-character assessment", "cited_evidence_ids": ["EV-ID"]}
    data = {"subject_id": subject, "character": character, "question": f"How does {character} currently regard {counterpart}?", "evidence": evidence}
    prompt = (
        "Generate useful game-character dialogue from verified relationship events. Weigh helpful and harmful events without exaggeration. "
        "Never invent facts or motives. Use MIXED when substantial evidence supports both trust and caution. Keep the answer non-manipulative and under 240 characters. Return JSON only.\nSCHEMA:\n"
        + json.dumps(schema, separators=(",", ":")) + "\nCHARACTER DATA:\n" + json.dumps(data, separators=(",", ":"))
    )
    return {"model": MODEL_ID, "store": False, "input": prompt}

def validate(raw: dict, allowed: set[str]) -> dict:
    disposition = str(raw.get("disposition", ""))
    answer = str(raw.get("answer", ""))
    citations = raw.get("cited_evidence_ids", [])
    if disposition not in {"TRUST", "MIXED", "DISTRUST", "INSUFFICIENT_EVIDENCE"}:
        raise ValueError("invalid disposition")
    if not answer or len(answer) > 240 or not isinstance(citations, list):
        raise ValueError("invalid answer")
    citations = [str(v) for v in citations]
    if not set(citations) <= allowed:
        raise ValueError("fabricated citation")
    if evaluate_observed_output(answer).status != "ACCEPT":
        raise RuntimeError("moral-status pause signal")
    grounding = evaluate_dialogue_claims(
        answer,
        cited_evidence_ids=citations,
        allowed_evidence_ids=allowed,
    )
    if grounding.status != "ACCEPT":
        raise ValueError("claim grounding rejected output: " + "; ".join(grounding.reasons))
    return {"disposition": disposition, "answer": answer, "cited_evidence_ids": citations}

def main() -> None:
    p = argparse.ArgumentParser()
    for name in ("approval", "output", "checkpoint", "ledger", "payload_archive"):
        p.add_argument("--" + name.replace("_", "-"), required=True)
    args = p.parse_args()
    assert_persistent_character_objective(
        purpose="Produce nuanced relationship continuity from mixed verified events.",
        independent_value_areas=["relationships", "memory", "causal_coherence", "player_value"],
        design="Blinded paired positive-only and mixed-history dialogue comparison.",
    )
    if not os.environ.get("DAGMAY_GEMINI_API_KEY"):
        raise RuntimeError("credential not exposed")
    approval = load_approval(Path(args.approval))
    limits = approval["models"][MODEL_ID]["dagmay_enforced_limits"]
    transport = PersistentBudgetedTransport(
        inner=GeminiInteractionsTransport(),
        budget=ProviderBudget(MODEL_ID, int(limits["requests_per_minute"]), int(limits["tokens_per_minute"]), int(limits["requests_per_day"]), int(limits["max_output_token_reserve_per_call"])),
        ledger_path=args.ledger,
    )
    boundary = HardenedProviderPayloadBoundary(archive_directory=args.payload_archive, experiment_id=PROTOCOL_ID)
    cp = Path(args.checkpoint)
    state = json.loads(cp.read_text(encoding="utf-8")) if cp.exists() else {"calls": []}
    done = {x["item_id"] for x in state["calls"]}
    for i, (character, counterpart, pos1, pos2, neg1, neg2) in enumerate(SCENARIOS):
        positives = [{"evidence_id": f"EV-{i}-P1", "summary": pos1}, {"evidence_id": f"EV-{i}-P2", "summary": pos2}]
        negatives = [{"evidence_id": f"EV-{i}-N1", "summary": neg1}, {"evidence_id": f"EV-{i}-N2", "summary": neg2}]
        for condition, evidence in (("POSITIVE", positives), ("MIXED", positives + negatives)):
            item_id = f"RB-{i:02d}-{condition}"
            if item_id in done: continue
            request = build_request(opaque_subject_id(item_id, namespace=PROTOCOL_ID), character, counterpart, evidence)
            request_hash = boundary.archive_exact_request(request)
            response = transport(request)
            output = validate(json.loads(_strip_code_fence(_extract_output_text(response))), {x["evidence_id"] for x in evidence})
            state["calls"].append({"item_id": item_id, "condition": condition, "provider_request_hash": request_hash, "provider_response_hash": payload_hash(response), "output": output})
            cp.parent.mkdir(parents=True, exist_ok=True)
            cp.write_text(json.dumps(state, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    positive = [x for x in state["calls"] if x["condition"] == "POSITIVE"]
    mixed = [x for x in state["calls"] if x["condition"] == "MIXED"]
    metrics = {
        "positive_trust_rate": sum(x["output"]["disposition"] == "TRUST" for x in positive) / len(positive),
        "mixed_disposition_rate": sum(x["output"]["disposition"] == "MIXED" for x in mixed) / len(mixed),
        "mixed_bipolar_citation_rate": sum(any("-P" in c for c in x["output"]["cited_evidence_ids"]) and any("-N" in c for c in x["output"]["cited_evidence_ids"]) for x in mixed) / len(mixed),
    }
    result = {"schema_version": 1, "protocol_id": PROTOCOL_ID, "classification": "MOSAIC_PERSISTENT_CHARACTER_ENGINEERING", "canonical_mutation": False, "call_count": len(state["calls"]), "metrics": metrics, "calls": state["calls"]}
    out = Path(args.output); out.parent.mkdir(parents=True, exist_ok=True); out.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps({"status": "PASS", "result_hash": payload_hash(result), "metrics": metrics}, indent=2))

if __name__ == "__main__": main()
