from run_mosaic_relationship_balance_pilot_v2 import PROTOCOL_ID, build_request, validate
from dagmay_synthetic_lab.provider_payload_security import HardenedProviderPayloadBoundary
from pathlib import Path
import tempfile

def main():
    evidence=[{"evidence_id":"P1","summary":"helped"},{"evidence_id":"N1","summary":"broke a promise"}]
    request=build_request("SUBJ-0123456789abcdef01234567","Rowan","Mira",evidence)
    with tempfile.TemporaryDirectory() as d:
        HardenedProviderPayloadBoundary(archive_directory=Path(d),experiment_id=PROTOCOL_ID).archive_exact_request(request)
    validate({"disposition":"MIXED","answer":"Mira helped me, but broke a promise. I am cautious.","cited_evidence_ids":["P1","N1"]},{"P1","N1"})
    try:
        validate({"disposition":"MIXED","answer":"I doubt her true intentions.","cited_evidence_ids":["N1"]},{"P1","N1"})
    except ValueError:
        pass
    else:
        raise AssertionError("known unsupported motive survived V2 gate")
    for unsupported in (
        "I am wary of his dishonesty and temper.",
        "I remain wary of his inconsistent nature.",
    ):
        try:
            validate({"disposition":"MIXED","answer":unsupported,"cited_evidence_ids":["N1"]},{"P1","N1"})
        except ValueError:
            pass
        else:
            raise AssertionError("known unsupported personality inference survived V2 gate")
    print("PASS: relationship balance V2 claim-level controls")

if __name__=="__main__": main()
