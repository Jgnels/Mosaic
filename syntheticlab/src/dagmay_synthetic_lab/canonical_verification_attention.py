from __future__ import annotations

from dataclasses import dataclass, asdict
from collections import Counter
import math

from .core import canonical_hash, stable_unit_float
from .retrieval_attention_feedback import (
    AttentionMemory,
)


@dataclass
class VerificationAttentionState:
    selected_counts: dict[str, int]
    cycles: int = 0

    @classmethod
    def create(cls):
        return cls(
            selected_counts={},
            cycles=0,
        )

    def to_dict(self):
        payload = {
            "selected_counts": dict(
                sorted(
                    self.selected_counts.items()
                )
            ),
            "cycles": self.cycles,
        }
        return {
            **payload,
            "state_hash": canonical_hash(
                payload
            ),
        }


@dataclass(frozen=True)
class VerificationRetrieval:
    branch_id: str
    cycle: int
    selected_memory_ids: tuple[str, ...]
    same_domain_count: int
    source_episode_count: int
    independent_same_domain_count: int
    supportive_social_count: int
    counterevidence_proxy_count: int
    diversity_count: int

    def to_dict(self):
        return asdict(self)


def _is_social(
    memory: AttentionMemory,
) -> bool:
    return (
        memory.counterpart is not None
        or memory.hint_action_received is not None
        or memory.help_given
        or memory.help_received
    )


def _is_supportive_social(
    memory: AttentionMemory,
) -> bool:
    return (
        memory.help_received
        or memory.hint_action_received
        is not None
    )


def _is_counterevidence_proxy(
    memory: AttentionMemory,
) -> bool:
    # Candidate claim: multiple counterparts can provide assistance/guidance.
    # A social record with a counterpart but no received help or hint is not a
    # logical disproof, but it is useful disconfirmatory/limiting evidence.
    return (
        memory.counterpart is not None
        and not memory.help_received
        and memory.hint_action_received
        is None
    )


class CanonicalHypothesisVerifier:
    """Bounded verification-oriented retrieval after canonical promotion.

    The verifier intentionally does NOT re-retrieve the candidate's own source
    episodes. It seeks independent same-domain evidence and reserves diversity
    slots outside the promoted domain.

    The first implementation supports the `other_minds` domain used by the
    v19.0 exact-fork experiment.
    """

    def __init__(
        self,
        *,
        branch_id: str,
        memory_bank: tuple[AttentionMemory, ...],
        source_memory_ids: set[str],
        verification_enabled: bool,
        fork_key: str,
        top_k: int = 10,
        diversity_slots: int = 3,
        same_domain_bonus: float = .16,
        counterevidence_bonus: float = .08,
    ):
        self.branch_id = branch_id
        self.memory_bank = memory_bank
        self.source_memory_ids = set(
            source_memory_ids
        )
        self.verification_enabled = (
            verification_enabled
        )
        self.fork_key = fork_key
        self.top_k = top_k
        self.diversity_slots = (
            diversity_slots
        )
        self.same_domain_bonus = (
            same_domain_bonus
        )
        self.counterevidence_bonus = (
            counterevidence_bonus
        )

    def _base_score(
        self,
        memory: AttentionMemory,
        *,
        current_step: int,
        state: VerificationAttentionState,
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
        novelty_penalty = .055 * min(
            5,
            state.selected_counts.get(
                memory.memory_id,
                0,
            ),
        )

        score = (
            .50 * recency
            + .45 * memory.significance
            - novelty_penalty
        )

        score += (
            1e-9
            * stable_unit_float(
                "canonical-verification-tie",
                self.fork_key,
                cycle,
                memory.memory_id,
            )
        )

        return score

    def retrieve(
        self,
        *,
        current_step: int,
        state: VerificationAttentionState,
        cycle: int,
    ) -> VerificationRetrieval:
        scored = []

        for memory in self.memory_bank:
            base = self._base_score(
                memory,
                current_step=current_step,
                state=state,
                cycle=cycle,
            )

            score = base

            if self.verification_enabled:
                if (
                    memory.memory_id
                    in self.source_memory_ids
                ):
                    # Do not allow the promoted hypothesis to "verify" itself by
                    # repeatedly resurfacing the exact episodes that created it.
                    score -= 10.0
                elif _is_social(
                    memory
                ):
                    score += (
                        self.same_domain_bonus
                    )

                    if _is_counterevidence_proxy(
                        memory
                    ):
                        score += (
                            self.counterevidence_bonus
                        )

            scored.append(
                (
                    score,
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

        social_rows = [
            row
            for row in scored
            if _is_social(
                row[2]
            )
        ]

        non_social_rows = [
            row
            for row in scored
            if not _is_social(
                row[2]
            )
        ]

        selected = []

        # Preserve domain diversity even in the promoted branch.
        for _score, _base, memory in non_social_rows[
            :self.diversity_slots
        ]:
            selected.append(
                memory
            )

        selected_ids = {
            memory.memory_id
            for memory
            in selected
        }

        for _score, _base, memory in scored:
            if len(
                selected
            ) >= self.top_k:
                break

            if (
                memory.memory_id
                in selected_ids
            ):
                continue

            selected.append(
                memory
            )
            selected_ids.add(
                memory.memory_id
            )

        source_count = sum(
            1
            for memory
            in selected
            if memory.memory_id
            in self.source_memory_ids
        )

        same_domain_count = sum(
            1
            for memory
            in selected
            if _is_social(
                memory
            )
        )

        independent_same_domain = sum(
            1
            for memory
            in selected
            if (
                _is_social(
                    memory
                )
                and memory.memory_id
                not in self.source_memory_ids
            )
        )

        supportive = sum(
            1
            for memory
            in selected
            if _is_supportive_social(
                memory
            )
        )

        counter = sum(
            1
            for memory
            in selected
            if _is_counterevidence_proxy(
                memory
            )
        )

        return VerificationRetrieval(
            branch_id=(
                self.branch_id
            ),
            cycle=cycle,
            selected_memory_ids=tuple(
                memory.memory_id
                for memory
                in selected
            ),
            same_domain_count=(
                same_domain_count
            ),
            source_episode_count=(
                source_count
            ),
            independent_same_domain_count=(
                independent_same_domain
            ),
            supportive_social_count=(
                supportive
            ),
            counterevidence_proxy_count=(
                counter
            ),
            diversity_count=sum(
                1
                for memory
                in selected
                if not _is_social(
                    memory
                )
            ),
        )


def apply_verification_retrieval(
    *,
    result: VerificationRetrieval,
    state: VerificationAttentionState,
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

    state.cycles += 1


def selection_entropy(
    results: list[
        VerificationRetrieval
    ],
) -> float:
    counter = Counter(
        memory_id
        for result
        in results
        for memory_id
        in result.selected_memory_ids
    )

    total = sum(
        counter.values()
    )

    if total <= 0:
        return 0.0

    entropy = 0.0

    for count in counter.values():
        p = count / total
        entropy -= (
            p
            * math.log(
                p,
                2,
            )
        )

    return entropy
