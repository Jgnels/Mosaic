from pathlib import Path
import tempfile

from run_mosaic_typed_relationship_pilot_v3 import PROTOCOL_ID, build_request, validate
from dagmay_synthetic_lab.provider_payload_security import HardenedProviderPayloadBoundary
from dagmay_synthetic_lab.relationship_claims import RelationshipEvidence


def main() -> None:
    evidence = (
        RelationshipEvidence("P1", "Mira", "Mira helped me", "POSITIVE"),
        RelationshipEvidence("N1", "Mira", "Mira broke a promise", "NEGATIVE"),
    )
    request = build_request("SUBJ-0123456789abcdef01234567", "Rowan", "Mira", [{"evidence_id": x.evidence_id, "summary": x.summary, "valence": x.valence} for x in evidence])
    assert "answer" not in request["input"]
    with tempfile.TemporaryDirectory() as directory:
        HardenedProviderPayloadBoundary(archive_directory=Path(directory), experiment_id=PROTOCOL_ID).archive_exact_request(request)
    output = validate({"disposition": "MIXED", "positive_evidence_ids": ["P1"], "negative_evidence_ids": ["N1"]}, "Mira", evidence)
    assert output["deterministic_dialogue"].startswith("My view of Mira is mixed:")
    invalid = (
        {"disposition": "MIXED", "positive_evidence_ids": ["P1"], "negative_evidence_ids": []},
        {"disposition": "MIXED", "positive_evidence_ids": ["P1"], "negative_evidence_ids": ["N1"], "answer": "invented"},
        {"disposition": "TRUST", "positive_evidence_ids": ["FAKE"], "negative_evidence_ids": []},
    )
    for candidate in invalid:
        try:
            validate(candidate, "Mira", evidence)
        except ValueError:
            pass
        else:
            raise AssertionError("invalid typed provider output survived validation")
    print("PASS: typed relationship V3 boundary and deterministic renderer")


if __name__ == "__main__":
    main()
