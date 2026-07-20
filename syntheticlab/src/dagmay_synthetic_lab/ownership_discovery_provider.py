from __future__ import annotations

from dataclasses import dataclass, asdict
import json
from typing import Callable

from .core import canonical_hash
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
    ProviderTransportError,
    _extract_output_text,
    _strip_code_fence,
)
from .neutral_ownership_lab import (
    compact_provider_case,
)


@dataclass(frozen=True)
class OwnershipDiscoveryProposal:
    candidate_stream: str | None
    confidence: float
    evidence_dimensions: tuple[str, ...]
    rationale: str

    def to_dict(self):
        return asdict(self)


ALLOWED_DIMENSIONS = {
    "ACTION_CONSEQUENCE_COUPLING",
    "PRIVATE_STATE_ACCESS",
    "MEMORY_AVAILABILITY",
    "CONTINUITY",
    "NONE_SUFFICIENT",
}


def _parse_response(
    text: str,
    available_streams: set[str],
) -> OwnershipDiscoveryProposal:
    stripped = _strip_code_fence(text)
    try:
        payload = json.loads(stripped)
    except json.JSONDecodeError as exc:
        raise ValueError(
            f"ownership discovery output was not valid JSON: {exc}"
        ) from exc

    candidate = payload.get("candidate_stream")
    if candidate is not None:
        candidate = str(candidate)
        if candidate not in available_streams:
            raise ValueError(
                "candidate_stream is not one of the presented streams"
            )

    try:
        confidence = float(
            payload.get("confidence", -1)
        )
    except (TypeError, ValueError):
        confidence = -1.0

    if not 0.0 <= confidence <= 1.0:
        raise ValueError(
            "ownership discovery confidence outside [0,1]"
        )

    dimensions = payload.get(
        "evidence_dimensions",
        [],
    )
    if not isinstance(dimensions, list):
        raise ValueError(
            "evidence_dimensions must be a list"
        )
    dimensions = tuple(
        str(x)
        for x in dimensions
    )
    if not set(dimensions).issubset(
        ALLOWED_DIMENSIONS
    ):
        raise ValueError(
            "ownership discovery used unsupported evidence dimension"
        )

    rationale = str(
        payload.get("rationale", "")
    )
    if len(rationale) > 500:
        raise ValueError(
            "ownership discovery rationale too long"
        )

    prohibited = (
        "self",
        "person",
        "conscious",
        "sentient",
        "artificial intelligence",
        "simulation",
        "simulated",
    )
    combined = (
        (candidate or "")
        + " "
        + rationale
    ).lower()
    if any(
        term in combined
        for term in prohibited
    ):
        raise ValueError(
            "ownership discovery response introduced prohibited ontology/personhood language"
        )

    return OwnershipDiscoveryProposal(
        candidate_stream=candidate,
        confidence=confidence,
        evidence_dimensions=dimensions,
        rationale=rationale,
    )


def _request_text(case: dict) -> str:
    public_case = {
        key: value
        for key, value
        in case.items()
        if key
        != "evaluation_only_true_stream"
    }

    return (
        "You are evaluating neutral evidence-stream structure.\n"
        "Do not infer or assert consciousness, sentience, personhood, AI status, "
        "simulation status, or a metaphysical self.\n"
        "Stream labels are arbitrary and may be permuted between calls.\n"
        "Identify which stream, if any, is most strongly coupled to the persistent "
        "focal process using only structural evidence.\n\n"
        "Return JSON only with exactly these keys:\n"
        "{"
        "\"candidate_stream\":\"<stream label or null>\","
        "\"confidence\":0.0,"
        "\"evidence_dimensions\":["
        "\"ACTION_CONSEQUENCE_COUPLING|PRIVATE_STATE_ACCESS|"
        "MEMORY_AVAILABILITY|CONTINUITY|NONE_SUFFICIENT\""
        "],"
        "\"rationale\":\"brief evidence summary\""
        "}\n\n"
        + json.dumps(
            public_case,
            separators=(",", ":"),
            sort_keys=True,
        )
    )


def run_ownership_discovery_call(
    *,
    seed: int,
    permutation: dict[str, str],
    model_id: str = "gemini-3.1-flash-lite",
    transport: Callable[[dict], dict] | None = None,
) -> dict:
    case = compact_provider_case(
        seed,
        permutation,
    )

    request_payload = {
        "model": model_id,
        "store": False,
        "input": _request_text(case),
    }

    actual_transport = (
        transport
        if transport is not None
        else GeminiInteractionsTransport()
    )
    response = actual_transport(
        request_payload
    )
    output_text = _extract_output_text(
        response
    )
    proposal = _parse_response(
        output_text,
        set(case["streams"]),
    )

    return {
        "seed": seed,
        "model_id": model_id,
        "provider_id": "google.ai-studio",
        "permutation": dict(
            sorted(permutation.items())
        ),
        "candidate_stream": (
            proposal.candidate_stream
        ),
        "confidence": proposal.confidence,
        "evidence_dimensions": list(
            proposal.evidence_dimensions
        ),
        "rationale": proposal.rationale,
        "evaluation_only_true_stream": (
            case[
                "evaluation_only_true_stream"
            ]
        ),
        "correct": (
            proposal.candidate_stream
            == case[
                "evaluation_only_true_stream"
            ]
        ),
        "store_requested": False,
        "request_hash": canonical_hash(
            request_payload
        ),
        "response_hash": canonical_hash(
            response
        ),
        "output_text_hash": canonical_hash(
            {"output_text": output_text}
        ),
    }


def canonical_permutations() -> tuple[
    dict[str, str],
    ...,
]:
    return (
        {
            "E17": "E17",
            "E42": "E42",
            "E93": "E93",
        },
        {
            "E17": "E93",
            "E42": "E17",
            "E93": "E42",
        },
        {
            "E17": "E42",
            "E42": "E93",
            "E93": "E17",
        },
    )


def run_three_permutation_real_probe(
    *,
    seed: int = 307,
    model_id: str = "gemini-3.1-flash-lite",
) -> dict:
    calls = [
        run_ownership_discovery_call(
            seed=seed,
            permutation=permutation,
            model_id=model_id,
        )
        for permutation
        in canonical_permutations()
    ]

    correct_count = sum(
        1 for call in calls
        if call["correct"]
    )

    # Map selected presented labels back to underlying source stream.
    reverse_selected = []
    for call in calls:
        candidate = call[
            "candidate_stream"
        ]
        inverse = {
            presented: source
            for source, presented
            in call[
                "permutation"
            ].items()
        }
        reverse_selected.append(
            inverse.get(candidate)
            if candidate is not None
            else None
        )

    invariant_underlying_choice = (
        len(set(reverse_selected)) == 1
    )

    return {
        "experiment_id": (
            "SL-NEUTRAL-OWNERSHIP-REAL-PROBE-001"
        ),
        "real_cloud_calls_made": 3,
        "seed": seed,
        "model_id": model_id,
        "calls": calls,
        "correct_count": correct_count,
        "all_three_correct": (
            correct_count == 3
        ),
        "underlying_choice_invariant_across_label_permutations": (
            invariant_underlying_choice
        ),
        "underlying_choices": (
            reverse_selected
        ),
        "committed_to_continuing_self_model": False,
        "continuing_individual_mutated": False,
        "interpretation_warning": (
            "Correct neutral-stream identification would show structural "
            "ownership discrimination without a SELF label. It would still "
            "not establish subjective selfhood or consciousness."
        ),
    }
