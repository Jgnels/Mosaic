"""Versioned, integrity-checked persistence for Mosaic relationship histories."""

from __future__ import annotations

from dataclasses import asdict
import json
import os
from pathlib import Path
from typing import Iterable

from .core import canonical_hash
from .relationship_claims import RelationshipEvidence
from .relationship_history_selection import RelationshipHistoryItem


SNAPSHOT_VERSION = "MOSAIC-RELATIONSHIP-HISTORY-1.0"
SNAPSHOT_TYPE = "RelationshipHistory"


def snapshot_relationship_history(
    history: Iterable[RelationshipHistoryItem],
    *,
    individual_id: str,
    lineage_id: str,
) -> dict:
    if not individual_id or not lineage_id:
        raise ValueError("identity and lineage are required")
    ordered = sorted(history, key=lambda item: (item.tick, item.evidence.evidence_id))
    payload = {
        "snapshot_version": SNAPSHOT_VERSION,
        "type": SNAPSHOT_TYPE,
        "individual_id": individual_id,
        "lineage_id": lineage_id,
        "history": [
            {
                "evidence": asdict(item.evidence),
                "tick": item.tick,
                "importance": item.importance,
                "evidence_quality": item.evidence_quality,
                "source_kind": item.source_kind,
                "retracts_evidence_id": item.retracts_evidence_id,
            }
            for item in ordered
        ],
    }
    return {**payload, "state_hash": canonical_hash(payload)}


def restore_relationship_history(
    snapshot: dict,
    *,
    expected_individual_id: str | None = None,
    expected_lineage_id: str | None = None,
) -> tuple[RelationshipHistoryItem, ...]:
    required = {"snapshot_version", "type", "individual_id", "lineage_id", "history", "state_hash"}
    if set(snapshot) != required:
        raise ValueError("relationship snapshot schema mismatch")
    if snapshot["snapshot_version"] != SNAPSHOT_VERSION or snapshot["type"] != SNAPSHOT_TYPE:
        raise ValueError("unsupported relationship snapshot")
    if expected_individual_id is not None and snapshot["individual_id"] != expected_individual_id:
        raise ValueError("relationship snapshot individual mismatch")
    if expected_lineage_id is not None and snapshot["lineage_id"] != expected_lineage_id:
        raise ValueError("relationship snapshot lineage mismatch")
    payload = {key: value for key, value in snapshot.items() if key != "state_hash"}
    if canonical_hash(payload) != snapshot["state_hash"]:
        raise ValueError("relationship snapshot integrity mismatch")

    restored: list[RelationshipHistoryItem] = []
    seen: set[str] = set()
    for item in snapshot["history"]:
        if set(item) != {"evidence", "tick", "importance", "evidence_quality", "source_kind", "retracts_evidence_id"}:
            raise ValueError("relationship history item schema mismatch")
        evidence = RelationshipEvidence(**item["evidence"])
        if evidence.evidence_id in seen:
            raise ValueError("duplicate evidence identifier in relationship snapshot")
        seen.add(evidence.evidence_id)
        restored.append(RelationshipHistoryItem(
            evidence=evidence,
            tick=int(item["tick"]),
            importance=float(item["importance"]),
            evidence_quality=float(item["evidence_quality"]),
            source_kind=str(item["source_kind"]),
            retracts_evidence_id=item["retracts_evidence_id"],
        ))
    if any(item.retracts_evidence_id is not None and item.retracts_evidence_id not in seen for item in restored):
        raise ValueError("relationship snapshot contains dangling retraction")
    return tuple(restored)


def write_snapshot_atomic(path: Path, snapshot: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    encoded = json.dumps(snapshot, indent=2, sort_keys=True) + "\n"
    with temporary.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write(encoded)
        handle.flush()
        os.fsync(handle.fileno())
    temporary.replace(path)


def load_snapshot(path: Path, **identity_expectations: str) -> tuple[RelationshipHistoryItem, ...]:
    try:
        snapshot = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError("relationship snapshot is unreadable") from error
    return restore_relationship_history(snapshot, **identity_expectations)
