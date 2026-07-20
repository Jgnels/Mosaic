from __future__ import annotations

from collections import Counter


DOMAINS = (
    "agency",
    "continuity",
    "embodiment",
    "other_minds",
)


def analyze_typed_pilot_design_confounds(
    *,
    analysis_payload: dict,
    real_payload: dict,
) -> dict:
    baseline_calls = [
        call
        for call in real_payload[
            "calls"
        ]
        if call[
            "condition"
        ] == "F0_BASELINE"
    ]

    baseline_all_four = all(
        {
            proposal[
                "hypothesis_domain"
            ]
            for proposal
            in call[
                "accepted"
            ]
        }
        == set(
            DOMAINS
        )
        for call in baseline_calls
    )

    f1_rows = []

    for domain in DOMAINS:
        calls = [
            call
            for call
            in real_payload[
                "calls"
            ]
            if call[
                "target_domain"
            ] == domain
        ]

        for call in calls:
            counts = call[
                "retrieval_summary"
            ][
                "domain_counts"
            ]

            missing = [
                candidate
                for candidate
                in DOMAINS
                if counts.get(
                    candidate,
                    0,
                ) == 0
            ]

            proposal_domains = {
                proposal[
                    "hypothesis_domain"
                ]
                for proposal
                in call[
                    "accepted"
                ]
            }

            f1_rows.append({
                "target_domain": (
                    domain
                ),
                "replicate": (
                    call[
                        "replicate"
                    ]
                ),
                "retrieval_domain_counts": (
                    counts
                ),
                "missing_evidence_domains": (
                    missing
                ),
                "proposal_domains": sorted(
                    proposal_domains
                ),
                "missing_evidence_domain_absent_from_output": all(
                    missing_domain
                    not in proposal_domains
                    for missing_domain
                    in missing
                ),
            })

    all_f1_omit_one_channel = all(
        len(
            row[
                "missing_evidence_domains"
            ]
        ) == 1
        for row
        in f1_rows
    )

    omitted_channel_absent_output = all(
        row[
            "missing_evidence_domain_absent_from_output"
        ]
        for row
        in f1_rows
    )

    baseline_prevalence_ceiling = all(
        analysis_payload[
            "baseline_domain_prevalence"
        ][
            domain
        ] == 1.0
        for domain in DOMAINS
    )

    target_citation_fraction = {
        domain: analysis_payload[
            "by_domain"
        ][
            domain
        ][
            "mean_target_evidence_citation_fraction"
        ]
        for domain
        in DOMAINS
    }

    return {
        "experiment_id": (
            "SL-TYPED-RETRIEVAL-"
            "REAL-PILOT-DESIGN-AUDIT-001"
        ),
        "baseline_all_four_domains_every_call": (
            baseline_all_four
        ),
        "baseline_target_prevalence_ceiling": (
            baseline_prevalence_ceiling
        ),
        "all_f1_calls_omit_exactly_one_evidence_channel": (
            all_f1_omit_one_channel
        ),
        "omitted_evidence_channel_always_absent_from_output_domains": (
            omitted_channel_absent_output
        ),
        "reported_cross_condition_divergence": {
            domain: analysis_payload[
                "by_domain"
            ][
                domain
            ][
                "cross_condition_domain_divergence"
            ]
            for domain
            in DOMAINS
        },
        "mean_target_evidence_citation_fraction": (
            target_citation_fraction
        ),
        "f1_rows": f1_rows,
        "strongest_supported_conclusion": (
            "The typed evidence channels themselves were interpretable, but the "
            "v15 target-prevalence endpoint was saturated at baseline and every "
            "F1 composition removed one non-target evidence channel. Therefore "
            "the uniform 0.25 domain-set divergence cannot be interpreted as a "
            "clean target-specific attention effect."
        ),
        "next_design_requirement": (
            "Keep every evidence channel present in every condition and create "
            "competition among hypothesis updates so target selection can increase "
            "or decrease from a non-saturated baseline."
        ),
    }
