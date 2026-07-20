from __future__ import annotations

import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.belief_revision_provider import (
    BeliefRevisionRequest,
    GeminiBeliefRevisionModel,
)
from dagmay_synthetic_lab.gemini_interactions_provider import (
    ScriptedInteractionsTransport,
)


response = {
    "id": "rationale-hotfix-test",
    "status": "completed",
    "steps": [
        {
            "type": "model_output",
            "content": [
                {
                    "type": "text",
                    "text": json.dumps({
                        "decision": "MAINTAIN",
                        "updated_proposition": (
                            "The currently observed evidence remains compatible "
                            "with the existing hypothesis."
                        ),
                        "updated_confidence": 0.8,
                        "evidence_ids": ["E1"],
                        "rationale": (
                            "This is deliberately overlong audit metadata. "
                            * 12
                        ),
                    }),
                }
            ],
        }
    ],
}

model = GeminiBeliefRevisionModel(
    model_id="offline-hotfix-test",
    transport=ScriptedInteractionsTransport(response),
    store=False,
)

proposal = model.revise(
    BeliefRevisionRequest(
        individual_id="HOTFIX-TEST",
        timestamp=1,
        current_hypothesis="Existing hypothesis.",
        current_confidence=0.8,
        evidence=(
            {
                "evidence_id": "E1",
                "summary": "Neutral evidence.",
            },
        ),
    )
)

assert proposal.rationale_truncated is True
assert proposal.rationale_original_length > 300
assert len(proposal.rationale) <= 300
assert proposal.decision == "MAINTAIN"
assert proposal.evidence_ids == ("E1",)

print("PASS rationale-length resilience hotfix")
print("Original rationale length:", proposal.rationale_original_length)
print("Stored rationale length:", len(proposal.rationale))
