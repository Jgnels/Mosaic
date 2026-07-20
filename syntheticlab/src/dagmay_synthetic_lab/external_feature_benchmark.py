from __future__ import annotations

import statistics

from .minigrid_adapter import (
    MiniGridEnvironmentAdapter,
    dependency_status,
)
from .external_environment_pilot import (
    REAL_ENVIRONMENTS,
)
from .external_adaptive_agent import (
    ExternalAdaptiveAgent,
)
from .external_feature_trace_agent import (
    ExternalFeatureTraceAgent,
)
from .external_benchmark import (
    ExternalRandomControl,
)


def _agent(
    condition,
    env_id,
    seed,
):
    if condition == "FEATURE_TRACE":
        return ExternalFeatureTraceAgent(
            f"FEATURE-{env_id}",
            seed,
        )
    if condition == "TABULAR_TRACE":
        return ExternalAdaptiveAgent(
            f"TABULAR-{env_id}",
            seed,
        )
    if condition == "RANDOM":
        return ExternalRandomControl(
            seed,
        )
    raise ValueError(condition)


def _run(
    env_id,
    seed,
    steps,
    condition,
    vary_reset_seed,
):
    adapter = (
        MiniGridEnvironmentAdapter.create(
            env_id,
            mission_mode="opaque",
            render_mode=None,
        )
    )
    obs = adapter.reset(seed)
    agent = _agent(
        condition,
        env_id,
        seed,
    )

    completions = 0
    episodes = 0
    total_reward = 0.0

    for _ in range(steps):
        action = agent.choose_action(
            obs
        )
        transition = adapter.step(
            action
        )
        agent.observe_transition(
            obs,
            action,
            transition.reward,
            transition.terminated,
            transition.truncated,
        )
        total_reward += (
            transition.reward
        )
        obs = transition.next_observation

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
                if vary_reset_seed
                else seed
            )
            obs = adapter.reset(
                reset_seed
            )

    adapter.close()
    return {
        "completions": completions,
        "episodes": episodes,
        "total_reward": total_reward,
        "completion_rate": (
            completions / episodes
            if episodes
            else 0.0
        ),
    }


def _summarize(rows):
    return {
        "mean_completions": (
            statistics.mean(
                x["completions"]
                for x in rows
            )
        ),
        "mean_total_reward": (
            statistics.mean(
                x["total_reward"]
                for x in rows
            )
        ),
        "mean_completion_rate": (
            statistics.mean(
                x["completion_rate"]
                for x in rows
            )
        ),
    }


def run_external_feature_benchmark(
    seed_count: int = 10,
    steps_per_condition: int = 1600,
) -> dict:
    status = dependency_status()
    if not all(status.values()):
        return {
            "experiment_id": (
                "SL-MINIGRID-FEATURE-TRACE-001"
            ),
            "status": (
                "SKIPPED_DEPENDENCY_MISSING"
            ),
            "dependencies": status,
        }

    conditions = (
        "FEATURE_TRACE",
        "TABULAR_TRACE",
        "RANDOM",
    )
    modes = {
        "FIXED_LAYOUT": False,
        "VARYING_LAYOUT": True,
    }

    results = {}

    for mode, vary_seed in modes.items():
        results[mode] = {}

        for env_index, env_id in enumerate(
            REAL_ENVIRONMENTS
        ):
            results[mode][env_id] = {}

            condition_rows = {}
            for condition in conditions:
                rows = []
                for seed in range(
                    1,
                    seed_count + 1,
                ):
                    rows.append(
                        _run(
                            env_id,
                            seed=(
                                90_000
                                + env_index
                                * 1000
                                + seed
                            ),
                            steps=(
                                steps_per_condition
                            ),
                            condition=condition,
                            vary_reset_seed=(
                                vary_seed
                            ),
                        )
                    )
                condition_rows[
                    condition
                ] = rows
                results[mode][
                    env_id
                ][condition] = (
                    _summarize(rows)
                )

            feature = condition_rows[
                "FEATURE_TRACE"
            ]
            tabular = condition_rows[
                "TABULAR_TRACE"
            ]
            random_rows = condition_rows[
                "RANDOM"
            ]

            results[mode][env_id][
                "paired_feature_minus_tabular_completions"
            ] = statistics.mean(
                a["completions"]
                - b["completions"]
                for a, b in zip(
                    feature,
                    tabular,
                )
            )
            results[mode][env_id][
                "paired_feature_minus_random_completions"
            ] = statistics.mean(
                a["completions"]
                - b["completions"]
                for a, b in zip(
                    feature,
                    random_rows,
                )
            )

    return {
        "experiment_id": (
            "SL-MINIGRID-FEATURE-TRACE-001"
        ),
        "status": "EXECUTED",
        "dependencies": status,
        "seed_count": seed_count,
        "steps_per_condition": (
            steps_per_condition
        ),
        "results": results,
        "interpretation_warning": (
            "Opaque positional feature reuse and eligibility traces are "
            "researcher-authored mechanisms. Improved performance would "
            "support affordance-like transfer, not semantic understanding."
        ),
    }
