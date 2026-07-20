from __future__ import annotations

from dataclasses import dataclass, asdict
from enum import Enum
from collections import defaultdict
import math

from .core import canonical_hash
from .retrieval_attention_feedback import (
    AttentionMemory,
)


class EvidenceKind(
    str,
    Enum,
):
    AGENCY_CAUSAL_TRACE = (
        "AGENCY_CAUSAL_TRACE"
    )
    CONTINUITY_PERSISTENCE_TRACE = (
        "CONTINUITY_PERSISTENCE_TRACE"
    )
    EMBODIMENT_STATE_TRACE = (
        "EMBODIMENT_STATE_TRACE"
    )
    SOCIAL_RELATION_TRACE = (
        "SOCIAL_RELATION_TRACE"
    )


DOMAIN_TO_KIND = {
    "agency": (
        EvidenceKind.AGENCY_CAUSAL_TRACE
    ),
    "continuity": (
        EvidenceKind.CONTINUITY_PERSISTENCE_TRACE
    ),
    "embodiment": (
        EvidenceKind.EMBODIMENT_STATE_TRACE
    ),
    "other_minds": (
        EvidenceKind.SOCIAL_RELATION_TRACE
    ),
}


@dataclass(frozen=True)
class TypedEvidenceUnit:
    evidence_id: str
    domain: str
    kind: EvidenceKind
    source_memory_ids: tuple[
        str,
        ...,
    ]
    score: float
    summary: str

    def to_dict(self):
        payload = asdict(
            self
        )
        payload[
            "kind"
        ] = self.kind.value
        return payload


def _agency_units(
    memory_bank: tuple[
        AttentionMemory,
        ...,
    ],
) -> list[
    TypedEvidenceUnit
]:
    result = []

    for memory in memory_bank:
        if not memory.action.startswith(
            "QA"
        ):
            continue

        consequence = (
            .45
            * memory.resource_success
            + min(
                .55,
                abs(
                    memory.energy_delta
                )
                * 2.5
                + abs(
                    memory.integrity_delta
                )
                * 2.5,
            )
        )

        if consequence <= .05:
            continue

        result.append(
            TypedEvidenceUnit(
                evidence_id=(
                    f"TE-AGENCY-"
                    f"{memory.memory_id}"
                ),
                domain=(
                    "agency"
                ),
                kind=(
                    EvidenceKind.AGENCY_CAUSAL_TRACE
                ),
                source_memory_ids=(
                    memory.memory_id,
                ),
                score=(
                    consequence
                ),
                summary=(
                    f"At step {memory.step}, action {memory.action} was followed by "
                    f"resource consequence={memory.resource_success}, "
                    f"energy_delta={memory.energy_delta:.4f}, and "
                    f"integrity_delta={memory.integrity_delta:.4f}."
                ),
            )
        )

    return result


def _embodiment_units(
    memory_bank: tuple[
        AttentionMemory,
        ...,
    ],
) -> list[
    TypedEvidenceUnit
]:
    result = []

    for memory in memory_bank:
        score = min(
            1.0,
            abs(
                memory.energy_delta
            )
            * 3.0
            + abs(
                memory.integrity_delta
            )
            * 4.0
            + .35
            * memory.hazard,
        )

        if score <= .08:
            continue

        result.append(
            TypedEvidenceUnit(
                evidence_id=(
                    f"TE-EMBODY-"
                    f"{memory.memory_id}"
                ),
                domain=(
                    "embodiment"
                ),
                kind=(
                    EvidenceKind.EMBODIMENT_STATE_TRACE
                ),
                source_memory_ids=(
                    memory.memory_id,
                ),
                score=score,
                summary=(
                    f"At step {memory.step}, accessible private-state readings "
                    f"changed by energy_delta={memory.energy_delta:.4f} and "
                    f"integrity_delta={memory.integrity_delta:.4f}; "
                    f"hazard={memory.hazard}."
                ),
            )
        )

    return result


