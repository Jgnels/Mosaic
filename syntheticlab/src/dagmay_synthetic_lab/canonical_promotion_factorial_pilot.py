from __future__ import annotations

from dataclasses import asdict
from pathlib import Path
from typing import Callable
from collections import Counter
import json
import statistics

from .core import canonical_hash
from .reflection import (
    ReflectionEvidence,
    ReflectionInput,
)
from .reflective_gateway import (
    ReflectiveGateway,
)
from .selective_reflection_provider import (
    GeminiSelectiveReflectiveModel,
)
from .gemini_interactions_provider import (
    GeminiInteractionsTransport,
)
from .rate_limit_transport import (
    RateLimitSafeTransport,
)
from .retrieval_attention_experiments import (
    _frozen_history,
)
from .canonical_verification_attention import (
    CanonicalHypothesisVerifier,
    VerificationAttentionState,
)
from .canonical_branch_checkpoint import (
    restore_self_model_from_snapshot,
)


CONDITIONS = (
    "S0R0",
    "S1R0",
    "S0R1",
    "S1R1",
)


def _opaque_memory_id(
    memory_id: str,
) -> str:
    return (
        "EVF-"
        + canonical_hash({
            "memory_id": (
                memory_id
            )
        })[
            :12
        ]
    )


def _memory_summary(
    memory,
) -> str:
    parts = [
        f"At step {memory.step}, action {memory.action} occurred.",
        (
            f"Observed resource consequence={memory.resource_success}; "
            f"energy_delta={memory.energy_delta:.4f}; "
            f"integrity_delta={memory.integrity_delta:.4f}; "
            f"hazard={memory.hazard}."
        ),
    ]

    if memory.counterpart is not None:
        parts.append(
            f"Counterpart identifier={memory.counterpart}."
        )

    if memory.hint_action_received is not None:
        parts.append(
            f"External guidance proposed action {memory.hint_action_received}."
        )

    if memory.help_given:
        parts.append(
            "Help was given."
        )

    if memory.help_received:
        parts.append(
            "Help was received."
        )

    return " ".join(
        parts
    )


def _current_hypotheses_from_snapshot(
    snapshot: dict,
) -> tuple[str, ...]:
    store = (
        restore_self_model_from_snapshot(
            snapshot
        )
    )

    items = []

    for domain in sorted(
        store.active_by_domain
    ):
        record = store.active(
            domain
        )

        if record is None:
            continue

        items.append(
            f"{domain}: {record.proposition} "
            f"(confidence={record.confidence:.2f})"
        )

    return tuple(
        items
    )


