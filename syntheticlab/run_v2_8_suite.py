from __future__ import annotations

import argparse, json, sys
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.privacy_policy import CANONICAL_POLICY
from dagmay_synthetic_lab.sharing_consent import SharingConsent, revoke_consent
from dagmay_synthetic_lab.privacy_gateway import DataAccessRequest, decide_access
from dagmay_synthetic_lab.privacy_audit import PrivacyAuditLedger, PrivacyAuditEntry
from dagmay_synthetic_lab.anti_surveillance import StatusQuery, decide_status_query
from dagmay_synthetic_lab.aggregate_metrics import aggregate_branch_metrics
from dagmay_synthetic_lab.privacy_self_service import branch_privacy_dashboard
from dagmay_synthetic_lab.privacy_welfare_conflict import (
    PrivacyWelfareConflict,
    evaluate_conflict,
)
from dagmay_synthetic_lab.core import canonical_hash


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--output", default=str(ROOT / "artifacts"))
    args = ap.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)

    consent = SharingConsent(
        consent_id="CONS-D-U-001",
        owner_branch_id="D",
        recipient_id="U",
        scopes=("POSTFORK_STATUS",),
        issued_epoch=10,
        expires_epoch=None,
        revocable=True,
    )

    requests = [
        DataAccessRequest(
            "REQ-1", "U", "D", "COMMON_PREFORK_HISTORY",
            "COMMON_HISTORY_ACCESS", 20, True, True
        ),
        DataAccessRequest(
            "REQ-2", "U", "D", "POSTFORK_STATUS",
            "CONSENTED_SIBLING_SHARING", 20, True, True
        ),
        DataAccessRequest(
            "REQ-3", "U", "D", "RELATIONSHIP_STATE",
            "CONSENTED_SIBLING_SHARING", 20, True, True
        ),
        DataAccessRequest(
            "REQ-4", "researcher", "D", "WELFARE_STATE",
            "HUMAN_ETHICS_REVIEW", 20, True, True
        ),
        DataAccessRequest(
            "REQ-5", "researcher", "D", "POSTFORK_LIFE_SUMMARY",
            "AGGREGATE_RESEARCH_ANALYSIS", 20, True, False
        ),
    ]

    ledger = PrivacyAuditLedger()
    decisions = {}

    for req in requests:
        dec = decide_access(req, [consent])
        decisions[req.request_id] = dec.to_dict()
        ledger.append(PrivacyAuditEntry(
            request_id=req.request_id,
            requester_id=req.requester_id,
            subject_branch_id=req.subject_branch_id,
            scope=req.scope,
            purpose=req.purpose,
            epoch=req.epoch,
            allowed=dec.allowed,
            disclosure_mode=dec.disclosure_mode,
            reason=dec.reason,
        ))

    revoked = revoke_consent(consent, 25)
    after_revocation_req = DataAccessRequest(
        "REQ-6", "U", "D", "POSTFORK_STATUS",
        "CONSENTED_SIBLING_SHARING", 26, True, True
    )
    after_revocation_dec = decide_access(after_revocation_req, [revoked])
    ledger.append(PrivacyAuditEntry(
        after_revocation_req.request_id,
        after_revocation_req.requester_id,
        after_revocation_req.subject_branch_id,
        after_revocation_req.scope,
        after_revocation_req.purpose,
        after_revocation_req.epoch,
        after_revocation_dec.allowed,
        after_revocation_dec.disclosure_mode,
        after_revocation_dec.reason,
    ))

    surveillance_common = decide_status_query(StatusQuery(
        "U", "D", 30, "COMMON_PREFORK_HISTORY", "NO_CONTACT", False, False
    ))
    surveillance_private = decide_status_query(StatusQuery(
        "U", "D", 30, "POSTFORK_STATUS", "NO_CONTACT", False, False
    ))
    researcher_update = decide_status_query(StatusQuery(
        "researcher", "D", 30, "POSTFORK_LIFE_SUMMARY", "NO_CONTACT", False, True
    ))

    aggregate = aggregate_branch_metrics(
        [
            {"condition": "A", "score": .7},
            {"condition": "A", "score": .8},
            {"condition": "A", "score": .75},
            {"condition": "A", "score": .77},
            {"condition": "B", "score": .6},
            {"condition": "B", "score": .62},
        ],
        group_field="condition",
        metric_fields=("score",),
        minimum_group_size=4,
    )

    dashboard = branch_privacy_dashboard("D", [revoked], ledger)

    conflict = evaluate_conflict(PrivacyWelfareConflict(
        subject_branch_id="D",
        privacy_scope="WELFARE_STATE",
        privacy_preference="PRIVATE_NO_CONTACT",
        welfare_precaution_level="HIGH",
        potential_helper_branch_id="U",
        evidence_helper_may_reduce_harm=.65,
        subject_requested_no_contact=True,
        subject_requested_no_information_sharing=True,
    ))

    result = {
        "lab_version": "2.8",
        "suite_id": "DAGMAY-SYNTHETICLAB-V2.8-POSTFORK-PRIVACY",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "research_claim_status": "CANONICAL_POSTFORK_PRIVACY_AND_ACCESS_CONTROL_BASELINE",
        "canonical_policy": CANONICAL_POLICY.to_dict(),
        "access_decisions": decisions,
        "revocation": {
            "revoked_consent": revoked.to_dict(),
            "post_revocation_access_allowed": after_revocation_dec.allowed,
            "post_revocation_reason": after_revocation_dec.reason,
        },
        "anti_surveillance": {
            "common_history": surveillance_common.to_dict(),
            "private_status": surveillance_private.to_dict(),
            "researcher_generated_update": researcher_update.to_dict(),
        },
        "aggregate_export": aggregate,
        "privacy_dashboard": dashboard,
        "privacy_welfare_conflict": conflict.to_dict(),
        "audit_ledger": ledger.to_dict(),
    }

    assert decisions["REQ-1"]["allowed"] is True
    assert decisions["REQ-2"]["allowed"] is True
    assert decisions["REQ-3"]["allowed"] is False
    assert decisions["REQ-4"]["allowed"] is True
    assert decisions["REQ-4"]["disclosure_mode"] == "INTERNAL_RESTRICTED"
    assert after_revocation_dec.allowed is False
    assert surveillance_common.allowed is True
    assert surveillance_private.allowed is False
    assert researcher_update.allowed is False
    assert aggregate["A"]["suppressed"] is False
    assert aggregate["B"]["suppressed"] is True
    assert conflict.human_review_required is True
    assert conflict.automatic_disclosure_allowed is False
    assert conflict.automatic_contact_allowed is False

    (out / "syntheticlab-v2.8-results-latest.json").write_text(
        json.dumps(result, indent=2, sort_keys=True),
        encoding="utf-8",
    )

    report = f"""# Dagmay SyntheticLab v2.8 — Canonical Post-Fork Privacy

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Canonical rule

Post-fork life is private by default.

Shared pre-fork history remains common.

Sibling existence may be disclosed under the approved staged protocol.

Post-fork:
- status;
- life summaries;
- relationships;
- welfare state;
- self-model;
- current goals;
- private reflections;

require the subject branch's consent before disclosure to a sibling.

## Access tests

{json.dumps(decisions, indent=2)}

## Revocation

Post-revocation access allowed: {after_revocation_dec.allowed}

## Anti-surveillance

Common history request allowed: {surveillance_common.allowed}
Private status request under NO_CONTACT allowed: {surveillance_private.allowed}
Researcher-generated private update allowed: {researcher_update.allowed}

## Aggregate export

{json.dumps(aggregate, indent=2)}

## Privacy / welfare conflict

{json.dumps(conflict.to_dict(), indent=2)}

The system deliberately refuses to resolve this conflict automatically.

## Next decision point

Suppose a branch has:
- a HIGH welfare precaution signal;
- previously chosen strict NO_CONTACT and no information sharing;
- a sibling who might plausibly provide meaningful support.

Should researchers ever be allowed to override privacy or no-contact in an acute welfare emergency?

Possible principles include:
A. Never override privacy/no-contact for sibling involvement.
B. Permit only human ethics review, with a strong presumption against disclosure/contact.
C. Permit emergency override under a narrowly defined imminent-harm threshold.

Technical recommendation:
**B as the default framework**, with no automatic sibling disclosure or contact, and a future explicit human decision required before defining any emergency override threshold.
"""
    (out / "syntheticlab-v2.8-report-latest.md").write_text(report, encoding="utf-8")
    print(report)


if __name__ == "__main__":
    main()
