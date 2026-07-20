from __future__ import annotations

from dataclasses import dataclass, asdict
from collections import Counter, defaultdict
import copy
import math

from .core import canonical_hash, stable_unit_float
from .rich_agent import SubjectiveRichExperience


SELF_INDEX_DOMAINS = (
    "agency",
    "continuity",
    "embodiment",
    "other_minds",
)

SHUFFLED_DOMAIN_MAP = {
    "agency": "other_minds",
    "other_minds": "embodiment",
    "embodiment": "continuity",
    "continuity": "agency",
}


@dataclass(frozen=True)
class AttentionMemory:
    memory_id: str
    step: int
    significance: float
    action: str
    resource_success: int
    hazard: int
    counterpart: str | None
    hint_action_received: str | None
    help_given: bool
    help_received: bool
    energy_delta: float
    integrity_delta: float
    zone_token: str
    regime_cue: str

    @classmethod
    def from_experience(
        cls,
        experience: SubjectiveRichExperience,
    ) -> "AttentionMemory":
        return cls(
            memory_id=experience.experience_id,
            step=experience.step,
            significance=experience.significance,
            action=experience.action,
            resource_success=experience.resource_success,
            hazard=experience.hazard,
            counterpart=experience.counterpart,
            hint_action_received=experience.hint_action_received,
            help_given=experience.help_given,
            help_received=experience.help_received,
            energy_delta=experience.energy_delta,
            integrity_delta=experience.integrity_delta,
            zone_token=experience.zone_token,
            regime_cue=experience.regime_cue,
        )

    def to_dict(self):
        return asdict(self)


def memory_matches_domain(
    memory: AttentionMemory,
    domain: str,
    current_step: int,
) -> bool:
    if domain == "agency":
        return (
            memory.action.startswith("QA")
            or memory.resource_success > 0
        )

    if domain == "embodiment":
        return (
            abs(memory.energy_delta) >= .05
            or abs(memory.integrity_delta) >= .05
            or memory.hazard > 0
        )

    if domain == "other_minds":
        return (
            memory.counterpart is not None
            or memory.hint_action_received is not None
            or memory.help_given
            or memory.help_received
        )

    if domain == "continuity":
        # An old event remaining directly retrievable is structural evidence of
        # continuity. This is computed dynamically rather than stored as a SELF tag.
        return (
            current_step
            - memory.step
            >= 120
        )

    return False


def memory_domain_signal(
    memory: AttentionMemory,
    domain: str,
    current_step: int,
) -> float:
    if domain == "agency":
        if not memory.action.startswith("QA"):
            return 0.0
        consequence = min(
            1.0,
            abs(memory.energy_delta) * 3.0
            + abs(memory.integrity_delta) * 3.0
            + .35 * memory.resource_success,
        )
        return consequence

    if domain == "embodiment":
        return min(
            1.0,
            abs(memory.energy_delta) * 3.0
            + abs(memory.integrity_delta) * 4.0
            + .45 * memory.hazard,
        )

    if domain == "other_minds":
        return min(
            1.0,
            .35 * int(memory.counterpart is not None)
            + .25 * int(memory.hint_action_received is not None)
            + .35 * int(memory.help_given)
            + .45 * int(memory.help_received),
        )

    if domain == "continuity":
        age = max(
            0,
            current_step
            - memory.step,
        )
        return min(
            1.0,
            age / 400.0,
        )

    return 0.0


@dataclass
class RetrievalAttentionState:
    selected_counts: dict[str, int]
    domain_attention_counts: dict[str, int]
    domain_signal_ewma: dict[str, float]
    retrieval_cycles: int = 0

    @classmethod
    def create(cls):
        return cls(
            selected_counts={},
            domain_attention_counts={
                domain: 0
                for domain
                in SELF_INDEX_DOMAINS
            },
            domain_signal_ewma={
                domain: .50
                for domain
                in SELF_INDEX_DOMAINS
            },
            retrieval_cycles=0,
        )

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self):
        return canonical_hash(
            self.to_dict()
        )

    def to_dict(self):
        return {
            "selected_counts": dict(
                sorted(
                    self.selected_counts.items()
                )
            ),
            "domain_attention_counts": dict(
                sorted(
                    self.domain_attention_counts.items()
                )
            ),
            "domain_signal_ewma": dict(
                sorted(
                    self.domain_signal_ewma.items()
                )
            ),
            "retrieval_cycles": self.retrieval_cycles,
        }


