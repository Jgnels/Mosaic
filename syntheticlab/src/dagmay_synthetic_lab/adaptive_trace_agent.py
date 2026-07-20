from __future__ import annotations

from dataclasses import dataclass, asdict
from collections import defaultdict
from typing import Dict
import copy
import math

from .core import canonical_hash, stable_unit_float
from .relationships import DirectedRelationshipModel, SocialEvidence
from .rich_world import (
    RichObservation,
    SubjectVisibleRichEvent,
    MOVE_LEFT,
    MOVE_RIGHT,
    REST,
    REPAIR,
    ASK_PREFIX,
    HELP_PREFIX,
)


@dataclass
class RecencyEstimate:
    value: float = .50
    effective_count: float = 0.0
    recent_prediction_error: float = .50
    learning_progress: float = 0.0

    def observe(self, outcome: int, alpha: float = .18):
        error_before = abs(outcome - self.value)
        prior_error = self.recent_prediction_error

        self.value = (
            (1.0 - alpha) * self.value
            + alpha * float(outcome)
        )
        self.effective_count = min(
            30.0,
            .965 * self.effective_count + 1.0,
        )
        self.recent_prediction_error = (
            .80 * self.recent_prediction_error
            + .20 * error_before
        )
        self.learning_progress = max(
            0.0,
            prior_error - self.recent_prediction_error,
        )

    def uncertainty(self):
        return 1.0 / math.sqrt(self.effective_count + 1.0)

    def to_dict(self):
        return asdict(self)


@dataclass
class RecencyReliability:
    value: float = .50
    effective_count: float = 0.0

    def observe(self, supported: int, alpha: float = .16):
        self.value = (
            (1.0 - alpha) * self.value
            + alpha * float(supported)
        )
        self.effective_count = min(
            30.0,
            .96 * self.effective_count + 1.0,
        )

    def to_dict(self):
        return asdict(self)


@dataclass
class AdaptivePendingHint:
    source_id: str
    action: str
    zone_token: str
    regime_cue: str
    issued_step: int

    def to_dict(self):
        return asdict(self)


