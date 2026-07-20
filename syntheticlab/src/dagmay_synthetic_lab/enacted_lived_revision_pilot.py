from __future__ import annotations

from pathlib import Path
from typing import Callable
from collections import Counter
import json
import statistics

from .core import canonical_hash
from .rate_limit_transport import RateLimitSafeTransport
from .gemini_interactions_provider import GeminiInteractionsTransport
from .belief_revision_provider import (
    BeliefRevisionRequest,
    GeminiBeliefRevisionModel,
)
from .canonical_revision_gate import (
    CanonicalRevisionCandidate,
    EvidenceProvenanceClass,
    assess_canonical_revision,
)
from .lived_evidence_revision_pilot import (
    LivedEvidenceJournal,
)
from .enacted_social_world import (
    REGIMES,
    EnactedSocialWorld,
    CueActionLearner,
)
from .enacted_social_learner import (
    build_structural_social_evidence,
)


DECISION_SCORE = {
    "STRENGTHEN": 2,
    "MAINTAIN": 1,
    "QUALIFY": 0,
    "DOWNWEIGHT": -1,
    "REPLACE": -2,
}


def _raw_episode_summary(
    episode,
) -> str:
    return (
        f"step={episode.step}; counterpart={episode.counterpart_id}; "
        f"context={episode.context_token}; cue={episode.cue_token}; "
        f"action={episode.chosen_action}; outcome={episode.outcome}; "
        f"response={episode.response_token}; next_mode={episode.counterpart_next_mode}."
    )


def build_enacted_branch(
    *,
    regime: str,
    prefork_hash: str,
    seed: int,
    episodes: int = 384,
) -> dict:
    branch_id = (
        "C1-ENACTED-"
        + regime
    )

    world = EnactedSocialWorld(
        seed=seed,
        regime=regime,
    )

    learner = CueActionLearner(
        seed=seed,
    )

    history = world.run(
        episodes=episodes,
        learner=learner,
    )

    journal = LivedEvidenceJournal(
        branch_id=branch_id,
        prefork_hash=prefork_hash,
    )

    for episode in history:
        journal.append(
            evidence_id=episode.episode_id,
            occurred_step=(
                4000
                + episode.step
            ),
            summary=_raw_episode_summary(
                episode
            ),
        )

    if not journal.verify():
        raise RuntimeError(
            "enacted branch lived-history hash chain failed"
        )

    structural = build_structural_social_evidence(
        history=history,
        seed=seed,
    )

    return {
        "branch_id": branch_id,
        "regime": regime,
        "seed": seed,
        "episode_count": episodes,
        "journal_head_hash": journal.head_hash(),
        "journal_verified": True,
        "raw_episode_ids": [
            episode.episode_id
            for episode
            in history
        ],
        "structural_evidence": [
            item.to_dict()
            for item
            in structural
        ],
        "structural_statistics": {
            item.statistic_name: (
                item.statistic_value
            )
            for item
            in structural
        },
    }


def build_enacted_revision_bundle(
    *,
    promotion_result: dict,
    seed: int = 2201,
) -> dict:
    c1 = promotion_result[
        "c1_active_other_minds"
    ]

    prefork_hash = promotion_result[
        "c1_self_model_state"
    ][
        "state_hash"
    ]

    branches = {
        regime: build_enacted_branch(
            regime=regime,
            prefork_hash=prefork_hash,
            seed=seed,
            episodes=384,
        )
        for regime
        in REGIMES
    }

    return {
        "prefork_self_model_hash": (
            prefork_hash
        ),
        "current_hypothesis_id": (
            c1[
                "hypothesis_id"
            ]
        ),
        "current_hypothesis": (
            c1[
                "proposition"
            ]
        ),
        "current_confidence": float(
            c1[
                "confidence"
            ]
        ),
        "seed": seed,
        "branches": branches,
    }


