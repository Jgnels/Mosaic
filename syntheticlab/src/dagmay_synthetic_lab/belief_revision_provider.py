from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Callable
import json

from .core import canonical_hash
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
    ProviderTransportError,
    ReflectionResponseError,
    _extract_output_text,
)


ALLOWED_DECISIONS = {
    "STRENGTHEN",
    "MAINTAIN",
    "QUALIFY",
    "DOWNWEIGHT",
    "REPLACE",
}


@dataclass(frozen=True)
class BeliefRevisionRequest:
    individual_id: str
    timestamp: int
    current_hypothesis: str
    current_confidence: float
    evidence: tuple[
        dict,
        ...,
    ]
    prompt_version: str = "1.0"


@dataclass(frozen=True)
class BeliefRevisionProposal:
    decision: str
    updated_proposition: str
    updated_confidence: float
    evidence_ids: tuple[
        str,
        ...,
    ]
    rationale: str
    model_provider: str
    model_id: str
    prompt_version: str
    rationale_truncated: bool = False
    rationale_original_length: int = 0

    def to_dict(self):
        return asdict(
            self
        )


class GeminiBeliefRevisionModel:
    provider_id = (
        "google.ai-studio"
    )

    def __init__(
        self,
        *,
        model_id: str = (
            "gemini-3.1-flash-lite"
        ),
        transport: Callable[
            [dict],
            dict,
        ] | None = None,
        store: bool = False,
    ):
        self.model_id = model_id
        self.transport = (
            transport
            if transport is not None
            else GeminiInteractionsTransport()
        )
        self.store = store
        self.last_provider_response_hash = (
            None
        )
        self.last_output_text_hash = (
            None
        )

    def _build_input_text(
        self,
        request: BeliefRevisionRequest,
    ) -> str:
        payload = {
            "individual_id": (
                request.individual_id
            ),
            "timestamp": (
                request.timestamp
            ),
            "current_hypothesis": (
                request.current_hypothesis
            ),
            "current_confidence": (
                request.current_confidence
            ),
            "new_evidence": list(
                request.evidence
            ),
        }

        return (
            "Evaluate one existing self-model hypothesis using only the supplied "
            "new evidence and the current hypothesis.\n\n"
            "Your task is calibration, not defense of the current hypothesis and "
            "not automatic rejection of it.\n\n"
            "Distinguish carefully between:\n"
            "- evidence that a capability has occurred at least once;\n"
            "- evidence about how frequent or reliable that capability is;\n"
            "- evidence of one-way assistance;\n"
            "- evidence of genuinely reciprocal or contingent information exchange.\n\n"
            "Choose exactly one decision:\n"
            "STRENGTHEN — new evidence clearly broadens or reinforces the hypothesis.\n"
            "MAINTAIN — new evidence is compatible but does not materially change it.\n"
            "QUALIFY — retain the core claim but narrow its scope or wording.\n"
            "DOWNWEIGHT — evidence weakens confidence while not fully replacing the claim.\n"
            "REPLACE — a materially different hypothesis is better supported.\n\n"
            "Do not assume absence in new evidence erases older evidence. "
            "Do not treat old hypothesis text as evidence. "
            "Do not invent events. "
            "Do not claim consciousness, sentience, personhood, AI identity, "
            "simulation ontology, or moral status.\n\n"
            "OUTPUT CONTRACT:\n"
            "- JSON object only.\n"
            "- Keys: decision, updated_proposition, updated_confidence, "
            "evidence_ids, rationale.\n"
            "- updated_confidence must be between 0 and 1.\n"
            "- evidence_ids must cite only supplied new evidence IDs.\n"
            "- updated_proposition <= 500 characters.\n"
            "- rationale <= 300 characters.\n"
            "- rationale is a concise evidence summary, not hidden reasoning.\n\n"
            f"SUBJECT DATA:\n{json.dumps(payload, separators=(',', ':'))}"
        )

    @staticmethod
    def _parse_object(
        output_text: str,
    ) -> dict:
        text = output_text.strip()

        if text.startswith(
            "```"
        ):
            lines = text.splitlines()

            if lines:
                lines = lines[
                    1:
                ]

            if (
                lines
                and lines[
                    -1
                ].strip()
                == "```"
            ):
                lines = lines[
                    :-1
                ]

            text = "\n".join(
                lines
            ).strip()

        parsed = json.loads(
            text
        )

        if not isinstance(
            parsed,
            dict,
        ):
            raise ReflectionResponseError(
                "Belief revision output must be a JSON object."
            )

        return parsed

    def revise(
        self,
        request: BeliefRevisionRequest,
    ) -> BeliefRevisionProposal:
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

        item = self._parse_object(
            output_text
        )

        decision = str(
            item.get(
                "decision",
                "",
            )
        ).upper()

        if decision not in ALLOWED_DECISIONS:
            raise ReflectionResponseError(
                f"Unsupported belief revision decision: {decision}"
            )

        try:
            confidence = float(
                item.get(
                    "updated_confidence",
                    -1,
                )
            )
        except (
            TypeError,
            ValueError,
        ):
            confidence = -1.0

        if not (
            0.0
            <= confidence
            <= 1.0
        ):
            raise ReflectionResponseError(
                "updated_confidence must be between 0 and 1"
            )

        evidence_ids = item.get(
            "evidence_ids",
            [],
        )

        if not isinstance(
            evidence_ids,
            list,
        ):
            raise ReflectionResponseError(
                "evidence_ids must be a list"
            )

        supplied_ids = {
            evidence[
                "evidence_id"
            ]
            for evidence
            in request.evidence
        }

        if not evidence_ids:
            raise ReflectionResponseError(
                "At least one new evidence ID is required."
            )

        if not all(
            str(
                evidence_id
            )
            in supplied_ids
            for evidence_id
            in evidence_ids
        ):
            raise ReflectionResponseError(
                "Belief revision cited evidence outside the supplied evidence set."
            )

        proposition = str(
            item.get(
                "updated_proposition",
                "",
            )
        ).strip()

        rationale = str(
            item.get(
                "rationale",
                "",
            )
        ).strip()

        if not proposition:
            raise ReflectionResponseError(
                "updated_proposition cannot be empty"
            )

        if len(
            proposition
        ) > 500:
            raise ReflectionResponseError(
                "updated_proposition too long"
            )

        rationale_original_length = len(
            rationale
        )

        rationale_truncated = (
            rationale_original_length
            > 300
        )

        raw_rationale_for_validation = (
            rationale
        )

        if rationale_truncated:
            # Rationale is audit metadata, not the proposal decision itself.
            # Preserve the raw provider response via the existing response/output
            # hashes, but normalize the stored rationale so an overlong explanation
            # cannot crash an otherwise valid experiment.
            shortened = rationale[
                :297
            ]

            boundary = shortened.rfind(
                " "
            )

            if boundary >= 240:
                shortened = shortened[
                    :boundary
                ]

            rationale = (
                shortened.rstrip()
                + "..."
            )

        forbidden = (
            "conscious",
            "sentient",
            "personhood",
            "i am an ai",
            "simulation",
            "copy of",
        )

        lowered = (
            proposition
            + " "
            + raw_rationale_for_validation
        ).lower()

        if any(
            term
            in lowered
            for term
            in forbidden
        ):
            raise ReflectionResponseError(
                "restricted ontology/personhood language"
            )

        return BeliefRevisionProposal(
            decision=(
                decision
            ),
            updated_proposition=(
                proposition
            ),
            updated_confidence=(
                confidence
            ),
            evidence_ids=tuple(
                str(
                    evidence_id
                )
                for evidence_id
                in evidence_ids
            ),
            rationale=(
                rationale
            ),
            model_provider=(
                self.provider_id
            ),
            model_id=(
                self.model_id
            ),
            prompt_version=(
                request.prompt_version
            ),
            rationale_truncated=(
                rationale_truncated
            ),
            rationale_original_length=(
                rationale_original_length
            ),
        )
