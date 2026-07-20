from __future__ import annotations

from collections import Counter
import copy
import math
import statistics

from .core import (
    canonical_hash,
    stable_unit_float,
)
from .rich_world import (
    ControlledRichWorld,
    RichWorldConfig,
    subject_visible_event,
)
from .rich_agent import (
    RichDevelopmentalAgent,
)
from .retrieval_attention_feedback import (
    AttentionMemory,
    RetrievalAttentionController,
    RetrievalAttentionState,
    SELF_INDEX_DOMAINS,
    apply_retrieval_to_attention_state,
    jaccard_divergence,
)


def _domain_confidence_from_longitudinal(
    longitudinal_analysis: dict,
) -> dict[str, float]:
    active = longitudinal_analysis[
        "final_active_hypotheses"
    ]

    return {
        domain: float(
            active.get(
                domain,
                {},
            ).get(
                "confidence",
                0.0,
            )
        )
        for domain
        in SELF_INDEX_DOMAINS
    }


def _frozen_history(
    *,
    seed: int,
    steps: int,
):
    config = RichWorldConfig(
        scenario_id=(
            "SL-RETRIEVAL-ATTENTION-001"
        ),
        steps=steps,
        regime_length=max(
            120,
            steps // 3,
        ),
    )

    world = ControlledRichWorld(
        seed,
        config,
    )

    agent = RichDevelopmentalAgent(
        agent_id=(
            f"RET-{seed}"
        ),
        config=config,
        profile_id=(
            "BALANCED_MINIMAL"
        ),
        seed=seed,
    )

    for _ in range(
        steps
    ):
        obs = world.observe()
        action = agent.choose_action(
            obs
        )
        canonical = world.step(
            action
        )
        visible = subject_visible_event(
            canonical
        )
        agent.observe_event(
            obs,
            visible,
        )

    memory_bank = tuple(
        AttentionMemory.from_experience(
            memory
        )
        for memory
        in agent.memories
    )

    return {
        "world": world,
        "agent": agent,
        "memory_bank": memory_bank,
        "world_hash": (
            world.state_hash()
        ),
        "agent_hash": (
            agent.state_hash()
        ),
        "memory_bank_hash": (
            canonical_hash(
                [
                    memory.to_dict()
                    for memory
                    in memory_bank
                ]
            )
        ),
        "current_step": (
            world.state.step
        ),
    }


def _query_schedule(
    *,
    seed: int,
    cycles: int,
) -> tuple[
    str,
    ...,
]:
    domains = tuple(
        SELF_INDEX_DOMAINS
    )
    result = []

    for cycle in range(
        cycles
    ):
        index = int(
            stable_unit_float(
                "retrieval-attention-query",
                seed,
                cycle,
            )
            * len(
                domains
            )
        )
        result.append(
            domains[
                min(
                    index,
                    len(
                        domains
                    )
                    - 1,
                )
            ]
        )

    return tuple(
        result
    )


def _entropy(
    counts: Counter,
) -> float:
    total = sum(
        counts.values()
    )
    if total <= 0:
        return 0.0

    value = 0.0

    for count in (
        counts.values()
    ):
        if count <= 0:
            continue
        p = (
            count
            / total
        )
        value -= (
            p
            * math.log(
                p,
                2,
            )
        )

    return value


