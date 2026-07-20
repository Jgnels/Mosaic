from __future__ import annotations

from pathlib import Path
import tempfile

from run_mosaic_memory_grounding_pilot import PROTOCOL_ID, _request, _validate
from dagmay_synthetic_lab.provider_payload_security import HardenedProviderPayloadBoundary


def main() -> None:
    with tempfile.TemporaryDirectory() as directory:
        boundary = HardenedProviderPayloadBoundary(
            archive_directory=Path(directory), experiment_id=PROTOCOL_ID
        )
        request = _request(
            subject="SUBJ-0123456789abcdef01234567",
            character="Ari",
            counterpart="Bo",
            evidence=[{"evidence_id": "EV-0-A", "summary": "repaired Ari's generator"}],
        )
        digest = boundary.archive_exact_request(request)
        assert len(digest) == 64
        assert "CAUSAL" not in str(request)
        assert "DISTRACTOR" not in str(request)
        answered = _validate(
            {"status": "ANSWERED", "answer": "Bo repaired my generator.", "cited_evidence_ids": ["EV-0-A"]},
            {"EV-0-A"},
        )
        assert answered["status"] == "ANSWERED"
        abstained = _validate(
            {"status": "INSUFFICIENT_EVIDENCE", "answer": "I do not have enough remembered evidence.", "cited_evidence_ids": []},
            {"EV-0-D1"},
        )
        assert abstained["status"] == "INSUFFICIENT_EVIDENCE"
        try:
            _validate(
                {"status": "ANSWERED", "answer": "Invented.", "cited_evidence_ids": ["FAKE"]},
                {"EV-0-A"},
            )
        except ValueError:
            pass
        else:
            raise AssertionError("fabricated evidence citation was accepted")
    print("PASS: Mosaic memory grounding provider protocol offline controls")


if __name__ == "__main__":
    main()

