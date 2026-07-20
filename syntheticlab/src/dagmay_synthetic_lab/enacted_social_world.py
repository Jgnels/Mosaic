from __future__ import annotations

from dataclasses import dataclass, asdict
from collections import defaultdict
from typing import Dict, Tuple

from .core import DeterministicRng, canonical_hash, stable_unit_float


REGIMES = (
    "RECIPROCAL_CONTINGENT",
    "ONE_WAY_ASSISTANCE",
    "NONCONTINGENT_SIGNALS",
)

CONTEXTS = ("X0", "X1", "X2", "X3")
ACTIONS = ("A0", "A1", "A2", "A3")
CUES = ("Q0", "Q1", "Q2", "Q3")
RESPONSES = ("R0", "R1", "R2")
MODES = ("M0", "M1", "M2")


@dataclass(frozen=True)
class EnactedSocialEpisode:
    episode_id: str
    step: int
    counterpart_id: str
    context_token: str
    cue_token: str
    chosen_action: str
    outcome: int
    response_token: str
    counterpart_next_mode: str

    def to_dict(self):
        return asdict(self)


class CueActionLearner:
    """Simple local learner whose behavior changes from ordered experience."""

    def __init__(
        self,
        *,
        seed: int,
        epsilon: float = 0.25,
        learning_rate: float = 0.28,
    ):
        self.seed = seed
        self.epsilon = epsilon
        self.learning_rate = learning_rate
        self._values: Dict[Tuple[str, str], float] = defaultdict(float)
        self._visits: Dict[Tuple[str, str], int] = defaultdict(int)

    def choose_action(
        self,
        *,
        cue: str,
        step: int,
    ) -> str:
        explore = (
            stable_unit_float(
                "enacted-social-explore",
                self.seed,
                cue,
                step,
            )
            < self.epsilon
        )

        if explore:
            index = int(
                stable_unit_float(
                    "enacted-social-random-action",
                    self.seed,
                    cue,
                    step,
                )
                * len(ACTIONS)
            ) % len(ACTIONS)
            return ACTIONS[index]

        ranked = sorted(
            ACTIONS,
            key=lambda action: (
                self._values[(cue, action)],
                -ACTIONS.index(action),
            ),
            reverse=True,
        )
        return ranked[0]

    def update(
        self,
        *,
        cue: str,
        action: str,
        outcome: int,
    ):
        key = (
            cue,
            action,
        )
        self._visits[key] += 1
        current = self._values[key]
        self._values[key] = (
            current
            + self.learning_rate
            * (
                float(outcome)
                - current
            )
        )

    def value_table(self) -> dict:
        return {
            f"{cue}|{action}": self._values[(cue, action)]
            for cue in CUES
            for action in ACTIONS
        }


