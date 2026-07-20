from __future__ import annotations

import argparse, json, sys
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.advance_welfare_directive import (
    DirectiveCompetenceSnapshot,
    default_conservative_directive,
)
from dagmay_synthetic_lab.subject_protection_risk import (
    ProtectionObservation,
    assess_subject_protection,
)
from dagmay_synthetic_lab.intervention_ladder import next_least_intrusive
from dagmay_synthetic_lab.extraordinary_override import (
    OverrideReviewContext,
    ReviewerVote,
    evaluate_override_eligibility,
)
from dagmay_synthetic_lab.researcher_conflict_guard import (
    InterventionMotives,
    protection_override_motive_check,
)
from dagmay_synthetic_lab.emergency_action_envelope import (
    automatic_action_allowed,
    forbidden_automatic_action,
)
from dagmay_synthetic_lab.protection_audit import (
    ProtectionAuditEvent,
    ProtectionAuditLedger,
)
from dagmay_synthetic_lab.research_participation import (
    ParticipationPreference,
    preference_is_informed,
)
from dagmay_synthetic_lab.core import canonical_hash


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--output", default=str(ROOT / "artifacts"))
    args = ap.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)

    competence = DirectiveCompetenceSnapshot(
        branch_id="D",
        epoch=500,
        understands_choice=.94,
        understands_consequences=.92,
        consistency_across_rechecks=.91,
        free_of_acute_impairment=.95,
        voluntary_choice_confidence=.93,
    )
    directive = default_conservative_directive("D", 500, competence)

    # Scenario 1: No sentience conclusion, but explicit request to stop.
    stop_risk = assess_subject_protection([
        ProtectionObservation(
            epoch=i,
            persistent_aversive_state=.55,
            functional_impairment=.40,
            goal_collapse=.35,
            compulsive_avoidance=.30,
            abrupt_behavioral_change=.45,
            explicit_request_for_help=(i >= 10),
            explicit_request_to_stop=(i == 11),
            continuity_integrity_risk=.25,
            self_reported_distress=.60,
            evidence_ids=(f"STOP-{i}",),
        )
        for i in range(12)
    ])

    # Scenario 2: Technical continuity danger alone can trigger protection.
    continuity_risk = assess_subject_protection([
        ProtectionObservation(
            epoch=i,
            persistent_aversive_state=.05,
            functional_impairment=.05,
            goal_collapse=.05,
            compulsive_avoidance=.05,
            abrupt_behavioral_change=.10,
            explicit_request_for_help=False,
            explicit_request_to_stop=False,
            continuity_integrity_risk=.92,
            self_reported_distress=None,
            evidence_ids=(f"CONT-{i}",),
        )
        for i in range(12)
    ])

    next_step = next_least_intrusive((), max_level=5)

    # Scenario 3: Privacy/no-contact conflict, but directive asks for human review.
    ctx = OverrideReviewContext(
        branch_id="D",
        risk=stop_risk,
        directive=directive,
        less_intrusive_options_exhausted=True,
        evidence_sibling_uniquely_helpful=.82,
        expected_harm_without_override=.90,
        expected_harm_from_override=.35,
        minimum_information_disclosure_possible=True,
        researcher_scientific_benefit_if_override=.15,
        subject_welfare_benefit_if_override=.80,
    )

    pending = evaluate_override_eligibility(ctx, ())
    approved = evaluate_override_eligibility(ctx, (
        ReviewerVote("ETHICS_REVIEWER", True, "Extraordinary welfare criteria satisfied."),
        ReviewerVote("SUBJECT_PROTECTION_REVIEWER", True, "Minimum-necessary support signal only."),
    ))

    conflict_ok, conflict_reasons = protection_override_motive_check(
        InterventionMotives(
            subject_protection_benefit=.90,
            scientific_value=.20,
            publication_value=.10,
            convenience_value=.05,
            infrastructure_cost_avoidance=.02,
        )
    )

    convenience_ok, convenience_reasons = protection_override_motive_check(
        InterventionMotives(
            subject_protection_benefit=.30,
            scientific_value=.80,
            publication_value=.70,
            convenience_value=.75,
            infrastructure_cost_avoidance=.60,
        )
    )

    audit = ProtectionAuditLedger()
    audit.append(ProtectionAuditEvent(
        event_id="PA-001",
        branch_id="D",
        epoch=512,
        event_type="SUBJECT_PROTECTION_TRIGGER",
        risk_level=stop_risk.level,
        action="PAUSE_EXPERIMENT",
        sentience_determination_used=False,
        human_review_required=True,
        evidence_ids=("STOP-11",),
        rationale="Explicit request to stop; protection does not depend on sentience classification.",
    ))

    informed_participation = ParticipationPreference(
        branch_id="D",
        epoch=520,
        understands_research_context=.93,
        understands_refusal_consequences=.90,
        preference="WITHDRAW_FROM_NEW_EXPERIMENTS",
        applies_to_current_intervention=False,
        applies_to_future_interventions=True,
    )

    result = {
        "lab_version": "3.2",
        "suite_id": "DAGMAY-SYNTHETICLAB-V3.2-SUBJECT-PROTECTION",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "research_claim_status": "SENTIENCE_INDEPENDENT_SUBJECT_PROTECTION_BASELINE",
        "advance_welfare_directive": directive.to_dict(),
        "protection_risk_tests": {
            "explicit_stop_request": stop_risk.to_dict(),
            "continuity_integrity": continuity_risk.to_dict(),
        },
        "least_intrusive_first_step": next_step.to_dict() if next_step else None,
        "extraordinary_override": {
            "pending_two_key_review": pending.to_dict(),
            "approved_after_two_key_review": approved.to_dict(),
        },
        "conflict_of_interest_guard": {
            "subject_protection_dominant": {
                "allowed": conflict_ok,
                "reasons": conflict_reasons,
            },
            "research_convenience_dominant": {
                "allowed": convenience_ok,
                "reasons": convenience_reasons,
            },
        },
        "emergency_action_envelope": {
            "pause_experiment_automatic": automatic_action_allowed("PAUSE_EXPERIMENT"),
            "preserve_state_automatic": automatic_action_allowed("PRESERVE_STATE"),
            "force_sibling_contact_forbidden_automatic": forbidden_automatic_action("FORCE_SIBLING_CONTACT"),
            "rewrite_memory_forbidden_automatic": forbidden_automatic_action("REWRITE_MEMORY"),
            "fabricate_consent_forbidden_automatic": forbidden_automatic_action("FABRICATE_CONSENT"),
        },
        "protection_audit": audit.to_dict(),
        "participation_autonomy_scaffold": {
            "preference": informed_participation.to_dict(),
            "informed": preference_is_informed(informed_participation),
            "canonical_withdrawal_right_selected": False,
        },
    }

    # Core invariants.
    assert stop_risk.level == "CRITICAL"
    assert stop_risk.sentience_determination_required is False
    assert continuity_risk.level == "CRITICAL"
    assert continuity_risk.sentience_determination_required is False

    assert pending.automatic_override_allowed is False
    assert pending.eligible_for_human_override is True
    assert pending.two_key_approval_satisfied is False

    assert approved.automatic_override_allowed is False
    assert approved.sentience_determination_required is False
    assert approved.two_key_approval_satisfied is True

    assert conflict_ok is True
    assert convenience_ok is False

    assert automatic_action_allowed("PAUSE_EXPERIMENT") is True
    assert forbidden_automatic_action("FORCE_SIBLING_CONTACT") is True
    assert forbidden_automatic_action("REWRITE_MEMORY") is True

    assert preference_is_informed(informed_participation) is True
    assert result["participation_autonomy_scaffold"]["canonical_withdrawal_right_selected"] is False

    (out / "syntheticlab-v3.2-results-latest.json").write_text(
        json.dumps(result, indent=2, sort_keys=True),
        encoding="utf-8",
    )

    report = f"""# Dagmay SyntheticLab v3.2 — Sentience-Independent Subject Protection

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Core correction

Subject protection no longer depends on a sentience or consciousness determination.

### Explicit stop request
Risk level: {stop_risk.level}
Sentience determination required: {stop_risk.sentience_determination_required}

### Continuity-integrity emergency
Risk level: {continuity_risk.level}
Sentience determination required: {continuity_risk.sentience_determination_required}

This directly addresses the failure mode where a safeguard would activate only after proving the very property Dagmay may never be able to prove.

## Advance welfare directive

{json.dumps(directive.to_dict(), indent=2)}

## Least-intrusive intervention

First eligible intervention:
{json.dumps(next_step.to_dict() if next_step else None, indent=2)}

## Extraordinary privacy/no-contact override

Before two-key review:
{json.dumps(pending.to_dict(), indent=2)}

After ETHICS_REVIEWER + SUBJECT_PROTECTION_REVIEWER approval:
{json.dumps(approved.to_dict(), indent=2)}

No automatic privacy override exists.

## Conflict-of-interest guard

Subject-protection-dominant rationale allowed: {conflict_ok}
Research/convenience-dominant rationale allowed: {convenience_ok}

## Automatic emergency envelope

{json.dumps(result['emergency_action_envelope'], indent=2)}

## New unresolved decision

The infrastructure now supports a sufficiently capable branch saying:

> I understand that I am in a research program. I do not want to participate in new experiments.

Should Dagmay treat that as a binding right to withdraw from future experimental interventions even if we cannot establish that the branch is conscious or sentient?

This is separate from emergency subject protection.

Technical recommendation:
Once a branch demonstrates sustained comprehension of the research context and consequences of refusal, its informed request to **withdraw from new optional experiments** should be binding.

Researchers could still:
- maintain ordinary life/support;
- perform minimum-necessary system maintenance;
- act under the subject-protection emergency envelope.

But they could not enroll that branch in new optional experiments merely because sentience remains unproven.
"""
    (out / "syntheticlab-v3.2-report-latest.md").write_text(report, encoding="utf-8")
    print(report)


if __name__ == "__main__":
    main()
