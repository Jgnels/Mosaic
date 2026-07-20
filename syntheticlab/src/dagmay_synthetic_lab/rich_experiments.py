from __future__ import annotations

from dataclasses import replace
from typing import Dict
from collections import defaultdict
import statistics
import math

from .core import canonical_hash, DeterministicRng
from .rich_world import (
    ControlledRichWorld,
    PartnerProfile,
    richness_configs,
    subject_visible_event,
)
from .rich_agent import (
    RichDevelopmentalAgent,
    ReactiveRichAgent,
    MemoryOnlyRichAgent,
    OracleRichAgent,
)


AGENT_KINDS = (
    "DEVELOPMENTAL",
    "MEMORY_ONLY",
    "REACTIVE",
    "ORACLE",
)


def _entropy(counts) -> float:
    counts = [x for x in counts if x > 0]
    total = sum(counts)
    if not total:
        return 0.0
    return -sum((x / total) * math.log(x / total, 2) for x in counts)


def _make_agent(kind, world, seed, profile_id="BALANCED_MINIMAL"):
    if kind == "DEVELOPMENTAL":
        return RichDevelopmentalAgent(
            "RICH-SUBJECT",
            world.config,
            profile_id=profile_id,
            seed=seed,
        )
    if kind == "MEMORY_ONLY":
        return MemoryOnlyRichAgent("RICH-SUBJECT", world.config)
    if kind == "REACTIVE":
        return ReactiveRichAgent("RICH-SUBJECT", world.config)
    if kind == "ORACLE":
        return OracleRichAgent(
            "RICH-SUBJECT",
            world.config,
            world.oracle_mapping_by_cue(),
        )
    raise ValueError(kind)


