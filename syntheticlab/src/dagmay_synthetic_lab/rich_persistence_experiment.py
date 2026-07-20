from __future__ import annotations

import json
import statistics

from .hard_rich_world import (
    NonstationaryRichWorld,
    HardRichConfig,
)
from .adaptive_trace_agent import AdaptiveTraceAgent
from .rich_world import subject_visible_event
from .rich_snapshot import snapshot_pair, restore_pair


def _advance(world, agent, steps):
    actions = []
    event_ids = []
    energy = []

    for _ in range(steps):
        obs = world.observe()
        action = agent.choose_action(obs)
        event = world.step(action)
        agent.observe_event(
            obs,
            subject_visible_event(event),
        )
        actions.append(action)
        event_ids.append(event.event_id)
        energy.append(event.energy_after)

    return {
        "actions": actions,
        "event_ids": event_ids,
        "energy": energy,
    }


def run_rich_persistence_restart(seed_count: int = 24) -> dict:
    exact_snapshot_restore = []
    exact_continuation = []
    tamper_detection = []

    for seed in range(1, seed_count + 1):
        config = HardRichConfig(steps=1500)
        world = NonstationaryRichWorld(seed, config)
        agent = AdaptiveTraceAgent(
            "PERSISTENT-SUBJECT",
            config,
            seed,
        )

        _advance(world, agent, 777)

        snapshot = snapshot_pair(world, agent)

        # Force a real JSON serialization boundary.
        encoded = json.dumps(
            snapshot,
            sort_keys=True,
            separators=(",", ":"),
        )
        decoded = json.loads(encoded)

        restored_world, restored_agent = restore_pair(decoded)

        exact_snapshot_restore.append(
            1.0
            if (
                world.state_hash()
                == restored_world.state_hash()
                and agent.state_hash()
                == restored_agent.state_hash()
            )
            else 0.0
        )

        original_trace = _advance(
            world,
            agent,
            500,
        )
        restored_trace = _advance(
            restored_world,
            restored_agent,
            500,
        )

        exact_continuation.append(
            1.0
            if (
                original_trace["actions"]
                == restored_trace["actions"]
                and original_trace["event_ids"]
                == restored_trace["event_ids"]
                and original_trace["energy"]
                == restored_trace["energy"]
                and world.state_hash()
                == restored_world.state_hash()
                and agent.state_hash()
                == restored_agent.state_hash()
            )
            else 0.0
        )

        tampered = json.loads(encoded)
        tampered["world"]["state"]["energy"] = max(
            0.0,
            float(tampered["world"]["state"]["energy"]) - .01,
        )
        detected = False
        try:
            restore_pair(tampered)
        except ValueError:
            detected = True
        tamper_detection.append(1.0 if detected else 0.0)

    return {
        "experiment_id": "SL-RICH-PERSISTENCE-001",
        "seed_count": seed_count,
        "exact_snapshot_restore_rate": statistics.mean(
            exact_snapshot_restore
        ),
        "exact_post_restart_continuation_rate": statistics.mean(
            exact_continuation
        ),
        "tamper_detection_rate": statistics.mean(
            tamper_detection
        ),
        "interpretation": (
            "A rich developmental trajectory can cross a JSON persistence boundary "
            "and resume bit-for-bit behaviorally identical under deterministic conditions."
        ),
    }
