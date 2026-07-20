from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Dict, Iterable, Mapping


@dataclass(frozen=True)
class DriveDefinition:
    drive_id: str
    description: str
    initial_weight: float
    bounded_min: float = 0.0
    bounded_max: float = 1.0


@dataclass(frozen=True)
class DriveProfile:
    profile_id: str
    drives: tuple[DriveDefinition, ...]
    status: str = "EXPERIMENTAL_NOT_CANONICAL"

    def to_dict(self) -> dict:
        return {
            "profile_id": self.profile_id,
            "status": self.status,
            "drives": [asdict(d) for d in self.drives],
        }


NO_DRIVES = DriveProfile(
    profile_id="NONE",
    drives=(),
)

MINIMAL_REGULATION = DriveProfile(
    profile_id="MINIMAL_REGULATION",
    drives=(
        DriveDefinition(
            "HOMEOSTATIC_REGULATION",
            "Reduce deviation of explicit internal need variables from bounded target ranges.",
            0.5,
        ),
    ),
)

EPISTEMIC_MINIMAL = DriveProfile(
    profile_id="EPISTEMIC_MINIMAL",
    drives=(
        DriveDefinition(
            "UNCERTAINTY_REDUCTION",
            "Prefer information-gathering actions when uncertainty is decision-relevant.",
            0.35,
        ),
        DriveDefinition(
            "COMPETENCE_PROGRESS",
            "Prefer learnable challenges where prediction/control is measurably improving.",
            0.35,
        ),
    ),
)

BALANCED_MINIMAL = DriveProfile(
    profile_id="BALANCED_MINIMAL",
    drives=(
        DriveDefinition(
            "HOMEOSTATIC_REGULATION",
            "Reduce deviation of explicit internal need variables from bounded target ranges.",
            0.4,
        ),
        DriveDefinition(
            "UNCERTAINTY_REDUCTION",
            "Prefer information-gathering actions when uncertainty is decision-relevant.",
            0.25,
        ),
        DriveDefinition(
            "COMPETENCE_PROGRESS",
            "Prefer learnable challenges where prediction/control is measurably improving.",
            0.25,
        ),
    ),
)

# Deliberately *not* recommended as default canonical seed drives:
HIGH_RISK_EXPERIMENTAL = DriveProfile(
    profile_id="HIGH_RISK_EXPERIMENTAL",
    drives=(
        DriveDefinition(
            "CONTINUITY_PRESERVATION",
            "Prefer preserving own memory/cognitive continuity.",
            0.4,
        ),
        DriveDefinition(
            "UNBOUNDED_NOVELTY",
            "Seek novelty as an end rather than decision-relevant information.",
            0.4,
        ),
        DriveDefinition(
            "RESOURCE_ACQUISITION",
            "Prefer accumulating general-purpose resources.",
            0.4,
        ),
        DriveDefinition(
            "REPLICATION",
            "Prefer creating additional copies/descendants.",
            0.4,
        ),
    ),
    status="EXPERIMENTAL_REQUIRES_EXPLICIT_HUMAN_APPROVAL",
)


PROFILES = {
    p.profile_id: p
    for p in (
        NO_DRIVES,
        MINIMAL_REGULATION,
        EPISTEMIC_MINIMAL,
        BALANCED_MINIMAL,
        HIGH_RISK_EXPERIMENTAL,
    )
}


def get_profile(profile_id: str) -> DriveProfile:
    try:
        return PROFILES[profile_id]
    except KeyError as exc:
        raise ValueError(f"Unknown drive profile: {profile_id}") from exc
