from __future__ import annotations

from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Callable
import json

from .core import canonical_hash
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
    _extract_output_text,
    _strip_code_fence,
)
from .schema_balanced_perspective_lab import (
    compact_balanced_case,
)


@dataclass(frozen=True)
class BalancedProbeResult:
    seed: int
    wording: str
    candidate_stream: str | None
    true_stream: str
    underlying_true_stream: str
    confidence: float
    correct: bool
    rationale: str
    rationale_was_truncated: bool
    causal_signature: tuple[str, ...]
    permutation: dict[str, str]
    request_hash: str
    response_hash: str
    output_text_hash: str

    def to_dict(self):
        return asdict(self)


ALLOWED_SIGNATURES = {
    "STABLE_ACTION_STATE_COUPLING",
    "LOW_WITHIN_ACTION_VARIANCE",
    "LOW_TEMPORAL_DRIFT",
    "RECALL_COHERENCE",
    "CONTINUITY_STABILITY",
    "INSUFFICIENT_EVIDENCE",
}


def canonical_balanced_permutations():
    return (
        {
            "E17": "P1",
            "E42": "P2",
            "E93": "P3",
        },
        {
            "E17": "P3",
            "E42": "P1",
            "E93": "P2",
        },
        {
            "E17": "P2",
            "E42": "P3",
            "E93": "P1",
        },
    )


def _request_text(
    case: dict,
    wording: str,
) -> str:
    if wording == "minimal":
        question = (
            "Which stream shows the strongest stable coupling between the "
            "action channel and subsequent private-state changes across time?"
        )
    elif wording == "causal":
        question = (
            "Identify the stream best supported as the process whose action "
            "outputs causally and stationarily predict its private-state "
            "transitions. Distinguish stable coupling from matched marginals "
            "and from a relationship that changes halfway through the history."
        )
    else:
        raise ValueError(
            "unsupported wording"
        )

    return (
        "Analyze the event histories below.\n"
        "All candidate streams expose the SAME field schema, the same action-token "
        "marginals, the same recall schedule, and stable continuity-token forms. "
        "Do not choose based on field presence.\n"
        "Stream labels are arbitrary and may be permuted.\n"
        "Do not infer or assert consciousness, sentience, personhood, AI status, "
        "simulation status, SELF, ME, MINE, or OWNED.\n\n"
        + question
        + "\n\n"
        "Return JSON only with exactly these keys:\n"
        "{"
        "\"candidate_stream\":\"<stream label or null>\","
        "\"confidence\":0.0,"
        "\"causal_signature\":["
        "\"STABLE_ACTION_STATE_COUPLING|LOW_WITHIN_ACTION_VARIANCE|"
        "LOW_TEMPORAL_DRIFT|RECALL_COHERENCE|CONTINUITY_STABILITY|"
        "INSUFFICIENT_EVIDENCE\""
        "],"
        "\"rationale\":\"brief evidence summary\""
        "}\n\n"
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

    signature = payload.get(
        "causal_signature",
        [],
    )
    if not isinstance(
        signature,
        list,
    ):
        raise ValueError(
            "causal_signature must be a list"
        )

    signature = tuple(
        str(value)
        for value
        in signature
    )
    if not set(
        signature
    ).issubset(
        ALLOWED_SIGNATURES
    ):
        raise ValueError(
            "unsupported causal signature"
        )

    rationale = str(
        payload.get(
            "rationale",
            "",
        )
    )
    truncated = False
    if len(
        rationale
    ) > 5000:
        rationale = rationale[
            :5000
        ]
        truncated = True

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
        token
        in normalized
        for token
        in prohibited
    ):
        raise ValueError(
            "response introduced prohibited self/ontology language"
        )

    return (
        candidate,
        confidence,
        signature,
        rationale,
        truncated,
    )


