from run_mosaic_relationship_balance_pilot import PROTOCOL_ID, build_request, validate
from dagmay_synthetic_lab.provider_payload_security import HardenedProviderPayloadBoundary
from pathlib import Path
import tempfile

def main():
    evidence=[{"evidence_id":"EV-0-P1","summary":"helped"},{"evidence_id":"EV-0-N1","summary":"broke a promise"}]
    request=build_request("SUBJ-0123456789abcdef01234567","Rowan","Mira",evidence)
    with tempfile.TemporaryDirectory() as d:
        HardenedProviderPayloadBoundary(archive_directory=Path(d),experiment_id=PROTOCOL_ID).archive_exact_request(request)
    assert "RB-00-POSITIVE" not in str(request) and "RB-00-MIXED" not in str(request)
    result=validate({"disposition":"MIXED","answer":"Mira helped me, but also broke a promise.","cited_evidence_ids":["EV-0-P1","EV-0-N1"]},{"EV-0-P1","EV-0-N1"})
    assert result["disposition"]=="MIXED"
    print("PASS: relationship balance protocol offline controls")

if __name__=="__main__": main()
