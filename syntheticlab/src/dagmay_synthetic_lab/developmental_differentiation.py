from __future__ import annotations

import statistics

from .hard_rich_world import NonstationaryRichWorld, HardRichConfig
from .adaptive_trace_agent import AdaptiveTraceAgent
from .rich_world import RichObservation, subject_visible_event
from .core import canonical_hash


def _develop(world, agent, steps):
    for _ in range(steps):
        obs = world.observe()
        action = agent.choose_action(obs)
        event = world.step(action)
        agent.observe_event(obs, subject_visible_event(event))


def _probe_battery(config):
    probes = []
    step = 10_000
    for cue in ("RC0", "RC1"):
        for zone in config.zones:
            for signal in ("LS0", "LS1", "LS2", "LS3"):
                for energy in ("B0", "B1", "B2", "B3"):
                    probes.append(RichObservation(
                        step=step,
                        zone_token=zone,
                        regime_cue=cue,
                        energy_band=energy,
                        integrity_band="B2",
                        local_signal=signal,
                        available_partners=(),
                        help_request_from=None,
                        last_received_hint=None,
                    ))
                    step += 1
    return probes


def _policy_signature(agent, probes):
    clone = agent.clone()
    return tuple(clone.choose_action(obs) for obs in probes)


def _relationship_signature(agent):
    return {
        pid: agent.relationships[pid].compute().to_dict()
        for pid in sorted(agent.relationships)
    }


def run_developmental_differentiation(seed_count: int = 24) -> dict:
    independent_history_policy_divergence = []
    identical_history_policy_divergence = []
    fresh_control_policy_divergence = []
    independent_state_divergence = []
    identical_state_equality = []
    partner_reliability_divergence = []

    config = HardRichConfig(steps=1100)
    probes = _probe_battery(config)

    for seed in range(1, seed_count + 1):
        # Same architecture and same internal tie-break seed. Only lived world history differs.
        agent_seed = 424242

        world_a = NonstationaryRichWorld(seed * 2, config)
        world_b = NonstationaryRichWorld(seed * 2 + 1, config)
        agent_a = AdaptiveTraceAgent("A", config, agent_seed)
        agent_b = AdaptiveTraceAgent("B", config, agent_seed)

        _develop(world_a, agent_a, 900)
        _develop(world_b, agent_b, 900)

        sig_a = _policy_signature(agent_a, probes)
        sig_b = _policy_signature(agent_b, probes)

        independent_history_policy_divergence.append(
            statistics.mean(
                1.0 if a != b else 0.0
                for a, b in zip(sig_a, sig_b)
            )
        )
        independent_state_divergence.append(
            1.0 if agent_a.state_hash() != agent_b.state_hash() else 0.0
        )

        rel_gap = []
        for pid in config.partner_ids:
            rel_gap.append(abs(
                agent_a.partner_reliability[pid].value
                - agent_b.partner_reliability[pid].value
            ))
        partner_reliability_divergence.append(statistics.mean(rel_gap))

        # Determinism control: same world history must produce identical development.
        world_c = NonstationaryRichWorld(seed * 2, config)
        world_d = NonstationaryRichWorld(seed * 2, config)
        agent_c = AdaptiveTraceAgent("C", config, agent_seed)
        agent_d = AdaptiveTraceAgent("C", config, agent_seed)
        _develop(world_c, agent_c, 900)
        _develop(world_d, agent_d, 900)

        sig_c = _policy_signature(agent_c, probes)
        sig_d = _policy_signature(agent_d, probes)
        identical_history_policy_divergence.append(
            statistics.mean(
                1.0 if a != b else 0.0
                for a, b in zip(sig_c, sig_d)
            )
        )
        identical_state_equality.append(
            1.0 if agent_c.state_hash() == agent_d.state_hash() else 0.0
        )

        # Fresh control: before history, otherwise identical agents should match.
        fresh_a = AdaptiveTraceAgent("F", config, agent_seed)
        fresh_b = AdaptiveTraceAgent("F", config, agent_seed)
        fresh_sig_a = _policy_signature(fresh_a, probes)
        fresh_sig_b = _policy_signature(fresh_b, probes)
        fresh_control_policy_divergence.append(
            statistics.mean(
                1.0 if a != b else 0.0
                for a, b in zip(fresh_sig_a, fresh_sig_b)
            )
        )

    return {
        "experiment_id": "SL-DEVELOPMENTAL-DIFFERENTIATION-001",
        "seed_count": seed_count,
        "mean_policy_divergence_after_different_histories": statistics.mean(
            independent_history_policy_divergence
        ),
        "different_history_internal_state_divergence_rate": statistics.mean(
            independent_state_divergence
        ),
        "mean_partner_reliability_divergence_after_different_histories": statistics.mean(
            partner_reliability_divergence
        ),
        "identical_history_mean_policy_divergence": statistics.mean(
            identical_history_policy_divergence
        ),
        "identical_history_exact_state_equality_rate": statistics.mean(
            identical_state_equality
        ),
        "fresh_identical_agent_policy_divergence": statistics.mean(
            fresh_control_policy_divergence
        ),
        "causal_claim_scope": (
            "Under deterministic architecture and identical internal seeds, different synthetic "
            "life histories cause persistent differences in learned state and later policy. "
            "This does not establish personality, selfhood, or consciousness; world-specific "
            "learning remains an important alternative explanation."
        ),
    }