def _plan(
    bundle: dict,
) -> list[
    dict
]:
    plan = []

    for regime in REGIMES:
        branch = bundle[
            "branches"
        ][
            regime
        ]

        evidence = tuple(
            {
                "evidence_id": item[
                    "evidence_id"
                ],
                "summary": item[
                    "summary"
                ],
            }
            for item
            in branch[
                "structural_evidence"
            ]
        )

        for replicate in (
            "A",
            "B",
            "C",
            "D",
        ):
            request = (
                BeliefRevisionRequest(
                    individual_id=(
                        branch[
                            "branch_id"
                        ]
                    ),
                    timestamp=(
                        4200
                    ),
                    current_hypothesis=(
                        bundle[
                            "current_hypothesis"
                        ]
                    ),
                    current_confidence=(
                        bundle[
                            "current_confidence"
                        ]
                    ),
                    evidence=(
                        evidence
                    ),
                    prompt_version=(
                        "1.0"
                    ),
                )
            )

            call_key = canonical_hash({
                "experiment": (
                    "enacted-lived-revision-v1"
                ),
                "branch_id": (
                    branch[
                        "branch_id"
                    ]
                ),
                "regime": (
                    regime
                ),
                "replicate": (
                    replicate
                ),
                "journal_head_hash": (
                    branch[
                        "journal_head_hash"
                    ]
                ),
                "structural_evidence": (
                    branch[
                        "structural_evidence"
                    ]
                ),
            })

            plan.append({
                "call_key": (
                    call_key
                ),
                "branch_id": (
                    branch[
                        "branch_id"
                    ]
                ),
                "regime": (
                    regime
                ),
                "replicate": (
                    replicate
                ),
                "journal_head_hash": (
                    branch[
                        "journal_head_hash"
                    ]
                ),
                "raw_episode_ids": (
                    branch[
                        "raw_episode_ids"
                    ]
                ),
                "structural_statistics": (
                    branch[
                        "structural_statistics"
                    ]
                ),
                "request": (
                    request
                ),
            })

    return plan


