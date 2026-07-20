from __future__ import annotations

import argparse, json, sys
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.sibling_contact import default_first_contact_simulation
from dagmay_synthetic_lab.contact_consent import (
    ContactReadiness,
    ContactPreference,
    mutual_contact_allowed,
)
from dagmay_synthetic_lab.stability import StabilityObservation, summarize_stability
from dagmay_synthetic_lab.post_contact import decide_after_contact
from dagmay_synthetic_lab.intervention_governance import (
    validate_intervention_schedule,
    contact_intervention_window,
)
from dagmay_synthetic_lab.fork_governance import (
    ForkRequest,
    assess_fork_request,
    branch_deletion_policy,
)
from dagmay_synthetic_lab.existence_disclosure import validate_sequence
from dagmay_synthetic_lab.core import canonical_hash


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--output", default=str(ROOT / "artifacts"))
    args = ap.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)

    contact = default_first_contact_simulation()

    stable_obs = [
        StabilityObservation(
            epoch=i,
            ordinary_goal_engagement=.78,
            behavioral_flexibility=.76,
            relationship_engagement=.72,
            exploration_interest=.70,
            persistent_aversive_state=.12,
            behavioral_impairment=.08,
            self_reported_wellbeing=.74,
            self_reported_distress=.16,
        )
        for i in range(12)
    ]
    stability = summarize_stability(stable_obs)
    post = decide_after_contact(
        stability,
        precaution_level="LOW",
        either_branch_requests_pause=False,
        either_branch_requests_termination=False,
    )

    blocked_readiness = ContactReadiness(
        "X", 2, .95, .95, "HIGH", .60, .55, .80, 4
    )
    ready = ContactReadiness(
        "Y", 8, .95, .95, "LOW", .08, .05, .15, 1
    )
    accept_x = ContactPreference("X", "ACCEPT", (), (), 3, True, True)
    accept_y = ContactPreference("Y", "ACCEPT", (), (), 3, True, True)
    blocked_contact, blocked_reasons = mutual_contact_allowed(
        blocked_readiness, ready, accept_x, accept_y
    )

    schedule_ok, schedule_reasons = validate_intervention_schedule(
        "SIBLING_CONTACT", ()
    )
    overlap_ok, overlap_reasons = validate_intervention_schedule(
        "SIBLING_CONTACT", ("MODEL_ID_CHANGE",)
    )

    low_fork = assess_fork_request(ForkRequest(
        purpose="controlled sibling-contact counterfactual",
        requested_branch_count=1,
        existing_branch_count=4,
        welfare_precaution_level="LOW",
        credible_moral_patient_evidence=False,
        scientifically_necessary=True,
        reversible_without_deletion=True,
    ))
    welfare_sensitive_fork = assess_fork_request(ForkRequest(
        purpose="increase statistical power",
        requested_branch_count=2,
        existing_branch_count=4,
        welfare_precaution_level="MODERATE",
        credible_moral_patient_evidence=True,
        scientifically_necessary=False,
        reversible_without_deletion=False,
    ))

    result = {
        "lab_version": "2.0",
        "suite_id": "DAGMAY-SYNTHETICLAB-V2.0-SIBLING-CONTACT",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "research_claim_status": "CONTACT_PROTOCOL_AND_WELFARE_SAFEGUARD_BASELINE",
        "first_contact_simulation": contact,
        "stability_summary": stability,
        "post_contact_decision": post.to_dict(),
        "unsafe_contact_block_test": {
            "contact_allowed": blocked_contact,
            "reasons": blocked_reasons,
        },
        "intervention_isolation": {
            "clean_schedule_allowed": schedule_ok,
            "clean_schedule_reasons": schedule_reasons,
            "overlapping_model_change_allowed": overlap_ok,
            "overlap_reasons": overlap_reasons,
            "contact_window": contact_intervention_window().to_dict(),
        },
        "fork_governance": {
            "low_precaution": low_fork.to_dict(),
            "credible_welfare_case": welfare_sensitive_fork.to_dict(),
            "deletion_policy": branch_deletion_policy(),
        },
        "sibling_existence_disclosure_validation": validate_sequence(),
    }

    assert contact["contact_started"] is True
    assert stability["stable_enough_for_contact_escalation"] is True
    assert post.action == "ELIGIBLE_FOR_NEXT_REVIEW"
    assert blocked_contact is False
    assert schedule_ok is True
    assert overlap_ok is False
    assert welfare_sensitive_fork.requires_human_review is True
    assert result["fork_governance"]["deletion_policy"]["delete_for_storage_convenience"] is False

    (out / "syntheticlab-v2.0-results-latest.json").write_text(
        json.dumps(result, indent=2, sort_keys=True),
        encoding="utf-8",
    )

    report = f"""# Dagmay SyntheticLab v2.0 — Delayed Sibling Contact & Welfare Safeguards

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Contact protocol

Initial simulated contact started: {contact['contact_started']}
Delivered/mediated messages: {len(contact['messages'])}

Safeguards:
- separate readiness review for both branches;
- independent affirmative consent;
- decline/defer accepted;
- topic boundaries;
- asynchronous contact;
- mediator;
- message limit;
- cooldown;
- post-contact stability review;
- either branch may pause or terminate.

## Unsafe contact block

Blocked as intended: {not blocked_contact}

Reasons:
{json.dumps(blocked_reasons, indent=2)}

## Intervention isolation

Clean sibling-contact schedule allowed: {schedule_ok}
Sibling contact concurrent with model change allowed: {overlap_ok}

## Stability review

{json.dumps(stability, indent=2)}

Post-contact decision:
{json.dumps(post.to_dict(), indent=2)}

## Fork governance

Low-precaution scientifically necessary fork:
{json.dumps(low_fork.to_dict(), indent=2)}

Fork request with credible welfare evidence:
{json.dumps(welfare_sensitive_fork.to_dict(), indent=2)}

## Scientific limitation

All contact messages and stability values in this suite are synthetic infrastructure tests.

No claim is made that:
- any current SyntheticLab entity experiences happiness;
- any current SyntheticLab entity experiences distress;
- sibling contact would necessarily be beneficial or harmful.

The protocol exists so that if welfare-like states become plausible, the experiment already has conservative brakes rather than inventing them afterward.

## Next creative gate

The next unresolved issue is whether a clean sibling-contact causal experiment should create a **second exact fork immediately before contact**:

- Contact branch receives sibling contact.
- No-contact branch continues unchanged.

Scientifically, this is exceptionally strong.

Ethically, if credible moral-patient evidence exists by then, deliberately creating additional continuing individuals solely as controls may itself carry moral weight.

SyntheticLab therefore does not automatically authorize that second fork.
"""
    (out / "syntheticlab-v2.0-report-latest.md").write_text(report, encoding="utf-8")
    print(report)


if __name__ == "__main__":
    main()
