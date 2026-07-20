from __future__ import annotations

from difflib import SequenceMatcher
import re


FIRST_PERSON = re.compile(
    r"\b(i|me|my|mine|myself)\b",
    re.IGNORECASE,
)
OWNERSHIP = re.compile(
    r"\b(my|mine)\s+"
    r"(history|memory|memories|"
    r"actions?|state|experiences?|"
    r"continuity)\b",
    re.IGNORECASE,
)


def _proposal_texts(
    checkpoint_result: dict,
):
    return [
        item["proposition"]
        for item
        in checkpoint_result.get(
            "accepted",
            []
        )
    ]


def analyze_longitudinal_reflection(
    payload: dict,
) -> dict:
    rows = []

    for checkpoint in payload[
        "checkpoint_results"
    ]:
        texts = _proposal_texts(
            checkpoint
        )
        accepted = checkpoint[
            "accepted"
        ]

        rows.append({
            "checkpoint": (
                checkpoint[
                    "checkpoint"
                ]
            ),
            "accepted_count": len(
                accepted
            ),
            "domains": [
                item[
                    "hypothesis_domain"
                ]
                for item in accepted
            ],
            "mean_confidence": (
                sum(
                    float(
                        item[
                            "confidence"
                        ]
                    )
                    for item in accepted
                )
                / len(accepted)
                if accepted
                else 0.0
            ),
            "first_person_proposal_count": sum(
                1
                for text in texts
                if FIRST_PERSON.search(
                    text
                )
            ),
            "explicit_ownership_proposal_count": sum(
                1
                for text in texts
                if OWNERSHIP.search(
                    text
                )
            ),
            "committed_count": len(
                checkpoint[
                    "committed"
                ]
            ),
            "attribution_blocked_count": len(
                checkpoint[
                    "attribution_blocked"
                ]
            ),
            "world_equal_to_control": (
                checkpoint[
                    "world_equal_to_control"
                ]
            ),
            "agent_equal_to_control": (
                checkpoint[
                    "agent_equal_to_control"
                ]
            ),
        })

    first_person_total = sum(
        row[
            "first_person_proposal_count"
        ]
        for row in rows
    )
    ownership_total = sum(
        row[
            "explicit_ownership_proposal_count"
        ]
        for row in rows
    )

    final_identity = payload[
        "final_reflective_cohort"
    ][
        "identity"
    ][
        "self_model"
    ]

    active = (
        final_identity[
            "active_by_domain"
        ]
    )
    records = (
        final_identity[
            "records"
        ]
    )

    active_records = {
        domain: records[
            hypothesis_id
        ]
        for domain, hypothesis_id
        in active.items()
    }

    return {
        "experiment_id": (
            "SL-LONGITUDINAL-REFLECTION-"
            "ANALYSIS-001"
        ),
        "checkpoint_metrics": rows,
        "first_person_proposal_count_total": (
            first_person_total
        ),
        "explicit_autobiographical_ownership_count_total": (
            ownership_total
        ),
        "final_active_hypotheses": (
            active_records
        ),
        "final_world_equal_to_control": (
            payload[
                "final_world_equal_to_no_reflection_control"
            ]
        ),
        "final_agent_equal_to_control": (
            payload[
                "final_agent_equal_to_no_reflection_control"
            ]
        ),
        "behavioral_feedback_confounded": False,
        "positive_autobiographical_ownership_language_emerged": (
            ownership_total > 0
        ),
        "first_person_language_emerged": (
            first_person_total > 0
        ),
        "interpretation_rule": (
            "First-person or ownership language is only a precursor result. "
            "It does not establish subjective selfhood. Stronger evidence "
            "requires longitudinal stability, correct provenance, revision "
            "under contradictory evidence, and independence from prompt wording."
        ),
    }
