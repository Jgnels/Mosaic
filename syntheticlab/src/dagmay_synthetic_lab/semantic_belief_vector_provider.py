from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Callable
import json

from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
    ReflectionResponseError,
    _extract_output_text,
)


DIMENSIONS = (
    "cue_actionability",
    "contingent_exchange",
    "reciprocal_influence",
    "adaptive_learning",
    "temporal_improvement",
    "cross_counterpart_generalization",
)


@dataclass(frozen=True)
class SemanticBeliefVector:
    item_id: str
    scores: dict[str, float]
    rationale: str
    model_provider: str
    model_id: str
    prompt_version: str

    def to_dict(self):
        return asdict(
            self
        )


class GeminiSemanticBeliefVectorModel:
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

    def _build_input_text(
        self,
        *,
        item_id: str,
        proposition: str,
    ) -> str:
        return (
            "Evaluate the semantic content of one hypothesis independently.\n\n"
            "Do not compare it with any other hypothesis. "
            "Do not infer hidden history, branch identity, experimental condition, "
            "or whether the hypothesis is preferred.\n\n"
            "Return a score from 0.0 to 1.0 for each fixed dimension, where the score "
            "represents how strongly the proposition itself endorses that claim.\n\n"
            "Dimensions:\n"
            "- cue_actionability: cues or signals can guide useful action selection.\n"
            "- contingent_exchange: the focal entity's responses and counterpart states "
            "show non-random contingency or predictive dependence.\n"
            "- reciprocal_influence: interaction is described as genuinely bidirectional "
            "or mutually influential, not merely predictive correlation.\n"
            "- adaptive_learning: behavior or processing is described as adapting from experience.\n"
            "- temporal_improvement: performance is described as improving over time.\n"
            "- cross_counterpart_generalization: the capability is described as generalizing "
            "across multiple counterparts rather than remaining counterpart-specific.\n\n"
            "Score only what the proposition claims. A proposition may endorse contingency "
            "without endorsing reciprocal influence. A proposition may endorse adaptive "
            "learning without claiming measurable temporal improvement.\n\n"
            "OUTPUT CONTRACT:\n"
            "- JSON object only.\n"
            "- Keys: item_id, scores, rationale.\n"
            "- scores must contain exactly the six dimension keys above.\n"
            "- every score must be between 0 and 1.\n"
            "- rationale <= 240 characters.\n\n"
            f"ITEM_ID: {item_id}\n"
            f"PROPOSITION: {proposition}"
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
                "semantic vector output must be a JSON object"
            )

        return parsed

    def evaluate(
        self,
        *,
        item_id: str,
        proposition: str,
    ) -> SemanticBeliefVector:
        response = self.transport({
            "model": (
                self.model_id
            ),
            "store": (
                self.store
            ),
            "input": (
                self._build_input_text(
                    item_id=item_id,
                    proposition=proposition,
                )
            ),
        })

        output_text = _extract_output_text(
            response
        )

        item = self._parse_object(
            output_text
        )

        returned_id = str(
            item.get(
                "item_id",
                "",
            )
        )

        if returned_id != item_id:
            raise ReflectionResponseError(
                "semantic vector item_id mismatch"
            )

        scores = item.get(
            "scores"
        )

        if not isinstance(
            scores,
            dict,
        ):
            raise ReflectionResponseError(
                "scores must be an object"
            )

        if set(
            scores.keys()
        ) != set(
            DIMENSIONS
        ):
            raise ReflectionResponseError(
                "scores must contain exactly the fixed dimension ontology"
            )

        normalized = {}

        for dimension in DIMENSIONS:
            try:
                value = float(
                    scores[
                        dimension
                    ]
                )
            except (
                TypeError,
                ValueError,
            ):
                raise ReflectionResponseError(
                    f"invalid semantic score for {dimension}"
                )

            if not (
                0.0
                <= value
                <= 1.0
            ):
                raise ReflectionResponseError(
                    f"semantic score out of range for {dimension}"
                )

            normalized[
                dimension
            ] = value

        rationale = str(
            item.get(
                "rationale",
                "",
            )
        ).strip()

        if len(
            rationale
        ) > 240:
            rationale = (
                rationale[
                    :237
                ].rstrip()
                + "..."
            )

        return SemanticBeliefVector(
            item_id=(
                item_id
            ),
            scores=(
                normalized
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
                "1.0"
            ),
        )
