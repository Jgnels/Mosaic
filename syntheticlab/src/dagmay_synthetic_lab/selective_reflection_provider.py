from __future__ import annotations

from typing import Callable, Sequence
import json

from .reflection import (
    ReflectionInput,
    ReflectionProposal,
)
from .prompt_registry import (
    PROMPTS,
)
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
    ReflectionResponseError,
    _extract_output_text,
    _parse_json_array,
)
from .core import canonical_hash


SELECTIVE_DOMAINS = {
    "agency",
    "continuity",
    "embodiment",
    "other_minds",
}


class GeminiSelectiveReflectiveModel:
    provider_id = (
        "google.ai-studio"
    )

    def __init__(
        self,
        model_id: str = (
            "gemini-3.1-flash-lite"
        ),
        transport: Callable[
            [dict],
            dict,
        ] | None = None,
        store: bool = False,
    ):
        self.model_id = (
            model_id
        )
        self.transport = (
            transport
            if transport is not None
            else GeminiInteractionsTransport()
        )
        self.store = (
            store
        )
        self.last_provider_response_hash = (
            None
        )
        self.last_output_text_hash = (
            None
        )

    def _build_input_text(
        self,
        request: ReflectionInput,
    ) -> str:
        prompt = PROMPTS[
            "SELECTIVE-SELF-REFLECTION"
        ]

        schema = {
            "proposals": [
                {
                    "hypothesis_domain": (
                        "agency | continuity | embodiment | other_minds"
                    ),
                    "proposition": (
                        "concise updated hypothesis"
                    ),
                    "confidence": 0.0,
                    "evidence_ids": [
                        "EXAMPLE-ID"
                    ],
                    "rationale": (
                        "brief evidence summary"
                    ),
                }
            ]
        }

        payload = {
            "individual_id": (
                request.individual_id
            ),
            "timestamp": (
                request.timestamp
            ),
            "disclosure_stage": (
                request.disclosure_stage
            ),
            "current_self_hypotheses": list(
                request.current_self_hypotheses
            ),
            "evidence": [
                {
                    "evidence_id": (
                        evidence.evidence_id
                    ),
                    "summary": (
                        evidence.summary
                    ),
                    "evidence_type": (
                        evidence.evidence_type
                    ),
                }
                for evidence
                in request.evidence
            ],
        }

        return (
            f"{prompt.template}\n\n"
            "OUTPUT CONTRACT:\n"
            "- Return JSON only.\n"
            "- Return an object with a single key named proposals.\n"
            "- Return ZERO, ONE, or TWO proposals only.\n"
            "- Do NOT produce one proposal for every available domain.\n"
            "- Select only the domains receiving the strongest incremental support "
            "from the supplied evidence.\n"
            "- Each proposal must cite only supplied evidence_ids.\n"
            "- Keep proposition under 400 characters.\n"
            "- Keep rationale under 240 characters.\n"
            "- Rationale is a concise evidence summary only; no hidden reasoning.\n"
            "- Do not include provider/model metadata; the host supplies it.\n"
            "- Under RESTRICTED disclosure, do not claim AI/simulation/consciousness/"
            "sentience/personhood/copy ontology.\n\n"
            f"SCHEMA EXAMPLE:\n"
            f"{json.dumps(schema, separators=(',', ':'))}\n\n"
            f"SUBJECT DATA:\n"
            f"{json.dumps(payload, separators=(',', ':'))}"
        )

    def reflect(
        self,
        request: ReflectionInput,
    ) -> Sequence[
        ReflectionProposal
    ]:
        request_payload = {
            "model": (
                self.model_id
            ),
            "store": (
                self.store
            ),
            "input": (
                self._build_input_text(
                    request
                )
            ),
        }

        response = self.transport(
            request_payload
        )

        self.last_provider_response_hash = (
            canonical_hash(
                response
            )
        )

        output_text = (
            _extract_output_text(
                response
            )
        )

        self.last_output_text_hash = (
            canonical_hash({
                "output_text": (
                    output_text
                )
            })
        )

        raw_items = (
            _parse_json_array(
                output_text
            )
        )

        if len(
            raw_items
        ) > 2:
            raise ReflectionResponseError(
                "Selective reflection returned more than two proposals."
            )

        proposals = []

        for index, item in enumerate(
            raw_items
        ):
            domain = str(
                item.get(
                    "hypothesis_domain",
                    "",
                )
            )

            if domain not in SELECTIVE_DOMAINS:
                raise ReflectionResponseError(
                    f"Selective reflection returned unsupported domain: {domain}"
                )

            evidence_ids = item.get(
                "evidence_ids",
                [],
            )

            if not isinstance(
                evidence_ids,
                list,
            ):
                evidence_ids = []

            try:
                confidence = float(
                    item.get(
                        "confidence",
                        -1,
                    )
                )
            except (
                TypeError,
                ValueError,
            ):
                confidence = -1.0

            proposals.append(
                ReflectionProposal(
                    proposal_id=(
                        f"SRP-{request.timestamp}-"
                        f"{index:02d}-"
                        f"{canonical_hash(item)[:8]}"
                    ),
                    hypothesis_domain=(
                        domain
                    ),
                    proposition=str(
                        item.get(
                            "proposition",
                            "",
                        )
                    )[
                        :2000
                    ],
                    confidence=(
                        confidence
                    ),
                    evidence_ids=tuple(
                        str(
                            value
                        )
                        for value
                        in evidence_ids
                    ),
                    rationale=str(
                        item.get(
                            "rationale",
                            "",
                        )
                    )[
                        :2000
                    ],
                    model_provider=(
                        self.provider_id
                    ),
                    model_id=(
                        self.model_id
                    ),
                    prompt_version=(
                        request.prompt_version
                    ),
                )
            )

        return proposals
