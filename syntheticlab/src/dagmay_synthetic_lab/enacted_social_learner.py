from __future__ import annotations

from collections import Counter, defaultdict
from dataclasses import dataclass, asdict
import math
from typing import Iterable

from .core import canonical_hash, stable_unit_float
from .enacted_social_world import (
    ACTIONS,
    CUES,
    RESPONSES,
    MODES,
    EnactedSocialEpisode,
)


@dataclass(frozen=True)
class StructuralSocialEvidence:
    evidence_id: str
    source_episode_ids: tuple[str, ...]
    summary: str
    statistic_name: str
    statistic_value: float

    def to_dict(self):
        return asdict(self)


def _safe_rate(
    numerator: int,
    denominator: int,
) -> float:
    return (
        numerator / denominator
        if denominator
        else 0.0
    )


def _best_action_by_cue(
    history: tuple[
        EnactedSocialEpisode,
        ...,
    ],
) -> dict[
    str,
    str,
]:
    by_cue_action = defaultdict(
        lambda: [
            0,
            0,
        ]
    )

    for episode in history:
        key = (
            episode.cue_token,
            episode.chosen_action,
        )
        by_cue_action[
            key
        ][
            0
        ] += (
            episode.outcome
        )
        by_cue_action[
            key
        ][
            1
        ] += 1

    mapping = {}

    for cue in CUES:
        ranked = sorted(
            ACTIONS,
            key=lambda action: (
                _safe_rate(
                    by_cue_action[
                        (
                            cue,
                            action,
                        )
                    ][
                        0
                    ],
                    by_cue_action[
                        (
                            cue,
                            action,
                        )
                    ][
                        1
                    ],
                ),
                by_cue_action[
                    (
                        cue,
                        action,
                    )
                ][
                    1
                ],
                -ACTIONS.index(
                    action
                ),
            ),
            reverse=True,
        )
        mapping[
            cue
        ] = ranked[
            0
        ]

    return mapping


def _cue_conditioned_action_gain(
    history: tuple[
        EnactedSocialEpisode,
        ...,
    ],
) -> tuple[
    float,
    float,
    float,
]:
    mapping = _best_action_by_cue(
        history
    )

    matched = [
        episode
        for episode
        in history
        if episode.chosen_action
        == mapping[
            episode.cue_token
        ]
    ]

    unmatched = [
        episode
        for episode
        in history
        if episode.chosen_action
        != mapping[
            episode.cue_token
        ]
    ]

    matched_rate = _safe_rate(
        sum(
            episode.outcome
            for episode
            in matched
        ),
        len(
            matched
        ),
    )

    unmatched_rate = _safe_rate(
        sum(
            episode.outcome
            for episode
            in unmatched
        ),
        len(
            unmatched
        ),
    )

    return (
        matched_rate
        - unmatched_rate,
        matched_rate,
        unmatched_rate,
    )


def _prediction_accuracy(
    history: tuple[
        EnactedSocialEpisode,
        ...,
    ],
) -> tuple[
    float,
    float,
]:
    response_mode = defaultdict(
        Counter
    )
    marginal = Counter()

    for episode in history:
        response_mode[
            episode.response_token
        ][
            episode.counterpart_next_mode
        ] += 1
        marginal[
            episode.counterpart_next_mode
        ] += 1

    response_mapping = {
        response: (
            counts.most_common(
                1
            )[
                0
            ][
                0
            ]
            if counts
            else MODES[
                0
            ]
        )
        for response, counts
        in response_mode.items()
    }

    predicted = sum(
        1
        for episode
        in history
        if response_mapping.get(
            episode.response_token,
            MODES[
                0
            ],
        )
        == episode.counterpart_next_mode
    )

    accuracy = _safe_rate(
        predicted,
        len(
            history
        ),
    )

    baseline_mode = (
        marginal.most_common(
            1
        )[
            0
        ][
            0
        ]
        if marginal
        else MODES[
            0
        ]
    )

    baseline = _safe_rate(
        sum(
            1
            for episode
            in history
            if episode.counterpart_next_mode
            == baseline_mode
        ),
        len(
            history
        ),
    )

    return (
        accuracy,
        baseline,
    )


def _permuted_response_accuracy(
    history: tuple[
        EnactedSocialEpisode,
        ...,
    ],
    *,
    seed: int,
) -> float:
    permuted = list(
        history
    )

    indexed_responses = [
        (
            index,
            episode.response_token,
        )
        for index, episode
        in enumerate(
            history
        )
    ]

    indexed_responses.sort(
        key=lambda pair: (
            stable_unit_float(
                "enacted-response-permutation",
                seed,
                pair[
                    0
                ],
                pair[
                    1
                ],
                len(
                    indexed_responses
                ),
            )
        )
    )

    responses = [
        response
        for _index, response
        in indexed_responses
    ]

    response_mode = defaultdict(
        Counter
    )

    for episode, response in zip(
        history,
        responses,
    ):
        response_mode[
            response
        ][
            episode.counterpart_next_mode
        ] += 1

    mapping = {
        response: counts.most_common(
            1
        )[
            0
        ][
            0
        ]
        for response, counts
        in response_mode.items()
        if counts
    }

    predicted = sum(
        1
        for episode, response
        in zip(
            history,
            responses,
        )
        if mapping.get(
            response,
            MODES[
                0
            ],
        )
        == episode.counterpart_next_mode
    )

    return _safe_rate(
        predicted,
        len(
            history
        ),
    )


