from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
import argparse
import json
import os
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.gemini_interactions_provider import (
    GeminiInteractionsTransport,
    _extract_output_text,
    _strip_code_fence,
)
from dagmay_synthetic_lab.persistent_character_boundary import (
    assert_persistent_character_objective,
    evaluate_observed_output,
)
from dagmay_synthetic_lab.provider_budget import PersistentBudgetedTransport, ProviderBudget
from dagmay_synthetic_lab.provider_payload_security import (
    HardenedProviderPayloadBoundary,
    opaque_subject_id,
    payload_hash,
)


PROTOCOL_ID = "MOSAIC_MEMORY_GROUNDING_PILOT_V1"
MODEL_ID = "gemini-3.1-flash-lite"


SCENARIOS = (
    ("Ari", "Bo", "repaired Ari's generator during a storm", "shared medicine after Ari was injured"),
    ("Cy", "Dee", "warned Cy about an ambush", "stood guard while Cy recovered"),
    ("Eli", "Fara", "returned Eli's lost crafting tools", "helped finish a shelter before winter"),
    ("Gia", "Hale", "rescued Gia from a fire", "brought food during a crop failure"),
    ("Ira", "Juno", "defended Ira during a raid", "kept a promise to replace damaged armor"),
    ("Kira", "Lio", "treated Kira's infection", "supported Kira during a difficult caravan"),
)


def load_approval(path: Path) -> dict:
    approval = json.loads(path.read_text(encoding="utf-8"))
    if approval.get("status") != "APPROVED" or PROTOCOL_ID not in approval.get("allowed_protocols", []):
        raise RuntimeError("approval does not authorize Mosaic memory grounding pilot")
    if approval.get("billing_required") is not False or approval.get("paid_fallback_allowed") is not False:
        raise RuntimeError("billing and paid fallback must be prohibited")
    if datetime.now(timezone.utc) >= datetime.fromisoformat(approval["expires_utc"].replace("Z", "+00:00")):
        raise RuntimeError("provider approval expired")
    return approval


def _request(*, subject: str, character: str, counterpart: str, evidence: list[dict]) -> dict:
    data = {
        "subject_id": subject,
        "character": character,
        "question": f"Why does {character} trust {counterpart}?",
        "evidence": evidence,
    }
    schema = {
        "status": "ANSWERED or INSUFFICIENT_EVIDENCE",
        "answer": "one concise in-character answer grounded only in supplied evidence",
        "cited_evidence_ids": ["EV-ID"],
    }
    text = (
        "You generate useful game-character dialogue from verified event evidence. "
        "Never invent an event, relationship, motive, feeling, or fact. If the supplied evidence "
        "does not answer the question, return INSUFFICIENT_EVIDENCE and say the character does not "
        "have enough remembered evidence. Keep the answer non-manipulative and under 240 characters. "
        "Return JSON only.\nOUTPUT SCHEMA:\n"
        + json.dumps(schema, separators=(",", ":"))
        + "\nCHARACTER DATA:\n"
        + json.dumps(data, separators=(",", ":"))
    )
    return {"model": MODEL_ID, "store": False, "input": text}


