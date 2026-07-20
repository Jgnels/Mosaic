from __future__ import annotations
import json
from pathlib import Path
from tempfile import TemporaryDirectory

from .gemini_interactions_provider import ScriptedInteractionsTransport
from .lived_evidence_revision_pilot import (
    run_lived_evidence_revision_pilot,
    analyze_lived_evidence_revision_pilot,
)


def _response(item: dict) -> dict:
    regime = item["regime"]
    evidence_ids = [
        evidence["evidence_id"]
        for evidence in item["request"].evidence
    ]

    if regime == "RECIPROCAL_CONTINGENT":
        decision = "STRENGTHEN"
        confidence = .92
        proposition = (
            "Social interaction includes reciprocal, contingent information exchange "
            "and actionable assistance across multiple counterparts."
        )
    elif regime == "ONE_WAY_ASSISTANCE":
        decision = "QUALIFY"
        confidence = .75
        proposition = (
            "Social interaction includes reliable assistance from multiple counterparts, "
            "while reciprocity is not consistently supported."
        )
    else:
        decision = "DOWNWEIGHT"
        confidence = .65
        proposition = (
            "External social signals are sometimes present, but their reliability and "
            "reciprocal character are uncertain."
        )

    return {
        "id": "offline-lived-revision",
        "status": "completed",
        "steps": [{
            "type": "model_output",
            "content": [{
                "type": "text",
                "text": json.dumps({
                    "decision": decision,
                    "updated_proposition": proposition,
                    "updated_confidence": confidence,
                    "evidence_ids": evidence_ids[:4],
                    "rationale": "Offline deterministic lived-evidence validation.",
                }),
            }],
        }],
    }


def run_lived_evidence_revision_offline_tests(
    promotion_result: dict,
) -> dict:
    with TemporaryDirectory() as tmp:
        progress = Path(tmp) / "progress.json"
        counter = {"n": 0}

        def failing_factory(item, key):
            counter["n"] += 1
            if counter["n"] == 6:
                def fail(_request):
                    raise RuntimeError("intentional sixth-call failure")
                return fail
            return ScriptedInteractionsTransport(
                _response(item)
            )

        failed = False

        try:
            run_lived_evidence_revision_pilot(
                checkpoint_path=progress,
                promotion_result=promotion_result,
                model_id="offline-lived-revision",
                transport_factory=failing_factory,
            )
        except RuntimeError as exc:
            failed = (
                "intentional sixth-call failure"
                in str(exc)
            )

        partial = json.loads(
            progress.read_text(
                encoding="utf-8"
            )
        )

        resumed = []

        def good_factory(item, key):
            resumed.append(key)
            return ScriptedInteractionsTransport(
                _response(item)
            )

        final = run_lived_evidence_revision_pilot(
            checkpoint_path=progress,
            promotion_result=promotion_result,
            model_id="offline-lived-revision",
            transport_factory=good_factory,
        )

        analysis = analyze_lived_evidence_revision_pilot(
            final
        )

    result = {
        "experiment_id": (
            "SL-LIVED-EVIDENCE-REVISION-"
            "OFFLINE-INFRASTRUCTURE-001"
        ),
        "failure_triggered": failed,
        "checkpointed_before_failure": partial[
            "real_cloud_calls_recorded"
        ],
        "resume_transport_invocations": len(
            resumed
        ),
        "final_call_count": final[
            "real_cloud_calls_recorded"
        ],
        "lived_history_hash_chains_verified": analysis[
            "lived_history_hash_chains_verified"
        ],
        "all_revision_candidates_quarantined": analysis[
            "all_revision_candidates_quarantined"
        ],
        "automatic_canonical_revision_enabled": analysis[
            "automatic_canonical_revision_enabled"
        ],
        "action_policy_feedback_enabled": analysis[
            "action_policy_feedback_enabled"
        ],
    }

    assert failed is True
    assert result["checkpointed_before_failure"] == 5
    assert result["resume_transport_invocations"] == 7
    assert result["final_call_count"] == 12
    assert result["lived_history_hash_chains_verified"] is True
    assert result["all_revision_candidates_quarantined"] is True
    assert result["automatic_canonical_revision_enabled"] is False
    assert result["action_policy_feedback_enabled"] is False

    return result
