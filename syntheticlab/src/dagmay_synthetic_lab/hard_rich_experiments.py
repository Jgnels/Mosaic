from __future__ import annotations

from collections import defaultdict
import math
import statistics

from .core import canonical_hash, DeterministicRng
from .research_stats import paired_difference_summary
from .rich_world import subject_visible_event
from .rich_agent import (
    RichDevelopmentalAgent,
    MemoryOnlyRichAgent,
    ReactiveRichAgent,
)
from .hard_rich_world import (
    NonstationaryRichWorld,
    HardRichConfig,
)
from .adaptive_trace_agent import (
    AdaptiveTraceAgent,
    HardOracleAgent,
)


HARD_AGENT_KINDS = (
    "TABULAR_DEVELOPMENTAL",
    "ADAPTIVE_TRACE",
    "MEMORY_ONLY",
    "REACTIVE",
    "ORACLE",
)


def _entropy(counts):
    counts = [x for x in counts if x > 0]
    total = sum(counts)
    if not total:
        return 0.0
    return -sum(
        (x / total) * math.log(x / total, 2)
        for x in counts
    )


def _make_agent(kind, world, seed):
    if kind == "TABULAR_DEVELOPMENTAL":
        return RichDevelopmentalAgent(
            "HARD-SUBJECT",
            world.config,
            "BALANCED_MINIMAL",
            seed,
        )
    if kind == "ADAPTIVE_TRACE":
        return AdaptiveTraceAgent(
            "HARD-SUBJECT",
            world.config,
            seed,
        )
    if kind == "MEMORY_ONLY":
        return MemoryOnlyRichAgent(
            "HARD-SUBJECT",
            world.config,
        )
    if kind == "REACTIVE":
        return ReactiveRichAgent(
            "HARD-SUBJECT",
            world.config,
        )
    if kind == "ORACLE":
        return HardOracleAgent(
            "HARD-SUBJECT",
            world.config,
            world.optimal_interaction_at_step,
        )
    raise ValueError(kind)


def run_hard_episode(
    seed: int,
    agent_kind: str,
    config: HardRichConfig | None = None,
) -> dict:
    world = NonstationaryRichWorld(seed, config)
    agent = _make_agent(agent_kind, world, seed)

    energy = []
    integrity = []
    resource_outcomes = []
    optimal_action_flags = []
    hazards = 0
    helps_received = 0
    helps_given = 0
    asks = 0
    moves = 0

    by_regime_optimal = defaultdict(list)
    by_regime_resource = defaultdict(list)

    for _ in range(world.config.steps):
        obs = world.observe()
        action = agent.choose_action(obs)
        event = world.step(action)

        # Cognition only receives the stripped subject-visible event.
        agent.observe_event(
            obs,
            subject_visible_event(event),
        )

        energy.append(event.energy_after)
        integrity.append(event.integrity_after)
        hazards += event.hazard
        helps_received += int(event.help_received)
        helps_given += int(event.help_given)
        asks += int(action.startswith("ASK:"))
        moves += int(action in {"MVL", "MVR"})

        if action in world.config.interactions:
            resource_outcomes.append(event.resource_success)
            optimal = int(
                action
                == world.optimal_interaction_at_step(
                    event.step,
                    event.zone_token,
                )
            )
            optimal_action_flags.append(optimal)
            by_regime_optimal[event.world_regime].append(optimal)
            by_regime_resource[event.world_regime].append(
                event.resource_success
            )

    adaptation = {}
    optimality_gains = []
    for regime, flags in sorted(by_regime_optimal.items()):
        n = min(30, len(flags))
        early = (
            statistics.mean(flags[:n])
            if n
            else None
        )
        late = (
            statistics.mean(flags[-n:])
            if n
            else None
        )
        resource = by_regime_resource[regime]
        early_resource = (
            statistics.mean(resource[:n])
            if n
            else None
        )
        late_resource = (
            statistics.mean(resource[-n:])
            if n
            else None
        )

        adaptation[str(regime)] = {
            "attempts": len(flags),
            "early_optimal_action_rate": early,
            "late_optimal_action_rate": late,
            "early_resource_success_rate": early_resource,
            "late_resource_success_rate": late_resource,
        }

        if (
            regime > 0
            and early is not None
            and late is not None
        ):
            optimality_gains.append(late - early)

    # Evaluate current-policy accuracy only on the final hidden regime.
    final_regime = (
        (world.config.steps - 1)
        // world.config.regime_length
    )
    final_cue = f"RC{final_regime % world.config.regime_cue_cardinality}"

    final_policy_accuracy = None
    if isinstance(agent, RichDevelopmentalAgent):
        comparisons = []
        for zone in world.config.zones:
            predicted = agent.predicted_best_action(
                final_cue,
                zone,
            )
            actual = world._mappings[final_regime][zone]
            comparisons.append(
                1.0 if predicted == actual else 0.0
            )
        final_policy_accuracy = statistics.mean(comparisons)

    elif isinstance(agent, AdaptiveTraceAgent):
        comparisons = []
        # Probe across stock bands rather than assuming one stock state.
        for zone in world.config.zones:
            actual = world._mappings[final_regime][zone]
            for signal in ("LS0", "LS1", "LS2", "LS3"):
                predicted = agent.predicted_action(
                    final_cue,
                    zone,
                    signal,
                )
                comparisons.append(
                    1.0 if predicted == actual else 0.0
                )
        final_policy_accuracy = statistics.mean(comparisons)

    return {
        "seed": seed,
        "agent_kind": agent_kind,
        "steps": world.config.steps,
        "mean_energy": statistics.mean(energy),
        "low_energy_fraction": statistics.mean(
            1.0 if x < .20 else 0.0
            for x in energy
        ),
        "mean_integrity": statistics.mean(integrity),
        "final_integrity": integrity[-1],
        "resource_success_rate": (
            statistics.mean(resource_outcomes)
            if resource_outcomes
            else 0.0
        ),
        "optimal_action_selection_rate": (
            statistics.mean(optimal_action_flags)
            if optimal_action_flags
            else 0.0
        ),
        "mean_post_change_optimality_gain": (
            statistics.mean(optimality_gains)
            if optimality_gains
            else None
        ),
        "final_hidden_regime_policy_accuracy": final_policy_accuracy,
        "hazards": hazards,
        "help_received": helps_received,
        "help_given": helps_given,
        "ask_count": asks,
        "move_count": moves,
        "action_entropy": _entropy(agent.action_counts.values()),
        "adaptation_by_hidden_regime": adaptation,
        "final_mean_resource_stock": statistics.mean(
            world.state.resource_stock.values()
        ),
        "world_state_hash": world.state_hash(),
        "agent_state_hash": agent.state_hash(),
    }


