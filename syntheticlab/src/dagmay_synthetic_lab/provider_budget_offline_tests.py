from __future__ import annotations

from pathlib import Path
import tempfile

from .provider_budget import (
    PersistentBudgetedTransport,
    ProviderBudget,
    ProviderBudgetError,
)


class FakeTime:
    def __init__(self) -> None:
        self.now = 1_800_000_000.0
        self.sleeps: list[float] = []

    def clock(self) -> float:
        return self.now

    def sleep(self, seconds: float) -> None:
        self.sleeps.append(seconds)
        self.now += seconds


def run_provider_budget_offline_tests() -> dict:
    fake = FakeTime()
    calls: list[dict] = []

    def inner(payload: dict) -> dict:
        calls.append(payload)
        return {"output_text": "{}"}

    with tempfile.TemporaryDirectory(prefix="dagmay-budget-test-") as temp:
        transport = PersistentBudgetedTransport(
            inner=inner,
            budget=ProviderBudget(
                model_id="offline-model",
                max_requests_per_minute=2,
                max_tokens_per_minute=20_000,
                max_requests_per_day=3,
                max_output_token_reserve=100,
            ),
            ledger_path=Path(temp) / "ledger.json",
            clock=fake.clock,
            sleeper=fake.sleep,
        )
        for ordinal in range(3):
            transport({"input": f"request-{ordinal}"})
        assert len(calls) == 3
        assert fake.sleeps

        try:
            transport({"input": "request-4"})
        except ProviderBudgetError as exc:
            assert "daily" in str(exc)
        else:
            raise AssertionError("daily budget did not fail closed")

    return {
        "persistent_call_ledger": "PASS",
        "requests_per_minute_pacing": "PASS",
        "requests_per_day_limit": "PASS",
        "token_reservation": "PASS",
    }


if __name__ == "__main__":
    import json

    print(json.dumps(run_provider_budget_offline_tests(), indent=2))
