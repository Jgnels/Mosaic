from __future__ import annotations

from collections import defaultdict

from .hard_rich_world import (
    NonstationaryRichWorld,
    HardRichConfig,
    HardRichState,
)
from .rich_world import PartnerProfile
from .adaptive_trace_agent import (
    AdaptiveTraceAgent,
    RecencyEstimate,
    RecencyReliability,
    AdaptivePendingHint,
)
from .relationships import DirectedRelationshipModel, SocialEvidence
from .core import canonical_hash


SNAPSHOT_VERSION = "RICH-SNAPSHOT-1.0"


def _config_from_dict(d: dict) -> HardRichConfig:
    payload = dict(d)
    for key in ("zones", "interactions", "partner_ids"):
        if key in payload:
            payload[key] = tuple(payload[key])
    return HardRichConfig(**payload)


def snapshot_hard_world(world: NonstationaryRichWorld) -> dict:
    return {
        "snapshot_version": SNAPSHOT_VERSION,
        "type": "NonstationaryRichWorld",
        "seed": world.seed,
        "config": world.config.to_dict(),
        "state": world.state.to_dict(),
        "partner_profiles": {
            k: v.to_dict()
            for k, v in sorted(world.partner_profiles.items())
        },
        "mappings": {
            str(k): dict(sorted(v.items()))
            for k, v in sorted(world._mappings.items())
        },
        "state_hash": world.state_hash(),
    }


def restore_hard_world(snapshot: dict) -> NonstationaryRichWorld:
    if snapshot["snapshot_version"] != SNAPSHOT_VERSION:
        raise ValueError("unsupported rich-world snapshot version")
    if snapshot["type"] != "NonstationaryRichWorld":
        raise ValueError("snapshot type mismatch")

    config = _config_from_dict(snapshot["config"])
    profiles = {
        k: PartnerProfile(**v)
        for k, v in snapshot["partner_profiles"].items()
    }
    world = NonstationaryRichWorld(
        int(snapshot["seed"]),
        config,
        profiles,
    )

    state = snapshot["state"]
    world.state = HardRichState(
        step=int(state["step"]),
        subject_zone_index=int(state["subject_zone_index"]),
        energy=float(state["energy"]),
        integrity=float(state["integrity"]),
        last_received_hint=(
            tuple(state["last_received_hint"])
            if state["last_received_hint"] is not None
            else None
        ),
        last_hint_source=state["last_hint_source"],
        help_debt={
            str(k): float(v)
            for k, v in state["help_debt"].items()
        },
        resource_stock={
            str(k): float(v)
            for k, v in state["resource_stock"].items()
        },
    )
    world._mappings = {
        int(k): dict(v)
        for k, v in snapshot["mappings"].items()
    }

    if world.state_hash() != snapshot["state_hash"]:
        raise ValueError("restored rich-world state hash mismatch")
    return world


def _relationship_from_dict(data: dict) -> DirectedRelationshipModel:
    rel = DirectedRelationshipModel(
        data["source_id"],
        data["target_id"],
    )
    for item in data["evidence"]:
        rel.add_evidence(SocialEvidence(**item))
    return rel


def snapshot_adaptive_agent(agent: AdaptiveTraceAgent) -> dict:
    return {
        "snapshot_version": SNAPSHOT_VERSION,
        "type": "AdaptiveTraceAgent",
        "agent_id": agent.agent_id,
        "seed": agent.seed,
        "alpha": agent.alpha,
        "config": agent.config.to_dict(),
        "estimates": {
            "|".join(k): v.to_dict()
            for k, v in sorted(agent.estimates.items())
        },
        "base_estimates": {
            "|".join(k): v.to_dict()
            for k, v in sorted(agent.base_estimates.items())
        },
        "partner_reliability": {
            k: v.to_dict()
            for k, v in sorted(agent.partner_reliability.items())
        },
        "relationships": {
            k: v.to_dict()
            for k, v in sorted(agent.relationships.items())
        },
        "pending_hint": (
            agent.pending_hint.to_dict()
            if agent.pending_hint is not None
            else None
        ),
        "zone_last_visit": dict(sorted(agent.zone_last_visit.items())),
        "action_counts": dict(sorted(agent.action_counts.items())),
        "memory_ids": list(agent.memory_ids),
        "significant_memory_ids": list(agent.significant_memory_ids),
        "help_given_recently": dict(sorted(agent.help_given_recently.items())),
        "state_hash": agent.state_hash(),
    }


