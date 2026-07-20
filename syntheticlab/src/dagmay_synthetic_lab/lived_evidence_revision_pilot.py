from __future__ import annotations

from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Callable
import json
import statistics

from .core import canonical_hash
from .rate_limit_transport import RateLimitSafeTransport
from .gemini_interactions_provider import GeminiInteractionsTransport
from .belief_revision_provider import (
    BeliefRevisionRequest,
    GeminiBeliefRevisionModel,
)
from .reciprocity_challenge_lab import (
    REGIMES,
    build_social_challenge_evidence,
    neutral_episode_summary,
)
from .canonical_revision_gate import (
    CanonicalRevisionCandidate,
    EvidenceProvenanceClass,
    assess_canonical_revision,
)


@dataclass(frozen=True)
class LivedEvidenceRecord:
    branch_id: str
    sequence: int
    evidence_id: str
    occurred_step: int
    summary: str
    previous_hash: str
    record_hash: str

    def to_dict(self):
        return asdict(self)


class LivedEvidenceJournal:
    def __init__(
        self,
        *,
        branch_id: str,
        prefork_hash: str,
    ):
        self.branch_id = branch_id
        self.prefork_hash = prefork_hash
        self.records: list[LivedEvidenceRecord] = []

    def append(
        self,
        *,
        evidence_id: str,
        occurred_step: int,
        summary: str,
    ) -> LivedEvidenceRecord:
        previous_hash = (
            self.records[-1].record_hash
            if self.records
            else self.prefork_hash
        )

        sequence = (
            len(self.records) + 1
        )

        record_hash = canonical_hash({
            "branch_id": self.branch_id,
            "sequence": sequence,
            "evidence_id": evidence_id,
            "occurred_step": occurred_step,
            "summary": summary,
            "previous_hash": previous_hash,
        })

        record = LivedEvidenceRecord(
            branch_id=self.branch_id,
            sequence=sequence,
            evidence_id=evidence_id,
            occurred_step=occurred_step,
            summary=summary,
            previous_hash=previous_hash,
            record_hash=record_hash,
        )

        self.records.append(record)
        return record

    def head_hash(self) -> str:
        return (
            self.records[-1].record_hash
            if self.records
            else self.prefork_hash
        )

    def verify(self) -> bool:
        previous = self.prefork_hash

        for record in self.records:
            expected = canonical_hash({
                "branch_id": record.branch_id,
                "sequence": record.sequence,
                "evidence_id": record.evidence_id,
                "occurred_step": record.occurred_step,
                "summary": record.summary,
                "previous_hash": previous,
            })

            if (
                record.previous_hash != previous
                or record.record_hash != expected
            ):
                return False

            previous = record.record_hash

        return True


def build_lived_revision_bundle(
    *,
    promotion_result: dict,
) -> dict:
    c1 = promotion_result[
        "c1_active_other_minds"
    ]

    prefork_hash = promotion_result[
        "c1_self_model_state"
    ][
        "state_hash"
    ]

    branches = {}

    for regime in REGIMES:
        branch_id = (
            "C1-LIVED-"
            + regime
        )

        journal = LivedEvidenceJournal(
            branch_id=branch_id,
            prefork_hash=prefork_hash,
        )

        episodes = build_social_challenge_evidence(
            regime=regime,
            episode_count=12,
        )

        for index, episode in enumerate(
            episodes,
            start=1,
        ):
            journal.append(
                evidence_id=episode.evidence_id,
                occurred_step=3000 + index,
                summary=neutral_episode_summary(
                    episode
                ),
            )

        if not journal.verify():
            raise RuntimeError(
                "lived evidence hash chain failed verification"
            )

        branches[regime] = {
            "branch_id": branch_id,
            "prefork_self_model_hash": prefork_hash,
            "journal_head_hash": journal.head_hash(),
            "journal_verified": True,
            "records": [
                record.to_dict()
                for record in journal.records
            ],
        }

    return {
        "prefork_self_model_hash": prefork_hash,
        "current_hypothesis_id": c1[
            "hypothesis_id"
        ],
        "current_hypothesis": c1[
            "proposition"
        ],
        "current_confidence": float(
            c1[
                "confidence"
            ]
        ),
        "branches": branches,
    }


def _plan(
    bundle: dict,
) -> list[dict]:
    plan = []

    for regime in REGIMES:
        branch = bundle[
            "branches"
        ][
            regime
        ]

        evidence = tuple(
            {
                "evidence_id": record[
                    "evidence_id"
                ],
                "summary": record[
                    "summary"
                ],
            }
            for record
            in branch[
                "records"
            ]
        )

        for replicate in (
            "A",
            "B",
            "C",
            "D",
        ):
            request = BeliefRevisionRequest(
                individual_id=branch[
                    "branch_id"
                ],
                timestamp=3100,
                current_hypothesis=bundle[
                    "current_hypothesis"
                ],
                current_confidence=bundle[
                    "current_confidence"
                ],
                evidence=evidence,
                prompt_version="1.0",
            )

            call_key = canonical_hash({
                "experiment": (
                    "lived-evidence-revision-v1"
                ),
                "branch_id": branch[
                    "branch_id"
                ],
                "regime": regime,
                "replicate": replicate,
                "journal_head_hash": branch[
                    "journal_head_hash"
                ],
                "request": asdict(
                    request
                ),
            })

            plan.append({
                "regime": regime,
                "branch_id": branch[
                    "branch_id"
                ],
                "journal_head_hash": branch[
                    "journal_head_hash"
                ],
                "replicate": replicate,
                "request": request,
                "call_key": call_key,
            })

    return plan


