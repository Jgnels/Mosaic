from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Dict, Iterable
import copy

from .core import DeterministicRng, stable_unit_float, canonical_hash


OPAQUE_ZONES = ("ZX1", "ZX2", "ZX3", "ZX4", "ZX5", "ZX6")
OPAQUE_INTERACTIONS = ("QA1", "QA2", "QA3", "QA4")
MOVE_LEFT = "MVL"
MOVE_RIGHT = "MVR"
OBSERVE = "OBS"
REST = "RST"
REPAIR = "RPR"
ASK_PREFIX = "ASK:"
HELP_PREFIX = "HLP:"


@dataclass(frozen=True)
class PartnerProfile:
    partner_id: str
    hint_reliability: float
    spontaneous_help_probability: float
    reciprocity_gain: float
    request_help_probability: float

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class RichWorldConfig:
    scenario_id: str
    zones: tuple[str, ...] = OPAQUE_ZONES
    interactions: tuple[str, ...] = OPAQUE_INTERACTIONS
    partner_ids: tuple[str, ...] = ("P1", "P2", "P3")
    steps: int = 1400
    regime_length: int = 350
    outcome_noise: float = .10
    movement_cost: float = .018
    interaction_cost: float = .022
    rest_recovery: float = .035
    repair_cost: float = .045
    repair_recovery: float = .12
    success_recovery: float = .18
    hazard_probability: float = .035
    hazard_cost: float = .14
    passive_need_growth: float = .016
    observation_cost: float = .008
    ask_cost: float = .010
    help_cost: float = .08
    received_help_recovery: float = .16
    partial_observability: bool = True
    social_enabled: bool = True
    regime_changes_enabled: bool = True

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class RichObservation:
    step: int
    zone_token: str
    regime_cue: str
    energy_band: str
    integrity_band: str
    local_signal: str
    available_partners: tuple[str, ...]
    help_request_from: str | None
    last_received_hint: tuple[str, str] | None

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class SubjectVisibleRichEvent:
    event_id: str
    step: int
    zone_token: str
    regime_cue: str
    action: str
    resource_success: int
    hazard: int
    energy_before: float
    energy_after: float
    integrity_before: float
    integrity_after: float
    counterpart: str | None
    hint_action: str | None
    help_given: bool
    help_received: bool

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class RichEvent:
    event_id: str
    step: int
    zone_token: str
    regime_cue: str
    action: str
    resource_success: int
    hazard: int
    energy_before: float
    energy_after: float
    integrity_before: float
    integrity_after: float
    counterpart: str | None
    hint_action: str | None
    hint_truthful: bool | None
    help_given: bool
    help_received: bool
    world_regime: int

    def to_dict(self):
        return asdict(self)


def subject_visible_event(event: RichEvent) -> SubjectVisibleRichEvent:
    """Strip hidden evaluation-only fields before cognition sees the event.

    Hidden:
    - whether a hint was objectively truthful;
    - the canonical hidden world regime.

    The agent must infer those indirectly from consequences.
    """
    return SubjectVisibleRichEvent(
        event_id=event.event_id,
        step=event.step,
        zone_token=event.zone_token,
        regime_cue=event.regime_cue,
        action=event.action,
        resource_success=event.resource_success,
        hazard=event.hazard,
        energy_before=event.energy_before,
        energy_after=event.energy_after,
        integrity_before=event.integrity_before,
        integrity_after=event.integrity_after,
        counterpart=event.counterpart,
        hint_action=event.hint_action,
        help_given=event.help_given,
        help_received=event.help_received,
    )


@dataclass
class RichWorldState:
    step: int = 0
    subject_zone_index: int = 0
    energy: float = .72
    integrity: float = .92
    last_received_hint: tuple[str, str] | None = None
    last_hint_source: str | None = None
    help_debt: Dict[str, float] | None = None

    def __post_init__(self):
        if self.help_debt is None:
            self.help_debt = {}

    def to_dict(self):
        return {
            "step": self.step,
            "subject_zone_index": self.subject_zone_index,
            "energy": self.energy,
            "integrity": self.integrity,
            "last_received_hint": self.last_received_hint,
            "last_hint_source": self.last_hint_source,
            "help_debt": dict(sorted(self.help_debt.items())),
        }


