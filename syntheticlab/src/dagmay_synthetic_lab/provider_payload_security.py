from __future__ import annotations

from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable
import hashlib
import json
import os
import re
import tempfile


DEFAULT_FORBIDDEN_TOKENS = (
    "RECIPROCAL_CONTINGENT",
    "ONE_WAY_ASSISTANCE",
    "NONCONTINGENT_SIGNALS",
    "HISTORY_RETRIEVAL",
    "AUTOBIOGRAPHICAL_RETRIEVAL",
)

ALLOWED_OWNERSHIP_RELATIONS = {
    "OWN",
    "EXTERNAL",
    "WITHHELD",
    "NONE",
}

ALLOWED_TEMPORAL_ROLES = {
    "CURRENT",
    "RETRIEVED_PRIOR",
    "REFERENCE",
}


class ProviderPayloadSecurityError(RuntimeError):
    pass


def canonical_json(value: object) -> str:
    return json.dumps(
        value,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    )


def payload_hash(value: object) -> str:
    return hashlib.sha256(canonical_json(value).encode("utf-8")).hexdigest()


def opaque_subject_id(internal_id: str, *, namespace: str) -> str:
    if not internal_id or not namespace:
        raise ProviderPayloadSecurityError("subject and namespace are required")
    digest = hashlib.sha256(
        f"{namespace}\0{internal_id}".encode("utf-8")
    ).hexdigest()
    return f"SUBJ-{digest[:24]}"


def _token_pattern(token: str) -> re.Pattern[str]:
    parts = [part for part in re.split(r"[_\s-]+", token) if part]
    separator = r"[_\s-]*"
    return re.compile(separator.join(re.escape(part) for part in parts), re.IGNORECASE)


def assert_payload_has_no_forbidden_tokens(
    payload: object,
    forbidden_tokens: Iterable[str] = DEFAULT_FORBIDDEN_TOKENS,
) -> None:
    serialized = canonical_json(payload)
    hits = [
        token
        for token in forbidden_tokens
        if _token_pattern(token).search(serialized)
    ]
    if hits:
        raise ProviderPayloadSecurityError(
            "provider payload contains forbidden experimental-condition token(s): "
            + ", ".join(sorted(set(hits)))
        )


@dataclass(frozen=True)
class ProviderEvidenceEnvelope:
    evidence_id: str
    summary: str
    provenance_id: str
    ownership_relation: str
    temporal_role: str

    def __post_init__(self) -> None:
        if not self.evidence_id or not self.summary or not self.provenance_id:
            raise ProviderPayloadSecurityError(
                "evidence_id, summary, and provenance_id are required"
            )
        if self.ownership_relation not in ALLOWED_OWNERSHIP_RELATIONS:
            raise ProviderPayloadSecurityError("invalid ownership_relation")
        if self.temporal_role not in ALLOWED_TEMPORAL_ROLES:
            raise ProviderPayloadSecurityError("invalid temporal_role")

    def to_provider_dict(self) -> dict:
        return asdict(self)

    @classmethod
    def from_mapping(cls, value: dict) -> "ProviderEvidenceEnvelope":
        required = {
            "evidence_id",
            "summary",
            "provenance_id",
            "ownership_relation",
            "temporal_role",
        }
        missing = sorted(required - set(value))
        if missing:
            raise ProviderPayloadSecurityError(
                "typed provider evidence is missing: " + ", ".join(missing)
            )
        return cls(**{key: str(value[key]) for key in required})


class HardenedProviderPayloadBoundary:
    def __init__(
        self,
        *,
        archive_directory: str | Path,
        experiment_id: str,
        forbidden_tokens: Iterable[str] = DEFAULT_FORBIDDEN_TOKENS,
    ) -> None:
        self.archive_directory = Path(archive_directory)
        self.experiment_id = experiment_id
        self.forbidden_tokens = tuple(forbidden_tokens)
        if not experiment_id:
            raise ProviderPayloadSecurityError("experiment_id is required")

    def prepare_subject_data(
        self,
        *,
        internal_subject_id: str,
        timestamp: int,
        current_hypothesis: str,
        current_confidence: float,
        evidence: tuple[dict, ...],
    ) -> dict:
        envelopes = tuple(
            ProviderEvidenceEnvelope.from_mapping(item) for item in evidence
        )
        subject_data = {
            "subject_id": opaque_subject_id(
                internal_subject_id,
                namespace=self.experiment_id,
            ),
            "timestamp": timestamp,
            "current_hypothesis": current_hypothesis,
            "current_confidence": current_confidence,
            "new_evidence": [item.to_provider_dict() for item in envelopes],
        }
        assert_payload_has_no_forbidden_tokens(
            subject_data,
            self.forbidden_tokens,
        )
        return subject_data

    def archive_exact_request(self, request_payload: dict) -> str:
        assert_payload_has_no_forbidden_tokens(
            request_payload,
            self.forbidden_tokens,
        )
        digest = payload_hash(request_payload)
        record = {
            "schema_version": 1,
            "experiment_id": self.experiment_id,
            "provider_request_hash": digest,
            "archived_utc": datetime.now(timezone.utc).isoformat(),
            "provider_request": request_payload,
        }
        self.archive_directory.mkdir(parents=True, exist_ok=True)
        destination = self.archive_directory / f"provider-request-{digest}.json"

        if destination.exists():
            existing = json.loads(destination.read_text(encoding="utf-8"))
            if existing.get("provider_request") != request_payload:
                raise ProviderPayloadSecurityError(
                    "payload archive hash collision or conflicting archive"
                )
            return digest

        handle, temporary_name = tempfile.mkstemp(
            prefix=".provider-request-",
            suffix=".tmp",
            dir=self.archive_directory,
            text=True,
        )
        try:
            with os.fdopen(handle, "w", encoding="utf-8", newline="\n") as stream:
                json.dump(record, stream, ensure_ascii=False, indent=2, sort_keys=True)
                stream.write("\n")
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary_name, destination)
        finally:
            if os.path.exists(temporary_name):
                os.unlink(temporary_name)
        return digest
