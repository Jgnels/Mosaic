from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable
import json
import os
import tempfile
import time

from .provider_payload_security import canonical_json, payload_hash


class ProviderBudgetError(RuntimeError):
    pass


@dataclass(frozen=True)
class ProviderBudget:
    model_id: str
    max_requests_per_minute: int
    max_tokens_per_minute: int
    max_requests_per_day: int
    max_output_token_reserve: int = 4096

    def __post_init__(self) -> None:
        if not self.model_id:
            raise ProviderBudgetError("model_id is required")
        if min(
            self.max_requests_per_minute,
            self.max_tokens_per_minute,
            self.max_requests_per_day,
            self.max_output_token_reserve,
        ) <= 0:
            raise ProviderBudgetError("all budget limits must be positive")


def conservative_token_estimate(payload: dict) -> int:
    # A deliberately conservative character/token approximation. The output
    # reserve is accounted for separately before every call.
    return max(1, (len(canonical_json(payload)) + 2) // 3)


class PersistentBudgetedTransport:
    def __init__(
        self,
        *,
        inner: Callable[[dict], dict],
        budget: ProviderBudget,
        ledger_path: str | Path,
        clock: Callable[[], float] = time.time,
        sleeper: Callable[[float], None] = time.sleep,
    ) -> None:
        self.inner = inner
        self.budget = budget
        self.ledger_path = Path(ledger_path)
        self.clock = clock
        self.sleeper = sleeper

    def _load(self) -> dict:
        if not self.ledger_path.exists():
            return {"schema_version": 1, "events": []}
        value = json.loads(self.ledger_path.read_text(encoding="utf-8"))
        if value.get("schema_version") != 1 or not isinstance(value.get("events"), list):
            raise ProviderBudgetError("provider budget ledger is invalid")
        return value

    def _write(self, ledger: dict) -> None:
        self.ledger_path.parent.mkdir(parents=True, exist_ok=True)
        handle, temporary_name = tempfile.mkstemp(
            prefix=".provider-budget-",
            suffix=".tmp",
            dir=self.ledger_path.parent,
            text=True,
        )
        try:
            with os.fdopen(handle, "w", encoding="utf-8", newline="\n") as stream:
                json.dump(ledger, stream, indent=2, sort_keys=True)
                stream.write("\n")
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary_name, self.ledger_path)
        finally:
            if os.path.exists(temporary_name):
                os.unlink(temporary_name)

    @staticmethod
    def _utc_day(timestamp: float) -> str:
        return datetime.fromtimestamp(timestamp, timezone.utc).date().isoformat()

    def _reserve(self, payload: dict) -> dict:
        now = self.clock()
        day = self._utc_day(now)
        requested_tokens = (
            conservative_token_estimate(payload)
            + self.budget.max_output_token_reserve
        )
        if requested_tokens > self.budget.max_tokens_per_minute:
            raise ProviderBudgetError("one request exceeds the configured token/minute budget")

        ledger = self._load()
        model_events = [
            event
            for event in ledger["events"]
            if event.get("model_id") == self.budget.model_id
        ]
        today_events = [event for event in model_events if event.get("utc_day") == day]
        if len(today_events) >= self.budget.max_requests_per_day:
            raise ProviderBudgetError("daily provider request budget exhausted")

        recent = [event for event in model_events if now - float(event["timestamp"]) < 60.0]
        request_wait = 0.0
        if len(recent) >= self.budget.max_requests_per_minute:
            request_wait = 60.0 - (now - float(recent[0]["timestamp"]))

        running_tokens = sum(int(event["reserved_tokens"]) for event in recent)
        token_wait = 0.0
        if running_tokens + requested_tokens > self.budget.max_tokens_per_minute and recent:
            token_wait = 60.0 - (now - float(recent[0]["timestamp"]))

        minimum_interval = 60.0 / self.budget.max_requests_per_minute
        interval_wait = 0.0
        if recent:
            interval_wait = minimum_interval - (now - float(recent[-1]["timestamp"]))

        wait = max(0.0, request_wait, token_wait, interval_wait)
        if wait > 0:
            self.sleeper(wait + 0.05)
            return self._reserve(payload)

        event = {
            "event_id": payload_hash({
                "payload_hash": payload_hash(payload),
                "timestamp": now,
                "ordinal": len(ledger["events"]),
            }),
            "model_id": self.budget.model_id,
            "timestamp": now,
            "utc_day": day,
            "reserved_tokens": requested_tokens,
            "provider_request_hash": payload_hash(payload),
            "status": "RESERVED",
        }
        ledger["events"].append(event)
        self._write(ledger)
        return event

    def _mark(self, event_id: str, status: str) -> None:
        ledger = self._load()
        matching = [event for event in ledger["events"] if event.get("event_id") == event_id]
        if len(matching) != 1:
            raise ProviderBudgetError("provider budget reservation is missing or ambiguous")
        matching[0]["status"] = status
        matching[0]["completed_timestamp"] = self.clock()
        self._write(ledger)

    def __call__(self, payload: dict) -> dict:
        event = self._reserve(payload)
        try:
            response = self.inner(payload)
        except Exception:
            self._mark(event["event_id"], "FAILED_ATTEMPT")
            raise
        self._mark(event["event_id"], "SUCCEEDED")
        return response
