from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, Iterable, Mapping, Protocol, runtime_checkable


@dataclass(frozen=True)
class Observation:
    """Environment-neutral observation envelope.

    `payload` is intentionally structured and semantically neutral.
    `oracle` is evaluator-only ground truth and must not be exposed to the learner
    unless an experiment explicitly defines a privileged-information condition.
    """
    timestep: int
    payload: Mapping[str, Any]
    private_payload: Mapping[str, Any]
    oracle: Mapping[str, Any]


@dataclass(frozen=True)
class Action:
    actor_id: str
    action_type: str
    parameters: Mapping[str, Any]


@dataclass(frozen=True)
class StepResult:
    observation: Observation
    reward_signal: float | None
    done: bool
    event_ids: tuple[str, ...]


@runtime_checkable
class IEnvironmentAdapter(Protocol):
    @property
    def environment_id(self) -> str: ...

    @property
    def environment_version(self) -> str: ...

    def reset(self, seed: int) -> Observation: ...

    def legal_actions(self, actor_id: str) -> Iterable[Action]: ...

    def step(self, action: Action) -> StepResult: ...

    def oracle_state(self) -> Mapping[str, Any]:
        """Evaluator-only world state. Never a learner input by default."""
        ...


@runtime_checkable
class IDevelopmentalLearner(Protocol):
    @property
    def mechanism_id(self) -> str: ...

    @property
    def mechanism_version(self) -> str: ...

    def observe(self, observation: Observation) -> None: ...

    def propose_action(self, legal_actions: Iterable[Action]) -> Action: ...

    def export_state(self) -> Dict[str, Any]: ...
