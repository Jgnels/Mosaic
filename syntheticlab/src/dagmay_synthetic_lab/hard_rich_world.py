from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Dict
import copy

from .core import DeterministicRng, stable_unit_float, canonical_hash
from .rich_world import (
    RichWorldConfig,
    PartnerProfile,
    RichObservation,
    RichEvent,
    OPAQUE_ZONES,
    OPAQUE_INTERACTIONS,
    MOVE_LEFT,
    MOVE_RIGHT,
    OBSERVE,
    REST,
    REPAIR,
    ASK_PREFIX,
    HELP_PREFIX,
)


@dataclass
class HardRichState:
    step: int = 0
    subject_zone_index: int = 0
    energy: float = .72
    integrity: float = .92
    last_received_hint: tuple[str, str] | None = None
    last_hint_source: str | None = None
    help_debt: Dict[str, float] | None = None
    resource_stock: Dict[str, float] | None = None

    def __post_init__(self):
        if self.help_debt is None:
            self.help_debt = {}
        if self.resource_stock is None:
            self.resource_stock = {}

    def to_dict(self):
        return {
            "step": self.step,
            "subject_zone_index": self.subject_zone_index,
            "energy": self.energy,
            "integrity": self.integrity,
            "last_received_hint": self.last_received_hint,
            "last_hint_source": self.last_hint_source,
            "help_debt": dict(sorted(self.help_debt.items())),
            "resource_stock": dict(sorted(self.resource_stock.items())),
        }


@dataclass(frozen=True)
class HardRichConfig:
    scenario_id: str = "RICH_HARD"
    zones: tuple[str, ...] = OPAQUE_ZONES
    interactions: tuple[str, ...] = OPAQUE_INTERACTIONS
    partner_ids: tuple[str, ...] = ("P1", "P2", "P3")
    steps: int = 1800
    regime_length: int = 225
    outcome_noise: float = .11
    passive_need_growth: float = .014
    movement_cost: float = .014
    interaction_cost: float = .019
    observation_cost: float = .008
    ask_cost: float = .009
    help_cost: float = .08
    repair_cost: float = .040
    rest_recovery: float = .040
    repair_recovery: float = .14
    success_recovery: float = .19
    hazard_probability: float = .018
    hazard_cost: float = .09
    received_help_recovery: float = .15
    stock_regeneration: float = .012
    stock_depletion_on_success: float = .14
    regime_cue_cardinality: int = 2

    def to_dict(self):
        return asdict(self)


