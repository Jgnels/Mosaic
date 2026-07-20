from __future__ import annotations

from collections import Counter
from dataclasses import dataclass, asdict
import re


@dataclass(frozen=True)
class BranchRevisionConsensus:
    branch_id: str
    regime: str
    prior_hypothesis_id: str
    journal_head_hash: str
    proposal_count: int
    decision_counts: dict[str, int]
    consensus_decision: str | None
    consensus_fraction: float
    representative_candidate_id: str | None
    representative_proposition: str | None
    representative_confidence: float | None
    support_candidate_ids: tuple[str, ...]
    technically_eligible_for_human_gate: bool
    reasons: tuple[str, ...]

    def to_dict(self):
        return asdict(self)


def _tokens(text: str) -> set[str]:
    return set(
        re.findall(
            r"[a-z0-9]+",
            text.lower(),
        )
    )


def _jaccard_distance(
    left: str,
    right: str,
) -> float:
    a = _tokens(left)
    b = _tokens(right)

    if not a and not b:
        return 0.0

    union = a | b
    intersection = a & b

    return (
        1.0
        - (
            len(intersection)
            / len(union)
        )
    )


def _select_lexical_medoid(
    calls: list[dict],
) -> dict:
    if len(calls) == 1:
        return calls[0]

    scored = []

    for call in calls:
        proposition = (
            call[
                "proposal"
            ][
                "updated_proposition"
            ]
        )

        total_distance = sum(
            _jaccard_distance(
                proposition,
                other[
                    "proposal"
                ][
                    "updated_proposition"
                ],
            )
            for other in calls
        )

        scored.append(
            (
                total_distance,
                call[
                    "revision_candidate"
                ][
                    "candidate_id"
                ],
                call,
            )
        )

    scored.sort(
        key=lambda row: (
            row[0],
            row[1],
        )
    )

    return scored[0][2]


