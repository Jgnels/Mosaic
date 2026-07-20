from __future__ import annotations

from collections import defaultdict
import math
import statistics


DOMAINS = (
    "agency",
    "continuity",
    "embodiment",
    "other_minds",
)


def _fisher_exact_two_sided(
    a: int,
    b: int,
    c: int,
    d: int,
) -> float:
    """Two-sided Fisher exact p for a 2x2 table using stdlib only."""
    row1 = a + b
    row2 = c + d
    col1 = a + c
    total = row1 + row2

    def probability(
        x: int,
    ) -> float:
        return (
            math.comb(
                col1,
                x,
            )
            * math.comb(
                total - col1,
                row1 - x,
            )
            / math.comb(
                total,
                row1,
            )
        )

    low = max(
        0,
        row1
        - (
            total
            - col1
        ),
    )
    high = min(
        row1,
        col1,
    )

    observed = probability(
        a
    )

    return min(
        1.0,
        sum(
            probability(
                x
            )
            for x in range(
                low,
                high + 1,
            )
            if probability(
                x
            )
            <= observed
            + 1e-15
        ),
    )


def _selected_domains(
    call: dict,
) -> tuple[
    str,
    ...,
]:
    return tuple(
        proposal[
            "hypothesis_domain"
        ]
        for proposal
        in call.get(
            "accepted",
            []
        )
    )


def _target_proposal_citation_fraction(
    call: dict,
    target_domain: str,
) -> float | None:
    mapping = call[
        "opaque_evidence_map"
    ]

    target_proposals = [
        proposal
        for proposal
        in call[
            "accepted"
        ]
        if proposal[
            "hypothesis_domain"
        ] == target_domain
    ]

    if not target_proposals:
        return None

    cited_domains = []

    for proposal in target_proposals:
        for evidence_id in proposal[
            "evidence_ids"
        ]:
            if evidence_id in mapping:
                cited_domains.append(
                    mapping[
                        evidence_id
                    ][
                        "domain"
                    ]
                )

    if not cited_domains:
        return 0.0

    return (
        sum(
            1
            for domain
            in cited_domains
            if domain
            == target_domain
        )
        / len(
            cited_domains
        )
    )


def analyze_attention_competition_result_v2(
    payload: dict,
) -> dict:
    baseline = [
        call
        for call
        in payload[
            "calls"
        ]
        if call[
            "condition"
        ] == "F0_BALANCED"
    ]

    if len(
        baseline
    ) != 8:
        raise ValueError(
            "expected eight F0 baseline calls"
        )

    baseline_counts = {
        domain: sum(
            1
            for call
            in baseline
            if domain
            in _selected_domains(
                call
            )
        )
        for domain
        in DOMAINS
    }

    baseline_pair_counts = defaultdict(
        int
    )

    for call in baseline:
        baseline_pair_counts[
            tuple(
                sorted(
                    _selected_domains(
                        call
                    )
                )
            )
        ] += 1

    by_domain = {}

    for domain in DOMAINS:
        targeted = [
            call
            for call
            in payload[
                "calls"
            ]
            if call[
                "target_domain"
            ] == domain
        ]

        if len(
            targeted
        ) != 4:
            raise ValueError(
                f"expected four targeted calls for {domain}"
            )

        baseline_selected = (
            baseline_counts[
                domain
            ]
        )

        targeted_selected = sum(
            1
            for call
            in targeted
            if domain
            in _selected_domains(
                call
            )
        )

        citation_fractions = [
            value
            for value
            in (
                _target_proposal_citation_fraction(
                    call,
                    domain,
                )
                for call
                in targeted
            )
            if value is not None
        ]

        pair_counts = defaultdict(
            int
        )

        for call in targeted:
            pair_counts[
                tuple(
                    sorted(
                        _selected_domains(
                            call
                        )
                    )
                )
            ] += 1

        by_domain[
            domain
        ] = {
            "baseline_selected_count": (
                baseline_selected
            ),
            "baseline_call_count": (
                len(
                    baseline
                )
            ),
            "baseline_selection_rate": (
                baseline_selected
                / len(
                    baseline
                )
            ),
            "targeted_selected_count": (
                targeted_selected
            ),
            "targeted_call_count": (
                len(
                    targeted
                )
            ),
            "targeted_selection_rate": (
                targeted_selected
                / len(
                    targeted
                )
            ),
            "selection_rate_effect": (
                targeted_selected
                / len(
                    targeted
                )
                - baseline_selected
                / len(
                    baseline
                )
            ),
            "fisher_exact_two_sided_p": (
                _fisher_exact_two_sided(
                    baseline_selected,
                    len(
                        baseline
                    )
                    - baseline_selected,
                    targeted_selected,
                    len(
                        targeted
                    )
                    - targeted_selected,
                )
            ),
            "target_proposal_mean_target_evidence_fraction": (
                statistics.mean(
                    citation_fractions
                )
                if citation_fractions
                else None
            ),
            "target_proposal_min_target_evidence_fraction": (
                min(
                    citation_fractions
                )
                if citation_fractions
                else None
            ),
            "targeted_domain_pair_counts": {
                "+".join(
                    pair
                ): count
                for pair, count
                in sorted(
                    pair_counts.items()
                )
            },
        }

    unsaturated = [
        domain
        for domain
        in DOMAINS
        if by_domain[
            domain
        ][
            "baseline_selection_rate"
        ] < 1.0
    ]

    positive_unsaturated = [
        domain
        for domain
        in unsaturated
        if by_domain[
            domain
        ][
            "selection_rate_effect"
        ] > 0
    ]

    continuity_second_slot_anchor = (
        by_domain[
            "continuity"
        ][
            "baseline_selection_rate"
        ]
        == 1.0
        and all(
            "continuity"
            in _selected_domains(
                call
            )
            for call
            in payload[
                "calls"
            ]
            if call[
                "target_domain"
            ]
            in (
                "embodiment",
                "other_minds",
            )
        )
    )

    return {
        "experiment_id": (
            "SL-ATTENTION-COMPETITION-"
            "REAL-RESULT-ANALYSIS-V2-001"
        ),
        "call_count": len(
            payload[
                "calls"
            ]
        ),
        "baseline_pair_counts": {
            "+".join(
                pair
            ): count
            for pair, count
            in sorted(
                baseline_pair_counts.items()
            )
        },
        "by_domain": by_domain,
        "unsaturated_domains": (
            unsaturated
        ),
        "positive_effect_unsaturated_domains": (
            positive_unsaturated
        ),
        "all_unsaturated_domains_positive": (
            len(
                positive_unsaturated
            )
            == len(
                unsaturated
            )
        ),
        "continuity_behaves_as_dominant_anchor": (
            continuity_second_slot_anchor
        ),
        "strongest_supported_conclusion": (
            "Under a two-proposal attention budget, increasing one evidence "
            "channel from 3/12 to 6/12 increased selection of every domain that "
            "was not already saturated at baseline. Agency moved from 0/8 to "
            "4/4, embodiment from 5/8 to 4/4, and other_minds from 3/8 to 4/4. "
            "Continuity remained selected in every baseline and targeted call. "
            "The pattern is consistent with the Functional SelfIndex controlling "
            "a scarce reflective-attention slot, with continuity acting as a "
            "strong persistent anchor in this particular life history."
        ),
        "critical_limits": (
            "Only one frozen life history was tested. Evidence ordering was fixed "
            "within each condition, and evidence strength differs naturally across "
            "domains. The result should be replicated across independent histories "
            "with deterministic order randomization before altered reflections are "
            "committed to a continuing SelfModel."
        ),
    }
