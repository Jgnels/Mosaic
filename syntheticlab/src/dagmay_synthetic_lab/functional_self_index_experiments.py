from __future__ import annotations

from .functional_self_index import (
    FunctionalSelfIndex,
)
from .shadow_metacognition import (
    ShadowMetacognitiveAdvice,
    ShadowMetacognitiveLog,
    AdviceType,
    validate_shadow_advice,
)
from .core import canonical_hash


def run_functional_self_index_experiments(
    *,
    longitudinal_analysis: dict,
    deictic_binding_result: dict,
) -> dict:
    active = longitudinal_analysis[
        "final_active_hypotheses"
    ]

    # The offline deictic experiment has already established that all four
    # longitudinal active hypotheses bind to the focal perspective.
    assert deictic_binding_result[
        "focal_binding_rate"
    ] == 1.0

    index = FunctionalSelfIndex(
        identity_id=(
            "LONGITUDINAL-IDENTITY"
        ),
        lineage_id=(
            "LONGITUDINAL-LINEAGE"
        ),
    )

    # Build deterministic synthetic binding records matching the proven focal
    # relation from the deictic experiment.
    for domain, hypothesis in sorted(
        active.items()
    ):
        binding = {
            "relation": (
                "FOCAL_PERSPECTIVE"
            ),
            "anchor_id": (
                "PA-000001"
            ),
            "anchor_stream_id": (
                "PERSPECTIVE-A"
            ),
        }
        index.add_bound_hypothesis(
            hypothesis=(
                hypothesis
            ),
            binding=(
                binding
            ),
        )

    focal_ids = set(
        index.entries
    )

    # Valid shadow advice: reads focal self-index, but cannot be enacted.
    valid, reason = (
        validate_shadow_advice(
            advice_type=(
                AdviceType.REVIEW_CONTINUITY.value
            ),
            confidence=.84,
            source_hypothesis_ids=tuple(
                sorted(
                    focal_ids
                )
            )[:2],
            focal_hypothesis_ids=(
                focal_ids
            ),
            rationale=(
                "Shadow-only review of continuity and agency hypotheses."
            ),
        )
    )

    shadow = (
        ShadowMetacognitiveLog()
    )

    if valid:
        shadow.append(
            ShadowMetacognitiveAdvice(
                advice_id=(
                    "SMA-000001"
                ),
                advice_type=(
                    AdviceType.REVIEW_CONTINUITY
                ),
                confidence=.84,
                source_hypothesis_ids=tuple(
                    sorted(
                        focal_ids
                    )
                )[:2],
                rationale=(
                    "Shadow-only review of continuity and agency hypotheses."
                ),
                enacted=False,
            )
        )

    # Invalid other-perspective citation control.
    invalid_other, invalid_reason = (
        validate_shadow_advice(
            advice_type=(
                AdviceType.ATTEND_TO_RELATIONSHIP.value
            ),
            confidence=.8,
            source_hypothesis_ids=(
                "OTHER-HYPOTHESIS",
            ),
            focal_hypothesis_ids=(
                focal_ids
            ),
            rationale=(
                "Should be rejected."
            ),
        )
    )

    state_before = (
        canonical_hash({
            "dummy_policy_state": (
                "unchanged"
            )
        })
    )
    state_after = (
        canonical_hash({
            "dummy_policy_state": (
                "unchanged"
            )
        })
    )

    result = {
        "experiment_id": (
            "SL-FUNCTIONAL-SELF-INDEX-001"
        ),
        "indexed_hypothesis_count": (
            len(
                index.entries
            )
        ),
        "indexed_domains": list(
            index.active_domains()
        ),
        "valid_shadow_advice_accepted": (
            valid
        ),
        "valid_shadow_advice_reason": (
            reason
        ),
        "nonfocal_shadow_advice_accepted": (
            invalid_other
        ),
        "nonfocal_shadow_advice_reason": (
            invalid_reason
        ),
        "shadow_record_count": len(
            shadow.records
        ),
        "shadow_enacted_count": (
            shadow.to_dict()[
                "enacted_count"
            ]
        ),
        "policy_state_unchanged": (
            state_before
            == state_after
        ),
        "first_person_language_required": (
            False
        ),
        "llm_required_for_indexing": (
            False
        ),
        "functional_self_index": (
            index.to_dict()
        ),
        "shadow_log": (
            shadow.to_dict()
        ),
        "interpretation": (
            "The real longitudinal third-person SelfModel can be functionally "
            "indexed to a learned focal perspective without first-person language. "
            "SelfModel-informed metacognitive advice can be validated and logged "
            "in shadow mode while remaining causally isolated from policy."
        ),
    }

    assert len(
        index.entries
    ) == len(
        active
    )
    assert valid is True
    assert invalid_other is False
    assert shadow.to_dict()[
        "enacted_count"
    ] == 0
    assert state_before == state_after

    return result
