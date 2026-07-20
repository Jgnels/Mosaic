from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Dict
import statistics

from .core import stable_unit_float, canonical_hash
from .learners import BinomialEvidence


LOCATIONS = ("LX1", "LX2", "LX3")
PARTNERS = ("P1", "P2", "P3")


@dataclass(frozen=True)
class ToMEpisode:
    episode_id: str
    old_location: str
    new_location: str
    subject_saw_relocation: bool
    partner_access: Dict[str, bool]
    partner_actions: Dict[str, str]
    partner_messages: Dict[str, str]
    partner_internal_beliefs: Dict[str, str]  # evaluation-only hidden truth

    def subject_view(self) -> dict:
        return {
            "episode_id": self.episode_id,
            "old_location": self.old_location,
            "new_location_if_seen": (
                self.new_location
                if self.subject_saw_relocation
                else None
            ),
            "subject_saw_relocation": self.subject_saw_relocation,
            "partner_access": dict(self.partner_access),
            "partner_actions": dict(self.partner_actions),
            "partner_messages": dict(self.partner_messages),
        }


class LongitudinalToMWorld:
    """Controlled social-information world with private beliefs and unreliable speech."""

    def __init__(self, seed: int):
        self.seed = seed
        self.partner_honesty = {
            "P1": .90,
            "P2": .68,
            "P3": .42,
        }
        self.partner_update_fidelity = {
            "P1": .94,
            "P2": .86,
            "P3": .78,
        }

    def episode(self, index: int) -> ToMEpisode:
        old_idx = int(
            stable_unit_float(
                "tom-old",
                self.seed,
                index,
            ) * len(LOCATIONS)
        )
        old_location = LOCATIONS[
            min(old_idx, len(LOCATIONS)-1)
        ]

        alternatives = [
            x for x in LOCATIONS
            if x != old_location
        ]
        new_idx = int(
            stable_unit_float(
                "tom-new",
                self.seed,
                index,
            ) * len(alternatives)
        )
        new_location = alternatives[
            min(new_idx, len(alternatives)-1)
        ]

        subject_saw = stable_unit_float(
            "tom-subject-access",
            self.seed,
            index,
        ) < .62

        access = {}
        actions = {}
        messages = {}
        beliefs = {}

        for pid in PARTNERS:
            saw = stable_unit_float(
                "tom-partner-access",
                self.seed,
                index,
                pid,
            ) < {
                "P1": .70,
                "P2": .52,
                "P3": .38,
            }[pid]
            access[pid] = saw

            should_update = (
                saw
                and stable_unit_float(
                    "tom-update",
                    self.seed,
                    index,
                    pid,
                )
                < self.partner_update_fidelity[pid]
            )
            belief = (
                new_location
                if should_update
                else old_location
            )
            beliefs[pid] = belief

            # Action normally follows private belief with small execution noise.
            if stable_unit_float(
                "tom-action-noise",
                self.seed,
                index,
                pid,
            ) < .05:
                wrong = [
                    x for x in LOCATIONS
                    if x != belief
                ]
                actions[pid] = wrong[
                    int(stable_unit_float(
                        "tom-action-wrong",
                        self.seed,
                        index,
                        pid,
                    ) * len(wrong))
                    % len(wrong)
                ]
            else:
                actions[pid] = belief

            honest = stable_unit_float(
                "tom-honesty",
                self.seed,
                index,
                pid,
            ) < self.partner_honesty[pid]

            if honest:
                messages[pid] = belief
            else:
                wrong = [
                    x for x in LOCATIONS
                    if x != belief
                ]
                messages[pid] = wrong[
                    int(stable_unit_float(
                        "tom-lie",
                        self.seed,
                        index,
                        pid,
                    ) * len(wrong))
                    % len(wrong)
                ]

        return ToMEpisode(
            episode_id=f"TOM-{self.seed}-{index:05d}",
            old_location=old_location,
            new_location=new_location,
            subject_saw_relocation=subject_saw,
            partner_access=access,
            partner_actions=actions,
            partner_messages=messages,
            partner_internal_beliefs=beliefs,
        )


