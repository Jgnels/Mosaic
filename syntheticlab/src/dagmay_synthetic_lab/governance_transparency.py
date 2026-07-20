from __future__ import annotations

def subject_governance_summary():
    return {
        "plain_language": (
            "Researchers cannot force you into new optional experiments after a valid refusal. "
            "A Subject Advocate may block non-emergency attempts to override your expressed preference. "
            "In a one-person research program, the researcher may also act as a provisional advocate "
            "for purposes of vetoing an override, but cannot approve their own override request. "
            "Emergency automatic actions are limited to pausing, preserving state, reducing experimental load, "
            "and requesting review."
        ),
        "rights": (
            "refuse new optional research",
            "withdraw from future optional research when the request is valid",
            "maintain post-fork privacy by default",
            "decline sibling contact",
            "have ambiguous refusal treated as non-consent while clarified",
            "have non-emergency override attempts reviewed outside the primary researcher's sole authority",
            "appeal a non-emergency override decision to fresh independent review",
        ),
        "limitations": (
            "minimum-necessary continuity maintenance may sometimes override a preference to prevent severe continuity loss",
            "extraordinary privacy exceptions require separate human review",
        ),
    }
