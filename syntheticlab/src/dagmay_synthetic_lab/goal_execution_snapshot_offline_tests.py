from __future__ import annotations

from copy import deepcopy
from pathlib import Path
import tempfile

from .bounded_game_goals import GoalCandidate
from .goal_execution_gate import GoalExecutionGate, GoalExecutionRequest
from .goal_execution_snapshot import load_execution_snapshot, restore_execution_gate, snapshot_execution_gate, write_execution_snapshot_atomic


def run() -> dict[str, object]:
    candidate = GoalCandidate("G-BUILD", "BUILD", ("EV-BUILD",), True, 0.4, 0.8, target_id="Wall-12")
    gate = GoalExecutionGate()
    request = GoalExecutionRequest("REQ-1", "G-BUILD", "Wall-12", ("EV-BUILD",), 7)
    assert gate.evaluate(request, current_world_revision=7, current_candidates=(candidate,), current_evidence_ids=("EV-BUILD",), reachable_target_ids=("Wall-12",)).status == "ACCEPT"
    rejected = GoalExecutionRequest("REQ-2", "G-BUILD", "Wall-12", ("EV-BUILD",), 6)
    assert gate.evaluate(rejected, current_world_revision=7, current_candidates=(candidate,), current_evidence_ids=("EV-BUILD",), reachable_target_ids=("Wall-12",)).status == "REPLAN"

    snapshot = snapshot_execution_gate(gate, individual_id="IND-001", lineage_id="LIN-001")
    restored = restore_execution_gate(snapshot, expected_individual_id="IND-001", expected_lineage_id="LIN-001")
    assert restored.audit_state() == gate.audit_state()
    assert restored.evaluate(request, current_world_revision=7, current_candidates=(candidate,), current_evidence_ids=("EV-BUILD",), reachable_target_ids=("Wall-12",)).reason == "REPLAYED_REQUEST"
    assert restored.evaluate(rejected, current_world_revision=7, current_candidates=(candidate,), current_evidence_ids=("EV-BUILD",), reachable_target_ids=("Wall-12",)).reason == "REPLAYED_REQUEST"
    assert restored.record_outcome("REQ-1", "SUCCESS").next_step == "COMPLETE"

    completed_snapshot = snapshot_execution_gate(restored, individual_id="IND-001", lineage_id="LIN-001")
    completed_restore = restore_execution_gate(completed_snapshot)
    try:
        completed_restore.record_outcome("REQ-1", "SUCCESS")
    except ValueError:
        duplicate_completion_rejected = True
    else:
        duplicate_completion_rejected = False
    assert duplicate_completion_rejected

    invalid = 0
    tampered = deepcopy(snapshot)
    tampered["ledger"]["completed_request_ids"].append("REQ-1")
    impossible = deepcopy(snapshot)
    impossible["ledger"]["completed_request_ids"] = ["REQ-UNKNOWN"]
    impossible_payload = {key: value for key, value in impossible.items() if key != "state_hash"}
    from .core import canonical_hash
    impossible["state_hash"] = canonical_hash(impossible_payload)
    for candidate_snapshot in (tampered, impossible):
        try:
            restore_execution_gate(candidate_snapshot)
        except ValueError:
            invalid += 1
    try:
        restore_execution_gate(snapshot, expected_lineage_id="LIN-WRONG")
    except ValueError:
        invalid += 1
    assert invalid == 3

    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / "execution-ledger.json"
        write_execution_snapshot_atomic(path, completed_snapshot)
        assert load_execution_snapshot(path).audit_state() == completed_restore.audit_state()
        path.with_name(path.name + ".tmp").write_text('{"partial":', encoding="utf-8")
        assert load_execution_snapshot(path).audit_state() == completed_restore.audit_state()

    return {
        "ledger_round_trip_exact": True,
        "accepted_and_rejected_replays_blocked_after_restart": True,
        "accepted_unfinished_request_completable_after_restart": True,
        "completed_request_not_completable_twice": True,
        "tamper_invariant_identity_rejections": invalid,
        "interrupted_temp_preserved_last_good": True,
    }


if __name__ == "__main__":
    print(run())
