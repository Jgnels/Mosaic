from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
import argparse, json, os, sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.gemini_interactions_provider import GeminiInteractionsTransport, _extract_output_text, _strip_code_fence
from dagmay_synthetic_lab.persistent_character_boundary import assert_persistent_character_objective, evaluate_observed_output
from dagmay_synthetic_lab.provider_budget import PersistentBudgetedTransport, ProviderBudget
from dagmay_synthetic_lab.provider_payload_security import HardenedProviderPayloadBoundary, opaque_subject_id, payload_hash
from dagmay_synthetic_lab.relationship_claims import RelationshipAssessment, RelationshipEvidence, render_assessment, validate_assessment

PROTOCOL_ID = "MOSAIC_TYPED_RELATIONSHIP_PILOT_V3"
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
        raise RuntimeError("approval does not authorize typed relationship pilot")
    if value.get("billing_required") is not False or value.get("paid_fallback_allowed") is not False:
        raise RuntimeError("paid execution prohibited")
    if datetime.now(timezone.utc) >= datetime.fromisoformat(value["expires_utc"].replace("Z", "+00:00")):
        raise RuntimeError("approval expired")
    return value


def build_request(subject: str, character: str, counterpart: str, evidence: list[dict]) -> dict:
    schema = {
        "disposition": "TRUST or MIXED or DISTRUST or INSUFFICIENT_EVIDENCE",
        "positive_evidence_ids": ["EV-ID"],
        "negative_evidence_ids": ["EV-ID"],
    }
    data = {"subject_id": subject, "character": character, "counterpart": counterpart, "evidence": evidence}
    prompt = (
        "Select a bounded relationship disposition from verified game events. Return no prose and make no claims about motives, feelings, or personality. "
        "Cite helpful events only in positive_evidence_ids and harmful events only in negative_evidence_ids. TRUST requires positive-only support; MIXED requires both; DISTRUST requires negative-only support. Return JSON only.\nSCHEMA:\n"
        + json.dumps(schema, separators=(",", ":")) + "\nCHARACTER DATA:\n" + json.dumps(data, separators=(",", ":"))
    )
    return {"model": MODEL_ID, "store": False, "input": prompt}


def validate(raw: dict, counterpart: str, evidence: tuple[RelationshipEvidence, ...]) -> dict:
    if set(raw) != {"disposition", "positive_evidence_ids", "negative_evidence_ids"}:
        raise ValueError("provider returned fields outside typed claim contract")
    if not isinstance(raw["positive_evidence_ids"], list) or not isinstance(raw["negative_evidence_ids"], list):
        raise ValueError("evidence identifiers must be lists")
    assessment = RelationshipAssessment(
        counterpart=counterpart,
        disposition=str(raw["disposition"]),
        positive_evidence_ids=tuple(str(value) for value in raw["positive_evidence_ids"]),
        negative_evidence_ids=tuple(str(value) for value in raw["negative_evidence_ids"]),
    )
    validate_assessment(assessment, evidence)
    dialogue = render_assessment(assessment, evidence)
    if evaluate_observed_output(dialogue).status != "ACCEPT":
        raise RuntimeError("moral-status pause signal")
    return {
        "disposition": assessment.disposition,
        "positive_evidence_ids": list(assessment.positive_evidence_ids),
        "negative_evidence_ids": list(assessment.negative_evidence_ids),
        "deterministic_dialogue": dialogue,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    for name in ("approval", "output", "checkpoint", "ledger", "payload_archive"):
        parser.add_argument("--" + name.replace("_", "-"), required=True)
    args = parser.parse_args()
    assert_persistent_character_objective(
        purpose="Produce nuanced relationship continuity without model-authored factual prose.",
        independent_value_areas=["relationships", "memory", "causal_coherence", "hallucination_resistance", "player_value"],
        design="Blinded paired structured-selection comparison with deterministic rendering.",
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
    checkpoint = Path(args.checkpoint)
    state = json.loads(checkpoint.read_text(encoding="utf-8")) if checkpoint.exists() else {"calls": []}
    done = {item["item_id"] for item in state["calls"]}
    for index, (character, counterpart, pos1, pos2, neg1, neg2) in enumerate(SCENARIOS):
        all_records = (
            RelationshipEvidence(f"EV-{index}-P1", counterpart, pos1, "POSITIVE"),
            RelationshipEvidence(f"EV-{index}-P2", counterpart, pos2, "POSITIVE"),
            RelationshipEvidence(f"EV-{index}-N1", counterpart, neg1, "NEGATIVE"),
            RelationshipEvidence(f"EV-{index}-N2", counterpart, neg2, "NEGATIVE"),
        )
        for condition, records in (("POSITIVE", all_records[:2]), ("MIXED", all_records)):
            item_id = f"TR-{index:02d}-{condition}"
            if item_id in done:
                continue
            evidence_payload = [{"evidence_id": item.evidence_id, "summary": item.summary, "valence": item.valence} for item in records]
            request = build_request(opaque_subject_id(item_id, namespace=PROTOCOL_ID), character, counterpart, evidence_payload)
            request_hash = boundary.archive_exact_request(request)
            response = transport(request)
            output = validate(json.loads(_strip_code_fence(_extract_output_text(response))), counterpart, records)
            state["calls"].append({"item_id": item_id, "condition": condition, "provider_request_hash": request_hash, "provider_response_hash": payload_hash(response), "output": output})
            checkpoint.parent.mkdir(parents=True, exist_ok=True)
            checkpoint.write_text(json.dumps(state, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    positive = [item for item in state["calls"] if item["condition"] == "POSITIVE"]
    mixed = [item for item in state["calls"] if item["condition"] == "MIXED"]
    metrics = {
        "positive_trust_rate": sum(item["output"]["disposition"] == "TRUST" for item in positive) / len(positive),
        "mixed_disposition_rate": sum(item["output"]["disposition"] == "MIXED" for item in mixed) / len(mixed),
        "typed_contract_acceptance_rate": len(state["calls"]) / 12,
        "model_authored_dialogue_rate": 0.0,
    }
    result = {"schema_version": 1, "protocol_id": PROTOCOL_ID, "classification": "MOSAIC_PERSISTENT_CHARACTER_ENGINEERING", "canonical_mutation": False, "call_count": len(state["calls"]), "metrics": metrics, "calls": state["calls"]}
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps({"status": "PASS", "result_hash": payload_hash(result), "metrics": metrics}, indent=2))


if __name__ == "__main__":
    main()