def _mean(items, field):
    vals = [
        x[field]
        for x in items
        if x[field] is not None
    ]
    return statistics.mean(vals) if vals else None


def run_hard_benchmark(seed_count: int = 32) -> dict:
    rows = {
        kind: [
            run_hard_episode(seed, kind)
            for seed in range(1, seed_count + 1)
        ]
        for kind in HARD_AGENT_KINDS
    }

    summary = {}
    for kind, items in rows.items():
        summary[kind] = {
            "mean_energy": _mean(items, "mean_energy"),
            "low_energy_fraction": _mean(
                items,
                "low_energy_fraction",
            ),
            "mean_integrity": _mean(
                items,
                "mean_integrity",
            ),
            "resource_success_rate": _mean(
                items,
                "resource_success_rate",
            ),
            "optimal_action_selection_rate": _mean(
                items,
                "optimal_action_selection_rate",
            ),
            "mean_post_change_optimality_gain": _mean(
                items,
                "mean_post_change_optimality_gain",
            ),
            "final_hidden_regime_policy_accuracy": _mean(
                items,
                "final_hidden_regime_policy_accuracy",
            ),
            "action_entropy": _mean(
                items,
                "action_entropy",
            ),
        }

    adaptive = rows["ADAPTIVE_TRACE"]
    tabular = rows["TABULAR_DEVELOPMENTAL"]

    paired = {
        "optimal_action_selection_rate": paired_difference_summary(
            [x["optimal_action_selection_rate"] for x in adaptive],
            [x["optimal_action_selection_rate"] for x in tabular],
            "ADAPTIVE_MINUS_TABULAR",
        ),
        "resource_success_rate": paired_difference_summary(
            [x["resource_success_rate"] for x in adaptive],
            [x["resource_success_rate"] for x in tabular],
            "ADAPTIVE_MINUS_TABULAR",
        ),
        "mean_energy": paired_difference_summary(
            [x["mean_energy"] for x in adaptive],
            [x["mean_energy"] for x in tabular],
            "ADAPTIVE_MINUS_TABULAR",
        ),
        "final_hidden_regime_policy_accuracy": paired_difference_summary(
            [x["final_hidden_regime_policy_accuracy"] for x in adaptive],
            [x["final_hidden_regime_policy_accuracy"] for x in tabular],
            "ADAPTIVE_MINUS_TABULAR",
        ),
    }

    return {
        "experiment_id": "SL-HARD-RICH-BENCHMARK-001",
        "seed_count": seed_count,
        "summary": summary,
        "paired_adaptive_vs_tabular": paired,
        "design": {
            "ambiguous_regime_cues": True,
            "resource_depletion": True,
            "nonstationary_partner_reliability": True,
            "hidden_truth_stripped_from_cognition": True,
        },
        "interpretation_warning": (
            "Performance differences identify mechanism strengths and weaknesses "
            "inside this synthetic environment. They do not establish general intelligence "
            "or psychological realism."
        ),
    }


