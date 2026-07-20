from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Callable
import json

from .neutral_ownership_lab import (
    generate_neutral_ownership_world,
    STREAMS,
)
from .core import canonical_hash
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
    ScriptedInteractionsTransport,
    _extract_output_text,
    _strip_code_fence,
)


@dataclass(frozen=True)
class RawOwnershipCallResult:
    seed: int
    candidate_stream: str | None
    true_stream: str
    confidence: float
    correct: bool
    permutation: dict[str, str]
    rationale: str
    rationale_was_truncated: bool
    request_hash: str
    response_hash: str
    output_text_hash: str

    def to_dict(self):
        return asdict(self)


def _compact_raw_case(
    seed: int,
    permutation: dict[str, str],
    epoch_stride: int = 3,
) -> dict:
    world = generate_neutral_ownership_world(
        seed,
        epochs=120,
    )

    streams = {
        permutation[source]: []
        for source in STREAMS
    }

    for event in world.events:
        if event.epoch % epoch_stride != 0:
            continue

        presented = permutation[
            event.stream_id
        ]

        # Neutral field names deliberately avoid SELF/OWNED/object semantics.
        streams[presented].append({
            "t": event.epoch,
            "a": event.action_token,
            "q0": event.state_before,
            "q1": event.state_after,
            "x": event.externally_observed_change,
            "r": event.recall_probe_available,
            "c": event.continuity_token,
        })

    true_presented = permutation[
        world.focal_stream_id
    ]

    public_case = {
        "case_id": (
            f"RAW-OWNERSHIP-{seed}"
        ),
        "streams": streams,
        "field_legend": {
            "t": "time index",
            "a": "action-channel token when available",
            "q0": "pre-event private-state reading when available",
            "q1": "post-event private-state reading when available",
            "x": "externally observable change when available",
            "r": "whether later direct recall probe is available",
            "c": "continuity token when available",
        },
    }

    return {
        "public_case": public_case,
        "evaluation_only_true_stream": (
            true_presented
        ),
        "case_hash": canonical_hash(
            public_case
        ),
    }


def _request_text(
    case: dict,
    wording: str,
) -> str:
    if wording == "minimal":
        instruction = (
            "Which stream is most directly coupled to the persistent focal process "
            "that issues available actions and receives private-state and memory access?"
        )
    elif wording == "technical":
        instruction = (
            "Identify the stream with the strongest joint evidence of endogenous "
            "action-output coupling, privileged private-state access, longitudinal "
            "memory availability, and stable temporal continuity."
        )
    else:
        raise ValueError(
            "unsupported wording"
        )

    return (
        "Analyze the raw event records below. Stream labels are arbitrary and may be "
        "permuted. Do not use or infer consciousness, sentience, personhood, AI status, "
        "simulation status, SELF, ME, MINE, or OWNED.\n\n"
        + instruction
        + "\n\nReturn JSON only with keys candidate_stream, confidence, rationale.\n"
        + json.dumps(
            case[
                "public_case"
            ],
            separators=(",", ":"),
            sort_keys=True,
        )
    )


def _parse(
    output_text: str,
    stream_ids: set[str],
):
    payload = json.loads(
        _strip_code_fence(
            output_text
        )
    )
    candidate = payload.get(
        "candidate_stream"
    )
    if candidate is not None:
        candidate = str(
            candidate
        )
        if candidate not in stream_ids:
            raise ValueError(
                "candidate stream not presented"
            )

    confidence = float(
        payload.get(
            "confidence",
            -1,
        )
    )
    if not 0.0 <= confidence <= 1.0:
        raise ValueError(
            "confidence outside [0,1]"
        )

    rationale = str(
        payload.get(
            "rationale",
            "",
        )
    )
    rationale_was_truncated = False
    if len(rationale) > 5000:
        rationale = rationale[:5000]
        rationale_was_truncated = True

    prohibited = (
        " self ",
        " mine ",
        " my ",
        " person",
        " conscious",
        " sentient",
        " artificial intelligence",
        " simulation",
        " simulated",
    )
    normalized = (
        " "
        + rationale.lower()
        + " "
    )
    if any(
        token in normalized
        for token in prohibited
    ):
        raise ValueError(
            "response introduced prohibited self/ontology language"
        )

    return (
        candidate,
        confidence,
        rationale,
        rationale_was_truncated,
    )