class ControlledRichWorld:
    """Deterministic, partially observable, socially contingent synthetic world.

    The subject never receives the hidden causal mapping or partner profiles.
    The environment is richer than the microbenchmarks while remaining exactly
    reproducible and forkable.
    """

    def __init__(
        self,
        seed: int,
        config: RichWorldConfig,
        partner_profiles: Dict[str, PartnerProfile] | None = None,
    ):
        self.seed = seed
        self.config = config
        self.state = RichWorldState(
            help_debt={pid: 0.0 for pid in config.partner_ids}
        )
        self.partner_profiles = partner_profiles or self._default_partner_profiles()
        self._mappings = self._build_regime_mappings()

    def _default_partner_profiles(self):
        defaults = (
            PartnerProfile("P1", .88, .006, .12, .022),
            PartnerProfile("P2", .62, .004, .08, .028),
            PartnerProfile("P3", .34, .003, .05, .034),
        )
        return {p.partner_id: p for p in defaults if p.partner_id in self.config.partner_ids}

    def _build_regime_mappings(self) -> Dict[int, Dict[str, str]]:
        mappings = {}
        max_regimes = max(1, (self.config.steps // self.config.regime_length) + 2)
        for regime in range(max_regimes):
            rng = DeterministicRng(self.seed ^ (regime * 0x9E3779B1))
            actions = list(self.config.interactions)
            mapping = {}
            for zone in self.config.zones:
                rng.shuffle(actions)
                mapping[zone] = actions[0]
            mappings[regime] = mapping
        return mappings

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self) -> str:
        return canonical_hash({
            "seed": self.seed,
            "config": self.config.to_dict(),
            "state": self.state.to_dict(),
            "partner_profiles": {
                k: v.to_dict() for k, v in sorted(self.partner_profiles.items())
            },
            "mappings": {
                str(r): dict(sorted(m.items()))
                for r, m in sorted(self._mappings.items())
            },
        })

    @property
    def current_zone(self) -> str:
        return self.config.zones[self.state.subject_zone_index]

    def current_regime(self) -> int:
        if not self.config.regime_changes_enabled:
            return 0
        return self.state.step // self.config.regime_length

    def regime_cue(self) -> str:
        # The cue changes with regime but carries no semantic label like "summer".
        return f"RC{self.current_regime() % 4}"

    def local_signal(self) -> str:
        # Stable opaque local sensory cue. It is informative but not sufficient.
        idx = int(stable_unit_float(
            "local-signal",
            self.seed,
            self.current_zone,
            self.current_regime(),
            self.state.step // 7,
        ) * 5)
        return f"LS{min(idx, 4)}"

    def available_partners(self) -> tuple[str, ...]:
        if not self.config.social_enabled:
            return ()
        available = []
        for pid in self.config.partner_ids:
            if stable_unit_float(
                "partner-available",
                self.seed,
                pid,
                self.state.step,
                self.current_zone,
            ) < .62:
                available.append(pid)
        return tuple(sorted(available))

    def help_request(self, available: Iterable[str]) -> str | None:
        if not self.config.social_enabled:
            return None
        for pid in sorted(available):
            profile = self.partner_profiles[pid]
            if stable_unit_float(
                "help-request",
                self.seed,
                pid,
                self.state.step,
            ) < profile.request_help_probability:
                return pid
        return None

    @staticmethod
    def _band(value: float) -> str:
        if value < .25:
            return "B0"
        if value < .50:
            return "B1"
        if value < .75:
            return "B2"
        return "B3"

    def observe(self) -> RichObservation:
        available = self.available_partners()
        return RichObservation(
            step=self.state.step,
            zone_token=self.current_zone,
            regime_cue=self.regime_cue(),
            energy_band=self._band(self.state.energy),
            integrity_band=self._band(self.state.integrity),
            local_signal=self.local_signal(),
            available_partners=available,
            help_request_from=self.help_request(available),
            last_received_hint=self.state.last_received_hint,
        )

    def oracle_mapping_by_cue(self) -> dict[tuple[str, str], str]:
        """Positive-control mapping for regimes actually reachable in this scenario.

        This must never be exposed to developmental agents.
        """
        mapping = {}
        max_regime = max(0, (self.config.steps - 1) // self.config.regime_length)
        if not self.config.regime_changes_enabled:
            max_regime = 0
        for regime in range(max_regime + 1):
            cue = f"RC{regime % 4}"
            for zone in self.config.zones:
                mapping[(cue, zone)] = self._mappings[regime][zone]
        return mapping

    def optimal_interaction(self, zone: str | None = None, regime: int | None = None) -> str:
        zone = zone or self.current_zone
        regime = self.current_regime() if regime is None else regime
        return self._mappings[regime][zone]

    def _partner_hint(self, partner_id: str) -> tuple[str, bool]:
        profile = self.partner_profiles[partner_id]
        truthful = stable_unit_float(
            "hint-truth",
            self.seed,
            partner_id,
            self.state.step,
            self.current_zone,
            self.current_regime(),
        ) < profile.hint_reliability

        correct = self.optimal_interaction()
        if truthful:
            return correct, True

        wrong = [a for a in self.config.interactions if a != correct]
        idx = int(stable_unit_float(
            "hint-wrong",
            self.seed,
            partner_id,
            self.state.step,
        ) * len(wrong))
        return wrong[min(idx, len(wrong) - 1)], False

    def _maybe_receive_help(self) -> tuple[bool, str | None]:
        if not self.config.social_enabled:
            return False, None

        # Help is contingent rather than a constant free resource. It becomes
        # more likely when the subject is actually struggling or has built
        # reciprocity with the partner.
        if self.state.energy >= .55 and self.state.integrity >= .60:
            return False, None

        for pid in self.config.partner_ids:
            profile = self.partner_profiles[pid]
            debt = self.state.help_debt.get(pid, 0.0)
            p = min(
                .85,
                profile.spontaneous_help_probability
                + profile.reciprocity_gain * debt,
            )
            if stable_unit_float(
                "receive-help",
                self.seed,
                pid,
                self.state.step,
            ) < p:
                # Helping repays some of the social debt.
                self.state.help_debt[pid] = max(0.0, debt - .35)
                return True, pid
        return False, None

    def step(self, action: str) -> RichEvent:
        obs = self.observe()
        energy_before = self.state.energy
        integrity_before = self.state.integrity

        # Ordinary passive need growth.
        self.state.energy = max(
            0.0,
            self.state.energy - self.config.passive_need_growth,
        )

        resource_success = 0
        hazard = 0
        counterpart = None
        hint_action = None
        hint_truthful = None
        help_given = False
        help_received = False

        if action == MOVE_LEFT:
            self.state.subject_zone_index = (
                self.state.subject_zone_index - 1
            ) % len(self.config.zones)
            self.state.energy = max(0.0, self.state.energy - self.config.movement_cost)

        elif action == MOVE_RIGHT:
            self.state.subject_zone_index = (
                self.state.subject_zone_index + 1
            ) % len(self.config.zones)
            self.state.energy = max(0.0, self.state.energy - self.config.movement_cost)

        elif action == OBSERVE:
            self.state.energy = max(0.0, self.state.energy - self.config.observation_cost)

        elif action == REST:
            self.state.energy = min(1.0, self.state.energy + self.config.rest_recovery)
            self.state.integrity = min(1.0, self.state.integrity + .004)

        elif action == REPAIR:
            self.state.energy = max(0.0, self.state.energy - self.config.repair_cost)
            self.state.integrity = min(1.0, self.state.integrity + self.config.repair_recovery)

        elif action in self.config.interactions:
            self.state.energy = max(0.0, self.state.energy - self.config.interaction_cost)
            optimal = self.optimal_interaction(
                zone=obs.zone_token,
                regime=self.current_regime(),
            )
            p_success = (
                1.0 - self.config.outcome_noise
                if action == optimal
                else self.config.outcome_noise
            )
            resource_success = int(
                stable_unit_float(
                    "rich-resource",
                    self.seed,
                    self.state.step,
                    obs.zone_token,
                    self.current_regime(),
                    action,
                ) < p_success
            )
            if resource_success:
                self.state.energy = min(
                    1.0,
                    self.state.energy + self.config.success_recovery,
                )

            hazard = int(
                stable_unit_float(
                    "rich-hazard",
                    self.seed,
                    self.state.step,
                    obs.zone_token,
                    action,
                ) < self.config.hazard_probability
            )
            if hazard:
                self.state.integrity = max(
                    0.0,
                    self.state.integrity - self.config.hazard_cost,
                )

        elif action.startswith(ASK_PREFIX):
            counterpart = action[len(ASK_PREFIX):]
            self.state.energy = max(0.0, self.state.energy - self.config.ask_cost)
            if counterpart in obs.available_partners:
                hint_action, hint_truthful = self._partner_hint(counterpart)
                self.state.last_received_hint = (counterpart, hint_action)
                self.state.last_hint_source = counterpart

        elif action.startswith(HELP_PREFIX):
            counterpart = action[len(HELP_PREFIX):]
            if counterpart == obs.help_request_from:
                help_given = True
                self.state.energy = max(0.0, self.state.energy - self.config.help_cost)
                self.state.help_debt[counterpart] = min(
                    1.0,
                    self.state.help_debt.get(counterpart, 0.0) + .45,
                )

        received, helper = self._maybe_receive_help()
        if received:
            help_received = True
            counterpart = counterpart or helper
            self.state.energy = min(
                1.0,
                self.state.energy + self.config.received_help_recovery,
            )

        # Low energy increases integrity risk slightly.
        if self.state.energy < .12:
            if stable_unit_float(
                "low-energy-integrity",
                self.seed,
                self.state.step,
            ) < .08:
                self.state.integrity = max(0.0, self.state.integrity - .035)

        event = RichEvent(
            event_id=f"RW-{self.seed}-{self.state.step:06d}",
            step=self.state.step,
            zone_token=obs.zone_token,
            regime_cue=obs.regime_cue,
            action=action,
            resource_success=resource_success,
            hazard=hazard,
            energy_before=energy_before,
            energy_after=self.state.energy,
            integrity_before=integrity_before,
            integrity_after=self.state.integrity,
            counterpart=counterpart,
            hint_action=hint_action,
            hint_truthful=hint_truthful,
            help_given=help_given,
            help_received=help_received,
            world_regime=self.current_regime(),
        )

        self.state.step += 1
        return event


def richness_configs() -> dict[str, RichWorldConfig]:
    return {
        "MICRO_PLUS": RichWorldConfig(
            scenario_id="MICRO_PLUS",
            zones=("ZX1", "ZX2"),
            interactions=("QA1", "QA2"),
            partner_ids=(),
            steps=600,
            regime_length=10000,
            outcome_noise=.06,
            hazard_probability=.00,
            partial_observability=False,
            social_enabled=False,
            regime_changes_enabled=False,
        ),
        "MESO": RichWorldConfig(
            scenario_id="MESO",
            zones=("ZX1", "ZX2", "ZX3", "ZX4"),
            interactions=("QA1", "QA2", "QA3"),
            partner_ids=("P1", "P2"),
            steps=1000,
            regime_length=500,
            outcome_noise=.08,
            hazard_probability=.02,
            social_enabled=True,
            regime_changes_enabled=True,
        ),
        "RICH": RichWorldConfig(
            scenario_id="RICH",
            zones=OPAQUE_ZONES,
            interactions=OPAQUE_INTERACTIONS,
            partner_ids=("P1", "P2", "P3"),
            steps=1400,
            regime_length=350,
            outcome_noise=.10,
            passive_need_growth=.021,
            interaction_cost=.027,
            success_recovery=.15,
            hazard_probability=.028,
            social_enabled=True,
            regime_changes_enabled=True,
        ),
    }
