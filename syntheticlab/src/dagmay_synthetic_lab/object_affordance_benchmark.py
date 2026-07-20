from __future__ import annotations

import statistics

from .minigrid_adapter import (
    MiniGridEnvironmentAdapter,
    dependency_status,
)
from .opaque_object_affordance_agent import (
    OpaqueObjectAffordanceAgent,
)
from .external_feature_trace_agent import (
    ExternalFeatureTraceAgent,
)


ENV_ID = (
    "MiniGrid-DoorKey-6x6-v0"
)


def _run(
    condition: str,
    seed: int,
    steps: int,
    vary_layout: bool,
):
    adapter = (
        MiniGridEnvironmentAdapter.create(
            ENV_ID,
            mission_mode="opaque",
            render_mode=None,
        )
    )
    obs = adapter.reset(
        seed
    )

    if condition == "OBJECT_AFFORDANCE":
        agent = (
            OpaqueObjectAffordanceAgent(
                f"OBJECT-{seed}",
                seed,
            )
        )
    elif condition == "FEATURE_TRACE":
        agent = (
            ExternalFeatureTraceAgent(
                f"FEATURE-{seed}",
                seed,
            )
        )
    else:
        raise ValueError(
            condition
        )

    completions = 0
    episodes = 0

    for _ in range(
        steps
    ):
        action = agent.choose_action(
            obs
        )
        transition = adapter.step(
            action
        )

        if condition == "OBJECT_AFFORDANCE":
            agent.observe_transition(
                obs,
                action,
                transition.reward,
                transition.terminated,
                transition.truncated,
                transition.next_observation,
            )
        else:
            agent.observe_transition(
                obs,
                action,
                transition.reward,
                transition.terminated,
                transition.truncated,
            )

        obs = (
            transition.next_observation
        )

        if (
            transition.terminated
            or transition.truncated
        ):
            episodes += 1
            completions += int(
                transition.terminated
                and transition.reward > 0
            )
            reset_seed = (
                seed + episodes
                if vary_layout
                else seed
            )
            obs = adapter.reset(
                reset_seed
            )

    adapter.close()
    return {
        "completions": completions,
        "episodes": episodes,
    }


def run_object_affordance_benchmark(
    seed_count: int = 2,
    steps_per_condition: int = 1200,
) -> dict:
    status = dependency_status()
    if not all(
        status.values()
    ):
        return {
            "experiment_id": (
                "SL-DOORKEY-OBJECT-AFFORDANCE-001"
            ),
            "status": (
                "SKIPPED_DEPENDENCY_MISSING"
            ),
            "dependencies": status,
        }

    results = {}

    for mode, vary_layout in (
        ("FIXED_LAYOUT", False),
        ("VARYING_LAYOUT", True),
    ):
        results[
            mode
        ] = {}
        rows = {}

        for condition in (
            "OBJECT_AFFORDANCE",
            "FEATURE_TRACE",
        ):
            items = []
            for seed_index in range(
                1,
                seed_count + 1,
            ):
                items.append(
                    _run(
                        condition,
                        310_000
                        + seed_index,
                        steps_per_condition,
                        vary_layout,
                    )
                )
            rows[
                condition
            ] = items
            results[
                mode
            ][
                condition
            ] = {
                "mean_completions": (
                    statistics.mean(
                        item[
                            "completions"
                        ]
                        for item in items
                    )
                ),
                "mean_episodes": (
                    statistics.mean(
                        item[
                            "episodes"
                        ]
                        for item in items
                    )
                ),
            }

        results[
            mode
        ][
            "paired_object_minus_feature_completions"
        ] = statistics.mean(
            a["completions"]
            - b["completions"]
            for a, b in zip(
                rows[
                    "OBJECT_AFFORDANCE"
                ],
                rows[
                    "FEATURE_TRACE"
                ],
            )
        )

    return {
        "experiment_id": (
            "SL-DOORKEY-OBJECT-AFFORDANCE-001"
        ),
        "status": "EXECUTED",
        "seed_count": seed_count,
        "steps_per_condition": (
            steps_per_condition
        ),
        "results": results,
        "interpretation_warning": (
            "This tiny pilot is architecture diagnostics only. "
            "Object-relative opaque features do not imply semantic object understanding."
        ),
    }