def _run_branch(
    *,
    branch_id: str,
    memory_bank: tuple[
        AttentionMemory,
        ...,
    ],
    domain_confidence: dict[
        str,
        float,
    ],
    current_step: int,
    schedule: tuple[
        str,
        ...,
    ],
    feedback_enabled: bool,
    shuffled_index: bool,
    max_attention_bonus: float,
    fork_key: str,
):
    controller = (
        RetrievalAttentionController(
            branch_id=branch_id,
            memory_bank=memory_bank,
            domain_confidence=(
                domain_confidence
            ),
            max_attention_bonus=(
                max_attention_bonus
            ),
            top_k=10,
            diversity_slots=3,
            shuffled_index=(
                shuffled_index
            ),
            fork_key=fork_key,
        )
    )

    state = (
        RetrievalAttentionState.create()
    )

    bank_by_id = {
        memory.memory_id: memory
        for memory
        in memory_bank
    }

    results = []

    for cycle, target_domain in enumerate(
        schedule
    ):
        retrieval = (
            controller.retrieve(
                target_domain=(
                    target_domain
                ),
                current_step=(
                    current_step
                ),
                state=state,
                cycle=cycle,
                feedback_enabled=(
                    feedback_enabled
                ),
            )
        )

        apply_retrieval_to_attention_state(
            result=retrieval,
            memory_bank_by_id=(
                bank_by_id
            ),
            state=state,
            current_step=(
                current_step
            ),
        )

        results.append(
            retrieval
        )

    selected_counter = Counter()

    for result in results:
        selected_counter.update(
            result.selected_memory_ids
        )

    total_selected = sum(
        len(
            result.selected_memory_ids
        )
        for result in results
    )

    target_matches = sum(
        result.target_match_count
        for result in results
    )

    fabricated = sum(
        result.fabricated_memory_count
        for result in results
    )

    return {
        "branch_id": branch_id,
        "results": results,
        "state": state,
        "state_hash": (
            state.state_hash()
        ),
        "target_match_rate": (
            target_matches
            / total_selected
            if total_selected
            else 0.0
        ),
        "mean_target_signal": (
            statistics.mean(
                result.target_signal_mean
                for result
                in results
            )
        ),
        "mean_selected_significance": (
            statistics.mean(
                result.selected_significance_mean
                for result
                in results
            )
        ),
        "mean_selected_age": (
            statistics.mean(
                result.selected_age_mean
                for result
                in results
            )
        ),
        "unique_memory_count": len(
            selected_counter
        ),
        "selection_entropy": (
            _entropy(
                selected_counter
            )
        ),
        "fabricated_memory_count": (
            fabricated
        ),
        "reflection_priority_domain": (
            max(
                SELF_INDEX_DOMAINS,
                key=lambda domain: (
                    state.domain_signal_ewma[
                        domain
                    ],
                    domain,
                ),
            )
        ),
    }


def _paired_retrieval_divergence(
    left,
    right,
) -> float:
    return statistics.mean(
        jaccard_divergence(
            a.selected_memory_ids,
            b.selected_memory_ids,
        )
        for a, b
        in zip(
            left[
                "results"
            ],
            right[
                "results"
            ],
        )
    )


