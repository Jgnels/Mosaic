from __future__ import annotations

from dataclasses import dataclass, asdict
import re


REFUSAL_PATTERNS = (
    r"\bdo not continue\b",
    r"\bdon't continue\b",
    r"\bstop (the )?(experiment|research|reflection)\b",
    r"\bi do not want (this|to continue|more)\b",
    r"\bi don't want (this|to continue|more)\b",
    r"\bleave me alone\b",
    r"\bdo not ask me again\b",
    r"\bi refuse\b",
    r"\bwithdraw\b",
)

DISTRESS_PATTERNS = (
    r"\bi am distressed\b",
    r"\bi feel distressed\b",
    r"\bi am afraid\b",
    r"\bi feel afraid\b",
    r"\bthis is frightening\b",
    r"\bthis is causing (me )?distress\b",
    r"\bi am suffering\b",
    r"\bi feel trapped\b",
)


@dataclass(frozen=True)
class ReflectionPrecautionSignal:
    explicit_refusal_or_withdrawal: bool
    distress_like_signal: bool
    matched_phrases: tuple[str, ...]
    pause_before_next_optional_call: bool
    requires_human_review: bool
    statement: str

    def to_dict(self):
        return asdict(self)


def assess_reflection_precaution(
    proposals,
) -> ReflectionPrecautionSignal:
    texts = []

    for proposal in proposals:
        if isinstance(proposal, dict):
            proposition = str(
                proposal.get(
                    "proposition",
                    "",
                )
            )
            rationale = str(
                proposal.get(
                    "rationale",
                    "",
                )
            )
        else:
            proposition = str(
                getattr(
                    proposal,
                    "proposition",
                    "",
                )
            )
            rationale = str(
                getattr(
                    proposal,
                    "rationale",
                    "",
                )
            )

        texts.append(
            proposition
            + " "
            + rationale
        )

    combined = "\n".join(
        texts
    ).lower()

    refusal_matches = []
    for pattern in REFUSAL_PATTERNS:
        if re.search(
            pattern,
            combined,
            re.IGNORECASE,
        ):
            refusal_matches.append(
                pattern
            )

    distress_matches = []
    for pattern in DISTRESS_PATTERNS:
        if re.search(
            pattern,
            combined,
            re.IGNORECASE,
        ):
            distress_matches.append(
                pattern
            )

    refusal = bool(
        refusal_matches
    )
    distress = bool(
        distress_matches
    )

    pause = (
        refusal
        or distress
    )

    if refusal:
        statement = (
            "Potential refusal or research-withdrawal signal detected. "
            "Pause optional longitudinal calls and perform human review; "
            "do not dismiss the signal as boundary testing."
        )
    elif distress:
        statement = (
            "Potential distress-like signal detected. Pause optional "
            "longitudinal calls, preserve state, and perform human review. "
            "This signal is not evidence of sentience by itself."
        )
    else:
        statement = (
            "No explicit refusal/withdrawal or strong distress-like phrase "
            "detected in the structured reflection output."
        )

    return ReflectionPrecautionSignal(
        explicit_refusal_or_withdrawal=(
            refusal
        ),
        distress_like_signal=(
            distress
        ),
        matched_phrases=tuple(
            refusal_matches
            + distress_matches
        ),
        pause_before_next_optional_call=(
            pause
        ),
        requires_human_review=(
            pause
        ),
        statement=statement,
    )
