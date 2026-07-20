from __future__ import annotations

from .self_model import SelfModelStore


def restore_self_model_from_snapshot(
    snapshot: dict,
) -> SelfModelStore:
    store = SelfModelStore()

    records = sorted(
        snapshot[
            "records"
        ].values(),
        key=lambda record: (
            int(
                record[
                    "valid_from"
                ]
            ),
            record[
                "hypothesis_id"
            ],
        ),
    )

    for record in records:
        store.revise(
            domain=(
                record[
                    "domain"
                ]
            ),
            proposition=(
                record[
                    "proposition"
                ]
            ),
            confidence=float(
                record[
                    "confidence"
                ]
            ),
            source_ids=tuple(
                record[
                    "source_ids"
                ]
            ),
            timestamp=int(
                record[
                    "valid_from"
                ]
            ),
            mechanism=(
                record[
                    "mechanism"
                ]
            ),
            mechanism_version=(
                record[
                    "mechanism_version"
                ]
            ),
        )

    return store


def verify_branch_checkpoint_roundtrip(
    promotion_result: dict,
) -> dict:
    c0_snapshot = (
        promotion_result[
            "c0_self_model_state"
        ]
    )
    c1_snapshot = (
        promotion_result[
            "c1_self_model_state"
        ]
    )

    c0_restored = (
        restore_self_model_from_snapshot(
            c0_snapshot
        )
    )

    c1_restored = (
        restore_self_model_from_snapshot(
            c1_snapshot
        )
    )

    c0_exact = (
        c0_restored.to_dict()[
            "state_hash"
        ]
        == c0_snapshot[
            "state_hash"
        ]
    )

    c1_exact = (
        c1_restored.to_dict()[
            "state_hash"
        ]
        == c1_snapshot[
            "state_hash"
        ]
    )

    c0_other = (
        c0_restored.active(
            "other_minds"
        )
    )

    c1_other = (
        c1_restored.active(
            "other_minds"
        )
    )

    return {
        "experiment_id": (
            "SL-CANONICAL-BRANCH-"
            "CHECKPOINT-ROUNDTRIP-001"
        ),
        "c0_exact_roundtrip": (
            c0_exact
        ),
        "c1_exact_roundtrip": (
            c1_exact
        ),
        "c0_state_hash": (
            c0_restored.to_dict()[
                "state_hash"
            ]
        ),
        "c1_state_hash": (
            c1_restored.to_dict()[
                "state_hash"
            ]
        ),
        "branches_remain_distinct": (
            c0_restored.to_dict()[
                "state_hash"
            ]
            != c1_restored.to_dict()[
                "state_hash"
            ]
        ),
        "c0_other_minds_proposition": (
            c0_other.proposition
            if c0_other
            else None
        ),
        "c1_other_minds_proposition": (
            c1_other.proposition
            if c1_other
            else None
        ),
        "c1_promotion_persisted": (
            c1_other is not None
            and c1_other.mechanism
            == "canonical_reflection_promotion"
        ),
    }
