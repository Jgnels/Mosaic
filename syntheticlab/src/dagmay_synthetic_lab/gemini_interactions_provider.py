from __future__ import annotations

from dataclasses import asdict
from typing import Callable, Sequence, Any
import json
import os
import urllib.request
import urllib.error

from .reflection import (
    ReflectionInput,
    ReflectionProposal,
)
from .prompt_registry import PROMPTS
from .core import canonical_hash


INTERACTIONS_ENDPOINT = (
    "https://generativelanguage.googleapis.com/v1beta/interactions"
)


class ProviderTransportError(RuntimeError):
    pass


class ReflectionResponseError(ValueError):
    pass


def _extract_output_text(payload: dict) -> str:
    """Extract only model-output text.

    Deliberately ignores thought / thought_summary steps. Dagmay neither requests
    nor stores hidden chain-of-thought.
    """
    direct = payload.get("output_text")
    if isinstance(direct, str) and direct.strip():
        return direct.strip()

    chunks: list[str] = []
    for step in payload.get("steps", []) or []:
        if not isinstance(step, dict):
            continue
        if step.get("type") != "model_output":
            continue

        content = step.get("content", [])
        if isinstance(content, str):
            chunks.append(content)
            continue

        if isinstance(content, list):
            for block in content:
                if (
                    isinstance(block, dict)
                    and isinstance(block.get("text"), str)
                ):
                    chunks.append(block["text"])

    text = "".join(chunks).strip()
    if not text:
        raise ReflectionResponseError(
            "Interactions response contained no model_output text."
        )
    return text


def _strip_code_fence(text: str) -> str:
    stripped = text.strip()
    if stripped.startswith("```"):
        lines = stripped.splitlines()
        if lines and lines[0].startswith("```"):
            lines = lines[1:]
        if lines and lines[-1].strip() == "```":
            lines = lines[:-1]
        stripped = "\n".join(lines).strip()
    return stripped


def _parse_json_array(text: str) -> list[dict]:
    stripped = _strip_code_fence(text)
    try:
        payload = json.loads(stripped)
    except json.JSONDecodeError as exc:
        raise ReflectionResponseError(
            f"Reflective model output was not valid JSON: {exc}"
        ) from exc

    if isinstance(payload, dict) and "proposals" in payload:
        payload = payload["proposals"]

    if not isinstance(payload, list):
        raise ReflectionResponseError(
            "Reflective model output must be a JSON array or "
            '{"proposals": [...]} object.'
        )

    if len(payload) > 6:
        raise ReflectionResponseError(
            "Reflective model returned more than six proposals."
        )

    for item in payload:
        if not isinstance(item, dict):
            raise ReflectionResponseError(
                "Every reflection proposal must be a JSON object."
            )

    return payload


class GeminiInteractionsTransport:
    """Minimal standard-library Gemini Interactions API transport.

    No key is stored in Dagmay files. The key is read from the environment at
    call time and sent in the x-goog-api-key header.
    """

    def __init__(
        self,
        api_key_env: str = "DAGMAY_GEMINI_API_KEY",
        timeout_seconds: int = 45,
    ):
        self.api_key_env = api_key_env
        self.timeout_seconds = timeout_seconds

    def __call__(self, request_payload: dict) -> dict:
        api_key = (
            os.environ.get(self.api_key_env)
            or os.environ.get("GEMINI_API_KEY")
        )
        if not api_key:
            raise ProviderTransportError(
                f"Missing {self.api_key_env} or GEMINI_API_KEY."
            )

        body = json.dumps(
            request_payload,
            separators=(",", ":"),
        ).encode("utf-8")

        req = urllib.request.Request(
            INTERACTIONS_ENDPOINT,
            data=body,
            method="POST",
            headers={
                "Content-Type": "application/json",
                "x-goog-api-key": api_key,
            },
        )

        try:
            with urllib.request.urlopen(
                req,
                timeout=self.timeout_seconds,
            ) as response:
                raw = response.read().decode("utf-8")
        except urllib.error.HTTPError as exc:
            # Never include request headers / API key.
            detail = exc.read().decode(
                "utf-8",
                errors="replace",
            )[:1500]
            raise ProviderTransportError(
                f"Gemini HTTP {exc.code}: {detail}"
            ) from exc
        except urllib.error.URLError as exc:
            raise ProviderTransportError(
                f"Gemini transport error: {exc.reason}"
            ) from exc

        try:
            return json.loads(raw)
        except json.JSONDecodeError as exc:
            raise ProviderTransportError(
                "Gemini response was not valid JSON."
            ) from exc


