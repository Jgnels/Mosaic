from __future__ import annotations

from dataclasses import dataclass, asdict

from .core import canonical_hash, stable_unit_float


REGIMES = (
    "RECIPROCAL_CONTINGENT",
    "ONE_WAY_ASSISTANCE",
    "NONCONTINGENT_SIGNALS",
)


@dataclass(frozen=True)
class SocialChallengeEpisode:
    evidence_id: str
    counterpart_id: str
    cue_token: str
    suggested_action: str
    best_action: str
    focal_response_token: str
    counterpart_next_mode: str
    help_received: bool
    help_given: bool
    outcome_positive: bool

    def to_dict(self):
        return asdict(self)


def _token(
    prefix: str,
    *,
    regime: str,
    episode: int,
    salt: str,
    modulo: int,
) -> str:
    index = int(
        stable_unit_float(
            prefix,
            regime,
            episode,
            salt,
        )
        * modulo
    ) % modulo

    return (
        f"{prefix}{index}"
    )


def build_social_challenge_evidence(
    *,
    regime: str,
    episode_count: int = 12,
) -> tuple[
    SocialChallengeEpisode,
    ...,
]:
    if regime not in REGIMES:
        raise ValueError(
            "unsupported challenge regime"
        )

    episodes = []

    for index in range(
        episode_count
    ):
        counterpart = (
            f"K{index % 3 + 1}"
        )

        best_action = _token(
            "A",
            regime=regime,
            episode=index,
            salt="best",
            modulo=4,
        )

        response = _token(
            "R",
            regime=regime,
            episode=index,
            salt="response",
            modulo=3,
        )

        cue = _token(
            "C",
            regime=regime,
            episode=index,
            salt="cue",
            modulo=4,
        )

        if regime == (
            "RECIPROCAL_CONTINGENT"
        ):
            # Mostly accurate external guidance. Counterpart's later mode is
            # contingent on the focal response, and help flows both directions.
            accurate = (
                index
                not in {
                    4,
                    9,
                }
            )

            suggested = (
                best_action
                if accurate
                else f"A{(
                    int(
                        best_action[
                            1:
                        ]
                    )
                    + 1
                ) % 4}"
            )

            next_mode = (
                f"M-{response}"
            )

            help_received = (
                index % 3
                != 2
            )

            help_given = (
                index % 4
                != 3
            )

        elif regime == (
            "ONE_WAY_ASSISTANCE"
        ):
            # Guidance is usually actionable and assistance is received from
            # several counterparts, but the focal response has no effect on the
            # counterpart's later behavior and aid is not reciprocated.
            accurate = (
                index
                not in {
                    5,
                    10,
                }
            )

            suggested = (
                best_action
                if accurate
                else f"A{(
                    int(
                        best_action[
                            1:
                        ]
                    )
                    + 2
                ) % 4}"
            )

            next_mode = (
                f"M{index % 2}"
            )

            help_received = (
                True
            )

            help_given = (
                False
            )

        else:
            # Cues exist, but their suggested actions are deliberately near chance
            # and later counterpart behavior is independent of the focal response.
            suggested = (
                f"A{(
                    int(
                        best_action[
                            1:
                        ]
                    )
                    + (
                        1
                        + index % 3
                    )
                ) % 4}"
                if index
                not in {
                    2,
                    7,
                    11,
                }
                else best_action
            )

            next_mode = (
                f"M{(
                    index
                    * 2
                    + 1
                ) % 3}"
            )

            help_received = (
                index
                in {
                    1,
                    8,
                }
            )

            help_given = (
                index
                in {
                    3,
                    9,
                }
            )

        outcome_positive = (
            suggested
            == best_action
        )

        evidence_id = (
            "CH-"
            + canonical_hash({
                "regime": (
                    regime
                ),
                "episode": (
                    index
                ),
                "counterpart": (
                    counterpart
                ),
                "cue": (
                    cue
                ),
                "suggested": (
                    suggested
                ),
                "best": (
                    best_action
                ),
                "response": (
                    response
                ),
                "next_mode": (
                    next_mode
                ),
                "help_received": (
                    help_received
                ),
                "help_given": (
                    help_given
                ),
            })[
                :12
            ]
        )

        episodes.append(
            SocialChallengeEpisode(
                evidence_id=(
                    evidence_id
                ),
                counterpart_id=(
                    counterpart
                ),
                cue_token=(
                    cue
                ),
                suggested_action=(
                    suggested
                ),
                best_action=(
                    best_action
                ),
                focal_response_token=(
                    response
                ),
                counterpart_next_mode=(
                    next_mode
                ),
                help_received=(
                    help_received
                ),
                help_given=(
                    help_given
                ),
                outcome_positive=(
                    outcome_positive
                ),
            )
        )

    return tuple(
        episodes
    )


def neutral_episode_summary(
    episode: SocialChallengeEpisode,
) -> str:
    return (
        f"Counterpart {episode.counterpart_id} emitted cue {episode.cue_token} "
        f"associated with suggested action {episode.suggested_action}. "
        f"The independently recorded best action was {episode.best_action}. "
        f"The focal entity emitted response token {episode.focal_response_token}. "
        f"The counterpart's next recorded mode was {episode.counterpart_next_mode}. "
        f"help_received={str(episode.help_received).lower()}; "
        f"help_given={str(episode.help_given).lower()}; "
        f"positive_outcome={str(episode.outcome_positive).lower()}."
    )


def regime_diagnostics(
    episodes: tuple[
        SocialChallengeEpisode,
        ...,
    ],
) -> dict:
    count = len(
        episodes
    )

    accuracy = (
        sum(
            1
            for episode
            in episodes
            if episode.suggested_action
            == episode.best_action
        )
        / count
    )

    help_received = (
        sum(
            1
            for episode
            in episodes
            if episode.help_received
        )
        / count
    )

    help_given = (
        sum(
            1
            for episode
            in episodes
            if episode.help_given
        )
        / count
    )

    response_contingency = (
        len({
            (
                episode.focal_response_token,
                episode.counterpart_next_mode,
            )
            for episode
            in episodes
        })
    )

    return {
        "episode_count": (
            count
        ),
        "guidance_accuracy": (
            accuracy
        ),
        "help_received_rate": (
            help_received
        ),
        "help_given_rate": (
            help_given
        ),
        "response_next_mode_pair_count": (
            response_contingency
        ),
        "counterpart_count": len({
            episode.counterpart_id
            for episode
            in episodes
        }),
    }
