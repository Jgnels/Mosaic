from __future__ import annotations

import math

from .adaptive_trace_agent import AdaptiveTraceAgent
from .core import stable_unit_float


class MetaAdaptiveAgent(AdaptiveTraceAgent):
    """Adaptive substrate with a transferable volatility/learning-rate prior.

    World-specific causal estimates are still reset on transfer.

    What can transfer:
    - estimated environmental volatility;
    - a learned prior over how quickly recent evidence should replace old evidence.

    What does NOT transfer:
    - zone/action causal mappings;
    - partner identities or reliability;
    - relationship states;
    - autobiographical memories.

    This isolates a weak form of "learning how quickly to learn" from copying
    semantic answers between worlds.
    """

    def __init__(
        self,
        agent_id,
        config,
        seed,
        volatility_prior: float = .22,
    ):
        super().__init__(
            agent_id,
            config,
            seed,
            alpha=self._alpha_from_volatility(volatility_prior),
        )
        self.volatility = volatility_prior
        self.error_ema = .30
        self.meta_observations = 0

    @staticmethod
    def _alpha_from_volatility(volatility: float) -> float:
        return min(
            .42,
            max(
                .08,
                .08 + .42 * volatility,
            ),
        )

    def _best_action(self, obs):
        energy_need = {
            "B0": 1.0,
            "B1": .75,
            "B2": .40,
            "B3": .15,
        }[obs.energy_band]

        # High-volatility history increases the value of current uncertainty
        # and recent learning progress.
        uncertainty_weight = .20 + .24 * self.volatility
        progress_weight = .12 + .20 * self.volatility

        def score(action):
            est = self.estimate(obs, action)
            base = self.base_estimate(obs, action)
            exploitation = energy_need * self.blended_value(obs, action)
            epistemic = (
                uncertainty_weight
                * self.blended_uncertainty(obs, action)
            )
            competence = progress_weight * (
                .60 * est.learning_progress
                + .40 * base.learning_progress
            )
            return (
                exploitation
                + epistemic
                + competence
                + 1e-9 * stable_unit_float(
                    "meta-adaptive-tie",
                    self.seed,
                    obs.step,
                    obs.zone_token,
                    obs.local_signal,
                    action,
                )
            )

        return max(
            sorted(self.config.interactions),
            key=score,
        )

    def observe_event(self, obs, event):
        if event.action in self.config.interactions:
            prediction = self.blended_value(
                obs,
                event.action,
            )
            error = abs(
                event.resource_success - prediction
            )

            previous_error = self.error_ema
            self.error_ema = (
                .94 * self.error_ema
                + .06 * error
            )

            # Volatility rises when current errors remain unexpectedly high and
            # falls when prediction becomes consistently stable.
            surprise_change = max(
                0.0,
                error - previous_error,
            )
            stability_signal = max(
                0.0,
                previous_error - error,
            )
            self.volatility = min(
                1.0,
                max(
                    .02,
                    .97 * self.volatility
                    + .09 * surprise_change
                    - .025 * stability_signal,
                ),
            )
            self.alpha = self._alpha_from_volatility(
                self.volatility
            )
            self.meta_observations += 1

        super().observe_event(obs, event)

    def spawn_for_new_world(
        self,
        new_agent_id,
        new_config,
        new_seed,
    ):
        child = MetaAdaptiveAgent(
            new_agent_id,
            new_config,
            new_seed,
            volatility_prior=self.volatility,
        )
        child.error_ema = self.error_ema
        child.meta_observations = self.meta_observations
        return child

    def to_dict(self):
        base = super().to_dict()
        return {
            **base,
            "type": "MetaAdaptiveAgent",
            "volatility": self.volatility,
            "error_ema": self.error_ema,
            "meta_observations": self.meta_observations,
        }