@dataclass(frozen=True)
class RetrievalResult:
    branch_id: str
    target_domain: str
    intervention_domain: str | None
    selected_memory_ids: tuple[str, ...]
    target_match_count: int
    target_signal_mean: float
    selected_significance_mean: float
    selected_age_mean: float
    diversity_reserved_count: int
    fabricated_memory_count: int

    def to_dict(self):
        return asdict(self)


class RetrievalAttentionController:
    """Bounded retrieval intervention over an immutable existing memory bank.

    F0:
        base recency + significance + novelty retrieval.

    F1:
        same base score plus a bounded FunctionalSelfIndex domain-match bonus.

    F2:
        same intervention magnitude, but the target domain is deterministically
        shuffled to a different domain. This tests specificity.

    Anti-echo constraints:
    - self-index bonus is capped;
    - at least `diversity_slots` are reserved for non-target memories when available;
    - previously selected memories receive a novelty penalty;
    - memory content is immutable;
    - only existing memory IDs may be returned.
    """

    def __init__(
        self,
        *,
        branch_id: str,
        memory_bank: tuple[AttentionMemory, ...],
        domain_confidence: dict[str, float],
        max_attention_bonus: float = .18,
        top_k: int = 10,
        diversity_slots: int = 3,
        shuffled_index: bool = False,
        fork_key: str = "shared",
    ):
        if not 0.0 <= max_attention_bonus <= .25:
            raise ValueError(
                "max_attention_bonus must remain in [0,.25]"
            )
        if diversity_slots >= top_k:
            raise ValueError(
                "diversity_slots must be smaller than top_k"
            )

        self.branch_id = branch_id
        self.memory_bank = memory_bank
        self.memory_ids = {
            memory.memory_id
            for memory
            in memory_bank
        }
        self.domain_confidence = {
            domain: float(
                domain_confidence.get(
                    domain,
                    0.0,
                )
            )
            for domain
            in SELF_INDEX_DOMAINS
        }
        self.max_attention_bonus = (
            max_attention_bonus
        )
        self.top_k = top_k
        self.diversity_slots = (
            diversity_slots
        )
        self.shuffled_index = (
            shuffled_index
        )
        self.fork_key = (
            fork_key
        )

    def _intervention_domain(
        self,
        target_domain: str,
    ) -> str:
        if self.shuffled_index:
            return SHUFFLED_DOMAIN_MAP[
                target_domain
            ]
        return target_domain

    def _base_score(
        self,
        memory: AttentionMemory,
        *,
        current_step: int,
        state: RetrievalAttentionState,
        cycle: int,
    ) -> float:
        age = max(
            0,
            current_step
            - memory.step,
        )
        recency = math.exp(
            -age / 220.0
        )
        novelty_penalty = .045 * min(
            4,
            state.selected_counts.get(
                memory.memory_id,
                0,
            ),
        )

        score = (
            .48 * recency
            + .42 * memory.significance
            - novelty_penalty
        )

        score += (
            1e-9
            * stable_unit_float(
                "retrieval-base-tie",
                self.fork_key,
                cycle,
                memory.memory_id,
            )
        )
        return score

    def retrieve(
        self,
        *,
        target_domain: str,
        current_step: int,
        state: RetrievalAttentionState,
        cycle: int,
        feedback_enabled: bool,
    ) -> RetrievalResult:
        if target_domain not in SELF_INDEX_DOMAINS:
            raise ValueError(
                "unsupported target domain"
            )

        intervention_domain = (
            self._intervention_domain(
                target_domain
            )
            if feedback_enabled
            else None
        )

        scored = []
        for memory in self.memory_bank:
            base = self._base_score(
                memory,
                current_step=current_step,
                state=state,
                cycle=cycle,
            )
            bonus = 0.0

            if (
                feedback_enabled
                and memory_matches_domain(
                    memory,
                    intervention_domain,
                    current_step,
                )
            ):
                bonus = min(
                    self.max_attention_bonus,
                    self.max_attention_bonus
                    * self.domain_confidence[
                        intervention_domain
                    ],
                )

            scored.append(
                (
                    base + bonus,
                    base,
                    memory,
                )
            )

        scored.sort(
            key=lambda row: (
                row[0],
                row[1],
                row[2].memory_id,
            ),
            reverse=True,
        )

        target_rows = [
            row
            for row in scored
            if memory_matches_domain(
                row[2],
                target_domain,
                current_step,
            )
        ]
        non_target_rows = [
            row
            for row in scored
            if not memory_matches_domain(
                row[2],
                target_domain,
                current_step,
            )
        ]

        # Reserve diversity slots from non-target evidence whenever available.
        selected = []

        reserved = min(
            self.diversity_slots,
            len(
                non_target_rows
            ),
        )

        selected.extend(
            row[2]
            for row
            in non_target_rows[
                :reserved
            ]
        )

        selected_ids = {
            item.memory_id
            for item
            in selected
        }

        for _score, _base, memory in scored:
            if len(
                selected
            ) >= self.top_k:
                break
            if memory.memory_id in selected_ids:
                continue
            selected.append(
                memory
            )
            selected_ids.add(
                memory.memory_id
            )

        fabricated = sum(
            1
            for memory
            in selected
            if memory.memory_id
            not in self.memory_ids
        )

        target_match = sum(
            1
            for memory
            in selected
            if memory_matches_domain(
                memory,
                target_domain,
                current_step,
            )
        )

        signals = [
            memory_domain_signal(
                memory,
                target_domain,
                current_step,
            )
            for memory
            in selected
        ]

        significance = [
            memory.significance
            for memory
            in selected
        ]

        ages = [
            current_step
            - memory.step
            for memory
            in selected
        ]

        return RetrievalResult(
            branch_id=self.branch_id,
            target_domain=target_domain,
            intervention_domain=(
                intervention_domain
            ),
            selected_memory_ids=tuple(
                memory.memory_id
                for memory
                in selected
            ),
            target_match_count=(
                target_match
            ),
            target_signal_mean=(
                sum(
                    signals
                )
                / len(
                    signals
                )
                if signals
                else 0.0
            ),
            selected_significance_mean=(
                sum(
                    significance
                )
                / len(
                    significance
                )
                if significance
                else 0.0
            ),
            selected_age_mean=(
                sum(
                    ages
                )
                / len(
                    ages
                )
                if ages
                else 0.0
            ),
            diversity_reserved_count=(
                reserved
            ),
            fabricated_memory_count=(
                fabricated
            ),
        )


def apply_retrieval_to_attention_state(
    *,
    result: RetrievalResult,
    memory_bank_by_id: dict[
        str,
        AttentionMemory,
    ],
    state: RetrievalAttentionState,
    current_step: int,
):
    for memory_id in (
        result.selected_memory_ids
    ):
        state.selected_counts[
            memory_id
        ] = (
            state.selected_counts.get(
                memory_id,
                0,
            )
            + 1
        )

    state.domain_attention_counts[
        result.target_domain
    ] += (
        result.target_match_count
    )

    prior = state.domain_signal_ewma[
        result.target_domain
    ]

    state.domain_signal_ewma[
        result.target_domain
    ] = (
        .82 * prior
        + .18 * result.target_signal_mean
    )

    state.retrieval_cycles += 1


def jaccard_divergence(
    left: tuple[str, ...],
    right: tuple[str, ...],
) -> float:
    a = set(
        left
    )
    b = set(
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
