"""Atomic composite checkpoint for Mosaic relationship, goal, and execution state."""

from __future__ import annotations

from dataclasses import asdict, dataclass
import json
import os
from pathlib import Path
from typing import Iterable

from .core import canonical_hash
from .goal_continuity import GoalContinuityState
from .goal_execution_gate import GoalExecutionGate
from .goal_execution_snapshot import restore_execution_gate, snapshot_execution_gate
from .relationship_history_selection import RelationshipHistoryItem
from .relationship_history_snapshot import restore_relationship_history, snapshot_relationship_history


SNAPSHOT_VERSION = "MOSAIC-CHARACTER-RUNTIME-1.0"


@dataclass(frozen=True)
class RestoredRuntime:
    individual_id: str
    lineage_id: str
    checkpoint_generation: int
    world_revision: int
    relationship_history: tuple[RelationshipHistoryItem, ...]
    goal_continuity: GoalContinuityState
    execution_gate: GoalExecutionGate


def snapshot_runtime(
    *,
    individual_id: str,
    lineage_id: str,
    checkpoint_generation: int,
    world_revision: int,
    relationship_history: Iterable[RelationshipHistoryItem],
    goal_continuity: GoalContinuityState,
    execution_gate: GoalExecutionGate,
) -> dict:
    if checkpoint_generation < 0 or world_revision < 0:
        raise ValueError("runtime generations must be non-negative")
    payload = {
        "snapshot_version": SNAPSHOT_VERSION,
        "type": "CharacterRuntime",
        "individual_id": individual_id,
        "lineage_id": lineage_id,
        "checkpoint_generation": checkpoint_generation,
        "world_revision": world_revision,
        "relationship_component": snapshot_relationship_history(relationship_history, individual_id=individual_id, lineage_id=lineage_id),
        "goal_continuity": asdict(goal_continuity),
        "execution_component": snapshot_execution_gate(execution_gate, individual_id=individual_id, lineage_id=lineage_id),
    }
    return {**payload, "state_hash": canonical_hash(payload)}


def restore_runtime(
    snapshot: dict,
    *,
    expected_individual_id: str | None = None,
    expected_lineage_id: str | None = None,
    minimum_generation: int | None = None,
) -> RestoredRuntime:
    required = {"snapshot_version", "type", "individual_id", "lineage_id", "checkpoint_generation", "world_revision", "relationship_component", "goal_continuity", "execution_component", "state_hash"}
    if set(snapshot) != required:
        raise ValueError("runtime checkpoint schema mismatch")
    if snapshot["snapshot_version"] != SNAPSHOT_VERSION or snapshot["type"] != "CharacterRuntime":
        raise ValueError("unsupported runtime checkpoint")
    individual_id = str(snapshot["individual_id"])
    lineage_id = str(snapshot["lineage_id"])
    if expected_individual_id is not None and individual_id != expected_individual_id:
        raise ValueError("runtime individual mismatch")
    if expected_lineage_id is not None and lineage_id != expected_lineage_id:
        raise ValueError("runtime lineage mismatch")
    generation = int(snapshot["checkpoint_generation"])
    revision = int(snapshot["world_revision"])
    if generation < 0 or revision < 0:
        raise ValueError("invalid runtime generation")
    if minimum_generation is not None and generation < minimum_generation:
        raise ValueError("runtime checkpoint rollback detected")
    payload = {key: value for key, value in snapshot.items() if key != "state_hash"}
    if canonical_hash(payload) != snapshot["state_hash"]:
        raise ValueError("runtime checkpoint integrity mismatch")
    history = restore_relationship_history(snapshot["relationship_component"], expected_individual_id=individual_id, expected_lineage_id=lineage_id)
    execution = restore_execution_gate(snapshot["execution_component"], expected_individual_id=individual_id, expected_lineage_id=lineage_id)
    if set(snapshot["goal_continuity"]) != {"committed_goal_id", "active_goal_id", "switch_count", "interruption_count"}:
        raise ValueError("goal continuity schema mismatch")
    goal = GoalContinuityState(**snapshot["goal_continuity"])
    return RestoredRuntime(individual_id, lineage_id, generation, revision, history, goal, execution)


def write_runtime_atomic(path: Path, snapshot: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    with temporary.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write(json.dumps(snapshot, indent=2, sort_keys=True) + "\n")
        handle.flush()
        os.fsync(handle.fileno())
    temporary.replace(path)


def load_runtime(path: Path, **expectations: object) -> RestoredRuntime:
    try:
        snapshot = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError("runtime checkpoint is unreadable") from error
    return restore_runtime(snapshot, **expectations)