def _restore_recency_estimate(data: dict) -> RecencyEstimate:
    return RecencyEstimate(
        value=float(data["value"]),
        effective_count=float(data["effective_count"]),
        recent_prediction_error=float(data["recent_prediction_error"]),
        learning_progress=float(data["learning_progress"]),
    )


def _restore_reliability(data: dict) -> RecencyReliability:
    return RecencyReliability(
        value=float(data["value"]),
        effective_count=float(data["effective_count"]),
    )


def restore_adaptive_agent(snapshot: dict) -> AdaptiveTraceAgent:
    if snapshot["snapshot_version"] != SNAPSHOT_VERSION:
        raise ValueError("unsupported adaptive-agent snapshot version")
    if snapshot["type"] != "AdaptiveTraceAgent":
        raise ValueError("snapshot type mismatch")

    config = _config_from_dict(snapshot["config"])
    agent = AdaptiveTraceAgent(
        snapshot["agent_id"],
        config,
        int(snapshot["seed"]),
        float(snapshot["alpha"]),
    )

    agent.estimates = {
        tuple(k.split("|")): _restore_recency_estimate(v)
        for k, v in snapshot["estimates"].items()
    }
    agent.base_estimates = {
        tuple(k.split("|")): _restore_recency_estimate(v)
        for k, v in snapshot["base_estimates"].items()
    }
    agent.partner_reliability = {
        k: _restore_reliability(v)
        for k, v in snapshot["partner_reliability"].items()
    }
    agent.relationships = {
        k: _relationship_from_dict(v)
        for k, v in snapshot["relationships"].items()
    }

    pending = snapshot["pending_hint"]
    agent.pending_hint = (
        AdaptivePendingHint(**pending)
        if pending is not None
        else None
    )
    agent.zone_last_visit = {
        str(k): int(v)
        for k, v in snapshot["zone_last_visit"].items()
    }
    agent.action_counts = defaultdict(
        int,
        {
            str(k): int(v)
            for k, v in snapshot["action_counts"].items()
        },
    )
    agent.memory_ids = list(snapshot["memory_ids"])
    agent.significant_memory_ids = list(
        snapshot["significant_memory_ids"]
    )
    agent.help_given_recently = {
        str(k): int(v)
        for k, v in snapshot["help_given_recently"].items()
    }

    if agent.state_hash() != snapshot["state_hash"]:
        raise ValueError("restored adaptive-agent state hash mismatch")
    return agent


def snapshot_pair(
    world: NonstationaryRichWorld,
    agent: AdaptiveTraceAgent,
) -> dict:
    payload = {
        "snapshot_version": SNAPSHOT_VERSION,
        "world": snapshot_hard_world(world),
        "agent": snapshot_adaptive_agent(agent),
    }
    return {
        **payload,
        "pair_hash": canonical_hash(payload),
    }


def restore_pair(snapshot: dict):
    if snapshot["snapshot_version"] != SNAPSHOT_VERSION:
        raise ValueError("unsupported pair snapshot version")
    payload = {
        "snapshot_version": snapshot["snapshot_version"],
        "world": snapshot["world"],
        "agent": snapshot["agent"],
    }
    if canonical_hash(payload) != snapshot["pair_hash"]:
        raise ValueError("pair snapshot hash mismatch")
    return (
        restore_hard_world(snapshot["world"]),
        restore_adaptive_agent(snapshot["agent"]),
    )