class NonstationaryRichWorld:
    """Hard controlled world with aliased cues and resource depletion.

    Compared with ControlledRichWorld:
    - four or more hidden regimes map onto only two observable regime cues;
    - old evidence can therefore become misleading;
    - resource success depletes local stock;
    - local signal is a noisy indicator of stock, not a direct truth value;
    - partner hint reliability drifts by hidden regime.

    This environment is designed to make simple accumulation-based learners
    fail gracefully rather than converge to trivial perfection.
    """

    def __init__(
        self,
        seed: int,
        config: HardRichConfig | None = None,
        partner_profiles: Dict[str, PartnerProfile] | None = None,
    ):
        self.seed = seed
        self.config = config or HardRichConfig()
        self.state = HardRichState(
            help_debt={pid: 0.0 for pid in self.config.partner_ids},
            resource_stock={
                zone: .55 + .35 * stable_unit_float(
                    "initial-stock", seed, zone
                )
                for zone in self.config.zones
            },
        )
        self.partner_profiles = partner_profiles or {
            "P1": PartnerProfile("P1", .82, .006, .12, .025),
            "P2": PartnerProfile("P2", .60, .004, .08, .030),
            "P3": PartnerProfile("P3", .38, .003, .05, .036),
        }
        self._mappings = self._build_mappings()

    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self):
        return canonical_hash({
            "seed": self.seed,
            "config": self.config.to_dict(),
            "state": self.state.to_dict(),
            "partner_profiles": {
                k: v.to_dict() for k, v in sorted(self.partner_profiles.items())
            },
            "mappings": {
                str(k): dict(sorted(v.items()))
                for k, v in sorted(self._mappings.items())
            },
        })

    def _build_mappings(self):
        max_regimes = (self.config.steps // self.config.regime_length) + 2
        out = {}
        for regime in range(max_regimes):
            rng = DeterministicRng(
                self.seed
                ^ (regime * 0xA24BAED4)
                ^ 0xC2B2AE35
            )
            actions = list(self.config.interactions)
            mapping = {}
            for zone in self.config.zones:
                rng.shuffle(actions)
                mapping[zone] = actions[0]
            out[regime] = mapping
        return out

    def _ensure_regime_mapping(self, regime: int):
        """Lazily extend deterministic hidden mappings beyond config.steps.

        Longitudinal developmental cohorts may intentionally continue past the
        original episode-length planning horizon. Mapping generation is a pure
        function of seed + regime, so extending the table cannot alter any
        earlier regime.
        """
        if regime in self._mappings:
            return

        rng = DeterministicRng(
            self.seed
            ^ (regime * 0xA24BAED4)
            ^ 0xC2B2AE35
        )
        actions = list(
            self.config.interactions
        )
        mapping = {}
        for zone in self.config.zones:
            rng.shuffle(actions)
            mapping[zone] = actions[0]

        self._mappings[regime] = mapping

    @property
    def current_zone(self):
        return self.config.zones[self.state.subject_zone_index]

    def current_regime(self):
        return self.state.step // self.config.regime_length

    def regime_cue(self):
        # Aliasing is intentional: regimes 0,2,4... share cues.
        base = self.current_regime() % self.config.regime_cue_cardinality

        # Small observation noise makes cue identity imperfect.
        if stable_unit_float(
            "hard-cue-noise",
            self.seed,
            self.state.step,
        ) < .06:
            base = (base + 1) % self.config.regime_cue_cardinality
        return f"RC{base}"

    @staticmethod
    def _band(value):
        if value < .25:
            return "B0"
        if value < .50:
            return "B1"
        if value < .75:
            return "B2"
        return "B3"

    def stock_signal(self):
        stock = self.state.resource_stock[self.current_zone]
        # The signal correlates with stock but is noisy and coarse.
        noisy = min(
            1.0,
            max(
                0.0,
                stock
                + (stable_unit_float(
                    "stock-noise",
                    self.seed,
                    self.state.step,
                    self.current_zone,
                ) - .5) * .28,
            ),
        )
        if noisy < .25:
            return "LS0"
        if noisy < .50:
            return "LS1"
        if noisy < .75:
            return "LS2"
        return "LS3"

    def available_partners(self):
        available = []
        for pid in self.config.partner_ids:
            if stable_unit_float(
                "hard-partner-available",
                self.seed,
                pid,
                self.state.step,
                self.current_zone,
            ) < .55:
                available.append(pid)
        return tuple(sorted(available))

    def help_request(self, available):
        for pid in sorted(available):
            profile = self.partner_profiles[pid]
            if stable_unit_float(
                "hard-help-request",
                self.seed,
                pid,
                self.state.step,
            ) < profile.request_help_probability:
                return pid
        return None

    def observe(self):
        available = self.available_partners()
        return RichObservation(
            step=self.state.step,
            zone_token=self.current_zone,
            regime_cue=self.regime_cue(),
            energy_band=self._band(self.state.energy),
            integrity_band=self._band(self.state.integrity),
            local_signal=self.stock_signal(),
            available_partners=available,
            help_request_from=self.help_request(available),
            last_received_hint=self.state.last_received_hint,
        )

    def optimal_interaction(self, zone=None, regime=None):
        zone = zone or self.current_zone
        regime = self.current_regime() if regime is None else regime
        self._ensure_regime_mapping(regime)
        return self._mappings[regime][zone]

    def optimal_interaction_at_step(self, step, zone):
        regime = step // self.config.regime_length
        self._ensure_regime_mapping(regime)
        return self._mappings[regime][zone]

    def dynamic_partner_reliability(self, partner_id):
        base = self.partner_profiles[partner_id].hint_reliability
        regime = self.current_regime()

        # Reliability changes over time but remains bounded.
        drift = {
            "P1": (.12 if regime % 3 == 0 else -.14),
            "P2": (.08 if regime % 2 == 0 else -.08),
            "P3": (.15 if regime % 4 == 3 else -.05),
        }.get(partner_id, 0.0)
        return min(.97, max(.03, base + drift))

    def partner_hint(self, partner_id):
        reliability = self.dynamic_partner_reliability(partner_id)
        truthful = stable_unit_float(
            "hard-hint-truth",
            self.seed,
            partner_id,
            self.state.step,
            self.current_zone,
            self.current_regime(),
        ) < reliability

        correct = self.optimal_interaction()
        if truthful:
            return correct, True

        wrong = [a for a in self.config.interactions if a != correct]
        idx = int(stable_unit_float(
            "hard-wrong-hint",
            self.seed,
            partner_id,
            self.state.step,
        ) * len(wrong))
        return wrong[min(idx, len(wrong)-1)], False

    def _regenerate_stock(self):
        for zone in self.config.zones:
            self.state.resource_stock[zone] = min(
                1.0,
                self.state.resource_stock[zone]
                + self.config.stock_regeneration,
            )

    def _maybe_receive_help(self):
        if self.state.energy >= .50 and self.state.integrity >= .55:
            return False, None

        for pid in self.config.partner_ids:
            profile = self.partner_profiles[pid]
            debt = self.state.help_debt.get(pid, 0.0)
            p = min(
                .50,
                profile.spontaneous_help_probability
                + profile.reciprocity_gain * debt,
            )
            if stable_unit_float(
                "hard-receive-help",
                self.seed,
                pid,
                self.state.step,
            ) < p:
                self.state.help_debt[pid] = max(0.0, debt - .35)
                return True, pid
        return False, None

    def step(self, action):
        obs = self.observe()
        energy_before = self.state.energy
        integrity_before = self.state.integrity

        self._regenerate_stock()
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
            self.state.energy = max(
                0.0,
                self.state.energy - self.config.movement_cost,
            )

        elif action == MOVE_RIGHT:
            self.state.subject_zone_index = (
                self.state.subject_zone_index + 1
            ) % len(self.config.zones)
            self.state.energy = max(
                0.0,
                self.state.energy - self.config.movement_cost,
            )

        elif action == OBSERVE:
            self.state.energy = max(
                0.0,
                self.state.energy - self.config.observation_cost,
            )

        elif action == REST:
            self.state.energy = min(
                1.0,
                self.state.energy + self.config.rest_recovery,
            )
            self.state.integrity = min(1.0, self.state.integrity + .004)

        elif action == REPAIR:
            self.state.energy = max(
                0.0,
                self.state.energy - self.config.repair_cost,
            )
            self.state.integrity = min(
                1.0,
                self.state.integrity + self.config.repair_recovery,
            )

        elif action in self.config.interactions:
            self.state.energy = max(
                0.0,
                self.state.energy - self.config.interaction_cost,
            )
            optimal = self.optimal_interaction(
                zone=obs.zone_token,
                regime=self.current_regime(),
            )
            stock = self.state.resource_stock[obs.zone_token]
            base_p = (
                1.0 - self.config.outcome_noise
                if action == optimal
                else self.config.outcome_noise
            )
            # Depleted zones become unreliable even when the action is correct.
            p_success = base_p * (.25 + .75 * stock)

            resource_success = int(stable_unit_float(
                "hard-resource",
                self.seed,
                self.state.step,
                obs.zone_token,
                self.current_regime(),
                action,
            ) < p_success)

            if resource_success:
                self.state.energy = min(
                    1.0,
                    self.state.energy + self.config.success_recovery,
                )
                self.state.resource_stock[obs.zone_token] = max(
                    0.0,
                    stock - self.config.stock_depletion_on_success,
                )

            hazard_p = self.config.hazard_probability * (
                1.35 if self.state.resource_stock[obs.zone_token] < .20 else 1.0
            )
            hazard = int(stable_unit_float(
                "hard-hazard",
                self.seed,
                self.state.step,
                obs.zone_token,
                action,
            ) < hazard_p)
            if hazard:
                self.state.integrity = max(
                    0.0,
                    self.state.integrity - self.config.hazard_cost,
                )

        elif action.startswith(ASK_PREFIX):
            counterpart = action[len(ASK_PREFIX):]
            self.state.energy = max(
                0.0,
                self.state.energy - self.config.ask_cost,
            )
            if counterpart in obs.available_partners:
                hint_action, hint_truthful = self.partner_hint(counterpart)
                self.state.last_received_hint = (
                    counterpart,
                    hint_action,
                )
                self.state.last_hint_source = counterpart

        elif action.startswith(HELP_PREFIX):
            counterpart = action[len(HELP_PREFIX):]
            if counterpart == obs.help_request_from:
                help_given = True
                self.state.energy = max(
                    0.0,
                    self.state.energy - self.config.help_cost,
                )
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

        if self.state.energy < .12:
            if stable_unit_float(
                "hard-low-energy-integrity",
                self.seed,
                self.state.step,
            ) < .10:
                self.state.integrity = max(
                    0.0,
                    self.state.integrity - .04,
                )

        event = RichEvent(
            event_id=f"HRW-{self.seed}-{self.state.step:06d}",
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