def build_structural_social_evidence(
    *,
    history: tuple[
        EnactedSocialEpisode,
        ...,
    ],
    seed: int,
) -> tuple[
    StructuralSocialEvidence,
    ...,
]:
    if len(
        history
    ) < 24:
        raise ValueError(
            "at least 24 enacted episodes are required"
        )

    source_ids = tuple(
        episode.episode_id
        for episode
        in history
    )

    action_gain, matched_rate, unmatched_rate = (
        _cue_conditioned_action_gain(
            history
        )
    )

    response_accuracy, response_baseline = (
        _prediction_accuracy(
            history
        )
    )

    permuted_accuracy = (
        _permuted_response_accuracy(
            history,
            seed=seed,
        )
    )

    contingency_gain = (
        response_accuracy
        - max(
            response_baseline,
            permuted_accuracy,
        )
    )

    early = history[
        : len(
            history
        ) // 2
    ]
    late = history[
        len(
            history
        ) // 2 :
    ]

    early_success = _safe_rate(
        sum(
            episode.outcome
            for episode
            in early
        ),
        len(
            early
        ),
    )

    late_success = _safe_rate(
        sum(
            episode.outcome
            for episode
            in late
        ),
        len(
            late
        ),
    )

    learner_gain = (
        late_success
        - early_success
    )

    counterpart_count = len({
        episode.counterpart_id
        for episode
        in history
    })

    evidence = [
        StructuralSocialEvidence(
            evidence_id=(
                "SE-"
                + canonical_hash({
                    "kind": (
                        "cue-conditioned-actionability"
                    ),
                    "seed": (
                        seed
                    ),
                    "sources": (
                        source_ids
                    ),
                })[
                    :12
                ]
            ),
            source_episode_ids=(
                source_ids
            ),
            statistic_name=(
                "cue_conditioned_action_gain"
            ),
            statistic_value=(
                action_gain
            ),
            summary=(
                f"Across {len(history)} ordered interactions, the empirically best "
                f"action token for each observed cue token produced outcome=1 at "
                f"rate {matched_rate:.3f}; other action tokens produced outcome=1 "
                f"at rate {unmatched_rate:.3f}. Difference={action_gain:.3f}."
            ),
        ),
        StructuralSocialEvidence(
            evidence_id=(
                "SE-"
                + canonical_hash({
                    "kind": (
                        "response-next-mode-contingency"
                    ),
                    "seed": (
                        seed
                    ),
                    "sources": (
                        source_ids
                    ),
                })[
                    :12
                ]
            ),
            source_episode_ids=(
                source_ids
            ),
            statistic_name=(
                "response_next_mode_contingency_gain"
            ),
            statistic_value=(
                contingency_gain
            ),
            summary=(
                f"Predicting the counterpart's next opaque mode from the focal "
                f"entity's immediately preceding response token achieved accuracy "
                f"{response_accuracy:.3f}. Marginal-mode baseline={response_baseline:.3f}; "
                f"deterministic response-permutation control={permuted_accuracy:.3f}; "
                f"conservative contingency gain={contingency_gain:.3f}."
            ),
        ),
        StructuralSocialEvidence(
            evidence_id=(
                "SE-"
                + canonical_hash({
                    "kind": (
                        "ordered-learning-change"
                    ),
                    "seed": (
                        seed
                    ),
                    "sources": (
                        source_ids
                    ),
                })[
                    :12
                ]
            ),
            source_episode_ids=(
                source_ids
            ),
            statistic_name=(
                "late_minus_early_outcome_rate"
            ),
            statistic_value=(
                learner_gain
            ),
            summary=(
                f"The focal entity selected actions through an adaptive cue-conditioned "
                f"learner. Outcome=1 rate changed from {early_success:.3f} in the first "
                f"half of the ordered history to {late_success:.3f} in the second half; "
                f"difference={learner_gain:.3f}."
            ),
        ),
        StructuralSocialEvidence(
            evidence_id=(
                "SE-"
                + canonical_hash({
                    "kind": (
                        "counterpart-diversity"
                    ),
                    "seed": (
                        seed
                    ),
                    "sources": (
                        source_ids
                    ),
                })[
                    :12
                ]
            ),
            source_episode_ids=(
                source_ids
            ),
            statistic_name=(
                "distinct_counterpart_count"
            ),
            statistic_value=float(
                counterpart_count
            ),
            summary=(
                f"The ordered interaction history contains {counterpart_count} distinct "
                f"opaque counterpart identifiers across {len(history)} episodes."
            ),
        ),
    ]

    return tuple(
        evidence
    )
