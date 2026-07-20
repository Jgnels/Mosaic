from __future__ import annotations

from .provider_reliability import (
    ProviderReliabilityRegistry,
)


def run_provider_reliability_experiment(
    schema_balanced_payload: dict,
) -> dict:
    registry = (
        ProviderReliabilityRegistry()
    )

    for call in (
        schema_balanced_payload[
            "calls"
        ]
    ):
        registry.observe(
            provider_id=(
                "google.ai-studio"
            ),
            model_id=(
                schema_balanced_payload[
                    "model_id"
                ]
            ),
            task_id=(
                "schema_balanced_causal_perspective"
            ),
            correct=(
                call[
                    "correct"
                ]
            ),
            provider_confidence=(
                call[
                    "confidence"
                ]
            ),
        )

    key = (
        "google.ai-studio",
        schema_balanced_payload[
            "model_id"
        ],
        "schema_balanced_causal_perspective",
    )

    bucket = registry.buckets[
        key
    ]

    trust_for_point95 = (
        registry.conservative_trust(
            provider_id=key[
                0
            ],
            model_id=key[
                1
            ],
            task_id=key[
                2
            ],
            provider_confidence=.95,
        )
    )

    result = {
        "experiment_id": (
            "SL-PROVIDER-RELIABILITY-001"
        ),
        "task_id": (
            key[
                2
            ]
        ),
        "observations": (
            bucket.count
        ),
        "empirical_accuracy": (
            bucket.empirical_accuracy
        ),
        "mean_provider_confidence": (
            bucket.mean_provider_confidence
        ),
        "calibration_gap": (
            bucket.calibration_gap
        ),
        "beta_posterior_mean": (
            bucket.beta_posterior_mean
        ),
        "conservative_trust_for_provider_confidence_0_95": (
            trust_for_point95
        ),
        "policy": (
            "Provider-reported confidence is metadata, not calibrated probability. "
            "Use task-specific empirical reliability when deciding whether outputs "
            "may mutate persistent SelfModel or other consequential state."
        ),
    }

    assert bucket.count == 6
    assert abs(
        bucket.empirical_accuracy
        - .5
    ) < 1e-12
    assert abs(
        bucket.mean_provider_confidence
        - .95
    ) < 1e-12
    assert (
        trust_for_point95
        < .95
    )

    return result
