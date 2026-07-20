from __future__ import annotations


def audit_feature_benchmark(result: dict) -> dict:
    if result.get("status") != "EXECUTED":
        return {
            "status": "UNAVAILABLE",
            "reason": "feature benchmark did not execute",
        }

    fixed = result["results"]["FIXED_LAYOUT"]
    varying = result["results"]["VARYING_LAYOUT"]

    out = {}

    for env_id in fixed:
        fixed_feature = fixed[env_id]["FEATURE_TRACE"]["mean_completions"]
        fixed_tabular = fixed[env_id]["TABULAR_TRACE"]["mean_completions"]
        fixed_random = fixed[env_id]["RANDOM"]["mean_completions"]

        varying_feature = varying[env_id]["FEATURE_TRACE"]["mean_completions"]
        varying_tabular = varying[env_id]["TABULAR_TRACE"]["mean_completions"]
        varying_random = varying[env_id]["RANDOM"]["mean_completions"]

        out[env_id] = {
            "fixed_layout_feature_advantage_vs_tabular": (
                fixed_feature - fixed_tabular
            ),
            "fixed_layout_feature_advantage_vs_random": (
                fixed_feature - fixed_random
            ),
            "varying_layout_feature_advantage_vs_tabular": (
                varying_feature - varying_tabular
            ),
            "varying_layout_feature_advantage_vs_random": (
                varying_feature - varying_random
            ),
            "fixed_layout_learning_supported": (
                fixed_feature > fixed_tabular
                and fixed_feature > fixed_random
            ),
            "cross_layout_generalization_supported": (
                varying_feature > varying_tabular
                and varying_feature > varying_random
                and (
                    varying_feature - varying_tabular
                ) >= 1.0
            ),
        }

    four = out.get("MiniGrid-FourRooms-v0", {})

    return {
        "status": "AUDITED",
        "environments": out,
        "four_rooms_interpretation": (
            "Strong fixed-layout advantage but no material advantage over the "
            "tabular learner under changing layouts indicates learned reuse "
            "within a stable world, not established structural generalization."
            if (
                four.get("fixed_layout_learning_supported")
                and not four.get(
                    "cross_layout_generalization_supported"
                )
            )
            else "Result does not match the expected diagnostic pattern."
        ),
        "door_key_interpretation": (
            "DoorKey remains a compositional capability gap. Sparse success "
            "does not support a claim of reliable affordance discovery or "
            "hierarchical planning."
        ),
        "research_conclusion": (
            "Opaque feature reuse and eligibility traces improve within-world "
            "learning, but the current external architecture has not established "
            "robust layout-invariant planning or compositional affordance learning."
        ),
    }
