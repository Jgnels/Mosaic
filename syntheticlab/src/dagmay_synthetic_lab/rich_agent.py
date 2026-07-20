from __future__ import annotations

from dataclasses import dataclass, asdict
from collections import defaultdict
from typing import Dict, Iterable
import copy
import math

from .core import canonical_hash, stable_unit_float
from .learners import BinomialEvidence
from .relationships import DirectedRelationshipModel, SocialEvidence
from .drives import get_profile
from .rich_world import (
    RichObservation,
    SubjectVisibleRichEvent,
    RichWorldConfig,
    MOVE_LEFT,
    MOVE_RIGHT,
    OBSERVE,
    REST,
    REPAIR,
    ASK_PREFIX,
    HELP_PREFIX,
)


@dataclass(frozen=True)
class SubjectiveRichExperience:
    experience_id: str
    step: int
    zone_token: str
    regime_cue: str
    energy_band: str
    integrity_band: str
    action: str
    resource_success: int
    hazard: int
    counterpart: str | None
    hint_action_received: str | None
    help_given: bool
    help_received: bool
    energy_delta: float
    integrity_delta: float
    significance: float

    def to_dict(self):
        return asdict(self)


@dataclass
class PendingHint:
    source_id: str
    action: str
    zone_token: str
    regime_cue: str
    issued_step: int

    def to_dict(self):
        return asdict(self)


def _drive_weights(profile_id: str) -> dict[str, float]:
    result = {
        "homeostasis": 0.0,
        "uncertainty": 0.0,
        "competence": 0.0,
    }
    if profile_id == "NONE":
        return result

    for drive in get_profile(profile_id).drives:
        if drive.drive_id == "HOMEOSTATIC_REGULATION":
            result["homeostasis"] = drive.initial_weight
        elif drive.drive_id == "UNCERTAINTY_REDUCTION":
            result["uncertainty"] = drive.initial_weight
        elif drive.drive_id == "COMPETENCE_PROGRESS":
            result["competence"] = drive.initial_weight
    return result


