from __future__ import annotations

def build_override_case_packet(
    proposal,
    subject_preference_history,
    capacity_history,
    alternatives_attempted,
    advocate_decision,
    reviewer_decisions,
    conflicts,
):
    return {
        "proposal": proposal.to_dict(),
        "subject_preference_history": subject_preference_history,
        "capacity_history": capacity_history,
        "alternatives_attempted": alternatives_attempted,
        "advocate_decision": advocate_decision.to_dict(),
        "reviewer_decisions": [r.to_dict() for r in reviewer_decisions],
        "conflicts": conflicts,
        "required_questions": (
            "Is the individual's preference represented accurately?",
            "Was decision-specific capacity assessed before the research team knew the preferred answer?",
            "Were less restrictive alternatives genuinely attempted?",
            "Is subject protection the dominant purpose rather than scientific value?",
            "Is any reviewer conflicted or non-independent?",
            "Would the same standard be used if the individual's answer favored the researchers?",
        ),
        "automatic_authorization": False,
    }
