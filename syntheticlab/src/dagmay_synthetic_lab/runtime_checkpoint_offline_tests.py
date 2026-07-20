from __future__ import annotations

from copy import deepcopy
from pathlib import Path
import tempfile

from .bounded_game_goals import GoalCandidate
from .core import canonical_hash
from .goal_continuity import GoalContinuityState
from .goal_execution_gate import GoalExecutionGate, GoalExecutionRequest
from .goal_execution_snapshot import snapshot_execution_gate
from .relationship_claims import DETERMINISTIC_CLAUSE_SOURCE, RelationshipEvidence
from .relationship_history_selection import RelationshipHistoryItem
from .runtime_checkpoint import load_runtime, restore_runtime, snapshot_runtime, write_runtime_atomic


def run() -> dict[str, object]:
    evidence = RelationshipEvidence("EV-REL", "Mira", "Mira treated my wound", "POSITIVE", "Mira treated my wound", DETERMINISTIC_CLAUSE_SOURCE)
    history = (RelationshipHistoryItem(evidence, 4, 0.9),)
    goal_state = GoalContinuityState("G-BUILD", "G-BUILD", 2, 1)
    candidate = GoalCandidate("G-BUILD", "BUILD", ("EV-BUILD",), True, 0.4, 0.8, target_id="Wall-12")
    gate = GoalExecutionGate()
    request = GoalExecutionRequest("REQ-1", "G-BUILD", "Wall-12", ("EV-BUILD",), 7)
    gate.evaluate(request, current_world_revision=7, current_candidates=(candidate,), current_evidence_ids=("EV-BUILD",), reachable_target_ids=("Wall-12",))

    snapshot = snapshot_runtime(individual_id="IND-1", lineage_id="LIN-1", checkpoint_generation=12, world_revision=7, relationship_history=history, goal_continuity=goal_state, execution_gate=gate)
    restored = restore_runtime(snapshot, expected_individual_id="IND-1", expected_lineage_id="LIN-1", minimum_generation=12)
    assert restored.relationship_history == history
    assert restored.goal_continuity == goal_state
    assert restored.execution_gate.audit_state() == gate.audit_state()
    assert restored.execution_gate.evaluate(request, current_world_revision=7, current_candidates=(candidate,), current_evidence_ids=("EV-BUILD",), reachable_target_ids=("Wall-12",)).reason == "REPLAYED_REQUEST"

    rejected = 0
    tampered = deepcopy(snapshot)
    tampered["world_revision"] = 8
    mixed_identity = deepcopy(snapshot)
    mixed_identity["execution_component"] = snapshot_execution_gate(gate, individual_id="IND-OTHER", lineage_id="LIN-1")
    mixed_payload = {key: value for key, value in mixed_identity.items() if key != "state_hash"}
    mixed_identity["state_hash"] = canonical_hash(mixed_payload)
    for candidate_snapshot, kwargs in ((tampered, {}), (mixed_identity, {}), (snapshot, {"minimum_generation": 13})):
        try:
            restore_runtime(candidate_snapshot, **kwargs)
        except ValueError:
            rejected += 1
    assert rejected == 3

    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / "runtime.json"
        write_runtime_atomic(path, snapshot)
        assert load_runtime(path, minimum_generation=12).checkpoint_generation == 12
        path.with_name(path.name + ".tmp").write_text('{"partial":', encoding="utf-8")
        assert load_runtime(path).checkpoint_generation == 12

    return {
        "composite_round_trip_exact": True,
        "replay_protection_preserved": True,
        "cross_component_identity_mix_rejected": True,
        "rollback_generation_rejected": True,
        "outer_tamper_rejected": True,
        "interrupted_temp_preserved_last_good": True,
    }


if __name__ == "__main__":
    print(run())