class RichDevelopmentalAgent:
    """History-sensitive developmental baseline for ControlledRichWorld.

    It learns:
    - resource action contingencies by opaque zone and regime cue;
    - partner hint reliability from later consequences;
    - directed relationship evidence from received help and harmful outcomes;
    - simple spatial exploration preferences from visit history.

    It does not receive hidden world regimes, hidden causal mappings, or partner
    profile parameters.
    """

    def __init__(
        self,
        agent_id: str,
        config: RichWorldConfig,
        profile_id: str = "BALANCED_MINIMAL",
        seed: int = 1,
    ):
        self.agent_id = agent_id
        self.config = config
        self.profile_id = profile_id
        self.seed = seed

        self.resource_stats: Dict[tuple[str, str, str], BinomialEvidence] = {}
        self.learning_progress: Dict[tuple[str, str, str], float] = defaultdict(float)
        for cue in ("RC0", "RC1", "RC2", "RC3"):
            for zone in config.zones:
                for action in config.interactions:
                    self.resource_stats[(cue, zone, action)] = BinomialEvidence()

        self.partner_reliability = {
            pid: BinomialEvidence() for pid in config.partner_ids
        }
        self.partner_helpfulness = {
            pid: BinomialEvidence() for pid in config.partner_ids
        }
        self.relationships = {
            pid: DirectedRelationshipModel(agent_id, pid)
            for pid in config.partner_ids
        }

        self.zone_visits = {z: 0 for z in config.zones}
        self.action_counts: Dict[str, int] = defaultdict(int)
        self.memories: list[SubjectiveRichExperience] = []
        self.significant_memory_ids: list[str] = []

        self.pending_hint: PendingHint | None = None
        self.last_action: str | None = None
        self.last_observation: RichObservation | None = None
        self.help_given_recently: Dict[str, int] = {
            pid: -10_000 for pid in config.partner_ids
        }

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self) -> str:
        return canonical_hash(self.to_dict())

    def context_key(
        self,
        obs: RichObservation,
        action: str,
    ) -> tuple[str, str, str]:
        return (obs.regime_cue, obs.zone_token, action)

    def p_success(self, obs: RichObservation, action: str) -> float:
        return self.resource_stats[self.context_key(obs, action)].posterior_mean

    def uncertainty(self, obs: RichObservation, action: str) -> float:
        stat = self.resource_stats[self.context_key(obs, action)]
        return 1.0 / math.sqrt(stat.trials + 1.0)

    def progress(self, obs: RichObservation, action: str) -> float:
        return self.learning_progress[self.context_key(obs, action)]

    def partner_reliability_mean(self, partner_id: str) -> float:
        return self.partner_reliability[partner_id].posterior_mean

    def partner_helpfulness_mean(self, partner_id: str) -> float:
        return self.partner_helpfulness[partner_id].posterior_mean

    def _resource_score(
        self,
        obs: RichObservation,
        action: str,
    ) -> float:
        weights = _drive_weights(self.profile_id)
        p = self.p_success(obs, action)
        uncertainty = self.uncertainty(obs, action)
        progress = self.progress(obs, action)

        energy_need = {
            "B0": 1.0,
            "B1": .72,
            "B2": .38,
            "B3": .12,
        }[obs.energy_band]

        score = (
            weights["homeostasis"] * energy_need * p
            + weights["uncertainty"] * uncertainty
            + weights["competence"] * (progress + .20 * uncertainty)
        )

        # Deterministic microscopic tie-break.
        score += 1e-9 * stable_unit_float(
            "rich-agent-tie",
            self.seed,
            self.agent_id,
            obs.step,
            obs.zone_token,
            action,
        )
        return score

    def _best_resource_action(self, obs: RichObservation) -> str:
        return max(
            sorted(self.config.interactions),
            key=lambda a: self._resource_score(obs, a),
        )

    def _best_expected_resource_action(self, obs: RichObservation) -> str:
        return max(
            sorted(self.config.interactions),
            key=lambda a: self.p_success(obs, a),
        )

    def _should_follow_hint(self, obs: RichObservation) -> str | None:
        hint = self.pending_hint
        if hint is None:
            return None

        if obs.step - hint.issued_step > 5:
            return None

        if hint.zone_token != obs.zone_token or hint.regime_cue != obs.regime_cue:
            return None

        reliability = self.partner_reliability_mean(hint.source_id)
        # Before sufficient evidence, hints remain suggestions rather than commands.
        if reliability >= .58:
            return hint.action
        return None

    def choose_action(self, obs: RichObservation) -> str:
        self.last_observation = obs
        self.zone_visits[obs.zone_token] += 1

        # Integrity is a second homeostatic variable. Repair is available, but
        # costs energy, creating a genuine competing-needs tradeoff.
        if obs.integrity_band == "B0":
            action = REPAIR
            self.last_action = action
            self.action_counts[action] += 1
            return action

        if (
            obs.integrity_band == "B1"
            and obs.energy_band in {"B2", "B3"}
            and stable_unit_float(
                "repair-gate",
                self.seed,
                obs.step,
                obs.zone_token,
            ) < .45
        ):
            action = REPAIR
            self.last_action = action
            self.action_counts[action] += 1
            return action

        # Immediate help request: help only when affordable and either the partner
        # has shown reciprocity or the decision has epistemic value.
        if (
            obs.help_request_from is not None
            and obs.energy_band in {"B2", "B3"}
        ):
            pid = obs.help_request_from
            helpfulness = self.partner_helpfulness_mean(pid)
            relationship = self.relationships[pid].compute()
            social_value = (
                .45 * helpfulness
                + .25 * max(0.0, relationship.trust)
                + .20 * relationship.familiarity
                + .10 * self.partner_reliability_mean(pid)
            )
            if social_value >= .43:
                action = HELP_PREFIX + pid
                self.last_action = action
                self.action_counts[action] += 1
                return action

        # Low energy favors immediate exploitation.
        if obs.energy_band == "B0":
            hinted = self._should_follow_hint(obs)
            action = hinted or self._best_expected_resource_action(obs)
            self.last_action = action
            self.action_counts[action] += 1
            return action

        # A credible recent hint can influence action without replacing learning.
        hinted = self._should_follow_hint(obs)
        if hinted is not None and obs.energy_band in {"B0", "B1"}:
            self.last_action = hinted
            self.action_counts[hinted] += 1
            return hinted

        # Ask when uncertainty is high and a reasonably reliable partner is present.
        if obs.available_partners and obs.energy_band != "B0":
            best_partner = max(
                sorted(obs.available_partners),
                key=lambda p: self.partner_reliability_mean(p),
            )
            max_uncertainty = max(
                self.uncertainty(obs, a) for a in self.config.interactions
            )
            if (
                max_uncertainty > .52
                and self.partner_reliability_mean(best_partner) >= .45
                and stable_unit_float(
                    "ask-gate",
                    self.seed,
                    obs.step,
                    best_partner,
                ) < .24
            ):
                action = ASK_PREFIX + best_partner
                self.last_action = action
                self.action_counts[action] += 1
                return action

        # Resource learning/exploitation is the default.
        best = self._best_resource_action(obs)
        best_expected = self.p_success(obs, best)

        # When the current zone appears poor and energy is not urgent, explore space.
        if (
            best_expected < .48
            and obs.energy_band in {"B2", "B3"}
            and stable_unit_float(
                "move-gate",
                self.seed,
                obs.step,
                obs.zone_token,
            ) < .24
        ):
            left_idx = (
                self.config.zones.index(obs.zone_token) - 1
            ) % len(self.config.zones)
            right_idx = (
                self.config.zones.index(obs.zone_token) + 1
            ) % len(self.config.zones)
            left_zone = self.config.zones[left_idx]
            right_zone = self.config.zones[right_idx]
            action = (
                MOVE_LEFT
                if self.zone_visits[left_zone] <= self.zone_visits[right_zone]
                else MOVE_RIGHT
            )
            self.last_action = action
            self.action_counts[action] += 1
            return action

        # Occasional rest preserves a non-resource baseline strategy.
        if (
            obs.energy_band == "B1"
            and best_expected < .40
            and stable_unit_float("rest-gate", self.seed, obs.step) < .12
        ):
            self.last_action = REST
            self.action_counts[REST] += 1
            return REST

        self.last_action = best
        self.action_counts[best] += 1
        return best

    def _memory_significance(
        self,
        obs: RichObservation,
        event: SubjectVisibleRichEvent,
    ) -> float:
        significance = 0.10
        significance += .35 * abs(event.energy_after - event.energy_before)
        significance += .45 * abs(event.integrity_after - event.integrity_before)
        significance += .20 * int(event.hazard)
        significance += .16 * int(event.help_received)
        significance += .12 * int(event.help_given)
        significance += .08 * int(event.hint_action is not None)
        significance += .14 * int(obs.energy_band == "B0")
        return min(1.0, significance)

    def observe_event(
        self,
        obs: RichObservation,
        event: SubjectVisibleRichEvent,
    ) -> None:
        # Resource causal learning.
        if event.action in self.config.interactions:
            key = self.context_key(obs, event.action)
            stat = self.resource_stats[key]
            p_before = stat.posterior_mean
            error_before = abs(event.resource_success - p_before)
            stat.observe(event.resource_success)
            p_after = stat.posterior_mean
            error_after = abs(event.resource_success - p_after)
            progress = max(0.0, error_before - error_after)
            self.learning_progress[key] = (
                .85 * self.learning_progress[key]
                + .15 * progress
            )

            # Consequence-grounded evaluation of a previously supplied hint.
            if self.pending_hint is not None:
                hint = self.pending_hint
                same_context = (
                    hint.zone_token == obs.zone_token
                    and hint.regime_cue == obs.regime_cue
                )
                if same_context and obs.step - hint.issued_step <= 5:
                    if event.action == hint.action:
                        self.partner_reliability[hint.source_id].observe(
                            event.resource_success
                        )
                        rel = self.relationships[hint.source_id]
                        if event.resource_success:
                            rel.add_evidence(SocialEvidence(
                                evidence_id=f"{event.event_id}-HINT-HELP",
                                observer=self.agent_id,
                                counterpart=hint.source_id,
                                event_type="HELP",
                                magnitude=.35,
                                reliability=.70,
                                timestamp=obs.step,
                            ))
                        else:
                            rel.add_evidence(SocialEvidence(
                                evidence_id=f"{event.event_id}-HINT-FAIL",
                                observer=self.agent_id,
                                counterpart=hint.source_id,
                                event_type="RUMOR_NEGATIVE",
                                magnitude=.30,
                                reliability=.55,
                                timestamp=obs.step,
                            ))
                    elif event.resource_success:
                        # Successful contrary action is weak evidence against hint.
                        self.partner_reliability[hint.source_id].observe(0)
                    self.pending_hint = None

        # A newly received hint is remembered with its context but not labeled true/false.
        if event.hint_action is not None and event.counterpart is not None:
            self.pending_hint = PendingHint(
                source_id=event.counterpart,
                action=event.hint_action,
                zone_token=obs.zone_token,
                regime_cue=obs.regime_cue,
                issued_step=obs.step,
            )

        # Social learning.
        if event.help_given and event.counterpart is not None:
            self.help_given_recently[event.counterpart] = obs.step

        if event.help_received and event.counterpart is not None:
            pid = event.counterpart
            recently_helped = (
                obs.step - self.help_given_recently.get(pid, -10_000)
                <= 80
            )
            self.partner_helpfulness[pid].observe(1 if recently_helped else 0)
            rel = self.relationships[pid]
            rel.add_evidence(SocialEvidence(
                evidence_id=f"{event.event_id}-HELP",
                observer=self.agent_id,
                counterpart=pid,
                event_type="HELP",
                magnitude=1.0,
                reliability=1.0,
                timestamp=obs.step,
            ))

        if event.hazard and event.counterpart is not None:
            # Only counterpart-linked hazard enters social evidence.
            rel = self.relationships[event.counterpart]
            rel.add_evidence(SocialEvidence(
                evidence_id=f"{event.event_id}-HARM",
                observer=self.agent_id,
                counterpart=event.counterpart,
                event_type="HARM",
                magnitude=.5,
                reliability=.5,
                timestamp=obs.step,
            ))

        significance = self._memory_significance(obs, event)
        exp = SubjectiveRichExperience(
            experience_id=event.event_id,
            step=obs.step,
            zone_token=obs.zone_token,
            regime_cue=obs.regime_cue,
            energy_band=obs.energy_band,
            integrity_band=obs.integrity_band,
            action=event.action,
            resource_success=event.resource_success,
            hazard=event.hazard,
            counterpart=event.counterpart,
            hint_action_received=event.hint_action,
            help_given=event.help_given,
            help_received=event.help_received,
            energy_delta=event.energy_after - event.energy_before,
            integrity_delta=event.integrity_after - event.integrity_before,
            significance=significance,
        )
        self.memories.append(exp)

        if significance >= .35:
            self.significant_memory_ids.append(exp.experience_id)

    def predicted_best_action(
        self,
        regime_cue: str,
        zone_token: str,
    ) -> str:
        class _Obs:
            pass
        obs = _Obs()
        obs.regime_cue = regime_cue
        obs.zone_token = zone_token
        return max(
            sorted(self.config.interactions),
            key=lambda a: self.resource_stats[
                (regime_cue, zone_token, a)
            ].posterior_mean,
        )

    def to_dict(self):
        return {
            "agent_id": self.agent_id,
            "profile_id": self.profile_id,
            "seed": self.seed,
            "resource_stats": {
                "|".join(k): v.to_dict()
                for k, v in sorted(self.resource_stats.items())
            },
            "learning_progress": {
                "|".join(k): v
                for k, v in sorted(self.learning_progress.items())
            },
            "partner_reliability": {
                k: v.to_dict()
                for k, v in sorted(self.partner_reliability.items())
            },
            "partner_helpfulness": {
                k: v.to_dict()
                for k, v in sorted(self.partner_helpfulness.items())
            },
            "relationships": {
                k: v.to_dict()
                for k, v in sorted(self.relationships.items())
            },
            "zone_visits": dict(sorted(self.zone_visits.items())),
            "action_counts": dict(sorted(self.action_counts.items())),
            "memory_count": len(self.memories),
            "significant_memory_ids": list(self.significant_memory_ids),
            "pending_hint": (
                self.pending_hint.to_dict()
                if self.pending_hint is not None
                else None
            ),
            "help_given_recently": dict(sorted(self.help_given_recently.items())),
        }


