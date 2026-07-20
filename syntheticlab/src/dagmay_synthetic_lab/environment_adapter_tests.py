from __future__ import annotations

from .rich_world import (
    ControlledRichWorld,
    richness_configs,
    subject_visible_event,
)
from .hard_rich_world import (
    NonstationaryRichWorld,
    HardRichConfig,
)
from .rich_agent import RichDevelopmentalAgent
from .adaptive_trace_agent import AdaptiveTraceAgent
from .environment_contract import (
    environment_contract_check,
    agent_contract_check,
)
from .information_boundary import (
    cognition_packet_is_clean,
)


def run_environment_adapter_tests():
    controlled = ControlledRichWorld(
        1,
        richness_configs()["RICH"],
    )
    hard = NonstationaryRichWorld(
        2,
        HardRichConfig(steps=100),
    )

    controlled_agent = RichDevelopmentalAgent(
        "A",
        controlled.config,
        "BALANCED_MINIMAL",
        1,
    )
    hard_agent = AdaptiveTraceAgent(
        "B",
        hard.config,
        2,
    )

    obs_a = controlled.observe()
    event_a = controlled.step(
        controlled.config.interactions[0]
    )
    visible_a = subject_visible_event(event_a)

    obs_b = hard.observe()
    event_b = hard.step(
        hard.config.interactions[0]
    )
    visible_b = subject_visible_event(event_b)

    clean_a = cognition_packet_is_clean(
        obs_a,
        visible_a,
    )
    clean_b = cognition_packet_is_clean(
        obs_b,
        visible_b,
    )

    # Canonical events should contain at least one scoring-only hidden field.
    canonical_a = cognition_packet_is_clean(
        obs_a,
        event_a,
    )
    canonical_b = cognition_packet_is_clean(
        obs_b,
        event_b,
    )

    return {
        "experiment_id": "SL-ENVIRONMENT-CONTRACT-001",
        "controlled_environment": environment_contract_check(controlled),
        "hard_environment": environment_contract_check(hard),
        "controlled_agent": agent_contract_check(controlled_agent),
        "hard_agent": agent_contract_check(hard_agent),
        "controlled_subject_packet_clean": clean_a[0],
        "hard_subject_packet_clean": clean_b[0],
        "controlled_canonical_event_would_leak": not canonical_a[0],
        "hard_canonical_event_would_leak": not canonical_b[0],
        "controlled_hidden_fields_detected": canonical_a[1],
        "hard_hidden_fields_detected": canonical_b[1],
    }
