from __future__ import annotations

from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class ScenarioDefinition:
    scenario_id: str
    layer: str
    partial_observability: bool
    nonstationary_causality: bool
    social_contingency: bool
    resource_depletion: bool
    hidden_truth_scoring: bool
    exact_fork_support: bool
    persistence_restart_support: bool
    llm_required: bool
    primary_question: str
    claim_limit: str

    def to_dict(self):
        return asdict(self)


SCENARIOS = {
    "MICRO_PLUS": ScenarioDefinition(
        "MICRO_PLUS",
        "MICRO",
        False,
        False,
        False,
        False,
        True,
        True,
        False,
        False,
        "Can the mechanism learn a simple causal relation?",
        "Mechanism validation only.",
    ),
    "MESO": ScenarioDefinition(
        "MESO",
        "MESO",
        True,
        True,
        True,
        False,
        True,
        True,
        False,
        False,
        "Do basic learning and social mechanisms survive moderate complexity?",
        "Controlled synthetic ecology only.",
    ),
    "RICH": ScenarioDefinition(
        "RICH",
        "RICH",
        True,
        True,
        True,
        False,
        True,
        True,
        False,
        False,
        "Can a history-sensitive agent regulate multiple needs and relationships over a longer life?",
        "No claim of consciousness, personhood, or general intelligence.",
    ),
    "RICH_HARD": ScenarioDefinition(
        "RICH_HARD",
        "RICH_HARD",
        True,
        True,
        True,
        True,
        True,
        True,
        True,
        False,
        "Can a continually adapting substrate cope when old experience becomes misleading?",
        "Architecture comparison under controlled nonstationarity.",
    ),
}


def registry_manifest():
    return {
        key: value.to_dict()
        for key, value in sorted(SCENARIOS.items())
    }