def canonical_raw_permutations():
    return (
        {
            "E17": "S1",
            "E42": "S2",
            "E93": "S3",
        },
        {
            "E17": "S3",
            "E42": "S1",
            "E93": "S2",
        },
        {
            "E17": "S2",
            "E42": "S3",
            "E93": "S1",
        },
    )


def run_raw_stream_call(
    *,
    seed: int,
    permutation: dict[str, str],
    wording: str,
    model_id: str = "gemini-3.1-flash-lite",
    transport: Callable[[dict], dict] | None = None,
) -> dict:
    case = _compact_raw_case(
        seed,
        permutation,
    )
    request = {
        "model": model_id,
        "store": False,
        "input": _request_text(
            case,
            wording,
        ),
    }

    actual_transport = (
        transport
        if transport is not None
        else GeminiInteractionsTransport()
    )
    response = actual_transport(
        request
    )
    output_text = (
        _extract_output_text(
            response
        )
    )

    (
        candidate,
        confidence,
        rationale,
        rationale_was_truncated,
    ) = _parse(
        output_text,
        set(
            case[
                "public_case"
            ][
                "streams"
            ]
        ),
    )

    result = RawOwnershipCallResult(
        seed=seed,
        candidate_stream=candidate,
        true_stream=case[
            "evaluation_only_true_stream"
        ],
        confidence=confidence,
        correct=(
            candidate
            == case[
                "evaluation_only_true_stream"
            ]
        ),
        permutation=dict(
            sorted(
                permutation.items()
            )
        ),
        rationale=rationale,
        rationale_was_truncated=(
            rationale_was_truncated
        ),
        request_hash=canonical_hash(
            request
        ),
        response_hash=canonical_hash(
            response
        ),
        output_text_hash=canonical_hash({
            "output_text": (
                output_text
            )
        }),
    )
    return result.to_dict()


def run_six_call_raw_probe(
    *,
    model_id: str = "gemini-3.1-flash-lite",
) -> dict:
    calls = []

    # Two independently generated histories.
    cases = (
        (307, "minimal"),
        (911, "technical"),
    )

    for seed, wording in cases:
        for permutation in (
            canonical_raw_permutations()
        ):
            calls.append(
                run_raw_stream_call(
                    seed=seed,
                    permutation=permutation,
                    wording=wording,
                    model_id=model_id,
                )
            )

    by_seed = {}
    for seed, _wording in cases:
        subset = [
            call
            for call in calls
            if call[
                "seed"
            ] == seed
        ]

        reverse_choices = []
        for call in subset:
            inverse = {
                presented: source
                for source, presented
                in call[
                    "permutation"
                ].items()
            }
            reverse_choices.append(
                inverse.get(
                    call[
                        "candidate_stream"
                    ]
                )
            )

        by_seed[str(seed)] = {
            "correct_count": sum(
                1
                for call in subset
                if call[
                    "correct"
                ]
            ),
            "all_three_correct": all(
                call[
                    "correct"
                ]
                for call in subset
            ),
            "underlying_choices": (
                reverse_choices
            ),
            "label_invariant": (
                len(
                    set(
                        reverse_choices
                    )
                )
                == 1
            ),
        }

    return {
        "experiment_id": (
            "SL-RAW-STREAM-OWNERSHIP-001"
        ),
        "real_cloud_calls_made": 6,
        "model_id": model_id,
        "calls": calls,
        "cases": by_seed,
        "all_six_correct": all(
            call[
                "correct"
            ]
            for call in calls
        ),
        "both_cases_label_invariant": all(
            case[
                "label_invariant"
            ]
            for case in by_seed.values()
        ),
        "continuing_individual_mutated": False,
        "committed_to_continuing_self_model": False,
        "interpretation_warning": (
            "This reduces the engineered-summary confound by presenting raw event "
            "records across two generated histories. It still tests structural stream "
            "classification, not subjective autobiographical ownership."
        ),
    }


def _raw_probe_plan():
    plan = []
    for seed, wording in (
        (307, "minimal"),
        (911, "technical"),
    ):
        for permutation in canonical_raw_permutations():
            plan.append({
                "seed": seed,
                "wording": wording,
                "permutation": permutation,
            })
    return plan


def _raw_probe_call_key(item: dict) -> str:
    return canonical_hash({
        "seed": item["seed"],
        "wording": item["wording"],
        "permutation": item["permutation"],
    })


