from __future__ import annotations

from dataclasses import dataclass, asdict
from collections import defaultdict

from .core import canonical_hash


@dataclass
class ReliabilityBucket:
    successes: int = 0
    failures: int = 0
    provider_confidence_sum: float = 0.0

    def observe(
        self,
        *,
        correct: bool,
        provider_confidence: float,
    ):
        if correct:
            self.successes += 1
        else:
            self.failures += 1
        self.provider_confidence_sum += float(
            provider_confidence
        )

    @property
    def count(
        self,
    ) -> int:
        return (
            self.successes
            + self.failures
        )

    @property
    def empirical_accuracy(
        self,
    ) -> float:
        if not self.count:
            return 0.0
        return (
            self.successes
            / self.count
        )

    @property
    def mean_provider_confidence(
        self,
    ) -> float:
        if not self.count:
            return 0.0
        return (
            self.provider_confidence_sum
            / self.count
        )

    @property
    def beta_posterior_mean(
        self,
    ) -> float:
        # Uniform Beta(1,1) prior.
        return (
            1
            + self.successes
        ) / (
            2
            + self.count
        )

    @property
    def calibration_gap(
        self,
    ) -> float:
        return (
            self.mean_provider_confidence
            - self.empirical_accuracy
        )

    def to_dict(
        self,
    ):
        return {
            **asdict(
                self
            ),
            "count": self.count,
            "empirical_accuracy": (
                self.empirical_accuracy
            ),
            "mean_provider_confidence": (
                self.mean_provider_confidence
            ),
            "beta_posterior_mean": (
                self.beta_posterior_mean
            ),
            "calibration_gap": (
                self.calibration_gap
            ),
        }


class ProviderReliabilityRegistry:
    """Task-specific empirical reliability separate from provider self-confidence."""

    def __init__(
        self,
    ):
        self.buckets = defaultdict(
            ReliabilityBucket
        )

    def observe(
        self,
        *,
        provider_id: str,
        model_id: str,
        task_id: str,
        correct: bool,
        provider_confidence: float,
    ):
        key = (
            provider_id,
            model_id,
            task_id,
        )
        self.buckets[
            key
        ].observe(
            correct=correct,
            provider_confidence=(
                provider_confidence
            ),
        )

    def conservative_trust(
        self,
        *,
        provider_id: str,
        model_id: str,
        task_id: str,
        provider_confidence: float,
    ) -> float:
        bucket = self.buckets.get(
            (
                provider_id,
                model_id,
                task_id,
            )
        )

        if (
            bucket is None
            or bucket.count == 0
        ):
            # Unknown task reliability: do not promote provider confidence to
            # empirical certainty.
            return min(
                float(
                    provider_confidence
                ),
                .50,
            )

        return min(
            float(
                provider_confidence
            ),
            bucket.beta_posterior_mean,
        )

    def to_dict(
        self,
    ):
        payload = {
            "|".join(
                key
            ): value.to_dict()
            for key, value
            in sorted(
                self.buckets.items()
            )
        }

        return {
            "buckets": payload,
            "state_hash": (
                canonical_hash(
                    payload
                )
            ),
        }
