from __future__ import annotations

import statistics

from .hard_rich_world import (
    NonstationaryRichWorld,
    HardRichConfig,
)
from .adaptive_trace_agent import AdaptiveTraceAgent
from .rich_world import subject_visible_event
from .rich_belief_tracker import RichBeliefTracker


def run_rich_belief_provenance(seed_count: int = 24) -> dict:
    source_coverage = []
    active_accuracy = []
    revision_counts = []
    contradicted_or_superseded = []

    for seed in range(1, seed_count + 1):
        config = HardRichConfig(
            steps=1200,
            regime_length=200,
        )
        world = NonstationaryRichWorld(seed, config)
        agent = AdaptiveTraceAgent(
            "BELIEF-SUBJECT",
            config,
            seed,
        )
        tracker = RichBeliefTracker(
            "BELIEF-SUBJECT"
        )

        for _ in range(config.steps):
            obs = world.observe()
            action = agent.choose_action(obs)
            canonical = world.step(action)
            visible = subject_visible_event(canonical)

            partner_reliability = None
            if visible.counterpart in agent.partner_reliability:
                partner_reliability = agent.partner_reliability[
                    visible.counterpart
                ].value

            tracker.observe(
                obs,
                visible,
                partner_reliability,
            )
            agent.observe_event(obs, visible)

        graph = tracker.graph

        # Every belief should have at least one provenance source.
        beliefs = list(graph.records.values())
        if beliefs:
            source_coverage.append(
                statistics.mean(
                    1.0 if b.source_ids else 0.0
                    for b in beliefs
                )
            )
        else:
            source_coverage.append(1.0)

        contradicted_or_superseded.append(
            sum(
                1
                for b in beliefs
                if b.status in {
                    "CONTRADICTED",
                    "SUPERSEDED",
                }
            )
        )
        revision_counts.append(
            tracker.revision_count
        )

        # Evaluation-only comparison against final hidden regime.
        final_regime = (
            (config.steps - 1)
            // config.regime_length
        )
        cue = f"RC{final_regime % config.regime_cue_cardinality}"

        probes = []
        for zone in config.zones:
            belief = graph.active_belief(
                "BELIEF-SUBJECT",
                f"{cue}|{zone}",
                "BEST_ACTION",
            )
            if belief is not None:
                actual = world._mappings[
                    final_regime
                ][zone]
                probes.append(
                    1.0
                    if belief.object_value == actual
                    else 0.0
                )

        active_accuracy.append(
            statistics.mean(probes)
            if probes
            else 0.0
        )

    return {
        "experiment_id": "SL-RICH-BELIEF-PROVENANCE-001",
        "seed_count": seed_count,
        "belief_provenance_source_coverage_rate": statistics.mean(
            source_coverage
        ),
        "mean_active_belief_accuracy_against_hidden_final_regime": statistics.mean(
            active_accuracy
        ),
        "mean_revision_count": statistics.mean(
            revision_counts
        ),
        "mean_preserved_nonactive_belief_versions": statistics.mean(
            contradicted_or_superseded
        ),
        "interpretation": (
            "Temporal beliefs are derived only from subject-visible hints and direct consequences, "
            "retain superseded/contradicted history, and can be evaluated against hidden truth "
            "without exposing that truth to cognition."
        ),
    }
