from __future__ import annotations

import statistics

from .fake_minigrid_env import (
    FakeMiniGridEnv,
)
from .minigrid_adapter import (
    MiniGridEnvironmentAdapter,
    dependency_status,
    restore_by_replay,
)
from .external_adaptive_agent import (
    ExternalAdaptiveAgent,
)


REAL_ENVIRONMENTS = (
    "MiniGrid-Empty-5x5-v0",
    "MiniGrid-DoorKey-6x6-v0",
    "MiniGrid-FourRooms-v0",
)


def _fake_factory(
    env_id,
    mission_mode,
):
    return MiniGridEnvironmentAdapter(
        FakeMiniGridEnv(),
        env_id=env_id,
        mission_mode=mission_mode,
    )


def run_fake_external_contract(
    seed_count: int = 16,
) -> dict:
    replay_equal = []
    deterministic_equal = []
    mission_opaque = []
    completion_counts = []

    for seed in range(
        1,
        seed_count + 1,
    ):
        adapter = _fake_factory(
            "FakeMiniGrid-v0",
            "opaque",
        )
        obs = adapter.reset(seed)
        agent = ExternalAdaptiveAgent(
            "EXTERNAL-SUBJECT",
            seed,
        )

        completions = 0

        for _ in range(180):
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
            obs = (
                transition.next_observation
            )

            if (
                transition.terminated
                or transition.truncated
            ):
                completions += int(
                    transition.terminated
                    and transition.reward > 0
                )
                obs = adapter.reset(
                    seed
                )

        completion_counts.append(
            completions
        )
        mission_opaque.append(
            1.0
            if obs.mission_token.startswith(
                "MISSION-"
            )
            and "green" not in (
                obs.mission_token.lower()
            )
            else 0.0
        )

        # Separate deterministic replay contract with a known finite trace.
        replay_adapter = _fake_factory(
            "FakeMiniGrid-v0",
            "opaque",
        )
        replay_adapter.reset(seed)
        trace = [
            1, 2, 2, 0, 2, 1, 2
        ]
        for action in trace:
            replay_adapter.step(action)

        checkpoint = (
            replay_adapter.replay_checkpoint()
        )
        restored = restore_by_replay(
            _fake_factory,
            checkpoint,
        )

        replay_equal.append(
            1.0
            if (
                restored.last_observation
                == replay_adapter.last_observation
                and restored.action_history
                == replay_adapter.action_history
            )
            else 0.0
        )

        second = _fake_factory(
            "FakeMiniGrid-v0",
            "opaque",
        )
        a = replay_adapter.reset(seed)
        b = second.reset(seed)
        deterministic_equal.append(
            1.0 if a == b else 0.0
        )

        adapter.close()
        replay_adapter.close()
        restored.close()
        second.close()

    return {
        "experiment_id": (
            "SL-EXTERNAL-ENV-OFFLINE-CONTRACT-001"
        ),
        "external_library_used": False,
        "seed_count": seed_count,
        "replay_checkpoint_equality_rate": (
            statistics.mean(
                replay_equal
            )
        ),
        "seeded_reset_equality_rate": (
            statistics.mean(
                deterministic_equal
            )
        ),
        "opaque_mission_rate": (
            statistics.mean(
                mission_opaque
            )
        ),
        "mean_fake_environment_completions": (
            statistics.mean(
                completion_counts
            )
        ),
        "interpretation_warning": (
            "This validates the external-environment adapter contract only. "
            "The fake environment is not evidence of external generalization."
        ),
    }


def _run_real_env(
    env_id: str,
    seed: int,
    steps: int,
) -> dict:
    adapter = (
        MiniGridEnvironmentAdapter.create(
            env_id,
            mission_mode="opaque",
            render_mode=None,
        )
    )
    obs = adapter.reset(seed)
    agent = ExternalAdaptiveAgent(
        f"EXTERNAL-{env_id}",
        seed,
    )

    rewards = []
    completions = 0
    episodes = 0

    for _ in range(steps):
        action = agent.choose_action(obs)
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

    result = {
        "env_id": env_id,
        "seed": seed,
        "steps": steps,
        "episodes": episodes,
        "completions": completions,
        "total_reward": sum(
            rewards
        ),
        "agent_state_hash": (
            agent.state_hash()
        ),
        "mission_mode": "opaque",
    }
    adapter.close()
    return result


def run_real_minigrid_smoke_if_available(
    seed: int = 42,
    steps_per_env: int = 600,
) -> dict:
    status = dependency_status()
    if not all(
        status.values()
    ):
        return {
            "experiment_id": (
                "SL-MINIGRID-EXTERNAL-PILOT-001"
            ),
            "status": "SKIPPED_DEPENDENCY_MISSING",
            "dependencies": status,
            "real_external_environment_used": False,
            "environments": [],
        }

    rows = [
        _run_real_env(
            env_id,
            seed + index,
            steps_per_env,
        )
        for index, env_id in enumerate(
            REAL_ENVIRONMENTS
        )
    ]

    return {
        "experiment_id": (
            "SL-MINIGRID-EXTERNAL-PILOT-001"
        ),
        "status": "EXECUTED",
        "dependencies": status,
        "real_external_environment_used": True,
        "environments": rows,
        "interpretation_warning": (
            "This is an adapter/smoke pilot. Sparse-reward task performance "
            "is not yet a validated measure of developmental generalization."
        ),
    }
