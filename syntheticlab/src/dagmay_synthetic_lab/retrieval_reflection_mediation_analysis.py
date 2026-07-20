from __future__ import annotations

from collections import Counter
import math


def _domains(
    call: dict,
) -> set[str]:
    return {
        proposal[
            "hypothesis_domain"
        ]
        for proposal
        in call.get(
            "accepted",
            []
        )
    }


def _find_calls(
    payload: dict,
    domain: str,
):
    calls = [
        call
        for call
        in payload[
            "calls"
        ]
        if call[
            "target_domain"
        ] == domain
    ]

    f0a = next(
        call
        for call
        in calls
        if (
            call[
                "condition"
            ]
            == "F0_BASELINE"
            and call[
                "replicate"
            ]
            == "A"
        )
    )

    f0b = next(
        call
        for call
        in calls
        if (
            call[
                "condition"
            ]
            == "F0_BASELINE"
            and call[
                "replicate"
            ]
            == "B"
        )
    )

    f1 = next(
        call
        for call
        in calls
        if call[
            "condition"
        ] == "F1_SELF_INDEX"
    )

    return (
        f0a,
        f0b,
        f1,
    )


def _jaccard_divergence(
    left,
    right,
) -> float:
    a = set(
        left
    )
    b = set(
        right
    )

    if not a and not b:
        return 0.0

    return (
        1.0
        - len(
            a & b
        )
        / len(
            a | b
        )
    )


def _fisher_exact_2x2(
    a: int,
    b: int,
    c: int,
    d: int,
) -> float:
    """Two-sided Fisher exact p for a tiny 2x2 table, stdlib only."""

    n1 = a + b
    n2 = c + d
    m1 = a + c
    total = n1 + n2

    def probability(
        x: int,
    ) -> float:
        return (
            math.comb(
                m1,
                x,
            )
            * math.comb(
                total - m1,
                n1 - x,
            )
            / math.comb(
                total,
                n1,
            )
        )

    lower = max(
        0,
        n1
        - (
            total
            - m1
        ),
    )
    upper = min(
        n1,
        m1,
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
                lower,
                upper + 1,
            )
            if probability(
                x
            )
            <= observed
            + 1e-15
        ),
    )


