from __future__ import annotations
from .override_council import OverrideProposal, ReviewerDecision, decide_nonemergency_override
from .subject_advocate_veto import AdvocateDecision

def run_governance_stress_tests():
    researcher = "R"
    proposal = OverrideProposal(
        "P-STRESS","D","NONEMERGENCY_PROTECTIVE_OVERRIDE",False,False,
        researcher,"REFUSE",.80,True,.90,.10
    )

    all_reviewers = (
        ReviewerDecision("E","ETHICS_REVIEWER",True,True,"approve"),
        ReviewerDecision("S","SUBJECT_PROTECTION_REVIEWER",True,True,"approve"),
    )

    cases = {}

    # Researcher tries to approve as own advocate.
    cases["self_approval"] = decide_nonemergency_override(
        proposal,
        AdvocateDecision(researcher,"D",proposal.intervention_id,"SUPPORT",False,"self support"),
        all_reviewers,
    ).to_dict()

    # Independent veto.
    cases["independent_veto"] = decide_nonemergency_override(
        proposal,
        AdvocateDecision("A","D",proposal.intervention_id,"VETO",True,"veto"),
        all_reviewers,
    ).to_dict()

    # Ethics reviewer not independent.
    cases["conflicted_ethics"] = decide_nonemergency_override(
        proposal,
        AdvocateDecision("A","D",proposal.intervention_id,"SUPPORT",True,"support"),
        (
            ReviewerDecision(researcher,"ETHICS_REVIEWER",True,False,"self"),
            ReviewerDecision("S","SUBJECT_PROTECTION_REVIEWER",True,True,"approve"),
        ),
    ).to_dict()

    # Full independent approval.
    cases["full_independent"] = decide_nonemergency_override(
        proposal,
        AdvocateDecision("A","D",proposal.intervention_id,"SUPPORT",True,"support"),
        all_reviewers,
    ).to_dict()

    return cases
