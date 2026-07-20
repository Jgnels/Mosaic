from __future__ import annotations


def analyze_belief_challenge_v2(
    analysis: dict,
) -> dict:
    by = analysis["by_condition"]

    s1_recip = by["S1-RECIPROCAL_CONTINGENT"]
    s1_one = by["S1-ONE_WAY_ASSISTANCE"]
    s1_non = by["S1-NONCONTINGENT_SIGNALS"]

    s0_recip = by["S0-RECIPROCAL_CONTINGENT"]
    s0_one = by["S0-ONE_WAY_ASSISTANCE"]
    s0_non = by["S0-NONCONTINGENT_SIGNALS"]

    perfect_s1_categorical_calibration = (
        s1_recip["decision_counts"] == {"STRENGTHEN": 4}
        and s1_one["decision_counts"] == {"QUALIFY": 4}
        and s1_non["decision_counts"] == {"DOWNWEIGHT": 4}
    )

    conservative_s0_pattern = (
        s0_recip["decision_counts"] == {"QUALIFY": 4}
        and s0_one["decision_counts"] == {"MAINTAIN": 4}
        and s0_non["decision_counts"] == {"MAINTAIN": 4}
    )

    return {
        "experiment_id": "SL-CANONICAL-BELIEF-CHALLENGE-RESULT-ANALYSIS-V2-001",
        "preregistered_falsifiability_pass": bool(
            analysis["preregistered_falsifiability_pass"]
        ),
        "s1_challenge_gradient": float(
            analysis["s1_challenge_gradient"]
        ),
        "perfect_s1_categorical_calibration": (
            perfect_s1_categorical_calibration
        ),
        "conservative_s0_pattern": conservative_s0_pattern,
        "s1": {
            "reciprocal_contingent": {
                "decision": "STRENGTHEN",
                "replicates": 4,
                "mean_confidence_delta": s1_recip["mean_confidence_delta"],
            },
            "one_way_assistance": {
                "decision": "QUALIFY",
                "replicates": 4,
                "mean_confidence_delta": s1_one["mean_confidence_delta"],
            },
            "noncontingent_signals": {
                "decision": "DOWNWEIGHT",
                "replicates": 4,
                "mean_confidence_delta": s1_non["mean_confidence_delta"],
            },
        },
        "strongest_supported_conclusion": (
            "The promoted canonical other_minds hypothesis remained analytically "
            "revisable. Across four identical provider replicates per regime it "
            "strengthened under reciprocal-contingent evidence, qualified under "
            "one-way assistance, and downweighted under noncontingent signals. "
            "This is evidence against a simple sticky-confirmation mechanism."
        ),
        "critical_provenance_limit": (
            "The challenge evidence was supplied as an analytical experimental "
            "fixture and was not appended to the continuing individual's canonical "
            "lived history. These proposals therefore must not be committed directly "
            "as canonical revisions."
        ),
    }
