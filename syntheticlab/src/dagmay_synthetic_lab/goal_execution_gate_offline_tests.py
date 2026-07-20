from __future__ import annotations

from .bounded_game_goals import GoalCandidate
from .goal_execution_gate import GoalExecutionGate, GoalExecutionRequest


def run() -> dict[str, object]:
    candidate = GoalCandidate("G-BUILD", "BUILD", ("EV-BUILD",), True, 0.4, 0.8, target_id="Wall-12")

    gate = GoalExecutionGate()
    accepted_request = GoalExecutionRequest("REQ-1", "G-BUILD", "Wall-12", ("EV-BUILD",), 7)
    accepted = gate.evaluate(accepted_request, current_world_revision=7, current_candidates=(candidate,), current_evidence_ids=("EV-BUILD",), reachable_target_ids=("Wall-12",))
    assert accepted.status == "ACCEPT"
    replay = gate.evaluate(accepted_request, current_world_revision=7, current_candidates=(candidate,), current_evidence_ids=("EV-BUILD",), reachable_target_ids=("Wall-12",))
    assert replay.status == "REPLAN" and replay.reason == "REPLAYED_REQUEST"
    outcome = gate.record_outcome("REQ-1", "SUCCESS")
    assert outcome.next_step == "COMPLETE"
    try:
        gate.record_outcome("REQ-1", "SUCCESS")
    except ValueError:
        duplicate_outcome_rejected = True
    else:
        duplicate_outcome_rejected = False
    assert duplicate_outcome_rejected

    cases = (
        (GoalExecutionRequest("REQ-2", "G-BUILD", "Wall-12", ("EV-BUILD",), 6), 7, (candidate,), ("EV-BUILD",), ("Wall-12",), "WORLD_REVISION_CHANGED"),
        (GoalExecutionRequest("REQ-3", "G-BUILD", "Wall-12", ("EV-BUILD",), 7), 7, (), ("EV-BUILD",), ("Wall-12",), "GOAL_NO_LONGER_FEASIBLE"),
        (GoalExecutionRequest("REQ-4", "G-BUILD", "Wall-13", ("EV-BUILD",), 7), 7, (candidate,), ("EV-BUILD",), ("Wall-13",), "TARGET_CHANGED"),
        (GoalExecutionRequest("REQ-5", "G-BUILD", "Wall-12", ("EV-OTHER",), 7), 7, (candidate,), ("EV-OTHER",), ("Wall-12",), "EVIDENCE_CONTRACT_CHANGED"),
        (GoalExecutionRequest("REQ-6", "G-BUILD", "Wall-12", ("EV-BUILD",), 7), 7, (candidate,), (), ("Wall-12",), "EVIDENCE_NO_LONGER_CURRENT"),
        (GoalExecutionRequest("REQ-7", "G-BUILD", "Wall-12", ("EV-BUILD",), 7), 7, (candidate,), ("EV-BUILD",), (), "TARGET_UNREACHABLE"),
    )
    rejected = 0
    for request, revision, candidates, evidence, reachable, reason in cases:
        decision = gate.evaluate(request, current_world_revision=revision, current_candidates=candidates, current_evidence_ids=evidence, reachable_target_ids=reachable)
        assert decision.status == "REPLAN" and decision.reason == reason
        rejected += 1

    failure_gate = GoalExecutionGate()
    failure_gate.evaluate(GoalExecutionRequest("REQ-8", "G-BUILD", "Wall-12", ("EV-BUILD",), 7), current_world_revision=7, current_candidates=(candidate,), current_evidence_ids=("EV-BUILD",), reachable_target_ids=("Wall-12",))
    assert failure_gate.record_outcome("REQ-8", "TARGET_GONE").next_step == "REPLAN"
    assert set(gate.audit_state()) == {"evaluated_request_ids", "accepted_request_ids", "completed_request_ids"}
    return {
        "valid_request_accepted": True,
        "time_of_check_failures_replanned": rejected,
        "request_replay_rejected": True,
        "duplicate_outcome_rejected": True,
        "failed_job_routes_to_replan": True,
        "gate_issues_jobs": False,
    }


if __name__ == "__main__":
    print(run())
