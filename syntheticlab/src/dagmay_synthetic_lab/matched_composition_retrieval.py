from __future__ import annotations

from .typed_evidence_retrieval import (
    TypedEvidenceUnit,
)


DOMAINS = (
    "agency",
    "continuity",
    "embodiment",
    "other_minds",
)


def retrieve_matched_composition(
    *,
    fabric: dict[
        str,
        tuple[
            TypedEvidenceUnit,
            ...,
        ],
    ],
    target_domain: str | None,
) -> tuple[
    TypedEvidenceUnit,
    ...,
]:
    """Return exactly 12 evidence units while keeping all four channels present.

    Baseline:
        3 / 3 / 3 / 3

    Targeted:
        target = 6
        each non-target = 2

    This removes the v15 confound where targeting one domain completely removed a
    different domain from the evidence set.
    """

    if target_domain is not None and target_domain not in DOMAINS:
        raise ValueError(
            "unsupported target domain"
        )

    quotas = {
        domain: (
            3
            if target_domain is None
            else (
                6
                if domain == target_domain
                else 2
            )
        )
        for domain in DOMAINS
    }

    selected = []

    for domain in DOMAINS:
        available = fabric[
            domain
        ]
        quota = quotas[
            domain
        ]

        if len(
            available
        ) < quota:
            raise ValueError(
                f"insufficient {domain} evidence for matched composition"
            )

        selected.extend(
            available[
                :quota
            ]
        )

    if len(
        selected
    ) != 12:
        raise AssertionError(
            "matched composition must contain exactly 12 evidence units"
        )

    return tuple(
        selected
    )


def composition_counts(
    units: tuple[
        TypedEvidenceUnit,
        ...,
    ],
) -> dict[
    str,
    int,
]:
    return {
        domain: sum(
            1
            for unit
            in units
            if unit.domain == domain
        )
        for domain in DOMAINS
    }
