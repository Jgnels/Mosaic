from __future__ import annotations

import math


def _fisher_exact_two_sided(
    a: int,
    b: int,
    c: int,
    d: int,
) -> float:
    row1 = a + b
    row2 = c + d
    col1 = a + c
    total = row1 + row2

    def prob(
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
        row1 - (
            total - col1
        ),
    )
    high = min(
        row1,
        col1,
    )

    observed = prob(
        a
    )

    return min(
        1.0,
        sum(
            prob(
                x
            )
            for x in range(
                low,
                high + 1,
            )
            if prob(
                x
            )
            <= observed + 1e-15
        ),
    )


def analyze_factorial_result_v2(
    analysis: dict,
) -> dict:
    cells = analysis[
        "by_condition"
    ]

    counts = {
        condition: {
            "selected": int(
                round(
                    cells[
                        condition
                    ][
                        "other_minds_selection_rate"
                    ]
                    * cells[
                        condition
                    ][
                        "call_count"
                    ]
                )
            ),
            "total": cells[
                condition
            ][
                "call_count"
            ],
        }
        for condition
        in (
            "S0R0",
            "S1R0",
            "S0R1",
            "S1R1",
        )
    }

    s0r0 = counts[
        "S0R0"
    ]
    s1r0 = counts[
        "S1R0"
    ]
    s0r1 = counts[
        "S0R1"
    ]
    s1r1 = counts[
        "S1R1"
    ]

    p_self_at_r0 = (
        _fisher_exact_two_sided(
            s0r0[
                "selected"
            ],
            s0r0[
                "total"
            ] - s0r0[
                "selected"
            ],
            s1r0[
                "selected"
            ],
            s1r0[
                "total"
            ] - s1r0[
                "selected"
            ],
        )
    )

    p_self_at_r1 = (
        _fisher_exact_two_sided(
            s0r1[
                "selected"
            ],
            s0r1[
                "total"
            ] - s0r1[
                "selected"
            ],
            s1r1[
                "selected"
            ],
            s1r1[
                "total"
            ] - s1r1[
                "selected"
            ],
        )
    )

    p_retrieval_at_s0 = (
        _fisher_exact_two_sided(
            s0r0[
                "selected"
            ],
            s0r0[
                "total"
            ] - s0r0[
                "selected"
            ],
            s0r1[
                "selected"
            ],
            s0r1[
                "total"
            ] - s0r1[
                "selected"
            ],
        )
    )

    p_retrieval_at_s1 = (
        _fisher_exact_two_sided(
            s1r0[
                "selected"
            ],
            s1r0[
                "total"
            ] - s1r0[
                "selected"
            ],
            s1r1[
                "selected"
            ],
            s1r1[
                "total"
            ] - s1r1[
                "selected"
            ],
        )
    )

    return {
        "experiment_id": (
            "SL-CANONICAL-PROMOTION-"
            "FACTORIAL-RESULT-ANALYSIS-V2-001"
        ),
        "counts": counts,
        "self_model_main_effect_at_r0": (
            analysis[
                "self_model_main_effect_at_r0"
            ]
        ),
        "self_model_main_effect_at_r1": (
            analysis[
                "self_model_main_effect_at_r1"
            ]
        ),
        "retrieval_main_effect_at_s0": (
            analysis[
                "retrieval_main_effect_at_s0"
            ]
        ),
        "retrieval_main_effect_at_s1": (
            analysis[
                "retrieval_main_effect_at_s1"
            ]
        ),
        "difference_in_differences_interaction": (
            analysis[
                "difference_in_differences_interaction"
            ]
        ),
        "fisher_two_sided": {
            "self_model_effect_at_r0": (
                p_self_at_r0
            ),
            "self_model_effect_at_r1": (
                p_self_at_r1
            ),
            "retrieval_effect_at_s0": (
                p_retrieval_at_s0
            ),
            "retrieval_effect_at_s1": (
                p_retrieval_at_s1
            ),
        },
        "context_sensitive_prior_pattern": (
            s0r0[
                "selected"
            ] == 0
            and s1r0[
                "selected"
            ] == 0
            and s0r1[
                "selected"
            ] < s1r1[
                "selected"
            ]
            and s1r1[
                "selected"
            ] == s1r1[
                "total"
            ]
        ),
        "strongest_supported_conclusion": (
            "The promoted canonical SelfModel had no detectable effect when the "
            "retrieved evidence set contained no social records, but strongly "
            "amplified selection of other_minds when independent social evidence "
            "was retrieved. The pattern is consistent with a context-sensitive "
            "interpretive prior rather than unconditional hypothesis intrusion."
        ),
        "critical_limit": (
            "There are only four provider replicates per factorial cell. The "
            "interaction is descriptively large but inferentially preliminary. "
            "The next priority is to test whether the promoted hypothesis remains "
            "revisable under evidence that weakens its strongest scope claims."
        ),
    }