def run_rich_episode(
    seed: int,
    scenario_id: str = "RICH",
    agent_kind: str = "DEVELOPMENTAL",
    profile_id: str = "BALANCED_MINIMAL",
    steps_override: int | None = None,
) -> dict:
    base_config = richness_configs()[scenario_id]
    config = (
        replace(base_config, steps=steps_override)
        if steps_override is not None
        else base_config
    )
    world = ControlledRichWorld(seed, config)
    agent = _make_agent(agent_kind, world, seed, profile_id)

    energy_trace = []
    integrity_trace = []
    resource_attempts = 0
    resource_successes = 0
    hazards = 0
    help_given = 0
    help_received = 0
    ask_count = 0
    move_count = 0

    regime_interaction_outcomes: Dict[int, list[int]] = defaultdict(list)

    for _ in range(config.steps):
        obs = world.observe()
        action = agent.choose_action(obs)
        event = world.step(action)
        agent.observe_event(obs, subject_visible_event(event))

        energy_trace.append(event.energy_after)
        integrity_trace.append(event.integrity_after)

        if action in config.interactions:
            resource_attempts += 1
            resource_successes += event.resource_success
            regime_interaction_outcomes[event.world_regime].append(
                event.resource_success
            )

        hazards += event.hazard
        help_given += int(event.help_given)
        help_received += int(event.help_received)
        ask_count += int(action.startswith("ASK:"))
        move_count += int(action in {"MVL", "MVR"})

    model_accuracy = None
    partner_reliability_mae = None
    relationship_trust_spread = None
    significant_memory_count = None

    if isinstance(agent, RichDevelopmentalAgent):
        comparisons = []
        max_regime = max(0, (config.steps - 1) // config.regime_length)
        if not config.regime_changes_enabled:
            max_regime = 0

        for regime in range(max_regime + 1):
            cue = f"RC{regime % 4}"
            for zone in config.zones:
                predicted = agent.predicted_best_action(cue, zone)
                actual = world._mappings[regime][zone]
                comparisons.append(1.0 if predicted == actual else 0.0)

        model_accuracy = statistics.mean(comparisons) if comparisons else None

        if config.partner_ids:
            errors = []
            for pid in config.partner_ids:
                learned = agent.partner_reliability_mean(pid)
                actual = world.partner_profiles[pid].hint_reliability
                errors.append(abs(learned - actual))
            partner_reliability_mae = statistics.mean(errors)

            trusts = [
                agent.relationships[pid].compute().trust
                for pid in config.partner_ids
            ]
            relationship_trust_spread = (
                max(trusts) - min(trusts)
                if trusts
                else 0.0
            )

        significant_memory_count = len(agent.significant_memory_ids)

    adaptation_gains = []
    per_regime = {}
    for regime, outcomes in sorted(regime_interaction_outcomes.items()):
        first = outcomes[: min(30, len(outcomes))]
        last = outcomes[-min(30, len(outcomes)) :]
        early = statistics.mean(first) if first else None
        late = statistics.mean(last) if last else None
        per_regime[str(regime)] = {
            "attempts": len(outcomes),
            "early_success": early,
            "late_success": late,
        }
        if regime > 0 and early is not None and late is not None:
            adaptation_gains.append(late - early)

    return {
        "seed": seed,
        "scenario_id": scenario_id,
        "agent_kind": agent_kind,
        "profile_id": profile_id,
        "steps": config.steps,
        "mean_energy": statistics.mean(energy_trace),
        "energy_p10": sorted(energy_trace)[
            int(.10 * (len(energy_trace) - 1))
        ],
        "low_energy_fraction": statistics.mean(
            1.0 if x < .20 else 0.0 for x in energy_trace
        ),
        "mean_integrity": statistics.mean(integrity_trace),
        "final_integrity": integrity_trace[-1],
        "resource_attempts": resource_attempts,
        "resource_success_rate": (
            resource_successes / resource_attempts
            if resource_attempts
            else 0.0
        ),
        "hazards": hazards,
        "help_given": help_given,
        "help_received": help_received,
        "ask_count": ask_count,
        "move_count": move_count,
        "action_entropy": _entropy(agent.action_counts.values()),
        "causal_model_accuracy": model_accuracy,
        "partner_reliability_mae": partner_reliability_mae,
        "relationship_trust_spread": relationship_trust_spread,
        "significant_memory_count": significant_memory_count,
        "regime_metrics": per_regime,
        "mean_post_change_adaptation_gain": (
            statistics.mean(adaptation_gains)
            if adaptation_gains
            else None
        ),
        "world_state_hash": world.state_hash(),
        "agent_state_hash": agent.state_hash(),
    }


def _mean_non_none(items, field):
    vals = [x[field] for x in items if x[field] is not None]
    return statistics.mean(vals) if vals else None


def run_complexity_ladder(seed_count: int = 24) -> dict:
    scenarios = ("MICRO_PLUS", "MESO", "RICH")
    agent_kinds = AGENT_KINDS

    rows = {
        scenario: {
            kind: [
                run_rich_episode(
                    seed=s,
                    scenario_id=scenario,
                    agent_kind=kind,
                )
                for s in range(1, seed_count + 1)
            ]
            for kind in agent_kinds
        }
        for scenario in scenarios
    }

    summary = {}
    for scenario in scenarios:
        summary[scenario] = {}
        for kind in agent_kinds:
            items = rows[scenario][kind]
            summary[scenario][kind] = {
                "mean_energy": statistics.mean(
                    x["mean_energy"] for x in items
                ),
                "low_energy_fraction": statistics.mean(
                    x["low_energy_fraction"] for x in items
                ),
                "mean_integrity": statistics.mean(
                    x["mean_integrity"] for x in items
                ),
                "resource_success_rate": statistics.mean(
                    x["resource_success_rate"] for x in items
                ),
                "action_entropy": statistics.mean(
                    x["action_entropy"] for x in items
                ),
                "causal_model_accuracy": _mean_non_none(
                    items, "causal_model_accuracy"
                ),
                "partner_reliability_mae": _mean_non_none(
                    items, "partner_reliability_mae"
                ),
                "mean_post_change_adaptation_gain": _mean_non_none(
                    items, "mean_post_change_adaptation_gain"
                ),
            }

    return {
        "experiment_id": "SL-RICH-COMPLEXITY-LADDER-001",
        "seed_count": seed_count,
        "scenarios": scenarios,
        "agent_kinds": agent_kinds,
        "summary": summary,
        "interpretation_warning": (
            "Richer environments improve ecological challenge but reduce causal simplicity. "
            "Microbenchmarks remain necessary for mechanism isolation."
        ),
    }


def _run_until(world, agent, target_step):
    while world.state.step < target_step:
        obs = world.observe()
        action = agent.choose_action(obs)
        event = world.step(action)
        agent.observe_event(obs, subject_visible_event(event))


def _continue_branch(world, agent, steps):
    actions = []
    energy = []
    for _ in range(steps):
        obs = world.observe()
        action = agent.choose_action(obs)
        event = world.step(action)
        agent.observe_event(obs, subject_visible_event(event))
        actions.append(action)
        energy.append(event.energy_after)
    return actions, energy


def run_rich_exact_fork(seed_count: int = 24) -> dict:
    fork_equal = []
    post_diverged = []
    action_divergence = []
    p1_reliability_gap = []
    p1_trust_gap = []

    for seed in range(1, seed_count + 1):
        config = replace(
            richness_configs()["RICH"],
            steps=1200,
            regime_length=300,
        )
        world = ControlledRichWorld(seed, config)
        agent = RichDevelopmentalAgent(
            "RICH-SUBJECT",
            config,
            "BALANCED_MINIMAL",
            seed,
        )

        _run_until(world, agent, 600)

        world_a = world.clone()
        world_b = world.clone()
        agent_a = agent.clone()
        agent_b = agent.clone()

        pre_world_equal = world_a.state_hash() == world_b.state_hash()
        pre_agent_equal = agent_a.state_hash() == agent_b.state_hash()
        fork_equal.append(1.0 if pre_world_equal and pre_agent_equal else 0.0)

        # Controlled post-fork social intervention.
        world_a.partner_profiles["P1"] = PartnerProfile(
            "P1", .96, .24, .75, .04
        )
        world_b.partner_profiles["P1"] = PartnerProfile(
            "P1", .08, .01, .02, .04
        )

        actions_a, energy_a = _continue_branch(world_a, agent_a, 450)
        actions_b, energy_b = _continue_branch(world_b, agent_b, 450)

        post_diverged.append(
            1.0
            if (
                world_a.state_hash() != world_b.state_hash()
                and agent_a.state_hash() != agent_b.state_hash()
            )
            else 0.0
        )

        action_divergence.append(
            statistics.mean(
                1.0 if a != b else 0.0
                for a, b in zip(actions_a, actions_b)
            )
        )

        p1_reliability_gap.append(
            abs(
                agent_a.partner_reliability_mean("P1")
                - agent_b.partner_reliability_mean("P1")
            )
        )
        p1_trust_gap.append(
            abs(
                agent_a.relationships["P1"].compute().trust
                - agent_b.relationships["P1"].compute().trust
            )
        )

    return {
        "experiment_id": "SL-RICH-EXACT-FORK-001",
        "seed_count": seed_count,
        "exact_prefork_equality_rate": statistics.mean(fork_equal),
        "postfork_state_divergence_rate": statistics.mean(post_diverged),
        "mean_action_sequence_divergence": statistics.mean(action_divergence),
        "mean_p1_reliability_gap": statistics.mean(p1_reliability_gap),
        "mean_p1_trust_gap": statistics.mean(p1_trust_gap),
        "interpretation_warning": (
            "This demonstrates history-dependent branch divergence under a controlled social intervention. "
            "It does not establish personhood or subjective relationship experience."
        ),
    }


def run_path_dependence_after_normalization(seed_count: int = 24) -> dict:
    reliability_gaps_after_normalization = []
    trust_gaps_after_normalization = []
    action_divergence_after_normalization = []

    neutral_p1 = PartnerProfile("P1", .55, .08, .30, .04)

    for seed in range(1, seed_count + 1):
        config = replace(
            richness_configs()["RICH"],
            steps=1300,
            regime_length=325,
        )
        base_world = ControlledRichWorld(seed, config)
        base_agent = RichDevelopmentalAgent(
            "RICH-SUBJECT",
            config,
            "BALANCED_MINIMAL",
            seed,
        )

        _run_until(base_world, base_agent, 350)

        world_a = base_world.clone()
        world_b = base_world.clone()
        agent_a = base_agent.clone()
        agent_b = base_agent.clone()

        # Divergent developmental phase.
        world_a.partner_profiles["P1"] = PartnerProfile(
            "P1", .95, .25, .75, .05
        )
        world_b.partner_profiles["P1"] = PartnerProfile(
            "P1", .10, .01, .02, .05
        )

        _continue_branch(world_a, agent_a, 350)
        _continue_branch(world_b, agent_b, 350)

        # Normalize external conditions.
        world_a.partner_profiles["P1"] = neutral_p1
        world_b.partner_profiles["P1"] = neutral_p1

        actions_a, _ = _continue_branch(world_a, agent_a, 400)
        actions_b, _ = _continue_branch(world_b, agent_b, 400)

        reliability_gaps_after_normalization.append(
            abs(
                agent_a.partner_reliability_mean("P1")
                - agent_b.partner_reliability_mean("P1")
            )
        )
        trust_gaps_after_normalization.append(
            abs(
                agent_a.relationships["P1"].compute().trust
                - agent_b.relationships["P1"].compute().trust
            )
        )
        action_divergence_after_normalization.append(
            statistics.mean(
                1.0 if a != b else 0.0
                for a, b in zip(actions_a, actions_b)
            )
        )

    return {
        "experiment_id": "SL-RICH-PATH-DEPENDENCE-001",
        "seed_count": seed_count,
        "mean_reliability_gap_after_environment_normalization": statistics.mean(
            reliability_gaps_after_normalization
        ),
        "mean_trust_gap_after_environment_normalization": statistics.mean(
            trust_gaps_after_normalization
        ),
        "mean_action_divergence_after_environment_normalization": statistics.mean(
            action_divergence_after_normalization
        ),
        "interpretation": (
            "Branches share current external conditions but retain different internal states "
            "because their prior histories differed."
        ),
    }
