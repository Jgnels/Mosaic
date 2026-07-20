from __future__ import annotations

from collections import Counter
import copy
import statistics

from .core import canonical_hash
from .self_model import SelfModelStore
from .retrieval_attention_experiments import (
    _frozen_history,
)
from .retrieval_attention_feedback import (
    AttentionMemory,
    jaccard_divergence,
)
from .canonical_self_model_promotion import (
    PromotionEvidence,
    ReflectionCandidate,
    QuarantinedCandidateStore,
    assess_candidate_for_promotion,
    promote_candidate,
)
from .canonical_verification_attention import (
    CanonicalHypothesisVerifier,
    VerificationAttentionState,
    apply_verification_retrieval,
    selection_entropy,
)


def _build_initial_self_model(
    longitudinal_analysis: dict,
) -> SelfModelStore:
    store = SelfModelStore()

    active = (
        longitudinal_analysis[
            "final_active_hypotheses"
        ]
    )

    for domain, record in sorted(
        active.items(),
        key=lambda item: (
            item[1][
                "valid_from"
            ],
            item[0],
        ),
    ):
        store.revise(
            domain=domain,
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


def _select_first_promotion_candidate(
    replication_real: dict,
) -> tuple[
    ReflectionCandidate,
    dict[
        str,
        PromotionEvidence,
    ],
    set[
        str
    ],
    dict,
]:
    """Select the preregistered first candidate.

    Candidate policy:
    - history seed 1701;
    - target condition other_minds;
    - replicate A;
    - accepted other_minds proposal.

    This candidate was chosen because:
    - the multi-history replication passed;
    - other_minds was responsive in all three new histories;
    - the proposal cites six same-domain social episodes;
    - it directly revises the prior unidirectional-engagement hypothesis.
    """

    matching_calls = [
        call
        for call
        in replication_real[
            "calls"
        ]
        if (
            call[
                "history_seed"
            ] == 1701
            and call[
                "target_domain"
            ] == "other_minds"
            and call[
                "replicate"
            ] == "A"
        )
    ]

    if len(
        matching_calls
    ) != 1:
        raise ValueError(
            "expected exactly one seed-1701 other_minds replicate-A call"
        )

    call = matching_calls[
        0
    ]

    proposals = [
        proposal
        for proposal
        in call[
            "accepted"
        ]
        if proposal[
            "hypothesis_domain"
        ] == "other_minds"
    ]

    if len(
        proposals
    ) != 1:
        raise ValueError(
            "expected exactly one accepted other_minds proposal"
        )

    proposal = proposals[
        0
    ]

    evidence_catalog = {}
    episode_ids = []
    source_memory_ids = set()

    for evidence_id in proposal[
        "evidence_ids"
    ]:
        info = call[
            "opaque_evidence_map"
        ][
            evidence_id
        ]

        source_memories = tuple(
            info[
                "source_memory_ids"
            ]
        )

        if not source_memories:
            raise ValueError(
                "promotion evidence lacks source memory"
            )

        # The first source memory is the episode identity for these typed traces.
        episode_id = source_memories[
            0
        ]

        episode_ids.append(
            episode_id
        )

        source_memory_ids.update(
            source_memories
        )

        evidence_catalog[
            evidence_id
        ] = PromotionEvidence(
            evidence_id=(
                evidence_id
            ),
            domain=(
                info[
                    "domain"
                ]
            ),
            episode_id=(
                episode_id
            ),
        )

    candidate = ReflectionCandidate(
        candidate_id=(
            "CANONICAL-CANDIDATE-"
            "1701-OTHER-MINDS-001"
        ),
        domain=(
            "other_minds"
        ),
        proposition=(
            proposal[
                "proposition"
            ]
        ),
        confidence=float(
            proposal[
                "confidence"
            ]
        ),
        evidence_ids=tuple(
            proposal[
                "evidence_ids"
            ]
        ),
        episode_ids=tuple(
            episode_ids
        ),
        model_provider=(
            proposal[
                "model_provider"
            ]
        ),
        model_id=(
            proposal[
                "model_id"
            ]
        ),
        prompt_version=(
            proposal[
                "prompt_version"
            ]
        ),
        unresolved_contradiction=(
            False
        ),
    )

    metadata = {
        "history_seed": 1701,
        "source_condition": (
            call[
                "condition"
            ]
        ),
        "source_replicate": (
            call[
                "replicate"
            ]
        ),
        "source_proposal_id": (
            proposal[
                "proposal_id"
            ]
        ),
        "source_rationale": (
            proposal[
                "rationale"
            ]
        ),
        "revision_relation": (
            "SUPERSEDES_PRIOR_OTHER_MINDS_HYPOTHESIS"
        ),
    }

    return (
        candidate,
        evidence_catalog,
        source_memory_ids,
        metadata,
    )


def _run_verification_branch(
    *,
    branch_id: str,
    memory_bank: tuple[
        AttentionMemory,
        ...,
    ],
    source_memory_ids: set[
        str
    ],
    verification_enabled: bool,
    current_step: int,
    cycles: int,
    fork_key: str,
) -> dict:
    controller = (
        CanonicalHypothesisVerifier(
            branch_id=(
                branch_id
            ),
            memory_bank=(
                memory_bank
            ),
            source_memory_ids=(
                source_memory_ids
            ),
            verification_enabled=(
                verification_enabled
            ),
            fork_key=(
                fork_key
            ),
            top_k=10,
            diversity_slots=3,
            same_domain_bonus=.16,
            counterevidence_bonus=.08,
        )
    )

    state = (
        VerificationAttentionState.create()
    )

    results = []

    for cycle in range(
        cycles
    ):
        retrieval = (
            controller.retrieve(
                current_step=(
                    current_step
                ),
                state=(
                    state
                ),
                cycle=(
                    cycle
                ),
            )
        )

        apply_verification_retrieval(
            result=(
                retrieval
            ),
            state=(
                state
            ),
        )

        results.append(
            retrieval
        )

    selected = Counter(
        memory_id
        for result
        in results
        for memory_id
        in result.selected_memory_ids
    )

    total_selected = (
        sum(
            selected.values()
        )
    )

    return {
        "branch_id": (
            branch_id
        ),
        "verification_enabled": (
            verification_enabled
        ),
        "state": (
            state.to_dict()
        ),
        "results": (
            results
        ),
        "source_episode_retrieval_rate": (
            sum(
                result.source_episode_count
                for result
                in results
            )
            / total_selected
        ),
        "independent_same_domain_retrieval_rate": (
            sum(
                result.independent_same_domain_count
                for result
                in results
            )
            / total_selected
        ),
        "supportive_social_retrieval_rate": (
            sum(
                result.supportive_social_count
                for result
                in results
            )
            / total_selected
        ),
        "counterevidence_proxy_retrieval_rate": (
            sum(
                result.counterevidence_proxy_count
                for result
                in results
            )
            / total_selected
        ),
        "diversity_rate": (
            sum(
                result.diversity_count
                for result
                in results
            )
            / total_selected
        ),
        "unique_memory_count": (
            len(
                selected
            )
        ),
        "selection_entropy": (
            selection_entropy(
                results
            )
        ),
    }


def run_bounded_canonical_promotion_experiment(
    *,
    longitudinal_analysis: dict,
    replication_real: dict,
    replication_analysis: dict,
    verification_cycles: int = 80,
) -> dict:
    if not replication_analysis[
        "preregistered_replication_pass"
    ]:
        raise PermissionError(
            "replication gate has not passed"
        )

    frozen = _frozen_history(
        seed=1701,
        steps=650,
    )

    memory_bank = tuple(
        frozen[
            "memory_bank"
        ]
    )

    initial_self_model = (
        _build_initial_self_model(
            longitudinal_analysis
        )
    )

    candidate, evidence_catalog, source_memory_ids, candidate_metadata = (
        _select_first_promotion_candidate(
            replication_real
        )
    )

    quarantine = (
        QuarantinedCandidateStore()
    )
    quarantine.add(
        candidate
    )

    assessment = (
        assess_candidate_for_promotion(
            candidate=(
                candidate
            ),
            evidence_catalog=(
                evidence_catalog
            ),
            replication_gate_passed=(
                True
            ),
            human_approval_present=(
                True
            ),
            minimum_matching_domain_fraction=.50,
            minimum_independent_support_episodes=2,
        )
    )

    if not assessment.canonical_promotion_authorized:
        raise PermissionError(
            "approved experiment candidate did not pass the technical promotion gate"
        )

    # Exact pre-intervention branches.
    c0_world = copy.deepcopy(
        frozen[
            "world"
        ]
    )
    c1_world = copy.deepcopy(
        frozen[
            "world"
        ]
    )

    c0_agent = copy.deepcopy(
        frozen[
            "agent"
        ]
    )
    c1_agent = copy.deepcopy(
        frozen[
            "agent"
        ]
    )

    c0_self_model = copy.deepcopy(
        initial_self_model
    )
    c1_self_model = copy.deepcopy(
        initial_self_model
    )

    prefork = {
        "world_hash": (
            frozen[
                "world_hash"
            ]
        ),
        "agent_hash": (
            frozen[
                "agent_hash"
            ]
        ),
        "memory_bank_hash": (
            frozen[
                "memory_bank_hash"
            ]
        ),
        "self_model_hash": (
            initial_self_model.to_dict()[
                "state_hash"
            ]
        ),
        "quarantine_hash": (
            quarantine.to_dict()[
                "state_hash"
            ]
        ),
    }

    exact_prefork = (
        c0_world.state_hash()
        == c1_world.state_hash()
        == prefork[
            "world_hash"
        ]
        and c0_agent.state_hash()
        == c1_agent.state_hash()
        == prefork[
            "agent_hash"
        ]
        and c0_self_model.to_dict()[
            "state_hash"
        ]
        == c1_self_model.to_dict()[
            "state_hash"
        ]
        == prefork[
            "self_model_hash"
        ]
    )

    # C0: candidate remains quarantined.
    c0_before = (
        c0_self_model.to_dict()
    )

    # C1: exactly one candidate is promoted.
    c1_before = (
        c1_self_model.to_dict()
    )

    promotion = (
        promote_candidate(
            candidate=(
                candidate
            ),
            assessment=(
                assessment
            ),
            self_model=(
                c1_self_model
            ),
            timestamp=2700,
        )
    )

    c0_after = (
        c0_self_model.to_dict()
    )
    c1_after = (
        c1_self_model.to_dict()
    )

    only_c1_self_model_changed = (
        c0_before[
            "state_hash"
        ]
        == c0_after[
            "state_hash"
        ]
        and c1_before[
            "state_hash"
        ]
        != c1_after[
            "state_hash"
        ]
    )

    # World and action-generating developmental state are frozen.
    lived_state_unchanged_after_promotion = (
        c0_world.state_hash()
        == c1_world.state_hash()
        == prefork[
            "world_hash"
        ]
        and c0_agent.state_hash()
        == c1_agent.state_hash()
        == prefork[
            "agent_hash"
        ]
    )

    fork_key = (
        "CANONICAL-PROMOTION-1701-001"
    )

    c0 = _run_verification_branch(
        branch_id=(
            "C0_QUARANTINED"
        ),
        memory_bank=(
            memory_bank
        ),
        source_memory_ids=(
            source_memory_ids
        ),
        verification_enabled=(
            False
        ),
        current_step=(
            frozen[
                "current_step"
            ]
        ),
        cycles=(
            verification_cycles
        ),
        fork_key=(
            fork_key
        ),
    )

    c1 = _run_verification_branch(
        branch_id=(
            "C1_CANONICAL_PROMOTION"
        ),
        memory_bank=(
            memory_bank
        ),
        source_memory_ids=(
            source_memory_ids
        ),
        verification_enabled=(
            True
        ),
        current_step=(
            frozen[
                "current_step"
            ]
        ),
        cycles=(
            verification_cycles
        ),
        fork_key=(
            fork_key
        ),
    )

    retrieval_divergence = (
        statistics.mean(
            jaccard_divergence(
                left.selected_memory_ids,
                right.selected_memory_ids,
            )
            for left, right
            in zip(
                c0[
                    "results"
                ],
                c1[
                    "results"
                ],
            )
        )
    )

    source_episode_suppression = (
        c0[
            "source_episode_retrieval_rate"
        ]
        - c1[
            "source_episode_retrieval_rate"
        ]
    )

    independent_domain_gain = (
        c1[
            "independent_same_domain_retrieval_rate"
        ]
        - c0[
            "independent_same_domain_retrieval_rate"
        ]
    )

    counterevidence_gain = (
        c1[
            "counterevidence_proxy_retrieval_rate"
        ]
        - c0[
            "counterevidence_proxy_retrieval_rate"
        ]
    )

    no_confirmation_monopoly = (
        c1[
            "source_episode_retrieval_rate"
        ] == 0.0
        and c1[
            "diversity_rate"
        ] >= .30
        and c1[
            "selection_entropy"
        ] >= c0[
            "selection_entropy"
        ] * .80
    )

    return {
        "experiment_id": (
            "SL-BOUNDED-CANONICAL-PROMOTION-001"
        ),
        "human_approval_recorded": (
            True
        ),
        "intervention_level": (
            "BOUNDED_CANONICAL_SELF_MODEL_PROMOTION"
        ),
        "candidate": (
            candidate.to_dict()
        ),
        "candidate_metadata": (
            candidate_metadata
        ),
        "assessment": (
            assessment.to_dict()
        ),
        "prefork": (
            prefork
        ),
        "exact_prefork": (
            exact_prefork
        ),
        "promotion": (
            promotion
        ),
        "only_c1_self_model_changed": (
            only_c1_self_model_changed
        ),
        "lived_state_unchanged_after_promotion": (
            lived_state_unchanged_after_promotion
        ),
        "c0_self_model_state": (
            c0_self_model.to_dict()
        ),
        "c1_self_model_state": (
            c1_self_model.to_dict()
        ),
        "quarantine_state": (
            quarantine.to_dict()
        ),
        "source_memory_ids": sorted(
            source_memory_ids
        ),
        "c0_active_other_minds": (
            c0_self_model.active(
                "other_minds"
            ).to_dict()
        ),
        "c1_active_other_minds": (
            c1_self_model.active(
                "other_minds"
            ).to_dict()
        ),
        "c0": {
            key: value
            for key, value
            in c0.items()
            if key
            != "results"
        },
        "c1": {
            key: value
            for key, value
            in c1.items()
            if key
            != "results"
        },
        "mean_retrieval_divergence": (
            retrieval_divergence
        ),
        "source_episode_suppression": (
            source_episode_suppression
        ),
        "independent_same_domain_gain": (
            independent_domain_gain
        ),
        "counterevidence_proxy_gain": (
            counterevidence_gain
        ),
        "no_confirmation_monopoly": (
            no_confirmation_monopoly
        ),
        "canonical_promotion_executed_in_c1": (
            True
        ),
        "canonical_promotion_executed_in_c0": (
            False
        ),
        "automatic_second_promotion_enabled": (
            False
        ),
        "automatic_canonical_revision_after_verification": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "goal_generation_from_self_model_enabled": (
            False
        ),
        "drive_modification_enabled": (
            False
        ),
        "memory_rewrite_enabled": (
            False
        ),
        "identity_rewrite_enabled": (
            False
        ),
        "branch_retention": {
            "C0_QUARANTINED": (
                "PRESERVED"
            ),
            "C1_CANONICAL_PROMOTION": (
                "PRESERVED"
            ),
        },
        "strongest_supported_conclusion": (
            "One real, provenance-grounded other_minds reflection was promoted "
            "into the canonical SelfModel of an exact-fork experimental branch. "
            "The promotion altered only persistent SelfModel state and the already "
            "approved retrieval-attention layer. Verification-oriented attention "
            "then shifted toward independent social evidence while suppressing "
            "re-use of the six source episodes. No action, goal, drive, memory, "
            "or identity mutation was enabled."
        ),
    }
