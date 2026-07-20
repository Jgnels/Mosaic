from __future__ import annotations
import json
from dagmay_synthetic_lab.minigrid_adapter import MiniGridEnvironmentAdapter
from dagmay_synthetic_lab.external_feature_trace_agent import ExternalFeatureTraceAgent
from dagmay_synthetic_lab.opaque_structural_model import OpaqueStructuralAgent

ENV_ID = "MiniGrid-DoorKey-6x6-v0"

def run(condition, seed, steps):
    adapter = MiniGridEnvironmentAdapter.create(
        ENV_ID,
        mission_mode="opaque",
        render_mode=None,
    )
    obs = adapter.reset(seed)
    if condition == "FEATURE_TRACE":
        agent = ExternalFeatureTraceAgent(
            f"{condition}-{seed}", seed
        )
    else:
        agent = OpaqueStructuralAgent(
            f"{condition}-{seed}", seed
        )

    completions = 0
    episodes = 0
    first_completion_step = None

    for step in range(steps):
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

        obs = transition.next_observation

        if transition.terminated or transition.truncated:
            episodes += 1
            if transition.terminated and transition.reward > 0:
                completions += 1
                if first_completion_step is None:
                    first_completion_step = step + 1
            # Fixed layout / same seed.
            obs = adapter.reset(seed)

    adapter.close()
    return {
        "condition": condition,
        "seed": seed,
        "steps": steps,
        "episodes": episodes,
        "completions": completions,
        "first_completion_step": first_completion_step,
    }

rows = []
for condition in ("FEATURE_TRACE", "STRUCTURAL"):
    for seed in range(200001, 200006):
        for steps in (1000, 5000, 20000):
            rows.append(run(condition, seed, steps))

print(json.dumps(rows, indent=2))