def _generate_order_dataset(seed: int, steps: int = 900):
    world = NonstationaryRichWorld(
        seed,
        HardRichConfig(steps=steps),
    )
    pairs = []
    actions = list(world.config.interactions)

    for step in range(steps):
        obs = world.observe()

        # Deterministic exploration schedule with occasional movement.
        if step % 23 == 0:
            action = "MVR"
        else:
            action = actions[step % len(actions)]

        event = world.step(action)
        if action in world.config.interactions:
            pairs.append(
                (
                    obs,
                    subject_visible_event(event),
                )
            )

    return world.config, pairs


def _tabular_resource_state(agent):
    return canonical_hash({
        "|".join(k): v.to_dict()
        for k, v in sorted(agent.resource_stats.items())
    })


def _adaptive_estimate_state(agent):
    return canonical_hash({
        "|".join(k): v.to_dict()
        for k, v in sorted(agent.estimates.items())
    })


def run_history_order_audit(seed_count: int = 24) -> dict:
    tabular_multiset_invariance = []
    adaptive_order_divergence = []
    adaptive_policy_divergence = []

    for seed in range(1, seed_count + 1):
        config, pairs = _generate_order_dataset(seed)

        chronological = list(pairs)
        shuffled = list(pairs)
        rng = DeterministicRng(seed ^ 0xDEADBEEF)
        rng.shuffle(shuffled)

        tab_a = RichDevelopmentalAgent(
            "TAB-A",
            config,
            "BALANCED_MINIMAL",
            seed,
        )
        tab_b = RichDevelopmentalAgent(
            "TAB-B",
            config,
            "BALANCED_MINIMAL",
            seed,
        )

        for obs, event in chronological:
            tab_a.observe_event(obs, event)
        for obs, event in shuffled:
            tab_b.observe_event(obs, event)

        tabular_multiset_invariance.append(
            1.0
            if _tabular_resource_state(tab_a)
            == _tabular_resource_state(tab_b)
            else 0.0
        )

        adapt_a = AdaptiveTraceAgent(
            "ADAPT-A",
            config,
            seed,
        )
        adapt_b = AdaptiveTraceAgent(
            "ADAPT-B",
            config,
            seed,
        )

        for obs, event in chronological:
            adapt_a.observe_event(obs, event)
        for obs, event in shuffled:
            adapt_b.observe_event(obs, event)

        adaptive_order_divergence.append(
            1.0
            if _adaptive_estimate_state(adapt_a)
            != _adaptive_estimate_state(adapt_b)
            else 0.0
        )

        # Probe whether ordering can change later policy.
        probe_differences = []
        for cue in ("RC0", "RC1"):
            for zone in config.zones:
                for signal in ("LS0", "LS1", "LS2", "LS3"):
                    probe_differences.append(
                        1.0
                        if adapt_a.predicted_action(
                            cue,
                            zone,
                            signal,
                        )
                        != adapt_b.predicted_action(
                            cue,
                            zone,
                            signal,
                        )
                        else 0.0
                    )

        adaptive_policy_divergence.append(
            statistics.mean(probe_differences)
        )

    return {
        "experiment_id": "SL-HISTORY-ORDER-AUDIT-001",
        "seed_count": seed_count,
        "tabular_final_resource_state_multiset_invariance_rate": statistics.mean(
            tabular_multiset_invariance
        ),
        "adaptive_internal_state_order_divergence_rate": statistics.mean(
            adaptive_order_divergence
        ),
        "adaptive_mean_policy_divergence_after_shuffle": statistics.mean(
            adaptive_policy_divergence
        ),
        "scientific_interpretation": (
            "The accumulation-based tabular learner largely treats identical evidence "
            "multisets as equivalent, while the recency-weighted substrate is path dependent. "
            "This directly tests the proposition that ordered personal history must alter "
            "continuing internal state if development is to mean more than memory retrieval."
        ),
    }
