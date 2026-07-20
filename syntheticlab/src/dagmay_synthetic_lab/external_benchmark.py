from __future__ import annotations

import statistics

from .core import stable_unit_float
from .minigrid_adapter import (
    MiniGridEnvironmentAdapter,
    dependency_status,
)
from .external_adaptive_agent import (
    ExternalAdaptiveAgent,
)
from .external_environment_pilot import (
    REAL_ENVIRONMENTS,
)


class ExternalRandomControl:
    def __init__(
        self,
        seed: int,
        action_count: int = 7,
    ):
        self.seed = seed
        self.action_count = action_count
        self.step = 0

    def choose_action(self, obs):
        del obs
        action = int(
            stable_unit_float(
                "external-random-control",
                self.seed,
                self.step,
            )
            * self.action_count
        )
        self.step += 1
        return min(
            action,
            self.action_count - 1,
        )

    def observe_transition(
        self,
        obs,
        action,
        reward,
        terminated,
        truncated,
    ):
        del (
            obs,
            action,
            reward,
            terminated,
            truncated,
        )


def _run_condition(
    env_id: str,
    seed: int,
    steps: int,
    condition: str,
):
    adapter = (
        MiniGridEnvironmentAdapter.create(
            env_id,
            mission_mode="opaque",
            render_mode=None,
        )
    )
    obs = adapter.reset(seed)

    if condition == "ADAPTIVE":
        agent = ExternalAdaptiveAgent(
            f"ADAPTIVE-{env_id}",
            seed,
        )
    elif condition == "RANDOM":
        agent = ExternalRandomControl(
            seed,
        )
    else:
        raise ValueError(condition)

    completions = 0
    episodes = 0
    rewards = []

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
        rewards.append(
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
            obs = adapter.reset(
                seed + episodes
            )

    adapter.close()
    return {
        "completions": completions,
        "episodes": episodes,
        "total_reward": sum(
            rewards
        ),
        "completion_per_episode": (
            completions / episodes
            if episodes
            else 0.0
        ),
    }


def run_minigrid_paired_benchmark(
    seed_count: int = 12,
    steps_per_condition: int = 800,
) -> dict:
    status = dependency_status()
    if not all(
        status.values()
    ):
        return {
            "experiment_id": (
                "SL-MINIGRID-PAIRED-BENCHMARK-001"
            ),
            "status": (
                "SKIPPED_DEPENDENCY_MISSING"
            ),
            "dependencies": status,
            "results": {},
        }

    results = {}

    for env_index, env_id in enumerate(
        REAL_ENVIRONMENTS
    ):
        adaptive = []
        random_control = []

        for seed in range(
            1,
            seed_count + 1,
        ):
            actual_seed = (
                10_000
                + env_index * 1000
                + seed
            )
            adaptive.append(
                _run_condition(
                    env_id,
                    actual_seed,
                    steps_per_condition,
                    "ADAPTIVE",
                )
            )
            random_control.append(
                _run_condition(
                    env_id,
                    actual_seed,
                    steps_per_condition,
                    "RANDOM",
                )
            )

        completion_diffs = [
            a["completions"]
            - b["completions"]
            for a, b in zip(
                adaptive,
                random_control,
            )
        ]
        reward_diffs = [
            a["total_reward"]
            - b["total_reward"]
            for a, b in zip(
                adaptive,
                random_control,
            )
        ]

        results[env_id] = {
            "adaptive_mean_completions": (
                statistics.mean(
                    x["completions"]
                    for x in adaptive
                )
            ),
            "random_mean_completions": (
                statistics.mean(
                    x["completions"]
                    for x in random_control
                )
            ),
            "adaptive_mean_total_reward": (
                statistics.mean(
                    x["total_reward"]
                    for x in adaptive
                )
            ),
            "random_mean_total_reward": (
                statistics.mean(
                    x["total_reward"]
                    for x in random_control
                )
            ),
            "paired_mean_completion_difference": (
                statistics.mean(
                    completion_diffs
                )
            ),
            "paired_positive_completion_fraction": (
                sum(
                    1
                    for x in completion_diffs
                    if x > 0
                )
                / len(
                    completion_diffs
                )
            ),
            "paired_mean_reward_difference": (
                statistics.mean(
                    reward_diffs
                )
            ),
        }

    return {
        "experiment_id": (
            "SL-MINIGRID-PAIRED-BENCHMARK-001"
        ),
        "status": "EXECUTED",
        "dependencies": status,
        "seed_count": seed_count,
        "steps_per_condition": (
            steps_per_condition
        ),
        "results": results,
        "interpretation_warning": (
            "This benchmark tests the current generic external learner, not "
            "Dagmay's full future cognitive architecture. Failure on DoorKey "
            "or FourRooms is expected to reveal missing planning, object "
            "affordance, or hierarchical skill mechanisms."
        ),
    }


def run_real_minigrid_replay_determinism(
    seed_count: int = 12,
) -> dict:
    status = dependency_status()
    if not all(
        status.values()
    ):
        return {
            "experiment_id": (
                "SL-MINIGRID-REPLAY-001"
            ),
            "status": (
                "SKIPPED_DEPENDENCY_MISSING"
            ),
            "dependencies": status,
        }

    equality = []

    for env_index, env_id in enumerate(
        REAL_ENVIRONMENTS
    ):
        for seed in range(
            1,
            seed_count + 1,
        ):
            actual_seed = (
                50_000
                + env_index * 1000
                + seed
            )
            a = (
                MiniGridEnvironmentAdapter.create(
                    env_id,
                    mission_mode="opaque",
                    render_mode=None,
                )
            )
            b = (
                MiniGridEnvironmentAdapter.create(
                    env_id,
                    mission_mode="opaque",
                    render_mode=None,
                )
            )

            obs_a = a.reset(
                actual_seed
            )
            obs_b = b.reset(
                actual_seed
            )

            same = (
                obs_a == obs_b
            )

            for step in range(120):
                action = int(
                    stable_unit_float(
                        "minigrid-replay-action",
                        actual_seed,
                        step,
                    )
                    * 7
                )
                action = min(
                    action,
                    6,
                )

                ta = a.step(action)
                tb = b.step(action)

                if (
                    ta.reward
                    != tb.reward
                    or ta.terminated
                    != tb.terminated
                    or ta.truncated
                    != tb.truncated
                    or ta.next_observation
                    != tb.next_observation
                ):
                    same = False
                    break

                if (
                    ta.terminated
                    or ta.truncated
                ):
                    break

            equality.append(
                1.0 if same else 0.0
            )
            a.close()
            b.close()

    return {
        "experiment_id": (
            "SL-MINIGRID-REPLAY-001"
        ),
        "status": "EXECUTED",
        "dependencies": status,
        "tested_environment_count": len(
            REAL_ENVIRONMENTS
        ),
        "seed_count_per_environment": (
            seed_count
        ),
        "identical_seed_action_trace_equality_rate": (
            statistics.mean(
                equality
            )
        ),
    }