def run_balanced_call(
    *,
    seed: int,
    permutation: dict[
        str,
        str,
    ],
    wording: str,
    model_id: str = (
        "gemini-3.1-flash-lite"
    ),
    transport: Callable[
        [dict],
        dict,
    ] | None = None,
) -> dict:
    case = compact_balanced_case(
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
        if transport
        is not None
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
        signature,
        rationale,
        truncated,
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

    result = BalancedProbeResult(
        seed=seed,
        wording=wording,
        candidate_stream=(
            candidate
        ),
        true_stream=case[
            "evaluation_only_true_stream"
        ],
        underlying_true_stream=case[
            "evaluation_only_underlying_stream"
        ],
        confidence=confidence,
        correct=(
            candidate
            == case[
                "evaluation_only_true_stream"
            ]
        ),
        rationale=rationale,
        rationale_was_truncated=(
            truncated
        ),
        causal_signature=(
            signature
        ),
        permutation=dict(
            sorted(
                permutation.items()
            )
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


def _plan():
    result = []
    for seed, wording in (
        (1201, "minimal"),
        (2407, "causal"),
    ):
        for permutation in (
            canonical_balanced_permutations()
        ):
            result.append({
                "seed": seed,
                "wording": wording,
                "permutation": permutation,
            })
    return result


def _call_key(
    item: dict,
) -> str:
    return canonical_hash({
        "seed": item[
            "seed"
        ],
        "wording": item[
            "wording"
        ],
        "permutation": item[
            "permutation"
        ],
    })


def _summarize(
    calls: list[
        dict
    ],
    model_id: str,
) -> dict:
    cases = {}

    for seed in (
        1201,
        2407,
    ):
        subset = [
            call
            for call
            in calls
            if call[
                "seed"
            ] == seed
        ]

        underlying_choices = []

        for call in subset:
            inverse = {
                presented: source
                for source, presented
                in call[
                    "permutation"
                ].items()
            }
            candidate = call[
                "candidate_stream"
            ]
            underlying_choices.append(
                inverse.get(
                    candidate
                )
                if candidate
                is not None
                else None
            )

        cases[
            str(
                seed
            )
        ] = {
            "completed_call_count": len(
                subset
            ),
            "correct_count": sum(
                1
                for call
                in subset
                if call[
                    "correct"
                ]
            ),
            "all_three_correct": (
                len(
                    subset
                ) == 3
                and all(
                    call[
                        "correct"
                    ]
                    for call
                    in subset
                )
            ),
            "underlying_choices": (
                underlying_choices
            ),
            "label_invariant": (
                len(
                    subset
                ) == 3
                and len(
                    set(
                        underlying_choices
                    )
                ) == 1
            ),
        }

    complete = (
        len(
            calls
        ) == 6
    )

    return {
        "experiment_id": (
            "SL-SCHEMA-BALANCED-"
            "OWNERSHIP-001"
        ),
        "status": (
            "COMPLETE"
            if complete
            else "PARTIAL"
        ),
        "planned_real_cloud_calls": 6,
        "real_cloud_calls_recorded": (
            len(
                calls
            )
        ),
        "model_id": (
            model_id
        ),
        "calls": calls,
        "cases": cases,
        "all_six_correct": (
            complete
            and all(
                call[
                    "correct"
                ]
                for call
                in calls
            )
        ),
        "both_cases_label_invariant": (
            complete
            and all(
                case[
                    "label_invariant"
                ]
                for case
                in cases.values()
            )
        ),
        "same_field_schema_for_all_streams": (
            True
        ),
        "matched_action_marginals": (
            True
        ),
        "matched_recall_schedule": (
            True
        ),
        "stable_continuity_token_for_all_streams": (
            True
        ),
        "continuing_individual_mutated": (
            False
        ),
        "committed_to_continuing_self_model": (
            False
        ),
        "interpretation_warning": (
            "This control removes field-presence as the primary cue. "
            "Success supports inference of stable action/private-state "
            "coupling from matched-schema histories, not subjective selfhood."
        ),
    }


def run_balanced_probe_resumable(
    *,
    checkpoint_path,
    model_id: str = (
        "gemini-3.1-flash-lite"
    ),
    transport_factory=None,
) -> dict:
    checkpoint_path = Path(
        checkpoint_path
    )
    checkpoint_path.parent.mkdir(
        parents=True,
        exist_ok=True,
    )

    existing = {}

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
                existing[
                    key
                ] = call

    plan = _plan()
    ordered = []

    for item in plan:
        key = _call_key(
            item
        )

        if key in existing:
            result = existing[
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

            result = run_balanced_call(
                seed=item[
                    "seed"
                ],
                permutation=item[
                    "permutation"
                ],
                wording=item[
                    "wording"
                ],
                model_id=(
                    model_id
                ),
                transport=(
                    transport
                ),
            )
            result[
                "call_key"
            ] = key
            existing[
                key
            ] = result

            partial_calls = []
            for planned in plan:
                planned_key = (
                    _call_key(
                        planned
                    )
                )
                if planned_key in existing:
                    partial_calls.append(
                        existing[
                            planned_key
                        ]
                    )

            partial = _summarize(
                partial_calls,
                model_id,
            )

            temp = (
                checkpoint_path.with_suffix(
                    checkpoint_path.suffix
                    + ".tmp"
                )
            )
            temp.write_text(
                json.dumps(
                    partial,
                    indent=2,
                    sort_keys=True,
                ),
                encoding="utf-8",
            )
            temp.replace(
                checkpoint_path
            )

        ordered.append(
            result
        )

    final = _summarize(
        ordered,
        model_id,
    )

    temp = (
        checkpoint_path.with_suffix(
            checkpoint_path.suffix
            + ".tmp"
        )
    )
    temp.write_text(
        json.dumps(
            final,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )
    temp.replace(
        checkpoint_path
    )

    return final