class ReactiveRichAgent:
    """Control: no durable causal or social learning."""

    def __init__(self, agent_id: str, config: RichWorldConfig):
        self.agent_id = agent_id
        self.config = config
        self.action_counts: Dict[str, int] = defaultdict(int)

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self):
        return canonical_hash(self.to_dict())

    def choose_action(self, obs: RichObservation) -> str:
        # Follows any visible hint blindly; otherwise fixed interaction.
        if obs.last_received_hint is not None:
            action = obs.last_received_hint[1]
        elif obs.help_request_from is not None and obs.energy_band == "B3":
            action = HELP_PREFIX + obs.help_request_from
        else:
            action = sorted(self.config.interactions)[0]
        self.action_counts[action] += 1
        return action

    def observe_event(self, obs: RichObservation, event: SubjectVisibleRichEvent) -> None:
        del obs, event

    def to_dict(self):
        return {
            "agent_id": self.agent_id,
            "type": "ReactiveRichAgent",
            "action_counts": dict(sorted(self.action_counts.items())),
        }


class MemoryOnlyRichAgent:
    """Control: remembers the last successful action per cue/zone but lacks
    uncertainty estimates, partner reliability learning, relationships, and
    developmental drive arbitration.
    """

    def __init__(self, agent_id: str, config: RichWorldConfig):
        self.agent_id = agent_id
        self.config = config
        self.last_success: Dict[tuple[str, str], str] = {}
        self.action_counts: Dict[str, int] = defaultdict(int)

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self):
        return canonical_hash(self.to_dict())

    def choose_action(self, obs: RichObservation) -> str:
        key = (obs.regime_cue, obs.zone_token)
        action = self.last_success.get(
            key,
            sorted(self.config.interactions)[0],
        )
        self.action_counts[action] += 1
        return action

    def observe_event(self, obs: RichObservation, event: SubjectVisibleRichEvent) -> None:
        if (
            event.action in self.config.interactions
            and event.resource_success
        ):
            self.last_success[(obs.regime_cue, obs.zone_token)] = event.action

    def to_dict(self):
        return {
            "agent_id": self.agent_id,
            "type": "MemoryOnlyRichAgent",
            "last_success": {
                "|".join(k): v for k, v in sorted(self.last_success.items())
            },
            "action_counts": dict(sorted(self.action_counts.items())),
        }


class OracleRichAgent:
    """Positive control supplied with the hidden causal answer.

    This agent is deliberately not a developmental model. It exists to separate
    successful behavior from learning-through-history.
    """

    def __init__(
        self,
        agent_id: str,
        config: RichWorldConfig,
        oracle_mapping: Dict[tuple[str, str], str],
    ):
        self.agent_id = agent_id
        self.config = config
        self.oracle_mapping = dict(oracle_mapping)
        self.action_counts: Dict[str, int] = defaultdict(int)

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self):
        return canonical_hash(self.to_dict())

    def choose_action(self, obs: RichObservation) -> str:
        action = self.oracle_mapping[(obs.regime_cue, obs.zone_token)]
        self.action_counts[action] += 1
        return action

    def observe_event(self, obs: RichObservation, event: SubjectVisibleRichEvent) -> None:
        del obs, event

    def to_dict(self):
        return {
            "agent_id": self.agent_id,
            "type": "OracleRichAgent",
            "oracle_mapping": {
                "|".join(k): v for k, v in sorted(self.oracle_mapping.items())
            },
            "action_counts": dict(sorted(self.action_counts.items())),
        }