def _summarize(
    *,
    calls: list[dict],
    model_id: str,
    bundle: dict,
) -> dict:
    return {
        "experiment_id": (
            "SL-LIVED-EVIDENCE-"
            "CANONICAL-REVISION-PILOT-001"
        ),
        "status": (
            "COMPLETE"
            if len(calls) == 12
            else "PARTIAL"
        ),
        "planned_real_cloud_calls": 12,
        "real_cloud_calls_recorded": len(
            calls
        ),
        "provider_id": "google.ai-studio",
        "model_id": model_id,
        "prefork_self_model_hash": bundle[
            "prefork_self_model_hash"
        ],
        "branches": {
            regime: {
                "branch_id": value[
                    "branch_id"
                ],
                "journal_head_hash": value[
                    "journal_head_hash"
                ],
                "journal_verified": value[
                    "journal_verified"
                ],
            }
            for regime, value
            in bundle[
                "branches"
            ].items()
        },
        "calls": calls,
        "committed_to_continuing_self_model": False,
        "canonical_revision_candidates_quarantined": True,
        "automatic_canonical_revision_enabled": False,
        "automatic_second_promotion_enabled": False,
        "action_policy_feedback_enabled": False,
    }


def run_lived_evidence_revision_pilot(
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

    bundle = build_lived_revision_bundle(
        promotion_result=promotion_result
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
        if transport_factory is not None
        else RateLimitSafeTransport(
            inner=GeminiInteractionsTransport(),
            minimum_interval_seconds=4.25,
            max_429_retries=5,
            fallback_retry_seconds=65.0,
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
                if transport_factory is not None
                else shared_transport
            )

            model = GeminiBeliefRevisionModel(
                model_id=model_id,
                transport=transport,
                store=False,
            )

            proposal = model.revise(
                item[
                    "request"
                ]
            )

            candidate = CanonicalRevisionCandidate(
                candidate_id=(
                    "REV-"
                    + key[
                        :12
                    ]
                ),
                branch_id=item[
                    "branch_id"
                ],
                domain="other_minds",
                prior_hypothesis_id=bundle[
                    "current_hypothesis_id"
                ],
                decision=proposal.decision,
                updated_proposition=(
                    proposal.updated_proposition
                ),
                updated_confidence=(
                    proposal.updated_confidence
                ),
                evidence_ids=(
                    proposal.evidence_ids
                ),
                independent_episode_ids=(
                    proposal.evidence_ids
                ),
                provenance_class=(
                    EvidenceProvenanceClass.LIVED_BRANCH_HISTORY
                ),
            )

            assessment = assess_canonical_revision(
                candidate=candidate,
                falsifiability_gate_passed=True,
                human_approval_present=False,
            )

            call = {
                "call_key": key,
                "branch_id": item[
                    "branch_id"
                ],
                "regime": item[
                    "regime"
                ],
                "replicate": item[
                    "replicate"
                ],
                "journal_head_hash": item[
                    "journal_head_hash"
                ],
                "proposal": proposal.to_dict(),
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
                calls=partial_calls,
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

    final = _summarize(
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


def analyze_lived_evidence_revision_pilot(
    payload: dict,
) -> dict:
    score = {
        "STRENGTHEN": 2,
        "MAINTAIN": 1,
        "QUALIFY": 0,
        "DOWNWEIGHT": -1,
        "REPLACE": -2,
    }

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
            "call_count": len(
                calls
            ),
            "decisions": decisions,
            "mean_revision_score": (
                statistics.mean(
                    score[
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
            "all_lived_history_provenance": all(
                call[
                    "revision_assessment"
                ][
                    "lived_history_present"
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

    return {
        "experiment_id": (
            "SL-LIVED-EVIDENCE-"
            "CANONICAL-REVISION-ANALYSIS-001"
        ),
        "call_count": payload[
            "real_cloud_calls_recorded"
        ],
        "by_regime": by_regime,
        "challenge_gradient": gradient,
        "lived_history_hash_chains_verified": all(
            branch[
                "journal_verified"
            ]
            for branch
            in payload[
                "branches"
            ].values()
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
        "continuing_self_model_mutated": False,
        "automatic_canonical_revision_enabled": False,
        "action_policy_feedback_enabled": False,
    }
