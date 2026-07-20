from __future__ import annotations

from dataclasses import replace
import statistics

from .meta_adaptive_agent import MetaAdaptiveAgent
from .rich_world import (
    ControlledRichWorld,
    richness_configs,
    subject_visible_event,
)
from .hard_rich_world import (
    NonstationaryRichWorld,
    HardRichConfig,
)


def _develop(world, agent, steps):
    for _ in range(steps):
        obs = world.observe()
        action = agent.choose_action(obs)
        event = world.step(action)
        agent.observe_event(
            obs,
            subject_visible_event(event),
        )


def _target_run(world, agent, steps):
    optimal_flags = []
    successes = []
    energy = []

    for _ in range(steps):
        obs = world.observe()
        action = agent.choose_action(obs)
        event = world.step(action)
        agent.observe_event(
            obs,
            subject_visible_event(event),
        )

        if action in world.config.interactions:
            optimal_flags.append(
                1.0
                if action
                == world.optimal_interaction_at_step(
                    event.step,
                    event.zone_token,
                )
                else 0.0
            )
            successes.append(event.resource_success)
        energy.append(event.energy_after)

    return {
        "optimal_action_rate": (
            statistics.mean(optimal_flags)
            if optimal_flags else 0.0
        ),
        "resource_success_rate": (
            statistics.mean(successes)
            if successes else 0.0
        ),
        "mean_energy": statistics.mean(energy),
        "final_volatility": agent.volatility,
        "final_alpha": agent.alpha,
    }


def run_meta_transfer(seed_count: int = 24) -> dict:
    conditions = {
        "HARD_HISTORY_META_ONLY": [],
        "STABLE_HISTORY_META_ONLY": [],
        "SCRATCH": [],
    }

    learned_priors = {
        "hard": [],
        "stable": [],
        "scratch": [],
    }

    for seed in range(1, seed_count + 1):
        agent_seed = 919191

        # Source A: nonstationary life history.
        source_hard_config = HardRichConfig(
            steps=1000,
            regime_length=200,
        )
        source_hard_world = NonstationaryRichWorld(
            seed * 10 + 1,
            source_hard_config,
        )
        hard_meta = MetaAdaptiveAgent(
            "META-HARD",
            source_hard_config,
            agent_seed,
        )
        _develop(
            source_hard_world,
            hard_meta,
            900,
        )

        # Source B: stable simple history.
        stable_config = replace(
            richness_configs()["MICRO_PLUS"],
            steps=900,
        )
        stable_world = ControlledRichWorld(
            seed * 10 + 2,
            stable_config,
        )
        stable_meta = MetaAdaptiveAgent(
            "META-STABLE",
            stable_config,
            agent_seed,
        )
        _develop(
            stable_world,
            stable_meta,
            900,
        )

        target_config = HardRichConfig(
            steps=700,
            regime_length=175,
        )
        target_seed = seed * 10 + 3

        hard_transfer = hard_meta.spawn_for_new_world(
            "TARGET-HARD-META",
            target_config,
            agent_seed,
        )
        stable_transfer = stable_meta.spawn_for_new_world(
            "TARGET-STABLE-META",
            target_config,
            agent_seed,
        )
        scratch = MetaAdaptiveAgent(
            "TARGET-SCRATCH",
            target_config,
            agent_seed,
        )

        learned_priors["hard"].append(
            hard_transfer.volatility
        )
        learned_priors["stable"].append(
            stable_transfer.volatility
        )
        learned_priors["scratch"].append(
            scratch.volatility
        )

        # Each condition gets the same target world seed.
        conditions["HARD_HISTORY_META_ONLY"].append(
            _target_run(
                NonstationaryRichWorld(
                    target_seed,
                    target_config,
                ),
                hard_transfer,
                500,
            )
        )
        conditions["STABLE_HISTORY_META_ONLY"].append(
            _target_run(
                NonstationaryRichWorld(
                    target_seed,
                    target_config,
                ),
                stable_transfer,
                500,
            )
        )
        conditions["SCRATCH"].append(
            _target_run(
                NonstationaryRichWorld(
                    target_seed,
                    target_config,
                ),
                scratch,
                500,
            )
        )

    summary = {}
    for condition, rows in conditions.items():
        summary[condition] = {
            key: statistics.mean(
                row[key] for row in rows
            )
            for key in (
                "optimal_action_rate",
                "resource_success_rate",
                "mean_energy",
                "final_volatility",
                "final_alpha",
            )
        }

    return {
        "experiment_id": "SL-META-TRANSFER-001",
        "seed_count": seed_count,
        "transferred_state": (
            "volatility and adaptive learning-rate prior only"
        ),
        "explicitly_not_transferred": (
            "world causal mappings",
            "partner reliability",
            "relationships",
            "autobiographical memories",
        ),
        "source_volatility_priors": {
            k: statistics.mean(v)
            for k, v in learned_priors.items()
        },
        "target_summary": summary,
        "interpretation_warning": (
            "This tests transfer of a narrow learned adaptation prior, not general intelligence. "
            "The meta-update rule itself is researcher-authored."
        ),
    }