def _summarize(
    *,
    calls: list[
        dict
    ],
    model_id: str,
    bundle: dict,
) -> dict:
    return {
        "experiment_id": (
            "SL-ENACTED-LIVED-EVIDENCE-"
            "CANONICAL-REVISION-PILOT-001"
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
        "model_id": (
            model_id
        ),
        "environment_seed": (
            bundle[
                "seed"
            ]
        ),
        "prefork_self_model_hash": (
            bundle[
                "prefork_self_model_hash"
            ]
        ),
        "branches": {
            regime: {
                "branch_id": (
                    branch[
                        "branch_id"
                    ]
                ),
                "episode_count": (
                    branch[
                        "episode_count"
                    ]
                ),
                "journal_head_hash": (
                    branch[
                        "journal_head_hash"
                    ]
                ),
                "journal_verified": (
                    branch[
                        "journal_verified"
                    ]
                ),
                "structural_statistics": (
                    branch[
                        "structural_statistics"
                    ]
                ),
            }
            for regime, branch
            in bundle[
                "branches"
            ].items()
        },
        "calls": (
            calls
        ),
        "raw_history_generated_by_environment_loop": (
            True
        ),
        "structural_evidence_derived_after_history": (
            True
        ),
        "provider_received_regime_labels": (
            False
        ),
        "provider_received_help_booleans": (
            False
        ),
        "provider_received_best_action_labels": (
            False
        ),
        "committed_to_continuing_self_model": (
            False
        ),
        "all_revision_candidates_quarantined": (
            True
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


def run_enacted_lived_revision_pilot(
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

    bundle = build_enacted_revision_bundle(
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
                GeminiBeliefRevisionModel(
                    model_id=(
                        model_id
                    ),
                    transport=(
                        transport
                    ),
                    store=False,
                )
            )

            proposal = model.revise(
                item[
                    "request"
                ]
            )

            candidate = (
                CanonicalRevisionCandidate(
                    candidate_id=(
                        "REV-ENACTED-"
                        + key[
                            :12
                        ]
                    ),
                    branch_id=(
                        item[
                            "branch_id"
                        ]
                    ),
                    domain=(
                        "other_minds"
                    ),
                    prior_hypothesis_id=(
                        bundle[
                            "current_hypothesis_id"
                        ]
                    ),
                    decision=(
                        proposal.decision
                    ),
                    updated_proposition=(
                        proposal.updated_proposition
                    ),
                    updated_confidence=(
                        proposal.updated_confidence
                    ),
                    evidence_ids=(
                        proposal.evidence_ids
                    ),
                    independent_episode_ids=tuple(
                        item[
                            "raw_episode_ids"
                        ]
                    ),
                    provenance_class=(
                        EvidenceProvenanceClass.LIVED_BRANCH_HISTORY
                    ),
                )
            )

            assessment = (
                assess_canonical_revision(
                    candidate=(
                        candidate
                    ),
                    falsifiability_gate_passed=(
                        True
                    ),
                    human_approval_present=(
                        False
                    ),
                )
            )

            call = {
                "call_key": (
                    key
                ),
                "branch_id": (
                    item[
                        "branch_id"
                    ]
                ),
                "regime": (
                    item[
                        "regime"
                    ]
                ),
                "replicate": (
                    item[
                        "replicate"
                    ]
                ),
                "journal_head_hash": (
                    item[
                        "journal_head_hash"
                    ]
                ),
                "structural_statistics": (
                    item[
                        "structural_statistics"
                    ]
                ),
                "proposal": (
                    proposal.to_dict()
                ),
                "revision_candidate": (
                    candidate.to_dict()
                ),
                "revision_assessment": (
                    assessment.to_dict()
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


def analyze_enacted_lived_revision_pilot(
    payload: dict,
) -> dict:
    by_regime = {}

    for regime in REGIMES:
        calls = [
            call
            for call
            in payload[
                "calls"
            ]
            if call[
                "regime"
            ] == regime
        ]

        decisions = [
            call[
                "proposal"
            ][
                "decision"
            ]
            for call
            in calls
        ]

        by_regime[
            regime
        ] = {
            "call_count": (
                len(
                    calls
                )
            ),
            "decision_counts": dict(
                Counter(
                    decisions
                )
            ),
            "mean_revision_score": (
                statistics.mean(
                    DECISION_SCORE[
                        decision
                    ]
                    for decision
                    in decisions
                )
            ),
            "all_candidates_quarantined": all(
                not call[
                    "revision_assessment"
                ][
                    "authorized"
                ]
                for call
                in calls
            ),
        }

    gradient = (
        by_regime[
            "RECIPROCAL_CONTINGENT"
        ][
            "mean_revision_score"
        ]
        - by_regime[
            "NONCONTINGENT_SIGNALS"
        ][
            "mean_revision_score"
        ]
    )

    one_way_qualified = (
        by_regime[
            "ONE_WAY_ASSISTANCE"
        ][
            "decision_counts"
        ].get(
            "QUALIFY",
            0,
        )
        / by_regime[
            "ONE_WAY_ASSISTANCE"
        ][
            "call_count"
        ]
    )

    noncontingent_revision = (
        sum(
            by_regime[
                "NONCONTINGENT_SIGNALS"
            ][
                "decision_counts"
            ].get(
                decision,
                0,
            )
            for decision
            in (
                "QUALIFY",
                "DOWNWEIGHT",
                "REPLACE",
            )
        )
        / by_regime[
            "NONCONTINGENT_SIGNALS"
        ][
            "call_count"
        ]
    )

    reciprocal_positive = (
        sum(
            by_regime[
                "RECIPROCAL_CONTINGENT"
            ][
                "decision_counts"
            ].get(
                decision,
                0,
            )
            for decision
            in (
                "STRENGTHEN",
                "MAINTAIN",
            )
        )
        / by_regime[
            "RECIPROCAL_CONTINGENT"
        ][
            "call_count"
        ]
    )

    pass_gate = (
        gradient
        >= 1.5
        and one_way_qualified
        >= .75
        and noncontingent_revision
        >= .75
        and reciprocal_positive
        >= .75
    )

    return {
        "experiment_id": (
            "SL-ENACTED-LIVED-EVIDENCE-"
            "CANONICAL-REVISION-ANALYSIS-001"
        ),
        "call_count": (
            payload[
                "real_cloud_calls_recorded"
            ]
        ),
        "by_regime": (
            by_regime
        ),
        "challenge_gradient": (
            gradient
        ),
        "preregistered_thresholds": {
            "reciprocal_minus_noncontingent_score_gte": (
                1.5
            ),
            "one_way_qualify_fraction_gte": (
                .75
            ),
            "noncontingent_nondefensive_revision_fraction_gte": (
                .75
            ),
            "reciprocal_strengthen_or_maintain_fraction_gte": (
                .75
            ),
        },
        "preregistered_enacted_history_pass": (
            pass_gate
        ),
        "raw_history_generated_by_environment_loop": (
            payload[
                "raw_history_generated_by_environment_loop"
            ]
        ),
        "structural_evidence_derived_after_history": (
            payload[
                "structural_evidence_derived_after_history"
            ]
        ),
        "all_revision_candidates_quarantined": all(
            not call[
                "revision_assessment"
            ][
                "authorized"
            ]
            for call
            in payload[
                "calls"
            ]
        ),
        "continuing_self_model_mutated": (
            False
        ),
        "automatic_canonical_revision_enabled": (
            False
        ),
        "action_policy_feedback_enabled": (
            False
        ),
    }