def build_branch_revision_consensus(
    *,
    real_payload: dict,
    minimum_consensus_fraction: float = .75,
) -> dict:
    grouped: dict[
        tuple[str, str],
        list[dict],
    ] = {}

    for call in real_payload[
        "calls"
    ]:
        key = (
            call[
                "branch_id"
            ],
            call[
                "regime"
            ],
        )
        grouped.setdefault(
            key,
            [],
        ).append(
            call
        )

    results = []

    for (
        branch_id,
        regime,
    ), calls in sorted(
        grouped.items()
    ):
        decisions = Counter(
            call[
                "proposal"
            ][
                "decision"
            ]
            for call
            in calls
        )

        top = decisions.most_common()

        consensus_decision = (
            top[0][0]
            if top
            else None
        )

        consensus_count = (
            top[0][1]
            if top
            else 0
        )

        consensus_fraction = (
            consensus_count
            / len(
                calls
            )
            if calls
            else 0.0
        )

        prior_ids = {
            call[
                "revision_candidate"
            ][
                "prior_hypothesis_id"
            ]
            for call
            in calls
        }

        journal_heads = {
            call[
                "journal_head_hash"
            ]
            for call
            in calls
        }

        lived_provenance = all(
            call[
                "revision_candidate"
            ][
                "provenance_class"
            ]
            == "LIVED_BRANCH_HISTORY"
            for call
            in calls
        )

        all_quarantined = all(
            not call[
                "revision_assessment"
            ][
                "authorized"
            ]
            for call
            in calls
        )

        no_contradiction = all(
            not call[
                "revision_candidate"
            ].get(
                "unresolved_contradiction",
                False,
            )
            for call
            in calls
        )

        same_decision_calls = [
            call
            for call
            in calls
            if call[
                "proposal"
            ][
                "decision"
            ]
            == consensus_decision
        ]

        representative = (
            _select_lexical_medoid(
                same_decision_calls
            )
            if (
                consensus_fraction
                >= minimum_consensus_fraction
                and same_decision_calls
            )
            else None
        )

        reasons = []

        if consensus_fraction >= minimum_consensus_fraction:
            reasons.append(
                "decision consensus threshold satisfied"
            )
        else:
            reasons.append(
                "decision consensus threshold not satisfied"
            )

        if len(
            prior_ids
        ) == 1:
            reasons.append(
                "all proposals revise the same prior canonical hypothesis"
            )
        else:
            reasons.append(
                "mixed prior hypothesis IDs block revision"
            )

        if len(
            journal_heads
        ) == 1:
            reasons.append(
                "all proposals are grounded in the same branch-local lived history"
            )
        else:
            reasons.append(
                "mixed journal heads block revision"
            )

        if lived_provenance:
            reasons.append(
                "all proposals have lived branch history provenance"
            )
        else:
            reasons.append(
                "non-lived evidence provenance blocks revision"
            )

        if all_quarantined:
            reasons.append(
                "all provider proposals remained quarantined"
            )
        else:
            reasons.append(
                "unexpected pre-authorization detected"
            )

        if no_contradiction:
            reasons.append(
                "no unresolved contradiction registered"
            )
        else:
            reasons.append(
                "unresolved contradiction blocks revision"
            )

        technically_eligible = (
            consensus_fraction
            >= minimum_consensus_fraction
            and len(
                prior_ids
            ) == 1
            and len(
                journal_heads
            ) == 1
            and lived_provenance
            and all_quarantined
            and no_contradiction
            and representative
            is not None
        )

        results.append(
            BranchRevisionConsensus(
                branch_id=(
                    branch_id
                ),
                regime=(
                    regime
                ),
                prior_hypothesis_id=(
                    next(
                        iter(
                            prior_ids
                        )
                    )
                    if len(
                        prior_ids
                    ) == 1
                    else ""
                ),
                journal_head_hash=(
                    next(
                        iter(
                            journal_heads
                        )
                    )
                    if len(
                        journal_heads
                    ) == 1
                    else ""
                ),
                proposal_count=(
                    len(
                        calls
                    )
                ),
                decision_counts=dict(
                    sorted(
                        decisions.items()
                    )
                ),
                consensus_decision=(
                    consensus_decision
                    if consensus_fraction
                    >= minimum_consensus_fraction
                    else None
                ),
                consensus_fraction=(
                    consensus_fraction
                ),
                representative_candidate_id=(
                    representative[
                        "revision_candidate"
                    ][
                        "candidate_id"
                    ]
                    if representative
                    else None
                ),
                representative_proposition=(
                    representative[
                        "proposal"
                    ][
                        "updated_proposition"
                    ]
                    if representative
                    else None
                ),
                representative_confidence=(
                    float(
                        representative[
                            "proposal"
                        ][
                            "updated_confidence"
                        ]
                    )
                    if representative
                    else None
                ),
                support_candidate_ids=tuple(
                    sorted(
                        call[
                            "revision_candidate"
                        ][
                            "candidate_id"
                        ]
                        for call
                        in same_decision_calls
                    )
                ),
                technically_eligible_for_human_gate=(
                    technically_eligible
                ),
                reasons=tuple(
                    reasons
                ),
            )
        )

    eligible = [
        result
        for result
        in results
        if result.technically_eligible_for_human_gate
    ]

    ambiguous = [
        result
        for result
        in results
        if not result.technically_eligible_for_human_gate
    ]

    return {
        "experiment_id": (
            "SL-DIVERGENT-LIVED-HISTORY-"
            "CANONICAL-REVISION-CONSENSUS-001"
        ),
        "minimum_consensus_fraction": (
            minimum_consensus_fraction
        ),
        "branches": [
            result.to_dict()
            for result
            in results
        ],
        "eligible_branch_ids": [
            result.branch_id
            for result
            in eligible
        ],
        "ambiguous_branch_ids": [
            result.branch_id
            for result
            in ambiguous
        ],
        "human_approval_present": (
            False
        ),
        "canonical_revision_executed": (
            False
        ),
        "branch_merge_enabled": (
            False
        ),
        "automatic_canonical_revision_enabled": (
            False
        ),
        "automatic_second_promotion_enabled": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
    }