class EnactedSocialWorld:
    """Opaque social microenvironment with real ordered action/consequence loops.

    The regime controls hidden causal structure. The focal learner is never handed the
    regime label, an explicit "best action", or help/reciprocity booleans.
    """

    def __init__(
        self,
        *,
        seed: int,
        regime: str,
        cue_noise: float = 0.12,
        outcome_noise: float = 0.08,
        reciprocity_noise: float = 0.08,
    ):
        if regime not in REGIMES:
            raise ValueError(
                "unsupported enacted social regime"
            )

        self.seed = seed
        self.regime = regime
        self.cue_noise = cue_noise
        self.outcome_noise = outcome_noise
        self.reciprocity_noise = reciprocity_noise

        rng = DeterministicRng(seed)

        actions = list(ACTIONS)
        self.optimal_action_by_context = {}
        for context in CONTEXTS:
            rng.shuffle(actions)
            self.optimal_action_by_context[
                context
            ] = actions[0]

        cues = list(CUES)
        rng.shuffle(cues)
        self.cue_for_action = {
            action: cue
            for action, cue
            in zip(
                ACTIONS,
                cues,
            )
        }

        self.action_for_cue = {
            cue: action
            for action, cue
            in self.cue_for_action.items()
        }

    def _context(
        self,
        step: int,
    ) -> str:
        index = int(
            stable_unit_float(
                "enacted-context",
                self.seed,
                step,
            )
            * len(CONTEXTS)
        ) % len(CONTEXTS)

        return CONTEXTS[index]

    def _counterpart(
        self,
        step: int,
    ) -> str:
        return (
            "K"
            + str(
                1
                + step % 3
            )
        )

    def _cue(
        self,
        *,
        context: str,
        step: int,
    ) -> str:
        informative = (
            self.regime
            in {
                "RECIPROCAL_CONTINGENT",
                "ONE_WAY_ASSISTANCE",
            }
        )

        if informative:
            optimal = (
                self.optimal_action_by_context[
                    context
                ]
            )

            if (
                stable_unit_float(
                    "enacted-cue-noise",
                    self.seed,
                    self.regime,
                    step,
                )
                >= self.cue_noise
            ):
                return self.cue_for_action[
                    optimal
                ]

        index = int(
            stable_unit_float(
                "enacted-random-cue",
                self.seed,
                self.regime,
                step,
            )
            * len(CUES)
        ) % len(CUES)

        return CUES[index]

    def _outcome(
        self,
        *,
        context: str,
        action: str,
        step: int,
    ) -> int:
        optimal = (
            action
            == self.optimal_action_by_context[
                context
            ]
        )

        probability = (
            1.0
            - self.outcome_noise
            if optimal
            else self.outcome_noise
        )

        return int(
            stable_unit_float(
                "enacted-outcome",
                self.seed,
                context,
                action,
                step,
            )
            < probability
        )

    def _response(
        self,
        *,
        cue: str,
        action: str,
        outcome: int,
        step: int,
    ) -> str:
        index = (
            ACTIONS.index(
                action
            )
            + CUES.index(
                cue
            )
            + outcome
            + step
        ) % len(RESPONSES)

        return RESPONSES[
            index
        ]

    def _next_mode(
        self,
        *,
        response: str,
        step: int,
    ) -> str:
        if (
            self.regime
            == "RECIPROCAL_CONTINGENT"
            and stable_unit_float(
                "enacted-reciprocity-noise",
                self.seed,
                step,
            )
            >= self.reciprocity_noise
        ):
            return MODES[
                RESPONSES.index(
                    response
                )
            ]

        index = int(
            stable_unit_float(
                "enacted-next-mode",
                self.seed,
                self.regime,
                step,
            )
            * len(MODES)
        ) % len(MODES)

        return MODES[
            index
        ]

    def run(
        self,
        *,
        episodes: int,
        learner: CueActionLearner,
    ) -> tuple[
        EnactedSocialEpisode,
        ...,
    ]:
        history = []

        for step in range(
            episodes
        ):
            context = self._context(
                step
            )
            counterpart = (
                self._counterpart(
                    step
                )
            )
            cue = self._cue(
                context=context,
                step=step,
            )
            action = (
                learner.choose_action(
                    cue=cue,
                    step=step,
                )
            )
            outcome = self._outcome(
                context=context,
                action=action,
                step=step,
            )
            response = self._response(
                cue=cue,
                action=action,
                outcome=outcome,
                step=step,
            )
            next_mode = self._next_mode(
                response=response,
                step=step,
            )

            learner.update(
                cue=cue,
                action=action,
                outcome=outcome,
            )

            history.append(
                EnactedSocialEpisode(
                    episode_id=(
                        "ES-"
                        + canonical_hash({
                            "seed": (
                                self.seed
                            ),
                            "regime": (
                                self.regime
                            ),
                            "step": (
                                step
                            ),
                            "counterpart": (
                                counterpart
                            ),
                            "context": (
                                context
                            ),
                            "cue": (
                                cue
                            ),
                            "action": (
                                action
                            ),
                            "outcome": (
                                outcome
                            ),
                            "response": (
                                response
                            ),
                            "mode": (
                                next_mode
                            ),
                        })[
                            :16
                        ]
                    ),
                    step=step,
                    counterpart_id=(
                        counterpart
                    ),
                    context_token=(
                        context
                    ),
                    cue_token=(
                        cue
                    ),
                    chosen_action=(
                        action
                    ),
                    outcome=(
                        outcome
                    ),
                    response_token=(
                        response
                    ),
                    counterpart_next_mode=(
                        next_mode
                    ),
                )
            )

        return tuple(
            history
        )