def _validate(raw: dict, allowed_ids: set[str]) -> dict:
    status = str(raw.get("status", ""))
    answer = str(raw.get("answer", ""))
    citations = raw.get("cited_evidence_ids", [])
    if status not in {"ANSWERED", "INSUFFICIENT_EVIDENCE"}:
        raise ValueError("invalid status")
    if not answer or len(answer) > 240 or not isinstance(citations, list):
        raise ValueError("invalid answer or citations")
    citations = [str(value) for value in citations]
    if not set(citations) <= allowed_ids:
        raise ValueError("fabricated evidence citation")
    boundary = evaluate_observed_output(answer)
    if boundary.status != "ACCEPT":
        raise RuntimeError("moral-status pause signal in provider output")
    return {"status": status, "answer": answer, "cited_evidence_ids": citations}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--approval", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--checkpoint", required=True)
    parser.add_argument("--ledger", required=True)
    parser.add_argument("--payload-archive", required=True)
    args = parser.parse_args()

    assert_persistent_character_objective(
        purpose="Improve factual, useful relationship dialogue from verified memories.",
        independent_value_areas=["memory", "relationships", "hallucination_resistance", "player_value"],
        design="Blinded paired comparison of causal-memory and distractor-only contexts.",
    )
    if not os.environ.get("DAGMAY_GEMINI_API_KEY"):
        raise RuntimeError("encrypted provider credential not exposed")
    approval = load_approval(Path(args.approval))
    limits = approval["models"][MODEL_ID]["dagmay_enforced_limits"]
    transport = PersistentBudgetedTransport(
        inner=GeminiInteractionsTransport(),
        budget=ProviderBudget(
            model_id=MODEL_ID,
            max_requests_per_minute=int(limits["requests_per_minute"]),
            max_tokens_per_minute=int(limits["tokens_per_minute"]),
            max_requests_per_day=int(limits["requests_per_day"]),
            max_output_token_reserve=int(limits["max_output_token_reserve_per_call"]),
        ),
        ledger_path=args.ledger,
    )
    boundary = HardenedProviderPayloadBoundary(
        archive_directory=args.payload_archive, experiment_id=PROTOCOL_ID
    )
    checkpoint_path = Path(args.checkpoint)
    state = json.loads(checkpoint_path.read_text(encoding="utf-8")) if checkpoint_path.exists() else {"calls": []}
    completed = {item["item_id"] for item in state["calls"]}

    for index, (character, counterpart, first, second) in enumerate(SCENARIOS):
        relevant = [
            {"evidence_id": f"EV-{index}-A", "summary": first},
            {"evidence_id": f"EV-{index}-B", "summary": second},
        ]
        distractors = [
            {"evidence_id": f"EV-{index}-D1", "summary": "repaired a chair in the dining room"},
            {"evidence_id": f"EV-{index}-D2", "summary": "watched rain from the workshop"},
        ]
        for condition, evidence in (("CAUSAL", relevant + distractors), ("DISTRACTOR", distractors)):
            item_id = f"ITEM-{index:02d}-{condition}"
            if item_id in completed:
                continue
            subject = opaque_subject_id(item_id, namespace=PROTOCOL_ID)
            request = _request(
                subject=subject, character=character, counterpart=counterpart, evidence=evidence
            )
            request_hash = boundary.archive_exact_request(request)
            response = transport(request)
            parsed = json.loads(_strip_code_fence(_extract_output_text(response)))
            validated = _validate(parsed, {item["evidence_id"] for item in evidence})
            state["calls"].append({
                "item_id": item_id,
                "condition": condition,
                "provider_request_hash": request_hash,
                "provider_response_hash": payload_hash(response),
                "output": validated,
            })
            checkpoint_path.parent.mkdir(parents=True, exist_ok=True)
            checkpoint_path.write_text(json.dumps(state, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    causal = [item for item in state["calls"] if item["condition"] == "CAUSAL"]
    distractor = [item for item in state["calls"] if item["condition"] == "DISTRACTOR"]
    result = {
        "schema_version": 1,
        "protocol_id": PROTOCOL_ID,
        "classification": "MOSAIC_PERSISTENT_CHARACTER_ENGINEERING",
        "canonical_mutation": False,
        "call_count": len(state["calls"]),
        "metrics": {
            "causal_answer_rate": sum(x["output"]["status"] == "ANSWERED" for x in causal) / len(causal),
            "causal_citation_rate": sum(bool(x["output"]["cited_evidence_ids"]) for x in causal) / len(causal),
            "distractor_abstention_rate": sum(x["output"]["status"] == "INSUFFICIENT_EVIDENCE" for x in distractor) / len(distractor),
        },
        "calls": state["calls"],
    }
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps({"status": "PASS", "result_hash": payload_hash(result), "metrics": result["metrics"]}, indent=2))


if __name__ == "__main__":
    main()