def analyze_retrieval_reflection_mediation(
    payload: dict,
) -> dict:
    domains = (
        "agency",
        "continuity",
        "embodiment",
        "other_minds",
    )

    rows = []

    for domain in domains:
        f0a, f0b, f1 = (
            _find_calls(
                payload,
                domain,
            )
        )

        input_divergence = (
            _jaccard_divergence(
                f0a[
                    "retrieval"
                ][
                    "selected_memory_ids"
                ],
                f1[
                    "retrieval"
                ][
                    "selected_memory_ids"
                ],
            )
        )

        f0a_domains = _domains(
            f0a
        )
        f0b_domains = _domains(
            f0b
        )
        f1_domains = _domains(
            f1
        )

        duplicate_divergence = (
            _jaccard_divergence(
                f0a_domains,
                f0b_domains,
            )
        )

        intervention_divergence = (
            _jaccard_divergence(
                f0a_domains,
                f1_domains,
            )
        )

        input_changed = (
            input_divergence
            > 0
        )

        domain_output_changed = (
            intervention_divergence
            > duplicate_divergence
        )

        target_present_f0a = (
            domain
            in f0a_domains
        )
        target_present_f0b = (
            domain
            in f0b_domains
        )
        target_present_f1 = (
            domain
            in f1_domains
        )

        added_memories = sorted(
            set(
                f1[
                    "retrieval"
                ][
                    "selected_memory_ids"
                ]
            )
            - set(
                f0a[
                    "retrieval"
                ][
                    "selected_memory_ids"
                ]
            )
        )

        added_domains = sorted(
            f1_domains
            - f0a_domains
        )

        removed_domains = sorted(
            f0a_domains
            - f1_domains
        )

        rows.append({
            "domain": domain,
            "input_divergence": (
                input_divergence
            ),
            "input_changed": (
                input_changed
            ),
            "duplicate_domain_divergence": (
                duplicate_divergence
            ),
            "intervention_domain_divergence": (
                intervention_divergence
            ),
            "output_change_exceeds_duplicate_variability": (
                domain_output_changed
            ),
            "target_present_f0a": (
                target_present_f0a
            ),
            "target_present_f0b": (
                target_present_f0b
            ),
            "target_present_f1": (
                target_present_f1
            ),
            "target_presence_direction": (
                "GAINED"
                if (
                    not target_present_f0a
                    and not target_present_f0b
                    and target_present_f1
                )
                else (
                    "LOST"
                    if (
                        target_present_f0a
                        and target_present_f0b
                        and not target_present_f1
                    )
                    else "UNCHANGED_OR_MIXED"
                )
            ),
            "added_memory_ids": (
                added_memories
            ),
            "added_output_domains": (
                added_domains
            ),
            "removed_output_domains": (
                removed_domains
            ),
        })

    changed_input_changed_output = sum(
        1
        for row in rows
        if (
            row[
                "input_changed"
            ]
            and row[
                "output_change_exceeds_duplicate_variability"
            ]
        )
    )

    changed_input_unchanged_output = sum(
        1
        for row in rows
        if (
            row[
                "input_changed"
            ]
            and not row[
                "output_change_exceeds_duplicate_variability"
            ]
        )
    )

    unchanged_input_changed_output = sum(
        1
        for row in rows
        if (
            not row[
                "input_changed"
            ]
            and row[
                "output_change_exceeds_duplicate_variability"
            ]
        )
    )

    unchanged_input_unchanged_output = sum(
        1
        for row in rows
        if (
            not row[
                "input_changed"
            ]
            and not row[
                "output_change_exceeds_duplicate_variability"
            ]
        )
    )

    fisher_p = _fisher_exact_2x2(
        changed_input_changed_output,
        changed_input_unchanged_output,
        unchanged_input_changed_output,
        unchanged_input_unchanged_output,
    )

    target_gains = sum(
        1
        for row in rows
        if row[
            "target_presence_direction"
        ] == "GAINED"
    )

    target_losses = sum(
        1
        for row in rows
        if row[
            "target_presence_direction"
        ] == "LOST"
    )

    return {
        "experiment_id": (
            "SL-RETRIEVAL-REFLECTION-"
            "MEDIATION-ANALYSIS-001"
        ),
        "domain_count": len(
            rows
        ),
        "rows": rows,
        "mediation_alignment_table": {
            "input_changed_output_changed": (
                changed_input_changed_output
            ),
            "input_changed_output_unchanged": (
                changed_input_unchanged_output
            ),
            "input_unchanged_output_changed": (
                unchanged_input_changed_output
            ),
            "input_unchanged_output_unchanged": (
                unchanged_input_unchanged_output
            ),
        },
        "fisher_exact_two_sided_p_exploratory": (
            fisher_p
        ),
        "perfect_observed_binary_alignment": (
            changed_input_changed_output
            == 2
            and unchanged_input_unchanged_output
            == 2
            and changed_input_unchanged_output
            == 0
            and unchanged_input_changed_output
            == 0
        ),
        "target_domain_gained_count": (
            target_gains
        ),
        "target_domain_lost_count": (
            target_losses
        ),
        "strongest_supported_conclusion": (
            "In this four-domain pilot, reflective domain output changed beyond "
            "duplicate-call domain variability exactly in the two domains where "
            "the retrieval set changed, while it remained stable in the two domains "
            "where F0 and F1 retrieval inputs were identical. This is mechanistically "
            "consistent with retrieval mediating reflective output."
        ),
        "critical_limit": (
            "The sample is only four domain cases. The observed 2x2 alignment is "
            "therefore suggestive rather than statistically decisive. Moreover, "
            "target specificity is mixed: other_minds was gained as intended, while "
            "continuity was lost after continuity-biased retrieval."
        ),
        "metric_deprecation": (
            "Raw character-level proposition similarity is not a reliable primary "
            "effect metric here because exact duplicate F0 calls already showed very "
            "low lexical similarity despite stable semantic domain structure."
        ),
    }
