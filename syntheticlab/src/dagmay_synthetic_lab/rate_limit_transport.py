from __future__ import annotations

import re
import time
from typing import Callable

from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
    ProviderTransportError,
)


class RateLimitSafeTransport:
    """Paces live Gemini calls and retries free-tier HTTP 429 responses.

    The free-tier Gemini 3.1 Flash Lite quota observed during this experiment is
    15 requests per minute. A 4.25-second minimum start interval stays below
    that ceiling with a small safety margin.

    This wrapper is intentionally transport-only:
    - it does not alter prompts;
    - it does not alter model outputs;
    - it does not bypass quota;
    - it preserves the outer experiment's per-success checkpointing.
    """

    def __init__(
        self,
        *,
        inner: Callable[[dict], dict] | None = None,
        minimum_interval_seconds: float = 4.25,
        max_429_retries: int = 5,
        fallback_retry_seconds: float = 65.0,
    ):
        self.inner = (
            inner
            if inner is not None
            else GeminiInteractionsTransport()
        )
        self.minimum_interval_seconds = float(
            minimum_interval_seconds
        )
        self.max_429_retries = int(
            max_429_retries
        )
        self.fallback_retry_seconds = float(
            fallback_retry_seconds
        )
        self._last_start_monotonic: float | None = None

    def _pace(
        self,
    ):
        if self._last_start_monotonic is None:
            return

        elapsed = (
            time.monotonic()
            - self._last_start_monotonic
        )

        remaining = (
            self.minimum_interval_seconds
            - elapsed
        )

        if remaining > 0:
            time.sleep(
                remaining
            )

    @staticmethod
    def _retry_seconds_from_error(
        message: str,
    ) -> float | None:
        patterns = (
            r"retry in\s+([0-9]+(?:\.[0-9]+)?)s",
            r"retry after\s+([0-9]+(?:\.[0-9]+)?)",
        )

        for pattern in patterns:
            match = re.search(
                pattern,
                message,
                flags=re.IGNORECASE,
            )
            if match:
                return float(
                    match.group(
                        1
                    )
                )

        return None

    def __call__(
        self,
        payload: dict,
    ) -> dict:
        attempt = 0

        while True:
            self._pace()
            self._last_start_monotonic = (
                time.monotonic()
            )

            try:
                return self.inner(
                    payload
                )
            except ProviderTransportError as exc:
                message = str(
                    exc
                )

                is_rate_limit = (
                    "429"
                    in message
                    or "too many requests"
                    in message.lower()
                    or "quota exceeded"
                    in message.lower()
                )

                if (
                    not is_rate_limit
                    or attempt
                    >= self.max_429_retries
                ):
                    raise

                requested_wait = (
                    self._retry_seconds_from_error(
                        message
                    )
                )

                wait_seconds = (
                    max(
                        self.fallback_retry_seconds,
                        (
                            requested_wait
                            + 3.0
                            if requested_wait
                            is not None
                            else 0.0
                        ),
                    )
                )

                print(
                    "Gemini rate limit reached. "
                    f"Waiting {wait_seconds:.1f}s before retry "
                    f"{attempt + 1}/{self.max_429_retries}..."
                )

                time.sleep(
                    wait_seconds
                )

                # Reset pacing after the long quota wait.
                self._last_start_monotonic = None
                attempt += 1
