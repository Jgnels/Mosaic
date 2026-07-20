from __future__ import annotations
import statistics

from .minigrid_adapter import MiniGridEnvironmentAdapter, dependency_status
from .external_environment_pilot import REAL_ENVIRONMENTS
from .opaque_structural_model import OpaqueStructuralAgent
from .external_feature_trace_agent import ExternalFeatureTraceAgent
from .external_benchmark import ExternalRandomControl

CONDITIONS = ("STRUCTURAL", "FEATURE_TRACE", "RANDOM")

def _agent(condition: str, env_id: str, seed: int):
    if condition == "STRUCTURAL":
        return OpaqueStructuralAgent(f"STRUCTURAL-{env_id}", seed)
    if condition == "FEATURE_TRACE":
        return ExternalFeatureTraceAgent(f"FEATURE-{env_id}", seed)
    if condition == "RANDOM":
        return ExternalRandomControl(seed)
    raise ValueError(condition)

def _run(env_id: str, seed: int, steps: int, condition: str, vary_layout: bool):
    adapter = MiniGridEnvironmentAdapter.create(
        env_id,
        mission_mode="opaque",
        render_mode=None,
    )
    obs = adapter.reset(seed)
    agent = _agent(condition, env_id, seed)

    completions = 0
    episodes = 0
    total_reward = 0.0

    for _ in range(steps):
        action = agent.choose_action(obs)
        transition = adapter.step(action)

        if condition == "STRUCTURAL":
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

        total_reward += transition.reward
        obs = transition.next_observation

        if transition.terminated or transition.truncated:
            episodes += 1
            completions += int(
                transition.terminated and transition.reward > 0
            )
            reset_seed = seed + episodes if vary_layout else seed
            obs = adapter.reset(reset_seed)

    adapter.close()
    return {
        "completions": completions,
        "episodes": episodes,
        "total_reward": total_reward,
        "completion_rate": completions / episodes if episodes else 0.0,
    }

def _mean(rows, key):
    return statistics.mean(row[key] for row in rows)

def run_structural_external_benchmark(
    seed_count: int = 4,
    steps_per_condition: int = 900,
) -> dict:
    status = dependency_status()
    if not all(status.values()):
        return {
            "experiment_id": "SL-MINIGRID-STRUCTURAL-001",
            "status": "SKIPPED_DEPENDENCY_MISSING",
            "dependencies": status,
        }

    results = {}

    for layout_mode, vary_layout in (
        ("FIXED_LAYOUT", False),
        ("VARYING_LAYOUT", True),
    ):
        results[layout_mode] = {}

        for env_index, env_id in enumerate(REAL_ENVIRONMENTS):
            rows_by_condition = {}
            for condition in CONDITIONS:
                rows = []
                for seed_index in range(1, seed_count + 1):
                    seed = 150_000 + env_index * 10_000 + seed_index
                    rows.append(
                        _run(
                            env_id,
                            seed,
                            steps_per_condition,
                            condition,
                            vary_layout,
                        )
                    )
                rows_by_condition[condition] = rows

            results[layout_mode][env_id] = {
                condition: {
                    "mean_completions": _mean(rows, "completions"),
                    "mean_total_reward": _mean(rows, "total_reward"),
                    "mean_completion_rate": _mean(rows, "completion_rate"),
                }
                for condition, rows in rows_by_condition.items()
            }

            structural = rows_by_condition["STRUCTURAL"]
            feature = rows_by_condition["FEATURE_TRACE"]
            results[layout_mode][env_id][
                "paired_structural_minus_feature_completions"
            ] = statistics.mean(
                a["completions"] - b["completions"]
                for a, b in zip(structural, feature)
            )

    return {
        "experiment_id": "SL-MINIGRID-STRUCTURAL-001",
        "status": "EXECUTED",
        "seed_count": seed_count,
        "steps_per_condition": steps_per_condition,
        "results": results,
        "interpretation_warning": (
            "The structural agent is a researcher-authored graph/affordance baseline. "
            "Success supports the utility of explicit structure, not semantic understanding."
        ),
    }
