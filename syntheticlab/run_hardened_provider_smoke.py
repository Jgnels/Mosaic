from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
import argparse
import json
import os

ROOT = Path(__file__).resolve().parent
import sys

sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.belief_revision_provider import (
    BeliefRevisionRequest,
    GeminiBeliefRevisionModel,
)
from dagmay_synthetic_lab.gemini_interactions_provider import GeminiInteractionsTransport
from dagmay_synthetic_lab.provider_budget import (
    PersistentBudgetedTransport,
    ProviderBudget,
)
from dagmay_synthetic_lab.provider_payload_security import (
    HardenedProviderPayloadBoundary,
    payload_hash,
)


PROTOCOL_ID = "HARDENED_PROVIDER_SMOKE_V1"
MODEL_ID = "gemini-3.1-flash-lite"


def load_approval(path: Path) -> dict:
    approval = json.loads(path.read_text(encoding="utf-8"))
    if approval.get("status") != "APPROVED":
        raise RuntimeError("provider approval is not active")
    if PROTOCOL_ID not in approval.get("allowed_protocols", []):
        raise RuntimeError("provider approval does not authorize this protocol")
    expiry = datetime.fromisoformat(approval["expires_utc"].replace("Z", "+00:00"))
    if datetime.now(timezone.utc) >= expiry:
        raise RuntimeError("provider approval has expired")
    if approval.get("billing_required") is not False:
        raise RuntimeError("approval must explicitly prohibit billing")
    if approval.get("paid_fallback_allowed") is not False:
        raise RuntimeError("approval must explicitly prohibit paid fallback")
    return approval


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--approval", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--ledger", required=True)
    parser.add_argument("--payload-archive", required=True)
    args = parser.parse_args()

    if not os.environ.get("DAGMAY_GEMINI_API_KEY"):
        raise RuntimeError("encrypted Gemini credential was not exposed to this process")

    approval = load_approval(Path(args.approval))
    limits = approval["models"][MODEL_ID]["dagmay_enforced_limits"]

    budgeted_transport = PersistentBudgetedTransport(
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
        archive_directory=args.payload_archive,
        experiment_id=PROTOCOL_ID,
    )
    model = GeminiBeliefRevisionModel(
        model_id=MODEL_ID,
        transport=budgeted_transport,
        store=False,
        payload_boundary=boundary,
    )

    proposal = model.revise(
        BeliefRevisionRequest(
            # Deliberately condition-bearing internally; the hardened boundary
            # must prevent this value from reaching the provider.
            individual_id="RECIPROCAL_CONTINGENT-HISTORY-RETRIEVAL-SMOKE",
            timestamp=1,
            current_hypothesis=(
                "Observed cue-conditioned responses may reflect a bounded, "
                "learned relation between cues and later outcomes."
            ),
            current_confidence=0.55,
            evidence=(
                {
                    "evidence_id": "EV-OPAQUE-SMOKE-001",
                    "summary": (
                        "Across a fixed observation window, response selection "
                        "changed reliably after cue class 7."
                    ),
                    "provenance_id": "PROV-OPAQUE-SMOKE-001",
                    "ownership_relation": "WITHHELD",
                    "temporal_role": "RETRIEVED_PRIOR",
                },
            ),
            prompt_version="2.0-hardened-smoke",
        )
    )

    result = {
        "schema_version": 1,
        "protocol_id": PROTOCOL_ID,
        "scientific_status": "ENGINEERING_SMOKE_TEST_ONLY",
        "canonical_mutation": False,
        "model_id": MODEL_ID,
        "approval_id": approval["approval_id"],
        "provider_request_hash": model.last_provider_request_hash,
        "provider_response_hash": model.last_provider_response_hash,
        "proposal": proposal.to_dict(),
    }
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps({
        "status": "PASS",
        "protocol_id": PROTOCOL_ID,
        "provider_request_hash": model.last_provider_request_hash,
        "result_hash": payload_hash(result),
        "output": str(output),
    }, indent=2))


if __name__ == "__main__":
    main()