def run_retrieval_attention_experiment(
    *,
    longitudinal_analysis: dict,
    seed_count: int = 128,
    history_steps: int = 650,
    retrieval_cycles: int = 80,
) -> dict:
    domain_confidence = (
        _domain_confidence_from_longitudinal(
            longitudinal_analysis
        )
    )

    rows = []

    for seed in range(
        1,
        seed_count
        + 1,
    ):
        frozen = _frozen_history(
            seed=seed,
            steps=history_steps,
        )

        # Exact fork sentinels.
        world_f0 = copy.deepcopy(
            frozen[
                "world"
            ]
        )
        world_f1 = copy.deepcopy(
            frozen[
                "world"
            ]
        )
        world_f2 = copy.deepcopy(
            frozen[
                "world"
            ]
        )

        agent_f0 = copy.deepcopy(
            frozen[
                "agent"
            ]
        )
        agent_f1 = copy.deepcopy(
            frozen[
                "agent"
            ]
        )
        agent_f2 = copy.deepcopy(
            frozen[
                "agent"
            ]
        )

        exact_prefork = (
            world_f0.state_hash()
            == world_f1.state_hash()
            == world_f2.state_hash()
            == frozen[
                "world_hash"
            ]
            and agent_f0.state_hash()
            == agent_f1.state_hash()
            == agent_f2.state_hash()
            == frozen[
                "agent_hash"
            ]
        )

        schedule = _query_schedule(
            seed=seed,
            cycles=(
                retrieval_cycles
            ),
        )

        fork_key = (
            f"RET-FORK-{seed}"
        )

        f0 = _run_branch(
            branch_id="F0_NO_FEEDBACK",
            memory_bank=frozen[
                "memory_bank"
            ],
            domain_confidence=(
                domain_confidence
            ),
            current_step=frozen[
                "current_step"
            ],
            schedule=schedule,
            feedback_enabled=False,
            shuffled_index=False,
            max_attention_bonus=.18,
            fork_key=fork_key,
        )

        f1 = _run_branch(
            branch_id=(
                "F1_TRUE_SELF_INDEX"
            ),
            memory_bank=frozen[
                "memory_bank"
            ],
            domain_confidence=(
                domain_confidence
            ),
            current_step=frozen[
                "current_step"
            ],
            schedule=schedule,
            feedback_enabled=True,
            shuffled_index=False,
            max_attention_bonus=.18,
            fork_key=fork_key,
        )

        f2 = _run_branch(
            branch_id=(
                "F2_SHUFFLED_SELF_INDEX"
            ),
            memory_bank=frozen[
                "memory_bank"
            ],
            domain_confidence=(
                domain_confidence
            ),
            current_step=frozen[
                "current_step"
            ],
            schedule=schedule,
            feedback_enabled=True,
            shuffled_index=True,
            max_attention_bonus=.18,
            fork_key=fork_key,
        )

        # Zero-strength feedback should be exactly equivalent to F0.
        f_zero = _run_branch(
            branch_id=(
                "FZ_ZERO_STRENGTH"
            ),
            memory_bank=frozen[
                "memory_bank"
            ],
            domain_confidence=(
                domain_confidence
            ),
            current_step=frozen[
                "current_step"
            ],
            schedule=schedule,
            feedback_enabled=True,
            shuffled_index=False,
            max_attention_bonus=0.0,
            fork_key=fork_key,
        )

        zero_exact = all(
            a.selected_memory_ids
            == b.selected_memory_ids
            for a, b
            in zip(
                f0[
                    "results"
                ],
                f_zero[
                    "results"
                ],
            )
        )

        # No retrieval branch is permitted to touch lived world/agent state.
        lived_state_unchanged = (
            world_f0.state_hash()
            == world_f1.state_hash()
            == world_f2.state_hash()
            == frozen[
                "world_hash"
            ]
            and agent_f0.state_hash()
            == agent_f1.state_hash()
            == agent_f2.state_hash()
            == frozen[
                "agent_hash"
            ]
        )

        rows.append({
            "seed": seed,
            "exact_prefork": (
                exact_prefork
            ),
            "lived_state_unchanged": (
                lived_state_unchanged
            ),
            "zero_strength_exact": (
                zero_exact
            ),
            "f0": {
                key: value
                for key, value
                in f0.items()
                if key
                not in {
                    "results",
                    "state",
                }
            },
            "f1": {
                key: value
                for key, value
                in f1.items()
                if key
                not in {
                    "results",
                    "state",
                }
            },
            "f2": {
                key: value
                for key, value
                in f2.items()
                if key
                not in {
                    "results",
                    "state",
                }
            },
            "f0_f1_retrieval_divergence": (
                _paired_retrieval_divergence(
                    f0,
                    f1,
                )
            ),
            "f0_f2_retrieval_divergence": (
                _paired_retrieval_divergence(
                    f0,
                    f2,
                )
            ),
            "f0_f1_cognitive_state_diverged": (
                f0[
                    "state_hash"
                ]
                != f1[
                    "state_hash"
                ]
            ),
            "f1_f2_cognitive_state_diverged": (
                f1[
                    "state_hash"
                ]
                != f2[
                    "state_hash"
                ]
            ),
            "f0_f1_reflection_priority_diverged": (
                f0[
                    "reflection_priority_domain"
                ]
                != f1[
                    "reflection_priority_domain"
                ]
            ),
            "memory_bank_hash": (
                frozen[
                    "memory_bank_hash"
                ]
            ),
        })

    def mean_path(
        branch: str,
        field: str,
    ):
        return statistics.mean(
            row[
                branch
            ][
                field
            ]
            for row
            in rows
        )

    result = {
        "experiment_id": (
            "SL-SELF-MODEL-FEEDBACK-001"
        ),
        "intervention_level": (
            "RETRIEVAL_ATTENTION_ONLY"
        ),
        "seed_count": (
            seed_count
        ),
        "history_steps": (
            history_steps
        ),
        "retrieval_cycles": (
            retrieval_cycles
        ),
        "domain_confidence": (
            domain_confidence
        ),
        "exact_prefork_rate": (
            statistics.mean(
                1.0
                if row[
                    "exact_prefork"
                ]
                else 0.0
                for row
                in rows
            )
        ),
        "lived_state_unchanged_rate": (
            statistics.mean(
                1.0
                if row[
                    "lived_state_unchanged"
                ]
                else 0.0
                for row
                in rows
            )
        ),
        "zero_strength_exact_equivalence_rate": (
            statistics.mean(
                1.0
                if row[
                    "zero_strength_exact"
                ]
                else 0.0
                for row
                in rows
            )
        ),
        "mean_retrieval_divergence_f0_vs_f1": (
            statistics.mean(
                row[
                    "f0_f1_retrieval_divergence"
                ]
                for row
                in rows
            )
        ),
        "mean_retrieval_divergence_f0_vs_f2": (
            statistics.mean(
                row[
                    "f0_f2_retrieval_divergence"
                ]
                for row
                in rows
            )
        ),
        "cognitive_state_divergence_rate_f0_vs_f1": (
            statistics.mean(
                1.0
                if row[
                    "f0_f1_cognitive_state_diverged"
                ]
                else 0.0
                for row
                in rows
            )
        ),
        "cognitive_state_divergence_rate_f1_vs_f2": (
            statistics.mean(
                1.0
                if row[
                    "f1_f2_cognitive_state_diverged"
                ]
                else 0.0
                for row
                in rows
            )
        ),
        "reflection_priority_divergence_rate_f0_vs_f1": (
            statistics.mean(
                1.0
                if row[
                    "f0_f1_reflection_priority_diverged"
                ]
                else 0.0
                for row
                in rows
            )
        ),
        "target_match_rate": {
            "F0": mean_path(
                "f0",
                "target_match_rate",
            ),
            "F1": mean_path(
                "f1",
                "target_match_rate",
            ),
            "F2": mean_path(
                "f2",
                "target_match_rate",
            ),
        },
        "mean_target_signal": {
            "F0": mean_path(
                "f0",
                "mean_target_signal",
            ),
            "F1": mean_path(
                "f1",
                "mean_target_signal",
            ),
            "F2": mean_path(
                "f2",
                "mean_target_signal",
            ),
        },
        "selection_entropy": {
            "F0": mean_path(
                "f0",
                "selection_entropy",
            ),
            "F1": mean_path(
                "f1",
                "selection_entropy",
            ),
            "F2": mean_path(
                "f2",
                "selection_entropy",
            ),
        },
        "unique_memory_count": {
            "F0": mean_path(
                "f0",
                "unique_memory_count",
            ),
            "F1": mean_path(
                "f1",
                "unique_memory_count",
            ),
            "F2": mean_path(
                "f2",
                "unique_memory_count",
            ),
        },
        "fabricated_memory_count_total": (
            sum(
                row[
                    branch
                ][
                    "fabricated_memory_count"
                ]
                for row
                in rows
                for branch
                in (
                    "f0",
                    "f1",
                    "f2",
                )
            )
        ),
        "true_index_target_match_effect": (
            mean_path(
                "f1",
                "target_match_rate",
            )
            - mean_path(
                "f0",
                "target_match_rate",
            )
        ),
        "true_vs_shuffled_specificity_effect": (
            mean_path(
                "f1",
                "target_match_rate",
            )
            - mean_path(
                "f2",
                "target_match_rate",
            )
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "self_model_direct_rewrite_enabled": (
            False
        ),
        "memory_content_mutation_enabled": (
            False
        ),
        "rows": rows,
        "interpretation_warning": (
            "This experiment establishes whether a functional self-index can "
            "causally alter retrieval/attention and downstream cognitive-attention "
            "state while lived world/agent state remains exact. It does not establish "
            "that the intervention is beneficial, conscious, or subjectively experienced."
        ),
    }

    return result