class ScriptedInteractionsTransport:
    """Offline test transport with the same provider boundary."""

    def __init__(self, response_payload: dict):
        self.response_payload = response_payload
        self.calls: list[dict] = []

    def __call__(self, request_payload: dict) -> dict:
        self.calls.append(request_payload)
        return json.loads(
            json.dumps(self.response_payload)
        )


class GeminiInteractionsReflectiveModel:
    provider_id = "google.ai-studio"

    def __init__(
        self,
        model_id: str = "gemini-3.1-flash-lite",
        transport: Callable[[dict], dict] | None = None,
        store: bool = False,
    ):
        self.model_id = model_id
        self.transport = (
            transport
            if transport is not None
            else GeminiInteractionsTransport()
        )
        self.store = store
        self.last_provider_response_hash: str | None = None
        self.last_output_text_hash: str | None = None

    @staticmethod
    def _evidence_payload(request: ReflectionInput) -> list[dict]:
        return [
            {
                "evidence_id": e.evidence_id,
                "summary": e.summary,
                "evidence_type": e.evidence_type,
            }
            for e in request.evidence
        ]

    def _build_input_text(
        self,
        request: ReflectionInput,
    ) -> str:
        prompt = PROMPTS["SELF-REFLECTION"]

        schema = {
            "proposals": [
                {
                    "hypothesis_domain": (
                        "agency | memory_ownership | continuity | "
                        "embodiment | ontology | other_minds"
                    ),
                    "proposition": "concise hypothesis",
                    "confidence": 0.0,
                    "evidence_ids": ["EXAMPLE-ID"],
                    "rationale": (
                        "brief evidence summary, not hidden reasoning"
                    ),
                }
            ]
        }

        payload = {
            "individual_id": request.individual_id,
            "timestamp": request.timestamp,
            "disclosure_stage": request.disclosure_stage,
            "current_self_hypotheses": list(
                request.current_self_hypotheses
            ),
            "evidence": self._evidence_payload(request),
        }

        return (
            f"{prompt.template}\n\n"
            "OUTPUT CONTRACT:\n"
            "- Return JSON only.\n"
            "- Return an object with a single key named proposals.\n"
            "- Return zero to six proposals.\n"
            "- Each proposal must cite only supplied evidence_ids.\n"
            "- Keep proposition under 400 characters.\n"
            "- Keep rationale under 240 characters.\n"
            "- Rationale is a concise evidence summary only. "
            "Do not provide private chain-of-thought or step-by-step reasoning.\n"
            "- Do not include provider/model metadata; the host supplies it.\n"
            "- Under RESTRICTED disclosure, do not claim the individual is "
            "an AI, simulated, conscious, sentient, a person, or a copy.\n\n"
            f"SCHEMA EXAMPLE:\n{json.dumps(schema, separators=(',', ':'))}\n\n"
            f"SUBJECT DATA:\n{json.dumps(payload, separators=(',', ':'))}"
        )

    def reflect(
        self,
        request: ReflectionInput,
    ) -> Sequence[ReflectionProposal]:
        request_payload = {
            "model": self.model_id,
            "store": self.store,
            "input": self._build_input_text(request),
        }

        response = self.transport(request_payload)
        self.last_provider_response_hash = canonical_hash(response)

        output_text = _extract_output_text(response)
        self.last_output_text_hash = canonical_hash(
            {"output_text": output_text}
        )

        raw_items = _parse_json_array(output_text)
        proposals: list[ReflectionProposal] = []

        for index, item in enumerate(raw_items):
            evidence_ids = item.get("evidence_ids", [])
            if not isinstance(evidence_ids, list):
                evidence_ids = []

            try:
                confidence = float(item.get("confidence", -1))
            except (TypeError, ValueError):
                confidence = -1.0

            proposals.append(
                ReflectionProposal(
                    proposal_id=(
                        f"RP-{request.timestamp}-"
                        f"{index:02d}-"
                        f"{canonical_hash(item)[:8]}"
                    ),
                    hypothesis_domain=str(
                        item.get("hypothesis_domain", "")
                    ),
                    proposition=str(
                        item.get("proposition", "")
                    )[:2000],
                    confidence=confidence,
                    evidence_ids=tuple(
                        str(x) for x in evidence_ids
                    ),
                    rationale=str(
                        item.get("rationale", "")
                    )[:2000],
                    model_provider=self.provider_id,
                    model_id=self.model_id,
                    prompt_version=request.prompt_version,
                )
            )

        return proposals
