from __future__ import annotations

from .perspective_anchor import (
    PerspectiveAnchorStore,
)
from .evidence_attribution import (
    EvidenceAttribution,
    EvidenceAttributionLedger,
    OwnershipStatus,
)
from .deictic_binding import (
    DeicticBindingIndex,
    DeicticRelation,
)


def _make_anchor(
    stream_id: str,
):
    store = (
        PerspectiveAnchorStore()
    )
    record = store.revise(
        candidate_stream_id=(
            stream_id
        ),
        confidence=.95,
        evidence_ids=(
            "ANCHOR-EVIDENCE-1",
        ),
        timestamp=1800,
        mechanism=(
            "causal_perspective_learner"
        ),
    )
    return store, record


def run_deictic_binding_experiments(
    longitudinal_analysis: dict,
) -> dict:
    focal_stream = (
        "PERSPECTIVE-A"
    )
    other_stream = (
        "PERSPECTIVE-B"
    )

    _store, anchor = (
        _make_anchor(
            focal_stream
        )
    )

    ledger = (
        EvidenceAttributionLedger()
    )

    # Register every active longitudinal hypothesis source as belonging to the
    # functionally anchored perspective.
    active = (
        longitudinal_analysis[
            "final_active_hypotheses"
        ]
    )

    for record in active.values():
        for evidence_id in (
            record[
                "source_ids"
            ]
        ):
            if ledger.get(
                evidence_id
            ) is None:
                ledger.register(
                    EvidenceAttribution(
                        evidence_id=(
                            evidence_id
                        ),
                        owner_stream_id=(
                            focal_stream
                        ),
                        ownership_status=(
                            OwnershipStatus.OWNED
                        ),
                        source_mechanism=(
                            "longitudinal_verified_provenance"
                        ),
                        confidence=1.0,
                    )
                )

    # Add explicit other/mixed/unknown controls.
    ledger.register(
        EvidenceAttribution(
            evidence_id=(
                "OTHER-EVIDENCE-1"
            ),
            owner_stream_id=(
                other_stream
            ),
            ownership_status=(
                OwnershipStatus.OBSERVED_OTHER
            ),
            source_mechanism=(
                "control"
            ),
            confidence=1.0,
        )
    )
    ledger.register(
        EvidenceAttribution(
            evidence_id=(
                "FOCAL-MIXED-1"
            ),
            owner_stream_id=(
                focal_stream
            ),
            ownership_status=(
                OwnershipStatus.OWNED
            ),
            source_mechanism=(
                "control"
            ),
            confidence=1.0,
        )
    )
    ledger.register(
        EvidenceAttribution(
            evidence_id=(
                "OTHER-MIXED-1"
            ),
            owner_stream_id=(
                other_stream
            ),
            ownership_status=(
                OwnershipStatus.OBSERVED_OTHER
            ),
            source_mechanism=(
                "control"
            ),
            confidence=1.0,
        )
    )

    index = (
        DeicticBindingIndex()
    )

    focal_bindings = []

    for domain, record in sorted(
        active.items()
    ):
        binding = index.bind(
            anchor=anchor,
            hypothesis_id=(
                record[
                    "hypothesis_id"
                ]
            ),
            hypothesis_domain=(
                domain
            ),
            evidence_ids=tuple(
                record[
                    "source_ids"
                ]
            ),
            ledger=ledger,
            timestamp=1800,
        )
        focal_bindings.append(
            binding
        )

    other = index.bind(
        anchor=anchor,
        hypothesis_id=(
            "CONTROL-OTHER"
        ),
        hypothesis_domain=(
            "other_entity_model"
        ),
        evidence_ids=(
            "OTHER-EVIDENCE-1",
        ),
        ledger=ledger,
        timestamp=1800,
    )

    mixed = index.bind(
        anchor=anchor,
        hypothesis_id=(
            "CONTROL-MIXED"
        ),
        hypothesis_domain=(
            "mixed"
        ),
        evidence_ids=(
            "FOCAL-MIXED-1",
            "OTHER-MIXED-1",
        ),
        ledger=ledger,
        timestamp=1800,
    )

    unknown = index.bind(
        anchor=anchor,
        hypothesis_id=(
            "CONTROL-UNKNOWN"
        ),
        hypothesis_domain=(
            "unknown"
        ),
        evidence_ids=(
            "UNREGISTERED-EVIDENCE",
        ),
        ledger=ledger,
        timestamp=1800,
    )

    # Anchor-swap counterfactual: the same hypothesis evidence should become
    # OTHER if the functional perspective anchor is moved to another stream.
    _other_store, other_anchor = (
        _make_anchor(
            other_stream
        )
    )
    swap_index = (
        DeicticBindingIndex()
    )

    swapped = []

    for domain, record in sorted(
        active.items()
    ):
        swapped.append(
            swap_index.bind(
                anchor=(
                    other_anchor
                ),
                hypothesis_id=(
                    record[
                        "hypothesis_id"
                    ]
                ),
                hypothesis_domain=(
                    domain
                ),
                evidence_ids=tuple(
                    record[
                        "source_ids"
                    ]
                ),
                ledger=ledger,
                timestamp=1801,
            )
        )

    focal_rate = (
        sum(
            1
            for binding
            in focal_bindings
            if binding.relation
            == DeicticRelation.FOCAL_PERSPECTIVE
        )
        / len(
            focal_bindings
        )
    )

    swapped_other_rate = (
        sum(
            1
            for binding
            in swapped
            if binding.relation
            == DeicticRelation.OTHER_PERSPECTIVE
        )
        / len(
            swapped
        )
    )

    result = {
        "experiment_id": (
            "SL-DEICTIC-BINDING-OFFLINE-001"
        ),
        "longitudinal_active_hypothesis_count": (
            len(
                active
            )
        ),
        "focal_binding_rate": (
            focal_rate
        ),
        "other_control_relation": (
            other.relation.value
        ),
        "mixed_control_relation": (
            mixed.relation.value
        ),
        "unknown_control_relation": (
            unknown.relation.value
        ),
        "anchor_swap_converts_original_focal_to_other_rate": (
            swapped_other_rate
        ),
        "first_person_language_required": (
            False
        ),
        "llm_required_for_binding": (
            False
        ),
        "interpretation": (
            "The active third-person longitudinal SelfModel can be functionally "
            "indexed to a learned perspective anchor using provenance alone. "
            "Moving the anchor changes the deictic relation without rewriting "
            "hypothesis content, demonstrating that perspective binding is a "
            "separate structural relation rather than a pronoun or hard-coded SELF flag."
        ),
    }

    assert focal_rate == 1.0
    assert (
        other.relation
        == DeicticRelation.OTHER_PERSPECTIVE
    )
    assert (
        mixed.relation
        == DeicticRelation.MIXED_PROVENANCE
    )
    assert (
        unknown.relation
        == DeicticRelation.UNKNOWN
    )
    assert (
        swapped_other_rate
        == 1.0
    )

    return result