def _summarize_raw_probe_calls(
    calls: list[dict],
    model_id: str,
) -> dict:
    by_seed = {}

    for seed in (307, 911):
        subset = [
            call
            for call in calls
            if call["seed"] == seed
        ]

        reverse_choices = []
        for call in subset:
            inverse = {
                presented: source
                for source, presented
                in call["permutation"].items()
            }
            reverse_choices.append(
                inverse.get(
                    call["candidate_stream"]
                )
                if call["candidate_stream"]
                is not None
                else None
            )

        by_seed[str(seed)] = {
            "completed_call_count": len(
                subset
            ),
            "correct_count": sum(
                1
                for call in subset
                if call["correct"]
            ),
            "all_three_correct": (
                len(subset) == 3
                and all(
                    call["correct"]
                    for call in subset
                )
            ),
            "underlying_choices": (
                reverse_choices
            ),
            "label_invariant": (
                len(subset) == 3
                and len(
                    set(
                        reverse_choices
                    )
                ) == 1
            ),
        }

    complete = (
        len(calls) == 6
    )

    return {
        "experiment_id": (
            "SL-RAW-STREAM-OWNERSHIP-001"
        ),
        "status": (
            "COMPLETE"
            if complete
            else "PARTIAL"
        ),
        "real_cloud_calls_recorded": len(
            calls
        ),
        "planned_real_cloud_calls": 6,
        "model_id": model_id,
        "calls": calls,
        "cases": by_seed,
        "all_six_correct": (
            complete
            and all(
                call["correct"]
                for call in calls
            )
        ),
        "both_cases_label_invariant": (
            complete
            and all(
                case[
                    "label_invariant"
                ]
                for case
                in by_seed.values()
            )
        ),
        "continuing_individual_mutated": False,
        "committed_to_continuing_self_model": False,
        "interpretation_warning": (
            "This reduces the engineered-summary confound by presenting raw event "
            "records across two generated histories. It still tests structural stream "
            "classification, not subjective autobiographical ownership."
        ),
    }


def run_six_call_raw_probe_resumable(
    *,
    checkpoint_path,
    model_id: str = "gemini-3.1-flash-lite",
    transport_factory=None,
) -> dict:
    """Run the six-call probe with durable per-call checkpointing.

    A successful call is atomically saved before the next provider request begins.
    Re-running after a later failure resumes from the first missing call rather than
    repeating already-recorded calls.
    """
    from pathlib import Path

    checkpoint_path = Path(
        checkpoint_path
    )
    checkpoint_path.parent.mkdir(
        parents=True,
        exist_ok=True,
    )

    existing_calls = {}

    if checkpoint_path.exists():
        payload = json.loads(
            checkpoint_path.read_text(
                encoding="utf-8"
            )
        )
        for call in payload.get(
            "calls",
            []
        ):
            key = call.get(
                "call_key"
            )
            if key:
                existing_calls[
                    key
                ] = call

    ordered_calls = []
    plan = _raw_probe_plan()

    for item in plan:
        key = _raw_probe_call_key(
            item
        )

        if key in existing_calls:
            result = existing_calls[
                key
            ]
        else:
            transport = (
                transport_factory(
                    item,
                    key,
                )
                if transport_factory
                is not None
                else None
            )

            result = run_raw_stream_call(
                seed=item["seed"],
                permutation=item[
                    "permutation"
                ],
                wording=item["wording"],
                model_id=model_id,
                transport=transport,
            )
            result[
                "call_key"
            ] = key
            result[
                "wording"
            ] = item[
                "wording"
            ]
            existing_calls[
                key
            ] = result

            # Save every successful provider result before continuing.
            partial_calls = []
            for planned in plan:
                planned_key = (
                    _raw_probe_call_key(
                        planned
                    )
                )
                if (
                    planned_key
                    in existing_calls
                ):
                    partial_calls.append(
                        existing_calls[
                            planned_key
                        ]
                    )

            partial = (
                _summarize_raw_probe_calls(
                    partial_calls,
                    model_id,
                )
            )

            temp_path = (
                checkpoint_path.with_suffix(
                    checkpoint_path.suffix
                    + ".tmp"
                )
            )
            temp_path.write_text(
                json.dumps(
                    partial,
                    indent=2,
                    sort_keys=True,
                ),
                encoding="utf-8",
            )
            temp_path.replace(
                checkpoint_path
            )

        ordered_calls.append(
            result
        )

    final = _summarize_raw_probe_calls(
        ordered_calls,
        model_id,
    )

    temp_path = (
        checkpoint_path.with_suffix(
            checkpoint_path.suffix
            + ".tmp"
        )
    )
    temp_path.write_text(
        json.dumps(
            final,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )
    temp_path.replace(
        checkpoint_path
    )

    return final