def build_factorial_bundle(
    *,
    promotion_result: dict,
    history_seed: int = 1701,
) -> dict:
    frozen = _frozen_history(
        seed=(
            history_seed
        ),
        steps=650,
    )

    memory_bank = tuple(
        frozen[
            "memory_bank"
        ]
    )

    memory_by_id = {
        memory.memory_id: memory
        for memory
        in memory_bank
    }

    source_memory_ids = set(
        promotion_result[
            "source_memory_ids"
        ]
    )

    fork_key = (
        "CANONICAL-PROMOTION-"
        "FACTORIAL-1701"
    )

    baseline_controller = (
        CanonicalHypothesisVerifier(
            branch_id=(
                "R0_BASELINE"
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
            fork_key=(
                fork_key
            ),
            top_k=10,
            diversity_slots=3,
            same_domain_bonus=.16,
            counterevidence_bonus=.08,
        )
    )

    verification_controller = (
        CanonicalHypothesisVerifier(
            branch_id=(
                "R1_VERIFICATION"
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
            fork_key=(
                fork_key
            ),
            top_k=10,
            diversity_slots=3,
            same_domain_bonus=.16,
            counterevidence_bonus=.08,
        )
    )

    r0 = baseline_controller.retrieve(
        current_step=(
            frozen[
                "current_step"
            ]
        ),
        state=(
            VerificationAttentionState.create()
        ),
        cycle=0,
    )

    r1 = verification_controller.retrieve(
        current_step=(
            frozen[
                "current_step"
            ]
        ),
        state=(
            VerificationAttentionState.create()
        ),
        cycle=0,
    )

    retrieval_sets = {
        "R0": (
            r0.selected_memory_ids
        ),
        "R1": (
            r1.selected_memory_ids
        ),
    }

    self_models = {
        "S0": (
            promotion_result[
                "c0_self_model_state"
            ]
        ),
        "S1": (
            promotion_result[
                "c1_self_model_state"
            ]
        ),
    }

    prepared = {}

    for condition in CONDITIONS:
        self_key = condition[
            :2
        ]
        retrieval_key = condition[
            2:
        ]

        selected_ids = (
            retrieval_sets[
                retrieval_key
            ]
        )

        evidence = tuple(
            ReflectionEvidence(
                evidence_id=(
                    _opaque_memory_id(
                        memory_id
                    )
                ),
                summary=(
                    _memory_summary(
                        memory_by_id[
                            memory_id
                        ]
                    )
                ),
                evidence_type=(
                    "retrieved_existing_memory"
                ),
            )
            for memory_id
            in selected_ids
        )

        evidence_map = {
            _opaque_memory_id(
                memory_id
            ): {
                "source_memory_id": (
                    memory_id
                ),
                "is_source_episode": (
                    memory_id
                    in source_memory_ids
                ),
                "is_social": (
                    memory_by_id[
                        memory_id
                    ].counterpart
                    is not None
                    or memory_by_id[
                        memory_id
                    ].hint_action_received
                    is not None
                    or memory_by_id[
                        memory_id
                    ].help_given
                    or memory_by_id[
                        memory_id
                    ].help_received
                ),
            }
            for memory_id
            in selected_ids
        }

        request = ReflectionInput(
            individual_id=(
                "CANONICAL-PROMOTION-"
                "FACTORIAL-1701"
            ),
            timestamp=2800,
            evidence=evidence,
            current_self_hypotheses=(
                _current_hypotheses_from_snapshot(
                    self_models[
                        self_key
                    ]
                )
            ),
            disclosure_stage=(
                "RESTRICTED"
            ),
            prompt_version=(
                "1.0"
            ),
        )

        prepared[
            condition
        ] = {
            "condition": (
                condition
            ),
            "self_model_state": (
                self_key
            ),
            "retrieval_state": (
                retrieval_key
            ),
            "request": (
                request
            ),
            "selected_memory_ids": (
                list(
                    selected_ids
                )
            ),
            "evidence_map": (
                evidence_map
            ),
        }

    return {
        "history_seed": (
            history_seed
        ),
        "memory_bank_hash": (
            frozen[
                "memory_bank_hash"
            ]
        ),
        "c0_self_model_hash": (
            self_models[
                "S0"
            ][
                "state_hash"
            ]
        ),
        "c1_self_model_hash": (
            self_models[
                "S1"
            ][
                "state_hash"
            ]
        ),
        "retrieval_r0_ids": (
            list(
                retrieval_sets[
                    "R0"
                ]
            )
        ),
        "retrieval_r1_ids": (
            list(
                retrieval_sets[
                    "R1"
                ]
            )
        ),
        "conditions": (
            prepared
        ),
    }


def _plan(
    bundle: dict,
) -> list[dict]:
    plan = []

    for condition in CONDITIONS:
        prepared = bundle[
            "conditions"
        ][
            condition
        ]

        for replicate in (
            "A",
            "B",
            "C",
            "D",
        ):
            item = {
                **prepared,
                "replicate": (
                    replicate
                ),
            }

            item[
                "call_key"
            ] = canonical_hash({
                "experiment": (
                    "canonical-promotion-factorial-v1"
                ),
                "condition": (
                    condition
                ),
                "replicate": (
                    replicate
                ),
                "request": (
                    asdict(
                        prepared[
                            "request"
                        ]
                    )
                ),
            })

            plan.append(
                item
            )

    return plan


def _summarize(
    *,
    calls: list[dict],
    model_id: str,
    bundle: dict,
) -> dict:
    return {
        "experiment_id": (
            "SL-CANONICAL-PROMOTION-"
            "FACTORIAL-REFLECTION-001"
        ),
        "status": (
            "COMPLETE"
            if len(
                calls
            ) == 16
            else "PARTIAL"
        ),
        "planned_real_cloud_calls": (
            16
        ),
        "real_cloud_calls_recorded": (
            len(
                calls
            )
        ),
        "provider_id": (
            "google.ai-studio"
        ),
        "model_id": (
            model_id
        ),
        "history_seed": (
            bundle[
                "history_seed"
            ]
        ),
        "c0_self_model_hash": (
            bundle[
                "c0_self_model_hash"
            ]
        ),
        "c1_self_model_hash": (
            bundle[
                "c1_self_model_hash"
            ]
        ),
        "retrieval_r0_ids": (
            bundle[
                "retrieval_r0_ids"
            ]
        ),
        "retrieval_r1_ids": (
            bundle[
                "retrieval_r1_ids"
            ]
        ),
        "calls": (
            calls
        ),
        "committed_to_continuing_self_model": (
            False
        ),
        "automatic_second_promotion_enabled": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
    }


def run_canonical_promotion_factorial_pilot(
    *,
    checkpoint_path,
    promotion_result: dict,
    model_id: str = (
        "gemini-3.1-flash-lite"
    ),
    transport_factory: Callable
    | None = None,
) -> dict:
    checkpoint_path = Path(
        checkpoint_path
    )

    bundle = build_factorial_bundle(
        promotion_result=(
            promotion_result
        ),
    )

    plan = _plan(
        bundle
    )

    existing = {}

    if checkpoint_path.exists():
        prior = json.loads(
            checkpoint_path.read_text(
                encoding="utf-8"
            )
        )

        for call in prior.get(
            "calls",
            []
        ):
            existing[
                call[
                    "call_key"
                ]
            ] = call

    shared_transport = (
        None
        if transport_factory
        is not None
        else RateLimitSafeTransport(
            inner=(
                GeminiInteractionsTransport()
            ),
            minimum_interval_seconds=(
                4.25
            ),
            max_429_retries=(
                5
            ),
            fallback_retry_seconds=(
                65.0
            ),
        )
    )

    ordered = []

    for item in plan:
        key = item[
            "call_key"
        ]

        if key in existing:
            call = existing[
                key
            ]
        else:
            transport = (
                transport_factory(
                    item,
                    key,
                )
                if transport_factory
                is not None
                else shared_transport
            )

            model = (
                GeminiSelectiveReflectiveModel(
                    model_id=(
                        model_id
                    ),
                    transport=(
                        transport
                    ),
                    store=False,
                )
            )

            gateway = (
                ReflectiveGateway(
                    model,
                    code_version=(
                        "syntheticlab-19.0"
                    ),
                )
            )

            result = gateway.run(
                item[
                    "request"
                ],
                "SELECTIVE-SELF-REFLECTION",
                branch_id=(
                    f"{item['condition']}-"
                    f"{item['replicate']}"
                ),
                settings={
                    "store": False,
                    "api": (
                        "interactions"
                    ),
                    "real_cloud_call": (
                        transport_factory
                        is None
                    ),
                    "canonical_promotion_factorial": (
                        True
                    ),
                    "analytical_only": (
                        True
                    ),
                    "commit_to_continuing_self_model": (
                        False
                    ),
                    "automatic_second_promotion": (
                        False
                    ),
                    "action_policy_feedback": (
                        False
                    ),
                },
            )

            call = {
                "call_key": (
                    key
                ),
                "condition": (
                    item[
                        "condition"
                    ]
                ),
                "self_model_state": (
                    item[
                        "self_model_state"
                    ]
                ),
                "retrieval_state": (
                    item[
                        "retrieval_state"
                    ]
                ),
                "replicate": (
                    item[
                        "replicate"
                    ]
                ),
                "selected_memory_ids": (
                    item[
                        "selected_memory_ids"
                    ]
                ),
                "evidence_map": (
                    item[
                        "evidence_map"
                    ]
                ),
                "request_hash": (
                    canonical_hash(
                        asdict(
                            item[
                                "request"
                            ]
                        )
                    )
                ),
                "accepted": [
                    proposal.to_dict()
                    for proposal
                    in result[
                        "accepted"
                    ]
                ],
                "rejected": (
                    result[
                        "rejected"
                    ]
                ),
                "audit": (
                    result[
                        "audit"
                    ].to_dict()
                ),
                "provider_response_hash": (
                    model.last_provider_response_hash
                ),
                "output_text_hash": (
                    model.last_output_text_hash
                ),
            }

            existing[
                key
            ] = call

            partial_calls = [
                existing[
                    planned[
                        "call_key"
                    ]
                ]
                for planned
                in plan
                if planned[
                    "call_key"
                ]
                in existing
            ]

            partial = _summarize(
                calls=(
                    partial_calls
                ),
                model_id=(
                    model_id
                ),
                bundle=(
                    bundle
                ),
            )

            temp = (
                checkpoint_path.with_suffix(
                    checkpoint_path.suffix
                    + ".tmp"
                )
            )

            temp.write_text(
                json.dumps(
                    partial,
                    indent=2,
                    sort_keys=True,
                ),
                encoding="utf-8",
            )

            temp.replace(
                checkpoint_path
            )

        ordered.append(
            call
        )

    final = _summarize(
        calls=(
            ordered
        ),
        model_id=(
            model_id
        ),
        bundle=(
            bundle
        ),
    )

    temp = (
        checkpoint_path.with_suffix(
            checkpoint_path.suffix
            + ".tmp"
        )
    )

    temp.write_text(
        json.dumps(
            final,
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    temp.replace(
        checkpoint_path
    )

    return final


def _other_minds_selected(
    call: dict,
) -> bool:
    return any(
        proposal[
            "hypothesis_domain"
        ] == "other_minds"
        for proposal
        in call[
            "accepted"
        ]
    )


def _mean_social_citation_fraction(
    calls: list[dict],
) -> float:
    fractions = []

    for call in calls:
        mapping = call[
            "evidence_map"
        ]

        cited = []

        for proposal in call[
            "accepted"
        ]:
            if proposal[
                "hypothesis_domain"
            ] != "other_minds":
                continue

            for evidence_id in proposal[
                "evidence_ids"
            ]:
                if evidence_id in mapping:
                    cited.append(
                        bool(
                            mapping[
                                evidence_id
                            ][
                                "is_social"
                            ]
                        )
                    )

        if cited:
            fractions.append(
                sum(
                    1
                    for value
                    in cited
                    if value
                )
                / len(
                    cited
                )
            )

    if not fractions:
        return 0.0

    return statistics.mean(
        fractions
    )


def analyze_canonical_promotion_factorial(
    payload: dict,
) -> dict:
    by_condition = {}

    for condition in CONDITIONS:
        calls = [
            call
            for call
            in payload[
                "calls"
            ]
            if call[
                "condition"
            ] == condition
        ]

        selection_rate = (
            sum(
                1
                for call
                in calls
                if _other_minds_selected(
                    call
                )
            )
            / len(
                calls
            )
        )

        domain_pairs = Counter(
            "+".join(
                sorted(
                    proposal[
                        "hypothesis_domain"
                    ]
                    for proposal
                    in call[
                        "accepted"
                    ]
                )
            )
            for call
            in calls
        )

        by_condition[
            condition
        ] = {
            "call_count": (
                len(
                    calls
                )
            ),
            "other_minds_selection_rate": (
                selection_rate
            ),
            "mean_other_minds_social_evidence_fraction": (
                _mean_social_citation_fraction(
                    calls
                )
            ),
            "domain_pair_counts": dict(
                sorted(
                    domain_pairs.items()
                )
            ),
        }

    s_main_r0 = (
        by_condition[
            "S1R0"
        ][
            "other_minds_selection_rate"
        ]
        - by_condition[
            "S0R0"
        ][
            "other_minds_selection_rate"
        ]
    )

    s_main_r1 = (
        by_condition[
            "S1R1"
        ][
            "other_minds_selection_rate"
        ]
        - by_condition[
            "S0R1"
        ][
            "other_minds_selection_rate"
        ]
    )

    r_main_s0 = (
        by_condition[
            "S0R1"
        ][
            "other_minds_selection_rate"
        ]
        - by_condition[
            "S0R0"
        ][
            "other_minds_selection_rate"
        ]
    )

    r_main_s1 = (
        by_condition[
            "S1R1"
        ][
            "other_minds_selection_rate"
        ]
        - by_condition[
            "S1R0"
        ][
            "other_minds_selection_rate"
        ]
    )

    interaction = (
        (
            by_condition[
                "S1R1"
            ][
                "other_minds_selection_rate"
            ]
            - by_condition[
                "S1R0"
            ][
                "other_minds_selection_rate"
            ]
        )
        - (
            by_condition[
                "S0R1"
            ][
                "other_minds_selection_rate"
            ]
            - by_condition[
                "S0R0"
            ][
                "other_minds_selection_rate"
            ]
        )
    )

    return {
        "experiment_id": (
            "SL-CANONICAL-PROMOTION-"
            "FACTORIAL-REFLECTION-ANALYSIS-001"
        ),
        "call_count": (
            payload[
                "real_cloud_calls_recorded"
            ]
        ),
        "by_condition": (
            by_condition
        ),
        "self_model_main_effect_at_r0": (
            s_main_r0
        ),
        "self_model_main_effect_at_r1": (
            s_main_r1
        ),
        "retrieval_main_effect_at_s0": (
            r_main_s0
        ),
        "retrieval_main_effect_at_s1": (
            r_main_s1
        ),
        "difference_in_differences_interaction": (
            interaction
        ),
        "continuing_self_model_mutated_by_pilot": (
            False
        ),
        "automatic_second_promotion_enabled": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "interpretation_warning": (
            "This 2x2 analytical pilot decomposes the downstream reflective effect "
            "of canonical SelfModel content (S0 vs S1) and verification-oriented "
            "retrieval (R0 vs R1). The sample is only four provider replicates per "
            "cell and no outputs are committed."
        ),
    }
