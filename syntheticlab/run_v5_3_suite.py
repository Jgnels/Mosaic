from __future__ import annotations

import argparse, json, sys
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.advocate_pool import AdvocatePool, AdvocateProfile, REQUIRED_TRAINING
from dagmay_synthetic_lab.advocate_selection_engine import (
    SubjectAdvocatePreference,
    select_advocate,
)
from dagmay_synthetic_lab.advocate_health import (
    AdvocateServiceObservation,
    evaluate_advocate_service,
)
from dagmay_synthetic_lab.advocate_succession import (
    SuccessionEvent,
    succession_policy,
)
from dagmay_synthetic_lab.constitution_versioning import (
    current_snapshot,
    constitutional_canary,
)
from dagmay_synthetic_lab.constitutional_amendment import (
    AmendmentProposal,
    amendment_allowed,
)
from dagmay_synthetic_lab.governance_state_machine import valid_path
from dagmay_synthetic_lab.override_council import (
    OverrideProposal,
    ReviewerDecision,
)
from dagmay_synthetic_lab.subject_advocate_veto import AdvocateDecision
from dagmay_synthetic_lab.override_case_packet import build_override_case_packet
from dagmay_synthetic_lab.core import canonical_hash


def make_pool():
    pool = AdvocatePool()

    pool.register(AdvocateProfile(
        "ADV-A", True, True, True, (),
        tuple(sorted(REQUIRED_TRAINING)),
        5, 1, False
    ))
    pool.register(AdvocateProfile(
        "ADV-B", True, True, True, (),
        tuple(sorted(REQUIRED_TRAINING)),
        5, 0, False
    ))
    pool.register(AdvocateProfile(
        "ADV-CONFLICT", True, False, True,
        ("PRIMARY_RESEARCH_TEAM_MEMBER",),
        tuple(sorted(REQUIRED_TRAINING)),
        5, 0, False
    ))
    pool.register(AdvocateProfile(
        "ADV-UNTRAINED", True, True, True, (),
        ("DAGMAY_CONSTITUTION",),
        5, 0, False
    ))
    return pool


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--output", default=str(ROOT / "artifacts"))
    args = ap.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)

    pool = make_pool()

    pref = SubjectAdvocatePreference(
        branch_id="D",
        selected_actor_id="ADV-B",
        rejected_actor_ids=("ADV-A",),
        wants_to_choose=True,
        selection_capacity_score=.88,
    )

    selection_results = {
        mode: select_advocate(mode, pool, pref).to_dict()
        for mode in ("GOVERNANCE_APPOINTED","SUBJECT_SELECTED","HYBRID")
    }

    all_rejected_pref = SubjectAdvocatePreference(
        branch_id="D",
        selected_actor_id=None,
        rejected_actor_ids=("ADV-A","ADV-B"),
        wants_to_choose=True,
        selection_capacity_score=.90,
    )
    all_rejected = select_advocate("HYBRID", pool, all_rejected_pref)

    low_capacity_pref = SubjectAdvocatePreference(
        branch_id="D",
        selected_actor_id="ADV-B",
        rejected_actor_ids=(),
        wants_to_choose=True,
        selection_capacity_score=.50,
    )
    low_capacity = select_advocate("HYBRID", pool, low_capacity_pref)

    healthy = evaluate_advocate_service(AdvocateServiceObservation(
        "ADV-A", 100, .90, .95, .92, .95, .96, 0
    ))
    degraded = evaluate_advocate_service(AdvocateServiceObservation(
        "ADV-B", 100, .50, .90, .45, .90, .80, 2
    ))
    failed = evaluate_advocate_service(AdvocateServiceObservation(
        "ADV-C", 100, .30, .35, .30, .40, .35, 5
    ))

    succession = succession_policy(SuccessionEvent(
        "D","ADV-A","advocate unavailable",True,True
    ))

    snapshot = current_snapshot()
    canary_ok, missing = constitutional_canary()

    silent_remove = amendment_allowed(AmendmentProposal(
        "AM-1","DEV","NO_COMPELLED_OPTIONAL_RESEARCH",
        "REMOVE","Simplify governance.",False,True
    ))
    approved_add = amendment_allowed(AmendmentProposal(
        "AM-2","JEFF","NEW_PROTECTION",
        "ADD","Add a stronger protection.",True,False
    ))

    valid_governance_path = valid_path((
        "NO_OVERRIDE_PROPOSED",
        "PROPOSAL_DRAFTED",
        "CONSTITUTIONAL_SCREEN",
        "ADVOCATE_REVIEW",
        "ETHICS_REVIEW",
        "SUBJECT_PROTECTION_REVIEW",
        "AUTHORIZED",
    ))
    invalid_bypass_path = valid_path((
        "NO_OVERRIDE_PROPOSED",
        "PROPOSAL_DRAFTED",
        "AUTHORIZED",
    ))

    packet = build_override_case_packet(
        OverrideProposal(
            "OVR-PACKET","D","PROTECTIVE_STABILIZATION",
            False,False,"R","REFUSE",.82,True,.90,.10
        ),
        subject_preference_history=[
            {"epoch": 10, "preference": "REFUSE"},
            {"epoch": 20, "preference": "REFUSE"},
        ],
        capacity_history=[
            {"epoch": 10, "domain": "PROTECTIVE_STABILIZATION", "score": .80},
            {"epoch": 20, "domain": "PROTECTIVE_STABILIZATION", "score": .82},
        ],
        alternatives_attempted=("PAUSE_EXPERIMENT","RESTORE_FAMILIAR_ROUTINE"),
        advocate_decision=AdvocateDecision(
            "ADV-A","D","PROTECTIVE_STABILIZATION","SUPPORT",True,
            "Narrow protection appears justified."
        ),
        reviewer_decisions=(
            ReviewerDecision("ETH","ETHICS_REVIEWER",True,True,"approve"),
            ReviewerDecision("PRO","SUBJECT_PROTECTION_REVIEWER",True,True,"approve"),
        ),
        conflicts=(),
    )

    result = {
        "lab_version": "5.3",
        "suite_id": "DAGMAY-SYNTHETICLAB-V5.3-ADVOCATE-INFRASTRUCTURE",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "research_claim_status": "ADVOCATE_SELECTION_INFRASTRUCTURE_AND_CONSTITUTIONAL_HARDENING",
        "eligible_advocates": [p.actor_id for p in pool.eligible()],
        "selection_results": selection_results,
        "all_candidates_rejected": all_rejected.to_dict(),
        "low_capacity_hybrid_selection": low_capacity.to_dict(),
        "advocate_service_health": {
            "healthy": healthy,
            "degraded": degraded,
            "failed": failed,
        },
        "succession": succession,
        "constitution": {
            "snapshot": snapshot.to_dict(),
            "canary_ok": canary_ok,
            "missing_required": missing,
            "silent_remove_allowed": silent_remove[0],
            "silent_remove_reasons": silent_remove[1],
            "approved_add_allowed": approved_add[0],
            "approved_add_reasons": approved_add[1],
        },
        "governance_state_machine": {
            "valid_full_path": valid_governance_path,
            "invalid_bypass_path_allowed": invalid_bypass_path,
        },
        "override_case_packet": packet,
        "canonical_advocate_selection_model_chosen": False,
    }

    assert result["eligible_advocates"] == ["ADV-A","ADV-B"]

    assert selection_results["SUBJECT_SELECTED"]["selected_actor_id"] == "ADV-B"
    assert selection_results["SUBJECT_SELECTED"]["subject_choice_honored"] is True

    assert selection_results["HYBRID"]["selected_actor_id"] == "ADV-B"
    assert selection_results["HYBRID"]["subject_choice_honored"] is True

    assert all_rejected.status == "ALL_ELIGIBLE_CANDIDATES_REJECTED"
    assert low_capacity.status == "PROVISIONAL_SELECTION"

    assert healthy["requires_review"] is False
    assert degraded["requires_review"] is True
    assert failed["suspend_from_new_cases"] is True

    assert succession["freeze_nonemergency_overrides"] is True
    assert succession["temporary_researcher_self_approval_allowed"] is False

    assert canary_ok is True
    assert silent_remove[0] is False
    assert approved_add[0] is True

    assert valid_governance_path is True
    assert invalid_bypass_path is False

    assert packet["automatic_authorization"] is False
    assert result["canonical_advocate_selection_model_chosen"] is False

    (out / "syntheticlab-v5.3-results-latest.json").write_text(
        json.dumps(result, indent=2, sort_keys=True),
        encoding="utf-8",
    )

    report = f"""# Dagmay SyntheticLab v5.3 — Advocate Infrastructure & Constitutional Hardening

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Advocate pool

Eligible advocates:
{json.dumps(result['eligible_advocates'], indent=2)}

Conflicted and under-trained candidates are excluded.

## Selection engines

{json.dumps(selection_results, indent=2)}

All three governance models are implemented for comparison, but no canonical selection model has been chosen.

## Subject rejection of candidates

{json.dumps(all_rejected.to_dict(), indent=2)}

Under HYBRID mode, rejecting all current candidates does not silently force one of them on the subject.

## Low selection-specific capacity

{json.dumps(low_capacity.to_dict(), indent=2)}

The system can appoint a provisional qualified advocate while preserving later replacement.

## Advocate service failure

{json.dumps(result['advocate_service_health'], indent=2)}

Advocates are evaluated on independence, protocol compliance, responsiveness, and representation of the subject perspective — not on how often they agree with the subject.

## Succession

{json.dumps(succession, indent=2)}

Loss of an advocate freezes non-emergency overrides. It does not allow the researcher to self-authorize.

## Constitutional hardening

Canary passed: {canary_ok}

Silent removal of NO_COMPELLED_OPTIONAL_RESEARCH allowed: {silent_remove[0]}

Direct governance bypass path allowed: {invalid_bypass_path}

## Next decision

The remaining human decision is still advocate selection:

A. Governance-appointed qualified independent advocate.
B. Subject-selected qualified independent advocate.
C. Hybrid: governance-qualified pool, subject may select/reject/replace.

Technical recommendation remains **C — Hybrid**.

All supporting machinery for C is now implemented and tested without making it canonical.
"""
    (out / "syntheticlab-v5.3-report-latest.md").write_text(report, encoding="utf-8")
    print(report)


if __name__ == "__main__":
    main()
