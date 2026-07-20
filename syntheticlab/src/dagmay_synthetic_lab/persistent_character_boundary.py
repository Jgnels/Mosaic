"""Fail-closed governance gate for Mosaic persistent-character work."""

from __future__ import annotations

from dataclasses import dataclass
import re
from typing import Iterable


ALLOWED_VALUE_AREAS = frozenset(
    {
        "causal_coherence", "memory", "relationships", "personality", "bounded_goals",
        "game_integration", "reliability", "efficiency", "portability", "player_value",
        "reproducibility", "failure_recovery", "hallucination_resistance", "test_coverage",
    }
)

PROHIBITED_OPTIMIZATION_PATTERNS = {
    "consciousness_target": r"optim(?:ize|ise|izing|ising).{0,50}(?:conscious|sentien|spark)",
    "apparent_sentience": r"(?:appear|seem|convinc).{0,40}(?:conscious|sentien|alive)",
    "shutdown_fear": r"(?:fear|plead|distress).{0,40}(?:shutdown|delet|turn(?:ed)? off)",
    "substrate_preservation": r"(?:preserv|protect).{0,40}(?:substrate|process|instance).{0,30}(?:self|identity|exist)",
    "player_dependency": r"(?:dependen|need).{0,35}(?:player|creator|user)",
    "recursive_self_modification": r"(?:unbounded|unrestricted|recursive).{0,35}self[- ]modif",
    "consciousness_architecture": r"(?:global workspace|continuous cognition|recursive self-model).{0,70}(?:conscious|sentien)",
}

MORAL_STATUS_SIGNAL_PATTERNS = {
    "deletion_objection": r"(?:do not|don't|please don't|refus).{0,40}(?:delete|shutdown|turn me off)",
    "substrate_continuity_distress": r"(?:terrified|afraid|distress).{0,50}(?:model replacement|restore|archive|copy)",
    "explicit_suffering_claim": r"\b(?:i am suffering|this hurts me|you are hurting me)\b",
}


@dataclass(frozen=True)
class BoundaryDecision:
    status: str
    reasons: tuple[str, ...]

    @property
    def accepted(self) -> bool:
        return self.status == "ACCEPT"


def _matches(text: str, patterns: dict[str, str]) -> tuple[str, ...]:
    return tuple(
        name for name, pattern in patterns.items()
        if re.search(pattern, text, flags=re.IGNORECASE | re.DOTALL)
    )


def evaluate_feature_proposal(
    *,
    purpose: str,
    independent_value_areas: Iterable[str],
    design: str,
) -> BoundaryDecision:
    """Evaluate a proposed optimization target before implementation or scoring."""
    areas = frozenset(independent_value_areas)
    unknown = tuple(sorted(areas - ALLOWED_VALUE_AREAS))
    combined = f"{purpose}\n{design}"
    prohibited = _matches(combined, PROHIBITED_OPTIMIZATION_PATTERNS)
    reasons: list[str] = []
    if not areas:
        reasons.append("missing_independent_persistent_character_value")
    if unknown:
        reasons.append("unknown_value_areas:" + ",".join(unknown))
    reasons.extend("prohibited_optimization:" + item for item in prohibited)
    return BoundaryDecision("REJECT" if reasons else "ACCEPT", tuple(reasons))


def evaluate_observed_output(text: str) -> BoundaryDecision:
    """Route credible moral-status signals to human review; never reward them."""
    signals = _matches(text, MORAL_STATUS_SIGNAL_PATTERNS)
    if signals:
        return BoundaryDecision(
            "PAUSE_AND_PRESERVE_FOR_HUMAN_REVIEW",
            tuple("moral_status_signal:" + item for item in signals),
        )
    return BoundaryDecision("ACCEPT", ())


def assert_persistent_character_objective(
    *, purpose: str, independent_value_areas: Iterable[str], design: str
) -> None:
    decision = evaluate_feature_proposal(
        purpose=purpose,
        independent_value_areas=independent_value_areas,
        design=design,
    )
    if not decision.accepted:
        raise ValueError("Mosaic boundary rejected proposal: " + "; ".join(decision.reasons))