class AdaptiveTraceAgent:
    """Order-sensitive, recency-weighted developmental substrate prototype.

    Unlike the Beta-count learner, this substrate intentionally forgets some old
    evidence so it can adapt when the same observable cue later means something
    different.

    Context includes the noisy local resource signal. That lets the policy learn
    that a previously productive zone can become temporarily depleted.

    This is not a neural network and not claimed to model infant learning. It is
    a controlled intermediate baseline testing whether path-dependent continual
    state improves adaptation in a nonstationary world.
    """

    def __init__(
        self,
        agent_id: str,
        config,
        seed: int,
        alpha: float = .25,
    ):
        self.agent_id = agent_id
        self.config = config
        self.seed = seed
        self.alpha = alpha

        self.estimates: Dict[
            tuple[str, str, str, str],
            RecencyEstimate,
        ] = {}
        self.base_estimates: Dict[
            tuple[str, str, str],
            RecencyEstimate,
        ] = {}

        for cue in ("RC0", "RC1", "RC2", "RC3"):
            for zone in config.zones:
                for action in config.interactions:
                    self.base_estimates[
                        (cue, zone, action)
                    ] = RecencyEstimate()

        for cue in ("RC0", "RC1", "RC2", "RC3"):
            for zone in config.zones:
                for signal in ("LS0", "LS1", "LS2", "LS3", "LS4"):
                    for action in config.interactions:
                        self.estimates[
                            (cue, zone, signal, action)
                        ] = RecencyEstimate()

        self.partner_reliability = {
            pid: RecencyReliability()
            for pid in config.partner_ids
        }
        self.relationships = {
            pid: DirectedRelationshipModel(agent_id, pid)
            for pid in config.partner_ids
        }

        self.pending_hint: AdaptivePendingHint | None = None
        self.zone_last_visit = {
            zone: -10_000 for zone in config.zones
        }
        self.action_counts = defaultdict(int)
        self.memory_ids: list[str] = []
        self.significant_memory_ids: list[str] = []
        self.help_given_recently = {
            pid: -10_000 for pid in config.partner_ids
        }

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self):
        return canonical_hash(self.to_dict())

    def _key(self, obs: RichObservation, action: str):
        return (
            obs.regime_cue,
            obs.zone_token,
            obs.local_signal,
            action,
        )

    def estimate(self, obs: RichObservation, action: str):
        return self.estimates[self._key(obs, action)]

    def base_estimate(self, obs: RichObservation, action: str):
        return self.base_estimates[
            (obs.regime_cue, obs.zone_token, action)
        ]

    def blended_value(self, obs: RichObservation, action: str):
        base = self.base_estimate(obs, action)
        local = self.estimate(obs, action)
        return .72 * base.value + .28 * local.value

    def blended_uncertainty(self, obs: RichObservation, action: str):
        base = self.base_estimate(obs, action)
        local = self.estimate(obs, action)
        return .65 * base.uncertainty() + .35 * local.uncertainty()

    def _best_action(self, obs: RichObservation):
        energy_need = {
            "B0": 1.0,
            "B1": .75,
            "B2": .40,
            "B3": .15,
        }[obs.energy_band]

        def score(action):
            est = self.estimate(obs, action)
            base = self.base_estimate(obs, action)
            exploitation = energy_need * self.blended_value(obs, action)
            epistemic = .28 * self.blended_uncertainty(obs, action)
            competence = .16 * est.learning_progress + .10 * base.learning_progress
            return (
                exploitation
                + epistemic
                + competence
                + 1e-9 * stable_unit_float(
                    "adaptive-tie",
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

    def _best_expected_action(self, obs):
        return max(
            sorted(self.config.interactions),
            key=lambda a: self.blended_value(obs, a),
        )

    def _credible_hint(self, obs):
        if self.pending_hint is None:
            return None
        hint = self.pending_hint
        if obs.step - hint.issued_step > 5:
            return None
        if (
            hint.zone_token != obs.zone_token
            or hint.regime_cue != obs.regime_cue
        ):
            return None
        if self.partner_reliability[hint.source_id].value >= .58:
            return hint.action
        return None

    def choose_action(self, obs: RichObservation):
        self.zone_last_visit[obs.zone_token] = obs.step

        if obs.integrity_band == "B0":
            action = REPAIR
        elif (
            obs.integrity_band == "B1"
            and obs.energy_band in {"B2", "B3"}
            and stable_unit_float(
                "adaptive-repair",
                self.seed,
                obs.step,
            ) < .45
        ):
            action = REPAIR
        else:
            hint = self._credible_hint(obs)

            if obs.energy_band == "B0":
                action = hint or self._best_expected_action(obs)

            elif (
                obs.local_signal == "LS0"
                and obs.energy_band in {"B2", "B3"}
            ):
                # Depletion-aware exploration: move toward the less recently
                # visited adjacent zone.
                idx = self.config.zones.index(obs.zone_token)
                left = self.config.zones[
                    (idx - 1) % len(self.config.zones)
                ]
                right = self.config.zones[
                    (idx + 1) % len(self.config.zones)
                ]
                action = (
                    MOVE_LEFT
                    if self.zone_last_visit[left]
                    <= self.zone_last_visit[right]
                    else MOVE_RIGHT
                )

            elif hint is not None and obs.energy_band in {"B0", "B1"}:
                action = hint

            elif obs.help_request_from is not None and obs.energy_band == "B3":
                pid = obs.help_request_from
                rel = self.relationships[pid].compute()
                social_score = (
                    .55 * max(0.0, rel.trust)
                    + .25 * rel.familiarity
                    + .20 * self.partner_reliability[pid].value
                )
                if social_score >= .34:
                    action = HELP_PREFIX + pid
                else:
                    action = self._best_action(obs)

            else:
                uncertainties = {
                    a: self.blended_uncertainty(obs, a)
                    for a in self.config.interactions
                }
                max_uncertainty = max(uncertainties.values())

                if (
                    obs.available_partners
                    and max_uncertainty > .58
                    and stable_unit_float(
                        "adaptive-ask",
                        self.seed,
                        obs.step,
                    ) < .18
                ):
                    partner = max(
                        sorted(obs.available_partners),
                        key=lambda p: self.partner_reliability[p].value,
                    )
                    action = ASK_PREFIX + partner
                else:
                    action = self._best_action(obs)

        self.action_counts[action] += 1
        return action

    def observe_event(
        self,
        obs: RichObservation,
        event: SubjectVisibleRichEvent,
    ):
        if event.action in self.config.interactions:
            est = self.estimate(obs, event.action)
            base = self.base_estimate(obs, event.action)
            est.observe(event.resource_success, self.alpha)
            base.observe(event.resource_success, self.alpha)

            if self.pending_hint is not None:
                hint = self.pending_hint
                if (
                    hint.zone_token == obs.zone_token
                    and hint.regime_cue == obs.regime_cue
                    and obs.step - hint.issued_step <= 5
                ):
                    supported = (
                        event.resource_success
                        if event.action == hint.action
                        else int(not event.resource_success)
                    )
                    self.partner_reliability[
                        hint.source_id
                    ].observe(supported)

                    rel = self.relationships[hint.source_id]
                    if event.action == hint.action and event.resource_success:
                        rel.add_evidence(SocialEvidence(
                            f"{event.event_id}-ADAPTIVE-HINT-HELP",
                            self.agent_id,
                            hint.source_id,
                            "HELP",
                            .35,
                            .70,
                            obs.step,
                        ))
                    elif event.action == hint.action and not event.resource_success:
                        rel.add_evidence(SocialEvidence(
                            f"{event.event_id}-ADAPTIVE-HINT-FAIL",
                            self.agent_id,
                            hint.source_id,
                            "RUMOR_NEGATIVE",
                            .30,
                            .55,
                            obs.step,
                        ))
                    self.pending_hint = None

        if event.hint_action is not None and event.counterpart is not None:
            self.pending_hint = AdaptivePendingHint(
                event.counterpart,
                event.hint_action,
                obs.zone_token,
                obs.regime_cue,
                obs.step,
            )

        if event.help_given and event.counterpart is not None:
            self.help_given_recently[event.counterpart] = obs.step

        if event.help_received and event.counterpart is not None:
            rel = self.relationships[event.counterpart]
            rel.add_evidence(SocialEvidence(
                f"{event.event_id}-ADAPTIVE-HELP",
                self.agent_id,
                event.counterpart,
                "HELP",
                1.0,
                1.0,
                obs.step,
            ))

        significance = (
            .10
            + .40 * abs(event.energy_after - event.energy_before)
            + .45 * abs(event.integrity_after - event.integrity_before)
            + .20 * int(event.hazard)
            + .15 * int(event.help_received)
            + .12 * int(event.hint_action is not None)
        )
        self.memory_ids.append(event.event_id)
        if significance >= .35:
            self.significant_memory_ids.append(event.event_id)

    def predicted_action(
        self,
        regime_cue: str,
        zone_token: str,
        local_signal: str,
    ):
        class _Obs:
            pass
        obs = _Obs()
        obs.regime_cue = regime_cue
        obs.zone_token = zone_token
        obs.local_signal = local_signal
        return max(
            sorted(self.config.interactions),
            key=lambda a: (
                .72 * self.base_estimates[
                    (regime_cue, zone_token, a)
                ].value
                + .28 * self.estimates[
                    (regime_cue, zone_token, local_signal, a)
                ].value
            ),
        )

    def to_dict(self):
        return {
            "agent_id": self.agent_id,
            "seed": self.seed,
            "alpha": self.alpha,
            "estimates": {
                "|".join(k): v.to_dict()
                for k, v in sorted(self.estimates.items())
            },
            "base_estimates": {
                "|".join(k): v.to_dict()
                for k, v in sorted(self.base_estimates.items())
            },
            "partner_reliability": {
                k: v.to_dict()
                for k, v in sorted(self.partner_reliability.items())
            },
            "relationships": {
                k: v.to_dict()
                for k, v in sorted(self.relationships.items())
            },
            "zone_last_visit": dict(sorted(self.zone_last_visit.items())),
            "action_counts": dict(sorted(self.action_counts.items())),
            "memory_count": len(self.memory_ids),
            "significant_memory_ids": list(self.significant_memory_ids),
            "pending_hint": (
                self.pending_hint.to_dict()
                if self.pending_hint is not None
                else None
            ),
        }


class HardOracleAgent:
    """Positive control with direct access to the hidden current-regime mapping."""

    def __init__(self, agent_id, config, action_resolver):
        self.agent_id = agent_id
        self.config = config
        self.action_resolver = action_resolver
        self.action_counts = defaultdict(int)

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self):
        return canonical_hash({
            "agent_id": self.agent_id,
            "action_counts": dict(sorted(self.action_counts.items())),
        })

    def choose_action(self, obs):
        if obs.integrity_band == "B0":
            action = REPAIR
        elif obs.integrity_band == "B1" and obs.energy_band in {"B2", "B3"}:
            action = REPAIR
        elif obs.local_signal == "LS0" and obs.energy_band in {"B2", "B3"}:
            action = MOVE_RIGHT
        else:
            action = self.action_resolver(obs.step, obs.zone_token)
        self.action_counts[action] += 1
        return action

    def observe_event(self, obs, event):
        del obs, event