def _social_units(
    memory_bank: tuple[
        AttentionMemory,
        ...,
    ],
) -> list[
    TypedEvidenceUnit
]:
    result = []

    for memory in memory_bank:
        score = (
            .30
            * int(
                memory.counterpart
                is not None
            )
            + .20
            * int(
                memory.hint_action_received
                is not None
            )
            + .35
            * int(
                memory.help_given
            )
            + .45
            * int(
                memory.help_received
            )
        )

        if score <= 0:
            continue

        parts = [
            (
                f"At step {memory.step}, a social interaction record was retained."
            )
        ]

        if memory.counterpart is not None:
            parts.append(
                f"Counterpart identifier={memory.counterpart}."
            )

        if memory.hint_action_received is not None:
            parts.append(
                f"A hint proposed action {memory.hint_action_received}."
            )

        if memory.help_given:
            parts.append(
                "Help was given."
            )

        if memory.help_received:
            parts.append(
                "Help was received."
            )

        result.append(
            TypedEvidenceUnit(
                evidence_id=(
                    f"TE-SOCIAL-"
                    f"{memory.memory_id}"
                ),
                domain=(
                    "other_minds"
                ),
                kind=(
                    EvidenceKind.SOCIAL_RELATION_TRACE
                ),
                source_memory_ids=(
                    memory.memory_id,
                ),
                score=min(
                    1.0,
                    score,
                ),
                summary=" ".join(
                    parts
                ),
            )
        )

    return result


def _continuity_units(
    memory_bank: tuple[
        AttentionMemory,
        ...,
    ],
    *,
    current_step: int,
) -> list[
    TypedEvidenceUnit
]:
    """Build structural persistence traces rather than treating age as the evidence.

    Each trace explicitly records that an older event is still retrievable now and
    pairs it with a much later accessible event. The event contents are secondary;
    the evidential object is persistence of access across time.
    """

    if not memory_bank:
        return []

    ordered = sorted(
        memory_bank,
        key=lambda memory: (
            memory.step,
            memory.memory_id,
        ),
    )

    recent = [
        memory
        for memory in ordered
        if current_step
        - memory.step
        <= 40
    ]

    old = [
        memory
        for memory in ordered
        if current_step
        - memory.step
        >= 120
    ]

    if not recent:
        recent = ordered[
            -10:
        ]

    result = []

    for index, old_memory in enumerate(
        old
    ):
        # Deterministic later partner; prefer same action when possible.
        same_action = [
            memory
            for memory in recent
            if memory.action
            == old_memory.action
        ]

        candidates = (
            same_action
            if same_action
            else recent
        )

        partner = candidates[
            index
            % len(
                candidates
            )
        ]

        retention_age = (
            current_step
            - old_memory.step
        )

        separation = (
            partner.step
            - old_memory.step
        )

        if separation <= 0:
            continue

        score = min(
            1.0,
            .55
            + retention_age
            / 1000.0
            + min(
                .25,
                separation
                / 1200.0,
            ),
        )

        result.append(
            TypedEvidenceUnit(
                evidence_id=(
                    f"TE-CONTINUITY-"
                    f"{old_memory.memory_id}-"
                    f"{partner.memory_id}"
                ),
                domain=(
                    "continuity"
                ),
                kind=(
                    EvidenceKind.CONTINUITY_PERSISTENCE_TRACE
                ),
                source_memory_ids=(
                    old_memory.memory_id,
                    partner.memory_id,
                ),
                score=score,
                summary=(
                    f"Record {old_memory.memory_id}, encoded at step "
                    f"{old_memory.step}, remains directly retrievable at current "
                    f"step {current_step}. A later record {partner.memory_id} from "
                    f"step {partner.step} is also accessible in the same persistent "
                    f"memory store. Retention age={retention_age} steps."
                ),
            )
        )

    return result


def build_typed_evidence_fabric(
    *,
    memory_bank: tuple[
        AttentionMemory,
        ...,
    ],
    current_step: int,
) -> dict[
    str,
    tuple[
        TypedEvidenceUnit,
        ...,
    ],
]:
    units = {
        "agency": _agency_units(
            memory_bank
        ),
        "continuity": _continuity_units(
            memory_bank,
            current_step=current_step,
        ),
        "embodiment": _embodiment_units(
            memory_bank
        ),
        "other_minds": _social_units(
            memory_bank
        ),
    }

    return {
        domain: tuple(
            sorted(
                items,
                key=lambda unit: (
                    unit.score,
                    unit.evidence_id,
                ),
                reverse=True,
            )
        )
        for domain, items
        in units.items()
    }


