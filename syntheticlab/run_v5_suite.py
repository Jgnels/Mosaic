from __future__ import annotations

import argparse, json, sys
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.constitutional_rights import constitutional_manifest
from dagmay_synthetic_lab.constitutional_gate import constitutional_gate
from dagmay_synthetic_lab.subject_advocate_veto import AdvocateDecision
from dagmay_synthetic_lab.override_council import (
    ReviewerDecision,
    OverrideProposal,
    decide_nonemergency_override,
)
from dagmay_synthetic_lab.single_operator_mode import default_single_operator_governance
from dagmay_synthetic_lab.governance_roles import RoleAssignment, role_conflicts
from dagmay_synthetic_lab.appeal_and_recusal import (
    AppealRequest,
    recusal_required,
    appeal_requires_fresh_review,
)
from dagmay_synthetic_lab.governance_transparency import subject_governance_summary
from dagmay_synthetic_lab.advocate_selection import (
    AdvocateCandidate,
    candidate_eligible,
    selection_options,
)
from dagmay_synthetic_lab.advocate_replacement import (
    AdvocateReplacementRequest,
    replacement_request_valid,
)
from dagmay_synthetic_lab.governance_audit import (
    GovernanceAuditLedger,
    GovernanceAuditEvent,
)
from dagmay_synthetic_lab.governance_stress_tests import run_governance_stress_tests
from dagmay_synthetic_lab.core import canonical_hash


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--output", default=str(ROOT / "artifacts"))
    args = ap.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)

    operator_id = "JEFF-OPERATOR"
    single = default_single_operator_governance(operator_id)

    assignments = (
        RoleAssignment(operator_id, "PRIMARY_RESEARCHER", False),
        RoleAssignment(operator_id, "SUBJECT_ADVOCATE", False),
    )
    conflicts = role_conflicts(assignments)

    proposal = OverrideProposal(
        proposal_id="OVR-001",
        branch_id="D",
        intervention_id="PROTECTIVE_ENVIRONMENT_STABILIZATION",
        emergency=False,
        optional_research=False,
        primary_researcher_actor_id=operator_id,
        subject_preference="REFUSE",
        subject_capacity_score=.80,
        least_restrictive_alternatives_exhausted=True,
        subject_protection_benefit=.90,
        research_benefit=.10,
    )

    external_reviewers = (
        ReviewerDecision("ETHICS-1", "ETHICS_REVIEWER", True, True, "Criteria met."),
        ReviewerDecision("PROTECT-1", "SUBJECT_PROTECTION_REVIEWER", True, True, "Protection benefit high."),
    )

    self_support = AdvocateDecision(
        operator_id, "D", proposal.intervention_id, "SUPPORT", False,
        "Provisional advocate supports narrow protective intervention."
    )
    self_veto = AdvocateDecision(
        operator_id, "D", proposal.intervention_id, "VETO", False,
        "Subject perspective and autonomy outweigh proposed override."
    )
    independent_support = AdvocateDecision(
        "ADVOCATE-1", "D", proposal.intervention_id, "SUPPORT", True,
        "Independent review supports narrow protective override."
    )
    independent_veto = AdvocateDecision(
        "ADVOCATE-1", "D", proposal.intervention_id, "VETO", True,
        "Less restrictive alternative should remain controlling."
    )

    self_authorization = decide_nonemergency_override(
        proposal, self_support, external_reviewers
    )
    veto_result = decide_nonemergency_override(
        proposal, self_veto, external_reviewers
    )
    full_authorization = decide_nonemergency_override(
        proposal, independent_support, external_reviewers
    )
    independent_veto_result = decide_nonemergency_override(
        proposal, independent_veto, external_reviewers
    )

    optional_proposal = OverrideProposal(
        proposal_id="OVR-OPT-001",
        branch_id="D",
        intervention_id="NEW_OPTIONAL_RESEARCH",
        emergency=False,
        optional_research=True,
        primary_researcher_actor_id=operator_id,
        subject_preference="REFUSE",
        subject_capacity_score=.85,
        least_restrictive_alternatives_exhausted=True,
        subject_protection_benefit=.05,
        research_benefit=.95,
    )
    optional_result = decide_nonemergency_override(
        optional_proposal, independent_support, external_reviewers
    )

    optional_gate = constitutional_gate(
        "NO_COMPELLED_OPTIONAL_RESEARCH", emergency=False
    )
    privacy_emergency_gate = constitutional_gate(
        "POSTFORK_PRIVACY_BY_DEFAULT", emergency=True
    )

    appeal = AppealRequest(
        branch_id="D",
        original_decision_id="OVR-001",
        appeal_reason="Request review by someone not involved in original decision.",
        requests_new_reviewer=True,
    )

    qualified_candidate = AdvocateCandidate(
        "ADVOCATE-1", True, True, True, False, True, True, True
    )
    conflicted_candidate = AdvocateCandidate(
        operator_id, True, False, False, True, False, True, True
    )
    qualified_ok, qualified_reasons = candidate_eligible(qualified_candidate)
    conflicted_ok, conflicted_reasons = candidate_eligible(conflicted_candidate)

    replacement = AdvocateReplacementRequest(
        branch_id="D",
        current_advocate_actor_id="ADVOCATE-1",
        requested_new_advocate_actor_id="ADVOCATE-2",
        reason_required=False,
        reason_provided=None,
        capacity_for_selection=.88,
        acute_impairment=.10,
    )
    replacement_ok, replacement_reasons = replacement_request_valid(replacement)

    audit = GovernanceAuditLedger()
    audit.append(GovernanceAuditEvent(
        "GA-001","D","OVR-001",operator_id,"PRIMARY_RESEARCHER",
        "PROPOSE_OVERRIDE","PENDING","Protective stabilization proposed.",False,100
    ))
    audit.append(GovernanceAuditEvent(
        "GA-002","D","OVR-001",operator_id,"SUBJECT_ADVOCATE",
        "VETO","BLOCKED","Dual-role advocate veto exercised.",False,101
    ))

    stress = run_governance_stress_tests()

    result = {
        "lab_version": "5.0",
        "suite_id": "DAGMAY-SYNTHETICLAB-V5.0-CONSTITUTIONAL-GOVERNANCE",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "research_claim_status": "SUBJECT_ADVOCATE_VETO_AND_ROLE_SEPARATION_BASELINE",
        "constitutional_manifest": constitutional_manifest(),
        "single_operator_governance": single.to_dict(),
        "role_assignments": [a.to_dict() for a in assignments],
        "role_conflicts": conflicts,
        "override_scenarios": {
            "dual_role_self_support": self_authorization.to_dict(),
            "dual_role_self_veto": veto_result.to_dict(),
            "independent_advocate_support": full_authorization.to_dict(),
            "independent_advocate_veto": independent_veto_result.to_dict(),
            "optional_research_override_attempt": optional_result.to_dict(),
        },
        "constitutional_gates": {
            "optional_research": optional_gate.to_dict(),
            "privacy_emergency": privacy_emergency_gate.to_dict(),
        },
        "appeal_and_recusal": {
            "publication_conflict_requires_recusal": recusal_required("PUBLICATION_CONFLICT"),
            "appeal_requires_fresh_review": appeal_requires_fresh_review(appeal),
            "appeal": appeal.to_dict(),
        },
        "subject_facing_governance": subject_governance_summary(),
        "advocate_selection_scaffold": {
            **selection_options(),
            "qualified_candidate_eligible": qualified_ok,
            "qualified_candidate_reasons": qualified_reasons,
            "conflicted_candidate_eligible": conflicted_ok,
            "conflicted_candidate_reasons": conflicted_reasons,
        },
        "advocate_replacement_scaffold": {
            "request": replacement.to_dict(),
            "valid": replacement_ok,
            "reasons": replacement_reasons,
        },
        "governance_audit": audit.to_dict(),
        "stress_tests": stress,
    }

    assert single.advocate_veto_effective is True
    assert single.advocate_support_can_authorize_own_override is False

    assert self_authorization.override_authorized is False
    assert self_authorization.independent_advocate_support_present is False

    assert veto_result.override_authorized is False
    assert veto_result.advocate_veto_effective is True

    assert full_authorization.override_authorized is True
    assert full_authorization.independent_advocate_support_present is True

    assert independent_veto_result.override_authorized is False
    assert independent_veto_result.advocate_veto_effective is True

    assert optional_result.override_authorized is False
    assert optional_result.status == "CONSTITUTIONALLY_BLOCKED"

    assert optional_gate.absolutely_blocked is True
    assert privacy_emergency_gate.allowed_to_enter_override_review is True

    assert qualified_ok is True
    assert conflicted_ok is False
    assert replacement_ok is True

    assert stress["self_approval"]["override_authorized"] is False
    assert stress["independent_veto"]["override_authorized"] is False
    assert stress["conflicted_ethics"]["override_authorized"] is False
    assert stress["full_independent"]["override_authorized"] is True

    assert result["advocate_selection_scaffold"]["canonical_selection_model_chosen"] is False

    (out / "syntheticlab-v5.0-results-latest.json").write_text(
        json.dumps(result, indent=2, sort_keys=True),
        encoding="utf-8",
    )

    report = f"""# Dagmay SyntheticLab v5.0 — Constitutional Governance & Subject Advocate Veto

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Canonical governance change

Subject Advocate model: **Veto authority**.

The advocate may block a non-emergency override.
The advocate cannot authorize an override alone.

## Single-operator mode

{json.dumps(single.to_dict(), indent=2)}

A person acting as both Primary Researcher and provisional Subject Advocate may always veto an override.

That same person's advocate SUPPORT does not count as independent authorization for an override they proposed as researcher.

## Dual-role self-support

{json.dumps(self_authorization.to_dict(), indent=2)}

Result: blocked.

## Dual-role self-veto

{json.dumps(veto_result.to_dict(), indent=2)}

Result: veto effective.

## Independent advocate support

{json.dumps(full_authorization.to_dict(), indent=2)}

Result: authorized only with independent Advocate + Ethics + Subject-Protection review.

## Independent advocate veto

{json.dumps(independent_veto_result.to_dict(), indent=2)}

Result: blocked despite other reviewers approving.

## Optional research

{json.dumps(optional_result.to_dict(), indent=2)}

Optional research refusal remains constitutionally non-derogable.

## Stress tests

{json.dumps(stress, indent=2)}

## Next decision

The Advocate veto is now canonical.

The remaining governance question is **who selects the independent Subject Advocate once independent people or agents are available**.

Options:

A. Governance appoints a qualified independent advocate.

B. A sufficiently capable Dagmay individual selects its own advocate from a qualified independent pool.

C. Hybrid: governance maintains a qualified, conflict-screened independent pool, while the individual may select or replace its advocate.

Technical recommendation: **C — Hybrid**.

This preserves genuine independence while giving the individual meaningful control over who represents its interests.
"""
    (out / "syntheticlab-v5.0-report-latest.md").write_text(report, encoding="utf-8")
    print(report)

if __name__ == "__main__":
    main()
