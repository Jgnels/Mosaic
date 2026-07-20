"""Identity-bound atomic persistence for the goal execution replay ledger."""

from __future__ import annotations

import json
import os
from pathlib import Path

from .core import canonical_hash
from .goal_execution_gate import GoalExecutionGate


SNAPSHOT_VERSION = "MOSAIC-GOAL-EXECUTION-LEDGER-1.0"


def snapshot_execution_gate(gate: GoalExecutionGate, *, individual_id: str, lineage_id: str) -> dict:
    if not individual_id or not lineage_id:
        raise ValueError("execution ledger identity and lineage are required")
    state = gate.audit_state()
    payload = {
        "snapshot_version": SNAPSHOT_VERSION,
        "type": "GoalExecutionLedger",
        "individual_id": individual_id,
        "lineage_id": lineage_id,
        "ledger": {key: list(value) for key, value in state.items()},
    }
    return {**payload, "state_hash": canonical_hash(payload)}


def restore_execution_gate(snapshot: dict, *, expected_individual_id: str | None = None, expected_lineage_id: str | None = None) -> GoalExecutionGate:
    required = {"snapshot_version", "type", "individual_id", "lineage_id", "ledger", "state_hash"}
    if set(snapshot) != required:
        raise ValueError("execution snapshot schema mismatch")
    if snapshot["snapshot_version"] != SNAPSHOT_VERSION or snapshot["type"] != "GoalExecutionLedger":
        raise ValueError("unsupported execution snapshot")
    if expected_individual_id is not None and snapshot["individual_id"] != expected_individual_id:
        raise ValueError("execution snapshot individual mismatch")
    if expected_lineage_id is not None and snapshot["lineage_id"] != expected_lineage_id:
        raise ValueError("execution snapshot lineage mismatch")
    payload = {key: value for key, value in snapshot.items() if key != "state_hash"}
    if canonical_hash(payload) != snapshot["state_hash"]:
        raise ValueError("execution snapshot integrity mismatch")
    return GoalExecutionGate.from_audit_state(snapshot["ledger"])


def write_execution_snapshot_atomic(path: Path, snapshot: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    with temporary.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write(json.dumps(snapshot, indent=2, sort_keys=True) + "\n")
        handle.flush()
        os.fsync(handle.fileno())
    temporary.replace(path)


def load_execution_snapshot(path: Path, **identity_expectations: str) -> GoalExecutionGate:
    try:
        snapshot = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError("execution snapshot is unreadable") from error
    return restore_execution_gate(snapshot, **identity_expectations)