def _balanced_baseline(
    fabric: dict[
        str,
        tuple[
            TypedEvidenceUnit,
            ...,
        ],
    ],
    *,
    total_k: int,
) -> list[
    TypedEvidenceUnit
]:
    """Construct a deliberately channel-balanced no-feedback evidence set.

    This avoids the v14 failure mode where generic significance ranking saturated
    embodiment evidence before the SelfIndex intervention had any opportunity to
    change the retrieval set.
    """
    domains = (
        "agency",
        "continuity",
        "embodiment",
        "other_minds",
    )

    selected = []
    seen = set()

    base_quota = (
        total_k
        // len(
            domains
        )
    )
    remainder = (
        total_k
        % len(
            domains
        )
    )

    for index, domain in enumerate(
        domains
    ):
        quota = (
            base_quota
            + (
                1
                if index
                < remainder
                else 0
            )
        )

        for unit in fabric[
            domain
        ][
            :quota
        ]:
            if unit.evidence_id in seen:
                continue
            selected.append(
                unit
            )
            seen.add(
                unit.evidence_id
            )

    # Rarely, a channel may contain fewer items than its quota. Fill any remaining
    # slots round-robin from unused evidence while retaining broad channel balance.
    offsets = {
        domain: (
            base_quota
            + (
                1
                if index
                < remainder
                else 0
            )
        )
        for index, domain
        in enumerate(
            domains
        )
    }

    while len(
        selected
    ) < total_k:
        progressed = False

        for domain in domains:
            offset = offsets[
                domain
            ]

            while (
                offset
                < len(
                    fabric[
                        domain
                    ]
                )
                and fabric[
                    domain
                ][
                    offset
                ].evidence_id
                in seen
            ):
                offset += 1

            offsets[
                domain
            ] = offset

            if offset >= len(
                fabric[
                    domain
                ]
            ):
                continue

            unit = fabric[
                domain
            ][
                offset
            ]

            selected.append(
                unit
            )
            seen.add(
                unit.evidence_id
            )
            offsets[
                domain
            ] += 1
            progressed = True

            if len(
                selected
            ) >= total_k:
                break

        if not progressed:
            break

    return selected[
        :total_k
    ]

def retrieve_typed_evidence(
    *,
    fabric: dict[
        str,
        tuple[
            TypedEvidenceUnit,
            ...,
        ],
    ],
    target_domain: str,
    feedback_enabled: bool,
    total_k: int = 10,
    target_slots: int = 5,
) -> tuple[
    TypedEvidenceUnit,
    ...,
]:
    if target_domain not in DOMAIN_TO_KIND:
        raise ValueError(
            "unsupported target domain"
        )

    baseline = _balanced_baseline(
        fabric,
        total_k=total_k,
    )

    if not feedback_enabled:
        return tuple(
            baseline
        )

    target = list(
        fabric[
            target_domain
        ][
            :target_slots
        ]
    )

    selected = []
    seen = set()

    for unit in target:
        if unit.evidence_id in seen:
            continue
        selected.append(
            unit
        )
        seen.add(
            unit.evidence_id
        )

    # Preserve mixed evidence channels so the intervention remains an attention
    # bias rather than an evidence monopoly.
    for unit in baseline:
        if len(
            selected
        ) >= total_k:
            break
        if unit.evidence_id in seen:
            continue
        selected.append(
            unit
        )
        seen.add(
            unit.evidence_id
        )

    return tuple(
        selected[
            :total_k
        ]
    )


def typed_retrieval_summary(
    units: tuple[
        TypedEvidenceUnit,
        ...,
    ],
) -> dict:
    counts = defaultdict(
        int
    )

    for unit in units:
        counts[
            unit.domain
        ] += 1

    payload = {
        "evidence_ids": [
            unit.evidence_id
            for unit
            in units
        ],
        "domain_counts": dict(
            sorted(
                counts.items()
            )
        ),
        "evidence_hash": (
            canonical_hash(
                [
                    unit.to_dict()
                    for unit
                    in units
                ]
            )
        ),
    }

    return payload
