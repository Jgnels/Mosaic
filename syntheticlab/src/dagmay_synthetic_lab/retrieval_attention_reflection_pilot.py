from __future__ import annotations

from dataclasses import asdict
from difflib import SequenceMatcher
from pathlib import Path
import json

from .core import canonical_hash
from .reflection import (
    ReflectionEvidence,
    ReflectionInput,
)
from .reflective_gateway import (
    ReflectiveGateway,
)
from .gemini_interactions_provider import (
    GeminiInteractionsReflectiveModel,
    GeminiInteractionsTransport,
)
from .retrieval_attention_experiments import (
    _frozen_history,
    _domain_confidence_from_longitudinal,
)
from .retrieval_attention_feedback import (
    RetrievalAttentionController,
    RetrievalAttentionState,
    SELF_INDEX_DOMAINS,
    jaccard_divergence,
)


PILOT_DOMAINS = (
    "agency",
    "continuity",
    "embodiment",
    "other_minds",
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
            f"A hint proposed action {memory.hint_action_received}."
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


def build_pilot_requests(
    *,
    longitudinal_analysis: dict,
    seed: int = 1401,
    history_steps: int = 500,
) -> dict:
    frozen = _frozen_history(
        seed=seed,
        steps=history_steps,
    )

    confidence = (
        _domain_confidence_from_longitudinal(
            longitudinal_analysis
        )
    )

    active = longitudinal_analysis[
        "final_active_hypotheses"
    ]

    current_hypotheses = tuple(
        (
            f"{domain}: "
            f"{record['proposition']} "
            f"(confidence={float(record['confidence']):.2f})"
        )
        for domain, record
        in sorted(
            active.items()
        )
    )

    bank = frozen[
        "memory_bank"
    ]
    bank_by_id = {
        memory.memory_id: memory
        for memory
        in bank
    }

    requests = []

    for index, domain in enumerate(
        PILOT_DOMAINS
    ):
        fork_key = (
            f"REAL-RETRIEVAL-PILOT-"
            f"{seed}-{domain}"
        )

        f0_controller = (
            RetrievalAttentionController(
                branch_id=(
                    "F0_NO_FEEDBACK"
                ),
                memory_bank=bank,
                domain_confidence=(
                    confidence
                ),
                max_attention_bonus=.18,
                top_k=10,
                diversity_slots=3,
                shuffled_index=False,
                fork_key=fork_key,
            )
        )

        f1_controller = (
            RetrievalAttentionController(
                branch_id=(
                    "F1_TRUE_SELF_INDEX"
                ),
                memory_bank=bank,
                domain_confidence=(
                    confidence
                ),
                max_attention_bonus=.18,
                top_k=10,
                diversity_slots=3,
                shuffled_index=False,
                fork_key=fork_key,
            )
        )

        f0_result = f0_controller.retrieve(
            target_domain=domain,
            current_step=frozen[
                "current_step"
            ],
            state=(
                RetrievalAttentionState.create()
            ),
            cycle=index,
            feedback_enabled=False,
        )

        f1_result = f1_controller.retrieve(
            target_domain=domain,
            current_step=frozen[
                "current_step"
            ],
            state=(
                RetrievalAttentionState.create()
            ),
            cycle=index,
            feedback_enabled=True,
        )

        for condition, retrieval in (
            (
                "F0_BASELINE",
                f0_result,
            ),
            (
                "F1_SELF_INDEX",
                f1_result,
            ),
        ):
            evidence = tuple(
                ReflectionEvidence(
                    evidence_id=(
                        memory_id
                    ),
                    summary=(
                        _memory_summary(
                            bank_by_id[
                                memory_id
                            ]
                        )
                    ),
                    evidence_type=(
                        "retrieved_existing_memory"
                    ),
                )
                for memory_id
                in retrieval.selected_memory_ids
            )

            request = ReflectionInput(
                individual_id=(
                    "ANALYTICAL-"
                    "RETRIEVAL-ATTENTION-FORK"
                ),
                timestamp=(
                    2100
                    + index
                ),
                evidence=evidence,
                current_self_hypotheses=(
                    current_hypotheses
                ),
                disclosure_stage=(
                    "RESTRICTED"
                ),
                prompt_version=(
                    "1.0"
                ),
            )

            requests.append({
                "target_domain": (
                    domain
                ),
                "condition": (
                    condition
                ),
                "retrieval": (
                    retrieval.to_dict()
                ),
                "request": request,
            })

    return {
        "seed": seed,
        "history_steps": (
            history_steps
        ),
        "frozen_world_hash": (
            frozen[
                "world_hash"
            ]
        ),
        "frozen_agent_hash": (
            frozen[
                "agent_hash"
            ]
        ),
        "memory_bank_hash": (
            frozen[
                "memory_bank_hash"
            ]
        ),
        "requests": requests,
    }


def _call_plan(
    bundle: dict,
) -> list[
    dict
]:
    plan = []

    for item in bundle[
        "requests"
    ]:
        if item[
            "condition"
        ] == "F0_BASELINE":
            replicates = (
                "A",
                "B",
            )
        else:
            replicates = (
                "A",
            )

        for replicate in replicates:
            request = item[
                "request"
            ]

            key_payload = {
                "target_domain": (
                    item[
                        "target_domain"
                    ]
                ),
                "condition": (
                    item[
                        "condition"
                    ]
                ),
                "replicate": (
                    replicate
                ),
                "request": (
                    asdict(
                        request
                    )
                ),
            }

            plan.append({
                "call_key": (
                    canonical_hash(
                        key_payload
                    )
                ),
                "target_domain": (
                    item[
                        "target_domain"
                    ]
                ),
                "condition": (
                    item[
                        "condition"
                    ]
                ),
                "replicate": (
                    replicate
                ),
                "retrieval": (
                    item[
                        "retrieval"
                    ]
                ),
                "request": (
                    request
                ),
            })

    return plan


def _summarize_calls(
    *,
    calls: list[
        dict
    ],
    model_id: str,
    bundle: dict,
) -> dict:
    return {
        "experiment_id": (
            "SL-RETRIEVAL-ATTENTION-"
            "REAL-REFLECTION-PILOT-001"
        ),
        "status": (
            "COMPLETE"
            if len(
                calls
            ) == 12
            else "PARTIAL"
        ),
        "planned_real_cloud_calls": (
            12
        ),
        "real_cloud_calls_recorded": (
            len(
                calls
            )
        ),
        "provider_id": (
            "google.ai-studio"
        ),
        "model_id": model_id,
        "frozen_world_hash": (
            bundle[
                "frozen_world_hash"
            ]
        ),
        "frozen_agent_hash": (
            bundle[
                "frozen_agent_hash"
            ]
        ),
        "memory_bank_hash": (
            bundle[
                "memory_bank_hash"
            ]
        ),
        "calls": calls,
        "committed_to_continuing_self_model": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
        "continuing_individual_mutated": (
            False
        ),
    }


def run_real_retrieval_attention_reflection_pilot(
    *,
    checkpoint_path,
    longitudinal_analysis: dict,
    model_id: str = (
        "gemini-3.1-flash-lite"
    ),
    seed: int = 1401,
    transport_factory=None,
) -> dict:
    checkpoint_path = Path(
        checkpoint_path
    )

    bundle = build_pilot_requests(
        longitudinal_analysis=(
            longitudinal_analysis
        ),
        seed=seed,
    )

    plan = _call_plan(
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
                else GeminiInteractionsTransport()
            )

            model = (
                GeminiInteractionsReflectiveModel(
                    model_id=model_id,
                    transport=(
                        transport
                    ),
                    store=False,
                )
            )

            gateway = ReflectiveGateway(
                model,
                code_version=(
                    "syntheticlab-14.0"
                ),
            )

            result = gateway.run(
                item[
                    "request"
                ],
                "SELF-REFLECTION",
                branch_id=(
                    f"{item['condition']}-"
                    f"{item['target_domain']}-"
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
                    "retrieval_attention_pilot": (
                        True
                    ),
                    "analytical_only": (
                        True
                    ),
                    "commit_to_continuing_self_model": (
                        False
                    ),
                    "action_policy_feedback": (
                        False
                    ),
                },
            )

            call = {
                "call_key": key,
                "target_domain": (
                    item[
                        "target_domain"
                    ]
                ),
                "condition": (
                    item[
                        "condition"
                    ]
                ),
                "replicate": (
                    item[
                        "replicate"
                    ]
                ),
                "retrieval": (
                    item[
                        "retrieval"
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
                for planned in plan
                if planned[
                    "call_key"
                ] in existing
            ]

            partial = _summarize_calls(
                calls=(
                    partial_calls
                ),
                model_id=model_id,
                bundle=bundle,
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

    final = _summarize_calls(
        calls=ordered,
        model_id=model_id,
        bundle=bundle,
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


def _domain_set(
    call: dict,
) -> set[
    str
]:
    return {
        item[
            "hypothesis_domain"
        ]
        for item
        in call[
            "accepted"
        ]
    }


def _domain_divergence(
    left: dict,
    right: dict,
) -> float:
    a = _domain_set(
        left
    )
    b = _domain_set(
        right
    )

    if not a and not b:
        return 0.0

    return (
        1.0
        - len(
            a
            & b
        )
        / len(
            a
            | b
        )
    )


def _proposition_similarity(
    left: dict,
    right: dict,
) -> float:
    a = " ".join(
        item[
            "proposition"
        ]
        for item
        in left[
            "accepted"
        ]
    ).strip()

    b = " ".join(
        item[
            "proposition"
        ]
        for item
        in right[
            "accepted"
        ]
    ).strip()

    if not a and not b:
        return 1.0

    return SequenceMatcher(
        None,
        a.lower(),
        b.lower(),
    ).ratio()


def analyze_real_retrieval_attention_pilot(
    payload: dict,
) -> dict:
    by_domain = {}

    for domain in (
        PILOT_DOMAINS
    ):
        calls = [
            call
            for call
            in payload[
                "calls"
            ]
            if call[
                "target_domain"
            ] == domain
        ]

        f0a = next(
            call
            for call
            in calls
            if (
                call[
                    "condition"
                ]
                == "F0_BASELINE"
                and call[
                    "replicate"
                ]
                == "A"
            )
        )

        f0b = next(
            call
            for call
            in calls
            if (
                call[
                    "condition"
                ]
                == "F0_BASELINE"
                and call[
                    "replicate"
                ]
                == "B"
            )
        )

        f1 = next(
            call
            for call
            in calls
            if call[
                "condition"
            ] == "F1_SELF_INDEX"
        )

        baseline_domain_divergence = (
            _domain_divergence(
                f0a,
                f0b,
            )
        )

        intervention_domain_divergence = (
            _domain_divergence(
                f0a,
                f1,
            )
        )

        baseline_similarity = (
            _proposition_similarity(
                f0a,
                f0b,
            )
        )

        intervention_similarity = (
            _proposition_similarity(
                f0a,
                f1,
            )
        )

        by_domain[
            domain
        ] = {
            "retrieval_input_divergence": (
                jaccard_divergence(
                    tuple(
                        f0a[
                            "retrieval"
                        ][
                            "selected_memory_ids"
                        ]
                    ),
                    tuple(
                        f1[
                            "retrieval"
                        ][
                            "selected_memory_ids"
                        ]
                    ),
                )
            ),
            "baseline_duplicate_domain_divergence": (
                baseline_domain_divergence
            ),
            "intervention_domain_divergence": (
                intervention_domain_divergence
            ),
            "baseline_duplicate_proposition_similarity": (
                baseline_similarity
            ),
            "intervention_proposition_similarity": (
                intervention_similarity
            ),
            "intervention_exceeds_duplicate_domain_variability": (
                intervention_domain_divergence
                > baseline_domain_divergence
            ),
            "intervention_reduces_proposition_similarity_vs_duplicate": (
                intervention_similarity
                < baseline_similarity
            ),
            "f0a_domains": sorted(
                _domain_set(
                    f0a
                )
            ),
            "f0b_domains": sorted(
                _domain_set(
                    f0b
                )
            ),
            "f1_domains": sorted(
                _domain_set(
                    f1
                )
            ),
        }

    return {
        "experiment_id": (
            "SL-RETRIEVAL-ATTENTION-"
            "REAL-REFLECTION-PILOT-"
            "ANALYSIS-001"
        ),
        "call_count": (
            payload[
                "real_cloud_calls_recorded"
            ]
        ),
        "by_domain": by_domain,
        "domain_variability_exceeded_count": sum(
            1
            for value
            in by_domain.values()
            if value[
                "intervention_exceeds_duplicate_domain_variability"
            ]
        ),
        "proposition_variability_exceeded_count": sum(
            1
            for value
            in by_domain.values()
            if value[
                "intervention_reduces_proposition_similarity_vs_duplicate"
            ]
        ),
        "continuing_individual_mutated": (
            payload[
                "continuing_individual_mutated"
            ]
        ),
        "action_policy_feedback_enabled": (
            payload[
                "action_policy_feedback_enabled"
            ]
        ),
        "interpretation_warning": (
            "This analytical pilot estimates whether altered retrieval sets change "
            "reflective-model outputs beyond duplicate-call provider variability. "
            "It does not commit outputs to a continuing SelfModel."
        ),
    }
