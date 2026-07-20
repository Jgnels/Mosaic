from __future__ import annotations

from typing import Protocol, runtime_checkable, Any


@runtime_checkable
class DevelopmentEnvironment(Protocol):
    """Minimal environment-independent contract for developmental experiments."""

    seed: int
    config: Any

    def observe(self):
        """Return only subject-visible perception."""
        ...

    def step(self, action: str):
        """Advance canonical world state and return canonical evaluation event."""
        ...

    def clone(self):
        """Return an exact independent continuation candidate."""
        ...

    def state_hash(self) -> str:
        """Hash all canonical state needed for deterministic continuation."""
        ...


@runtime_checkable
class DevelopmentalAgent(Protocol):
    """Minimal persistent-agent contract used by environment harnesses."""

    def choose_action(self, observation):
        ...

    def observe_event(self, observation, subject_visible_event):
        ...

    def clone(self):
        ...

    def state_hash(self) -> str:
        ...


def environment_contract_check(env) -> dict:
    required = (
        "observe",
        "step",
        "clone",
        "state_hash",
    )
    missing = [
        name for name in required
        if not callable(getattr(env, name, None))
    ]
    return {
        "conforms": not missing,
        "missing": tuple(missing),
        "has_seed": hasattr(env, "seed"),
        "has_config": hasattr(env, "config"),
    }


def agent_contract_check(agent) -> dict:
    required = (
        "choose_action",
        "observe_event",
        "clone",
        "state_hash",
    )
    missing = [
        name for name in required
        if not callable(getattr(agent, name, None))
    ]
    return {
        "conforms": not missing,
        "missing": tuple(missing),
    }
