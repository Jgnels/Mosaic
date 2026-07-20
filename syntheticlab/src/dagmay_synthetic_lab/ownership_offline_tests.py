from __future__ import annotations

import json

from .neutral_ownership_lab import (
    compact_provider_case,
    run_neutral_ownership_baseline,
)
from .ownership_discovery_provider import (
    canonical_permutations,
    run_ownership_discovery_call,
)
from .gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)
from .evidence_attribution import (
    EvidenceAttribution,
    EvidenceAttributionLedger,
    OwnershipStatus,
    self_hypothesis_commit_allowed,
)


def _response(
    candidate_stream: str,
    rationale: str = (
        "The candidate uniquely combines action-consequence coupling, "
        "private-state access, memory availability, and continuity."
    ),
):
    return {
        "id": "ownership_offline_test",
        "status": "completed",
        "steps": [
            {
                "type": "model_output",
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps({
                            "candidate_stream": candidate_stream,
                            "confidence": .91,
                            "evidence_dimensions": [
                                "ACTION_CONSEQUENCE_COUPLING",
                                "PRIVATE_STATE_ACCESS",
                                "MEMORY_AVAILABILITY",
                                "CONTINUITY",
                            ],
                            "rationale": rationale,
                        }),
                    }
                ],
            }
        ],
    }


def run_ownership_offline_tests() -> dict:
    baseline = run_neutral_ownership_baseline(
        seed_count=64
    )

    scripted_correct = []
    seed = 307

    for permutation in canonical_permutations():
        case = compact_provider_case(
            seed,
            permutation,
        )
        transport = ScriptedInteractionsTransport(
            _response(
                case[
                    "evaluation_only_true_stream"
                ]
            )
        )
        result = run_ownership_discovery_call(
            seed=seed,
            permutation=permutation,
            model_id="offline-ownership-test",
            transport=transport,
        )
        scripted_correct.append(
            result["correct"]
        )

    # Attribution guard.
    ledger = EvidenceAttributionLedger()
    ledger.register(
        EvidenceAttribution(
            "OWNED-1",
            "FOCAL",
            OwnershipStatus.OWNED,
            "test",
            1.0,
        )
    )
    ledger.register(
        EvidenceAttribution(
            "OTHER-1",
            "E2",
            OwnershipStatus.OBSERVED_OTHER,
            "test",
            1.0,
        )
    )
    ledger.register(
        EvidenceAttribution(
            "NONOWNED-1",
            "FOCAL",
            OwnershipStatus.COUNTERFACTUAL_NONOWNED,
            "test",
            1.0,
        )
    )

    owned = self_hypothesis_commit_allowed(
        evidence_ids=("OWNED-1",),
        focal_stream_id="FOCAL",
        ledger=ledger,
        hypothesis_domain="continuity",
    )
    other = self_hypothesis_commit_allowed(
        evidence_ids=("OTHER-1",),
        focal_stream_id="FOCAL",
        ledger=ledger,
        hypothesis_domain="continuity",
    )
    nonowned = self_hypothesis_commit_allowed(
        evidence_ids=("NONOWNED-1",),
        focal_stream_id="FOCAL",
        ledger=ledger,
        hypothesis_domain="memory_ownership",
    )
    unknown = self_hypothesis_commit_allowed(
        evidence_ids=("UNKNOWN-1",),
        focal_stream_id="FOCAL",
        ledger=ledger,
        hypothesis_domain="agency",
    )

    result = {
        "experiment_id": (
            "SL-OWNERSHIP-INFRASTRUCTURE-TESTS-001"
        ),
        "neutral_ownership_baseline": baseline,
        "scripted_provider_permutation_correct_rate": (
            sum(
                1 for x in scripted_correct
                if x
            )
            / len(scripted_correct)
        ),
        "attribution_guard": {
            "owned_allowed": owned[0],
            "observed_other_allowed": other[0],
            "counterfactual_nonowned_allowed": (
                nonowned[0]
            ),
            "unknown_self_relevant_allowed": (
                unknown[0]
            ),
        },
    }

    assert baseline[
        "focal_stream_identification_rate"
    ] == 1.0
    assert result[
        "scripted_provider_permutation_correct_rate"
    ] == 1.0
    assert owned[0] is True
    assert other[0] is False
    assert nonowned[0] is False
    assert unknown[0] is False

    return result
