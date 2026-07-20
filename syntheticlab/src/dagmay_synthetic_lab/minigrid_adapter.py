from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Any
import importlib.util

from .core import canonical_hash


MINIGRID_ACTION_LABELS = {
    0: "TURN_LEFT",
    1: "TURN_RIGHT",
    2: "FORWARD",
    3: "PICKUP",
    4: "DROP",
    5: "TOGGLE",
    6: "DONE",
}


@dataclass(frozen=True)
class ExternalObservation:
    step: int
    direction: int
    image_digest: str
    mission_token: str
    opaque_features: tuple[str, ...]
    terminated: bool = False
    truncated: bool = False

    def context_token(self) -> str:
        return canonical_hash({
            "direction": self.direction,
            "image_digest": self.image_digest,
            "mission_token": self.mission_token,
            "opaque_features": self.opaque_features,
        })

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ExternalTransition:
    step: int
    action_index: int
    action_label: str
    reward: float
    terminated: bool
    truncated: bool
    next_observation: ExternalObservation

    def to_dict(self):
        return asdict(self)


def dependency_status() -> dict:
    return {
        "gymnasium": (
            importlib.util.find_spec("gymnasium")
            is not None
        ),
        "minigrid": (
            importlib.util.find_spec("minigrid")
            is not None
        ),
    }


def _normalize_image(image: Any):
    if hasattr(image, "tolist"):
        image = image.tolist()
    return image


def sanitize_minigrid_observation(
    observation: dict,
    step: int,
    terminated: bool = False,
    truncated: bool = False,
    mission_mode: str = "opaque",
) -> ExternalObservation:
    if not isinstance(observation, dict):
        raise TypeError(
            "MiniGrid observation must be a dict."
        )

    image = _normalize_image(
        observation.get("image")
    )
    direction = int(
        observation.get("direction", 0)
    )
    mission = str(
        observation.get("mission", "")
    )

    if mission_mode == "opaque":
        mission_token = (
            "MISSION-"
            + canonical_hash(
                {"mission": mission}
            )[:12]
        )
    elif mission_mode == "text":
        mission_token = mission
    elif mission_mode == "none":
        mission_token = "MISSION-OMITTED"
    else:
        raise ValueError(
            "mission_mode must be opaque, text, or none"
        )

    features = [
        f"DIRECTION:{direction}",
        f"MISSION:{mission_token}",
    ]

    if isinstance(image, list):
        for row_index, row in enumerate(image):
            if not isinstance(row, list):
                continue
            for col_index, cell in enumerate(row):
                if hasattr(cell, "tolist"):
                    cell = cell.tolist()
                if isinstance(cell, (list, tuple)):
                    encoded = "-".join(
                        str(x) for x in cell
                    )
                else:
                    encoded = str(cell)
                features.append(
                    f"CELL:{row_index}:{col_index}:{encoded}"
                )

    return ExternalObservation(
        step=step,
        direction=direction,
        image_digest=canonical_hash(
            {"partial_image": image}
        ),
        mission_token=mission_token,
        opaque_features=tuple(features),
        terminated=terminated,
        truncated=truncated,
    )


class MiniGridEnvironmentAdapter:
    """Environment adapter around a Gymnasium-compatible MiniGrid environment.

    Mission text is opaque by default so pretrained semantic knowledge is not
    accidentally introduced into LLM-free external-environment experiments.
    """

    def __init__(
        self,
        env,
        env_id: str,
        mission_mode: str = "opaque",
    ):
        self.env = env
        self.env_id = env_id
        self.mission_mode = mission_mode
        self.step_count = 0
        self.seed: int | None = None
        self.action_history: list[int] = []
        self.last_observation: ExternalObservation | None = None

    @classmethod
    def create(
        cls,
        env_id: str,
        mission_mode: str = "opaque",
        render_mode: str | None = None,
    ):
        status = dependency_status()
        if not all(status.values()):
            raise RuntimeError(
                "MiniGrid external pilot requires "
                "`pip install minigrid`."
            )

        import gymnasium as gym
        import minigrid  # noqa: F401 - registers envs

        env = gym.make(
            env_id,
            render_mode=render_mode,
        )
        return cls(
            env,
            env_id,
            mission_mode,
        )

    def reset(
        self,
        seed: int,
    ) -> ExternalObservation:
        raw, _info = self.env.reset(
            seed=seed
        )
        self.seed = seed
        self.step_count = 0
        self.action_history = []
        self.last_observation = (
            sanitize_minigrid_observation(
                raw,
                step=0,
                mission_mode=self.mission_mode,
            )
        )
        return self.last_observation

    def step(
        self,
        action_index: int,
    ) -> ExternalTransition:
        if action_index not in MINIGRID_ACTION_LABELS:
            raise ValueError(
                f"invalid MiniGrid action: {action_index}"
            )

        raw, reward, terminated, truncated, _info = (
            self.env.step(action_index)
        )
        self.step_count += 1
        self.action_history.append(
            int(action_index)
        )

        obs = sanitize_minigrid_observation(
            raw,
            step=self.step_count,
            terminated=bool(terminated),
            truncated=bool(truncated),
            mission_mode=self.mission_mode,
        )
        self.last_observation = obs

        return ExternalTransition(
            step=self.step_count,
            action_index=int(action_index),
            action_label=(
                MINIGRID_ACTION_LABELS[
                    int(action_index)
                ]
            ),
            reward=float(reward),
            terminated=bool(terminated),
            truncated=bool(truncated),
            next_observation=obs,
        )

    def replay_checkpoint(self) -> dict:
        if self.seed is None:
            raise RuntimeError(
                "environment has not been reset"
            )
        payload = {
            "type": "MINIGRID_REPLAY_CHECKPOINT",
            "env_id": self.env_id,
            "mission_mode": self.mission_mode,
            "seed": self.seed,
            "actions": list(
                self.action_history
            ),
        }
        return {
            **payload,
            "checkpoint_hash": canonical_hash(
                payload
            ),
        }

    def close(self):
        close = getattr(
            self.env,
            "close",
            None,
        )
        if callable(close):
            close()


def restore_by_replay(
    adapter_factory,
    checkpoint: dict,
) -> MiniGridEnvironmentAdapter:
    payload = {
        "type": checkpoint["type"],
        "env_id": checkpoint["env_id"],
        "mission_mode": checkpoint[
            "mission_mode"
        ],
        "seed": checkpoint["seed"],
        "actions": checkpoint["actions"],
    }
    if canonical_hash(payload) != checkpoint[
        "checkpoint_hash"
    ]:
        raise ValueError(
            "external replay checkpoint hash mismatch"
        )

    adapter = adapter_factory(
        checkpoint["env_id"],
        checkpoint["mission_mode"],
    )
    adapter.reset(
        int(checkpoint["seed"])
    )
    for action in checkpoint["actions"]:
        transition = adapter.step(
            int(action)
        )
        if (
            transition.terminated
            or transition.truncated
        ):
            break
    return adapter