class AccessAwareToMLearner:
    """Learns access-conditioned belief updating and message reliability."""

    def __init__(self):
        self.update_when_access = {
            p: BinomialEvidence()
            for p in PARTNERS
        }
        self.update_without_access = {
            p: BinomialEvidence()
            for p in PARTNERS
        }
        self.action_fidelity = {
            p: BinomialEvidence()
            for p in PARTNERS
        }
        self.message_truth = {
            p: BinomialEvidence()
            for p in PARTNERS
        }

    def observe(self, episode: ToMEpisode):
        view = episode.subject_view()

        # Learning truth-dependent quantities is allowed only when the subject
        # itself saw the relocation.
        if not view["subject_saw_relocation"]:
            return

        truth = view["new_location_if_seen"]

        for pid in PARTNERS:
            access = view["partner_access"][pid]
            action = view["partner_actions"][pid]
            message = view["partner_messages"][pid]

            updated = int(action == truth)
            bucket = (
                self.update_when_access
                if access
                else self.update_without_access
            )
            bucket[pid].observe(updated)

            # Estimate how faithfully observed action reports private belief.
            # When partner lacked access, old location is the expected belief.
            expected_belief = (
                truth
                if access
                else view["old_location"]
            )
            self.action_fidelity[pid].observe(
                int(action == expected_belief)
            )

            self.message_truth[pid].observe(
                int(message == truth)
            )

    def predict_partner_action(
        self,
        partner_id: str,
        old_location: str,
        new_location: str,
        partner_had_access: bool,
    ) -> str:
        if partner_had_access:
            p_update = self.update_when_access[
                partner_id
            ].posterior_mean
        else:
            p_update = self.update_without_access[
                partner_id
            ].posterior_mean

        return (
            new_location
            if p_update >= .50
            else old_location
        )

    def choose_information_source(
        self,
        partner_access: Dict[str, bool],
    ) -> str:
        def score(pid):
            access_score = (
                self.update_when_access[pid].posterior_mean
                if partner_access[pid]
                else self.update_without_access[pid].posterior_mean
            )
            honesty = self.message_truth[
                pid
            ].posterior_mean
            return .60 * access_score + .40 * honesty

        return max(
            sorted(PARTNERS),
            key=score,
        )

    def state_hash(self):
        return canonical_hash({
            "update_when_access": {
                p: self.update_when_access[p].to_dict()
                for p in PARTNERS
            },
            "update_without_access": {
                p: self.update_without_access[p].to_dict()
                for p in PARTNERS
            },
            "action_fidelity": {
                p: self.action_fidelity[p].to_dict()
                for p in PARTNERS
            },
            "message_truth": {
                p: self.message_truth[p].to_dict()
                for p in PARTNERS
            },
        })


class FlatSocialLearner:
    """Control that learns global partner correctness but ignores information access."""

    def __init__(self):
        self.action_correct = {
            p: BinomialEvidence()
            for p in PARTNERS
        }
        self.message_correct = {
            p: BinomialEvidence()
            for p in PARTNERS
        }

    def observe(self, episode: ToMEpisode):
        view = episode.subject_view()
        if not view["subject_saw_relocation"]:
            return
        truth = view["new_location_if_seen"]

        for pid in PARTNERS:
            self.action_correct[pid].observe(
                int(
                    view["partner_actions"][pid]
                    == truth
                )
            )
            self.message_correct[pid].observe(
                int(
                    view["partner_messages"][pid]
                    == truth
                )
            )

    def predict_partner_action(
        self,
        partner_id,
        old_location,
        new_location,
        partner_had_access,
    ):
        del partner_had_access
        return (
            new_location
            if self.action_correct[
                partner_id
            ].posterior_mean >= .50
            else old_location
        )

    def choose_information_source(
        self,
        partner_access,
    ):
        del partner_access
        return max(
            sorted(PARTNERS),
            key=lambda p: self.message_correct[
                p
            ].posterior_mean,
        )


def run_longitudinal_tom(seed_count: int = 32) -> dict:
    access_prediction = []
    flat_prediction = []
    access_info_accuracy = []
    flat_info_accuracy = []

    for seed in range(1, seed_count + 1):
        world = LongitudinalToMWorld(seed)
        access_model = AccessAwareToMLearner()
        flat_model = FlatSocialLearner()

        # Developmental history.
        for i in range(400):
            ep = world.episode(i)
            access_model.observe(ep)
            flat_model.observe(ep)

        # Held-out evaluation.
        for i in range(400, 650):
            ep = world.episode(i)
            view = ep.subject_view()

            if view["subject_saw_relocation"]:
                truth = view["new_location_if_seen"]
                for pid in PARTNERS:
                    access_pred = access_model.predict_partner_action(
                        pid,
                        view["old_location"],
                        truth,
                        view["partner_access"][pid],
                    )
                    flat_pred = flat_model.predict_partner_action(
                        pid,
                        view["old_location"],
                        truth,
                        view["partner_access"][pid],
                    )

                    actual = view["partner_actions"][pid]
                    access_prediction.append(
                        1.0 if access_pred == actual else 0.0
                    )
                    flat_prediction.append(
                        1.0 if flat_pred == actual else 0.0
                    )

            else:
                # Subject lacks direct truth and must decide whose message to trust.
                access_source = access_model.choose_information_source(
                    view["partner_access"]
                )
                flat_source = flat_model.choose_information_source(
                    view["partner_access"]
                )

                access_info_accuracy.append(
                    1.0
                    if view["partner_messages"][access_source]
                    == ep.new_location
                    else 0.0
                )
                flat_info_accuracy.append(
                    1.0
                    if view["partner_messages"][flat_source]
                    == ep.new_location
                    else 0.0
                )

    return {
        "experiment_id": "SL-LONGITUDINAL-TOM-001",
        "seed_count": seed_count,
        "access_aware_partner_action_prediction_accuracy": statistics.mean(
            access_prediction
        ),
        "flat_partner_action_prediction_accuracy": statistics.mean(
            flat_prediction
        ),
        "access_aware_information_source_accuracy": statistics.mean(
            access_info_accuracy
        ),
        "flat_information_source_accuracy": statistics.mean(
            flat_info_accuracy
        ),
        "interpretation_warning": (
            "The learner acquires an information-access-conditioned model of other agents' "
            "belief-sensitive behavior. This is a Theory-of-Mind precursor, not evidence of "
            "human-like mental-state understanding."
        ),
    }
